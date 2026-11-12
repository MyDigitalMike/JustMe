param([string]$OutputPath = (Join-Path $PSScriptRoot '..\..\Backups\Phase3\world-validation.json'))
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
if (-not (Test-Path -LiteralPath (Join-Path $root 'Assets\Script\GeneracionMundo\TraversalWorld.cs'))) {
    throw 'Missing traversal API: route-first generator and directed validator are not implemented.'
}
