# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

[CmdletBinding()]
param(
    [ValidateSet("arm64", "x64")]
    [string]$Platform = "arm64",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [switch]$Install,
    [switch]$NoSign
)

$ErrorActionPreference = "Stop"

function Find-WindowsSdkTool {
    param(
        [Parameter(Mandatory)]
        [string]$ToolName,

        [string]$Architecture = "x64"
    )

    $patterns = @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\$Architecture\$ToolName",
        "${env:ProgramFiles}\Windows Kits\10\bin\*\$Architecture\$ToolName"
    )

    foreach ($pattern in $patterns) {
        $match = Get-ChildItem $pattern -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending |
            Select-Object -First 1
        if ($match) {
            return $match.FullName
        }
    }

    throw "$ToolName was not found in Windows SDK."
}

function Get-MsBuildPath {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path $vswhere)) {
        throw "vswhere.exe not found at $vswhere"
    }

    $msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
    if (-not $msbuild) {
        throw "MSBuild.exe not found."
    }

    return $msbuild
}

function Get-Publisher {
    param(
        [Parameter(Mandatory)]
        [string]$RepoRoot
    )

    $hint = Join-Path $RepoRoot "src\PackageIdentity\.user\PowerToysSparse.publisher.txt"
    if (Test-Path $hint) {
        return (Get-Content -LiteralPath $hint -Raw).Trim()
    }

    return "CN=PowerToys Dev, O=PowerToys, L=Redmond, S=Washington, C=US"
}

function Get-Version {
    param(
        [Parameter(Mandatory)]
        [string]$RepoRoot
    )

    $versionProps = Join-Path $RepoRoot "src\Version.props"
    if (-not (Test-Path $versionProps)) {
        return "0.0.1.0"
    }

    [xml]$xml = Get-Content -LiteralPath $versionProps -Raw
    $version = $xml.Project.PropertyGroup.Version
    if (-not $version) {
        return "0.0.1.0"
    }

    $version = $version.Trim()
    if (($version -split '\.').Count -lt 4) {
        $version = "$version.0"
    }

    return $version
}

