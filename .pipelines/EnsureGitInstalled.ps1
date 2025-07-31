param(
    [string]$Architecture = "x64"
)

# Check if Git is installed
function Test-GitInstalled {
    try {
        git --version > $null 2>&1
        return $true
    }
    catch {
        return $false
    }
}

# Install Git
function Install-Git {
    Write-Host "##[section]Installing Git for Windows ($Architecture)..."
    
    $gitInstallerUrl = if ($Architecture.ToLower() -eq "arm64") {
        "https://github.com/git-for-windows/git/releases/latest/download/Git-2.50.1-arm64.exe"
    } else {
        "https://github.com/git-for-windows/git/releases/latest/download/Git-2.50.1-64-bit.exe"
    }
    
    $installerPath = "$env:TEMP\GitInstaller.exe"
    
    try {
        Write-Host "##[command]Downloading Git installer..."
        Invoke-WebRequest -Uri $gitInstallerUrl -OutFile $installerPath -UseBasicParsing
        
        Write-Host "##[command]Installing Git silently..."
        Start-Process -FilePath $installerPath -ArgumentList "/VERYSILENT", "/NORESTART" -Wait
        
        # Clean up
        Remove-Item $installerPath -Force -ErrorAction SilentlyContinue
        
        # Refresh PATH
        $env:Path = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path", "User")
        
        return $true
    }
    catch {
        Write-Host "##[error]Failed to install Git: $($_.Exception.Message)"
        return $false
    }
}

# Main logic
Write-Host "##[section]Checking Git installation..."

if (Test-GitInstalled) {
    Write-Host "##[section]Git is already installed"
} else {
    Write-Host "##[warning]Git not found, installing..."
    
    if (Install-Git) {
        Start-Sleep -Seconds 3
        
        if (Test-GitInstalled) {
            Write-Host "##[section]Git installation successful"
        } else {
            Write-Host "##[error]Git installation failed"
            exit 1
        }
    } else {
        Write-Host "##[error]Failed to install Git"
        exit 1
    }
}

Write-Host "##[section]Git setup completed successfully"
