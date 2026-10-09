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

# Force UTF-8 output for Unicode characters
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$ErrorActionPreference = 'Stop'
$script:StartTime = Get-Date

# Get script directory and project paths
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ModuleDir = Split-Path -Parent $ScriptDir
$RepoRoot = Resolve-Path (Join-Path $ModuleDir "..\..\..") | Select-Object -ExpandProperty Path
$AwakeProject = Join-Path $ModuleDir "Awake\Awake.csproj"
$ModuleServicesProject = Join-Path $ModuleDir "Awake.ModuleServices\Awake.ModuleServices.csproj"

# ============================================================================
# Modern UI Components
# ============================================================================

$script:Colors = @{
    Primary   = "Cyan"
    Success   = "Green"
    Error     = "Red"
    Warning   = "Yellow"
    Muted     = "DarkGray"
    Accent    = "Magenta"
    White     = "White"
}

# Box drawing characters (not emojis)
$script:UI = @{
    BoxH   = [char]0x2500  # Horizontal line
    BoxV   = [char]0x2502  # Vertical line
    BoxTL  = [char]0x256D  # Top-left corner (rounded)
    BoxTR  = [char]0x256E  # Top-right corner (rounded)
    BoxBL  = [char]0x2570  # Bottom-left corner (rounded)
    BoxBR  = [char]0x256F  # Bottom-right corner (rounded)
    TreeL  = [char]0x2514  # Tree last item
    TreeT  = [char]0x251C  # Tree item
}

# Braille spinner frames (the npm-style spinner)
$script:SpinnerFrames = @(
    [char]0x280B,  # ⠋
    [char]0x2819,  # ⠙
    [char]0x2839,  # ⠹
    [char]0x2838,  # ⠸
    [char]0x283C,  # ⠼
    [char]0x2834,  # ⠴
    [char]0x2826,  # ⠦
    [char]0x2827,  # ⠧
    [char]0x2807,  # ⠇
    [char]0x280F   # ⠏
)

function Get-ElapsedTime {
    $elapsed = (Get-Date) - $script:StartTime
    if ($elapsed.TotalSeconds -lt 60) {
        return "$([math]::Round($elapsed.TotalSeconds, 1))s"
    } else {
        return "$([math]::Floor($elapsed.TotalMinutes))m $($elapsed.Seconds)s"
    }
}

function Write-Header {
    Write-Host ""
    Write-Host "   Awake Build" -ForegroundColor $Colors.White
    Write-Host "   $Platform / $Configuration" -ForegroundColor $Colors.Muted
    Write-Host ""
}

function Write-Phase {
    param([string]$Name)
    Write-Host ""
    Write-Host "   $Name" -ForegroundColor $Colors.Accent
    Write-Host ""
}

function Write-Task {
    param([string]$Name, [switch]$Last)
    $tree = if ($Last) { $UI.TreeL } else { $UI.TreeT }
    Write-Host "   $tree$($UI.BoxH)$($UI.BoxH) " -NoNewline -ForegroundColor $Colors.Muted
    Write-Host $Name -NoNewline -ForegroundColor $Colors.White
}

function Write-TaskStatus {
    param([string]$Status, [string]$Time, [switch]$Failed)
    if ($Failed) {
        Write-Host " FAIL" -ForegroundColor $Colors.Error
    } else {
        Write-Host " " -NoNewline
        Write-Host $Status -NoNewline -ForegroundColor $Colors.Success
        if ($Time) {
            Write-Host " ($Time)" -ForegroundColor $Colors.Muted
        } else {
            Write-Host ""
        }
    }
}

function Write-BuildTree {
    param([string[]]$Items)
    $count = $Items.Count
    for ($i = 0; $i -lt $count; $i++) {
        $isLast = ($i -eq $count - 1)
        $tree = if ($isLast) { $UI.TreeL } else { $UI.TreeT }
        Write-Host "      $tree$($UI.BoxH) " -NoNewline -ForegroundColor $Colors.Muted
        Write-Host $Items[$i] -ForegroundColor $Colors.Muted
    }
}

