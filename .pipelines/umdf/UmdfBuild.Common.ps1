# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

function Get-UmdfBuildConfiguration {
    [pscustomobject]@{
        UpstreamRevision = 'bb68ed5e770bce04dc5395ec9258dafab034184d'
        WdkVersion = '10.0.28000.2526'
        NugetSha256 = '92DBED160DDEE0F64B901E907439E021211B428E57C089ECC12FC38DCC4BD9A5'
        PackageSha256 = @{
            'Microsoft.Windows.SDK.CPP' = 'BE1B419491607EAE6F7C57844EBAB39FACE9643C51E2AF1D9176A3BA0D0B23FC'
            'Microsoft.Windows.SDK.CPP.x64' = 'A9CAE2A8C5DA7F5DC5838AE6A76D06D0D2E2FDC3D8CFC69CA6C184E4B9193A00'
            'Microsoft.Windows.WDK.x64' = '63C939FB5A79295BF40E941DB592681272219B04EDFF095FE2F3D123E5579A90'
            'Microsoft.Windows.SDK.CPP.arm64' = 'F890C85F46CC76C094F30FA555A438A212EE3D36165BCD7AFDCA96BB52C9BC7A'
            'Microsoft.Windows.WDK.arm64' = 'DCD83A77C83EE3163FA07B26421C0B132AF0EA446D391BDA2E63AC6664A261A5'
        }
    }
}

function Assert-UmdfDependencyHash {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$Sha256)
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Sha256) {
        throw "Unexpected dependency SHA256: $Path"
    }
}

function Get-UmdfInfDriverVersion {
    param([Parameter(Mandatory)][string]$Content)
    $versions = [regex]::Matches($Content, '(?im)^\s*DriverVer\s*=\s*[^,\r\n]+,\s*(\d+\.\d+\.\d+\.\d+)\s*$')
    if ($versions.Count -ne 1) { throw 'The fixture INF must contain exactly one four-component DriverVer.' }
    $versions[0].Groups[1].Value
}

function Initialize-UmdfSample {
    param(
        [Parameter(Mandatory)][string]$WorkRoot,
        [Parameter(Mandatory)][string]$Revision,
        [Parameter(Mandatory)][string]$SparsePath,
        [Parameter(Mandatory)][string]$PatchPath
    )
    $sample = Join-Path ([IO.Path]::GetFullPath($WorkRoot)) 'sample'
    if (-not (Test-Path (Join-Path $sample '.git'))) {
        & git init -q $sample | Out-Host
        if ($LASTEXITCODE) { throw 'Could not initialize the pinned upstream checkout.' }
        & git -C $sample remote add origin https://github.com/microsoft/Windows-driver-samples.git | Out-Host
        if ($LASTEXITCODE) { throw 'Could not configure the public upstream remote.' }
        & git -C $sample sparse-checkout set $SparsePath | Out-Host
        if ($LASTEXITCODE) { throw 'Could not configure the sparse checkout.' }
        & git -C $sample fetch --depth 1 origin $Revision | Out-Host
        if ($LASTEXITCODE) { throw 'Could not fetch the pinned Microsoft sample.' }
        & git -C $sample checkout --detach FETCH_HEAD | Out-Host
        if ($LASTEXITCODE) { throw 'Could not check out the pinned Microsoft sample.' }
    }
    $actual = & git -C $sample rev-parse HEAD
    if ($LASTEXITCODE -or $actual -ne $Revision) { throw 'Unexpected upstream sample revision.' }
    & git -C $sample apply --unidiff-zero --reverse --check $PatchPath 2>$null
    if ($LASTEXITCODE) {
        & git -C $sample apply --unidiff-zero --check $PatchPath | Out-Host
        if ($LASTEXITCODE) { throw 'The fixture patch does not match the pinned sample.' }
        & git -C $sample apply --unidiff-zero $PatchPath | Out-Host
        if ($LASTEXITCODE) { throw 'Could not apply the fixture patch.' }
    }
    $sample
}

