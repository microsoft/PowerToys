#Requires -Version 7.4
# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [switch]$RegisterForWorkspaces
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (!$IsWindows) {
    throw 'Signature fixtures require Windows, PowerShell 7.4+, and the Visual Studio C++/Windows SDK tools.'
}
. (Join-Path $PSScriptRoot 'FixtureCommon.ps1')
. (Join-Path $PSScriptRoot 'Set-FixtureCmsSignature.ps1')
$OutputDirectory = Resolve-FixtureOutputDirectory $OutputDirectory
if (Test-Path -LiteralPath $OutputDirectory) {
    throw 'OutputDirectory already exists. Choose a new dedicated directory; existing fixtures are never overwritten.'
}

$buildDirectory = Join-Path $OutputDirectory 'build'
$scratchDirectory = Join-Path $buildDirectory 'scratch'
$sources = @('SignatureFixture.cpp', 'SignatureFixture.rc', 'SignatureFixture.vcxproj')
$base = Join-Path $buildDirectory 'bin\SignatureFixture.exe'
$savedTemp = $env:TEMP
$savedTmp = $env:TMP

function New-SignedFixture {
    param(
        [string]$Name,
        [string]$Usage = '1.3.6.1.5.5.7.3.3',
        [switch]$Expired
    )

    $rsa = [Security.Cryptography.RSA]::Create(2048)
    $certificate = $null
    $destination = Join-Path $OutputDirectory $Name
    try {
        $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new(
            "CN=PowerToys Harmless Fixture $([IO.Path]::GetFileNameWithoutExtension($Name))",
            $rsa, [Security.Cryptography.HashAlgorithmName]::SHA256,
            [Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($false, $false, 0, $true))
        $request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new(
            [Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature, $true))
        $usages = [Security.Cryptography.OidCollection]::new()
        [void]$usages.Add([Security.Cryptography.Oid]::new($Usage))
        $request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($usages, $true))
        $now = [DateTimeOffset]::UtcNow
        $start = if ($Expired) { $now.AddDays(-14) } else { $now.AddDays(-1) }
        $end = if ($Expired) { $now.AddDays(-7) } else { $now.AddYears(5) }
        $certificate = $request.CreateSelfSigned($start, $end)
        if ($Expired -or $Usage -ne '1.3.6.1.5.5.7.3.3') {
            Copy-Item -LiteralPath (Join-Path $OutputDirectory 'SelfSigned.exe') -Destination $destination
            Set-FixtureCmsSignature -Path $destination -Certificate $certificate
        }
        else {
            Copy-Item -LiteralPath $base -Destination $destination
            [Workspaces.SignatureFixtures.FixtureSigner]::Sign($destination, $certificate)
        }
    }

    catch {
        if (Test-Path -LiteralPath $destination) {
            Remove-Item -LiteralPath $destination
        }
        throw
    }
    finally {
        if ($certificate) {
            $certificate.Dispose()
        }
        $rsa.Dispose()
    }
}

try {
    New-Item -ItemType Directory -Path $scratchDirectory | Out-Null
    $env:TEMP = $scratchDirectory
    $env:TMP = $scratchDirectory
    foreach ($source in $sources) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $source) -Destination $buildDirectory
    }
    . (Join-Path $RepoRoot 'tools\build\build-common.ps1')
    if (!(Ensure-VsDevEnvironment)) {
        throw 'The repository build helper could not initialize the Visual Studio C++ tools.'
    }
    Push-Location $buildDirectory
    try {
        RunMSBuild -Solution (Join-Path $buildDirectory 'SignatureFixture.vcxproj') `
            -ExtraArgs '/p:FixtureStagedBuild=true /nr:false' -Platform x64 -Configuration $Configuration
    }
    finally {
        Pop-Location
    }
    if (!(Test-Path -LiteralPath $base)) {
        throw 'MSBuild did not produce the standalone fixture executable.'
    }
    Add-Type -Path (Join-Path $PSScriptRoot 'FixtureSigner.cs')
    Copy-Item -LiteralPath $base -Destination (Join-Path $OutputDirectory 'Unsigned.exe')
    New-SignedFixture -Name 'SelfSigned.exe'
    New-SignedFixture -Name 'ExpiredCertificate.exe' -Expired
    New-SignedFixture -Name 'WrongCertificateUsage.exe' -Usage '1.3.6.1.5.5.7.3.1'

    $tampered = Join-Path $OutputDirectory 'TamperedSignature.exe'
    Copy-Item -LiteralPath (Join-Path $OutputDirectory 'SelfSigned.exe') -Destination $tampered
    $bytes = [IO.File]::ReadAllBytes($tampered)
    $marker = 'PowerToysSignatureFixtureUnusedPayload-0001'
    $content = [Text.Encoding]::ASCII.GetString($bytes)
    $offset = $content.IndexOf($marker, [StringComparison]::Ordinal)
    if ($offset -lt 0 -or $offset -ne $content.LastIndexOf($marker, [StringComparison]::Ordinal)) {
        throw 'The unused resource marker was not found exactly once; refusing to modify executable code.'
    }
    $bytes[$offset + $marker.Length - 1] = [byte][char]'2'
    [IO.File]::WriteAllBytes($tampered, $bytes)

    New-Item -ItemType Directory -Path (Join-Path $OutputDirectory 'diagnostics') | Out-Null
    Set-Content -LiteralPath (Join-Path $OutputDirectory 'diagnostics\NotAnExecutable.exe') `
        -Value 'Not a PE executable. Signature-verification diagnostic only; never launch this file.' -Encoding ascii
    $records = foreach ($name in $FixtureExpectations.Keys) {
        [ordered]@{
            file = $name
            sha256 = (Get-FileHash -LiteralPath (Join-Path $OutputDirectory $name) -Algorithm SHA256).Hash
        }
    }
    [ordered]@{
        schemaVersion = 1
        generatedAtUtc = [DateTimeOffset]::UtcNow
        configuration = $Configuration
        certificateStoresModified = $false
        files = @($records)
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'fixtures.json')
    Write-Host "Generated six fixtures in $OutputDirectory. Run Verify-Fixtures.ps1 separately with the Debug launcher."
}
finally {
    $env:TEMP = $savedTemp
    $env:TMP = $savedTmp
    # Keep build logs and generated fixtures as evidence; discard only this run's build intermediates.
    foreach ($name in @('bin', 'obj', 'scratch') + $sources) {
        $path = Join-Path $buildDirectory $name
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }
}

if ($RegisterForWorkspaces) {
    & (Join-Path $PSScriptRoot 'Register-Fixtures.ps1') -OutputDirectory $OutputDirectory
}
