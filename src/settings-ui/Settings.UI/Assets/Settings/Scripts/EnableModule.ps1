[CmdletBinding()]
Param(
  [Parameter(Mandatory=$True,Position=1)]
  [string]$scriptPath
)

Write-Host "Enabling experimental feature: PSFeedbackProvider"
Enable-ExperimentalFeature PSFeedbackProvider
Write-Host "Enabling experimental feature: PSCommandNotFoundSuggestion"
Enable-ExperimentalFeature PSCommandNotFoundSuggestion

if (Get-Module -ListAvailable -Name Microsoft.WinGet.Client) {
    Write-Host "WinGet Client module detected"
} 
else {
    Write-Host "WinGet module was not found. Installation instructions can be found on https://www.powershellgallery.com/packages/Microsoft.WinGet.Client `r`n"
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
  Write-Host "Module is already registered in the profile file."
  # This message will be compared against in Command Not Found Settings page code behind. Take care when changing it.
}
else
{
  Add-Content -Path $PROFILE  -Value "`r`n#34de4b3d-13a8-4540-b76d-b9e8d3851756 PowerToys CommandNotFound module"
  Add-Content -Path $PROFILE  -Value "`r`nImport-Module `"$scriptPath\WinGetCommandNotFound.psd1`""
  Add-Content -Path $PROFILE  -Value "#34de4b3d-13a8-4540-b76d-b9e8d3851756"  
  Write-Host "Module was successfully registered in the profile file."
  # This message will be compared against in Command Not Found Settings page code behind. Take care when changing it.
}
