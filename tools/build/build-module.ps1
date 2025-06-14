#Requires -Version 5.1
<#
.SYNOPSIS
    Lists PowerToys modules or builds an individual PowerToys module (csproj/vcxproj).
.DESCRIPTION
    This script provides functionality to either list all available modules
    within the PowerToys source structure (src/modules) or to build a specific
    module. Building involves locating the .csproj or .vcxproj file for the
    module, restoring NuGet packages, and then compiling the project using MSBuild.
    All characters in this script are pure ASCII.
.PARAMETER ModuleName
    The name of the module to build. This should correspond to a directory name
    under src/modules. Required when Action is 'Build'.
.PARAMETER Action
    Specifies the operation to perform.
    'List': Lists all available modules. (Default)
    'Build': Builds the specified module.
.PARAMETER Configuration
    The build configuration (e.g., 'Debug', 'Release'). Defaults to 'Debug'.
.PARAMETER Platform
    The build platform (e.g., 'x64', 'ARM64'). Defaults to 'x64'.
.EXAMPLE
    powershell.exe -ExecutionPolicy Bypass -File .\\build-module.ps1 -Action List
    Lists all available PowerToys modules.
.EXAMPLE
    powershell.exe -ExecutionPolicy Bypass -File .\\build-module.ps1 -Action Build -ModuleName AlwaysOnTop
    Builds the 'AlwaysOnTop' module with Debug configuration and x64 platform.
.EXAMPLE
    powershell.exe -ExecutionPolicy Bypass -File .\\build-module.ps1 -Action Build -ModuleName PowerRename -Configuration Release -Platform ARM64
    Builds the 'PowerRename' module with Release configuration and ARM64 platform.
.NOTES
    For the 'Build' action, it is recommended to run this script from a
    Developer Command Prompt for Visual Studio, as this ensures MSBuild
    and its dependencies are correctly configured in the PATH.
    The script attempts to find MSBuild in common locations if not readily available.
