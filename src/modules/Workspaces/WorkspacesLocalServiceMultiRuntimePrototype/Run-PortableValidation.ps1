[CmdletBinding()]
param(
    [string]$FirstOwnerSid = 'S-1-5-21-1959867211-618815089-525172305-1122',
    [string]$SecondOwnerSid = 'S-1-5-21-1959867211-618815089-525172305-1123'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$metadataPath = Join-Path $root 'artifacts\release\artifacts.json'
$manifestPath = Join-Path $root 'artifact-manifest.json'
$lifecycle = Join-Path $root 'Lifecycle.ps1'
$teardown = Join-Path $root 'Teardown.ps1'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run-PortableValidation.ps1 requires an elevated PowerShell session.'
}
if (-not [Environment]::Is64BitOperatingSystem) {
    throw 'This prototype bundle requires 64-bit Windows.'
}
foreach ($required in $metadataPath, $manifestPath, $lifecycle, $teardown) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Portable bundle file is missing: $required"
    }
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$canonicalRoot = [IO.Path]::GetFullPath($root).TrimEnd('\') + '\'
foreach ($entry in $manifest.files) {
    $candidate = [IO.Path]::GetFullPath((Join-Path $root $entry.path))
    if (-not $candidate.StartsWith($canonicalRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Portable manifest path escapes the bundle root: $($entry.path)"
    }
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "Portable artifact is missing: $candidate"
    }
    $actualHash = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash
    if ($actualHash -ne $entry.sha256) {
        throw "Portable artifact hash mismatch for $($entry.path): expected $($entry.sha256), actual $actualHash"
    }
}

$metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
$releaseRoot = Join-Path $root 'artifacts\release'
$certificates = @(
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
foreach ($certificate in $certificates) {
    if ([string]::IsNullOrWhiteSpace($certificate.file) -or
        [string]::IsNullOrWhiteSpace($certificate.thumbprint)) {
        throw "Bundled certificate metadata is incomplete for $($certificate.role)."
    }
    $certificate.path = Join-Path $releaseRoot $certificate.file
    if (-not (Test-Path -LiteralPath $certificate.path -PathType Leaf)) {
        throw "Bundled certificate is missing: $($certificate.path)"
    }
    $parsed = [Security.Cryptography.X509Certificates.X509Certificate2]::new($certificate.path)
    if ($parsed.Thumbprint -ne $certificate.thumbprint) {
        throw "Bundled certificate thumbprint mismatch for $($certificate.role): expected $($certificate.thumbprint), actual $($parsed.Thumbprint)"
    }
}

$preexistingTrust = @{}
foreach ($certificate in $certificates) {
    $trustedPath = "Cert:\LocalMachine\TrustedPeople\$($certificate.thumbprint)"
    $preexistingTrust[$certificate.thumbprint] = Test-Path -LiteralPath $trustedPath
}
$validationFailure = $null
$cleanupFailure = $null
try {
    foreach ($certificate in $certificates) {
        $trustedPath = "Cert:\LocalMachine\TrustedPeople\$($certificate.thumbprint)"
        if (-not $preexistingTrust[$certificate.thumbprint]) {
            Import-Certificate -FilePath $certificate.path -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
        }
        if (-not (Test-Path -LiteralPath $trustedPath)) {
            throw "Could not establish machine trust for $($certificate.role) certificate."
        }
    }
    & $lifecycle -Verb validate -Configuration Release -FirstOwnerSid $FirstOwnerSid -SecondOwnerSid $SecondOwnerSid -PreserveTrustedCertificates
}
catch {
    $validationFailure = $_
}
finally {
    try {
        & $teardown -Configuration Release -FirstOwnerSid $FirstOwnerSid -SecondOwnerSid $SecondOwnerSid -PreserveTrustedCertificates
    }
    catch {
        $cleanupFailure = $_
    }
    foreach ($certificate in $certificates) {
        $trustedPath = "Cert:\LocalMachine\TrustedPeople\$($certificate.thumbprint)"
        if (-not $preexistingTrust[$certificate.thumbprint] -and (Test-Path -LiteralPath $trustedPath)) {
            Remove-Item -LiteralPath $trustedPath -Force
        }
        $actual = Test-Path -LiteralPath $trustedPath
        if ($actual -ne $preexistingTrust[$certificate.thumbprint] -and -not $cleanupFailure) {
            $cleanupFailure = [InvalidOperationException]::new(
                "Portable certificate restoration failed for $($certificate.role) $($certificate.thumbprint).")
        }
    }
}

if ($validationFailure) {
    throw $validationFailure
}
if ($cleanupFailure) {
    throw $cleanupFailure
}

$resultPath = Join-Path $root 'artifacts\validation-result.json'
$result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
if ($result.verdict -ne 'PASS') {
    throw "Portable validation did not record PASS: $resultPath"
}
Write-Host "PORTABLE VALIDATION PASS: $resultPath"
