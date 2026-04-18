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
  # Use conditional import to avoid crashes when PowerShell exits quickly (e.g. VS Code tasks)
  $conditionalImport = @'
if (-not ([Environment]::GetCommandLineArgs() | Where-Object { $_ -in @('-Command','-c','-EncodedCommand','-e','-ec','-File','-f','-NonInteractive') })) { Import-Module -Name Microsoft.WinGet.CommandNotFound }
'@
  $regex = "Import-Module .*WinGetCommandNotFound.psd1`""
  $match = [regex]::Match($profileContent, $regex)
  if ($match.Success)
  {
    $profileContent = $profileContent.Replace($match.Value, $conditionalImport)
    $profileContent = $profileContent -replace $legacyGuid, "f45873b3-b655-43a6-b217-97c00aa0db58"
    Set-Content -Path $PROFILE -Value $profileContent
  }
}