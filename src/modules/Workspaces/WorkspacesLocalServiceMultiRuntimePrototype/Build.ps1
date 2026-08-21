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

$runtime2Out = Join-Path $root "artifacts\bin\x64\$Configuration\runtime-track-2"
$runtime2CommonObj = Join-Path $root "artifacts\obj\PtLsmrCommonTrack2\x64\$Configuration"
$runtime2Obj = Join-Path $root "artifacts\obj\PtLsmrRuntimeTrack2\x64\$Configuration"
Remove-Item -LiteralPath $runtime2CommonObj -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $runtime2Obj -Recurse -Force -ErrorAction SilentlyContinue
& $msbuild (Join-Path $root 'Common\PtLsmrCommon.vcxproj') /m '/t:Rebuild' `
    "/p:Configuration=$Configuration" '/p:Platform=x64' '/p:RuntimeTrack=2' `
    "/p:OutDir=$runtime2Out\" "/p:IntDir=$runtime2CommonObj\" /v:minimal /nologo
if ($LASTEXITCODE -ne 0) {
    throw "Runtime track 2 common build failed with exit code $LASTEXITCODE."
}
& $msbuild (Join-Path $root 'Runtime\PtLsmrRuntime.vcxproj') /m '/t:Rebuild' `
    "/p:Configuration=$Configuration" '/p:Platform=x64' '/p:RuntimeTrack=2' `
    '/p:BuildProjectReferences=false' "/p:OutDir=$runtime2Out\" `
    "/p:IntDir=$runtime2Obj\" /v:minimal /nologo
if ($LASTEXITCODE -ne 0) {
    throw "Runtime track 2 build failed with exit code $LASTEXITCODE."
}

$updater6Out = Join-Path $root "artifacts\bin\x64\$Configuration\updater-v6"
$updater6CommonObj = Join-Path $root "artifacts\obj\PtLsmrCommonUpdater6\x64\$Configuration"
$updater6Obj = Join-Path $root "artifacts\obj\PtLsmrUpdater6\x64\$Configuration"
Remove-Item -LiteralPath $updater6CommonObj -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $updater6Obj -Recurse -Force -ErrorAction SilentlyContinue
& $msbuild (Join-Path $root 'Common\PtLsmrCommon.vcxproj') /m '/t:Rebuild' `
    "/p:Configuration=$Configuration" '/p:Platform=x64' '/p:UpdaterVersionMajor=6' `
    "/p:OutDir=$updater6Out\" "/p:IntDir=$updater6CommonObj\" /v:minimal /nologo
if ($LASTEXITCODE -ne 0) {
    throw "Updater v6 common build failed with exit code $LASTEXITCODE."
}
& $msbuild (Join-Path $root 'Updater\PtLsmrUpdater.vcxproj') /m '/t:Rebuild' `
    "/p:Configuration=$Configuration" '/p:Platform=x64' '/p:UpdaterVersionMajor=6' `
    '/p:BuildProjectReferences=false' "/p:OutDir=$updater6Out\" `
    "/p:IntDir=$updater6Obj\" /v:minimal /nologo
if ($LASTEXITCODE -ne 0) {
    throw "Updater v6 build failed with exit code $LASTEXITCODE."
}
Write-Host "Build succeeded: $(Join-Path $root "artifacts\bin\x64\$Configuration")"
