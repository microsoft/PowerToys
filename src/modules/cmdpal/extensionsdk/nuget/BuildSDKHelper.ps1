Param(
  [string]$Configuration = "release",
  [string]$VersionOfSDK = "",
  [string]$BuildStep = "all",
  [switch]$IsAzurePipelineBuild = $false,
  [switch]$Help = $false,
  [string]$ExpectedDotNetSdkVersion = ""
)

If ([String]::IsNullOrEmpty($VersionOfSDK)) {
  $VersionOfSDK = $Env:XES_PACKAGEVERSIONNUMBER
}

If ([String]::IsNullOrEmpty($VersionOfSDK)) {
  $VersionOfSDK = "0.0.0"
}

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

  -ExpectedDotNetSdkVersion <version>
      Require MSBuild to resolve this .NET SDK version before restoring.
      Example: -ExpectedDotNetSdkVersion "10.0.303"

  -Help
      Display this usage message.
"@
  Exit
}

$ErrorActionPreference = "Stop"

$buildPlatforms = "x64","arm64"

$msbuildPath = &"${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -prerelease -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($msbuildPath)) {
  throw "Could not locate MSBuild using vswhere."
}
if ($IsAzurePipelineBuild) {
  $nugetPath = "nuget.exe";
} else {
  $nugetPath = (Join-Path $PSScriptRoot "NugetWrapper.cmd")
}
$solutionPath = (Join-Path $PSScriptRoot "..\..\..\..\..\PowerToys.slnx")
$toolkitProject = (Join-Path $PSScriptRoot "..\Microsoft.CommandPalette.Extensions.Toolkit\Microsoft.CommandPalette.Extensions.Toolkit.csproj")
$packageOutputPath = (Join-Path $PSScriptRoot "..\_build")
$nuspecPath = (Join-Path $PSScriptRoot "Microsoft.CommandPalette.Extensions.SDK.nuspec")

if (($BuildStep -ieq "all") -Or ($BuildStep -ieq "build")) {
  if (-not [string]::IsNullOrWhiteSpace($ExpectedDotNetSdkVersion)) {
    $resolvedSdk = & $msbuildPath $toolkitProject '-nologo' '-verbosity:quiet' '-getProperty:NETCoreSdkVersion'
    if ($LASTEXITCODE -ne 0) {
      throw "Failed to resolve the Toolkit's .NET SDK version (MSBuild exit code $LASTEXITCODE)."
    }
    $resolvedSdk = ($resolvedSdk | Out-String).Trim()
    Write-Host "Resolved .NET SDK for Toolkit: $resolvedSdk"
    if ($resolvedSdk -ne $ExpectedDotNetSdkVersion) {
      throw "Expected .NET SDK $ExpectedDotNetSdkVersion, but MSBuild resolved '$resolvedSdk'."
    }
  }

  $restoreArgs = @(
    $solutionPath
    "/t:Restore"
    "/p:RestorePackagesConfig=true"
  )
  & $msbuildPath @restoreArgs
  if ($LASTEXITCODE -ne 0) {
    throw "Solution restore failed with exit code $LASTEXITCODE."
  }

  foreach ($config in $Configuration.Split(",")) {
    foreach ($platform in $buildPlatforms) {
      $msbuildArgs = @(
        $toolkitProject,
        ("/p:Platform="+$platform),
        ("/p:Configuration="+$config),
        ("/binaryLogger:CmdPal.Extensions.$platform.$config.binlog"),
        ("/p:VersionNumber="+$VersionOfSDK)
        )

      if ($IsAzurePipelineBuild) {
        $msbuildArgs += "/p:CIBuild=true"
      }

      & $msbuildPath @msbuildArgs
      if ($LASTEXITCODE -ne 0) {
        throw "Toolkit $platform $config build failed with exit code $LASTEXITCODE."
      }
    }
  }
}

if (($BuildStep -ieq "all") -Or ($BuildStep -ieq "pack")) {
  foreach ($config in $Configuration.Split(",")) {
    if ($config -eq "release")
    {
      New-Item -ItemType Directory -Force -Path $packageOutputPath
      & $nugetPath pack $nuspecPath -Version $VersionOfSDK -OutputDirectory $packageOutputPath
      if ($LASTEXITCODE -ne 0) {
        throw "SDK packaging failed with exit code $LASTEXITCODE."
      }
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
