# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#requires -Version 7.0
param(
    [Parameter(Mandatory)][string]$WorkRoot,
    [Parameter(Mandatory)][string]$OutputRoot,
    [ValidateSet('x64', 'ARM64')][string[]]$Platforms = @('x64', 'ARM64'),
    [string]$PackageCache,
    [switch]$DriverOnly
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
. (Join-Path $PSScriptRoot '..\umdf\UmdfBuild.Common.ps1')
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$work = [IO.Path]::GetFullPath($WorkRoot)
$output = [IO.Path]::GetFullPath($OutputRoot)
New-Item $work, $output -ItemType Directory -Force | Out-Null
$configuration = Get-UmdfBuildConfiguration
$sample = Initialize-UmdfSample -WorkRoot $work -Revision $configuration.UpstreamRevision `
    -SparsePath hid/vhidmini2 -PatchPath (Join-Path $PSScriptRoot 'vhidmini-input.patch')
$tools = Initialize-UmdfBuildTools -WorkRoot $work -WdkVersion $configuration.WdkVersion -Platforms $Platforms -PackageCache $PackageCache
$packages = $tools.Packages
$originalBuildEnvironment = @{}
foreach ($name in @('HidSampleRoot', 'HidPackagesRoot', 'HidOutputDir', 'HidIntermediateDir')) {
    $originalBuildEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
try {
    foreach ($requested in $Platforms) {
        $platform = switch ($requested.ToLowerInvariant()) {
            'x64' { 'x64' }
            'arm64' { 'ARM64' }
            default { throw "Unsupported smoke platform: $requested" }
        }
        $destination = Join-Path $output $platform
        $driver = Join-Path $destination 'driver'
        New-Item $driver -ItemType Directory -Force | Out-Null
        # MSBuild imports environment properties without the CMD wrapper's array quoting.
        $env:HidSampleRoot = $sample
        $env:HidPackagesRoot = $packages
        $env:HidOutputDir = $driver
        $env:HidIntermediateDir = Join-Path $work "obj\$platform"
        & (Join-Path $repo 'tools\build\build.cmd') -Path (Join-Path $PSScriptRoot 'driver') `
            -Platform $platform -Configuration Release
        if ($LASTEXITCODE) { throw "Native UMDF build failed: $platform" }
        & (Join-Path $repo 'tools\build\build.cmd') -Path (Join-Path $PSScriptRoot 'device') `
            -Platform $platform -Configuration Release
        if ($LASTEXITCODE) { throw "Native root-device helper build failed: $platform" }
        $inf = Get-Content (Join-Path $PSScriptRoot 'driver\PtHidProbe.inf') -Raw
        if ($platform -eq 'ARM64') { $inf = $inf.Replace('NTamd64', 'NTarm64') }
        Set-Content (Join-Path $driver 'PtHidProbe.inf') $inf -Encoding ascii
        $null = Complete-UmdfDriverPackage -DriverDirectory $driver -Platform $platform `
            -Subject 'CN=PowerToys Isolated UMDF CI Smoke' -CertificateName PtHidProbe.cer `
            -Binaries VhidminiUm.dll, CreateUmdfHidDevice.exe -CatalogName PtHidProbe.cat -SignTool $tools.SignTool -Inf2Cat $tools.Inf2Cat
        $obsoleteTool = Join-Path $driver 'devcon.exe'
        if (Test-Path -LiteralPath $obsoleteTool) { Remove-Item -LiteralPath $obsoleteTool }
        Copy-Item (Join-Path $sample 'LICENSE') (Join-Path $driver 'MS-PL.txt')
        Copy-Item (Join-Path $repo 'LICENSE') (Join-Path $driver 'MIT-LICENSE.txt')
        Copy-Item (Join-Path $PSScriptRoot 'UPSTREAM.txt') $driver

        if (-not $DriverOnly) {
            & dotnet restore (Join-Path $PSScriptRoot 'GrabAndMove.HidSmoke.Probe.csproj') "-p:Platform=$platform"
            if ($LASTEXITCODE) { throw "Smoke client restore failed: $platform" }
            & (Join-Path $repo 'tools\build\build.cmd') -Path $PSScriptRoot -Platform $platform -Configuration Release
            if ($LASTEXITCODE) { throw "Smoke client build failed: $platform" }
            $client = Get-UmdfManagedOutput -Project (Join-Path $PSScriptRoot 'GrabAndMove.HidSmoke.Probe.csproj') `
                -Platform $platform -ExpectedExecutable GrabAndMove.HidSmoke.Probe.exe
            New-Item (Join-Path $destination 'tests') -ItemType Directory -Force | Out-Null
            Copy-Item "$client\*" (Join-Path $destination 'tests') -Recurse -Force
        }
        Write-UmdfFileHashes -Directory $driver -ManifestPath (Join-Path $destination 'driver-hashes.json')
    }
}
finally {
    foreach ($name in $originalBuildEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $originalBuildEnvironment[$name], 'Process')
    }
}
