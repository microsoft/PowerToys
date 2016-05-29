# msbuild based installer generation is not working in appveyor, not sure why

$currentPath = Convert-Path .
Write-Host "Current path: " + $currentPath

$path = $env:APPVEYOR_BUILD_FOLDER + "\Deploy\wox.nuspec"
Write-Host "nuspec path: " + $path
$releasePath = $env:APPVEYOR_BUILD_FOLDER + "\Output\Release"
& nuget.exe pack $path -Version $env:APPVEYOR_BUILD_VERSION -Properties Configuration=Release -BasePath $releasePath

$nupkgPath = $env:APPVEYOR_BUILD_FOLDER + "\Wox." + $env:APPVEYOR_BUILD_VERSION + ".nupkg"
Write-Host "nupkg path: " + $nupkgPath

# must use Squirrel.com, Squirrel.exe will produce nothing 
$squirrelPath = $env:APPVEYOR_BUILD_FOLDER + "\packages\squirrel*\tools\Squirrel.com"
Write-Host "squirrel path: " + $squirrelPath
$iconPath = $env:APPVEYOR_BUILD_FOLDER + "\Wox\Resources\app.ico"
& $squirrelPath --releasify $nupkgPath --setupIcon $iconPath --no-msi

$path = "Releases\" + "Wox-" + $env:APPVEYOR_BUILD_VERSION + ".exe"
mv Releases\Setup.exe $path
