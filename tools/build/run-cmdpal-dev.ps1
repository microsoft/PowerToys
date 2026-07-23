#Requires -Version 5.1
<#
.SYNOPSIS
    Fast rebuild and launch loop for dev Command Palette (Microsoft.CmdPal.UI).

.EXAMPLE
    .\tools\build\run-cmdpal-dev.ps1

.EXAMPLE
    .\tools\build\run-cmdpal-dev.ps1 -BuildOnly
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$BuildOnly,
    [switch]$NoKill
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$msbuild = Join-Path ${env:ProgramFiles} 'Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'
if (-not (Test-Path $msbuild)) {
    $msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
}

if (-not $msbuild -or -not (Test-Path $msbuild)) {
    throw 'MSBuild not found. Install Visual Studio 2026 with the Desktop development workload.'
}

$project = Join-Path $repoRoot 'src\modules\cmdpal\Microsoft.CmdPal.UI\Microsoft.CmdPal.UI.csproj'
$exe = Join-Path $repoRoot "x64\$Configuration\WinUI3Apps\CmdPal\Microsoft.CmdPal.UI.exe"
$hoverLog = Join-Path $env:TEMP 'cmdpal-hover-debug.log'

Write-Host "Building Microsoft.CmdPal.UI ($Configuration | x64)..." -ForegroundColor Cyan
& $msbuild $project /p:Configuration=$Configuration /p:Platform=x64 /t:Build /m:1 /v:minimal
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE"
}

Write-Host "Build OK: $exe" -ForegroundColor Green

if ($BuildOnly) {
    return
}

if (-not $NoKill) {
    Get-Process Microsoft.CmdPal.UI -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "Stopping PID $($_.Id) (Microsoft.CmdPal.UI)..." -ForegroundColor Yellow
        Stop-Process -Id $_.Id -Force
    }
    Start-Sleep -Milliseconds 400
}

if (-not (Test-Path $exe)) {
    throw "Built executable not found: $exe"
}

Write-Host "Launching dev CmdPal..." -ForegroundColor Cyan
Write-Host "Hover debug log: $hoverLog" -ForegroundColor DarkGray
Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe -Parent)
