[CmdletBinding()]
Param(
  [Parameter(Mandatory=$True,Position=1)]
  [string]$versionNumber = "0.0.1",

  [Parameter(Mandatory=$True,Position=2)]
  [AllowEmptyString()]
  [string]$DevEnvironment = "Local",

  [Parameter(Mandatory=$True,Position=3)]
  [string]$cmdPalVersionNumber = "0.0.1"
)

Write-Host $PSScriptRoot
$versionRegex = "(\d+)\.(\d+)\.(\d+)"

if($versionNumber -match $versionRegEx)
{
  #$buildDayOfYear = (Get-Date).DayofYear;
  #$buildTime = Get-Date -Format HH;
  #$buildTime = Get-Date -Format HHmmss;
  #$buildYear = Get-Date -Format yy;
  #$revision = [string]::Format("{0}{1}{2}", $buildYear, $buildDayOfYear, $buildTime )

  # max UInt16, 65535
  #$revision = [string]::Format("{0}{1}", $buildDayOfYear, $buildTime )
  #Write-Host "Revision" $revision

  $versionNumber = [int]::Parse($matches[1]).ToString() + "." + [int]::Parse($matches[2]).ToString() + "." + [int]::Parse($matches[3]).ToString() # + "." + $revision
  Write-Host "Version Number" $versionNumber
}
else
{
  throw "Build format does not match the expected pattern (buildName_w.x.y.z)"
}

$verPropWriteFileLocation = $PSScriptRoot + '/../src/Version.props';
$verPropReadFileLocation = $verPropWriteFileLocation;

[XML]$verProps = Get-Content $verPropReadFileLocation
$verProps.Project.PropertyGroup.Version = $versionNumber;
$verProps.Project.PropertyGroup.DevEnvironment = $DevEnvironment;

Write-Host "xml" $verProps.Project.PropertyGroup.Version
$verProps.Save($verPropWriteFileLocation);


#### The same thing as above, but for the CmdPal version
$verPropWriteFileLocation = $PSScriptRoot + '/../src/CmdPalVersion.props';
$verPropReadFileLocation = $verPropWriteFileLocation;
[XML]$verProps = Get-Content $verPropReadFileLocation
$verProps.Project.PropertyGroup.CmdPalVersion = $cmdPalVersionNumber;
$verProps.Project.PropertyGroup.DevEnvironment = $DevEnvironment;
Write-Host "xml" $verProps.Project.PropertyGroup.Version
$verProps.Save($verPropWriteFileLocation);
#######

# Set PowerRenameContextMenu package version in AppManifest.xml
$powerRenameContextMenuAppManifestWriteFileLocation = $PSScriptRoot + '/../src/modules/powerrename/PowerRenameContextMenu/AppxManifest.xml';
$powerRenameContextMenuAppManifestReadFileLocation = $powerRenameContextMenuAppManifestWriteFileLocation;

[XML]$powerRenameContextMenuAppManifest = Get-Content $powerRenameContextMenuAppManifestReadFileLocation
$powerRenameContextMenuAppManifest.Package.Identity.Version = $versionNumber + '.0'
Write-Host "PowerRenameContextMenu version" $powerRenameContextMenuAppManifest.Package.Identity.Version
$powerRenameContextMenuAppManifest.Save($powerRenameContextMenuAppManifestWriteFileLocation);

# Set ImageResizerContextMenu package version in AppManifest.xml
$imageResizerContextMenuAppManifestWriteFileLocation = $PSScriptRoot + '/../src/modules/imageresizer/ImageResizerContextMenu/AppxManifest.xml';
$imageResizerContextMenuAppManifestReadFileLocation = $imageResizerContextMenuAppManifestWriteFileLocation;

[XML]$imageResizerContextMenuAppManifest = Get-Content $imageResizerContextMenuAppManifestReadFileLocation
$imageResizerContextMenuAppManifest.Package.Identity.Version = $versionNumber + '.0'
Write-Host "ImageResizerContextMenu version" $imageResizerContextMenuAppManifest.Package.Identity.Version
$imageResizerContextMenuAppManifest.Save($imageResizerContextMenuAppManifestWriteFileLocation);

# Set FileLocksmithContextMenu package version in AppManifest.xml
$fileLocksmithContextMenuAppManifestWriteFileLocation = $PSScriptRoot + '/../src/modules/FileLocksmith/FileLocksmithContextMenu/AppxManifest.xml';
$fileLocksmithContextMenuAppManifestReadFileLocation = $fileLocksmithContextMenuAppManifestWriteFileLocation;

[XML]$fileLocksmithContextMenuAppManifest = Get-Content $fileLocksmithContextMenuAppManifestReadFileLocation
$fileLocksmithContextMenuAppManifest.Package.Identity.Version = $versionNumber + '.0'
Write-Host "FileLocksmithContextMenu version" $fileLocksmithContextMenuAppManifest.Package.Identity.Version
$fileLocksmithContextMenuAppManifest.Save($fileLocksmithContextMenuAppManifestWriteFileLocation);

# Set NewPlusContextMenu package version in AppManifest.xml
$newPlusContextMenuAppManifestWriteFileLocation = $PSScriptRoot + '/../src/modules/NewPlus/NewShellExtensionContextMenu/AppxManifest.xml';
$newPlusContextMenuAppManifestReadFileLocation = $newPlusContextMenuAppManifestWriteFileLocation;

[XML]$newPlusContextMenuAppManifest = Get-Content $newPlusContextMenuAppManifestReadFileLocation
$newPlusContextMenuAppManifest.Package.Identity.Version = $versionNumber + '.0'
Write-Host "NewPlusContextMenu version" $newPlusContextMenuAppManifest.Package.Identity.Version
$newPlusContextMenuAppManifest.Save($newPlusContextMenuAppManifestWriteFileLocation);

# Set package version in Package.appxmanifest
$cmdPalAppManifestWriteFileLocation = $PSScriptRoot + '/../src/modules/cmdpal/Microsoft.CmdPal.UI/Package.appxmanifest';
$cmdPalAppManifestReadFileLocation = $cmdPalAppManifestWriteFileLocation;

[XML]$cmdPalAppManifest = Get-Content $cmdPalAppManifestReadFileLocation
$cmdPalAppManifest.Package.Identity.Version = $cmdPalVersionNumber + '.0'
Write-Host "CmdPal Package version: " $cmdPalAppManifest.Package.Identity.Version
$cmdPalAppManifest.Save($cmdPalAppManifestWriteFileLocation);
