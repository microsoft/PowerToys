<#
.SYNOPSIS
    Prepares the binary assets for a PowerToys GitHub release: downloads the
    four installers (per-user/per-machine x x64/arm64) and the symbol archives
    from an ADO pipeline build, computes SHA256 hashes, and emits the
    "Installer Hashes" markdown table.

.DESCRIPTION
    Given an ADO Dart pipeline build id, downloads and validates the four
    installer EXEs, GPO archive, and per-architecture symbol zips. The script
    validates installer signatures, ADO-published hashes, ZIP integrity, and
    GPO contents, then writes hashes.md and assets-manifest.json.

    Requires: az login (Azure CLI authenticated), az devops extension.

.EXAMPLE
    .\prepare-release-assets.ps1 -BuildId 145505247
    .\prepare-release-assets.ps1 -BuildId 145505247 -OutputFolder D:\Releases
#>
param(
    [Parameter(Mandatory = $true)]
    [int]$BuildId,

    [string]$Version,

    [string]$BuildMetadataPath,

    [string]$OutputFolder = "$env:USERPROFILE\Downloads",

    [string]$DestinationFolder,

    [string]$Organization = "https://dev.azure.com/microsoft",
    [string]$Project = "Dart",

    [string]$GitHubRepo = "microsoft/PowerToys",

    [ValidateRange(1, 20)]
    [int]$DownloadMaxAttempts = 3,

    [ValidateRange(0, 300)]
    [int]$DownloadRetryDelaySeconds = 10
)

$ErrorActionPreference = "Stop"
$env:AZURE_CORE_NO_PROMPT = "true"

. (Join-Path $PSScriptRoot "web-response-content.ps1")
. (Join-Path $PSScriptRoot "preview-release-assets.ps1")

# --- Helpers -----------------------------------------------------------------

# Invoke an `az` CLI command and capture stderr in $script:LastAzError so
# callers can surface the underlying message (expired login, blocked extension,
# tenant policy, ...) instead of swallowing it with `2>$null`.
function Invoke-Az {
    $tmpErr = [System.IO.Path]::GetTempFileName()
    try {
        $output = & az @args 2>$tmpErr
        # Get-Content -Raw returns $null for an empty file, and calling .Trim()
        # on $null throws under $ErrorActionPreference = 'Stop' -- which would
        # turn every successful (no-stderr) az call into a fatal error. Guard
        # explicitly so $script:LastAzError is always a (possibly empty) string.
        $rawErr = Get-Content $tmpErr -Raw -ErrorAction SilentlyContinue
        $script:LastAzError = if ($null -eq $rawErr) { '' } else { $rawErr.Trim() }
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
        [Parameter(Mandatory)][string]$Token
    )
    $lastError = $null
    for ($attempt = 1; $attempt -le $DownloadMaxAttempts; $attempt++) {
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
            if ($attempt -lt $DownloadMaxAttempts) {
                $backoffSec = if ($DownloadRetryDelaySeconds -gt 0) {
                    $DownloadRetryDelaySeconds
                }
                else {
                    [int][Math]::Pow(2, $attempt)
                }
                Write-Host "  Attempt $attempt failed: $($_.Exception.Message). Retrying in ${backoffSec}s..." -ForegroundColor Yellow
                Start-Sleep -Seconds $backoffSec
            }
        }
        finally {
            $webClient.Dispose()
        }
    }
    throw "Download failed after $DownloadMaxAttempts attempts. Last error: $($lastError.Exception.Message)`nURL: $Url"
}

