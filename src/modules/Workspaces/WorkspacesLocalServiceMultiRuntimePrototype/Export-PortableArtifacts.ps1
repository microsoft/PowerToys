[CmdletBinding()]
param(
    [string]$DestinationDirectory = (Join-Path $PSScriptRoot 'artifacts\portable')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Core' -or $PSVersionTable.PSVersion -lt [version]'7.0') {
    throw 'Export-PortableArtifacts.ps1 requires PowerShell 7 or later (pwsh.exe).'
}

$root = [IO.Path]::GetFullPath($PSScriptRoot)
$releaseRoot = Join-Path $root 'artifacts\release'
$releaseSetsRoot = Join-Path $root 'artifacts\release-sets'
$msiPath = Join-Path $root 'artifacts\msi\PtPuvrControlPlane.msi'
$metadataPath = Join-Path $releaseRoot 'artifacts.json'
$destination = [IO.Path]::GetFullPath($DestinationDirectory)

function Assert-True($Value, [string]$Label) {
    if (-not $Value) {
        throw "Assertion failed: $Label"
    }
}

function Test-PathWithin([string]$Candidate, [string]$Parent) {
    $candidateFull = [IO.Path]::GetFullPath($Candidate)
    $parentFull = [IO.Path]::GetFullPath($Parent).TrimEnd('\') + '\'
    return $candidateFull.StartsWith($parentFull, [StringComparison]::OrdinalIgnoreCase)
}

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Add-PortableFile([string]$SourcePath) {
    $sourceItem = Get-Item -LiteralPath $SourcePath -Force
    Assert-True (-not ($sourceItem.Attributes -band [IO.FileAttributes]::ReparsePoint)) "portable source is not a reparse point: $SourcePath"
    $sourceFull = [IO.Path]::GetFullPath($sourceItem.FullName)
    Assert-True (Test-PathWithin $sourceFull $root) "portable source remains under prototype root: $sourceFull"

    $relativePath = [IO.Path]::GetRelativePath($root, $sourceFull)
    Assert-True (
        -not [IO.Path]::IsPathRooted($relativePath) -and
        -not $relativePath.StartsWith('..', [StringComparison]::Ordinal)
    ) "portable relative path is contained: $relativePath"

    $destinationPath = Join-Path $stagingRoot $relativePath
    $destinationDirectory = Split-Path -Parent $destinationPath
    New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
    Copy-Item -LiteralPath $sourceFull -Destination $destinationPath -Force
    $entries.Add([pscustomobject]@{
            path = $relativePath.Replace('\', '/')
            sha256 = Get-Sha256 $destinationPath
            length = (Get-Item -LiteralPath $destinationPath).Length
        })
}

function Add-PortableTree([string]$SourceDirectory) {
    Assert-True (Test-Path -LiteralPath $SourceDirectory -PathType Container) "portable source directory exists: $SourceDirectory"
    Get-ChildItem -LiteralPath $SourceDirectory -File -Recurse | Sort-Object FullName | ForEach-Object {
        Add-PortableFile $_.FullName
    }
}

$repositoryRoot = (& git -C $root rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repositoryRoot)) {
    throw 'Portable export requires this prototype to be inside a Git worktree.'
}
$changes = @(& git -C $root status --porcelain)
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to inspect Git worktree status for portable export.'
}
if ($changes.Count -ne 0) {
    throw 'Portable export intentionally requires a clean committed worktree so the bundle has a reproducible source revision.'
}

$commit = (& git -C $root rev-parse --verify HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-fA-F]{40}$') {
    throw 'Portable export requires a committed HEAD revision.'
}

& (Join-Path $root 'Package.ps1') -Configuration Release
if ($LASTEXITCODE -notin @(0, $null)) {
    throw "Clean-HEAD package rebuild failed with exit code $LASTEXITCODE."
}

$postBuildChanges = @(& git -C $repositoryRoot status --porcelain)
if ($LASTEXITCODE -ne 0 -or $postBuildChanges.Count -ne 0) {
    throw 'The clean-HEAD package rebuild changed tracked or untracked source files.'
}
Assert-True (Test-Path -LiteralPath $metadataPath -PathType Leaf) 'rebuilt release metadata exists'
Assert-True (Test-Path -LiteralPath $releaseSetsRoot -PathType Container) 'rebuilt release sets exist'
Assert-True (Test-Path -LiteralPath $msiPath -PathType Leaf) 'rebuilt companion MSI exists'

$metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
Assert-True ($metadata.format -eq 2) 'release metadata format 2'
Assert-True ($metadata.sourceTreeClean -eq $true) 'package metadata records a clean source tree'
Assert-True (
    [string]$metadata.sourceCommit -eq $commit
) 'package metadata source commit matches export HEAD'
Assert-True ((Get-Sha256 $msiPath) -eq $metadata.msi.sha256) 'companion MSI hash matches artifact metadata'

Assert-True (
    -not $destination.Equals($root, [StringComparison]::OrdinalIgnoreCase)
) 'portable destination is not the prototype root'
New-Item -ItemType Directory -Path $destination -Force | Out-Null

$bundleName = "PtPuvr-ControlPlane-$($commit.Substring(0, 12))"
$stagingRoot = Join-Path $destination $bundleName
$archivePath = Join-Path $destination "$bundleName.zip"
$anchorPath = Join-Path $destination "$bundleName.anchors.txt"
if (Test-Path -LiteralPath $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}
if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}
if (Test-Path -LiteralPath $anchorPath) {
    Remove-Item -LiteralPath $anchorPath -Force
}
New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null

$entries = [System.Collections.Generic.List[object]]::new()
try {
    foreach ($relativeFile in @(
        'Lifecycle.ps1',
        'Teardown.ps1',
        'Run-PortableValidation.ps1',
        'PORTABLE-README.txt',
        'README.md'
    )) {
        Add-PortableFile (Join-Path $root $relativeFile)
    }
    Add-PortableTree $releaseRoot
    Add-PortableTree $releaseSetsRoot
    Add-PortableFile $msiPath

    $provenancePath = Join-Path $stagingRoot 'build-provenance.json'
    [ordered]@{
        format = 1
        sourceCommit = $commit
        sourceTreeClean = $true
        buildConfiguration = 'Release'
        buildEntryPoint = 'Package.ps1 -Configuration Release'
        artifactMetadataSha256 = Get-Sha256 $metadataPath
        companionMsiSha256 = Get-Sha256 $msiPath
    } | ConvertTo-Json -Depth 6 |
        Set-Content -LiteralPath $provenancePath -Encoding utf8NoBOM
    $entries.Add([pscustomobject]@{
            path = 'build-provenance.json'
            sha256 = Get-Sha256 $provenancePath
            length = (Get-Item -LiteralPath $provenancePath).Length
        })

    [ordered]@{
        format = 3
        sourceCommit = $commit
        artifactMetadataSha256 = Get-Sha256 $metadataPath
        buildProvenanceFile = 'build-provenance.json'
        files = @($entries | Sort-Object path)
    } | ConvertTo-Json -Depth 8 |
        Set-Content -LiteralPath (Join-Path $stagingRoot 'portable-manifest.json') -Encoding utf8NoBOM
    $portableManifestSha256 = Get-Sha256 (Join-Path $stagingRoot 'portable-manifest.json')

    $archiveInputs = @(Get-ChildItem -LiteralPath $stagingRoot -Force | Select-Object -ExpandProperty FullName)
    Assert-True ($archiveInputs.Count -gt 0) 'portable staging contains files'
    Compress-Archive -LiteralPath $archiveInputs -DestinationPath $archivePath -CompressionLevel Optimal
}
finally {
    if (Test-Path -LiteralPath $stagingRoot) {
        Remove-Item -LiteralPath $stagingRoot -Recurse -Force
    }
}

@(
    "EXPECTED_SOURCE_COMMIT=$commit"
    "EXPECTED_PORTABLE_MANIFEST_SHA256=$portableManifestSha256"
) | Set-Content -LiteralPath $anchorPath -Encoding ascii

Write-Output "PORTABLE EXPORT PASS: $archivePath"
Write-Output "EXPECTED_SOURCE_COMMIT=$commit"
Write-Output "EXPECTED_PORTABLE_MANIFEST_SHA256=$portableManifestSha256"
Write-Output "Validate only with independently obtained values:"
Write-Output "pwsh.exe -NoProfile -ExecutionPolicy Bypass -File .\Run-PortableValidation.ps1 -ExpectedSourceCommit $commit -ExpectedPortableManifestSha256 $portableManifestSha256"
