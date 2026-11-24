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

$settings=[TraversalGenerationSettings]::new();$settings.Layout=[TraversalLayout]::Cross
$world=[ProceduralWorld]::GenerateTraversal($settings,$caps,[StaminaSettings]::new(),0)
foreach($i in 58..64){$link=$world.Route.Links[$i];$a=$world.Route.Nodes[$link.FromNode];$b=$world.Route.Nodes[$link.ToNode];[pscustomobject]@{index=$i;action=$link.Action;from=$link.FromNode;to=$link.ToNode;ax=$a.FloorX;ay=$a.FloorY;bx=$b.FloorX;by=$b.FloorY;shaft=$link.ClimbX}}
$world|ConvertTo-Json -Depth 12|Set-Content -LiteralPath 'Backups/WorldTopology/cross-debug.json'
