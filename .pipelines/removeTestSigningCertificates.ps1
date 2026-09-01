<#
.SYNOPSIS
Remove PowerToys UI-test signing certificates recorded in an exact-thumbprint marker.

.DESCRIPTION
Deletes each recorded certificate from the machine/user trust stores and removes the CurrentUser
private key. The marker is deleted only after every store verifies clean, so an interrupted or failed
cleanup remains retryable by a later job.

.PARAMETER CertificateMarkerPath
Durable marker populated by signSparsePackages.ps1. Each non-empty line must be a SHA-1 certificate
thumbprint.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$CertificateMarkerPath
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $CertificateMarkerPath -ErrorAction Stop)) {
    Write-Host 'No PowerToys UI-test signing certificate marker was found.'
    return
}

$markerEntries = @(Get-Content -LiteralPath $CertificateMarkerPath -ErrorAction Stop |
    ForEach-Object { $_.Trim() } |
    Where-Object { $_ })
$invalidEntries = @($markerEntries | Where-Object { $_ -notmatch '^[0-9A-Fa-f]{40}$' })
if ($invalidEntries.Count -gt 0) {
    throw "PowerToys UI-test signing certificate marker contains invalid thumbprints: $($invalidEntries -join ', ')."
}

$thumbprints = @($markerEntries | ForEach-Object { $_.ToUpperInvariant() } | Select-Object -Unique)
$trustStorePaths = @(
    'Cert:\LocalMachine\Root',
    'Cert:\LocalMachine\TrustedPeople',
    'Cert:\CurrentUser\TrustedPeople')

foreach ($thumbprint in $thumbprints) {
    foreach ($storePath in $trustStorePaths) {
        $certificatePath = Join-Path $storePath $thumbprint
        if (Test-Path -LiteralPath $certificatePath -ErrorAction Stop) {
            Remove-Item -LiteralPath $certificatePath -Force -ErrorAction Stop
        }
    }

    $privateCertificatePath = Join-Path 'Cert:\CurrentUser\My' $thumbprint
    if (Test-Path -LiteralPath $privateCertificatePath -ErrorAction Stop) {
        Remove-Item -LiteralPath $privateCertificatePath -DeleteKey -Force -ErrorAction Stop
    }

    Remove-Item -LiteralPath (Join-Path $env:TEMP "pt-test-signer-$thumbprint.cer") `
        -Force -ErrorAction SilentlyContinue
}

$allStorePaths = @($trustStorePaths) + 'Cert:\CurrentUser\My'
$remaining = foreach ($thumbprint in $thumbprints) {
    foreach ($storePath in $allStorePaths) {
        $certificatePath = Join-Path $storePath $thumbprint
        if (Test-Path -LiteralPath $certificatePath -ErrorAction Stop) {
            $certificatePath
        }
    }
}

if ($remaining) {
    throw "PowerToys UI-test signing certificate cleanup failed: $($remaining -join ', ')."
}

Remove-Item -LiteralPath $CertificateMarkerPath -Force -ErrorAction Stop
Write-Host "Removed PowerToys UI-test signing certificate(s): $($thumbprints -join ', ')."
