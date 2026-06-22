# WinAppCli.PowerToys.psm1 — module entry point.
# Dot-sources every function file under .\functions\ so they share module scope.

$ErrorActionPreference = 'Stop'

# Ensure winapp CLI is on PATH for child invocations even when imported in
# a fresh shell that hasn't refreshed the User PATH yet.
$env:Path += ';' + [Environment]::GetEnvironmentVariable('Path', 'User')

$functionRoot = Join-Path $PSScriptRoot 'functions'
Get-ChildItem -Path $functionRoot -Filter '*.ps1' -File | Sort-Object Name | ForEach-Object {
    . $_.FullName
}

# Module-private state (kept here so multiple function files can share)
$script:TestResults = New-Object System.Collections.Generic.List[object]
