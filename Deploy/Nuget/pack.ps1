$root = (split-path -parent $MyInvocation.MyCommand.Definition) + '\..\..'
Write-Host $root
$version = [System.Reflection.Assembly]::LoadFile("$root\Output\Release\Wox.Plugin.dll").GetName().Version
$versionStr = "{0}.{1}.{2}" -f ($version.Major, $version.Minor, $version.Build)

Write-Host "Setting .nuspec version tag to $versionStr"

$content = (Get-Content $root\Deploy\NuGet\wox.plugin.nuspec) 
$content = $content -replace '\$version\$',$versionStr

$content | Out-File $root\wox.plugin.nuspec

& $root\.nuget\NuGet.exe pack $root\deploy\nuget\wox.plugin.nuspec
