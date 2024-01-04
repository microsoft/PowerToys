
if ((Get-AppxPackage microsoft.DesktopAppInstaller).Version -ge [System.Version]"1.21")
{
  Write-Host "Detected winget. Will try to install PowerShell."
}
else
{
  Write-Host "WinGet not detected. Will try to install."
  $cpuArchitecture="x64"
  $detectedArchitecture=""
  if ($env:PROCESSOR_ARCHITEW6432 -eq $null) {
    $detectedArchitecture=$env:PROCESSOR_ARCHITECTURE
  } else {
    $detectedArchitecture=$env:PROCESSOR_ARCHITEW6432
  }
  Write-Host "Detected the CPU architecture:$detectedArchitecture"
  if ($detectedArchitecture -ne "AMD64")
  {
    Write-Host "Mismatch with AMD64, setting it to arm64, since that's where we're likely running."
    $cpuArchitecture="arm64"
  }
  if((Get-AppxPackage Microsoft.VCLibs.140.00).Version -ge [System.Version]"14.0.30704")
  {
    Write-Host "Detected Microsoft.VCLibs.140.00."
  }
  else
  {
    Write-Host "Microsoft.VCLibs.140.00 not detected. Will try to install."
    Add-AppxPackage -Path "https://aka.ms/Microsoft.VCLibs.$cpuArchitecture.14.00.Desktop.appx"
  }
  if((Get-AppxPackage Microsoft.VCLibs.140.00.UWPDesktop).Version -ge [System.Version]"14.0.30704")
  {
    Write-Host "Detected Microsoft.VCLibs.140.00.UWPDesktop"
  }
  else
  {
    Write-Host "Microsoft.VCLibs.140.00.UWPDesktop not detected. Will try to install."
    Add-AppxPackage -Path "https://aka.ms/Microsoft.VCLibs.$cpuArchitecture.14.00.Desktop.appx"
  }
  if (Get-AppxPackage Microsoft.UI.Xaml.2.7)
  {
    Write-Host "Detected Microsoft.UI.Xaml.2.7"
  }
  else
  {
    Write-Host "Microsoft.UI.Xaml.2.7 not detected. Will try to install."
    Write-Host "Downloading to $Env:TMP\microsoft.ui.xaml.2.7.3.zip"
    Invoke-WebRequest -Uri https://www.nuget.org/api/v2/package/Microsoft.UI.Xaml/2.7.3 -OutFile "$Env:TMP\microsoft.ui.xaml.2.7.3.zip"
    Write-Host "Extracting $Env:TMP\microsoft.ui.xaml.2.7.3.zip"
    Expand-Archive "$Env:TMP\microsoft.ui.xaml.2.7.3.zip" -DestinationPath "$Env:TEMP\microsoft.ui.xaml.2.7.3"
    Write-Host "Installing $Env:TMP\microsoft.ui.xaml.2.7.3\tools\AppX\$cpuArchitecture\Release\Microsoft.UI.Xaml.2.7.appx"
    Add-AppxPackage "$Env:TMP\microsoft.ui.xaml.2.7.3\tools\AppX\$cpuArchitecture\Release\Microsoft.UI.Xaml.2.7.appx"
  }
  Write-Host "Getting winget to the latest stable"
  Add-AppxPackage -Path "https://aka.ms/getwinget"
}

winget install Microsoft.PowerShell --source winget
if ($LASTEXITCODE -eq 0)
{
  Write-Host "Powershell 7 successfully installed."
  # This message will be compared against in Command Not Found Settings page code behind. Take care when changing it.
}
else
{
  Write-Host "Powershell 7 was not installed."
}

