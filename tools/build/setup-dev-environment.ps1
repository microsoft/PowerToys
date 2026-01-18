<#
.SYNOPSIS
Sets up the development environment for building PowerToys.

.DESCRIPTION
This script automates the setup of prerequisites needed to build PowerToys locally:
- Enables Windows long path support (requires elevation)
- Installs required Visual Studio workloads from .vsconfig
- Initializes git submodules

Run this script once after cloning the repository to prepare your development environment.

.PARAMETER SkipLongPaths
Skip enabling long path support in Windows.

.PARAMETER SkipVSComponents
Skip installing Visual Studio components from .vsconfig.

.PARAMETER SkipSubmodules
Skip initializing git submodules.

.PARAMETER VSInstallPath
Path to Visual Studio installation. Default: auto-detected.

.PARAMETER Help
Show this help message.

.EXAMPLE
.\tools\build\setup-dev-environment.ps1
Runs the full setup process.

.EXAMPLE
.\tools\build\setup-dev-environment.ps1 -SkipVSComponents
Runs setup but skips Visual Studio component installation.

.EXAMPLE
.\tools\build\setup-dev-environment.ps1 -VSInstallPath "C:\Program Files\Microsoft Visual Studio\2022\Enterprise"
Runs setup with a custom Visual Studio installation path.

.NOTES
- Some operations require administrator privileges (long paths, VS component installation).
- If not running as administrator, the script will prompt for elevation for those steps.
- The script is idempotent and safe to run multiple times.
#>

param (
    [switch]$SkipLongPaths,
    [switch]$SkipVSComponents,
    [switch]$SkipSubmodules,
    [string]$VSInstallPath = '',
    [switch]$Help
)

if ($Help) {
    Get-Help $MyInvocation.MyCommand.Path -Detailed
    exit 0
}

$ErrorActionPreference = 'Stop'

# Find repository root
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$repoRoot = $scriptDir
while ($repoRoot -and -not (Test-Path (Join-Path $repoRoot "PowerToys.slnx"))) {
    $parent = Split-Path -Parent $repoRoot
    if ($parent -eq $repoRoot) {
        Write-Error "Could not find PowerToys repository root. Ensure this script is in the PowerToys repository."
        exit 1
    }
    $repoRoot = $parent
}

Write-Host "Repository: $repoRoot"
Write-Host ""

