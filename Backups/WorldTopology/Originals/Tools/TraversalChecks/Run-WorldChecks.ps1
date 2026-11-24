param([int]$Seed=1,[string]$Profile='Horizontal')
$ErrorActionPreference='Stop'
$rootPath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$sources=@('Assets/Script/Game/TraversalStamina.cs','Assets/Script/GeneracionMundo/ProceduralWorld.cs','Assets/Script/GeneracionMundo/TraversalWorld.cs','Assets/Script/GeneracionMundo/TraversalValidator.cs','Assets/Script/GeneracionMundo/DiaryPlanner.cs')|ForEach-Object{Join-Path $rootPath $_}
Add-Type -Path $sources
$raw=Get-Content (Join-Path $rootPath 'Assets/Resources/TraversalCapabilities.json') -Raw|ConvertFrom-Json
$caps=[TraversalCapabilities]::new()
foreach($p in $raw.PSObject.Properties){if($p.Name -like '*Samples'){ $caps.($p.Name)=@($p.Value|ForEach-Object{[TrajectorySample]::new($_.Time,$_.X,$_.Y)})}else{$caps.($p.Name)=$p.Value}}
$settings=[TraversalGenerationSettings]::new()
$settings.Profile=[WorldProfile]::$Profile
if($Profile -eq 'Ascent' -or $Profile -eq 'Descent'){$settings.Terrain.Width=56;$settings.Terrain.Height=96;$settings.Terrain.SurfaceHeight=88}
try {
 $world=[ProceduralWorld]::GenerateTraversal($settings,$caps,[StaminaSettings]::new(),$Seed)
 $result=[TraversalValidator]::Validate($world,$caps,[StaminaSettings]::new())
 [pscustomobject]@{seed=$world.Seed;profile=$Profile;links=$world.Route.Links.Length;valid=$result.Valid;seconds=$result.TotalSeconds;stamina=$result.RemainingStamina}
} catch { Write-Output $_.Exception.Message; exit 1 }
