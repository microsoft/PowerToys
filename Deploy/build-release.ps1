$sourceDirectoryName = $env:APPVEYOR_BUILD_FOLDER + "\Output\Release"
$fileName = $env:APPVEYOR_BUILD_FOLDER + "\Wox-$env:APPVEYOR_BUILD_VERSION.zip"

$current_path = Convert-Path .
Write-Host "Current path: " + $current_path
Write-Host "Target path: " + $sourceDirectoryName

[Reflection.Assembly]::LoadWithPartialName("System.IO.Compression.FileSystem")
[System.IO.Compression.ZipFile]::CreateFromDirectory($sourceDirectoryName, $fileName)