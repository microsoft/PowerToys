param (
  [string]$arch = "x64",
  [string]$configuration = "Release"
)

$PTRoot = "${PSScriptRoot}\..\.."
cd "$PTRoot\$arch\$configuration\VideoConferenceVirtualDriver"
$ddf = Resolve-Path "$PTRoot\src\modules\videoconference\make_cab.ddf"
makecab.exe /v0 /F $ddf | Out-Null
Move-Item -Path driver\driver.cab -Destination VideoConference.cab -Force
Remove-Item -Force -Recurse -Path driver
Remove-Item -Force -Path setup.inf
Remove-Item -Force -Path setup.rpt