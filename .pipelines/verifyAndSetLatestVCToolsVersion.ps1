# Construct vswhere arguments based on environment variable
$vsWhereArgs = @('-latest', '-requires', 'Microsoft.VisualStudio.Component.VC.Tools.x86.x64', '-include', 'packages', '-format', 'xml')
if ($env:VCWhereExtraVersionTarget) {
    # Add version target if specified (e.g., '-version [18.0,19.0)' for VS2026)
    $vsWhereArgs += $env:VCWhereExtraVersionTarget.Split(' ')
}

$VSInstances = ([xml](& 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe' @vsWhereArgs))
$VSPackages = $VSInstances.instances.instance.packages.package
$LatestVCPackage = ($VSPackages | ? { $_.id -eq "Microsoft.VisualCpp.Tools.Core" })
$LatestVCToolsVersion = $LatestVCPackage.version;

# Construct vswhere arguments for resolvedInstallationPath
$vsWhereRootArgs = @('-latest', '-requires', 'Microsoft.VisualStudio.Component.VC.Tools.x86.x64', '-property', 'resolvedInstallationPath')
if ($env:VCWhereExtraVersionTarget) {
    $vsWhereRootArgs += $env:VCWhereExtraVersionTarget.Split(' ')
}

$VSRoot = (& 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe' @vsWhereRootArgs)
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
