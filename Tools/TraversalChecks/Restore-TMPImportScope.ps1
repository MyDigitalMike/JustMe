param([switch]$ResumeRestore)
$ErrorActionPreference='Stop'
function Write-Bytes([string]$Path,[byte[]]$Bytes){
 $mode=if(Test-Path -LiteralPath $Path){[IO.FileMode]::Open}else{[IO.FileMode]::CreateNew}
 $stream=[IO.FileStream]::new($Path,$mode,[IO.FileAccess]::Write,[IO.FileShare]::Read)
 try{$stream.SetLength(0);$stream.Write($Bytes,0,$Bytes.Length)}finally{$stream.Dispose()}
}
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$assetRoot=Join-Path $workspace 'Assets/TextMesh Pro'
$backup=Join-Path $workspace 'Backups/Phase3/TMP'
$before=Get-Content (Join-Path $backup 'before-checksums.json') -Raw|ConvertFrom-Json
$allowed=Get-Content (Join-Path $workspace 'Backups/Phase3/tmp-resource-inventory.json') -Raw|ConvertFrom-Json
$reference=Join-Path $workspace 'Temp/JustMePhase3/TMPReference/Package'
$quarantine=Join-Path $backup 'UnexpectedImport'
if((Test-Path -LiteralPath $quarantine) -and !$ResumeRestore){throw 'Recovery already started; inspect its verified state before resuming.'}
if(!$ResumeRestore){
New-Item -ItemType Directory -Path $quarantine|Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($assetRoot,(Join-Path $quarantine 'unexpected-native-import.zip'),[IO.Compression.CompressionLevel]::Optimal,$false)
$settingsPath=Join-Path $assetRoot 'Resources/TMP Settings.asset'
$migrated=[IO.File]::ReadAllBytes($settingsPath)
[IO.File]::WriteAllBytes((Join-Path $quarantine 'TMP Settings.native-migration.asset'),$migrated)
$old=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($entry in $before){[void]$old.Add($entry.path)}
$newFiles=0
foreach($file in Get-ChildItem -LiteralPath $assetRoot -Recurse -Force -File){
 $relative=$file.FullName.Substring($assetRoot.Length+1).Replace('\','/')
 if(!$old.Contains($relative)){
  $target=[IO.Path]::GetFullPath((Join-Path (Join-Path $quarantine 'NewFiles') $relative))
  if(!$file.FullName.StartsWith($assetRoot+[IO.Path]::DirectorySeparatorChar) -or !$target.StartsWith($workspace+[IO.Path]::DirectorySeparatorChar)){throw 'Unsafe recovery path'}
  New-Item -ItemType Directory -Force -Path (Split-Path $target)|Out-Null
  Move-Item -LiteralPath $file.FullName -Destination $target
  $newFiles++
 }
}
# Only empty directories introduced by this import are moved. Original folders stay intact.
foreach($directory in Get-ChildItem -LiteralPath $assetRoot -Recurse -Force -Directory|Sort-Object {$_.FullName.Length} -Descending){
 $relative=$directory.FullName.Substring($assetRoot.Length+1).Replace('\','/')
 if(!$old.Contains($relative+'.meta') -and @(Get-ChildItem -LiteralPath $directory.FullName -Force).Count -eq 0){
  $target=[IO.Path]::GetFullPath((Join-Path (Join-Path $quarantine 'EmptyFolders') $relative))
  if(!$directory.FullName.StartsWith($assetRoot+[IO.Path]::DirectorySeparatorChar) -or !$target.StartsWith($workspace+[IO.Path]::DirectorySeparatorChar)){throw 'Unsafe folder recovery path'}
  New-Item -ItemType Directory -Force -Path (Split-Path $target)|Out-Null
  Move-Item -LiteralPath $directory.FullName -Destination $target
 }
}
}else{
 if(!(Test-Path -LiteralPath (Join-Path $quarantine 'unexpected-native-import.zip'))){throw 'Missing recoverable native import'}
 $migrated=[IO.File]::ReadAllBytes((Join-Path $quarantine 'TMP Settings.native-migration.asset'))
 $newFiles=@(Get-ChildItem -LiteralPath (Join-Path $quarantine 'NewFiles') -Recurse -Force -File).Count
}
$archive=[IO.Compression.ZipFile]::OpenRead((Join-Path $backup 'before-essential-repair.zip'))
try{
 foreach($entry in $archive.Entries){
  $target=[IO.Path]::GetFullPath((Join-Path $assetRoot $entry.FullName))
  if(!$target.StartsWith($assetRoot+[IO.Path]::DirectorySeparatorChar)){throw 'Unsafe archive entry'}
  New-Item -ItemType Directory -Force -Path (Split-Path $target)|Out-Null
  $inputStream=$entry.Open();$memory=[IO.MemoryStream]::new()
  try{$inputStream.CopyTo($memory);Write-Bytes $target $memory.ToArray()}finally{$inputStream.Dispose();$memory.Dispose()}
 }
}finally{$archive.Dispose()}
$applied=[Collections.Generic.List[object]]::new()
foreach($entry in $allowed){
 $target=if($entry.existingGuidPath){$entry.existingGuidPath}else{Join-Path $workspace $entry.path}
 if($entry.path.StartsWith('Assets/TextMesh Pro/Shaders/')){$target=Join-Path $assetRoot ('Resources/Shaders/'+[IO.Path]::GetFileName($entry.path))}
 $target=[IO.Path]::GetFullPath($target)
 if(!$target.StartsWith($assetRoot+[IO.Path]::DirectorySeparatorChar)){throw 'Official resource outside approved root'}
 $payload=Join-Path $reference $entry.packageFolder
 if($entry.path -eq 'Assets/TextMesh Pro/Resources/TMP Settings.asset'){
  Write-Bytes $target $migrated
 }else{
  New-Item -ItemType Directory -Force -Path (Split-Path $target)|Out-Null
  Write-Bytes $target ([IO.File]::ReadAllBytes((Join-Path $payload 'asset')))
 }
 $meta=$target+'.meta'
 if(Test-Path -LiteralPath $meta){
  $guid=[regex]::Match([IO.File]::ReadAllText($meta),'(?m)^guid: ([a-f0-9]+)').Groups[1].Value
  if($guid -ne $entry.guid){throw "GUID mismatch: $target"}
 }else{Write-Bytes $meta ([IO.File]::ReadAllBytes((Join-Path $payload 'asset.meta')))}
 $applied.Add([pscustomobject]@{path=$target;guid=$entry.guid;sha256=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash})
}
$protected=0
foreach($entry in $before){if($entry.path.StartsWith('Examples & Extras/')){
 $target=Join-Path $assetRoot $entry.path
 if(!(Test-Path -LiteralPath $target) -or (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $entry.sha256){throw "Custom resource changed: $($entry.path)"}
 $protected++
}}
foreach($name in @('Scripts','Scenes','Prefabs')){if(Test-Path -LiteralPath (Join-Path $assetRoot ('Examples & Extras/'+$name))){throw "Examples directory unexpectedly retained: $name"}}
$applied|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $backup 'applied-resources.json')
[pscustomobject]@{quarantinedNewFiles=$newFiles;protectedCustomFiles=$protected;officialFiles=$applied.Count;examplesRestoredToExcludedState=$true}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $backup 'repair-result.json')
Get-Content (Join-Path $backup 'repair-result.json')
