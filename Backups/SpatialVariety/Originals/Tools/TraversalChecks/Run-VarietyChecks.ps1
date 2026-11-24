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

foreach($seed in @(0,1,42)){
$settings=[TraversalGenerationSettings]::new();$settings.Layout=[TraversalLayout]::Horizontal
$world=[ProceduralWorld]::GenerateTraversal($settings,$caps,[StaminaSettings]::new(),$seed)
$challenges=@($world.Route.PrimaryLinks|ForEach-Object {$world.Route.Links[$_]}|Where-Object {$_.Action -in @([TraversalAction]::Jump,[TraversalAction]::DoubleJump,[TraversalAction]::Crawl)})
$seen=[Collections.Generic.HashSet[string]]::new()
for($i=0;$i -lt $challenges.Count;$i+=3){$key=($challenges[$i..($i+2)]|ForEach-Object {$_.Action.ToString()}) -join ',';if(!$seen.Add($key)){throw "Repeated full obstacle module: $key"}}
'No repeated obstacle-order modules in representative horizontal world'

$settings.Layout=[TraversalLayout]::Ascent
$world=[ProceduralWorld]::GenerateTraversal($settings,$caps,[StaminaSettings]::new(),$seed)
$last='';$types=[Collections.Generic.HashSet[string]]::new()
foreach($idx in $world.Route.PrimaryLinks){$l=$world.Route.Links[$idx];if($l.Action -eq [TraversalAction]::Climb){$a=$world.Route.Nodes[$l.FromNode];$b=$world.Route.Nodes[$l.ToNode];$key="$($b.FloorX-$a.FloorX),$($b.FloorY-$a.FloorY)";if($key -eq $last){throw 'Adjacent cloned climbing footprints'};$last=$key;[void]$types.Add($key)}}
if($types.Count -lt 3){throw 'Insufficient climb geometry variation'}
"Climb footprint variants: $($types.Count)"

}
@{passed=$true;seeds=@(0,1,42);checks=@("unique whole obstacle-order modules per horizontal level","no adjacent identical climb footprints","at least three climb footprints per ascent")}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $rootPath "Backups/VarietyFix/anti-repeat-checks.json")
