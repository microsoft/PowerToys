[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [ValidateSet('x64')]
    [string]$Platform = 'x64',
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) {
    throw "vswhere.exe was not found."
}
$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' |
    Select-Object -First 1
if (-not $msbuild) {
    throw "MSBuild was not found."
}
$target = if ($Clean) { 'Rebuild' } else { 'Build' }
& $msbuild (Join-Path $root 'PtAliasProto.sln') /m "/t:$target" "/p:Configuration=$Configuration" "/p:Platform=$Platform" /v:minimal /nologo
if ($LASTEXITCODE -ne 0) {
    throw "PtAliasProto build failed with exit code $LASTEXITCODE."
}

$selfTest = Join-Path $root "artifacts\bin\$Platform\$Configuration\PtAliasProtoSelfTest.exe"
Write-Host "Build succeeded: $selfTest"
