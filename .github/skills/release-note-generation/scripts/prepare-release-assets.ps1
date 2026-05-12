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

# --- Helpers -----------------------------------------------------------------

# Invoke an `az` CLI command and capture stderr in $script:LastAzError so
# callers can surface the underlying message (expired login, blocked extension,
# tenant policy, ...) instead of swallowing it with `2>$null`.
function Invoke-Az {
    $tmpErr = [System.IO.Path]::GetTempFileName()
    try {
        $output = & az @args 2>$tmpErr
        $script:LastAzError = (Get-Content $tmpErr -Raw -ErrorAction SilentlyContinue).Trim()
        return $output
    }
    finally {
        Remove-Item $tmpErr -Force -ErrorAction SilentlyContinue
    }
}

# Build an ADO artifact download URL from scratch instead of regex-replacing
# the URL returned by `az pipelines runs artifact list`. Preserves any other
# query parameters and only swaps `format` and `subPath`, so we don't break if
# the upstream URL shape ever changes.
function Get-ArtifactDownloadUrl {
    param(
        [Parameter(Mandatory)][string]$BaseUrl,
        [Parameter(Mandatory)][string]$SubPath,
        [Parameter(Mandatory)][ValidateSet('file', 'zip')][string]$Format
    )
    $encodedSubPath = [Uri]::EscapeDataString($SubPath)
    $idx = $BaseUrl.IndexOf('?')
    if ($idx -lt 0) {
        return "${BaseUrl}?format=${Format}&subPath=${encodedSubPath}"
    }
    $base = $BaseUrl.Substring(0, $idx)
    $kept = $BaseUrl.Substring($idx + 1) -split '&' | Where-Object {
        $_ -and -not ($_ -match '^(format|subPath)=')
    }
    $kept = @($kept) + @("format=$Format", "subPath=$encodedSubPath")
    return "${base}?$($kept -join '&')"
}

# Download a single ADO artifact file with bearer auth and a small retry/backoff
# loop. A transient network blip on a ~200 MB installer or symbol zip otherwise
# aborts the entire release-prep run.
function Invoke-AdoDownload {
    param(
        [Parameter(Mandatory)][string]$Url,
        [Parameter(Mandatory)][string]$DestPath,
        [Parameter(Mandatory)][string]$Token,
        [int]$MaxAttempts = 3
    )
    $lastError = $null
    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        $webClient = New-Object System.Net.WebClient
        $webClient.Headers.Add("Authorization", "Bearer $Token")
        try {
            $webClient.DownloadFile($Url, $DestPath)
            return
        }
        catch {
            $lastError = $_
            if (Test-Path $DestPath) {
                Remove-Item $DestPath -Force -ErrorAction SilentlyContinue
            }
            if ($attempt -lt $MaxAttempts) {
                $backoffSec = [int][Math]::Pow(2, $attempt)  # 2, 4, 8 ...
                Write-Host "  Attempt $attempt failed: $($_.Exception.Message). Retrying in ${backoffSec}s..." -ForegroundColor Yellow
                Start-Sleep -Seconds $backoffSec
            }
        }
        finally {
            $webClient.Dispose()
        }
    }
    throw "Download failed after $MaxAttempts attempts. Last error: $($lastError.Exception.Message)`nURL: $Url"
}

# -----------------------------------------------------------------------------

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
$ext = Invoke-Az extension list --query "[?name=='azure-devops']" -o tsv
if (-not $ext) {
    Write-Host "Installing azure-devops extension..." -ForegroundColor Yellow
    Invoke-Az extension add --name azure-devops --yes | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to install azure-devops extension.`naz: $script:LastAzError"
        exit 1
    }
}

# Configure az devops defaults
Invoke-Az devops configure --defaults organization=$Organization project=$Project | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to configure az devops defaults.`naz: $script:LastAzError"
    exit 1
}

