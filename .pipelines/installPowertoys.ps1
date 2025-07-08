$ProgressPreference = 'SilentlyContinue'

# Find PowerToys installer in artifact directory
$ArtifactPath = $ENV:BUILD_ARTIFACTSTAGINGDIRECTORY
if (-not $ArtifactPath) {
    throw "BUILD_ARTIFACTSTAGINGDIRECTORY environment variable not set"
}

$Installer = Get-ChildItem -Path $ArtifactPath -Recurse -Filter 'PowerToysSetup-*.exe' | Select-Object -First 1

if (-not $Installer) {
    throw "PowerToys installer not found in artifact directory"
}

Write-Host "Found PowerToys installer: $($Installer.Name)"

# Install PowerToys
Start-Process -Wait -FilePath $Installer.FullName -ArgumentList "/passive /norestart"

Write-Host "PowerToys installation completed"