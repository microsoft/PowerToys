[CmdletBinding()]
param(
    [string]$FirstOwnerSid = 'S-1-5-21-1959867211-618815089-525172305-1122',
    [string]$SecondOwnerSid = 'S-1-5-21-1959867211-618815089-525172305-1123'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$metadataPath = Join-Path $root 'artifacts\packages\packages.json'
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
    if (-not $candidate.StartsWith(
            $canonicalRoot,
            [StringComparison]::OrdinalIgnoreCase)) {
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
$certificatePath = Join-Path `
    (Join-Path $root 'artifacts\packages') `
    ([IO.Path]::GetFileName($metadata.certificatePath))
$certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new(
    $certificatePath)
if ($certificate.Thumbprint -ne $metadata.certificateThumbprint) {
    throw "Bundled certificate thumbprint mismatch: expected $($metadata.certificateThumbprint), actual $($certificate.Thumbprint)"
}

$trustedPath = "Cert:\LocalMachine\TrustedPeople\$($certificate.Thumbprint)"
$certificateWasAlreadyTrusted = Test-Path -LiteralPath $trustedPath
$validationFailure = $null
$cleanupFailure = $null
try {
    if (-not $certificateWasAlreadyTrusted) {
        Import-Certificate `
            -FilePath $certificatePath `
            -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
    }
    & $lifecycle `
        -Verb validate `
        -Configuration Release `
        -FirstOwnerSid $FirstOwnerSid `
        -SecondOwnerSid $SecondOwnerSid
}
catch {
    $validationFailure = $_
}
finally {
    try {
        & $teardown `
            -Configuration Release `
            -FirstOwnerSid $FirstOwnerSid `
            -SecondOwnerSid $SecondOwnerSid `
            -PreserveTrustedCertificates
    }
    catch {
        $cleanupFailure = $_
    }
    if (-not $certificateWasAlreadyTrusted -and
        (Test-Path -LiteralPath $trustedPath)) {
        Remove-Item -LiteralPath $trustedPath -Force
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
