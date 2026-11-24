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

$maps=@()
foreach($name in @('Horizontal','Ascent','Descent','U','Switchback','Cross')){
 $settings=[TraversalGenerationSettings]::new();$settings.Layout=[TraversalLayout]$name
 $w=[ProceduralWorld]::GenerateTraversal($settings,$caps,[StaminaSettings]::new(),0)
 $maps+=@{name=$name;nodes=$w.Route.Nodes;links=@($w.Route.PrimaryLinks|ForEach-Object {$w.Route.Links[$_]});pages=$w.FragmentNodes;cellSize=$w.CellSize}
}
$maps|ConvertTo-Json -Depth 10|Set-Content -LiteralPath 'Backups/WorldTopology/map-preview.json'
