taskkill /f /im explorer.exe
Get-AppxPackage -Name 'PowerToys' | select -ExpandProperty "PackageFullName" | Remove-AppxPackage
.\build_msix.ps1
signtool sign /debug /a /fd SHA256 /f PowerToys_TemporaryKey.pfx /p 12345 bin\PowerToys-x64.msix
Add-AppxPackage .\bin\PowerToys-x64.msix
start $Env:windir\explorer.exe