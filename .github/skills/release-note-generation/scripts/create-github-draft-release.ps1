<#
.SYNOPSIS
    Creates or updates a GitHub draft release from a prepared PowerToys release folder.

.DESCRIPTION
    Validates a prepared release folder, finds the four installers, two symbol
    zips, GPO zip, and release notes, then uses GitHub CLI (`gh`) to create or
    update a draft release and upload the assets. Asset filenames use the
    numeric product version; -TagName may include channel suffixes such as
    v0.100.2607.08001-preview.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$ReleaseFolder,

    [string]$GitHubRepo = "microsoft/PowerToys",

    [string]$TagName,

    [string]$Target,

    [string]$Title,

    [string]$NotesFile,

    [switch]$Prerelease,

    [switch]$LatestFalse,

    [switch]$Clobber,

    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

function Resolve-ExistingPath {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$Description)
    $resolved = Resolve-Path -LiteralPath $Path -ErrorAction SilentlyContinue
    if (-not $resolved) {
        throw "$Description not found: $Path"
    }

    return $resolved.ProviderPath
}

function Invoke-Gh {
    param([Parameter(Mandatory)][string[]]$Arguments, [switch]$AllowFailure)
    if ($DryRun) {
        Write-Host "gh $($Arguments -join ' ')" -ForegroundColor DarkGray
        return $null
    }

    $output = & gh @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0 -and -not $AllowFailure) {
        throw "gh $($Arguments -join ' ') failed with exit code $exitCode.`n$output"
    }

    if ($exitCode -ne 0) {
        return $null
    }

    return $output
}

function Get-ReleaseNotesFile {
    param([Parameter(Mandatory)][string]$Folder, [Parameter(Mandatory)][string]$Version, [string]$ExplicitNotesFile)
    if ($ExplicitNotesFile) {
        return Resolve-ExistingPath -Path $ExplicitNotesFile -Description "Release notes file"
    }

    $markdownFiles = @(Get-ChildItem -LiteralPath $Folder -File -Filter "*.md" | Where-Object {
        $_.Name -notmatch '^(hashes|sha256|checksums)\.md$'
    })

    foreach ($name in @("v$Version-release-notes.md", "$Version-release-notes.md", "release-notes.md", "ReleaseNotes.md", "notes.md")) {
        $match = $markdownFiles | Where-Object { $_.Name -ieq $name } | Select-Object -First 1
        if ($match) {
            return $match.FullName
        }
    }

    if ($markdownFiles.Count -eq 1) {
        return $markdownFiles[0].FullName
    }

    throw "Could not infer release notes file. Pass -NotesFile explicitly."
}

function Test-ReleaseNotesInstallerLinks {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$Tag, [Parameter(Mandatory)][string]$Version)
    $content = Get-Content -LiteralPath $Path -Raw
    $expectedLinks = @{
        "ptUserX64" = "https://github.com/$GitHubRepo/releases/download/$Tag/PowerToysUserSetup-$Version-x64.exe"
        "ptUserArm64" = "https://github.com/$GitHubRepo/releases/download/$Tag/PowerToysUserSetup-$Version-arm64.exe"
        "ptMachineX64" = "https://github.com/$GitHubRepo/releases/download/$Tag/PowerToysSetup-$Version-x64.exe"
        "ptMachineArm64" = "https://github.com/$GitHubRepo/releases/download/$Tag/PowerToysSetup-$Version-arm64.exe"
    }

    foreach ($link in $expectedLinks.GetEnumerator()) {
        if ($content -notmatch "(?im)^\[$([regex]::Escape($link.Key))\]:\s*$([regex]::Escape($link.Value))\s*$") {
            throw "Release notes are missing installer link reference [$($link.Key)]: $($link.Value)"
        }
    }
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI (`gh`) was not found in PATH."
}

Invoke-Gh -Arguments @("auth", "status") | Out-Null

$releaseFolderPath = Resolve-ExistingPath -Path $ReleaseFolder -Description "Release folder"
if (-not (Test-Path -LiteralPath $releaseFolderPath -PathType Container)) {
    throw "ReleaseFolder must be a directory: $releaseFolderPath"
}

$allFiles = @(Get-ChildItem -LiteralPath $releaseFolderPath -File)
$installerMatches = @()
foreach ($file in $allFiles) {
    if ($file.Name -match '^PowerToys(?<User>User)?Setup-(?<Version>.+)-(?<Arch>x64|arm64)\.exe$') {
        $installerMatches += [pscustomobject]@{ File = $file; Version = $Matches.Version; Arch = $Matches.Arch; IsUser = -not [string]::IsNullOrEmpty($Matches.User) }
    }
}

