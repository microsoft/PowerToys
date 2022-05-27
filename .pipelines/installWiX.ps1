$ProgressPreference = 'SilentlyContinue'

$WixDownloadUrl = "https://wixtoolset.org/downloads/v3.14.0.5722/wix314.exe"
$WixBinariesDownloadUrl = "https://wixtoolset.org/downloads/v3.14.0.5722/wix314-binaries.zip"

# Download WiX binaries and verify their hash sums
Invoke-WebRequest -Uri $WixDownloadUrl -OutFile "$($ENV:Temp)\wix314.exe"
$Hash = (Get-FileHash -Algorithm SHA256 "$($ENV:Temp)\wix314.exe").Hash
if ($Hash -ne 'B74ED29F1377AA759E764EDEF43B1E4C4312A7A4CED77108D2446F7117EF5D3B')
{
    Write-Error "$WixHash"
    throw "wix314.exe has unexpected SHA256 hash: $Hash"
}
Invoke-WebRequest -Uri $WixBinariesDownloadUrl -OutFile "$($ENV:Temp)\wix314-binaries.zip"
$Hash = (Get-FileHash -Algorithm SHA256 "$($ENV:Temp)\wix314-binaries.zip").Hash
if($Hash -ne 'FCBE136AB3D616B983C5BE19B46521745F842B7327BF2BC7011FD26DBE277F93')
{
    throw "wix314-binaries.zip has unexpected SHA256 hash: $Hash"
}

# Install WiX
Start-Process -Wait -FilePath "$($ENV:Temp)\wix314.exe" -ArgumentList "/install /quiet"

# Extract WiX binaries and copy wix.targets to the installed dir
Expand-Archive -Path "$($ENV:Temp)\wix314-binaries.zip" -Force -DestinationPath "$($ENV:Temp)"
Copy-Item -Path "$($ENV:Temp)\wix.targets" -Destination "C:\Program Files (x86)\WiX Toolset v3.14\"