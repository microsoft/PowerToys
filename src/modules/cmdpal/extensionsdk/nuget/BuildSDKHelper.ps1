Param(
  [string]$Configuration = "release",
  [string]$VersionOfSDK = "0.0.0",
  [string]$BuildStep = "all",
  [switch]$IsAzurePipelineBuild = $false,
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
      Builds the Command Palette SDK

Options:

  -Configuration <configuration>
      Only build the selected configuration(s)
      Example: -Configuration Release
      Example: -Configuration "Debug,Release"

  -VersionOfSDK <version>
      Set the version number of the build sdk nuget package
      Example: -VersionOfSDK "1.0.0"

  -Help
      Display this usage message.
"@
  Exit
}

$ErrorActionPreference = "Stop"

$buildPlatforms = "x64","arm64"

$msbuildPath = &"${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -prerelease -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe
if ($IsAzurePipelineBuild) {
  $nugetPath = "nuget.exe";
} else {
  $nugetPath = (Join-Path $PSScriptRoot "NugetWrapper.cmd")
}

if (($BuildStep -ieq "all") -Or ($BuildStep -ieq "build")) {
  & $nugetPath restore (Join-Path $PSScriptRoot "..\..\..\..\..\PowerToys.sln")

  Try {
    foreach ($config in $Configuration.Split(",")) {
      foreach ($platform in $buildPlatforms) {
        $msbuildArgs = @(
          ("$PSScriptRoot\..\Microsoft.CommandPalette.Extensions.Toolkit\Microsoft.CommandPalette.Extensions.Toolkit.csproj"),
          ("/p:Platform="+$platform),
          ("/p:Configuration="+$config),
          ("/binaryLogger:CmdPal.Extensions.$platform.$config.binlog"),
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
}

if (($BuildStep -ieq "all") -Or ($BuildStep -ieq "pack")) {
  foreach ($config in $Configuration.Split(",")) {
    if ($config -eq "release")
    {
      New-Item -ItemType Directory -Force -Path "$PSScriptRoot\..\_build"
      & $nugetPath pack (Join-Path $PSScriptRoot "Microsoft.CommandPalette.Extensions.SDK.nuspec") -Version $VersionOfSDK -OutputDirectory "$PSScriptRoot\..\_build"
    } else {
      Write-Host @"
WARNING: You are currently building as '$config' configuration.
CmdPalSDK nuget creation only supports 'release' configuration right now.
"@ -ForegroundColor YELLOW
    }
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
