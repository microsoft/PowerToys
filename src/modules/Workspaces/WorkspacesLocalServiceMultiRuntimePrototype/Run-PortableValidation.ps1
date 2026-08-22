[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$ExpectedSourceCommit,
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-fA-F]{64}$')]
    [string]$ExpectedPortableManifestSha256
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Core' -or $PSVersionTable.PSVersion -lt [version]'7.0') {
    throw 'Run-PortableValidation.ps1 requires PowerShell 7 or later (pwsh.exe).'
}

$root = [IO.Path]::GetFullPath($PSScriptRoot)
$manifestPath = Join-Path $root 'portable-manifest.json'
$metadataPath = Join-Path $root 'artifacts\release\artifacts.json'
$ownershipPath = Join-Path $root 'artifacts\release\certificate-ownership.json'
$msiPath = Join-Path $root 'artifacts\msi\PtPuvrControlPlane.msi'
$lifecyclePath = Join-Path $root 'Lifecycle.ps1'
$teardownPath = Join-Path $root 'Teardown.ps1'

function Assert-True($Value, [string]$Label) {
    if (-not $Value) {
        throw "Assertion failed: $Label"
    }
}

function Test-Elevated {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Get-ContainedFilePath([string]$RelativePath) {
    Assert-True (
        -not [string]::IsNullOrWhiteSpace($RelativePath) -and
        -not [IO.Path]::IsPathRooted($RelativePath) -and
        -not $RelativePath.Contains(':') -and
        -not $RelativePath.Contains('..')
    ) "portable manifest path is relative and traversal-free: $RelativePath"

    $nativeRelativePath = $RelativePath.Replace('/', '\')
    $candidate = [IO.Path]::GetFullPath((Join-Path $root $nativeRelativePath))
    Assert-True (
        $candidate.StartsWith($root.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)
    ) "portable manifest path stays within extraction root: $RelativePath"
    return $candidate
}

function Get-CertificateEntries([string]$Store, [string]$Thumbprint) {
    return @(
        Get-ChildItem -Path $Store | Where-Object { $_.Thumbprint -eq $Thumbprint }
    )
}

function Set-TargetCertificateOwnershipBaseline {
    $roleProperties = @(
        [ordered]@{ role = 'code'; property = 'codeSigner' },
        [ordered]@{ role = 'metadata'; property = 'metadataSigner' },
        [ordered]@{ role = 'foreign'; property = 'foreignSigner' }
    )

    foreach ($definition in $roleProperties) {
        $artifactCertificate = $metadata.($definition.property)
        Assert-True ($null -ne $artifactCertificate) "artifact certificate record $($definition.role)"
        $records = @($ownership.certificates | Where-Object { $_.role -eq $definition.role })
        Assert-True ($records.Count -eq 1) "ownership certificate record $($definition.role)"
        Assert-True (
            $records[0].thumbprint -eq $artifactCertificate.thumbprint
        ) "ownership thumbprint matches $($definition.role)"

        foreach ($store in $records[0].stores) {
            Assert-True (
                $store.path -in @(
                    'Cert:\CurrentUser\My',
                    'Cert:\CurrentUser\TrustedPeople',
                    'Cert:\LocalMachine\TrustedPeople'
                )
            ) "ownership store is an approved exact certificate store"
            $store.preRunPresent = @(Get-CertificateEntries $store.path $artifactCertificate.thumbprint).Count -ge 1
            $store.introducedByRun = $false
        }
    }

    $ownership | ConvertTo-Json -Depth 8 |
        Set-Content -LiteralPath $ownershipPath -Encoding utf8NoBOM
}

if (-not (Test-Elevated)) {
    throw 'Portable validation requires an elevated administrator token.'
}
Assert-True (Test-Path -LiteralPath $manifestPath -PathType Leaf) 'portable manifest exists'
Assert-True (
    (Get-Sha256 $manifestPath) -eq $ExpectedPortableManifestSha256
) 'portable manifest matches the out-of-band SHA-256 anchor'

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
Assert-True ($manifest.format -eq 3) 'portable manifest format 3'
Assert-True (
    $manifest.sourceCommit -match '^[0-9a-fA-F]{40}$'
) 'portable manifest source commit'
Assert-True (
    [string]$manifest.sourceCommit -eq $ExpectedSourceCommit
) 'portable manifest matches the out-of-band source commit'
Assert-True (
    [string]$manifest.artifactMetadataSha256 -match '^[0-9a-fA-F]{64}$'
) 'portable manifest artifact metadata hash'
Assert-True (@($manifest.files).Count -gt 0) 'portable manifest files'

$seenPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($entry in $manifest.files) {
    Assert-True ($seenPaths.Add([string]$entry.path)) "portable manifest has no duplicate path: $($entry.path)"
    $filePath = Get-ContainedFilePath ([string]$entry.path)
    Assert-True (Test-Path -LiteralPath $filePath -PathType Leaf) "portable file exists: $($entry.path)"
    $item = Get-Item -LiteralPath $filePath -Force
    Assert-True (
        -not ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)
    ) "portable file is not a reparse point: $($entry.path)"
    Assert-True (
        [int64]$item.Length -eq [int64]$entry.length
    ) "portable file length: $($entry.path)"
    Assert-True (
        (Get-Sha256 $filePath) -eq [string]$entry.sha256
    ) "portable file hash: $($entry.path)"
}

Assert-True (Test-Path -LiteralPath $metadataPath -PathType Leaf) 'portable artifact metadata exists'
Assert-True (Test-Path -LiteralPath $ownershipPath -PathType Leaf) 'portable certificate ownership exists'
Assert-True (Test-Path -LiteralPath $msiPath -PathType Leaf) 'portable companion MSI exists'
Assert-True (Test-Path -LiteralPath $lifecyclePath -PathType Leaf) 'portable lifecycle exists'
Assert-True (Test-Path -LiteralPath $teardownPath -PathType Leaf) 'portable teardown exists'

$metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
$ownership = Get-Content -LiteralPath $ownershipPath -Raw | ConvertFrom-Json
Assert-True ($metadata.format -eq 2) 'portable artifact metadata format 2'
Assert-True ($ownership.format -eq 2) 'portable certificate ownership format 2'
Assert-True ($metadata.sourceTreeClean -eq $true) 'portable artifacts were built from a clean source tree'
Assert-True (
    [string]$metadata.sourceCommit -eq $ExpectedSourceCommit
) 'portable artifact metadata source commit'
Assert-True (
    (Get-Sha256 $metadataPath) -eq [string]$manifest.artifactMetadataSha256
) 'portable artifact metadata matches manifest provenance'
Assert-True (
    (Get-Sha256 $msiPath) -eq $metadata.msi.sha256
) 'portable companion MSI hash matches metadata'

$provenancePath = Get-ContainedFilePath ([string]$manifest.buildProvenanceFile)
$provenance = Get-Content -LiteralPath $provenancePath -Raw | ConvertFrom-Json
Assert-True ($provenance.format -eq 1) 'portable build provenance format 1'
Assert-True ($provenance.sourceTreeClean -eq $true) 'portable build provenance clean source'
Assert-True (
    [string]$provenance.sourceCommit -eq $ExpectedSourceCommit
) 'portable build provenance source commit'
Assert-True (
    [string]$provenance.artifactMetadataSha256 -eq (Get-Sha256 $metadataPath)
) 'portable build provenance artifact metadata hash'

$validationFailure = $null
try {
    # The exported snapshot belongs to another machine. Record this machine's exact
    # certificate presence before Lifecycle.ps1 may temporarily add test trust.
    Set-TargetCertificateOwnershipBaseline
    & $lifecyclePath -Verb validate -Configuration Release
    if ($LASTEXITCODE -notin @(0, $null)) {
        throw "Lifecycle exited with code $LASTEXITCODE."
    }

    $resultPath = Join-Path $root 'artifacts\validation-result.json'
    Assert-True (Test-Path -LiteralPath $resultPath -PathType Leaf) 'portable validation result exists'
    $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    Assert-True ($result.verdict -eq 'PASS') 'portable validation verdict'
}
catch {
    $validationFailure = $_
}

$teardownFailure = $null
try {
    & $teardownPath
    if ($LASTEXITCODE -notin @(0, $null)) {
        throw "Teardown exited with code $LASTEXITCODE."
    }
}
catch {
    $teardownFailure = $_
}

if ($null -ne $validationFailure) {
    if ($null -ne $teardownFailure) {
        throw "Portable lifecycle failed: $($validationFailure.Exception.Message)`nPortable teardown also failed: $($teardownFailure.Exception.Message)"
    }
    throw $validationFailure
}
if ($null -ne $teardownFailure) {
    throw $teardownFailure
}

Write-Output "PORTABLE VALIDATION PASS: $(Join-Path $root 'artifacts\validation-result.json')"
