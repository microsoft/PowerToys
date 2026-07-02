#Requires -Version 5.1
<#
.SYNOPSIS
    Build dev CmdPal, ensure WinAppDriver is running, and run hover-action UI tests.

.EXAMPLE
    .\tools\build\run-hover-ui-test.ps1
#>
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

$winAppDriver = 'C:\Program Files (x86)\Windows Application Driver\WinAppDriver.exe'
if (-not (Test-Path $winAppDriver)) {
    throw "WinAppDriver not found at: $winAppDriver"
}

$driverProc = Get-Process WinAppDriver -ErrorAction SilentlyContinue
if (-not $driverProc) {
    Write-Host 'Starting WinAppDriver...' -ForegroundColor Cyan
    Start-Process -FilePath $winAppDriver -WindowStyle Minimized
    Start-Sleep -Seconds 2
}

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'run-cmdpal-dev.ps1') -BuildOnly
}

$testProject = Join-Path $repoRoot 'src\modules\cmdpal\Tests\Microsoft.CmdPal.UITests\Microsoft.CmdPal.UITests.csproj'
$msbuild = Join-Path ${env:ProgramFiles} 'Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'
if (-not (Test-Path $msbuild)) {
    $msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
}

Write-Host 'Building UI tests...' -ForegroundColor Cyan
& $msbuild $testProject /p:Configuration=Debug /p:Platform=x64 /t:Build /m:1 /v:minimal
if ($LASTEXITCODE -ne 0) {
    throw "UI test build failed with exit code $LASTEXITCODE"
}

$vstest = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.VisualStudio.PackageGroup.TestTools.Core -find 'Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' | Select-Object -First 1
if (-not $vstest) {
    throw 'vstest.console.exe not found. Install Visual Studio Test Tools.'
}

$testDll = Join-Path $repoRoot 'src\modules\cmdpal\Tests\Microsoft.CmdPal.UITests\x64\Debug\tests\Microsoft.CmdPal.UITests\net10.0-windows10.0.26100.0\Microsoft.CmdPal.UITests.dll'
if (-not (Test-Path $testDll)) {
    $testDll = Get-ChildItem -Path (Join-Path $repoRoot 'src\modules\cmdpal\Tests\Microsoft.CmdPal.UITests') -Recurse -Filter 'Microsoft.CmdPal.UITests.dll' -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
}

if (-not $testDll -or -not (Test-Path $testDll)) {
    throw 'Test assembly Microsoft.CmdPal.UITests.dll not found. Build the UI test project first.'
}

Write-Host "Test assembly: $testDll" -ForegroundColor DarkGray
Write-Host 'Running HoverActionTests...' -ForegroundColor Cyan
& $vstest $testDll /Tests:HoverActionTests /Logger:Console
exit $LASTEXITCODE
