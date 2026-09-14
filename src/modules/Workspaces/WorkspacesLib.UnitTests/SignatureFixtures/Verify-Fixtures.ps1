#Requires -Version 7.4
# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$Verifier
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (!$IsWindows) {
    throw 'Signature verification requires Windows and PowerShell 7.4+.'
}
. (Join-Path $PSScriptRoot 'FixtureCommon.ps1')
$OutputDirectory = Resolve-FixtureOutputDirectory $OutputDirectory
if (!$Verifier) {
    $Verifier = Join-Path $RepoRoot 'x64\Debug\PowerToys.WorkspacesLauncher.exe'
}
if (!(Test-Path -LiteralPath $Verifier -PathType Leaf)) {
    throw "Build the Debug Workspaces launcher first, or supply -Verifier with a Debug diagnostic build: $Verifier"
}
$Verifier = (Resolve-Path -LiteralPath $Verifier).ProviderPath
$manifest = Get-Content -LiteralPath (Join-Path $OutputDirectory 'fixtures.json') -Raw | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1 -or $manifest.files.Count -ne $FixtureExpectations.Count -or
    (Compare-Object @($manifest.files.file | Sort-Object) @($FixtureExpectations.Keys | Sort-Object))) {
    throw 'The fixture manifest must contain exactly the six known relative fixture paths.'
}

$results = foreach ($name in $FixtureExpectations.Keys) {
    $certificate = $null
    $record = [ordered]@{
        file = $name
        verdict = 'FAIL'
        status = $null
        reason = $null
        windowsStatus = $null
        sha256 = $null
        certificateNotAfterUtc = $null
        certificateUsages = @()
        error = $null
    }
    try {
        $path = Join-Path $OutputDirectory $name
        $entry = $manifest.files | Where-Object file -EQ $name
        $record.sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        if ($record.sha256 -ne $entry.sha256) {
            throw 'Fixture changed after generation (SHA-256 mismatch).'
        }
        $json = & $Verifier --verify-signature $path | Out-String
        if ($LASTEXITCODE -ne 0) {
            throw "The Debug verifier exited with code $LASTEXITCODE."
        }
        $actual = $json | ConvertFrom-Json
        if (!$actual.status -or !$actual.reason -or $actual.windowsStatus -notmatch '^0x[0-9A-Fa-f]{8}$') {
            throw 'The Debug verifier did not return status, reason, and windowsStatus JSON fields.'
        }
        $record.status = $actual.status
        $record.reason = $actual.reason
        $record.windowsStatus = $actual.windowsStatus
        if ($actual.status -ne $FixtureExpectations[$name]) {
            throw "Expected $($FixtureExpectations[$name]); observed $($actual.status). See the README's policy limitations."
        }
        if ($name -in @('SelfSigned.exe', 'TamperedSignature.exe', 'ExpiredCertificate.exe', 'WrongCertificateUsage.exe')) {
            $signature = Get-AuthenticodeSignature -LiteralPath $path
            $certificate = $signature.SignerCertificate
            if (!$certificate -or $signature.TimeStamperCertificate) {
                throw 'Expected an embedded signing certificate without a timestamp.'
            }
            $extensions = @($certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.37' })
            $usages = @($extensions | ForEach-Object { $_.EnhancedKeyUsages | ForEach-Object Value })
            $record.certificateNotAfterUtc = $certificate.NotAfter.ToUniversalTime()
            $record.certificateUsages = $usages
            $expectedUsage = if ($name -eq 'WrongCertificateUsage.exe') { '1.3.6.1.5.5.7.3.1' } else { '1.3.6.1.5.5.7.3.3' }
            if ($usages.Count -ne 1 -or $usages[0] -ne $expectedUsage) {
                throw 'The fixture does not contain its expected code-signing or TLS-only EKU.'
            }
            $now = [DateTime]::UtcNow
            if ($name -eq 'ExpiredCertificate.exe') {
                if ($certificate.NotAfter.ToUniversalTime() -ge $now) {
                    throw 'The expired fixture does not actually contain an expired certificate.'
                }
            }
            elseif ($certificate.NotBefore.ToUniversalTime() -gt $now -or $certificate.NotAfter.ToUniversalTime() -le $now) {
                throw 'The non-expired fixture certificate is outside its validity period; regenerate the fixtures.'
            }
        }
        if ($name -eq 'TamperedSignature.exe') {
            $original = [IO.File]::ReadAllBytes((Join-Path $OutputDirectory 'SelfSigned.exe'))
            $changed = [IO.File]::ReadAllBytes($path)
            $marker = 'PowerToysSignatureFixtureUnusedPayload-0001'
            $offset = [Text.Encoding]::ASCII.GetString($original).IndexOf($marker, [StringComparison]::Ordinal)
            if ($offset -lt 0 -or $original.Length -ne $changed.Length) {
                throw 'The signed template and tampered fixture have incompatible contents.'
            }
            $original[$offset + $marker.Length - 1] = [byte][char]'2'
            if (![Linq.Enumerable]::SequenceEqual[byte]($original, $changed)) {
                throw 'The tampered fixture differs by more than its single unused resource byte.'
            }
        }
        $record.verdict = 'PASS'
    }
    catch {
        $record.error = $_.Exception.Message
    }
    finally {
        if ($certificate) {
            $certificate.Dispose()
        }
    }
    [pscustomobject]$record
}

$report = Join-Path $OutputDirectory 'verification-results.json'
[ordered]@{
    verifiedAtUtc = [DateTimeOffset]::UtcNow
    verifier = $Verifier
    configuration = $manifest.configuration
    files = @($results)
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $report
$results | Format-Table file, verdict, status, reason, windowsStatus -AutoSize
if (@($results | Where-Object verdict -NE 'PASS').Count) {
    throw "Fixture verification failed. See $report for individual failures; no trust or policy changes were made."
}
Write-Host "PASS: all six fixtures. Evidence: $report"
