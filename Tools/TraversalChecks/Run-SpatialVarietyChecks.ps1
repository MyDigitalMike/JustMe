param([string]$ReportPath='Backups/SpatialVariety/spatial-checks.json')
$ErrorActionPreference='Stop'
$rootPath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$names=@('Assets/Script/Game/TraversalStamina.cs','Assets/Script/GeneracionMundo/ProceduralWorld.cs','Assets/Script/GeneracionMundo/TraversalWorld.cs','Assets/Script/GeneracionMundo/TraversalValidator.cs','Assets/Script/Game/DiaryProgress.cs','Assets/Script/GeneracionMundo/DiaryPlanner.cs','Assets/Script/GeneracionMundo/WorldRunSession.cs')
$planner=Join-Path $rootPath 'Assets/Script/GeneracionMundo/TraversalTopology.cs'
if(Test-Path $planner){$names+='Assets/Script/GeneracionMundo/TraversalTopology.cs'}
$names+='Tools/TraversalChecks/SpatialRunProbe.cs'
Add-Type -Path @($names|ForEach-Object {Join-Path $rootPath $_})
$raw=Get-Content -LiteralPath (Join-Path $rootPath 'Assets/Resources/TraversalCapabilities.json') -Raw|ConvertFrom-Json
$caps=[TraversalCapabilities]::new()
foreach($prop in $raw.PSObject.Properties){if($prop.Name -like '*Samples'){$caps.($prop.Name)=@($prop.Value|ForEach-Object {[TrajectorySample]::new($_.Time,$_.X,$_.Y)})}else{$caps.($prop.Name)=$prop.Value}}
function Headroom($world,$node){for($y=$node.FloorY+1;$y -lt $world.Tiles.GetLength(1);$y++){if($world.Tiles[$node.FloorX,$y] -eq 1){return ($y-$node.FloorY-1)*$world.CellSize}};return 999}
$settings=[TraversalGenerationSettings]::new();$settings.Layout=[TraversalLayout]::Horizontal
$records=@();$countPatterns=[Collections.Generic.HashSet[string]]::new()
foreach($seed in @(0,1,42)){
 $w=[ProceduralWorld]::GenerateTraversal($settings,$caps,[StaminaSettings]::new(),$seed)
 $levels=@($w.Route.PrimaryLinks|ForEach-Object {$w.Route.Nodes[$w.Route.Links[$_].ToNode].FloorY}|Select-Object -Unique)
 if($levels.Count -lt 2){throw 'Main route is still a flat stamped ribbon'}
 $counts=@{}
 foreach($kind in @('Jump','DoubleJump','Crawl')){$counts[$kind]=@($w.Route.PrimaryLinks|Where-Object {$w.Route.Links[$_].Action.ToString() -eq $kind}).Count}
 [void]$countPatterns.Add("$($counts.Jump),$($counts.DoubleJump),$($counts.Crawl)")
 $records+=@{seed=$seed;counts=$counts}
}
if($countPatterns.Count -lt 2){throw 'Every seed still mandates the same repeated challenge-count recipe'}
$roomCounts=@()
foreach($layout in @('Ascent','Descent','U','Switchback','Cross','Random')){
 foreach($seed in @(0,1,42)){
  $s=[TraversalGenerationSettings]::new();$s.Layout=[TraversalLayout]$layout;$s.RequiredFragments=12
  try{$w=[ProceduralWorld]::GenerateTraversal($s,$caps,[StaminaSettings]::new(),$seed)}catch{throw "$layout/$seed : $_"}
  $bank=0;$largest=0;$regular=0;$irregular=0
  foreach($index in $w.Route.PrimaryLinks){
   $link=$w.Route.Links[$index]
   if($link.Action -eq [TraversalAction]::Climb){$bank++;$largest=[Math]::Max($largest,$bank)}
   elseif($link.Action -ne [TraversalAction]::Rest){$bank=0}
   if($link.Action -eq [TraversalAction]::Walk -and [Math]::Abs($w.Route.Nodes[$link.ToNode].FloorX-$w.Route.Nodes[$link.FromNode].FloorX) -ge 4){
    if($link.ChamberHeight -gt 0){$regular++}else{$irregular++}
   }
  }
  if($largest -gt 4){throw "Repeated stair bank has $largest climbs without a chamber: $layout/$seed"}
  if($regular -eq 0 -or $irregular -eq 0){throw "Missing regular/irregular chamber mix: $layout/$seed"}
  $branch=[DiaryPlanner]::ExplorationLinks($w.Route)
  if($w.Route.Links[$branch[0]].FromNode -eq 0){throw "Required dead end still stamped at Spawn: $layout/$seed"}
  $ids=@($w.Route.Links|Where-Object {!$_.Primary -and $_.BranchId -ge 2}|ForEach-Object {$_.BranchId}|Select-Object -Unique)
  if($ids.Count -lt 2){throw "Missing secondary false path: $layout/$seed"}
  $terminal=$w.ExplorationNode;$pages=$w.FragmentNodes.Clone()
  $interior=$w.Route.Links[$branch[0]].ToNode;$w.ExplorationNode=$interior;$w.FragmentNodes[0]=$interior
  if(([TraversalValidator]::Validate($w,$caps,[StaminaSettings]::new())).Valid){throw 'Intermediate branch landing was accepted as a dead end'}
  $w.ExplorationNode=$terminal;$w.FragmentNodes=$pages
  $roomCounts+=@{layout=$layout;seed=$seed;largestBank=$largest;regular=$regular;irregular=$irregular;branches=$ids.Count}
 }
}
function Ceiling-Probe([TraversalAction]$action){
 $w=[WorldData]::new();$w.Seed=17;$w.CellSize=.5;$w.Tiles=[int[,]]::new(60,34);$w.Surface=[int[]]::new(60)
 for($x=0;$x -lt 60;$x++){for($y=0;$y -lt 34;$y++){$w.Tiles[$x,$y]=1}}
 $w.Route=[TraversalPlan]::new();$w.Route.Nodes=@([TraversalNode]::new(12,8),[TraversalNode]::new(17,8))
 $link=[TraversalLink]::new();$link.FromNode=0;$link.ToNode=1;$link.Primary=$false;$link.Action=$action
 $w.Route.Links=@($link);$w.Route.PrimaryLinks=@()
 $method=[HardcoreWorldGenerator].GetMethod('Rasterize',[Reflection.BindingFlags]'NonPublic,Static')
 [void]$method.Invoke($null,@($w,$caps,[double]2.5))
 $roof=@();for($x=12;$x -le 17;$x++){for($y=9;$y -lt 34;$y++){if($w.Tiles[$x,$y] -eq 1){$roof+=$y;break}}}
 return $roof -join ','
}
$exploration=@();$rejected=@()
foreach($seed in 0..31){
 $s=[TraversalGenerationSettings]::new();$s.Layout=[TraversalLayout]::Random;$s.RequiredFragments=12
 try{$w=[ProceduralWorld]::GenerateTraversal($s,$caps,[StaminaSettings]::new(),$seed)}catch{
  if($_.Exception.InnerException -isnot [TraversalGenerationException]){throw}
  $rejected+=@{seed=$seed;reason=$_.Exception.InnerException.Message};continue
 }
 if($w.Route.Profile -ne [WorldProfile]::Mixed){throw "Normal play still selects a rejected linear profile: seed=$seed"}
 $left=0;$right=0;$up=0;$down=0
 foreach($index in $w.Route.PrimaryLinks){
  $link=$w.Route.Links[$index];$a=$w.Route.Nodes[$link.FromNode];$b=$w.Route.Nodes[$link.ToNode]
  if($link.Action -eq [TraversalAction]::Walk){$left+=[Math]::Max(0,$a.FloorX-$b.FloorX);$right+=[Math]::Max(0,$b.FloorX-$a.FloorX)}
  $up+=[Math]::Max(0,$b.FloorY-$a.FloorY);$down+=[Math]::Max(0,$a.FloorY-$b.FloorY)
 }
 if($left -lt 10 -or $right -lt 10 -or $up -lt 14 -or $down -lt 14){throw "Normal world lacks meaningful reversals: seed=$seed"}
 $exploration+=@{seed=$seed;profile=$w.Route.Profile.ToString();width=$w.Tiles.GetLength(0);height=$w.Tiles.GetLength(1)}
}
if($exploration.Count -eq 0){throw 'No mixed exploration seeds accepted'}
# Normal play already retries a bounded set of distinct seeds. Rejected fixed debug
# seeds must never turn into an accepted straight fallback or block the next run.
$runs=[SpatialRunProbe]::Generate($caps,32)
$walk=Ceiling-Probe ([TraversalAction]::Walk)
$jump=Ceiling-Probe ([TraversalAction]::DoubleJump)
if($walk -ne $jump){throw "Ceiling still stamps the jump trajectory: walk=$walk doubleJump=$jump"}
@{passed=$true;ceilingsIndependentOfAction=$true;challengeCountPatterns=$countPatterns.Count;cases=$records;rooms=$roomCounts;exploration=$exploration;rejectedFixedSeeds=$rejected;normalRuns=$runs}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $rootPath $ReportPath)
[pscustomobject]@{passed=$true;ceilingIndependentOfJump=$true;countPatterns=$countPatterns.Count}
