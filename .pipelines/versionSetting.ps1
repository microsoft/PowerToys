[CmdletBinding()]
Param(
  [Parameter(Mandatory=$True,Position=1)]
  [string]$versionNumber = "0.0.1",

  [Parameter(Mandatory=$True,Position=2)]
  [AllowEmptyString()]
  [string]$DevEnvironment = "Local",

  [ValidateSet("stable", "preview")]
  [string]$Channel = "stable",

  [string]$SourceCommit = $env:BUILD_SOURCEVERSION,

  [string]$BuildNumber = $env:BUILD_BUILDNUMBER
)

Write-Host $PSScriptRoot

function Get-NormalizedVersion {
  param(
    [Parameter(Mandatory = $true)]
    [string]$InputVersion,
    [Parameter(Mandatory = $true)]
    [string]$ReleaseChannel,
    [string]$PipelineBuildNumber
  )

  if ($InputVersion -match "^(?<numeric>\d+\.\d+(?:\.\d+){0,2})-(?<suffix>preview)$") {
    if ($ReleaseChannel -ne "preview") {
      throw "Version suffix '-preview' can only be used with the preview release channel"
    }

    $InputVersion = $matches["numeric"]
  }

  if ($ReleaseChannel -eq "preview" -and $InputVersion -match "^(\d+)\.(\d+)$") {
    $major = [int]::Parse($matches[1])
    $minor = [int]::Parse($matches[2])
    $now = Get-Date
    $yyMM = [int]::Parse($now.ToString("yyMM"))
    $day = $now.ToString("dd")
    $rev = "001"
    if ($PipelineBuildNumber -match "_(?<yyMM>\d{4})\.(?<day>\d{2})(?<rev>\d{3})") {
      $yyMM = [int]::Parse($matches["yyMM"])
      $day = $matches["day"]
      $rev = $matches["rev"]
    }

    $build = [int]::Parse("$day$rev")
    return "$major.$minor.$yyMM.$build"
  }

  if ($InputVersion -match "^(\d+)\.(\d+)\.(\d+)(?:\.(\d+))?$") {
    $versionParts = @([int]::Parse($matches[1]), [int]::Parse($matches[2]), [int]::Parse($matches[3]))
    if ($matches[4]) {
      $versionParts += [int]::Parse($matches[4])
    }

    return $versionParts -join "."
  }

  throw "Build format does not match the expected pattern (w.x, w.x.y, w.x.y.z, or w.x.y.z-preview for preview channel)"
}

$versionNumber = Get-NormalizedVersion -InputVersion $versionNumber -ReleaseChannel $Channel -PipelineBuildNumber $BuildNumber
foreach ($part in ($versionNumber -split '\.')) {
  $value = [int]::Parse($part)
  if ($value -lt 0 -or $value -gt [UInt16]::MaxValue) {
    throw "Version component '$value' is outside the supported Windows version range 0-65535"
  }
}
Write-Host "Version Number" $versionNumber

$verPropWriteFileLocation = $PSScriptRoot + '/../src/Version.props';
$verPropReadFileLocation = $verPropWriteFileLocation;

[XML]$verProps = Get-Content $verPropReadFileLocation
$verProps.Project.PropertyGroup.Version = $versionNumber;
$verProps.Project.PropertyGroup.VersionChannel = $Channel;
$verProps.Project.PropertyGroup.SourceCommit = if ([string]::IsNullOrWhiteSpace($SourceCommit)) { "unknown" } else { $SourceCommit };
$verProps.Project.PropertyGroup.DevEnvironment = $DevEnvironment;

Write-Host "xml" $verProps.Project.PropertyGroup.Version
$verProps.Save($verPropWriteFileLocation);


#### The same thing as above, but for the CmdPal version
$verPropWriteFileLocation = $PSScriptRoot + '/../src/CmdPalVersion.props';
$verPropReadFileLocation = $verPropWriteFileLocation;
[XML]$verProps = Get-Content $verPropReadFileLocation
$verProps.Project.PropertyGroup.DevEnvironment = $DevEnvironment;
Write-Host "xml" $verProps.Project.PropertyGroup.Version
$verProps.Save($verPropWriteFileLocation);
#######

$manifestVersionNumber = if (($versionNumber.ToCharArray() | Where-Object { $_ -eq '.' }).Count -eq 2) { $versionNumber + '.0' } else { $versionNumber }

# Set PowerRenameContextMenu package version in AppManifest.xml
$powerRenameContextMenuAppManifestWriteFileLocation = $PSScriptRoot + '/../src/modules/powerrename/PowerRenameContextMenu/AppxManifest.xml';
$powerRenameContextMenuAppManifestReadFileLocation = $powerRenameContextMenuAppManifestWriteFileLocation;

[XML]$powerRenameContextMenuAppManifest = Get-Content $powerRenameContextMenuAppManifestReadFileLocation
$powerRenameContextMenuAppManifest.Package.Identity.Version = $manifestVersionNumber
Write-Host "PowerRenameContextMenu version" $powerRenameContextMenuAppManifest.Package.Identity.Version
$powerRenameContextMenuAppManifest.Save($powerRenameContextMenuAppManifestWriteFileLocation);

# Set ImageResizerContextMenu package version in AppManifest.xml
$imageResizerContextMenuAppManifestWriteFileLocation = $PSScriptRoot + '/../src/modules/imageresizer/ImageResizerContextMenu/AppxManifest.xml';
$imageResizerContextMenuAppManifestReadFileLocation = $imageResizerContextMenuAppManifestWriteFileLocation;

[XML]$imageResizerContextMenuAppManifest = Get-Content $imageResizerContextMenuAppManifestReadFileLocation
$imageResizerContextMenuAppManifest.Package.Identity.Version = $manifestVersionNumber
Write-Host "ImageResizerContextMenu version" $imageResizerContextMenuAppManifest.Package.Identity.Version
$imageResizerContextMenuAppManifest.Save($imageResizerContextMenuAppManifestWriteFileLocation);

# Set FileLocksmithContextMenu package version in AppManifest.xml
$fileLocksmithContextMenuAppManifestWriteFileLocation = $PSScriptRoot + '/../src/modules/FileLocksmith/FileLocksmithContextMenu/AppxManifest.xml';
$fileLocksmithContextMenuAppManifestReadFileLocation = $fileLocksmithContextMenuAppManifestWriteFileLocation;

[XML]$fileLocksmithContextMenuAppManifest = Get-Content $fileLocksmithContextMenuAppManifestReadFileLocation
$fileLocksmithContextMenuAppManifest.Package.Identity.Version = $manifestVersionNumber
Write-Host "FileLocksmithContextMenu version" $fileLocksmithContextMenuAppManifest.Package.Identity.Version
$fileLocksmithContextMenuAppManifest.Save($fileLocksmithContextMenuAppManifestWriteFileLocation);

# Set NewPlusContextMenu package version in AppManifest.xml
$newPlusContextMenuAppManifestWriteFileLocation = $PSScriptRoot + '/../src/modules/NewPlus/NewShellExtensionContextMenu/AppxManifest.xml';
$newPlusContextMenuAppManifestReadFileLocation = $newPlusContextMenuAppManifestWriteFileLocation;

[XML]$newPlusContextMenuAppManifest = Get-Content $newPlusContextMenuAppManifestReadFileLocation
$newPlusContextMenuAppManifest.Package.Identity.Version = $manifestVersionNumber
Write-Host "NewPlusContextMenu version" $newPlusContextMenuAppManifest.Package.Identity.Version
$newPlusContextMenuAppManifest.Save($newPlusContextMenuAppManifestWriteFileLocation);
