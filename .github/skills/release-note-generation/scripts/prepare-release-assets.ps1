<#
.SYNOPSIS
    Prepares the binary assets for a PowerToys GitHub release: downloads the
    four installers (per-user/per-machine x x64/arm64) and the symbol archives
    from an ADO pipeline build, computes SHA256 hashes, and emits the
    "Installer Hashes" markdown table.

.DESCRIPTION
    Given an ADO Dart pipeline build id (e.g. from
    https://microsoft.visualstudio.com/Dart/_build/results?buildId=NNN),
    downloads the four installer EXEs and the per-arch symbol zips into a
    single per-version folder, then writes a hashes.md alongside them with a
    markdown table ready to paste into the GitHub release notes.

    Requires: az login (Azure CLI authenticated), az devops extension.

.EXAMPLE
    .\prepare-release-assets.ps1 -BuildId 145505247
    .\prepare-release-assets.ps1 -BuildId 145505247 -OutputFolder D:\Releases
#>
param(
    [Parameter(Mandatory = $true)]
    [int]$BuildId,

    [string]$OutputFolder = "$env:USERPROFILE\Downloads",

    [string]$Organization = "https://dev.azure.com/microsoft",
    [string]$Project = "Dart",

    [string]$GitHubRepo = "microsoft/PowerToys"
)

$ErrorActionPreference = "Stop"
$env:AZURE_CORE_NO_PROMPT = "true"

# Work around broken az extensions: if the default extension dir has
# inaccessible files, redirect to a clean directory.
$defaultExtDir = "$env:USERPROFILE\.azure\cliextensions"
if (-not $env:AZURE_EXTENSION_DIR -and (Test-Path $defaultExtDir)) {
    $broken = Get-ChildItem "$defaultExtDir\*\*.dist-info" -Directory -ErrorAction SilentlyContinue | Where-Object {
        try { [System.IO.Directory]::GetFiles($_.FullName) | Out-Null; $false } catch { $true }
    }
    if ($broken) {
        $cleanDir = "$env:USERPROFILE\.azure\cliextensions_clean"
        Write-Host "  Detected broken az extension, redirecting to $cleanDir" -ForegroundColor Yellow
        $env:AZURE_EXTENSION_DIR = $cleanDir
        if (-not (Test-Path $cleanDir)) { New-Item -ItemType Directory -Path $cleanDir -Force | Out-Null }
    }
}

# Ensure azure-devops extension is installed
$ext = az extension list --query "[?name=='azure-devops']" -o tsv 2>$null
if (-not $ext) {
    Write-Host "Installing azure-devops extension..." -ForegroundColor Yellow
    az extension add --name azure-devops --yes 2>$null
}

# Configure az devops defaults
az devops configure --defaults organization=$Organization project=$Project 2>$null

# --- Step 1: Get build info to determine version ---
Write-Host "Fetching build $BuildId info..." -ForegroundColor Cyan
$buildJson = az pipelines build show --id $BuildId --output json 2>$null
if (-not $buildJson) {
    Write-Error "Could not fetch build $BuildId. Are you logged in (az login)?"
    exit 1
}
$build = $buildJson | ConvertFrom-Json

$versionParam = $build.templateParameters.VersionNumber
if (-not $versionParam) {
    Write-Error "Could not determine version from build $BuildId"
    exit 1
}
Write-Host "  Version: $versionParam" -ForegroundColor DarkGray

# --- Step 2: Get artifact metadata once ---
Write-Host "Fetching artifact metadata..." -ForegroundColor Cyan
$artifactsJson = az pipelines runs artifact list --run-id $BuildId --output json 2>$null
$artifacts = $artifactsJson | ConvertFrom-Json

# --- Step 3: Prepare destination folder ---
$destFolder = Join-Path $OutputFolder "PowerToys-v$versionParam"
if (-not (Test-Path $destFolder)) {
    New-Item -ItemType Directory -Path $destFolder -Force | Out-Null
}
Write-Host "  Destination: $destFolder" -ForegroundColor DarkGray

# --- Step 4: Get an ADO access token once ---
$token = az account get-access-token --resource "499b84ac-1321-427f-aa17-267ca6975798" --query accessToken -o tsv 2>$null
if (-not $token) {
    Write-Error "Failed to acquire ADO access token. Run 'az login' first."
    exit 1
}

# --- Step 5: Define the four installers to download ---
$targets = @(
    [pscustomobject]@{ Description = "Per user - x64";     Scope = "perUser";    Arch = "x64";    Artifact = "build-x64-Release";   FileName = "PowerToysUserSetup-$versionParam-x64.exe";   Ref = "ptUserX64" }
    [pscustomobject]@{ Description = "Per user - ARM64";   Scope = "perUser";    Arch = "arm64";  Artifact = "build-arm64-Release"; FileName = "PowerToysUserSetup-$versionParam-arm64.exe"; Ref = "ptUserArm64" }
    [pscustomobject]@{ Description = "Machine wide - x64"; Scope = "perMachine"; Arch = "x64";    Artifact = "build-x64-Release";   FileName = "PowerToysSetup-$versionParam-x64.exe";       Ref = "ptMachineX64" }
    [pscustomobject]@{ Description = "Machine wide - ARM64"; Scope = "perMachine"; Arch = "arm64"; Artifact = "build-arm64-Release"; FileName = "PowerToysSetup-$versionParam-arm64.exe";    Ref = "ptMachineArm64" }
)

# --- Step 6: Download each installer (skip if already present) ---
foreach ($t in $targets) {
    $destPath = Join-Path $destFolder $t.FileName

    if (Test-Path $destPath) {
        $sizeMB = [math]::Round((Get-Item $destPath).Length / 1MB, 1)
        Write-Host "[skip] $($t.FileName) already exists ($sizeMB MB)" -ForegroundColor DarkGray
        continue
    }

    $artifact = $artifacts | Where-Object { $_.name -eq $t.Artifact }
    if (-not $artifact) {
        Write-Error "Artifact '$($t.Artifact)' not found in build $BuildId. Available: $(($artifacts | ForEach-Object name) -join ', ')"
        exit 1
    }

    $baseDownloadUrl = $artifact.resource.downloadUrl
    $encodedSubPath = [Uri]::EscapeDataString("/$($t.FileName)")
    $fileUrl = $baseDownloadUrl -replace "format=zip", "format=file&subPath=$encodedSubPath"

    Write-Host "Downloading $($t.FileName) ..." -ForegroundColor Cyan
    $webClient = New-Object System.Net.WebClient
    $webClient.Headers.Add("Authorization", "Bearer $token")
    try {
        $webClient.DownloadFile($fileUrl, $destPath)
    }
    catch {
        Write-Error "Download failed for $($t.FileName): $_"
        Write-Host "URL: $fileUrl" -ForegroundColor DarkGray
        exit 1
    }
    finally {
        $webClient.Dispose()
    }

    $sizeMB = [math]::Round((Get-Item $destPath).Length / 1MB, 1)
    Write-Host "  Saved ($sizeMB MB)" -ForegroundColor Green
}

# --- Step 6b: Download symbols (one zip per arch) ---
$symbolTargets = @(
    [pscustomobject]@{ Arch = "x64";   Artifact = "build-x64-Release";   SubPath = "/symbols-x64" }
    [pscustomobject]@{ Arch = "arm64"; Artifact = "build-arm64-Release"; SubPath = "/symbols-arm64" }
)

foreach ($s in $symbolTargets) {
    $finalZip = Join-Path $destFolder "symbols-$($s.Arch).zip"
    if (Test-Path $finalZip) {
        $sizeMB = [math]::Round((Get-Item $finalZip).Length / 1MB, 1)
        Write-Host "[skip] symbols-$($s.Arch).zip already exists ($sizeMB MB)" -ForegroundColor DarkGray
        continue
    }

    $artifact = $artifacts | Where-Object { $_.name -eq $s.Artifact }
    if (-not $artifact) {
        Write-Error "Artifact '$($s.Artifact)' not found in build $BuildId."
        exit 1
    }

    $baseDownloadUrl = $artifact.resource.downloadUrl
    $encodedSubPath = [Uri]::EscapeDataString($s.SubPath)
    # Symbols are downloaded as a folder => keep format=zip and append subPath
    if ($baseDownloadUrl -match "subPath=") {
        $symbolsUrl = $baseDownloadUrl -replace "subPath=[^&]*", "subPath=$encodedSubPath"
    }
    else {
        $sep = if ($baseDownloadUrl.Contains("?")) { "&" } else { "?" }
        $symbolsUrl = "$baseDownloadUrl$sep" + "subPath=$encodedSubPath"
    }

    $tmpZip = Join-Path ([System.IO.Path]::GetTempPath()) ("ptsym-$($s.Arch)-$([Guid]::NewGuid().ToString('N')).zip")
    $tmpExtract = Join-Path ([System.IO.Path]::GetTempPath()) ("ptsym-$($s.Arch)-$([Guid]::NewGuid().ToString('N'))")

    Write-Host "Downloading symbols-$($s.Arch).zip ..." -ForegroundColor Cyan
    $webClient = New-Object System.Net.WebClient
    $webClient.Headers.Add("Authorization", "Bearer $token")
    try {
        $webClient.DownloadFile($symbolsUrl, $tmpZip)
    }
    catch {
        Write-Error "Symbols download failed for $($s.Arch): $_"
        Write-Host "URL: $symbolsUrl" -ForegroundColor DarkGray
        exit 1
    }
    finally {
        $webClient.Dispose()
    }

    Write-Host "  Extracting..." -ForegroundColor DarkGray
    if (Test-Path $tmpExtract) { Remove-Item $tmpExtract -Recurse -Force }
    Expand-Archive -Path $tmpZip -DestinationPath $tmpExtract -Force

    # Walk down while the current dir holds exactly one subfolder and no files.
    $current = Get-Item $tmpExtract
    while ($true) {
        $children = Get-ChildItem -LiteralPath $current.FullName -Force
        $subDirs = @($children | Where-Object { $_.PSIsContainer })
        $files = @($children | Where-Object { -not $_.PSIsContainer })
        if ($subDirs.Count -eq 1 -and $files.Count -eq 0) {
            $current = $subDirs[0]
        }
        else {
            break
        }
    }

    # Stage to a folder named symbols-<arch> so the zip extracts to that name.
    $stageRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("ptsym-stage-$([Guid]::NewGuid().ToString('N'))")
    $stageInner = Join-Path $stageRoot "symbols-$($s.Arch)"
    New-Item -ItemType Directory -Path $stageInner -Force | Out-Null
    Get-ChildItem -LiteralPath $current.FullName -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $stageInner -Recurse -Force
    }

    Write-Host "  Repacking to $finalZip ..." -ForegroundColor DarkGray
    if (Test-Path $finalZip) { Remove-Item $finalZip -Force }
    Compress-Archive -Path "$stageInner\*" -DestinationPath $finalZip -CompressionLevel Optimal

    Remove-Item $tmpZip -Force -ErrorAction SilentlyContinue
    Remove-Item $tmpExtract -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $stageRoot -Recurse -Force -ErrorAction SilentlyContinue

    $sizeMB = [math]::Round((Get-Item $finalZip).Length / 1MB, 1)
    Write-Host "  Saved symbols-$($s.Arch).zip ($sizeMB MB)" -ForegroundColor Green
}

# --- Step 7: Compute SHA256 and build markdown ---
Write-Host "`nComputing SHA256 hashes..." -ForegroundColor Cyan

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("## Installer Hashes")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("|  Description   | Filename | sha256 hash |")
[void]$sb.AppendLine("|----------------|----------|----------|")

foreach ($t in $targets) {
    $destPath = Join-Path $destFolder $t.FileName
    $hash = (Get-FileHash -Path $destPath -Algorithm SHA256).Hash.ToUpper()
    [void]$sb.AppendLine("| $($t.Description) | $($t.FileName) | $hash |")
    Write-Host "  $($t.FileName)  $hash" -ForegroundColor DarkGray
}

$markdown = $sb.ToString()
$mdPath = Join-Path $destFolder "hashes.md"
Set-Content -Path $mdPath -Value $markdown -Encoding UTF8

Write-Host "`nMarkdown written to: $mdPath" -ForegroundColor Green
Write-Host "`n----- Installer Hashes -----`n" -ForegroundColor Yellow
Write-Host $markdown
