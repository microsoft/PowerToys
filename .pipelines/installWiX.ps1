$ProgressPreference = 'SilentlyContinue'

$WixDownloadUrl = "https://github.com/wixtoolset/wix3/releases/download/wix314rtm/wix314.exe"
$WixBinariesDownloadUrl = "https://github.com/wixtoolset/wix3/releases/download/wix314rtm/wix314-binaries.zip"

# Download WiX binaries and verify their hash sums
Invoke-WebRequest -Uri $WixDownloadUrl -OutFile "$($ENV:Temp)\wix314.exe"
$Hash = (Get-FileHash -Algorithm SHA256 "$($ENV:Temp)\wix314.exe").Hash
if ($Hash -ne '704439EA88FC9E5A3647EEDEEB45943F9A392E3D209F58512280130096847937')
{
    Write-Error "$WixHash"
    throw "wix314.exe has unexpected SHA256 hash: $Hash"
}
Invoke-WebRequest -Uri $WixBinariesDownloadUrl -OutFile "$($ENV:Temp)\wix314-binaries.zip"
$Hash = (Get-FileHash -Algorithm SHA256 "$($ENV:Temp)\wix314-binaries.zip").Hash
if($Hash -ne '13F067F38969FAF163D93A804B48EA0576790A202C8F10291F2000F0E356E934')
{
    throw "wix314-binaries.zip has unexpected SHA256 hash: $Hash"
}

# Install WiX
Start-Process -Wait -FilePath "$($ENV:Temp)\wix314.exe" -ArgumentList "/install /quiet"

# Extract WiX binaries and copy wix.targets to the installed dir
Expand-Archive -Path "$($ENV:Temp)\wix314-binaries.zip" -Force -DestinationPath "$($ENV:Temp)"
Copy-Item -Path "$($ENV:Temp)\wix.targets" -Destination "C:\Program Files (x86)\WiX Toolset v3.14\"