$ProgressPreference = 'SilentlyContinue'

# Find PowerToys installer in artifact directory
$ArtifactPath = $ENV:BUILD_ARTIFACTSTAGINGDIRECTORY
if (-not $ArtifactPath) {
    throw "BUILD_ARTIFACTSTAGINGDIRECTORY environment variable not set"
}

$Installer = Get-ChildItem -Path $ArtifactPath -Recurse -Filter 'PowerToysUserSetup-*.exe' | Select-Object -First 1

if (-not $Installer) {
    throw "PowerToys installer not found in artifact directory"
}

Write-Host "Found PowerToys installer: $($Installer.Name)"

# Install PowerToys
$Process = Start-Process -Wait -FilePath $Installer.FullName -ArgumentList "/passive /norestart" -PassThru

if ($Process.ExitCode -ne 0 -and $Process.ExitCode -ne 3010) {
    throw "PowerToys installation failed with exit code: $($Process.ExitCode)"
}

# Verify installation
$PowerToysPath = "${env:LOCALAPPDATA}\PowerToys\PowerToys.exe"
if (-not (Test-Path $PowerToysPath)) {
    $PowerToysPath = "${env:LOCALAPPDATA}\PowerToys\PowerToys.exe"
}

if (Test-Path $PowerToysPath) {
    Write-Host "PowerToys installation completed successfully"
} else {
    throw "PowerToys installation verification failed - executable not found"
}