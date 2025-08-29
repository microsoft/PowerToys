<#
.SYNOPSIS
Light-weight wrapper to build local projects (solutions/projects) in the current working directory using helpers in build-common.ps1.

.DESCRIPTION
This script is intended for quick local builds. It dot-sources `build-common.ps1` and calls `BuildProjectsInDirectory` against the current directory. Use `-RestoreOnly` to only restore packages for local projects.

.PARAMETER Platform
Target platform (e.g., 'x64', 'arm64'). Default: 'x64'.

.PARAMETER Configuration
Build configuration (e.g., 'Debug', 'Release'). Default: 'Debug'.

.PARAMETER RestoreOnly
If specified, only perform package restore for local projects and skip the build steps.

.EXAMPLE
.
\tools\build\build.ps1
Builds any .sln/.csproj/.vcxproj in the current working directory.

.EXAMPLE
.
\tools\build\build.ps1 -RestoreOnly
Only restores packages for local projects in the current directory.

.NOTES
This file expects `build-common.ps1` to be located in the same folder and dot-sources it to load helper functions.
#>

param (
    [string]$Platform = 'x64',
    [string]$Configuration = 'Debug',
    [switch]$RestoreOnly
)

. "$PSScriptRoot\build-common.ps1"

$cwd = (Get-Location).ProviderPath
if (BuildProjectsInDirectory $cwd $_ $Platform $Configuration $RestoreOnly) {
    Write-Host "[BUILD] Local projects built; exiting."
    exit 0
} else {
    Write-Host "[BUILD] No local projects found in $cwd"
    exit 0
}