function Get-PayloadFileNames {
    param(
        [Parameter(Mandatory)]
        [string]$DepsJsonPath
    )

    $deps = Get-Content -LiteralPath $DepsJsonPath -Raw | ConvertFrom-Json
    $target = $deps.runtimeTarget.name
    $targetNode = $deps.targets.$target
    if (-not $targetNode) {
        throw "Unable to resolve deps target '$target' from $DepsJsonPath"
    }

    $files = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $alwaysInclude = @(
        "SettingsSearchEvaluation.exe",
        "SettingsSearchEvaluation.dll",
        "SettingsSearchEvaluation.deps.json",
        "SettingsSearchEvaluation.runtimeconfig.json",
        "SettingsSearchEvaluation.pri"
    )

    foreach ($item in $alwaysInclude) {
        [void]$files.Add($item)
    }

    foreach ($lib in $targetNode.PSObject.Properties) {
        foreach ($bucketName in @("runtime", "native")) {
            $bucket = $lib.Value.$bucketName
            if ($null -eq $bucket) {
                continue
            }

            foreach ($asset in $bucket.PSObject.Properties.Name) {
                $leaf = [System.IO.Path]::GetFileName($asset)
                if (-not [string]::IsNullOrWhiteSpace($leaf)) {
                    [void]$files.Add($leaf)
                }
            }
        }
    }

    return $files
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$outputRoot = Join-Path $repoRoot "$Platform\$Configuration"
$projectPath = Join-Path $repoRoot "tools\SettingsSearchEvaluation\SettingsSearchEvaluation.csproj"
$artifactsRoot = Join-Path $PSScriptRoot "artifacts\full-package"
$stagingDir = Join-Path $artifactsRoot "staging"
$packagePath = Join-Path $artifactsRoot "SettingsSearchEvaluation.msix"
$manifestPath = Join-Path $stagingDir "AppxManifest.xml"

if (Test-Path $stagingDir) {
    Remove-Item -LiteralPath $stagingDir -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null
New-Item -ItemType Directory -Path $artifactsRoot -Force | Out-Null

Write-Host "Building evaluator ($Platform/$Configuration) without sparse identity..."
$msbuild = Get-MsBuildPath
& $msbuild $projectPath /t:Build /p:Configuration=$Configuration /p:Platform=$Platform /p:UseSparseIdentity=false /p:WindowsAppSDKSelfContained=false /p:WindowsAppSdkUndockedRegFreeWinRTInitialize=false /m:1 /nologo

$depsPath = Join-Path $outputRoot "SettingsSearchEvaluation.deps.json"
if (-not (Test-Path $depsPath)) {
    throw "Missing build output: $depsPath"
}

$payloadFiles = Get-PayloadFileNames -DepsJsonPath $depsPath
foreach ($name in $payloadFiles) {
    $source = Join-Path $outputRoot $name
    if (Test-Path $source) {
        Copy-Item -LiteralPath $source -Destination (Join-Path $stagingDir $name) -Force
    }
}

# Ensure the entry point exists.
if (-not (Test-Path (Join-Path $stagingDir "SettingsSearchEvaluation.exe"))) {
    throw "Packaging failed: SettingsSearchEvaluation.exe was not copied to staging."
}

$imagesSource = Join-Path $repoRoot "src\PackageIdentity\Images"
Copy-Item -LiteralPath (Join-Path $imagesSource "Square150x150Logo.png") -Destination (Join-Path $stagingDir "Square150x150Logo.png") -Force
Copy-Item -LiteralPath (Join-Path $imagesSource "Square44x44Logo.png") -Destination (Join-Path $stagingDir "Square44x44Logo.png") -Force
Copy-Item -LiteralPath (Join-Path $imagesSource "StoreLogo.png") -Destination (Join-Path $stagingDir "StoreLogo.png") -Force

$publisher = Get-Publisher -RepoRoot $repoRoot
$version = Get-Version -RepoRoot $repoRoot

$manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
  xmlns:desktop="http://schemas.microsoft.com/appx/manifest/desktop/windows10"
  xmlns:systemai="http://schemas.microsoft.com/appx/manifest/systemai/windows10"
  IgnorableNamespaces="uap rescap desktop systemai">
  <Identity
    Name="Microsoft.PowerToys.SettingsSearchEvaluation"
    Publisher="$publisher"
    Version="$version" />
  <Properties>
    <DisplayName>PowerToys Settings Search Evaluation</DisplayName>
    <PublisherDisplayName>PowerToys</PublisherDisplayName>
    <Logo>StoreLogo.png</Logo>
  </Properties>
  <Resources>
    <Resource Language="en-us" />
  </Resources>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19000.0" MaxVersionTested="10.0.26226.0" />
    <PackageDependency Name="Microsoft.WindowsAppRuntime.2.0-experimental4" MinVersion="0.738.2207.0" Publisher="CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US" />
  </Dependencies>
  <Capabilities>
    <Capability Name="internetClient" />
    <rescap:Capability Name="runFullTrust" />
    <rescap:Capability Name="unvirtualizedResources" />
    <systemai:Capability Name="systemAIModels" />
  </Capabilities>
  <Applications>
    <Application Id="SettingsSearchEvaluation" Executable="SettingsSearchEvaluation.exe" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements
        DisplayName="PowerToys Settings Search Evaluation"
        Description="Settings search performance and recall evaluator"
        BackgroundColor="transparent"
        Square150x150Logo="Square150x150Logo.png"
        Square44x44Logo="Square44x44Logo.png"
        AppListEntry="none" />
    </Application>
  </Applications>
</Package>
"@
Set-Content -LiteralPath $manifestPath -Value $manifest -Encoding UTF8

if (Test-Path $packagePath) {
    Remove-Item -LiteralPath $packagePath -Force
}

$makeAppx = Find-WindowsSdkTool -ToolName "makeappx.exe"
Write-Host "Packing MSIX: $packagePath"
& $makeAppx pack /d $stagingDir /p $packagePath /o /nv

if (-not $NoSign) {
    $thumbFile = Join-Path $repoRoot "src\PackageIdentity\.user\PowerToysSparse.certificate.sample.thumbprint"
    if (-not (Test-Path $thumbFile)) {
        throw "Signing certificate thumbprint file not found: $thumbFile"
    }

    $thumb = (Get-Content -LiteralPath $thumbFile -Raw).Trim()
    if ([string]::IsNullOrWhiteSpace($thumb)) {
        throw "Signing certificate thumbprint is empty: $thumbFile"
    }

    $signtool = Find-WindowsSdkTool -ToolName "signtool.exe"
    Write-Host "Signing MSIX..."
    & $signtool sign /fd SHA256 /sha1 $thumb $packagePath
}

Write-Host "Package created: $packagePath"

if ($Install) {
    Write-Host "Installing package..."
    Add-AppxPackage -Path $packagePath -ForceApplicationShutdown
    $pkg = Get-AppxPackage Microsoft.PowerToys.SettingsSearchEvaluation
    if ($pkg) {
        Write-Host "Installed PackageFamilyName: $($pkg.PackageFamilyName)"
        Write-Host "Launch AUMID: shell:AppsFolder\$($pkg.PackageFamilyName)!SettingsSearchEvaluation"
    }
}
