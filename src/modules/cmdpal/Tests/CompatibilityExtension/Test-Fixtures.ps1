# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

param(
    [ValidateSet('x64', 'ARM64')]
    [string]$Platform = 'x64',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$runtime = if ($Platform -eq 'ARM64') { 'win-arm64' } else { 'win-x64' }
$packageIds = @()
$classIds = @()
[xml]$solution = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'CompatibilityExtension.slnx') -Raw
$projects = @($solution.Solution.Project | ForEach-Object {
    Get-Item -LiteralPath (Join-Path $PSScriptRoot $_.Path)
})
if ($projects.Count -eq 0) {
    throw 'The compatibility solution contains no fixture projects.'
}

foreach ($project in $projects) {
    [xml]$projectXml = Get-Content -LiteralPath $project.FullName -Raw
    $version = $projectXml.Project.PropertyGroup.CmdPalSdkVersion
    $assemblyName = $projectXml.Project.PropertyGroup.AssemblyName
    $clsid = $projectXml.Project.PropertyGroup.ExtensionClsid
    [xml]$manifest = Get-Content -LiteralPath (Join-Path $project.DirectoryName 'Package.appxmanifest') -Raw
    $namespaces = [System.Xml.XmlNamespaceManager]::new($manifest.NameTable)
    $namespaces.AddNamespace('p', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
    $namespaces.AddNamespace('com', 'http://schemas.microsoft.com/appx/manifest/com/windows10')
    $packageId = $manifest.SelectSingleNode('/p:Package/p:Identity', $namespaces).Name
    $manifestClass = $manifest.SelectSingleNode('//com:Class', $namespaces).Id
    $activationClass = $manifest.SelectSingleNode('//*[local-name()="CreateInstance"]').ClassId
    if ($clsid -ne $manifestClass -or $clsid -ne $activationClass) {
        throw "SDK $version has inconsistent COM class IDs."
    }

    if ($packageIds -contains $packageId -or $classIds -contains $clsid) {
        throw "SDK $version collides with another fixture identity."
    }

    $packageIds += $packageId
    $classIds += $clsid
    $assetsPath = Join-Path $project.DirectoryName 'obj/project.assets.json'
    $assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json
    $sdkLibrary = $assets.libraries.PSObject.Properties | Where-Object { $_.Name -eq "Microsoft.CommandPalette.Extensions/$version" }
    if (-not $sdkLibrary -or $sdkLibrary.Value.type -ne 'package') {
        throw "SDK $version was not restored from its pinned package."
    }

    if (@($assets.libraries.PSObject.Properties | Where-Object { $_.Value.type -eq 'project' }).Count -ne 0) {
        throw "SDK $version unexpectedly references an in-repo project."
    }

    $sources = @($assets.project.restore.sources.PSObject.Properties.Name | Where-Object { $_ -match '^https?://' })
    if ($sources.Count -ne 1 -or $sources[0] -ne 'https://pkgs.dev.azure.com/shine-oss/PowerToys/_packaging/PowerToysPublicDependencies/nuget/v3/index.json') {
        throw "SDK $version has an unexpected package source."
    }

    $output = Join-Path $project.DirectoryName "bin/$Platform/$Configuration/net10.0-windows10.0.26100.0/$runtime"
    $executable = Join-Path $output "$assemblyName.exe"
    if (-not (Test-Path -LiteralPath $executable)) {
        throw "Build $($project.Name) successfully before running the fixture checks: $executable"
    }

    $packageRoot = $assets.packageFolders.PSObject.Properties.Name | Select-Object -First 1
    $sdkRoot = Join-Path $packageRoot $sdkLibrary.Value.path
    $sdkFiles = @(
        'lib/net8.0-windows10.0.19041.0/Microsoft.CommandPalette.Extensions.Toolkit.dll',
        "runtimes/$runtime/native/Microsoft.CommandPalette.Extensions.dll",
        'winmd/Microsoft.CommandPalette.Extensions.winmd'
    )
    foreach ($sdkFile in $sdkFiles) {
        $fileName = Split-Path -Leaf $sdkFile
        $expectedHash = (Get-FileHash -LiteralPath (Join-Path $sdkRoot $sdkFile)).Hash
        $actualHash = (Get-FileHash -LiteralPath (Join-Path $output $fileName)).Hash
        if ($expectedHash -ne $actualHash) {
            throw "SDK $version output contains the wrong $fileName."
        }
    }

    $stdout = Join-Path $project.DirectoryName 'obj/fixture.stdout.log'
    $stderr = Join-Path $project.DirectoryName 'obj/fixture.stderr.log'
    $process = Start-Process -FilePath $executable -ArgumentList '--verify-fixture' -WorkingDirectory $output -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    Get-Content -LiteralPath $stdout
    if ($process.ExitCode -ne 0) {
        Get-Content -LiteralPath $stderr
        throw "SDK $version fixture checks failed with exit code $($process.ExitCode)."
    }
}

Write-Output "PASS: $($projects.Count) independent SDK fixtures. Live Command Palette UI testing is still required."
