$name='PowerToys'
taskkill /f /im explorer.exe
Get-AppxPackage -Name $name | select -ExpandProperty "PackageFullName" | Remove-AppxPackage
makeappx build /v /overwrite /f PackagingLayout.xml /id "x64" /op bin\
signtool sign /debug /a /fd SHA256 /f PowerToysTestKey.pfx /p 12345 bin\x64.msix
Add-AppxPackage .\bin\x64.msix
start $Env:windir\explorer.exe