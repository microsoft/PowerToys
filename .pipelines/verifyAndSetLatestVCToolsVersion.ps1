$LatestVersions = (([xml](& 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe' -latest -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -include packages -format xml)).instances.instance.packages.package);
Write-Output $LatestVersions
