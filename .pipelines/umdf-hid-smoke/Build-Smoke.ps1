# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#requires -Version 7.0
param(
    [Parameter(Mandatory)][string]$WorkRoot,
    [Parameter(Mandatory)][string]$OutputRoot,
    [string[]]$Platforms = @('x64', 'ARM64'),
    [string]$PackageCache,
    [switch]$DriverOnly
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$revision = 'bb68ed5e770bce04dc5395ec9258dafab034184d'
$version = '10.0.28000.2526'
$work = [IO.Path]::GetFullPath($WorkRoot)
$output = [IO.Path]::GetFullPath($OutputRoot)
New-Item $work, $output -ItemType Directory -Force | Out-Null
$sample = Join-Path $work 'sample'
if (-not (Test-Path (Join-Path $sample '.git'))) {
    & git init -q $sample
    if ($LASTEXITCODE) { throw 'Could not initialize the pinned upstream checkout.' }
    & git -C $sample remote add origin https://github.com/microsoft/Windows-driver-samples.git
    if ($LASTEXITCODE) { throw 'Could not configure the public upstream remote.' }
    & git -C $sample sparse-checkout set hid/vhidmini2
    if ($LASTEXITCODE) { throw 'Could not configure the sparse checkout.' }
    & git -C $sample fetch --depth 1 origin $revision
    if ($LASTEXITCODE) { throw 'Could not fetch the pinned Microsoft sample.' }
    & git -C $sample checkout --detach FETCH_HEAD
    if ($LASTEXITCODE) { throw 'Could not check out the pinned Microsoft sample.' }
}
$actualRevision = & git -C $sample rev-parse HEAD
if ($LASTEXITCODE -or $actualRevision -ne $revision) { throw 'Unexpected upstream sample revision.' }
$patch = Join-Path $PSScriptRoot 'vhidmini-input.patch'
& git -C $sample apply --unidiff-zero --reverse --check $patch 2>$null
if ($LASTEXITCODE) {
    & git -C $sample apply --unidiff-zero --check $patch
    if ($LASTEXITCODE) { throw 'The input patch does not match the pinned sample.' }
    & git -C $sample apply --unidiff-zero $patch
    if ($LASTEXITCODE) { throw 'Could not apply the input report patch.' }
}

$nuget = Join-Path $work 'nuget.exe'
if (-not (Test-Path $nuget)) {
    Invoke-WebRequest 'https://dist.nuget.org/win-x86-commandline/v6.14.0/nuget.exe' -OutFile $nuget
}
$signature = Get-AuthenticodeSignature $nuget
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Microsoft') {
    throw 'The NuGet executable is not signed by Microsoft.'
}
$packages = if ($PackageCache) { [IO.Path]::GetFullPath($PackageCache) } else { Join-Path $work 'packages' }
$downloads = Join-Path $work 'downloads'
New-Item $downloads -ItemType Directory -Force | Out-Null
$ids = @('Microsoft.Windows.SDK.CPP', 'Microsoft.Windows.SDK.CPP.x64', 'Microsoft.Windows.WDK.x64')
if ($Platforms -contains 'ARM64') {
    $ids += 'Microsoft.Windows.SDK.CPP.arm64', 'Microsoft.Windows.WDK.arm64'
}
foreach ($id in $ids) {
    $name = "$id.$version.nupkg"
    $file = if ($PackageCache) { Join-Path $packages "$id.$version\$name" } else { Join-Path $downloads $name }
    if (-not (Test-Path $file)) {
        if ($PackageCache) { throw "The requested verified package cache is incomplete: $name" }
        Invoke-WebRequest "https://www.nuget.org/api/v2/package/$id/$version" -OutFile $file
    }
    & $nuget verify -All $file | Out-File (Join-Path $work 'dependency-verification.log') -Append
    if ($LASTEXITCODE) { throw "Package signature verification failed: $name" }
}
if (-not $PackageCache) {
    $config = Join-Path $work 'NuGet.Config'
    $escapedDownloads = [Security.SecurityElement]::Escape($downloads)
    "<configuration><packageSources><clear/><add key=`"VerifiedMicrosoft`" value=`"$escapedDownloads`"/></packageSources><packageSourceMapping><clear/><packageSource key=`"VerifiedMicrosoft`"><package pattern=`"Microsoft.Windows.*`"/></packageSource></packageSourceMapping></configuration>" |
        Set-Content $config
    $packageReferences = ($ids | ForEach-Object { "<package id=`"$_`" version=`"$version`" />" }) -join ''
    $packageManifest = Join-Path $work 'packages.config'
    "<packages>$packageReferences</packages>" | Set-Content $packageManifest
    & $nuget restore $packageManifest -PackagesDirectory $packages -ConfigFile $config -NonInteractive -Verbosity quiet
    if ($LASTEXITCODE) { throw 'Official WDK/SDK restore failed.' }
}
$signTool = Get-ChildItem (Join-Path $packages "Microsoft.Windows.SDK.CPP.$version") -Recurse -Filter signtool.exe |
    Where-Object FullName -Match '\\x64\\' | Select-Object -First 1 -ExpandProperty FullName
$wdk = Join-Path $packages "Microsoft.Windows.WDK.x64.$version\c"
$inf2cat = Get-ChildItem $wdk -Recurse -Filter inf2cat.exe | Select-Object -First 1 -ExpandProperty FullName
if (-not $signTool -or -not $inf2cat) { throw 'Official signing/catalog tools were not found.' }

$certificate = New-SelfSignedCertificate -Subject 'CN=PowerToys Isolated UMDF CI Smoke' `
    -CertStoreLocation Cert:\CurrentUser\My -Type CodeSigningCert -HashAlgorithm SHA256 `
    -KeyExportPolicy NonExportable -NotAfter (Get-Date).AddDays(7)
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
        Export-Certificate -Cert $certificate -FilePath (Join-Path $driver 'PtHidProbe.cer') | Out-Null
        foreach ($binary in @('VhidminiUm.dll', 'CreateUmdfHidDevice.exe')) {
            & $signTool sign /s My /sha1 $certificate.Thumbprint /fd SHA256 (Join-Path $driver $binary)
            if ($LASTEXITCODE) { throw "Test binary signing failed: $platform, $binary" }
        }
        $catalogOs = if ($platform -eq 'ARM64') { '10_VB_ARM64,10_CO_ARM64' } else { '10_VB_X64,10_CO_X64' }
        & $inf2cat "/driver:$driver" "/os:$catalogOs" /uselocaltime
        if ($LASTEXITCODE) { throw "Driver catalog generation failed: $platform" }
        & $signTool sign /s My /sha1 $certificate.Thumbprint /fd SHA256 (Join-Path $driver 'PtHidProbe.cat')
        if ($LASTEXITCODE) { throw "Catalog signing failed: $platform" }
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
            $client = Join-Path $repo "$platform\Release\tests\GrabAndMove.HidSmoke.Probe\net10.0-windows10.0.26100.0"
            New-Item (Join-Path $destination 'tests') -ItemType Directory -Force | Out-Null
            Copy-Item "$client\*" (Join-Path $destination 'tests') -Recurse -Force
        }
        Get-ChildItem $driver -File | Get-FileHash -Algorithm SHA256 | Select-Object Path, Hash |
            ConvertTo-Json | Set-Content (Join-Path $destination 'driver-hashes.json')
    }
}
finally {
    Remove-Item "Cert:\CurrentUser\My\$($certificate.Thumbprint)" -DeleteKey
    foreach ($name in $originalBuildEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $originalBuildEnvironment[$name], 'Process')
    }
}