function Get-UmdfPackageIds {
    param([Parameter(Mandatory)][ValidateSet('x64', 'ARM64')][string[]]$Platforms)
    'Microsoft.Windows.SDK.CPP', 'Microsoft.Windows.SDK.CPP.x64', 'Microsoft.Windows.WDK.x64'
    if ($Platforms -contains 'ARM64') { 'Microsoft.Windows.SDK.CPP.arm64', 'Microsoft.Windows.WDK.arm64' }
}

function Initialize-UmdfBuildTools {
    param(
        [Parameter(Mandatory)][string]$WorkRoot,
        [Parameter(Mandatory)][string]$WdkVersion,
        [Parameter(Mandatory)][ValidateSet('x64', 'ARM64')][string[]]$Platforms,
        [string]$PackageCache
    )
    $configuration = Get-UmdfBuildConfiguration
    if ($WdkVersion -ne $configuration.WdkVersion) {
        throw "Unpinned WDK/SDK version: $WdkVersion. Update the version and dependency hashes together."
    }
    $work = [IO.Path]::GetFullPath($WorkRoot)
    $cache = Join-Path (Split-Path $work -Parent) "umdf-dependencies\$WdkVersion"
    $packages = if ($PackageCache) { [IO.Path]::GetFullPath($PackageCache) } else { Join-Path $cache 'packages' }
    $downloads = Join-Path $cache 'downloads'
    New-Item $work, $downloads -ItemType Directory -Force | Out-Null
    $nuget = Join-Path $cache 'nuget.exe'
    if (-not (Test-Path $nuget)) {
        Invoke-WebRequest 'https://dist.nuget.org/win-x86-commandline/v6.14.0/nuget.exe' -OutFile $nuget
    }
    $signature = Get-AuthenticodeSignature $nuget
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Microsoft') {
        throw 'The NuGet executable is not signed by Microsoft.'
    }
    Assert-UmdfDependencyHash -Path $nuget -Sha256 $configuration.NugetSha256
    $ids = @(Get-UmdfPackageIds -Platforms $Platforms)
    foreach ($id in $ids) {
        $name = "$id.$WdkVersion.nupkg"
        $installed = Join-Path $packages "$id.$WdkVersion\$name"
        $file = if (Test-Path $installed) { $installed } else { Join-Path $downloads $name }
        if ($PackageCache -and -not (Test-Path $installed)) {
            throw "The requested verified package cache is incomplete: $name"
        }
        if (-not (Test-Path $file)) {
            Invoke-WebRequest "https://www.nuget.org/api/v2/package/$id/$WdkVersion" -OutFile $file
        }
        Assert-UmdfDependencyHash -Path $file -Sha256 $configuration.PackageSha256[$id]
        & $nuget verify -All $file | Out-File (Join-Path $work 'dependency-verification.log') -Append
        if ($LASTEXITCODE) { throw "Package signature verification failed: $name" }
        if (-not $PackageCache -and $file -eq $installed) {
            Copy-Item -LiteralPath $file -Destination (Join-Path $downloads $name) -Force
        }
    }
    if (-not $PackageCache) {
        $config = Join-Path $work 'NuGet.Config'
        $source = [Security.SecurityElement]::Escape($downloads)
        "<configuration><packageSources><clear/><add key=`"VerifiedMicrosoft`" value=`"$source`"/></packageSources><packageSourceMapping><clear/><packageSource key=`"VerifiedMicrosoft`"><package pattern=`"Microsoft.Windows.*`"/></packageSource></packageSourceMapping></configuration>" |
            Set-Content $config
        $manifest = Join-Path $work 'packages.config'
        $entries = ($ids | ForEach-Object { "<package id=`"$_`" version=`"$WdkVersion`"/>" }) -join ''
        "<packages>$entries</packages>" | Set-Content $manifest
        & $nuget restore $manifest -PackagesDirectory $packages -ConfigFile $config -NonInteractive -Verbosity quiet | Out-Host
        if ($LASTEXITCODE) { throw 'Official WDK/SDK restore failed.' }
    }
    $signTool = Get-ChildItem (Join-Path $packages "Microsoft.Windows.SDK.CPP.$WdkVersion") -Recurse -Filter signtool.exe |
        Where-Object FullName -Match '\\x64\\' | Select-Object -First 1 -ExpandProperty FullName
    $inf2cat = Get-ChildItem (Join-Path $packages "Microsoft.Windows.WDK.x64.$WdkVersion\c") -Recurse -Filter inf2cat.exe |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not $signTool -or -not $inf2cat) { throw 'Official signing/catalog tools were not found.' }
    [pscustomobject]@{ Packages = $packages; SignTool = $signTool; Inf2Cat = $inf2cat }
}

