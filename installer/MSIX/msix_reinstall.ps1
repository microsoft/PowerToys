taskkill /f /im explorer.exe
Get-AppxPackage -Name 'PowerToys' | select -ExpandProperty "PackageFullName" | Remove-AppxPackage
makeappx build /v /overwrite /f PackagingLayout.xml /id "PowerToys-x64" /op bin\
signtool sign /debug /a /fd SHA256 /f PowerToys_TemporaryKey.pfx /p 12345 bin\PowerToys-x64.msix
Add-AppxPackage .\bin\PowerToys-x64.msix
start $Env:windir\explorer.exe