if ($installerMatches.Count -ne 4) {
    throw "Expected exactly four PowerToys installer EXEs, found $($installerMatches.Count)."
}

$versions = @($installerMatches | Select-Object -ExpandProperty Version -Unique)
if ($versions.Count -ne 1) {
    throw "Installer versions do not match: $($versions -join ', ')"
}

$numericVersion = $versions[0]
if (-not $TagName) {
    $TagName = "v$numericVersion"
}
if (-not $Title) {
    $displayVersion = $TagName.TrimStart('v', 'V') -replace '-preview$', ''
    $Title = if ($Prerelease) { "PowerToys v$displayVersion Preview" } else { "Release v$numericVersion" }
}

$filesByName = @{}
foreach ($file in $allFiles) {
    $filesByName[$file.Name] = $file
}

$expectedAssets = @(
    "PowerToysSetup-$numericVersion-x64.exe",
    "PowerToysSetup-$numericVersion-arm64.exe",
    "PowerToysUserSetup-$numericVersion-x64.exe",
    "PowerToysUserSetup-$numericVersion-arm64.exe",
    "symbols-x64.zip",
    "symbols-arm64.zip",
    "GroupPolicyObjectFiles-$numericVersion.zip"
)

$assets = New-Object System.Collections.Generic.List[string]
foreach ($name in $expectedAssets) {
    if (-not $filesByName.ContainsKey($name)) {
        throw "Required release asset is missing: $name"
    }
    $assets.Add($filesByName[$name].FullName)
}

$notesFilePath = Get-ReleaseNotesFile -Folder $releaseFolderPath -Version $numericVersion -ExplicitNotesFile $NotesFile
Test-ReleaseNotesInstallerLinks -Path $notesFilePath -Tag $TagName -Version $numericVersion

Write-Host "Release folder: $releaseFolderPath"
Write-Host "Repository:     $GitHubRepo"
Write-Host "Numeric version:$numericVersion"
Write-Host "Tag:            $TagName"
Write-Host "Title:          $Title"
Write-Host "Notes:          $notesFilePath"
if ($Target) {
    Write-Host "Target:         $Target"
} else {
    Write-Warning "No -Target was provided. If the tag does not already exist, gh will create it from the repository default branch."
}

$releaseJson = Invoke-Gh -Arguments @("release", "view", $TagName, "--repo", $GitHubRepo, "--json", "isDraft,url") -AllowFailure
$releaseExists = -not [string]::IsNullOrWhiteSpace(($releaseJson | Out-String))

if ($releaseExists) {
    $release = ($releaseJson | Out-String) | ConvertFrom-Json
    if (-not $release.isDraft) {
        throw "Release $TagName already exists and is not a draft: $($release.url)"
    }

    $editArgs = @("release", "edit", $TagName, "--repo", $GitHubRepo, "--draft", "--title", $Title, "--notes-file", $notesFilePath)
    if ($Target) { $editArgs += @("--target", $Target) }
    if ($Prerelease) { $editArgs += "--prerelease" }
    Invoke-Gh -Arguments $editArgs | Out-Null

    $uploadArgs = @("release", "upload", $TagName) + $assets + @("--repo", $GitHubRepo)
    if ($Clobber) { $uploadArgs += "--clobber" }
    Invoke-Gh -Arguments $uploadArgs | Out-Null
} else {
    $createArgs = @("release", "create", $TagName) + $assets + @("--repo", $GitHubRepo, "--draft", "--title", $Title, "--notes-file", $notesFilePath)
    if ($Target) { $createArgs += @("--target", $Target) }
    if ($Prerelease) { $createArgs += "--prerelease" }
    if ($Prerelease -or $LatestFalse) { $createArgs += "--latest=false" }
    Invoke-Gh -Arguments $createArgs | Out-Null
}

if ($DryRun) {
    Write-Host "Dry run complete. No GitHub release was created or modified." -ForegroundColor Yellow
} else {
    $result = Invoke-Gh -Arguments @("release", "view", $TagName, "--repo", $GitHubRepo, "--json", "url", "--jq", ".url")
    Write-Host "Draft release ready: $result" -ForegroundColor Green
}