#>
param (
    [string]$ModuleName,
    [ValidateSet('List', 'Build')]
    [string]$Action = 'List',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [ValidateSet('x64', 'ARM64')]
    [string]$Platform = 'ARM64'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Determine the root of the PowerToys repository based on script location
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$RepoRoot = Resolve-Path (Join-Path $ScriptRoot '..\..\')
$ModulesPath = Join-Path $RepoRoot 'src\modules'

# Function to find MSBuild.exe
function Get-MsBuildPath {
    # Prioritize MSBuild from VSWhere if available (more robust)
    # For simplicity in this script, we check common known paths first.
    # Users are encouraged to run from a Developer Command Prompt.

    $vsInstallPaths = @(
        Join-Path $env:ProgramFiles 'Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe'
        Join-Path $env:ProgramFiles 'Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe'
        Join-Path $env:ProgramFiles 'Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
        Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe'
        Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe'
        Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
        # Add paths for VS 2019 if necessary
        Join-Path $env:ProgramFiles 'Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe'
        Join-Path $env:ProgramFiles 'Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe'
        Join-Path $env:ProgramFiles 'Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe'
        Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe'
        Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe'
        Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe'
    )

    foreach ($pathAttempt in $vsInstallPaths) {
        if (Test-Path $pathAttempt) {
            Write-Verbose "Found MSBuild at: $pathAttempt"
            return $pathAttempt
        }
    }

    # Fallback: check if msbuild is in PATH
    $msbuildInPath = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($msbuildInPath) {
        Write-Verbose "Found MSBuild in PATH: $($msbuildInPath.Source)"
        return $msbuildInPath.Source
    }
    
    return $null
}

# Function to locate the project file for a given module
function Get-ModuleProjectFile {
    param (
        [string]$SelectedModule
    )
    $ModulePath = Join-Path $ModulesPath $SelectedModule
    if (-not (Test-Path $ModulePath -PathType Container)) {
        Write-Error "Module directory '$SelectedModule' not found at '$ModulePath'."
        return $null
    }

    # Search for .csproj or .vcxproj files, preferring those with the module name.
    $ProjectFiles = Get-ChildItem -Path $ModulePath -Recurse -File | Where-Object { $_.Name -like "*.csproj" -or $_.Name -like "*.vcxproj" }
    
    if ($ProjectFiles.Count -eq 0) {
        Write-Error "No .csproj or .vcxproj file found for module '$SelectedModule' in '$ModulePath' or its subdirectories."
        return $null
    }

    # Prefer project file that matches module name (e.g., ModuleName.csproj)
    $PreferredProjectFile = $ProjectFiles | Where-Object { $_.BaseName -eq $SelectedModule } | Select-Object -First 1
    if ($PreferredProjectFile) {
        Write-Verbose "Found preferred project file: $($PreferredProjectFile.FullName)"
        return $PreferredProjectFile.FullName
    }

    # If no direct match, return the first one found.
    # This might need refinement if modules have multiple projects.
    $FirstProjectFile = $ProjectFiles | Select-Object -First 1
    Write-Verbose "Found project file (first match): $($FirstProjectFile.FullName)"
    return $FirstProjectFile.FullName
}

# Main script logic
if ($Action -eq 'List') {
    Write-Host "Available PowerToys modules (directories in '$($ModulesPath)'):"
    Get-ChildItem -Path $ModulesPath -Directory | ForEach-Object {
        Write-Host "- $($_.Name)"
    }
    Write-Host "`nTo build a module, use: -Action Build -ModuleName <ModuleName>"
}
elseif ($Action -eq 'Build') {
    if ([string]::IsNullOrWhiteSpace($ModuleName)) {
        Write-Error "The -ModuleName parameter is required when Action is 'Build'."
        exit 1
    }

    Write-Host "Attempting to build module: $ModuleName"
    Write-Host "Configuration: $Configuration, Platform: $Platform"

    $MsbuildExe = Get-MsBuildPath
    if (-not $MsbuildExe) {
        Write-Error "MSBuild.exe could not be found. Please ensure Visual Studio is installed and/or run this script from a Developer Command Prompt."
        exit 1
    }
    Write-Host "Using MSBuild: $MsbuildExe"

    $ProjectFilePath = Get-ModuleProjectFile -SelectedModule $ModuleName
    if (-not $ProjectFilePath) {
        # Error already written by Get-ModuleProjectFile
        exit 1
    }

    Write-Host "Found project file: $ProjectFilePath"

    # Restore packages
    Write-Host "Step 1: Restoring packages for $ProjectFilePath..."
    $RestoreArgs = @(
        $ProjectFilePath
        '/t:Restore'
        "/p:Configuration=$Configuration"
        "/p:Platform=$Platform"
        '/p:RestorePackagesConfig=true' # Ensures packages.config are restored if present
    )
    Write-Verbose "Executing: $MsbuildExe $RestoreArgs"
    & $MsbuildExe $RestoreArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Package restore failed for '$ProjectFilePath' with exit code $LASTEXITCODE."
        exit $LASTEXITCODE
    }
    Write-Host "Package restore completed successfully."

    # Build project
    Write-Host "Step 2: Building project $ProjectFilePath..."
    $BuildArgs = @(
        $ProjectFilePath
        "/p:Configuration=$Configuration"
        "/p:Platform=$Platform"
    )
    Write-Verbose "Executing: $MsbuildExe $BuildArgs"
    & $MsbuildExe $BuildArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed for '$ProjectFilePath' with exit code $LASTEXITCODE."
        exit $LASTEXITCODE
    }
    Write-Host "Build completed successfully for module '$ModuleName'."
}
else {
    # This case should not be reached due to ValidateSet on Action parameter
    Write-Error "Invalid Action specified. Choose 'List' or 'Build'."
    exit 1
}

Write-Host "Script finished."
