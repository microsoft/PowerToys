taskkill /f /im explorer.exe

Get-AppxPackage -Name 'PowerToys' | select -ExpandProperty "PackageFullName" | Remove-AppxPackage

.\build_msix.ps1
signtool sign /debug /a /fd SHA256 /f PowerToys_TemporaryKey.pfx /p 12345 bin\PowerToys-x64.msix
signtool sign /debug /a /fd SHA256 /f PowerToys_TemporaryKey.pfx /p 12345 bin\PowerToys.msixbundle

Add-AppxPackage .\bin\PowerToys.msixbundle

start $Env:windir\explorer.exe