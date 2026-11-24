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

$s=[TraversalGenerationSettings]::new();$s.Layout=[TraversalLayout]::Random;$build=[HardcoreWorldGenerator].GetMethod('Build',[Reflection.BindingFlags]'NonPublic,Static')
foreach($attempt in 0..3){$w=$build.Invoke($null,@($s,$caps,[int]24,[int]$attempt));[void][DiaryPlanner]::Attach($w,12);$left=0;$right=0;$up=0;$down=0;foreach($i in $w.Route.PrimaryLinks){$l=$w.Route.Links[$i];$a=$w.Route.Nodes[$l.FromNode];$b=$w.Route.Nodes[$l.ToNode];if($l.Action -eq [TraversalAction]::Walk){$left+=[Math]::Max(0,$a.FloorX-$b.FloorX);$right+=[Math]::Max(0,$b.FloorX-$a.FloorX)};$up+=[Math]::Max(0,$b.FloorY-$a.FloorY);$down+=[Math]::Max(0,$a.FloorY-$b.FloorY)};$v=[TraversalValidator]::Validate($w,$caps,[StaminaSettings]::new());if($v.FailedLink -ge 0){foreach($j in ([Math]::Max(0,$v.FailedLink-1))..([Math]::Min($w.Route.Links.Length-1,$v.FailedLink+1))){$q=$w.Route.Links[$j];$qa=$w.Route.Nodes[$q.FromNode];$qb=$w.Route.Nodes[$q.ToNode];Write-Output "$j $($q.Action) [$($qa.FloorX),$($qa.FloorY)] -> [$($qb.FloorX),$($qb.FloorY)]"}};[pscustomobject]@{attempt=$attempt;left=$left;right=$right;up=$up;down=$down;reason=([TraversalValidator]::Validate($w,$caps,[StaminaSettings]::new())).Reason}}

