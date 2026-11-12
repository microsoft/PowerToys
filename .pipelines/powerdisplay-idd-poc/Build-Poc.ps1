# Copyright (c) Microsoft Corporation
# Licensed under the MIT license. See LICENSE in the project root.

#requires -Version 7.0
param(
    [Parameter(Mandatory)][string]$WorkRoot,
    [Parameter(Mandatory)][string]$OutputRoot,
    [ValidateSet('x64', 'ARM64')][string]$Platform = 'x64',
    [string]$PackageCache,
    [switch]$PrepareOnly
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
. (Join-Path $PSScriptRoot '..\umdf\UmdfBuild.Common.ps1')
$work = [IO.Path]::GetFullPath($WorkRoot)
$output = [IO.Path]::GetFullPath($OutputRoot)
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
New-Item $work, $output -ItemType Directory -Force | Out-Null
$configuration = Get-UmdfBuildConfiguration
$sample = Initialize-UmdfSample -WorkRoot $work -Revision $configuration.UpstreamRevision `
    -SparsePath video/IndirectDisplay -PatchPath (Join-Path $PSScriptRoot 'idd-fixture.patch')
$tools = Initialize-UmdfBuildTools -WorkRoot $work -WdkVersion $configuration.WdkVersion -Platforms $Platform -PackageCache $PackageCache
$packages = $tools.Packages
if ($PrepareOnly) { return }

$names = @('IddSampleRoot', 'IddPackagesRoot', 'IddOutputDir', 'IddIntermediateDir')
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    $env:IddSampleRoot = $sample
    $env:IddPackagesRoot = $packages
    $destination = Join-Path $output $Platform
    $driver = Join-Path $destination 'driver'
    $env:IddOutputDir = $driver
    $env:IddIntermediateDir = Join-Path $work "obj\$Platform"
    & (Join-Path $repo 'tools\build\build.cmd') -Path $PSScriptRoot -Platform $Platform -Configuration Release
    if ($LASTEXITCODE) { throw "IDD POC build failed: $Platform" }

    $inf = Get-Content (Join-Path $PSScriptRoot 'PtDisplayIdd.inf') -Raw
    $driverVersion = Get-UmdfInfDriverVersion -Content $inf
    if ($Platform -eq 'ARM64') { $inf = $inf.Replace('NTamd64', 'NTarm64') }
    Set-Content (Join-Path $driver 'PtDisplayIdd.inf') $inf -Encoding ascii
    Copy-Item (Join-Path $PSScriptRoot 'MS-PL.txt') (Join-Path $driver 'MS-PL.txt')
    Copy-Item (Join-Path $repo 'LICENSE') (Join-Path $driver 'MIT-LICENSE.txt')
    Copy-Item (Join-Path $PSScriptRoot 'UPSTREAM.txt') $driver
    $thumbprint = Complete-UmdfDriverPackage -DriverDirectory $driver -Platform $Platform `
        -Subject 'CN=PowerToys Isolated IDD POC' -CertificateName PtDisplayIdd.cer `
        -Binaries PtDisplayIdd.dll, IddProbe.exe -CatalogName PtDisplayIdd.cat -SignTool $tools.SignTool -Inf2Cat $tools.Inf2Cat
    Write-UmdfFileHashes -Directory $driver -ManifestPath (Join-Path $destination 'driver-hashes.json')
    [ordered]@{
        UpstreamRevision = $configuration.UpstreamRevision
        WdkVersion = $configuration.WdkVersion
        Platform = $Platform
        DriverVersion = $driverVersion
        SigningThumbprint = $thumbprint
    } | ConvertTo-Json | Set-Content (Join-Path $destination 'build.json')

    $productionProject = Join-Path $PSScriptRoot 'production\PowerDisplay.IddPoc.ProductionProbe.csproj'
    & dotnet restore $productionProject "-p:Platform=$Platform"
    if ($LASTEXITCODE) { throw 'Production discovery probe restore failed.' }
    & (Join-Path $repo 'tools\build\build.cmd') -Path (Split-Path $productionProject -Parent) -Platform $Platform -Configuration Release
    if ($LASTEXITCODE) { throw 'Production discovery probe build failed.' }
    $productionOutput = Get-UmdfManagedOutput -Project $productionProject -Platform $Platform -ExpectedExecutable PowerDisplay.IddPoc.ProductionProbe.exe
    $production = Join-Path $destination 'production'
    New-Item $production -ItemType Directory -Force | Out-Null
    Copy-Item "$productionOutput\*" $production -Recurse -Force
    Write-UmdfFileHashes -Directory $production -ManifestPath (Join-Path $destination 'production-hashes.json') -Relative
}
finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
}
