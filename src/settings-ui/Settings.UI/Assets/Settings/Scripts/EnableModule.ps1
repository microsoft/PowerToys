[CmdletBinding()]
Param(
  [Parameter(Mandatory=$True,Position=1)]
  [string]$scriptPath
)

Write-Host "Enabling experimental feature: PSFeedbackProvider"
Enable-ExperimentalFeature PSFeedbackProvider
Write-Host "Enabling experimental feature: PSCommandNotFoundSuggestion"
Enable-ExperimentalFeature PSCommandNotFoundSuggestion

$wingetModules = Get-Module -ListAvailable -Name Microsoft.WinGet.Client
if ($wingetModules) {

  $moduleUpToDate = $false;
  foreach ($mod in $wingetModules) {
    if ($mod.Version -ge "1.8.1133") {
      $moduleUpToDate = $true;
      break;
    }
  }

  if ($moduleUpToDate) {
    Write-Host "WinGet Client module detected"
  } else {
    Write-Host "WinGet module needs to be updated. Run `"Update-Module -Name Microsoft.WinGet.Client`" to update `r`n"
  }
} 
else {
    Write-Host "WinGet module was not found. Installation instructions can be found on https://www.powershellgallery.com/packages/Microsoft.WinGet.Client `r`n"
}

$CNFModule = Get-Module -ListAvailable -Name Microsoft.WinGet.CommandNotFound
if ($CNFModule) {
  Write-Host "Microsoft.WinGet.CommandNotFound module detected"
} else {
  Write-Host "Microsoft.WinGet.CommandNotFound was not found. Installing...`r`n"
  Install-Module -Name Microsoft.WinGet.CommandNotFound -Force
  Write-Host "Microsoft.WinGet.CommandNotFound module installed`r`n"
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
  if ($profileContent.Contains("Import-Module `"$scriptPath\WinGetCommandNotFound.psd1`""))
  {
    $profileContent = $profileContent.Replace("Import-Module `"$scriptPath\WinGetCommandNotFound.psd1`"",
                                              "Import-Module -Name Microsoft.WinGet.CommandNotFound")
    $profileContent = $profileContent.Replace("34de4b3d-13a8-4540-b76d-b9e8d3851756",
                                              "f45873b3-b655-43a6-b217-97c00aa0db58")
    Set-Content -Path $PROFILE -Value $profileContent
    Write-Host "Module was successfully upgraded in the profile file."
    # This message will be compared against in Command Not Found Settings page code behind. Take care when changing it.
  }
}
elseif ((-not [string]::IsNullOrEmpty($profileContent)) -and ($profileContent.Contains("f45873b3-b655-43a6-b217-97c00aa0db58")))
{
  Write-Host "Module is already registered in the profile file."
  # This message will be compared against in Command Not Found Settings page code behind. Take care when changing it.
}
else
{
  Add-Content -Path $PROFILE  -Value "`r`n#f45873b3-b655-43a6-b217-97c00aa0db58 PowerToys CommandNotFound module"
  Add-Content -Path $PROFILE  -Value "`r`nImport-Module -Name Microsoft.WinGet.CommandNotFound"
  Add-Content -Path $PROFILE  -Value "#f45873b3-b655-43a6-b217-97c00aa0db58"  
  Write-Host "Module was successfully registered in the profile file."
  # This message will be compared against in Command Not Found Settings page code behind. Take care when changing it.
}
