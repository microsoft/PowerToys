if (Get-Module -ListAvailable -Name Microsoft.WinGet.Client)
{
  Write-Host "WinGet Client module detected."
  # This message will be compared against in Command Not Found Settings page code behind. Take care when changing it.
} 
else {
  Install-Module -Name Microsoft.WinGet.Client
  if (Get-Module -ListAvailable -Name Microsoft.WinGet.Client)
  {
    Write-Host "WinGet Client module detected."
    # This message will be compared against in Command Not Found Settings page code behind. Take care when changing it.
  } else {
    Write-Host "WinGet Client module not detected. Installation instructions can be found on https://www.powershellgallery.com/packages/Microsoft.WinGet.Client `r`n"
    # This message will be compared against in Command Not Found Settings page code behind. Take care when changing it.
  }
}
