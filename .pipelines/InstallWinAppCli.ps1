[CmdletBinding()]
Param(
    # Target architecture: 'x64' or 'arm64'. Defaults to the pipeline's BuildPlatform variable.
    [string]$Platform = $env:BuildPlatform
)

$ProgressPreference = 'SilentlyContinue'
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\WinAppCli.Common.ps1"

# Both the standard harness and MWB use the same hash-verified official release. This
# portable CLI is not the MicrosoftWindows.WindowsSandbox client package.
$NormalizedPlatform = if ([string]::IsNullOrWhiteSpace($Platform)) { 'x64' } else { $Platform.ToLowerInvariant() }
$InstallDir = Join-Path $env:Temp 'winappcli'

# Fresh extract each run so a stale copy can't shadow the pinned version.
if (Test-Path $InstallDir)
{
    Remove-Item $InstallDir -Recurse -Force
}
$release = Save-WinAppCliRelease -Platform $NormalizedPlatform -Destination $InstallDir -WorkRoot $env:Temp
$winapp = Join-Path $InstallDir 'winapp.exe'

Write-Host "winappcli $($release.Tag) ($($release.Asset)) installed at: $winapp"

# The harness (WinappCli.TryResolveExecutable) checks WINAPP_CLI_PATH first; also prepend the
# folder to PATH so any other consumer in later steps resolves winapp.exe too.
Write-Host "##vso[task.setvariable variable=WINAPP_CLI_PATH]$winapp"
Write-Host "##vso[task.prependpath]$(Split-Path -Parent $winapp)"

& $winapp --version
if ($LASTEXITCODE -ne 0)
{
    throw "winapp.exe failed to run ('--version' exited with $LASTEXITCODE)."
}
