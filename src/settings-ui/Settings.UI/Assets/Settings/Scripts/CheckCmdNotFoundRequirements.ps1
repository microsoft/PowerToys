Write-Host $PSVersionTable
if ($PSVersionTable.PSVersion -ge 7.4)
{
  Write-Host "PowerShell 7.4 or greater detected."
  # This message will be compared against in Command Not Found Settings page code behind. Take care when changing it.
}
else
{
  Write-Host "PowerShell 7.4 or greater not detected. Installation instructions can be found on https://learn.microsoft.com/powershell/scripting/install/installing-powershell-on-windows `r`n"
  # This message will be compared against in Command Not Found Settings page code behind. Take care when changing it.
}

if ($mods = Get-Module -ListAvailable -Name Microsoft.WinGet.Client)
{
  Write-Host "WinGet Client module detected."
  # This message will be compared against in Command Not Found Settings page code behind. Take care when changing it.

  $needsUpdate = $true;
  foreach ($mod in $mods)
  {
    if ($mod.Version -ge "1.8.1133")
    {
      $needsUpdate = $false;
      break;
    }
  }
  if ($needsUpdate)
  {
    Write-Host "WinGet Client module needs to be updated."
    # This message will be compared against in Command Not Found Settings page code behind. Take care when changing it.
  }
} 
else {
  Write-Host "WinGet Client module not detected. Installation instructions can be found on https://www.powershellgallery.com/packages/Microsoft.WinGet.Client `r`n"
  # This message will be compared against in Command Not Found Settings page code behind. Take care when changing it.
}

if (!(Test-Path $PROFILE))
{
  Write-Host "Profile file $PROFILE not found".
  New-Item -Path $PROFILE -ItemType File
  Write-Host "Created profile file $PROFILE".
}

$profileContent = Get-Content -Path $PROFILE -Raw

if ((-not [string]::IsNullOrEmpty($profileContent)) -and ($profileContent.Contains("34de4b3d-13a8-4540-b76d-b9e8d3851756")))
{
  Write-Host "Outdated version of Command Not Found module found in the profile file."
  # This message will be compared against in Command Not Found Settings page code behind. Take care when changing it.
}
elseif ((-not [string]::IsNullOrEmpty($profileContent)) -and ($profileContent.Contains("f45873b3-b655-43a6-b217-97c00aa0db58")))
{
  Write-Host "Command Not Found module is registered in the profile file."
  # This message will be compared against in Command Not Found Settings page code behind. Take care when changing it.
}
else
{
  Write-Host "Command Not Found module is not registered in the profile file."
  # This message will be compared against in Command Not Found Settings page code behind. Take care when changing it.
}
