$path = $env:APPVEYOR_BUILD_FOLDER + "\Deploy\wox.plugin.nuspec"

$currentPath = Convert-Path .
Write-Host "Current path:" + $currentPath
Write-Host "nuspec path:" + $path

& nuget pack $path -Version $env:APPVEYOR_BUILD_VERSION
