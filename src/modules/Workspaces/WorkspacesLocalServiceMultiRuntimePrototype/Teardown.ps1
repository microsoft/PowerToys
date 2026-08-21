[CmdletBinding()]
param(
    [string]$FirstOwnerSid = 'S-1-5-21-1959867211-618815089-525172305-1122',
    [string]$SecondOwnerSid = 'S-1-5-21-1959867211-618815089-525172305-1123',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$PreserveTrustedCertificates
)

$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Teardown requires an elevated administrator token.'
}

$root = $PSScriptRoot
$releaseRoot = Join-Path $root 'artifacts\release'
$metadataPath = Join-Path $releaseRoot 'artifacts.json'
$ownershipPath = Join-Path $releaseRoot 'certificate-ownership.json'
$certificateStores = @(
    'Cert:\CurrentUser\My',
    'Cert:\CurrentUser\TrustedPeople',
    'Cert:\LocalMachine\My',
    'Cert:\LocalMachine\TrustedPeople'
)

function Assert-True($Value, [string]$Label) {
    if (-not $Value) {
        throw "Assertion failed: $Label"
    }
}

function Get-RuntimeServiceName([string]$OwnerSid) {
    $bytes = [Text.Encoding]::Unicode.GetBytes($OwnerSid)
    $digest = [Security.Cryptography.SHA256]::HashData($bytes)
    return 'PtPuvrRuntime_' + [Convert]::ToHexString($digest).ToLowerInvariant().Substring(0, 16)
}

function Stop-AndDeleteService([string]$Name) {
    $service = Get-Service -Name $Name -ErrorAction SilentlyContinue
    if (-not $service) {
        return
    }
    if ($service.Status -ne 'Stopped') {
        Stop-Service -Name $Name -Force
        $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
    }
    sc.exe delete $Name | Out-Null
}

function Get-TestCertificates {
    if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf)) {
        throw "Release metadata is missing: $metadataPath"
    }
    $metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
    $records = @(
        [ordered]@{
            role = 'primary'
            file = $metadata.certificateFile
            thumbprint = $metadata.certificateThumbprint
        },
        [ordered]@{
            role = 'foreign'
            file = $metadata.foreignSignerCertificateFile
            thumbprint = $metadata.foreignSignerCertificateThumbprint
        }
    )
    foreach ($record in $records) {
        if ([string]::IsNullOrWhiteSpace($record.file) -or
            [string]::IsNullOrWhiteSpace($record.thumbprint)) {
            throw "Certificate metadata is incomplete for $($record.role)."
        }
        $certificatePath = Join-Path $releaseRoot $record.file
        if (-not (Test-Path -LiteralPath $certificatePath -PathType Leaf)) {
            throw "Certificate file is missing: $certificatePath"
        }
        $certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($certificatePath)
        if ($certificate.Thumbprint -ne $record.thumbprint) {
            throw "Certificate thumbprint mismatch for $($record.role): expected $($record.thumbprint), actual $($certificate.Thumbprint)"
        }
    }
    return $records
}

function Get-CertificateOwnership([object[]]$Certificates) {
    if (-not (Test-Path -LiteralPath $ownershipPath -PathType Leaf)) {
        throw "Certificate ownership state is missing: $ownershipPath. Refusing to remove trusted certificates."
    }
    $ownership = Get-Content -LiteralPath $ownershipPath -Raw | ConvertFrom-Json
    if ($ownership.format -ne 1) {
        throw 'Certificate ownership state format is unsupported.'
    }
    foreach ($certificate in $Certificates) {
        $record = @($ownership.certificates | Where-Object { $_.thumbprint -eq $certificate.thumbprint })
        if ($record.Count -ne 1) {
            throw "Certificate ownership state does not uniquely identify $($certificate.role)."
        }
        $stores = @($record[0].stores)
        if ($stores.Count -ne $certificateStores.Count) {
            throw "Certificate ownership store count is invalid for $($certificate.role)."
        }
        foreach ($store in $certificateStores) {
            $entry = @($stores | Where-Object { $_.path -eq $store })
            if ($entry.Count -ne 1 -or
                $entry[0].preRunPresent -isnot [bool] -or
                $entry[0].introducedByRun -isnot [bool]) {
                throw "Certificate ownership state is invalid for $($certificate.role) at $store."
            }
        }
    }
    return $ownership
}

function Restore-TestCertificates([object[]]$Certificates, [pscustomobject]$Ownership) {
    foreach ($certificate in $Certificates) {
        $record = @($Ownership.certificates | Where-Object { $_.thumbprint -eq $certificate.thumbprint })[0]
        foreach ($store in $record.stores) {
            if ($store.introducedByRun) {
                $path = "$($store.path)\$($certificate.thumbprint)"
                if (Test-Path -LiteralPath $path) {
                    Remove-Item -LiteralPath $path -Force
                }
            }
        }
    }
}

function Assert-CertificatesRestored([object[]]$Certificates, [pscustomobject]$Ownership) {
    foreach ($certificate in $Certificates) {
        $record = @($Ownership.certificates | Where-Object { $_.thumbprint -eq $certificate.thumbprint })[0]
        foreach ($store in $record.stores) {
            $actual = Test-Path -LiteralPath "$($store.path)\$($certificate.thumbprint)"
            Assert-True ($actual -eq $store.preRunPresent) "certificate restoration $($certificate.role) $($certificate.thumbprint) at $($store.path)"
        }
    }
}

$certificates = Get-TestCertificates
$ownership = if ($PreserveTrustedCertificates) {
    $null
}
else {
    Get-CertificateOwnership $certificates
}

$installRoot = Join-Path $env:ProgramFiles 'PowerToys\WorkspacesProtectedRuntimeUpdaterPrototype'
$storeRoot = Join-Path $env:ProgramData 'Microsoft\PowerToys\WorkspacesProtectedRuntimeUpdaterPrototype'

foreach ($owner in $FirstOwnerSid, $SecondOwnerSid) {
    Stop-AndDeleteService (Get-RuntimeServiceName $owner)
}
foreach ($service in @(Get-Service -Name 'PtPuvrRuntime_*' -ErrorAction SilentlyContinue)) {
    Stop-AndDeleteService $service.Name
}
Stop-AndDeleteService 'PtPuvrUpdater'

Remove-Item -LiteralPath $installRoot -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $storeRoot -Recurse -Force -ErrorAction SilentlyContinue

if (-not $PreserveTrustedCertificates) {
    Restore-TestCertificates $certificates $ownership
}

if (@(Get-Service -Name 'PtPuvr*' -ErrorAction SilentlyContinue).Count -ne 0 -or
    (Test-Path -LiteralPath $installRoot) -or
    (Test-Path -LiteralPath $storeRoot)) {
    throw 'Teardown verification failed; prototype state remains.'
}
if (-not $PreserveTrustedCertificates) {
    Assert-CertificatesRestored $certificates $ownership
    $certificateSummary = @(
        foreach ($certificate in $certificates) {
            [ordered]@{
                role = $certificate.role
                thumbprint = $certificate.thumbprint
                restoredToPreRunState = $true
            }
        }
    ) | ConvertTo-Json -Compress
    Write-Host "Teardown PASS: no prototype services, roots, or stores remain; exact certificate state restored: $certificateSummary"
}
else {
    Write-Host 'Teardown PASS: no prototype services, roots, or stores remain; caller-owned certificates were preserved.'
}
