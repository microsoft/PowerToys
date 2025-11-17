$ProgressPreference = 'SilentlyContinue'

$WinAppDriverDownloadUrl = "https://github.com/microsoft/WinAppDriver/releases/download/v1.2.1/WindowsApplicationDriver_1.2.1.msi"

# Download WinAppDriver and verify their hash sums
Invoke-WebRequest -Uri $WinAppDriverDownloadUrl -OutFile "$($ENV:Temp)\WindowsApplicationDriver_1.2.1.msi"
$Hash = (Get-FileHash -Algorithm SHA256 "$($ENV:Temp)\WindowsApplicationDriver_1.2.1.msi").Hash
if ($Hash -ne 'a76a8f4e44b29bad331acf6b6c248fcc65324f502f28826ad2acd5f3c80857fe')
{
    Write-Error "$WinAppDriverHash"
    throw "WindowsApplicationDriver_1.2.1.msi has unexpected SHA256 hash: $Hash"
}

# Install WinAppDriver
Start-Process msiexec.exe -Wait -ArgumentList "/I $($ENV:Temp)\WindowsApplicationDriver_1.2.1.msi /quiet /passive"
