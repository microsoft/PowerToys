$path = $env:APPVEYOR_BUILD_FOLDER + "\Deploy\UpdateGenerator"

$current_path = Convert-Path .
Write-Host "Current path: " + $current_path
Write-Host "Target path: " + $path

Set-Location $path
& ".\Wox.UpdateFeedGenerator.exe"
Set-Location $current_path