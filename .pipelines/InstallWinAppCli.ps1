[CmdletBinding()]
Param(
    # Target architecture: 'x64' or 'arm64'. Defaults to the pipeline's BuildPlatform variable.
    [string]$Platform = $env:BuildPlatform
)

$ProgressPreference = 'SilentlyContinue'
$ErrorActionPreference = 'Stop'

# Pinned to the winappcli version the UITestAutomation.Next harness is validated against. Using
# the standalone CLI zip (rather than the MSIX / winget) keeps this working on agents that lack
# the App Installer and avoids MSIX registration entirely.
$Version = 'v0.3.2'
$NormalizedPlatform = if ([string]::IsNullOrWhiteSpace($Platform)) { 'x64' } else { $Platform.ToLowerInvariant() }

switch ($NormalizedPlatform)
{
    'arm64'
    {
        $Asset = 'winappcli-arm64.zip'
        $ExpectedHash = 'dfe9d6eb70618665e4adcee989be8ecd076bfd387714a35a5b38597196fed093'
    }
    default
    {
        $Asset = 'winappcli-x64.zip'
        $ExpectedHash = '231373a4605ce7749172a70534ebab9305f91116e7f68d25cc73051372a6c579'
    }
}

$DownloadUrl = "https://github.com/microsoft/winappCli/releases/download/$Version/$Asset"
$ZipPath = Join-Path $env:Temp $Asset
$InstallDir = Join-Path $env:Temp 'winappcli'

Write-Host "Downloading winappcli $Version ($Asset) from $DownloadUrl"
Invoke-WebRequest -Uri $DownloadUrl -OutFile $ZipPath

# Verify the download against the published SHA256 before trusting it.
$Hash = (Get-FileHash -Algorithm SHA256 $ZipPath).Hash
if ($Hash -ne $ExpectedHash)
{
    throw "$Asset has unexpected SHA256 hash: $Hash (expected $ExpectedHash)"
}

# Fresh extract each run so a stale copy can't shadow the pinned version.
if (Test-Path $InstallDir)
{
    Remove-Item $InstallDir -Recurse -Force
}
Expand-Archive -Path $ZipPath -DestinationPath $InstallDir -Force

# Clear Mark-of-the-Web in case the agent applied it, so the CLI runs non-interactively.
Get-ChildItem -Path $InstallDir -Recurse | Unblock-File -ErrorAction SilentlyContinue

$winapp = Get-ChildItem -Path $InstallDir -Recurse -Filter 'winapp.exe' | Select-Object -First 1 -ExpandProperty FullName
if (-not $winapp)
{
    throw "winapp.exe was not found after extracting $Asset to $InstallDir."
}

Write-Host "winappcli installed at: $winapp"

# The harness (WinappCli.TryResolveExecutable) checks WINAPP_CLI_PATH first; also prepend the
# folder to PATH so any other consumer in later steps resolves winapp.exe too.
Write-Host "##vso[task.setvariable variable=WINAPP_CLI_PATH]$winapp"
Write-Host "##vso[task.prependpath]$(Split-Path -Parent $winapp)"

& $winapp --version
if ($LASTEXITCODE -ne 0)
{
    throw "winapp.exe failed to run ('--version' exited with $LASTEXITCODE)."
}
