[CmdletBinding()]
Param(
  [Parameter(Mandatory=$True,Position=1)]
  [string]$scriptPath
)

Enable-ExperimentalFeature PSFeedbackProvider
Enable-ExperimentalFeature PSCommandNotFoundSuggestion

$profileContent = Get-Content($PROFILE)

if (-not $profileContent.Contains("34de4b3d-13a8-4540-b76d-b9e8d3851756"))
{
  Add-Content -Path $PROFILE  -Value "#34de4b3d-13a8-4540-b76d-b9e8d3851756 PowerToys CommandNotFound module"
  Add-Content -Path $PROFILE  -Value "`r`nImport-Module $scriptPath\WinGetCommandNotFound.psd1"
  Add-Content -Path $PROFILE  -Value "#34de4b3d-13a8-4540-b76d-b9e8d3851756"  
}
