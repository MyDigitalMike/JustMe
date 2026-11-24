param([string]$ReportPath='Backups/OpenVoid/pure.json')
$ErrorActionPreference='Stop'
$rootPath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$names=@('Assets/Script/Game/TraversalStamina.cs','Assets/Script/GeneracionMundo/ProceduralWorld.cs','Assets/Script/GeneracionMundo/TraversalWorld.cs','Assets/Script/GeneracionMundo/TraversalValidator.cs','Assets/Script/GeneracionMundo/DiaryPlanner.cs','Assets/Script/GeneracionMundo/TraversalTopology.cs')
Add-Type -Path @($names|ForEach-Object {Join-Path $rootPath $_})
$raw=Get-Content -LiteralPath (Join-Path $rootPath 'Assets/Resources/TraversalCapabilities.json') -Raw|ConvertFrom-Json
$caps=[TraversalCapabilities]::new()
foreach($p in $raw.PSObject.Properties){if($p.Name -like '*Samples'){$caps.($p.Name)=@($p.Value|ForEach-Object {[TrajectorySample]::new($_.Time,$_.X,$_.Y)})}else{$caps.($p.Name)=$p.Value}}
$records=@()
foreach($seed in @(2,11,42)){
 $s=[TraversalGenerationSettings]::new();$s.Layout=[TraversalLayout]::Random;$s.RequiredFragments=12
 $w=[ProceduralWorld]::GenerateTraversal($s,$caps,[StaminaSettings]::new(),$seed)
 $width=$w.Tiles.GetLength(0);$height=$w.Tiles.GetLength(1)
 for($x=0;$x -lt $width;$x++){if($w.Tiles[$x,0] -ne 0 -or $w.Tiles[$x,($height-1)] -ne 0){throw "Outer horizontal wall remains: seed=$seed"}}
 for($y=0;$y -lt $height;$y++){if($w.Tiles[0,$y] -ne 0 -or $w.Tiles[($width-1),$y] -ne 0){throw "Outer side wall remains: seed=$seed"}}
 if(@($w.VoidRegions|Where-Object {$_.MinX -eq 0}).Count -eq 0 -or @($w.VoidRegions|Where-Object {$_.MaxX -eq $width-1}).Count -eq 0){throw "Unmarked exterior opening: seed=$seed"}
 $open=0
 foreach($gap in $w.VoidRegions){for($x=$gap.MinX;$x -le $gap.MaxX;$x++){$clear=$true;for($y=0;$y -lt $gap.MinY;$y++){if($w.Tiles[$x,$y] -ne 0){$clear=$false;break}};if($clear){$open++;break}}}
 if($open -lt 1){throw "No lethal gap opens into the exterior: seed=$seed"}
 $validation=[TraversalValidator]::Validate($w,$caps,[StaminaSettings]::new())
 if(!$validation.Valid -or $validation.FragmentsReached -ne 12){throw "Required route broken: $seed / $($validation.Reason)"}
 $records+=@{seed=$seed;openPits=$open;fragments=$validation.FragmentsReached;remainingStamina=$validation.RemainingStamina}
}
@{passed=$true;cases=$records}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $rootPath $ReportPath)
[pscustomobject]@{passed=$true;worlds=$records.Count;openPits=($records.openPits|Measure-Object -Sum).Sum}
