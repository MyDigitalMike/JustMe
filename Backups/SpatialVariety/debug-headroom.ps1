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

$s=[TraversalGenerationSettings]::new();$s.Layout=[TraversalLayout]::Cross
$w=[ProceduralWorld]::GenerateTraversal($s,$caps,[StaminaSettings]::new(),42)
foreach($i in $w.Route.PrimaryLinks){$l=$w.Route.Links[$i];if($l.Action -notin @([TraversalAction]::Walk,[TraversalAction]::Sprint,[TraversalAction]::Rest)){continue};foreach($n in @($l.FromNode,$l.ToNode)){$node=$w.Route.Nodes[$n];$h=Headroom $w $node;if($h -lt 2.5){[pscustomobject]@{link=$i;node=$n;x=$node.FloorX;y=$node.FloorY;head=$h}|Format-List;foreach($other in $w.Route.Links){$b=$w.Route.Nodes[$other.ToNode];if([Math]::Abs($b.FloorX-$node.FloorX) -lt 5 -and [Math]::Abs($b.FloorY-$node.FloorY) -lt 10){[pscustomobject]@{action=$other.Action;from=$other.FromNode;to=$other.ToNode;x=$b.FloorX;y=$b.FloorY;branch=$other.BranchId}|Format-Table}}}}}
