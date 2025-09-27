<#
.SYNOPSIS
    [DEPRECATED] Ensure WiX Toolset 3.14 (build 3141) is installed and ready to use.

.DESCRIPTION
    This script is deprecated as PowerToys has migrated to WiX v5.
    WiX v3 is no longer used for building PowerToys installers as of release 0.94.
    This script is kept as a no-op to avoid breaking any existing local workflows.
    
    For WiX v5 tools, they are automatically installed via: dotnet tool install --global wix --version 5.0.2

.PARAMETER Force
    [Deprecated] Previously forced reinstallation even if the toolset was already detected.

.PARAMETER InstallDir
    [Deprecated] Previously specified the target installation path.

.EXAMPLE
    .\EnsureWix.ps1          # Displays deprecation warning
    .\EnsureWix.ps1 -Force   # Displays deprecation warning
#>
[CmdletBinding()]
param(
    [switch]$Force,
    [string]$InstallDir = 'C:\Program Files (x86)\WiX Toolset v3.14'
)

$ErrorActionPreference = 'Stop'

Write-Warning "ensure-wix.ps1 is deprecated. PowerToys now uses WiX v5 exclusively."
Write-Host "WiX v5 tools are automatically installed during the build process."
Write-Host "If you need WiX tools locally, use: dotnet tool install --global wix --version 5.0.2"
return

# Legacy WiX v3 installation code (disabled)
<#
$ProgressPreference    = 'SilentlyContinue'

# Download URLs and expected SHA-256 hashes
$WixDownloadUrl         = 'https://github.com/wixtoolset/wix3/releases/download/wix3141rtm/wix314.exe'
$WixBinariesDownloadUrl = 'https://github.com/wixtoolset/wix3/releases/download/wix3141rtm/wix314-binaries.zip'
$InstallerHashExpected  = '6BF6D03D6923D9EF827AE1D943B90B42B8EBB1B0F68EF6D55F868FA34C738A29'
$BinariesHashExpected   = '6AC824E1642D6F7277D0ED7EA09411A508F6116BA6FAE0AA5F2C7DAA2FF43D31'

# Check if WiX is already installed
$candlePath = Join-Path $InstallDir 'bin\candle.exe'
if (-not $Force -and (Test-Path $candlePath)) {
    Write-Host "WiX Toolset is already installed at `"$InstallDir`". Skipping installation."
    return
}

# Temp file paths
$tmpDir      = [IO.Path]::GetTempPath()
$installer   = Join-Path $tmpDir 'wix314.exe'
$binariesZip = Join-Path $tmpDir 'wix314-binaries.zip'

# Download installer and binaries
Write-Host 'Downloading WiX installer...'
Invoke-WebRequest -Uri $WixDownloadUrl         -OutFile $installer    -UseBasicParsing
Write-Host 'Downloading WiX binaries...'
Invoke-WebRequest -Uri $WixBinariesDownloadUrl -OutFile $binariesZip  -UseBasicParsing

# Verify SHA-256 hashes
Write-Host 'Verifying installer hash...'
if ((Get-FileHash -Algorithm SHA256 $installer).Hash -ne $InstallerHashExpected) {
    throw 'wix314.exe SHA256 hash mismatch'
}
Write-Host 'Verifying binaries hash...'
if ((Get-FileHash -Algorithm SHA256 $binariesZip).Hash -ne $BinariesHashExpected) {
    throw 'wix314-binaries.zip SHA256 hash mismatch'
}

# Perform silent installation
Write-Host 'Installing WiX Toolset silently...'
Start-Process -FilePath $installer -ArgumentList '/install','/quiet' -Wait

# Extract binaries and copy wix.targets
$expandDir = Join-Path $tmpDir 'wix-binaries'
if (Test-Path $expandDir) { Remove-Item $expandDir -Recurse -Force }
Expand-Archive -Path $binariesZip -DestinationPath $expandDir -Force
Copy-Item -Path (Join-Path $expandDir 'wix.targets') `
          -Destination (Join-Path $InstallDir  'wix.targets') -Force

Write-Host "WiX Toolset has been successfully installed at: $InstallDir"
#>
