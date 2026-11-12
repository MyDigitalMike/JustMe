param([string]$ReportPath)
$ErrorActionPreference='Stop'
$rootPath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$sources=@('Assets/Script/Game/TraversalStamina.cs','Assets/Script/GeneracionMundo/ProceduralWorld.cs','Assets/Script/GeneracionMundo/TraversalWorld.cs','Assets/Script/GeneracionMundo/TraversalValidator.cs')|ForEach-Object{Join-Path $rootPath $_}
Add-Type -Path $sources
$raw=Get-Content (Join-Path $rootPath 'Assets/Resources/TraversalCapabilities.json') -Raw|ConvertFrom-Json
$caps=[TraversalCapabilities]::new()
foreach($p in $raw.PSObject.Properties){if($p.Name -like '*Samples'){ $caps.($p.Name)=@($p.Value|ForEach-Object{[TrajectorySample]::new($_.Time,$_.X,$_.Y)})}else{$caps.($p.Name)=$p.Value}}
$script:assertions=0
function Assert-Check($condition,[string]$message){$script:assertions++;if(!$condition){throw $message}}
function World-Hash($world){$text=[Text.StringBuilder]::new();foreach($tile in $world.Tiles){[void]$text.Append($tile)};foreach($node in $world.Route.Nodes){[void]$text.Append("|$($node.FloorX),$($node.FloorY)")};$sha=[Security.Cryptography.SHA256]::Create();try{[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text.ToString())))}finally{$sha.Dispose()}}
$records=[Collections.Generic.List[object]]::new()
foreach($profile in @('Horizontal','Ascent','Descent','Mixed')){
 foreach($seed in @(0,1,-1,[int]::MaxValue)){
  $settings=[TraversalGenerationSettings]::new();$settings.Profile=[WorldProfile]::$profile
  if($profile -eq 'Ascent' -or $profile -eq 'Descent'){$settings.Terrain.Width=56;$settings.Terrain.Height=96;$settings.Terrain.SurfaceHeight=88}
  $world=[ProceduralWorld]::GenerateTraversal($settings,$caps,[StaminaSettings]::new(),$seed)
  $validation=[TraversalValidator]::Validate($world,$caps,[StaminaSettings]::new())
  Assert-Check $validation.Valid "Invalid $profile/$seed : $($validation.Reason)"
  $repeat=[ProceduralWorld]::GenerateTraversal($settings,$caps,[StaminaSettings]::new(),$seed)
  Assert-Check ((World-Hash $world) -eq (World-Hash $repeat)) 'Seed reproduction differs'
  Assert-Check (@($world.Route.Links|Where-Object Action -eq 'Climb').Count -ge 2) 'Missing nonflat traversal'
  Assert-Check (@($world.Route.Links|Where-Object Action -eq 'Crawl').Count -ge 1) 'Missing low tunnels'
  Assert-Check (@($world.Route.Links|Where-Object Action -eq 'DoubleJump').Count -ge 1) 'Missing double jump challenge'
  Assert-Check (@($world.Route.Links|Where-Object BranchId -eq 2).Count -eq 4) 'Missing directed dead-end return'
  Assert-Check ($world.VoidRegions.Length -gt 0) 'Missing explicit lethal hazards'
  foreach($hazard in $world.VoidRegions){
   Assert-Check ((($hazard.MaxX-$hazard.MinX+1)*$world.CellSize) -ge 1) 'Mandatory gap clear width must be at least the approved 1 unit'
  }
  $records.Add([pscustomobject]@{profile=$profile;seed=$seed;hash=(World-Hash $world);predictedSeconds=$validation.TotalSeconds;remaining=$validation.RemainingStamina})
 }
}
$settings=[TraversalGenerationSettings]::new();$world=[ProceduralWorld]::GenerateTraversal($settings,$caps,[StaminaSettings]::new(),1)
$x=$world.SpawnX;$y=$world.SpawnY;$old=$world.Tiles[$x,$y];$world.Tiles[$x,$y]=1
Assert-Check (!([TraversalValidator]::Validate($world,$caps,[StaminaSettings]::new())).Valid) 'Occupied spawn accepted';$world.Tiles[$x,$y]=$old
$low=[StaminaSettings]::new();$low.MaxStamina=10
Assert-Check (!([TraversalValidator]::Validate($world,$caps,$low)).Valid) 'Unaffordable route accepted'
$crawl=[StaminaSettings]::new();$crawl.CrawlDrainPerSecond=100
Assert-Check (!([TraversalValidator]::Validate($world,$caps,$crawl)).Valid) 'Unaffordable crawl accepted'
$rests=@($world.Route.Links|Where-Object Action -eq 'Rest');foreach($rest in $rests){$rest.Duration=.02;$rest.Path[1].Time=.02}
Assert-Check (!([TraversalValidator]::Validate($world,$caps,[StaminaSettings]::new())).Valid) 'Consecutive exertion magically refilled stamina';foreach($rest in $rests){$rest.Duration=10;$rest.Path[1].Time=10}
$jump=$world.Route.Links|Where-Object Action -eq 'Jump'|Select-Object -First 1;$jump.Action=[TraversalAction]::Walk
Assert-Check (!([TraversalValidator]::Validate($world,$caps,[StaminaSettings]::new())).Valid) 'Airborne walk accepted';$jump.Action=[TraversalAction]::Jump
$oldY=$jump.Path[3].Y;$jump.Path[3].Y+=.5
Assert-Check (!([TraversalValidator]::Validate($world,$caps,[StaminaSettings]::new())).Valid) 'Unmeasured jump accepted';$jump.Path[3].Y=$oldY
$regions=$world.ClimbRegions;$world.ClimbRegions=@()
Assert-Check (!([TraversalValidator]::Validate($world,$caps,[StaminaSettings]::new())).Valid) 'Undesignated climbing accepted';$world.ClimbRegions=$regions
$crawlLink=$world.Route.Links|Where-Object Action -eq 'Crawl'|Select-Object -First 1
$duration=$crawlLink.Duration;$endTime=$crawlLink.Path[1].Time;$crawlLink.Duration=.02;$crawlLink.Path[1].Time=.02
Assert-Check (!([TraversalValidator]::Validate($world,$caps,[StaminaSettings]::new())).Valid) 'Impossible crawl speed accepted';$crawlLink.Duration=$duration;$crawlLink.Path[1].Time=$endTime
$climbLink=$world.Route.Links|Where-Object Action -eq 'Climb'|Select-Object -First 1
$duration=$climbLink.Duration;$times=@($climbLink.Path|ForEach-Object{$_.Time})
foreach($point in $climbLink.Path){$point.Time*=.01};$climbLink.Duration=$climbLink.Path[-1].Time
Assert-Check (!([TraversalValidator]::Validate($world,$caps,[StaminaSettings]::new())).Valid) 'Impossible climb speed accepted'
for($i=0;$i -lt $times.Count;$i++){$climbLink.Path[$i].Time=$times[$i]};$climbLink.Duration=$duration
$hazards=$world.VoidRegions;$world.VoidRegions=@([TileRegion]::new(1,1,55,30))
Assert-Check (!([TraversalValidator]::Validate($world,$caps,[StaminaSettings]::new())).Valid) 'Lethal path accepted';$world.VoidRegions=$hazards
$world.Route.Nodes[0].SafeRest=$false
Assert-Check (!([TraversalValidator]::Validate($world,$caps,[StaminaSettings]::new())).Valid) 'Unsafe rest accepted';$world.Route.Nodes[0].SafeRest=$true
$random=[ProceduralWorld]::GenerateTraversal($settings,$caps,[StaminaSettings]::new(),$null)
$replayed=[ProceduralWorld]::GenerateTraversal($settings,$caps,[StaminaSettings]::new(),$random.Seed)
Assert-Check ((World-Hash $random) -eq (World-Hash $replayed)) 'Automatic seed cannot be replayed'
$bounded=$false;try{[void][ProceduralWorld]::GenerateTraversal($settings,$caps,$low,1)}catch{$bounded=$_.Exception.InnerException -is [TraversalGenerationException]}
Assert-Check $bounded 'Failed route was silently accepted or not bounded'
$report=[ordered]@{passed=$true;verification='Pure C# model; does not claim physical completion of every seed';assertions=$script:assertions;cases=$records;automaticSeed=$random.Seed;rejectedCases=@('spawn obstruction','low stamina','crawl exhaustion','removed recovery','airborne walk','forged jump','missing climbable regions','impossible crawl speed','impossible climb speed','lethal path','unsafe rest','bounded rejection')}
if(!$ReportPath){$ReportPath=Join-Path $rootPath 'Backups/Phase3/world-validation.json'}
$report|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $ReportPath
[pscustomobject]@{passed=$true;assertions=$script:assertions;acceptedCases=$records.Count;rejectedCases=12}
