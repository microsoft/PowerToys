New-Alias nuget.exe ".\packages\NuGet.CommandLine.*\tools\NuGet.exe"
$env:APPVEYOR_BUILD_FOLDER = Convert-Path .
$env:APPVEYOR_BUILD_VERSION = "1.2.0"
& .\Deploy\squirrel_installer.ps1