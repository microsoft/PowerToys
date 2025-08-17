$VSInstances = ([xml](& 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe' -latest -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -include packages -format xml))
$VSPackages = $VSInstances.instances.instance.packages.package
$LatestVCPackage = ($VSPackages | ? { $_.id -eq "Microsoft.VisualCpp.Tools.Core" })
$LatestVCToolsVersion = $LatestVCPackage.version;

$VSRoot = (& 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe' -latest -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property 'resolvedInstallationPath')
$VCToolsRoot = Join-Path $VSRoot "VC\Tools\MSVC"

# We have observed a few instances where the VC tools package version actually
# differs from the version on the files themselves. We might as well check
# whether the version we just found _actually exists_ before we use it.
# We'll use whichever highest version exists.
$PackageVCToolPath = Join-Path $VCToolsRoot $LatestVCToolsVersion
If ($Null -Eq (Get-Item $PackageVCToolPath -ErrorAction:Ignore)) {
    $VCToolsVersions = Get-ChildItem $VCToolsRoot | ForEach-Object {
        [Version]$_.Name
    } | Sort -Descending
    $LatestActualVCToolsVersion = $VCToolsVersions | Select -First 1

    If ([Version]$LatestVCToolsVersion -Ne $LatestActualVCToolsVersion) {
        Write-Output "VC Tools Mismatch: Directory = $LatestActualVCToolsVersion, Package = $LatestVCToolsVersion"
        $LatestVCToolsVersion = $LatestActualVCToolsVersion.ToString(3)
    }
}

Write-Output "Latest VCToolsVersion: $LatestVCToolsVersion"
Write-Output "Updating VCToolsVersion environment variable for job"
Write-Output "##vso[task.setvariable variable=VCToolsVersion]$LatestVCToolsVersion"