# --- Step 1: Get build info to determine version ---
Write-Host "Fetching build $BuildId info..." -ForegroundColor Cyan
$buildJson = Invoke-Az pipelines build show --id $BuildId --output json
if (-not $buildJson) {
    Write-Error "Could not fetch build $BuildId. Are you logged in (az login)?`naz: $script:LastAzError"
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
$artifactsJson = Invoke-Az pipelines runs artifact list --run-id $BuildId --output json
if (-not $artifactsJson) {
    Write-Error "Could not list artifacts for build $BuildId.`naz: $script:LastAzError"
    exit 1
}
$artifacts = $artifactsJson | ConvertFrom-Json

# --- Step 3: Prepare destination folder ---
$destFolder = Join-Path $OutputFolder "PowerToys-v$versionParam"
if (-not (Test-Path $destFolder)) {
    New-Item -ItemType Directory -Path $destFolder -Force | Out-Null
}
Write-Host "  Destination: $destFolder" -ForegroundColor DarkGray

# --- Step 4: Get an ADO access token once ---
$token = Invoke-Az account get-access-token --resource "499b84ac-1321-427f-aa17-267ca6975798" --query accessToken -o tsv
if (-not $token) {
    Write-Error "Failed to acquire ADO access token. Run 'az login' first.`naz: $script:LastAzError"
    exit 1
}

# --- Step 5: Define the four installers to download ---
$targets = @(
    [pscustomobject]@{ Description = "Per user - x64";       Scope = "perUser";    Arch = "x64";   Artifact = "build-x64-Release";   FileName = "PowerToysUserSetup-$versionParam-x64.exe" }
    [pscustomobject]@{ Description = "Per user - ARM64";     Scope = "perUser";    Arch = "arm64"; Artifact = "build-arm64-Release"; FileName = "PowerToysUserSetup-$versionParam-arm64.exe" }
    [pscustomobject]@{ Description = "Machine wide - x64";   Scope = "perMachine"; Arch = "x64";   Artifact = "build-x64-Release";   FileName = "PowerToysSetup-$versionParam-x64.exe" }
    [pscustomobject]@{ Description = "Machine wide - ARM64"; Scope = "perMachine"; Arch = "arm64"; Artifact = "build-arm64-Release"; FileName = "PowerToysSetup-$versionParam-arm64.exe" }
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

    $fileUrl = Get-ArtifactDownloadUrl -BaseUrl $artifact.resource.downloadUrl -SubPath "/$($t.FileName)" -Format file

    Write-Host "Downloading $($t.FileName) ..." -ForegroundColor Cyan
    try {
        Invoke-AdoDownload -Url $fileUrl -DestPath $destPath -Token $token
    }
    catch {
        Write-Error "Download failed for $($t.FileName): $_"
        exit 1
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

    # Symbols are downloaded as a folder => keep format=zip and append subPath
    $symbolsUrl = Get-ArtifactDownloadUrl -BaseUrl $artifact.resource.downloadUrl -SubPath $s.SubPath -Format zip

    $tmpZip = Join-Path ([System.IO.Path]::GetTempPath()) ("ptsym-$($s.Arch)-$([Guid]::NewGuid().ToString('N')).zip")
    $tmpExtract = Join-Path ([System.IO.Path]::GetTempPath()) ("ptsym-$($s.Arch)-$([Guid]::NewGuid().ToString('N'))")
    $stageRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("ptsym-stage-$([Guid]::NewGuid().ToString('N'))")

    try {
        Write-Host "Downloading symbols-$($s.Arch).zip ..." -ForegroundColor Cyan
        try {
            Invoke-AdoDownload -Url $symbolsUrl -DestPath $tmpZip -Token $token
        }
        catch {
            Write-Error "Symbols download failed for $($s.Arch): $_"
            exit 1
        }

        Write-Host "  Extracting..." -ForegroundColor DarkGray
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
        $stageInner = Join-Path $stageRoot "symbols-$($s.Arch)"
        New-Item -ItemType Directory -Path $stageInner -Force | Out-Null
        Get-ChildItem -LiteralPath $current.FullName -Force | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination $stageInner -Recurse -Force
        }

        Write-Host "  Repacking to $finalZip ..." -ForegroundColor DarkGray
        if (Test-Path $finalZip) { Remove-Item $finalZip -Force }
        Compress-Archive -Path "$stageInner\*" -DestinationPath $finalZip -CompressionLevel Optimal

        $sizeMB = [math]::Round((Get-Item $finalZip).Length / 1MB, 1)
        Write-Host "  Saved symbols-$($s.Arch).zip ($sizeMB MB)" -ForegroundColor Green
    }
    catch {
        # Don't leave a half-built zip behind if anything in the pipeline blew up.
        if (Test-Path $finalZip) { Remove-Item $finalZip -Force -ErrorAction SilentlyContinue }
        throw
    }
    finally {
        Remove-Item $tmpZip -Force -ErrorAction SilentlyContinue
        Remove-Item $tmpExtract -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item $stageRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# --- Step 7: Compute SHA256 and build markdown ---
Write-Host "`nComputing SHA256 hashes..." -ForegroundColor Cyan

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("## Installer Hashes")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("| Description | Filename | sha256 hash |")
[void]$sb.AppendLine("| --- | --- | --- |")

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

Write-Host "Draft a new GitHub release at: https://github.com/$GitHubRepo/releases/new?tag=v$versionParam" -ForegroundColor Green