function Complete-UmdfDriverPackage {
    param(
        [Parameter(Mandatory)][string]$DriverDirectory,
        [Parameter(Mandatory)][ValidateSet('x64', 'ARM64')][string]$Platform,
        [Parameter(Mandatory)][string]$Subject,
        [Parameter(Mandatory)][string]$CertificateName,
        [Parameter(Mandatory)][string[]]$Binaries,
        [Parameter(Mandatory)][string]$CatalogName,
        [Parameter(Mandatory)][string]$SignTool,
        [Parameter(Mandatory)][string]$Inf2Cat
    )
    $certificate = New-SelfSignedCertificate -Subject $Subject -CertStoreLocation Cert:\CurrentUser\My `
        -Type CodeSigningCert -HashAlgorithm SHA256 -KeyExportPolicy NonExportable -NotAfter (Get-Date).AddDays(7)
    try {
        Export-Certificate -Cert $certificate -FilePath (Join-Path $DriverDirectory $CertificateName) | Out-Null
        foreach ($binary in $Binaries) {
            & $SignTool sign /s My /sha1 $certificate.Thumbprint /fd SHA256 (Join-Path $DriverDirectory $binary) | Out-Host
            if ($LASTEXITCODE) { throw "Test binary signing failed: $Platform, $binary" }
        }
        $catalogOs = if ($Platform -eq 'ARM64') { '10_VB_ARM64,10_CO_ARM64' } else { '10_VB_X64,10_CO_X64' }
        & $Inf2Cat "/driver:$DriverDirectory" "/os:$catalogOs" /uselocaltime | Out-Host
        if ($LASTEXITCODE) { throw "Driver catalog generation failed: $Platform" }
        & $SignTool sign /s My /sha1 $certificate.Thumbprint /fd SHA256 (Join-Path $DriverDirectory $CatalogName) | Out-Host
        if ($LASTEXITCODE) { throw "Catalog signing failed: $Platform" }
        $certificate.Thumbprint
    }
    finally { Remove-Item "Cert:\CurrentUser\My\$($certificate.Thumbprint)" -DeleteKey }
}

function Write-UmdfFileHashes {
    param(
        [Parameter(Mandatory)][string]$Directory,
        [Parameter(Mandatory)][string]$ManifestPath,
        [switch]$Relative
    )
    $entries = @(Get-ChildItem $Directory -File -Recurse:$Relative | ForEach-Object {
        [ordered]@{
            Path = if ($Relative) { [IO.Path]::GetRelativePath($Directory, $_.FullName) } else { $_.FullName }
            Hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        }
    })
    ConvertTo-Json -InputObject $entries | Set-Content $ManifestPath
}

function Get-UmdfManagedOutput {
    param(
        [Parameter(Mandatory)][string]$Project,
        [Parameter(Mandatory)][ValidateSet('x64', 'ARM64')][string]$Platform,
        [Parameter(Mandatory)][string]$ExpectedExecutable
    )
    $output = & dotnet msbuild $Project "-p:Platform=$Platform" -p:Configuration=Release -getProperty:TargetDir -nologo
    if ($LASTEXITCODE -or @($output).Count -ne 1 -or -not [IO.Path]::IsPathFullyQualified($output) -or
        -not (Test-Path (Join-Path $output $ExpectedExecutable))) {
        throw "Could not resolve the built managed probe output: $Project"
    }
    $output
}
