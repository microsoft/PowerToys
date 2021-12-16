[CmdletBinding()]
Param(
  [Parameter(Mandatory=$True,Position=1)]
  [string]$versionNumber = "0.0.1",

  [Parameter(Mandatory=$True,Position=2)]
  [AllowEmptyString()]
  [string]$DevEnvironment = "Local"
)

Write-Host $PSScriptRoot
$versionRegex = "(\d+)\.(\d+)\.(\d+)"

if($versionNumber -match $versionRegEx)
{
  $buildDayOfYear = (Get-Date).DayofYear;
  $buildTime = Get-Date -Format HH;
  # $buildTime = Get-Date -Format HHmmss;
  # $buildYear = Get-Date -Format yy;
  # $revision = [string]::Format("{0}{1}{2}", $buildYear, $buildDayOfYear, $buildTime )

  # max UInt16, 65535
  #$revision = [string]::Format("{0}{1}", $buildDayOfYear, $buildTime )
  #Write-Host "Revision" $revision

  $versionNumber = [int]::Parse($matches[1]).ToString() + "." + [int]::Parse($matches[2]).ToString() + "." + [int]::Parse($matches[3]).ToString() # + "." + $revision
  Write-Host "Version Number" $versionNumber
}
else{
	throw "Build format does not match the expected pattern (buildName_w.x.y.z)"
}

$verPropWriteFileLocation = $PSScriptRoot + '/../../src/Version.props';
$verPropReadFileLocation = $verPropWriteFileLocation;

[XML]$verProps = Get-Content $verPropReadFileLocation
$verProps.Project.PropertyGroup.Version = $versionNumber;
$verProps.Project.PropertyGroup.DevEnvironment = $DevEnvironment;

Write-Host "xml" $verProps.Project.PropertyGroup.Version 
$verProps.Save($verPropWriteFileLocation);