Param(
  [string]$Configuration = "release",
  [string]$VersionOfSDK,
  [bool]$IsAzurePipelineBuild = $false,
  [switch]$BypassWarning = $false,
  [switch]$Help = $false
)

$StartTime = Get-Date

if ($Help) {
  Write-Host @"
Copyright (c) Microsoft Corporation.
Licensed under the MIT License.

Syntax:
      Build.cmd [options]

Description:
      Builds Cmdpal SDK.

Options:

  -Configuration <configuration>
      Only build the selected configuration(s)
      Example: -Configuration Release
      Example: -Configuration "Debug,Release"

  -Help
      Display this usage message.
"@
  Exit
}

if (-not $BypassWarning) {
  Write-Host @"
This script is not meant to be run directly.  To build the sdk, please run the following from the root directory:
build -BuildStep "sdk"
"@ -ForegroundColor RED
  Exit
}

$ErrorActionPreference = "Stop"

$buildPlatforms = "x64","x86","arm64","AnyCPU"

$msbuildPath = &"${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -prerelease -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe
if ($IsAzurePipelineBuild) {
  $nugetPath = "nuget.exe";
} else {
  $nugetPath = (Join-Path $PSScriptRoot "..\build\NugetWrapper.cmd")
}

New-Item -ItemType Directory -Force -Path "$PSScriptRoot\_build"
& $nugetPath restore (Join-Path $PSScriptRoot CmdPalSDK.sln)

Try {
  foreach ($platform in $buildPlatforms) {
    foreach ($config in $Configuration.Split(",")) {
      $msbuildArgs = @(
        ("$PSScriptRoot\CmdPalSDK.sln"),
        ("/p:Platform="+$platform),
        ("/p:Configuration="+$config),
        ("/binaryLogger:Microsoft.CmdPal.Extensions.$platform.$config.binlog"),
        ("/p:VersionNumber="+$VersionOfSDK)
      )

      & $msbuildPath $msbuildArgs
    }
  }
} Catch {
  $formatString = "`n{0}`n`n{1}`n`n"
  $fields = $_, $_.ScriptStackTrace
  Write-Host ($formatString -f $fields) -ForegroundColor RED
  Exit 1
}

foreach ($config in $Configuration.Split(",")) {
  if ($config -eq "release")
  {
    & $nugetPath pack (Join-Path $PSScriptRoot "nuget\Microsoft.CmdPal.Extensions.nuspec") -Version $VersionOfSDK -OutputDirectory "$PSScriptRoot\_build"
  } else {
Write-Host @"
WARNING: You are currently building as '$config' configuration.
CmdPalSDK nuget creation only supports 'release' configuration right now.
"@ -ForegroundColor YELLOW
  }
}

if ($IsAzurePipelineBuild) {
  Write-Host "##vso[task.setvariable variable=VersionOfSDK;]$VersionOfSDK"
  Write-Host "##vso[task.setvariable variable=VersionOfSDK;isOutput=true;]$VersionOfSDK"
}

$TotalTime = (Get-Date)-$StartTime
$TotalMinutes = [math]::Floor($TotalTime.TotalMinutes)
$TotalSeconds = [math]::Ceiling($TotalTime.TotalSeconds)

Write-Host @"
Total Running Time:
$TotalMinutes minutes and $TotalSeconds seconds
"@ -ForegroundColor CYAN
