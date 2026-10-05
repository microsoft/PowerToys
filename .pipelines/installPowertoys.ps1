param(
    [Parameter()]
    [ValidateSet("Machine", "PerUser")]
    [string]$InstallMode = "Machine",

    # Folder that contains the PowerToys installer. Defaults to the build staging directory used
    # by the official-build path (installer downloaded via DownloadPipelineArtifact@2). The
    # full-build (buildNow) path passes the downloaded pipeline-artifact folder instead, since
    # the installer ships inside that build's own artifact.
    [Parameter()]
    [string]$ArtifactPath = $ENV:BUILD_ARTIFACTSTAGINGDIRECTORY
)

$ProgressPreference = 'SilentlyContinue'

if (-not $ArtifactPath) {
    throw "Installer path not provided. Pass -ArtifactPath or set BUILD_ARTIFACTSTAGINGDIRECTORY."
}

# Since we only download PowerToysSetup-*.exe files, we can directly find it
$Installer = Get-ChildItem -Path $ArtifactPath -Filter 'PowerToys*.exe' | Select-Object -First 1

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
if ($InstallMode -eq "PerUser") {
    if (Test-Path "${env:LOCALAPPDATA}\PowerToys\PowerToys.exe") {
        Write-Host "✅ PowerToys verified at: ${env:LOCALAPPDATA}\PowerToys\PowerToys.exe"
    } else {
        throw "PowerToys installation verification failed"
    }
} else {
    if (Test-Path "${env:ProgramFiles}\PowerToys\PowerToys.exe") {
        Write-Host "✅ PowerToys verified at: ${env:ProgramFiles}\PowerToys\PowerToys.exe"
    } else {
        throw "PowerToys installation verification failed"
    }
}
