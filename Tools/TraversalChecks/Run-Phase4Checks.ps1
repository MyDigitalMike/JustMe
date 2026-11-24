param([string]$ReportPath)
$ErrorActionPreference='Stop'
$rootPath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$sourceNames=@('Assets/Script/Game/TraversalStamina.cs','Assets/Script/GeneracionMundo/ProceduralWorld.cs','Assets/Script/GeneracionMundo/TraversalWorld.cs','Assets/Script/GeneracionMundo/TraversalValidator.cs','Assets/Script/Game/DiaryProgress.cs','Assets/Script/GeneracionMundo/DiaryPlanner.cs','Assets/Script/GeneracionMundo/TraversalTopology.cs','Assets/Script/GeneracionMundo/WorldRunSession.cs')
foreach($name in $sourceNames){if(!(Test-Path -LiteralPath (Join-Path $rootPath $name))){throw "Phase4 API missing: $name"}}
Add-Type -Path @($sourceNames|ForEach-Object {Join-Path $rootPath $_})
$raw=Get-Content -LiteralPath (Join-Path $rootPath 'Assets/Resources/TraversalCapabilities.json') -Raw|ConvertFrom-Json
$caps=[TraversalCapabilities]::new()
foreach($p in $raw.PSObject.Properties){if($p.Name -like '*Samples'){$caps.($p.Name)=@($p.Value|ForEach-Object {[TrajectorySample]::new($_.Time,$_.X,$_.Y)})}else{$caps.($p.Name)=$p.Value}}
$script:assertions=0
function Assert-Check($value,[string]$message){$script:assertions++;if(!$value){throw $message}}
function Assert-Throws([scriptblock]$action,[string]$message){$caught=$false;try{& $action}catch{$caught=$true};Assert-Check $caught $message}
$growth=[DiaryProgressionSettings]::new()
foreach($level in @(1,2,3,10,11,[int]::MaxValue)){Assert-Check ($growth.RequiredForLevel($level) -eq [Math]::Min([long]12,([long]$level+2))) "Wrong growth at level $level"}
Assert-Throws {$growth.RequiredForLevel(0)} 'Level zero accepted'
$growth.MaximumFragments=2;Assert-Throws {$growth.RequiredForLevel(1)} 'Invalid growth cap accepted';$growth.MaximumFragments=12
$progress=[DiaryProgress]::new(3,8)
Assert-Check (!$progress.IsComplete -and $progress.CollectedCount -eq 0) 'New level not reset'
Assert-Check (!$progress.TryCollect(0,7)) 'Stale page counted'
Assert-Check (!$progress.TryCollect(-1,8) -and !$progress.TryCollect(3,8)) 'Out-of-range page counted'
foreach($id in 0..2){Assert-Check ($progress.TryCollect($id,8)) 'Valid page not collected';Assert-Check (!$progress.TryCollect($id,8)) 'Duplicate counted'}
Assert-Check ($progress.IsComplete -and $progress.CollectedCount -eq 3) 'Exit never unlocked'
Assert-Throws {[DiaryProgress]::new(13,9)} 'Unbounded fragment count accepted'
$settings=[TraversalGenerationSettings]::new();$settings.Profile=[WorldProfile]::Mixed
$script:seedCounter=50
$factory=[Func[int,WorldData]]{param($seed) [ProceduralWorld]::GenerateTraversal($settings,$caps,[StaminaSettings]::new(),$seed)}
$session=[WorldRunSession]::new([Func[int]]{$script:seedCounter++;return $script:seedCounter},8)
$first=$session.GenerateNext(1,$false,1,$factory);$signature=$session.CurrentSignature;$firstSeed=$session.CurrentSeed
Assert-Check ($first.Seed -eq 51 -and $session.CurrentSeed -eq 51) 'Accepted seed not recorded'
Assert-Check ($session.CurrentSignature -eq $signature -and $session.CurrentSeed -eq $firstSeed) 'Current level mutated without generation'
$next=$session.GenerateNext(2,$false,1,$factory)
Assert-Check ($next.Seed -eq 52 -and $session.CurrentSignature -ne $signature) 'Next level repeated seed/geometry'
$fixed=[WorldRunSession]::new([Func[int]]{throw 'Debug should not call random provider'},8)
$debug1=$fixed.GenerateNext(1,$true,123,$factory);$hash1=$fixed.CurrentSignature
$debug2=$fixed.GenerateNext(1,$true,123,$factory)
Assert-Check ($debug1.Seed -eq 123 -and $debug2.Seed -eq 123 -and $fixed.CurrentSignature -eq $hash1) 'Fixed replay is nondeterministic'
$script:seedCounter=100
$identicalFactory=[Func[int,WorldData]]{param($seed) $copy=[ProceduralWorld]::GenerateTraversal($settings,$caps,[StaminaSettings]::new(),1);$copy.Seed=$seed;return $copy}
$bounded=[WorldRunSession]::new([Func[int]]{$script:seedCounter++;return $script:seedCounter},3)
[void]$bounded.GenerateNext(1,$false,1,$identicalFactory);$accepted=$bounded.CurrentSeed;$acceptedSignature=$bounded.CurrentSignature
Assert-Throws {$bounded.GenerateNext(2,$false,1,$identicalFactory)} 'Repeated geometry silently accepted'
Assert-Check ($script:seedCounter -eq 104 -and $bounded.CurrentSeed -eq $accepted -and $bounded.CurrentSignature -eq $acceptedSignature) 'Retry bound or transactional acceptance failed'
$records=[Collections.Generic.List[object]]::new()
foreach($profile in @('Horizontal','Ascent','Descent','Mixed')){
 foreach($count in @(1,3,4,12)){
  foreach($seed in @(0,1,-1,[int]::MaxValue)){
   $s=[TraversalGenerationSettings]::new();$s.Profile=[WorldProfile]::$profile;$s.RequiredFragments=$count
   if($profile -eq 'Ascent' -or $profile -eq 'Descent'){$s.Terrain.Width=56;$s.Terrain.Height=96;$s.Terrain.SurfaceHeight=88}
   $world=[ProceduralWorld]::GenerateTraversal($s,$caps,[StaminaSettings]::new(),$seed)
   $validation=[TraversalValidator]::Validate($world,$caps,[StaminaSettings]::new(),$count)
   Assert-Check $validation.Valid "Collection rejected for $profile/$count/$seed : $($validation.Reason)"
   Assert-Check ($world.FragmentNodes.Length -eq $count -and @($world.FragmentNodes|Sort-Object -Unique).Count -eq $count) 'Missing/duplicate objective nodes'
   Assert-Check ($validation.FragmentsReached -eq $count) 'Full route did not collect every fragment'
   Assert-Check ($world.CollectionLinks.Length -eq $world.Route.PrimaryLinks.Length+4) 'Mandatory branch return absent'
   $again=[ProceduralWorld]::GenerateTraversal($s,$caps,[StaminaSettings]::new(),$seed)
   Assert-Check (([WorldRunSession]::GeometrySignature($world)) -eq ([WorldRunSession]::GeometrySignature($again)) -and (($world.FragmentNodes -join ',') -eq ($again.FragmentNodes -join ','))) 'Seed/map/objective replay differs'
   $records.Add([pscustomobject]@{profile=$profile;count=$count;seed=$seed;remaining=$validation.RemainingStamina;seconds=$validation.TotalSeconds;signature=[WorldRunSession]::GeometrySignature($world)})
  }
 }
}
$s=[TraversalGenerationSettings]::new();$world=[ProceduralWorld]::GenerateTraversal($s,$caps,[StaminaSettings]::new(),1)
$nodes=$world.FragmentNodes.Clone();$world.FragmentNodes[1]=$world.FragmentNodes[0]
Assert-Check (!([TraversalValidator]::Validate($world,$caps,[StaminaSettings]::new(),3)).Valid) 'Duplicate page location accepted';$world.FragmentNodes=$nodes
$route=$world.CollectionLinks;$world.CollectionLinks=$world.Route.PrimaryLinks
Assert-Check (!([TraversalValidator]::Validate($world,$caps,[StaminaSettings]::new(),3)).Valid) 'Primary route alone falsely certified branch page';$world.CollectionLinks=$route
$world.FragmentNodes=@($nodes[0],$nodes[1]);Assert-Check (!([TraversalValidator]::Validate($world,$caps,[StaminaSettings]::new(),3)).Valid) 'Too few pages accepted';$world.FragmentNodes=$nodes
$index=$world.CollectionLinks[3];$world.CollectionLinks=@($route|Where-Object {$_ -ne $index})
Assert-Check (!([TraversalValidator]::Validate($world,$caps,[StaminaSettings]::new(),3)).Valid) 'Missing branch return accepted';$world.CollectionLinks=$route
$deadNode=$world.FragmentNodes[0];$node=$world.Route.Nodes[$deadNode];$old=$world.Tiles[$node.FloorX,($node.FloorY+1)];$world.Tiles[$node.FloorX,($node.FloorY+1)]=1
Assert-Check (!([TraversalValidator]::Validate($world,$caps,[StaminaSettings]::new(),3)).Valid) 'Blocked page accepted';$world.Tiles[$node.FloorX,($node.FloorY+1)]=$old
$world.FragmentNodes[0]=0;Assert-Check (!([TraversalValidator]::Validate($world,$caps,[StaminaSettings]::new(),3)).Valid) 'Spawn used as a page location';$world.FragmentNodes=$nodes.Clone()
$originalNodes=$world.Route.Nodes
$copy=[TraversalNode]::new($world.Route.Nodes[$nodes[0]].FloorX,$world.Route.Nodes[$nodes[0]].FloorY)
$world.Route.Nodes=@($originalNodes)+@($copy);$world.FragmentNodes[1]=$world.Route.Nodes.Length-1
Assert-Check (!([TraversalValidator]::Validate($world,$caps,[StaminaSettings]::new(),3)).Valid) 'Different node ID hid duplicate coordinates';$world.Route.Nodes=$originalNodes;$world.FragmentNodes=$nodes.Clone()
$branch=@($route[0..3]);$world.CollectionLinks=@($branch)+@($branch)+@($branch)+@($world.Route.PrimaryLinks)
Assert-Check (!([TraversalValidator]::Validate($world,$caps,[StaminaSettings]::new(),3)).Valid) 'Repeated branches reset stamina or lost cumulative costs';$world.CollectionLinks=$route
$world.FragmentNodes=@($world.Route.PrimaryLinks|ForEach-Object {$world.Route.Links[$_].ToNode}|Where-Object {$_ -ne 0 -and $world.Route.Nodes[$_].FloorX -ne $world.GoalX}|Select-Object -Unique|Select-Object -First 3);$world.CollectionLinks=$world.Route.PrimaryLinks
Assert-Check (!([TraversalValidator]::Validate($world,$caps,[StaminaSettings]::new(),3)).Valid) 'All-main placement bypassed mandatory exploration';$world.FragmentNodes=$nodes.Clone();$world.CollectionLinks=$route
$s.RequiredFragments=13;Assert-Throws {[ProceduralWorld]::GenerateTraversal($s,$caps,[StaminaSettings]::new(),1)} 'Unbounded page request accepted'
if(!$ReportPath){$ReportPath=Join-Path $rootPath 'Backups/Phase4/pure-checks.json'}
[ordered]@{passed=$true;verification='Pure C# policy and complete collection model; not Unity compilation or physical proof of every seed';assertions=$script:assertions;worldCases=$records.Count;worlds=$records}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $ReportPath
[pscustomobject]@{passed=$true;assertions=$script:assertions;worldCases=$records.Count}
