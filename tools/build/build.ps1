<#
.SYNOPSIS
Light-weight wrapper to build local projects (solutions/projects) in the current working directory using helpers in build-common.ps1.

.DESCRIPTION
This script is intended for quick local builds. It dot-sources `build-common.ps1` and calls `BuildProjectsInDirectory` against the current directory. Use `-RestoreOnly` to only restore packages for local projects. If `-Platform` is omitted the script attempts to auto-detect the host platform.

.PARAMETER Platform
Target platform (e.g., 'x64', 'arm64'). If omitted the script will try to detect the host platform automatically.

.PARAMETER Configuration
Build configuration (e.g., 'Debug', 'Release'). Default: 'Debug'.

.PARAMETER Path
Optional directory path containing projects to build. If not specified, uses the current working directory.

.PARAMETER RestoreOnly
If specified, only perform package restore for local projects and skip the build steps for a solution file (i.e. .sln).

.PARAMETER ExtraArgs
Any remaining, positional arguments passed to the script are forwarded to MSBuild as additional arguments (e.g., '/p:CIBuild=true').

.EXAMPLE
.\tools\build\build.ps1
Builds any .sln/.csproj/.vcxproj in the current working directory (auto-detects Platform).

.EXAMPLE
.\tools\build\build.ps1 -Platform x64 -Configuration Release -Path "C:\MyProject\src"
Builds local projects in the specified directory for x64 Release.

.EXAMPLE
.\tools\build\build.ps1 -Platform x64 -Configuration Release
Builds local projects for x64 Release.

.EXAMPLE
.\tools\build\build.ps1 '/p:CIBuild=true' '/p:SomeOther=Value'
Pass additional MSBuild arguments; these are forwarded to the underlying msbuild calls.

.EXAMPLE
.\tools\build\build.ps1 -RestoreOnly '/p:CIBuild=true'
Only restores packages for local projects; ExtraArgs still forwarded to msbuild's restore phase.

.NOTES
- This file expects `build-common.ps1` to be located in the same folder and dot-sources it to load helper functions.
- ExtraArgs are captured using PowerShell's ValueFromRemainingArguments and joined before being passed to the helpers.
#>

param (
    [string]$Platform = '',
    [string]$Configuration = 'Debug',
    [string]$Path = '',
    [switch]$RestoreOnly,
    [Parameter(ValueFromRemainingArguments=$true)]
    [string[]]$ExtraArgs
)

. "$PSScriptRoot\build-common.ps1"

# Initialize Visual Studio dev environment
if (-not (Ensure-VsDevEnvironment)) { exit 1 }

# If user passed MSBuild-style args (e.g. './build.ps1 /p:CIBuild=true'),
# those will bind to $Platform/$Configuration; detect those and move them to ExtraArgs.
$positionalExtra = @()
if ($Platform -and $Platform -match '^[\/-]') {
    $positionalExtra += $Platform
    $Platform = ''
}
if ($Configuration -and $Configuration -match '^[\/-]') {
    $positionalExtra += $Configuration
    $Configuration = 'Debug'
}
if ($positionalExtra.Count -gt 0) {
    if (-not $ExtraArgs) { $ExtraArgs = @() }
    $ExtraArgs = $positionalExtra + $ExtraArgs
}

# Auto-detect platform when not provided
if (-not $Platform -or $Platform -eq '') {
    try {
        $Platform = Get-DefaultPlatform
        Write-Host ("[AUTO-PLATFORM] Detected platform: {0}" -f $Platform)
    } catch {
        Write-Warning "Failed to auto-detect platform; defaulting to x64"
        $Platform = 'x64'
    }
}

$cwd = if ($Path) {
    (Resolve-Path $Path).ProviderPath
} else {
    (Get-Location).ProviderPath
}
$extraArgsString = $null
if ($ExtraArgs -and $ExtraArgs.Count -gt 0) { $extraArgsString = ($ExtraArgs -join ' ') }

$built = BuildProjectsInDirectory -DirectoryPath $cwd -ExtraArgs $extraArgsString -Platform $Platform -Configuration $Configuration -RestoreOnly:$RestoreOnly
if ($built) {
    Write-Host "[BUILD] Local projects built; exiting."
    exit 0
} else {
    Write-Host "[BUILD] No local projects found in $cwd"
    exit 0
}
