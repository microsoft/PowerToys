if (!(Test-Path $PROFILE))
{
  return;
} 

$profileContent = Get-Content -Path $PROFILE -Raw
$legacyGuid = "34de4b3d-13a8-4540-b76d-b9e8d3851756"
if ((-not [string]::IsNullOrEmpty($profileContent)) -and ($profileContent.Contains($legacyGuid)))
{
  # Upgrade Microsoft.WinGet.Client module
  $wingetModule = Get-Module -ListAvailable -Name Microsoft.WinGet.Client
  if ($wingetModule -and $wingetModule.Version -lt "1.8.1133") {
    Update-Module -Name Microsoft.WinGet.Client
  }

  # Install Microsoft.WinGet.CommandNotFound module
  if (-Not (Get-Module -ListAvailable -Name Microsoft.WinGet.CommandNotFound)) {
    Install-Module -Name Microsoft.WinGet.CommandNotFound -Force
  }

  # Replace old module with new one (and new GUID comment)
  $regex = "Import-Module .*WinGetCommandNotFound.psd1`""
  if ($profileContent -match $regex)
  {
    $profileContent = $profileContent -replace $regex, "Import-Module -Name Microsoft.WinGet.CommandNotFound"
    $profileContent = $profileContent -replace $legacyGuid, "f45873b3-b655-43a6-b217-97c00aa0db58"
    Set-Content -Path $PROFILE -Value $profileContent
  }
}