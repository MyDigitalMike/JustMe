param([string]$ReportPath='Backups/WorldTopology/pure.json')
$ErrorActionPreference='Stop'
$rootPath=$PWD.Path
$names=@('Assets/Script/Game/TraversalStamina.cs','Assets/Script/GeneracionMundo/ProceduralWorld.cs','Assets/Script/GeneracionMundo/TraversalWorld.cs','Assets/Script/GeneracionMundo/TraversalValidator.cs','Assets/Script/Game/DiaryProgress.cs','Assets/Script/GeneracionMundo/DiaryPlanner.cs','Assets/Script/GeneracionMundo/WorldRunSession.cs')
$planner=Join-Path $rootPath 'Assets/Script/GeneracionMundo/TraversalTopology.cs'
if(Test-Path $planner){$names+='Assets/Script/GeneracionMundo/TraversalTopology.cs'}
Add-Type -Path @($names|ForEach-Object {Join-Path $rootPath $_})
$raw=Get-Content -LiteralPath (Join-Path $rootPath 'Assets/Resources/TraversalCapabilities.json') -Raw|ConvertFrom-Json
$caps=[TraversalCapabilities]::new()
foreach($prop in $raw.PSObject.Properties){if($prop.Name -like '*Samples'){$caps.($prop.Name)=@($prop.Value|ForEach-Object {[TrajectorySample]::new($_.Time,$_.X,$_.Y)})}else{$caps.($prop.Name)=$prop.Value}}
function Headroom($world,$node){for($y=$node.FloorY+1;$y -lt $world.Tiles.GetLength(1);$y++){if($world.Tiles[$node.FloorX,$y] -eq 1){return ($y-$node.FloorY-1)*$world.CellSize}};return 999}

$records=@();$failures=@()
foreach($seed in 0..31){
 $s=[TraversalGenerationSettings]::new();$s.Layout=[TraversalLayout]::Random;$s.RequiredFragments=12
 try{$w=[ProceduralWorld]::GenerateTraversal($s,$caps,[StaminaSettings]::new(),$seed);$back=0;$fwd=0;foreach($i in $w.Route.PrimaryLinks){$l=$w.Route.Links[$i];$dx=$w.Route.Nodes[$l.ToNode].FloorX-$w.Route.Nodes[$l.FromNode].FloorX;if($dx -gt 0){$fwd++};if($dx -lt 0){$back++}};$records+=@{seed=$seed;profile=$w.Route.Profile.ToString();width=$w.Tiles.GetLength(0);height=$w.Tiles.GetLength(1);left=$back;right=$fwd}}
 catch{$failures+=@{seed=$seed;reason=$_.Exception.Message}}
}
@{cases=$records;failures=$failures}|ConvertTo-Json -Depth 6|Set-Content Backups/SpatialVariety/exploration-seeds.json
[pscustomobject]@{accepted=$records.Count;failures=$failures.Count;reversing=@($records|Where-Object {$_.left -gt 0 -and $_.right -gt 0}).Count;profiles=($records.profile|Select-Object -Unique)-join ','};$failures|Format-Table -Wrap
