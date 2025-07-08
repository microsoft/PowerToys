# PowerToys Build Utilities Module
# Shared functions for PowerToys build scripts

function Test-MSBuildAvailability {
    <#
    .SYNOPSIS
    Checks if MSBuild is available and displays appropriate messages.
    #>
    Write-Host "Checking for MSBuild availability..." -ForegroundColor Yellow
    $msbuildExists = Get-Command msbuild -ErrorAction SilentlyContinue
    if (-not $msbuildExists) {
        Write-Host "MSBuild not found in the current environment." -ForegroundColor Red
        Write-Host "Please ensure you are running this script from a Visual Studio Developer Command Prompt," -ForegroundColor Red
        Write-Host "or that your VS Code terminal is configured to use the Developer PowerShell profile." -ForegroundColor Red
        Write-Host "Alternatively, ensure MSBuild.exe is in your system's PATH." -ForegroundColor Red
        Write-Host "You can find MSBuild typically in: C:\Program Files\Microsoft Visual Studio\2022\<Edition>\MSBuild\Current\Bin\MSBuild.exe" -ForegroundColor Red
        throw "MSBuild is required to build the solution. Please configure your environment."
    } else {
        Write-Host "MSBuild found at: $($msbuildExists.Source)" -ForegroundColor Green
    }
}

function Invoke-MSBuild {
    <#
    .SYNOPSIS
    Executes MSBuild with specified parameters and handles errors.
    
    .PARAMETER Solution
    Path to the solution file
    
    .PARAMETER Platform
    Target platform (x64, arm64, etc.)
    
    .PARAMETER Configuration
    Build configuration (Debug, Release)
    
    .PARAMETER Target
    MSBuild target (Build, Restore, Clean, etc.)
    
    .PARAMETER ExtraArgs
    Additional MSBuild arguments as a string    .PARAMETER UseMultiProcessor
    Whether to use multi-processor compilation (/m flag). Default is enabled.
    #>
    param (
        [Parameter(Mandatory)]
        [string]$Solution,
        
        [Parameter(Mandatory)]
        [string]$Platform,
        
        [Parameter(Mandatory)]
        [string]$Configuration,
        
        [string]$Target = "Build",
        
        [string]$ExtraArgs = "",
        
        [switch]$UseMultiProcessor = $true
    )

    $buildArgs = @(
        $Solution
        "/t:$Target"
        "/p:Platform=`"$Platform`""
        "/p:Configuration=$Configuration"
        "/verbosity:normal"
        "/nologo"    )
      # Add /m flag by default unless explicitly disabled
    if ($UseMultiProcessor -or $PSBoundParameters.ContainsKey('UseMultiProcessor') -eq $false) {
        $buildArgs += "/m"
    }
    
    if ($ExtraArgs) {
        $buildArgs += ($ExtraArgs -split ' ')
    }

    Write-Host "Executing: msbuild $($buildArgs -join ' ')" -ForegroundColor White
    & msbuild @buildArgs

    if ($LASTEXITCODE -ne 0) {
        throw "MSBuild failed for $Solution with target $Target. Exit code: $LASTEXITCODE"
    }
}

function Invoke-RestoreThenBuild {
    <#
    .SYNOPSIS
    Restores packages then builds a solution, handling parameter conflicts.
    
    .PARAMETER Solution
    Path to the solution file
    
    .PARAMETER Platform
    Target platform (x64, arm64, etc.)
    
    .PARAMETER Configuration
    Build configuration (Debug, Release)
    
    .PARAMETER ExtraArgs
    Additional arguments for the build step
    #>
    param (
        [Parameter(Mandatory)]
        [string]$Solution,
        
        [Parameter(Mandatory)]
        [string]$Platform,
        
        [Parameter(Mandatory)]
        [string]$Configuration,
        
        [string]$ExtraArgs = ""
    )    Write-Host "Restoring packages for: $Solution" -ForegroundColor Cyan
    Invoke-MSBuild -Solution $Solution -Platform $Platform -Configuration $Configuration -Target "Restore" -ExtraArgs "/p:RestorePackagesConfig=true"
    
    Write-Host "Building solution: $Solution" -ForegroundColor Cyan
    Invoke-MSBuild -Solution $Solution -Platform $Platform -Configuration $Configuration -Target "Build" -ExtraArgs $ExtraArgs -UseMultiProcessor
}

function Get-SolutionPath {
    <#
    .SYNOPSIS
    Gets the full path to a solution based on component name.
    
    .PARAMETER Component
    Component name (powertoys, bugreporttool, stylesreporttool)
    
    .PARAMETER ScriptDir
    Directory where the calling script is located
    #>
    param (
        [Parameter(Mandatory)]
        [ValidateSet("powertoys", "bugreporttool", "stylesreporttool")]
        [string]$Component,
        
        [Parameter(Mandatory)]
        [string]$ScriptDir
    )
    
    switch ($Component.ToLower()) {
        "powertoys" {
            $relativePath = "..\..\PowerToys.sln"
        }
        "bugreporttool" {
            $relativePath = "..\..\tools\BugReportTool\BugReportTool.sln"
        }
        "stylesreporttool" {
            $relativePath = "..\..\tools\StylesReportTool\StylesReportTool.sln"
        }
    }
    
    $solutionPath = Join-Path $ScriptDir $relativePath
    return [System.IO.Path]::GetFullPath($solutionPath)
}

function Write-BuildHeader {
    <#
    .SYNOPSIS
    Writes a consistent build header for PowerToys scripts.
    
    .PARAMETER Title
    Title to display
    #>
    param (
        [string]$Title = "PowerToys Build Script"
    )
    
    Write-Host $Title -ForegroundColor Green
    Write-Host ("=" * $Title.Length) -ForegroundColor Green
}

Export-ModuleMember -Function Test-MSBuildAvailability, Invoke-MSBuild, Invoke-RestoreThenBuild, Get-SolutionPath, Write-BuildHeader
