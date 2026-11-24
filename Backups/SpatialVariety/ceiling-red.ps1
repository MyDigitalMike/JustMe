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
$settings=[TraversalGenerationSettings]::new();$settings.Layout=[TraversalLayout]::Horizontal
$records=@();$countPatterns=[Collections.Generic.HashSet[string]]::new()
foreach($seed in @(0,1,42)){
 $w=[ProceduralWorld]::GenerateTraversal($settings,$caps,[StaminaSettings]::new(),$seed)
 $counts=@{}
 foreach($kind in @('Jump','DoubleJump','Crawl')){$counts[$kind]=@($w.Route.PrimaryLinks|Where-Object {$w.Route.Links[$_].Action.ToString() -eq $kind}).Count}
 [void]$countPatterns.Add("$($counts.Jump),$($counts.DoubleJump),$($counts.Crawl)")
 $records+=@{seed=$seed;counts=$counts}
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
$walk=Ceiling-Probe ([TraversalAction]::Walk)
$jump=Ceiling-Probe ([TraversalAction]::DoubleJump)
if($walk -ne $jump){throw "Ceiling still stamps the jump trajectory: walk=$walk doubleJump=$jump"}
@{passed=$true;ceilingsIndependentOfAction=$true;challengeCountPatterns=$countPatterns.Count;cases=$records}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $rootPath 'Backups/SpatialVariety/spatial-checks.json')
[pscustomobject]@{passed=$true;ceilingIndependentOfJump=$true;countPatterns=$countPatterns.Count}
