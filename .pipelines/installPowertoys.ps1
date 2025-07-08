# Install PowerToys
Write-Host "Installing PowerToys with arguments: /passive /norestart"
Write-Host "Installer path: $($Installer.FullName)"
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