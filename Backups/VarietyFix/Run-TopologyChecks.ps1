param([string]$ReportPath='Backups/WorldTopology/pure.json')
$ErrorActionPreference='Stop'
$rootPath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$names=@('Assets/Script/Game/TraversalStamina.cs','Assets/Script/GeneracionMundo/ProceduralWorld.cs','Assets/Script/GeneracionMundo/TraversalWorld.cs','Assets/Script/GeneracionMundo/TraversalValidator.cs','Assets/Script/Game/DiaryProgress.cs','Assets/Script/GeneracionMundo/DiaryPlanner.cs','Assets/Script/GeneracionMundo/WorldRunSession.cs')
$planner=Join-Path $rootPath 'Assets/Script/GeneracionMundo/TraversalTopology.cs'
if(Test-Path $planner){$names+='Assets/Script/GeneracionMundo/TraversalTopology.cs'}
Add-Type -Path @($names|ForEach-Object {Join-Path $rootPath $_})
$raw=Get-Content -LiteralPath (Join-Path $rootPath 'Assets/Resources/TraversalCapabilities.json') -Raw|ConvertFrom-Json
$caps=[TraversalCapabilities]::new()
foreach($prop in $raw.PSObject.Properties){if($prop.Name -like '*Samples'){$caps.($prop.Name)=@($prop.Value|ForEach-Object {[TrajectorySample]::new($_.Time,$_.X,$_.Y)})}else{$caps.($prop.Name)=$prop.Value}}
function Headroom($world,$node){for($y=$node.FloorY+1;$y -lt $world.Tiles.GetLength(1);$y++){if($world.Tiles[$node.FloorX,$y] -eq 1){return ($y-$node.FloorY-1)*$world.CellSize}};return 999}
$legacy=[ProceduralWorld]::GenerateTraversal([TraversalGenerationSettings]::new(),$caps,[StaminaSettings]::new(),1)
$oldHeight=Headroom $legacy $legacy.Route.Nodes[0]
if(!(Test-Path $planner)){throw "Missing macro planner; existing ordinary floor/ceiling=$oldHeight, character=$($caps.StandingHeight)"}
$script:assertions=0
function Check($condition,$message){$script:assertions++;if(!$condition){throw $message}}
$records=@();$chosen=[Collections.Generic.HashSet[string]]::new()
foreach($layout in @('Horizontal','Ascent','Descent','U','Switchback','Cross','Random')){
 foreach($seed in @(0,1,42)){
  foreach($pages in @(3,12)){
   $settings=[TraversalGenerationSettings]::new();$settings.Layout=[TraversalLayout]$layout;$settings.RequiredFragments=$pages
   try{$world=[ProceduralWorld]::GenerateTraversal($settings,$caps,[StaminaSettings]::new(),$seed)}catch{throw "Layout=$layout seed=$seed pages=$pages : $_"}
   $validation=[TraversalValidator]::Validate($world,$caps,[StaminaSettings]::new())
   Check ($validation.Valid -and $validation.FragmentsReached -eq $pages) "Invalid complete route: $layout/$seed/$pages"
   Check ($world.Tiles.GetLength(0) -le 384 -and $world.Tiles.GetLength(1) -le 384) 'Unbounded dynamic terrain'
   $again=[ProceduralWorld]::GenerateTraversal($settings,$caps,[StaminaSettings]::new(),$seed)
   Check ([WorldRunSession]::GeometrySignature($world) -eq [WorldRunSession]::GeometrySignature($again)) 'Non-deterministic topology'
   $positive=$false;$negative=$false;$up=$false;$down=$false;$head=999
   foreach($index in $world.Route.PrimaryLinks){
    $link=$world.Route.Links[$index];$a=$world.Route.Nodes[$link.FromNode];$b=$world.Route.Nodes[$link.ToNode]
    $positive=$positive -or $b.FloorX -gt $a.FloorX;$negative=$negative -or $b.FloorX -lt $a.FloorX
    $up=$up -or $b.FloorY -gt $a.FloorY;$down=$down -or $b.FloorY -lt $a.FloorY
    if($link.Action -in @([TraversalAction]::Walk,[TraversalAction]::Sprint,[TraversalAction]::Rest)){
      $head=[Math]::Min($head,(Headroom $world $a));$head=[Math]::Min($head,(Headroom $world $b))
    }
   }
   Check ($head -ge 2.5) "Ordinary headroom=$head at $layout/$seed"
   $ys=@($world.Route.Nodes|ForEach-Object {$_.FloorY});$extent=(($ys|Measure-Object -Maximum).Maximum-($ys|Measure-Object -Minimum).Minimum)*.5
   if($layout -in @('Ascent','Descent')){Check ($extent -ge 60) 'Vertical world too short'}
   if($layout -in @('U','Switchback','Cross')){Check ($up -and $down -or $layout -eq 'Switchback') 'Missing up/down arms'}
   if($layout -in @('Switchback','Cross')){Check ($positive -and $negative) 'Missing left/right reversal'}
   [void]$chosen.Add($world.Route.Profile.ToString())
   $records+=@{layout=$layout;seed=$seed;pages=$pages;width=$world.Tiles.GetLength(0)*.5;height=$world.Tiles.GetLength(1)*.5;verticalExtent=$extent;minimumOrdinaryHeight=$head;remaining=$validation.RemainingStamina;signature=[WorldRunSession]::GeometrySignature($world)}
  }
 }
}
Check ($chosen.Count -ge 4) 'Random profiles collapsed'
$report=@{passed=$true;assertions=$script:assertions;cases=$records;legacyPassageHeight=$oldHeight;standingHeight=$caps.StandingHeight;verification='Pure topology and full collection/stamina model; physical replay required'}
$report|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $rootPath $ReportPath)
[pscustomobject]@{passed=$true;assertions=$script:assertions;worlds=$records.Count;legacyPassage=$oldHeight;characterHeight=$caps.StandingHeight}