function Write-SuccessBox {
    param([string]$Time, [string]$Output, [string]$Size)

    $width = 44
    $lineChar = [string]$UI.BoxH
    $line = $lineChar * ($width - 2)

    Write-Host ""
    Write-Host "   $($UI.BoxTL)$line$($UI.BoxTR)" -ForegroundColor $Colors.Success

    # Title row
    $title = "  BUILD SUCCESSFUL"
    $titlePadding = $width - 2 - $title.Length
    Write-Host "   $($UI.BoxV)" -NoNewline -ForegroundColor $Colors.Success
    Write-Host $title -NoNewline -ForegroundColor $Colors.White
    Write-Host (" " * $titlePadding) -NoNewline
    Write-Host "$($UI.BoxV)" -ForegroundColor $Colors.Success

    # Empty row
    Write-Host "   $($UI.BoxV)" -NoNewline -ForegroundColor $Colors.Success
    Write-Host (" " * ($width - 2)) -NoNewline
    Write-Host "$($UI.BoxV)" -ForegroundColor $Colors.Success

    # Time row
    $timeText = "  Completed in $Time"
    $timePadding = $width - 2 - $timeText.Length
    Write-Host "   $($UI.BoxV)" -NoNewline -ForegroundColor $Colors.Success
    Write-Host $timeText -NoNewline -ForegroundColor $Colors.Muted
    Write-Host (" " * $timePadding) -NoNewline
    Write-Host "$($UI.BoxV)" -ForegroundColor $Colors.Success

    # Output row
    $outText = "  Output: $Output ($Size)"
    if ($outText.Length -gt ($width - 2)) {
        $outText = $outText.Substring(0, $width - 5) + "..."
    }
    $outPadding = $width - 2 - $outText.Length
    if ($outPadding -lt 0) { $outPadding = 0 }
    Write-Host "   $($UI.BoxV)" -NoNewline -ForegroundColor $Colors.Success
    Write-Host $outText -NoNewline -ForegroundColor $Colors.Muted
    Write-Host (" " * $outPadding) -NoNewline
    Write-Host "$($UI.BoxV)" -ForegroundColor $Colors.Success

    Write-Host "   $($UI.BoxBL)$line$($UI.BoxBR)" -ForegroundColor $Colors.Success
    Write-Host ""
}

function Write-ErrorBox {
    param([string]$Message)

    $width = 44
    $lineChar = [string]$UI.BoxH
    $line = $lineChar * ($width - 2)

    Write-Host ""
    Write-Host "   $($UI.BoxTL)$line$($UI.BoxTR)" -ForegroundColor $Colors.Error
    $title = "  BUILD FAILED"
    $titlePadding = $width - 2 - $title.Length
    Write-Host "   $($UI.BoxV)" -NoNewline -ForegroundColor $Colors.Error
    Write-Host $title -NoNewline -ForegroundColor $Colors.White
    Write-Host (" " * $titlePadding) -NoNewline
    Write-Host "$($UI.BoxV)" -ForegroundColor $Colors.Error
    Write-Host "   $($UI.BoxBL)$line$($UI.BoxBR)" -ForegroundColor $Colors.Error
    Write-Host ""
}

# ============================================================================
# Build Functions
# ============================================================================

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

    throw "MSBuild not found. Please install Visual Studio 2022."
}

function Invoke-BuildWithSpinner {
    param(
        [string]$TaskName,
        [string]$MSBuildPath,
        [string[]]$Arguments,
        [switch]$ShowProjects,
        [switch]$IsLast
    )

    $taskStart = Get-Date
    $isInteractive = [Environment]::UserInteractive -and -not [Console]::IsOutputRedirected

    # Only write initial task line in interactive mode (will be overwritten by spinner)
    if ($isInteractive) {
        Write-Task $TaskName -Last:$IsLast
        Write-Host " " -NoNewline
    }

    # Start MSBuild process
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $MSBuildPath
    $psi.Arguments = $Arguments -join " "
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    $psi.WorkingDirectory = $RepoRoot

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi

    # Collect output asynchronously
    $outputBuilder = [System.Text.StringBuilder]::new()
    $errorBuilder = [System.Text.StringBuilder]::new()

    $outputHandler = {
        if (-not [String]::IsNullOrEmpty($EventArgs.Data)) {
            $Event.MessageData.AppendLine($EventArgs.Data)
        }
    }

    $outputEvent = Register-ObjectEvent -InputObject $process -EventName OutputDataReceived -Action $outputHandler -MessageData $outputBuilder
    $errorEvent = Register-ObjectEvent -InputObject $process -EventName ErrorDataReceived -Action $outputHandler -MessageData $errorBuilder

    $process.Start() | Out-Null
    $process.BeginOutputReadLine()
    $process.BeginErrorReadLine()

    # Animate spinner while process is running
    $frameIndex = 0

    while (-not $process.HasExited) {
        if ($isInteractive) {
            $frame = $script:SpinnerFrames[$frameIndex]
            Write-Host "`r   $($UI.TreeL)$($UI.BoxH)$($UI.BoxH) $TaskName $frame " -NoNewline
            $frameIndex = ($frameIndex + 1) % $script:SpinnerFrames.Count
        }
        Start-Sleep -Milliseconds 80
    }

    $process.WaitForExit()

    Unregister-Event -SourceIdentifier $outputEvent.Name
    Unregister-Event -SourceIdentifier $errorEvent.Name
    Remove-Job -Name $outputEvent.Name -Force -ErrorAction SilentlyContinue
    Remove-Job -Name $errorEvent.Name -Force -ErrorAction SilentlyContinue

    $exitCode = $process.ExitCode
    $output = $outputBuilder.ToString() -split "`n"
    $errors = $errorBuilder.ToString()

    $taskElapsed = (Get-Date) - $taskStart
    $elapsed = "$([math]::Round($taskElapsed.TotalSeconds, 1))s"

    # Write final status line
    $tree = if ($IsLast) { $UI.TreeL } else { $UI.TreeT }
    if ($isInteractive) {
        Write-Host "`r" -NoNewline
    }
    Write-Host "   $tree$($UI.BoxH)$($UI.BoxH) " -NoNewline -ForegroundColor $Colors.Muted
    Write-Host $TaskName -NoNewline -ForegroundColor $Colors.White

    if ($exitCode -ne 0) {
        Write-TaskStatus "FAIL" -Failed
        Write-Host ""
        foreach ($line in $output) {
            if ($line -match "error\s+\w+\d*:") {
                Write-Host "      x $line" -ForegroundColor $Colors.Error
            }
        }
        return @{ Success = $false; Output = $output; ExitCode = $exitCode }
    }

    Write-TaskStatus "done" $elapsed

    # Show built projects
    if ($ShowProjects) {
        $projects = @()
        foreach ($line in $output) {
            if ($line -match "^\s*(\S+)\s+->\s+(.+)$") {
                $project = $Matches[1]
                $fileName = Split-Path $Matches[2] -Leaf
                $projects += "$project -> $fileName"
            }
        }
        if ($projects.Count -gt 0) {
            Write-BuildTree $projects
        }
    }

    return @{ Success = $true; Output = $output; ExitCode = 0 }
}

