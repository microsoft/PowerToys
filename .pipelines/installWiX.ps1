$ProgressPreference = 'SilentlyContinue'

# ===== DEPRECATED SCRIPT =====
# This script is deprecated as PowerToys has migrated to WiX v5.
# WiX v3 is no longer used for building PowerToys installers as of release 0.94.
# This script is kept as a no-op to avoid breaking any existing local workflows.
# Please use WiX v5 tools which are automatically installed via dotnet tool.
# =============================

Write-Warning "installWiX.ps1 is deprecated. PowerToys now uses WiX v5 exclusively."
Write-Host "WiX v5 tools are automatically installed during the build process."
Write-Host "If you need WiX tools locally, use: dotnet tool install --global wix --version 5.0.2"
exit 0

# Legacy WiX v3 installation code (disabled)
<#
$WixDownloadUrl = "https://github.com/wixtoolset/wix3/releases/download/wix3141rtm/wix314.exe"
$WixBinariesDownloadUrl = "https://github.com/wixtoolset/wix3/releases/download/wix3141rtm/wix314-binaries.zip"

# Download WiX binaries and verify their hash sums
Invoke-WebRequest -Uri $WixDownloadUrl -OutFile "$($ENV:Temp)\wix314.exe"
$Hash = (Get-FileHash -Algorithm SHA256 "$($ENV:Temp)\wix314.exe").Hash
if ($Hash -ne '6BF6D03D6923D9EF827AE1D943B90B42B8EBB1B0F68EF6D55F868FA34C738A29')
{
    Write-Error "$WixHash"
    throw "wix314.exe has unexpected SHA256 hash: $Hash"
}
Invoke-WebRequest -Uri $WixBinariesDownloadUrl -OutFile "$($ENV:Temp)\wix314-binaries.zip"
$Hash = (Get-FileHash -Algorithm SHA256 "$($ENV:Temp)\wix314-binaries.zip").Hash
if($Hash -ne '6AC824E1642D6F7277D0ED7EA09411A508F6116BA6FAE0AA5F2C7DAA2FF43D31')
{
    throw "wix314-binaries.zip has unexpected SHA256 hash: $Hash"
}

# Install WiX
Start-Process -Wait -FilePath "$($ENV:Temp)\wix314.exe" -ArgumentList "/install /quiet"

# Extract WiX binaries and copy wix.targets to the installed dir
Expand-Archive -Path "$($ENV:Temp)\wix314-binaries.zip" -Force -DestinationPath "$($ENV:Temp)"
Copy-Item -Path "$($ENV:Temp)\wix.targets" -Destination "C:\Program Files (x86)\WiX Toolset v3.14\"
#>