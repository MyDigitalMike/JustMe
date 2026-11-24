$ErrorActionPreference='Stop'
$before=(Get-Content 'Backups/PlayerAnimation/Originals/Assets/Script/Game/Player.cs' -Raw).Replace("`r`n","`n")
$after=(Get-Content 'Assets/Script/Game/Player.cs' -Raw).Replace("`r`n","`n")
function Method-Body([string]$source,[string]$name){
 $match=[regex]::Match($source,'(?m)^ {4}(?:public |private |static )?(?:void|bool|float|Vector2) '+[regex]::Escape($name)+'\(')
 if(!$match.Success){throw "Missing method: $name"};$start=$source.IndexOf('{',$match.Index);$depth=0
 for($i=$start;$i -lt $source.Length;$i++){
  if($source[$i] -eq '{'){$depth++}elseif($source[$i] -eq '}'){$depth--;if($depth -eq 0){return $source.Substring($start,$i-$start+1)}}
 };throw 'Unbalanced method'
}
$checks=@();foreach($name in @('SimulateTraversal','RefreshGroundContact','SetCrouched','CanStandAt','TryGrabLedge','GripSurfaceExists','HasValidGrip','MovePullUp','ReleaseGrip','NotifyWarp')){
 $same=(Method-Body $before $name) -ceq (Method-Body $after $name)
 if(!$same){throw "Changed physics method: $name"};$checks+=@{method=$name;unchanged=$true}
}
$originals=Get-Content 'Backups/PlayerAnimation/original-sprites.json' -Raw|ConvertFrom-Json
foreach($file in $originals){if((Get-FileHash -LiteralPath $file.path).Hash -ne $file.sha256){throw "Original sprite changed: $($file.path)"}}
$metas=@(Get-Content 'Backups/PlayerAnimation/before.json' -Raw|ConvertFrom-Json|Where-Object {$_.path.EndsWith('.meta')})
foreach($file in $metas){if((Get-FileHash -LiteralPath $file.path).Hash -ne $file.sha256){throw "Original GUID metadata changed: $($file.path)"}}
@{physicsMethods=$checks;originalSpriteFilesPreserved=$originals.Count;originalMetaFilesPreserved=$metas.Count}|ConvertTo-Json -Depth 5|Set-Content 'Backups/PlayerAnimation/preservation.json'
[pscustomobject]@{passed=$true;physicsMethods=$checks.Count;originalFiles=$originals.Count;metas=$metas.Count}
