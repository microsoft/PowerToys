<#
.SYNOPSIS
    Builds the PowerToys Awake module.

.DESCRIPTION
    This script builds the Awake module and its dependencies using MSBuild.
    It automatically locates the Visual Studio installation and uses the
    appropriate MSBuild version.

.PARAMETER Configuration
    The build configuration. Valid values are 'Debug' or 'Release'.
    Default: Release

.PARAMETER Platform
    The target platform. Valid values are 'x64' or 'ARM64'.
    Default: x64

.PARAMETER Clean
    If specified, cleans the build output before building.

.PARAMETER Restore
    If specified, restores NuGet packages before building.

.EXAMPLE
    .\Build-Awake.ps1
    Builds Awake in Release configuration for x64.

.EXAMPLE
    .\Build-Awake.ps1 -Configuration Debug
    Builds Awake in Debug configuration for x64.

.EXAMPLE
    .\Build-Awake.ps1 -Clean -Restore
    Cleans, restores packages, and builds Awake.

.EXAMPLE
    .\Build-Awake.ps1 -Platform ARM64
    Builds Awake for ARM64 architecture.
#>

[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [ValidateSet('x64', 'ARM64')]
    [string]$Platform = 'x64',

    [switch]$Clean,

    [switch]$Restore
)

$ErrorActionPreference = 'Stop'

# Get script directory and project paths
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ModuleDir = Split-Path -Parent $ScriptDir
$RepoRoot = Resolve-Path (Join-Path $ModuleDir "..\..\..") | Select-Object -ExpandProperty Path
$AwakeProject = Join-Path $ModuleDir "Awake\Awake.csproj"
$ModuleServicesProject = Join-Path $ModuleDir "Awake.ModuleServices\Awake.ModuleServices.csproj"

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  PowerToys Awake Build Script" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Platform:      $Platform" -ForegroundColor Yellow
Write-Host "Project:       $AwakeProject" -ForegroundColor Yellow
Write-Host ""

# Find MSBuild
function Find-MSBuild {
    $vsWherePath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"

    if (Test-Path $vsWherePath) {
        $vsPath = & $vsWherePath -latest -requires Microsoft.Component.MSBuild -property installationPath
        if ($vsPath) {
            $msbuildPath = Join-Path $vsPath "MSBuild\Current\Bin\MSBuild.exe"
            if (Test-Path $msbuildPath) {
                return $msbuildPath
            }
        }
    }

    # Fallback: Search common locations
    $commonPaths = @(
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
    )

    foreach ($path in $commonPaths) {
        if (Test-Path $path) {
            return $path
        }
    }

    throw "MSBuild not found. Please install Visual Studio 2022 with the .NET and C++ workloads."
}

$MSBuild = Find-MSBuild
Write-Host "MSBuild:       $MSBuild" -ForegroundColor Yellow
Write-Host ""

# Verify project exists
if (-not (Test-Path $AwakeProject)) {
    throw "Project file not found: $AwakeProject"
}

# Build arguments
$BuildArgs = @(
    $AwakeProject,
    "/p:Configuration=$Configuration",
    "/p:Platform=$Platform",
    "/v:minimal",
    "/m"  # Parallel build
)

# Clean if requested
if ($Clean) {
    Write-Host "Cleaning previous build..." -ForegroundColor Cyan
    $CleanArgs = $BuildArgs + @("/t:Clean")
    & $MSBuild $CleanArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Clean failed with exit code $LASTEXITCODE"
    }
    Write-Host "Clean completed." -ForegroundColor Green
    Write-Host ""
}

# Restore if requested
if ($Restore) {
    Write-Host "Restoring NuGet packages..." -ForegroundColor Cyan
    $RestoreArgs = $BuildArgs + @("/t:Restore")
    & $MSBuild $RestoreArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Restore failed with exit code $LASTEXITCODE"
    }
    Write-Host "Restore completed." -ForegroundColor Green
    Write-Host ""
}

# Build Awake
Write-Host "Building Awake..." -ForegroundColor Cyan
$BuildArgs += "/t:Build"
& $MSBuild $BuildArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Build FAILED with exit code $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

# Build Awake.ModuleServices if it exists
if (Test-Path $ModuleServicesProject) {
    Write-Host ""
    Write-Host "Building Awake.ModuleServices..." -ForegroundColor Cyan
    $ModuleServicesArgs = @(
        $ModuleServicesProject,
        "/p:Configuration=$Configuration",
        "/p:Platform=$Platform",
        "/v:minimal",
        "/m",
        "/t:Build"
    )
    & $MSBuild $ModuleServicesArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "Build FAILED with exit code $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

# Output location
$OutputDir = Join-Path $RepoRoot "$Platform\$Configuration"
$OutputDll = Join-Path $OutputDir "PowerToys.Awake.dll"

Write-Host ""
Write-Host "======================================" -ForegroundColor Green
Write-Host "  Build SUCCEEDED" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host ""
Write-Host "Output: $OutputDir" -ForegroundColor Yellow

if (Test-Path $OutputDll) {
    $fileInfo = Get-Item $OutputDll
    Write-Host "  PowerToys.Awake.dll ($([math]::Round($fileInfo.Length / 1KB, 1)) KB)" -ForegroundColor Gray
}

Write-Host ""