function Test-Administrator {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

$isAdmin = Test-Administrator

# Step 1: Enable Long Paths
if (-not $SkipLongPaths) {
    Write-Host "[1/3] Checking Windows long path support"

    $longPathsEnabled = $false
    try {
        $regValue = Get-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem" -Name "LongPathsEnabled" -ErrorAction SilentlyContinue
        $longPathsEnabled = ($regValue.LongPathsEnabled -eq 1)
    } catch {
        $longPathsEnabled = $false
    }

    if ($longPathsEnabled) {
        Write-Host "  Long paths already enabled" -ForegroundColor Green
    } elseif ($isAdmin) {
        Write-Host "  Enabling long paths..."
        try {
            Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem" -Name "LongPathsEnabled" -Value 1 -Type DWord
            Write-Host "  Long paths enabled" -ForegroundColor Green
        } catch {
            Write-Warning "  Failed to enable long paths: $_"
        }
    } else {
        Write-Warning "  Long paths not enabled. Run as Administrator to enable, or run manually:"
        Write-Host "  Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem' -Name 'LongPathsEnabled' -Value 1" -ForegroundColor DarkGray
    }
} else {
    Write-Host "[1/3] Skipping long path check" -ForegroundColor DarkGray
}

Write-Host ""

# Step 2: Install Visual Studio Components
if (-not $SkipVSComponents) {
    Write-Host "[2/3] Checking Visual Studio components"

    $vsConfigPath = Join-Path $repoRoot ".vsconfig"
    if (-not (Test-Path $vsConfigPath)) {
        Write-Warning "  .vsconfig not found at $vsConfigPath"
    } else {
        $vsWhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"

        if (-not $VSInstallPath -and (Test-Path $vsWhere)) {
            $VSInstallPath = & $vsWhere -latest -property installationPath 2>$null
        }

        if (-not $VSInstallPath) {
            $commonPaths = @(
                "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise",
                "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional",
                "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community"
            )
            foreach ($path in $commonPaths) {
                if (Test-Path $path) {
                    $VSInstallPath = $path
                    break
                }
            }
        }

        if (-not $VSInstallPath -or -not (Test-Path $VSInstallPath)) {
            Write-Warning "  Could not find Visual Studio 2022 installation"
            Write-Warning "  Please install Visual Studio 2022 and try again, or import .vsconfig manually"
        } else {
            Write-Host "  Found: $VSInstallPath" -ForegroundColor DarkGray

            $vsInstaller = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vs_installer.exe"

            if (Test-Path $vsInstaller) {
                Write-Host ""
                Write-Host "  To install required components:"
                Write-Host ""
                Write-Host "  Option A - Visual Studio Installer GUI:"
                Write-Host "    1. Open Visual Studio Installer"
                Write-Host "    2. Click 'More' > 'Import configuration'"
                Write-Host "    3. Select: $vsConfigPath"
                Write-Host ""
                Write-Host "  Option B - Command line (close VS first):"
                Write-Host "    & `"$vsInstaller`" modify --installPath `"$VSInstallPath`" --config `"$vsConfigPath`"" -ForegroundColor DarkGray
                Write-Host ""

                $choices = @(
                    [System.Management.Automation.Host.ChoiceDescription]::new("&Install", "Run VS Installer now"),
                    [System.Management.Automation.Host.ChoiceDescription]::new("&Skip", "Continue without installing")
                )

                try {
                    $decision = $Host.UI.PromptForChoice("", "Install VS components now?", $choices, 1)

                    if ($decision -eq 0) {
                        Write-Host "  Launching Visual Studio Installer..."
                        Write-Host "  Close Visual Studio if it's running." -ForegroundColor DarkGray
                        Start-Process -FilePath $vsInstaller -ArgumentList "modify", "--installPath", "`"$VSInstallPath`"", "--config", "`"$vsConfigPath`"" -Wait
                        Write-Host "  VS component installation completed" -ForegroundColor Green
                    } else {
                        Write-Host "  Skipped VS component installation"
                    }
                } catch {
                    Write-Host "  Non-interactive mode. Run the command above manually if needed." -ForegroundColor DarkGray
                }
            } else {
                Write-Warning "  Visual Studio Installer not found"
            }
        }
    }
} else {
    Write-Host "[2/3] Skipping VS component check" -ForegroundColor DarkGray
}

Write-Host ""

# Step 3: Initialize Git Submodules
if (-not $SkipSubmodules) {
    Write-Host "[3/3] Initializing git submodules"

    Push-Location $repoRoot
    try {
        $submoduleStatus = git submodule status 2>&1
        $uninitializedCount = ($submoduleStatus | Where-Object { $_ -match '^\-' }).Count

        if ($uninitializedCount -eq 0 -and $submoduleStatus) {
            Write-Host "  Submodules already initialized" -ForegroundColor Green
        } else {
            Write-Host "  Running: git submodule update --init --recursive" -ForegroundColor DarkGray
            git submodule update --init --recursive
            if ($LASTEXITCODE -eq 0) {
                Write-Host "  Submodules initialized" -ForegroundColor Green
            } else {
                Write-Warning "  Submodule initialization may have encountered issues (exit code: $LASTEXITCODE)"
            }
        }
    } catch {
        Write-Warning "  Failed to initialize submodules: $_"
    } finally {
        Pop-Location
    }
} else {
    Write-Host "[3/3] Skipping submodule initialization" -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "Setup complete" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Open PowerToys.slnx in Visual Studio 2022"
Write-Host "  2. If prompted to install additional components, click Install"
Write-Host "  3. Build the solution (Ctrl+Shift+B)"
Write-Host ""
Write-Host "Or build from command line:"
Write-Host "  .\tools\build\build.ps1" -ForegroundColor DarkGray
Write-Host ""
