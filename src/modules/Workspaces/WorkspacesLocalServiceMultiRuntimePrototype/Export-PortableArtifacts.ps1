[CmdletBinding()]
param(
    [string]$DestinationDirectory = (Join-Path $PSScriptRoot 'artifacts\portable')
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$changes = @(& git -C $root status --porcelain --untracked-files=all)
if ($LASTEXITCODE -ne 0) {
    throw 'Could not inspect the prototype worktree.'
}
if ($changes.Count -ne 0) {
    throw 'Commit or remove prototype worktree changes before exporting portable artifacts.'
}
$commit = (& git -C $root rev-parse --short=10 HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($commit)) {
    throw 'Could not determine the prototype commit.'
}

$bundleName = "PtPuvr-Portable-$commit"
$destination = [IO.Path]::GetFullPath($DestinationDirectory)
$staging = Join-Path $destination $bundleName
$archive = Join-Path $destination "$bundleName.zip"
$hashFile = "$archive.sha256"
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $archive -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $hashFile -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $staging -Force | Out-Null

$relativeFiles = @(
    'Run-PortableValidation.ps1',
    'Lifecycle.ps1',
    'Teardown.ps1',
    'PORTABLE-README.txt',
    'README.md',
    'artifacts\bin\x64\Release\PtPuvrController.exe',
    'artifacts\packages\packages.json',
    'artifacts\packages\PtPuvr-TestOnly.cer',
    'artifacts\packages\PtPuvrUpdater-5.0.0.0.msix',
    'artifacts\packages\PtPuvrRuntime-Track1-1.0.0.0.msix',
    'artifacts\packages\PtPuvrRuntime-Track2-2.0.0.0.msix',
    'artifacts\simulated-bundles\PowerToys-0.101\PtPuvrUpdater-5.0.0.0.msix',
    'artifacts\simulated-bundles\PowerToys-0.101\PtPuvrRuntime-Track1-1.0.0.0.msix',
    'artifacts\simulated-bundles\PowerToys-0.110\PtPuvrUpdater-5.0.0.0.msix',
    'artifacts\simulated-bundles\PowerToys-0.110\PtPuvrRuntime-Track2-2.0.0.0.msix'
)

foreach ($relativePath in $relativeFiles) {
    $source = Join-Path $root $relativePath
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "Portable source artifact is missing: $source"
    }
    $target = Join-Path $staging $relativePath
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $target
}

$fileManifest = @(
    foreach ($relativePath in $relativeFiles) {
        $path = Join-Path $staging $relativePath
        [ordered]@{
            path = $relativePath
            length = (Get-Item -LiteralPath $path).Length
            sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        }
    }
)
[ordered]@{
    sourceCommit = $commit
    generatedAt = (Get-Date).ToUniversalTime().ToString('o')
    generatedOnWindows = [Environment]::OSVersion.Version.ToString()
    files = $fileManifest
} | ConvertTo-Json -Depth 5 |
    Set-Content -LiteralPath (Join-Path $staging 'artifact-manifest.json') -Encoding utf8NoBOM

Compress-Archive -LiteralPath $staging -DestinationPath $archive -CompressionLevel Optimal
Remove-Item -LiteralPath $staging -Recurse -Force
$archiveHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
Set-Content `
    -LiteralPath $hashFile `
    -Value "$archiveHash  $([IO.Path]::GetFileName($archive))" `
    -Encoding ascii
Write-Host "Portable archive: $archive"
Write-Host "SHA-256: $archiveHash"
Write-Host "Checksum file: $hashFile"
