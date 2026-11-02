# Copyright (c) Microsoft Corporation
# Licensed under the MIT license. See LICENSE in the project root.

#requires -Version 7.0
param(
    [Parameter(Mandatory)][string]$WorkRoot,
    [Parameter(Mandatory)][string]$OutputRoot,
    [ValidateSet('x64', 'ARM64')][string]$Platform = 'x64',
    [switch]$PrepareOnly
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$revision = 'bb68ed5e770bce04dc5395ec9258dafab034184d'
$version = '10.0.28000.2526'
$work = [IO.Path]::GetFullPath($WorkRoot)
$output = [IO.Path]::GetFullPath($OutputRoot)
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
New-Item $work, $output -ItemType Directory -Force | Out-Null
$sample = Join-Path $work 'sample'
if (-not (Test-Path (Join-Path $sample '.git'))) {
    & git init -q $sample
    if ($LASTEXITCODE) { throw 'git init failed.' }
    & git -C $sample remote add origin https://github.com/microsoft/Windows-driver-samples.git
    if ($LASTEXITCODE) { throw 'git remote failed.' }
    & git -C $sample sparse-checkout set video/IndirectDisplay
    if ($LASTEXITCODE) { throw 'git sparse-checkout failed.' }
    & git -C $sample fetch --depth 1 origin $revision
    if ($LASTEXITCODE) { throw 'Pinned sample fetch failed.' }
    & git -C $sample checkout --detach FETCH_HEAD
    if ($LASTEXITCODE) { throw 'Pinned sample checkout failed.' }
}
if ((& git -C $sample rev-parse HEAD) -ne $revision -or $LASTEXITCODE) {
    throw 'Unexpected upstream sample revision.'
}
$patch = Join-Path $PSScriptRoot 'idd-fixture.patch'
& git -C $sample apply --unidiff-zero --reverse --check $patch 2>$null
if ($LASTEXITCODE) {
    & git -C $sample apply --unidiff-zero --check $patch
    if ($LASTEXITCODE) { throw 'Fixture patch does not match the pinned sample.' }
    & git -C $sample apply --unidiff-zero $patch
    if ($LASTEXITCODE) { throw 'Fixture patch failed.' }
}

$nuget = Join-Path $work 'nuget.exe'
if (-not (Test-Path $nuget)) {
    Invoke-WebRequest https://dist.nuget.org/win-x86-commandline/v6.14.0/nuget.exe -OutFile $nuget
}
$signature = Get-AuthenticodeSignature $nuget
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Microsoft') {
    throw 'NuGet executable is not signed by Microsoft.'
}
$packages = Join-Path $work 'packages'
$downloads = Join-Path $work 'downloads'
New-Item $downloads -ItemType Directory -Force | Out-Null
$ids = @('Microsoft.Windows.SDK.CPP', 'Microsoft.Windows.SDK.CPP.x64', 'Microsoft.Windows.WDK.x64')
if ($Platform -eq 'ARM64') { $ids += 'Microsoft.Windows.SDK.CPP.arm64', 'Microsoft.Windows.WDK.arm64' }
foreach ($id in $ids) {
    $file = Join-Path $downloads "$id.$version.nupkg"
    if (-not (Test-Path $file)) {
        Invoke-WebRequest "https://www.nuget.org/api/v2/package/$id/$version" -OutFile $file
    }
    & $nuget verify -All $file | Out-File (Join-Path $work 'dependency-verification.log') -Append
    if ($LASTEXITCODE) { throw "Package signature verification failed: $id" }
}
$config = Join-Path $work 'NuGet.Config'
$source = [Security.SecurityElement]::Escape($downloads)
"<configuration><packageSources><clear/><add key=`"VerifiedMicrosoft`" value=`"$source`"/></packageSources></configuration>" | Set-Content $config
$manifest = Join-Path $work 'packages.config'
$entries = ($ids | ForEach-Object { "<package id=`"$_`" version=`"$version`"/>" }) -join ''
"<packages>$entries</packages>" | Set-Content $manifest
& $nuget restore $manifest -PackagesDirectory $packages -ConfigFile $config -NonInteractive -Verbosity quiet
if ($LASTEXITCODE) { throw 'WDK/SDK restore failed.' }
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
    if ($Platform -eq 'ARM64') { $inf = $inf.Replace('NTamd64', 'NTarm64') }
    Set-Content (Join-Path $driver 'PtDisplayIdd.inf') $inf -Encoding ascii
    Copy-Item (Join-Path $sample 'LICENSE') (Join-Path $driver 'MS-PL.txt')
    Copy-Item (Join-Path $repo 'LICENSE') (Join-Path $driver 'MIT-LICENSE.txt')
    Copy-Item (Join-Path $PSScriptRoot 'UPSTREAM.txt') $driver
    $signTool = Get-ChildItem (Join-Path $packages "Microsoft.Windows.SDK.CPP.$version") -Recurse -Filter signtool.exe |
        Where-Object FullName -Match '\\x64\\' | Select-Object -First 1 -ExpandProperty FullName
    $inf2cat = Get-ChildItem (Join-Path $packages "Microsoft.Windows.WDK.x64.$version") -Recurse -Filter inf2cat.exe |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not $signTool -or -not $inf2cat) { throw 'Official signing tools were not found.' }
    $certificate = New-SelfSignedCertificate -Subject 'CN=PowerToys Isolated IDD POC' `
        -CertStoreLocation Cert:\CurrentUser\My -Type CodeSigningCert -HashAlgorithm SHA256 `
        -KeyExportPolicy NonExportable -NotAfter (Get-Date).AddDays(7)
    try {
        Export-Certificate -Cert $certificate -FilePath (Join-Path $driver 'PtDisplayIdd.cer') | Out-Null
        foreach ($binary in @('PtDisplayIdd.dll', 'IddProbe.exe')) {
            & $signTool sign /s My /sha1 $certificate.Thumbprint /fd SHA256 (Join-Path $driver $binary)
            if ($LASTEXITCODE) { throw "Binary signing failed: $binary" }
        }
        $catalogOs = if ($Platform -eq 'ARM64') { '10_VB_ARM64,10_CO_ARM64' } else { '10_VB_X64,10_CO_X64' }
        & $inf2cat "/driver:$driver" "/os:$catalogOs" /uselocaltime
        if ($LASTEXITCODE) { throw 'Catalog generation failed.' }
        & $signTool sign /s My /sha1 $certificate.Thumbprint /fd SHA256 (Join-Path $driver 'PtDisplayIdd.cat')
        if ($LASTEXITCODE) { throw 'Catalog signing failed.' }
    }
    finally { Remove-Item "Cert:\CurrentUser\My\$($certificate.Thumbprint)" -DeleteKey }
    Get-ChildItem $driver -File | Get-FileHash -Algorithm SHA256 | Select-Object Path, Hash |
        ConvertTo-Json | Set-Content (Join-Path $destination 'driver-hashes.json')
    [ordered]@{
        UpstreamRevision = $revision
        WdkVersion = $version
        Platform = $Platform
        DriverVersion = '0.0.1.0'
        SigningThumbprint = $certificate.Thumbprint
    } | ConvertTo-Json | Set-Content (Join-Path $destination 'build.json')

    $productionProject = Join-Path $PSScriptRoot 'production\PowerDisplay.IddPoc.ProductionProbe.csproj'
    & dotnet restore $productionProject "-p:Platform=$Platform"
    if ($LASTEXITCODE) { throw 'Production discovery probe restore failed.' }
    & (Join-Path $repo 'tools\build\build.cmd') -Path (Split-Path $productionProject -Parent) -Platform $Platform -Configuration Release
    if ($LASTEXITCODE) { throw 'Production discovery probe build failed.' }
    $productionOutput = Join-Path $repo "$Platform\Release\tests\PowerDisplay.IddPoc.ProductionProbe\net10.0-windows10.0.26100.0"
    $production = Join-Path $destination 'production'
    New-Item $production -ItemType Directory -Force | Out-Null
    Copy-Item "$productionOutput\*" $production -Recurse -Force
    Get-ChildItem $production -File -Recurse | ForEach-Object {
        [ordered]@{
            Path = [IO.Path]::GetRelativePath($production, $_.FullName)
            Hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        }
    } | ConvertTo-Json | Set-Content (Join-Path $destination 'production-hashes.json')
}
finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
}