# ============================================================================
# Main
# ============================================================================

# Verify project exists
if (-not (Test-Path $AwakeProject)) {
    Write-Host ""
    Write-Host "   x Project not found: $AwakeProject" -ForegroundColor $Colors.Error
    exit 1
}

$MSBuild = Find-MSBuild

# Display header
Write-Header

# Build arguments base
$BaseArgs = @(
    "/p:Configuration=$Configuration",
    "/p:Platform=$Platform",
    "/v:minimal",
    "/nologo",
    "/m"
)

# Clean phase
if ($Clean) {
    Write-Phase "Cleaning"
    $cleanArgs = @($AwakeProject) + $BaseArgs + @("/t:Clean")
    $result = Invoke-BuildWithSpinner -TaskName "Build artifacts" -MSBuildPath $MSBuild -Arguments $cleanArgs -IsLast
    if (-not $result.Success) {
        Write-ErrorBox
        exit $result.ExitCode
    }
}

# Restore phase
if ($Restore) {
    Write-Phase "Restoring"
    $restoreArgs = @($AwakeProject) + $BaseArgs + @("/t:Restore")
    $result = Invoke-BuildWithSpinner -TaskName "NuGet packages" -MSBuildPath $MSBuild -Arguments $restoreArgs -IsLast
    if (-not $result.Success) {
        Write-ErrorBox
        exit $result.ExitCode
    }
}

# Build phase
Write-Phase "Building"

$hasModuleServices = Test-Path $ModuleServicesProject

# Build Awake
$awakeArgs = @($AwakeProject) + $BaseArgs + @("/t:Build")
$result = Invoke-BuildWithSpinner -TaskName "Awake" -MSBuildPath $MSBuild -Arguments $awakeArgs -ShowProjects -IsLast:(-not $hasModuleServices)
if (-not $result.Success) {
    Write-ErrorBox
    exit $result.ExitCode
}

# Build ModuleServices
if ($hasModuleServices) {
    $servicesArgs = @($ModuleServicesProject) + $BaseArgs + @("/t:Build")
    $result = Invoke-BuildWithSpinner -TaskName "Awake.ModuleServices" -MSBuildPath $MSBuild -Arguments $servicesArgs -ShowProjects -IsLast
    if (-not $result.Success) {
        Write-ErrorBox
        exit $result.ExitCode
    }
}

# Summary
$OutputDir = Join-Path $RepoRoot "$Platform\$Configuration"
$AwakeDll = Join-Path $OutputDir "PowerToys.Awake.dll"
$elapsed = Get-ElapsedTime

if (Test-Path $AwakeDll) {
    $size = "$([math]::Round((Get-Item $AwakeDll).Length / 1KB, 1)) KB"
    Write-SuccessBox -Time $elapsed -Output "PowerToys.Awake.dll" -Size $size
} else {
    Write-SuccessBox -Time $elapsed -Output $OutputDir -Size "N/A"
}