function Get-RemoteHash {
    param(
        [Parameter(Mandatory)]$Artifact,
        [Parameter(Mandatory)][string]$HashFile,
        [Parameter(Mandatory)][string]$Token
    )

    $url = Get-ArtifactDownloadUrl -BaseUrl $Artifact.resource.downloadUrl -SubPath "/$HashFile" -Format file
    try {
        $response = Invoke-WebRequest `
            -Uri $url `
            -Headers @{ Authorization = "Bearer $Token" } `
            -TimeoutSec 30
        $text = ConvertFrom-WebResponseContent -Content $response.Content
        if ($text -match "[0-9a-fA-F]{64}") {
            return $matches[0].ToUpperInvariant()
        }
        throw "Hash file '$HashFile' does not contain a valid SHA256 hash."
    }
    catch {
        throw "Failed to load required ADO hash '$HashFile' from artifact '$($Artifact.name)'. $_"
    }
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
        Write-Error "Failed to install azure-devops extension. (az: $script:LastAzError)"
        exit 1
    }
}

# Configure az devops defaults
Invoke-Az devops configure --defaults organization=$Organization project=$Project | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to configure az devops defaults. (az: $script:LastAzError)"
    exit 1
}

# --- Step 1: Get build info to determine version ---
Write-Host "Fetching build $BuildId info..." -ForegroundColor Cyan
$buildJson = Invoke-Az pipelines build show --id $BuildId --output json
if (-not $buildJson) {
    Write-Error "Could not fetch build $BuildId. Are you logged in (az login)? (az: $script:LastAzError)"
    exit 1
}
$build = $buildJson | ConvertFrom-Json

$versionParam = $Version
if (-not $versionParam -and $BuildMetadataPath) {
    $versionParam = [string](Get-Content -LiteralPath $BuildMetadataPath -Raw | ConvertFrom-Json).version
}
if (-not $versionParam) {
    $versionParam = [string]$build.templateParameters.VersionNumber
}
if (-not $versionParam) {
    $metadataResolver = Join-Path $PSScriptRoot "get-release-build-metadata.ps1"
    try {
        $versionParam = [string](& $metadataResolver `
            -Build $BuildId `
            -Organization $Organization `
            -Project $Project).version
    }
    catch {
        throw "Could not determine version from build $BuildId. Pipeline metadata or an explicit -Version is required. $_"
    }
}
if ($versionParam -notmatch "^\d+\.\d+\.\d+\.0$") {
    throw "Resolved version '$versionParam' is not a valid four-component PowerToys version."
}
Write-Host "  Version: $versionParam" -ForegroundColor DarkGray

# --- Step 2: Get artifact metadata once ---
Write-Host "Fetching artifact metadata..." -ForegroundColor Cyan
$artifactsJson = Invoke-Az pipelines runs artifact list --run-id $BuildId --output json
if (-not $artifactsJson) {
    Write-Error "Could not list artifacts for build $BuildId. (az: $script:LastAzError)"
    exit 1
}
$artifacts = $artifactsJson | ConvertFrom-Json

# --- Step 3: Prepare destination folder ---
$destFolder = if ($DestinationFolder) {
    $DestinationFolder
}
else {
    Join-Path $OutputFolder "PowerToys-v$versionParam"
}
if (-not (Test-Path $destFolder)) {
    New-Item -ItemType Directory -Path $destFolder -Force | Out-Null
}
Write-Host "  Destination: $destFolder" -ForegroundColor DarkGray

