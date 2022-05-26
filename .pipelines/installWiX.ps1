$WixDownloadUrl = "https://wixtoolset.org/downloads/v3.14.0.5722/wix314.exe"
$WixBinariesDownloadUrl = "https://wixtoolset.org/downloads/v3.14.0.5722/wix314-binaries.zip"

# Download WiX binaries
Invoke-WebRequest -Uri $WixDownloadUrl -OutFile "$($ENV:Temp)\wix314.exe"
Invoke-WebRequest -Uri $WixBinariesDownloadUrl -OutFile "$($ENV:Temp)\wix314-binaries.zip"

# Install WiX
Start-Process -Wait -FilePath "$($ENV:Temp)\wix314.exe" -ArgumentList "/install /quiet"

# Extract WiX binaries and copy wix.targets to the installed dir
Expand-Archive -Path "$($ENV:Temp)\wix314-binaries.zip" -Force -DestinationPath "$($ENV:Temp)"
Copy-Item -Path "$($ENV:Temp)\wix.targets" -Destination "C:\Program Files (x86)\WiX Toolset v3.14\"