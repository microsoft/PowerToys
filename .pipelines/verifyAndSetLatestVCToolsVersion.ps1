$LatestVCToolsVersion = (([xml](& 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe' -latest -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -include packages -format xml)).instances.instance.packages.package | ? { $_.id -eq "Microsoft.VisualCpp.CRT.Source" }).version;

$test = Get-ChildItem "C:\Program Files (x86)\Microsoft SDKs\Windows Kits\10\ExtensionSDKs\"

Write-Output "AAAA $test"

New-Item -Path "C:\Program Files (x86)\Microsoft SDKs\Windows Kits\10\ExtensionSDKs\Microsoft.VCLibs.Desktop" -ItemType SymbolicLink -Value "C:\Program Files (x86)\Microsoft SDKs\Windows Kits\10\ExtensionSDKs\Microsoft.VCLibs.Desktop.120"

$test = Get-ChildItem "C:\Program Files (x86)\Microsoft SDKs\Windows Kits\10\ExtensionSDKs\"

Write-Output "AAAA $test"

Write-Output "Latest VCToolsVersion: $LatestVCToolsVersion"
Write-Output "Updating VCToolsVersion environment variable for job"
Write-Output "##vso[task.setvariable variable=VCToolsVersion]$LatestVCToolsVersion"