$buildMarkerPath = Join-Path $destFolder ".buildinfo.json"
$sameBuild = Test-PreviewReleaseAssetBuildMarker `
    -MarkerPath $buildMarkerPath `
    -BuildId $BuildId `
    -Version $versionParam

# --- Step 4: Get an ADO access token once ---
$token = Invoke-Az account get-access-token --resource "499b84ac-1321-427f-aa17-267ca6975798" --query accessToken -o tsv
if (-not $token) {
    Write-Error "Failed to acquire ADO access token. Run 'az login' first. (az: $script:LastAzError)"
    exit 1
}

# --- Step 5: Define the four installers to download ---
$targets = @(
    [pscustomobject]@{ Description = "Per user - x64";       Scope = "perUser";    Arch = "x64";   Artifact = "build-x64-Release";   FileName = "PowerToysUserSetup-$versionParam-x64.exe";   Ref = "ptUserX64";    HashFile = "hash_user_x64.txt" }
    [pscustomobject]@{ Description = "Per user - ARM64";     Scope = "perUser";    Arch = "arm64"; Artifact = "build-arm64-Release"; FileName = "PowerToysUserSetup-$versionParam-arm64.exe"; Ref = "ptUserArm64";  HashFile = "hash_user_arm64.txt" }
    [pscustomobject]@{ Description = "Machine wide - x64";   Scope = "perMachine"; Arch = "x64";   Artifact = "build-x64-Release";   FileName = "PowerToysSetup-$versionParam-x64.exe";       Ref = "ptMachineX64"; HashFile = "hash_machine_x64.txt" }
    [pscustomobject]@{ Description = "Machine wide - ARM64"; Scope = "perMachine"; Arch = "arm64"; Artifact = "build-arm64-Release"; FileName = "PowerToysSetup-$versionParam-arm64.exe";     Ref = "ptMachineArm64"; HashFile = "hash_machine_arm64.txt" }
)

# --- Step 6: Download each installer (skip if already present) ---
foreach ($t in $targets) {
    $destPath = Join-Path $destFolder $t.FileName

    $artifact = $artifacts | Where-Object { $_.name -eq $t.Artifact }
    if (-not $artifact) {
        Write-Error "Artifact '$($t.Artifact)' not found in build $BuildId. Available: $(($artifacts | ForEach-Object name) -join ', ')"
        exit 1
    }

    if (Test-Path $destPath) {
        $sizeMB = [math]::Round((Get-Item $destPath).Length / 1MB, 1)
        $remoteHash = Get-RemoteHash -Artifact $artifact -HashFile $t.HashFile -Token $token
        $localHash = (Get-FileHash -LiteralPath $destPath -Algorithm SHA256).Hash.ToUpperInvariant()
        if ($localHash -eq $remoteHash) {
            Write-Host "[skip] $($t.FileName) already matches build $BuildId ($sizeMB MB)" -ForegroundColor DarkGray
            continue
        }
        Write-Host "[update] $($t.FileName) cannot be verified against build $BuildId" -ForegroundColor Yellow
        Remove-Item -LiteralPath $destPath -Force
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

# --- Step 6a: Download Group Policy archive ---
$gpoFileName = "GroupPolicyObjectFiles-$versionParam.zip"
$gpoPath = Join-Path $destFolder $gpoFileName
$gpoArtifact = $artifacts | Where-Object { $_.name -eq "build-x64-Release" }
if (-not $gpoArtifact) {
    throw "Artifact 'build-x64-Release' is required for the GPO archive."
}
if ((Test-Path -LiteralPath $gpoPath) -and -not $sameBuild) {
    Remove-Item -LiteralPath $gpoPath -Force
}
elseif (Test-Path -LiteralPath $gpoPath) {
    try {
        Assert-PreviewReleaseZipReadable -Path $gpoPath | Out-Null
    }
    catch {
        Write-Host "[update] $gpoFileName is corrupt and will be downloaded again" -ForegroundColor Yellow
        Remove-Item -LiteralPath $gpoPath -Force
    }
}
if (-not (Test-Path -LiteralPath $gpoPath)) {
    $gpoUrl = Get-ArtifactDownloadUrl -BaseUrl $gpoArtifact.resource.downloadUrl -SubPath "/$gpoFileName" -Format file
    Write-Host "Downloading $gpoFileName ..." -ForegroundColor Cyan
    Invoke-AdoDownload -Url $gpoUrl -DestPath $gpoPath -Token $token
}

# --- Step 6b: Download symbols (one zip per arch) ---
$symbolTargets = @(
    [pscustomobject]@{ Arch = "x64";   Artifact = "build-x64-Release";   SubPath = "/symbols-x64" }
    [pscustomobject]@{ Arch = "arm64"; Artifact = "build-arm64-Release"; SubPath = "/symbols-arm64" }
)

foreach ($s in $symbolTargets) {
    $finalZip = Join-Path $destFolder "symbols-$($s.Arch).zip"
    if ((Test-Path $finalZip) -and -not $sameBuild) {
        Remove-Item -LiteralPath $finalZip -Force
    }
    elseif (Test-Path -LiteralPath $finalZip) {
        try {
            Assert-PreviewReleaseZipReadable -Path $finalZip | Out-Null
        }
        catch {
            Write-Host "[update] symbols-$($s.Arch).zip is corrupt and will be downloaded again" -ForegroundColor Yellow
            Remove-Item -LiteralPath $finalZip -Force
        }
    }
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

# --- Step 7: Validate and inventory all release assets ---
Write-Host "`nValidating release assets..." -ForegroundColor Cyan

