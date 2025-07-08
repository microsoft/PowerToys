$ProgressPreference = 'SilentlyContinue'

# Get artifact path
$ArtifactPath = $ENV:BUILD_ARTIFACTSTAGINGDIRECTORY
if (-not $ArtifactPath) {
    throw "BUILD_ARTIFACTSTAGINGDIRECTORY environment variable not set"
}

# Since we only download PowerToysSetup-*.exe files, we can directly find it
$Installer = Get-ChildItem -Path $ArtifactPath -Filter 'PowerToysSetup-*.exe' | Select-Object -First 1

if (-not $Installer) {
    throw "PowerToys installer not found"
}

Write-Host "Installing PowerToys: $($Installer.Name)"

# Install PowerToys
$Process = Start-Process -Wait -FilePath $Installer.FullName -ArgumentList "/passive", "/norestart" -PassThru -NoNewWindow

if ($Process.ExitCode -eq 0 -or $Process.ExitCode -eq 3010) {
    Write-Host "✅ PowerToys installation completed successfully"
} else {
    throw "PowerToys installation failed with exit code: $($Process.ExitCode)"
}

# Verify installation
if (Test-Path "${env:ProgramFiles}\PowerToys\PowerToys.exe") {
    Write-Host "✅ PowerToys verified at: ${env:ProgramFiles}\PowerToys\PowerToys.exe"
} else {
    throw "PowerToys installation verification failed"
}