$VSInstances = ([xml](& 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe' -latest -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -include packages -format xml))
$VSPackages = $VSInstances.instances.instance.packages.package
$LatestVCPackage = ($VSInstances.instances.instance.packages.package | ? { $_.id -eq "Microsoft.VisualCpp.Tools.Core" })
$LatestVCToolsVersion = $LatestVCPackage.version;
Write-Output "Latest VCToolsVersion: $LatestVCToolsVersion"
Write-Output "Updating VCToolsVersion environment variable for job"
Write-Output "##vso[task.setvariable variable=VCToolsVersion]$LatestVCToolsVersion"