$assetManifestItems = @()
foreach ($t in $targets) {
    $path = Join-Path $destFolder $t.FileName
    $file = Get-Item -LiteralPath $path
    if ($file.Length -le 0) {
        throw "Installer '$($file.Name)' is empty."
    }

    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToUpperInvariant()
    $artifact = $artifacts | Where-Object { $_.name -eq $t.Artifact }
    $remoteHash = Get-RemoteHash -Artifact $artifact -HashFile $t.HashFile -Token $token
    if ($hash -ne $remoteHash) {
        throw "Installer '$($file.Name)' hash '$hash' does not match ADO-published hash '$remoteHash'."
    }

    $signature = Get-AuthenticodeSignature -LiteralPath $path
    if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
        throw "Installer '$($file.Name)' has invalid Authenticode status '$($signature.Status)'."
    }
    if (-not $signature.SignerCertificate -or
        $signature.SignerCertificate.Subject -notmatch "(^|,\s*)CN=Microsoft Corporation(,|$)") {
        throw "Installer '$($file.Name)' is not signed by Microsoft Corporation."
    }

    $assetManifestItems += [pscustomobject]@{
        name = $file.Name
        size = [long]$file.Length
        sha256 = $hash
        adoSha256 = $remoteHash
        signature = "valid"
        signer = [string]$signature.SignerCertificate.Subject
        architecture = $t.Arch
        scope = $t.Scope
    }
}

$gpoEntries = @(Assert-PreviewReleaseZipReadable -Path $gpoPath)
if (-not ($gpoEntries | Where-Object { $_ -match "(^|/)PowerToys\.admx$" })) {
    throw "GPO archive '$gpoFileName' does not contain PowerToys.admx."
}
if (-not ($gpoEntries | Where-Object { $_ -match "(^|/)en-US/PowerToys\.adml$" })) {
    throw "GPO archive '$gpoFileName' does not contain en-US/PowerToys.adml."
}

foreach ($zipName in @($gpoFileName, "symbols-x64.zip", "symbols-arm64.zip")) {
    $path = Join-Path $destFolder $zipName
    $entries = @(Assert-PreviewReleaseZipReadable -Path $path)
    $file = Get-Item -LiteralPath $path
    if ($file.Length -le 0) {
        throw "Archive '$zipName' is empty."
    }
    $assetManifestItems += [pscustomobject]@{
        name = $file.Name
        size = [long]$file.Length
        sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToUpperInvariant()
        signature = $null
        entries = $entries.Count
    }
}

# --- Step 8: Build the installer hash markdown and manifests ---
$releaseTag = "v$versionParam"
$releaseBaseUrl = "https://github.com/$GitHubRepo/releases/download/$releaseTag"
$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("## Installer Hashes")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("| Description | Filename | sha256 hash |")
[void]$sb.AppendLine("| --- | --- | --- |")

foreach ($t in $targets) {
    $item = $assetManifestItems | Where-Object { $_.name -eq $t.FileName }
    [void]$sb.AppendLine("| $($t.Description) | [$($t.FileName)][$($t.Ref)] | $($item.sha256) |")
}
[void]$sb.AppendLine("")
foreach ($t in $targets) {
    [void]$sb.AppendLine("[$($t.Ref)]: $releaseBaseUrl/$($t.FileName)")
}

$markdown = $sb.ToString()
$mdPath = Join-Path $destFolder "hashes.md"
Set-Content -LiteralPath $mdPath -Value $markdown -Encoding utf8

$assetsManifestPath = Join-Path $destFolder "assets-manifest.json"
[ordered]@{
    schemaVersion = 1
    buildId = $BuildId
    version = $versionParam
    generatedAt = (Get-Date).ToUniversalTime().ToString("o")
    assets = $assetManifestItems
} | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $assetsManifestPath -Encoding utf8

[ordered]@{
    schemaVersion = 1
    buildId = $BuildId
    version = $versionParam
    updatedAt = (Get-Date).ToUniversalTime().ToString("o")
} | ConvertTo-Json | Set-Content -LiteralPath $buildMarkerPath -Encoding utf8

Write-Host "`nAll release assets passed validation." -ForegroundColor Green
Write-Host "  Hashes: $mdPath" -ForegroundColor DarkGray
Write-Host "  Manifest: $assetsManifestPath" -ForegroundColor DarkGray

[pscustomobject]@{
    buildId = $BuildId
    version = $versionParam
    destinationFolder = (Resolve-Path -LiteralPath $destFolder).Path
    hashesPath = (Resolve-Path -LiteralPath $mdPath).Path
    assetsManifestPath = (Resolve-Path -LiteralPath $assetsManifestPath).Path
    assetCount = $assetManifestItems.Count
}
