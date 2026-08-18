[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' |
    Select-Object -First 1
if (-not $msbuild) {
    throw 'MSBuild was not found.'
}
$target = if ($Clean) { 'Rebuild' } else { 'Build' }
& $msbuild (Join-Path $root 'PtLsmr.sln') /m "/t:$target" "/p:Configuration=$Configuration" '/p:Platform=x64' /v:minimal /nologo
if ($LASTEXITCODE -ne 0) {
    throw "PtLsmr build failed with exit code $LASTEXITCODE."
}
Write-Host "Build succeeded: $(Join-Path $root "artifacts\bin\x64\$Configuration")"
