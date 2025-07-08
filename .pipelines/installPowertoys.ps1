$ProgressPreference = 'SilentlyContinue'

# Find PowerToys installer in artifact directory
$ArtifactPath = $ENV:BUILD_ARTIFACTSTAGINGDIRECTORY
if (-not $ArtifactPath) {
    throw "BUILD_ARTIFACTSTAGINGDIRECTORY environment variable not set"
}

Write-Host "Looking for installer in: $ArtifactPath"

# Check if we have zip files that need to be extracted
$ZipFiles = Get-ChildItem -Path $ArtifactPath -Recurse -Filter '*.zip'
if ($ZipFiles) {
    Write-Host "Found zip files, extracting..."
    foreach ($ZipFile in $ZipFiles) {
        Write-Host "Extracting: $($ZipFile.Name)"
        $ExtractPath = Join-Path $ArtifactPath $ZipFile.BaseName
        Expand-Archive -Path $ZipFile.FullName -DestinationPath $ExtractPath -Force
    }
}

# List all files to debug
Write-Host "All files in artifact directory:"
Get-ChildItem -Path $ArtifactPath -Recurse | ForEach-Object { 
    Write-Host "  $($_.FullName) (Size: $($_.Length) bytes)"
}

# First try to find user installer
$Installer = Get-ChildItem -Path $ArtifactPath -Recurse -Filter 'PowerToysUserSetup-*.exe' | Select-Object -First 1

if (-not $Installer) {
    Write-Host "PowerToysUserSetup-*.exe not found, looking for machine installer..."
    $Installer = Get-ChildItem -Path $ArtifactPath -Recurse -Filter 'PowerToysSetup-*.exe' | Select-Object -First 1
}

if (-not $Installer) {
    throw "PowerToys installer not found in artifact directory"
}

Write-Host "Found PowerToys installer: $($Installer.Name)"
Write-Host "Full path: $($Installer.FullName)"

# Install PowerToys
Write-Host "Installing PowerToys with arguments: /passive /norestart"
Write-Host "Current user: $env:USERNAME"
Write-Host "User profile: $env:USERPROFILE"

$Process = Start-Process -Wait -FilePath $Installer.FullName -ArgumentList "/passive", "/norestart" -PassThru -NoNewWindow

Write-Host "Exit Code: $($Process.ExitCode)"
Write-Host "Process finished at: $(Get-Date)"

# Try to get more detailed error information
if ($Process.ExitCode -eq 1) {
    Write-Host "Exit code 1 typically means general error. Checking common issues..."
    
    # Check if installer is valid
    try {
        $FileInfo = Get-ItemProperty $Installer.FullName
        Write-Host "Installer file size: $($FileInfo.Length) bytes"
        Write-Host "Installer creation time: $($FileInfo.CreationTime)"
    } catch {
        Write-Host "Could not get installer file info: $($_.Exception.Message)"
    }
    
    # Try to get installer help
    Write-Host "Attempting to get installer help..."
    try {
        $HelpOutput = & $Installer.FullName /? 2>&1
        Write-Host "Installer help output: $HelpOutput"
    } catch {
        Write-Host "Could not get installer help: $($_.Exception.Message)"
    }
    
    # Check if there are any installer logs
    $LogPaths = @(
        "$env:TEMP\PowerToys*.log",
        "$env:LOCALAPPDATA\Temp\PowerToys*.log",
        "$env:USERPROFILE\AppData\Local\Temp\PowerToys*.log"
    )
    
    foreach ($LogPath in $LogPaths) {
        $LogFiles = Get-ChildItem -Path $LogPath -ErrorAction SilentlyContinue
        if ($LogFiles) {
            Write-Host "Found log files at $LogPath"
            foreach ($LogFile in $LogFiles) {
                Write-Host "Log file: $($LogFile.FullName)"
                Write-Host "Last 20 lines of log:"
                Get-Content $LogFile.FullName -Tail 20 | Write-Host
            }
        }
    }
}

if ($Process.ExitCode -ne 0 -and $Process.ExitCode -ne 3010) {
    throw "PowerToys installation failed with exit code: $($Process.ExitCode)"
}

# Verify installation
$PowerToysPath = "${env:LOCALAPPDATA}\PowerToys\PowerToys.exe"
if (-not (Test-Path $PowerToysPath)) {
    $PowerToysPath = "${env:ProgramFiles}\PowerToys\PowerToys.exe"
}

if (Test-Path $PowerToysPath) {
    Write-Host "PowerToys installation completed successfully at: $PowerToysPath"
} else {
    throw "PowerToys installation verification failed - executable not found"
}