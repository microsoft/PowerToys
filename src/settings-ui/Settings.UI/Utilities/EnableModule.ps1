[CmdletBinding()]
Param(
  [Parameter(Mandatory=$True,Position=1)]
  [string]$scriptPath
)

Enable-ExperimentalFeature PSFeedbackProvider
Enable-ExperimentalFeature PSCommandNotFoundSuggestion

Write-Host $scriptPath
Write-Host $PROFILE
cat $PROFILE

Add-Content -Path $PROFILE  -Value "`r`nImport-Module $scriptPath\WinGetCommandNotFound.psd1"

cat $PROFILE

$null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown');
