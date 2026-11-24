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
$build=[HardcoreWorldGenerator].GetMethod('Build',[Reflection.BindingFlags]'NonPublic,Static')
$w=$build.Invoke($null,@($settings,$caps,[int]42,[int]3));[void][DiaryPlanner]::Attach($w,3)
$result=[TraversalValidator]::Validate($w,$caps,[StaminaSettings]::new());$result|Format-List
foreach($i in 10..19){$l=$w.Route.Links[$i];$a=$w.Route.Nodes[$l.FromNode];$b=$w.Route.Nodes[$l.ToNode];[pscustomobject]@{i=$i;action=$l.Action;ax=$a.FloorX;ay=$a.FloorY;bx=$b.FloorX;by=$b.FloorY}}
