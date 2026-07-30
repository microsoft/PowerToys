$ErrorActionPreference = 'Stop'

$composeFile = Join-Path $PSScriptRoot 'compose.yml'
$environmentFile = Join-Path $PSScriptRoot '.env'
if (-not (Test-Path $environmentFile -PathType Leaf)) {
    throw "Create $environmentFile from .env.example first."
}

& docker compose --env-file $environmentFile -f $composeFile stop
if ($LASTEXITCODE -ne 0) {
    throw 'Failed to stop the dockur Windows stack.'
}

& docker compose --env-file $environmentFile -f $composeFile ps --all
