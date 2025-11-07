[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BuildPlatform,

    [Parameter(Mandatory = $true)]
    [string]$BuildConfiguration,

    [Parameter()]
    [string]$RepoRoot = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'

function Resolve-PlatformDirectory {
    param(
        [string]$Root,
        [string]$Platform
    )

    $normalized = $Platform.Trim()
    $candidates = @()
    $candidates += Join-Path $Root $normalized
    $candidates += Join-Path $Root ($normalized.ToUpperInvariant())
    $candidates += Join-Path $Root ($normalized.ToLowerInvariant())
    $candidates = $candidates | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    return $candidates[0]
}

Write-Host "Repo root: $RepoRoot"
Write-Host "Requested build platform: $BuildPlatform"
Write-Host "Requested configuration: $BuildConfiguration"

# Always use x64 PowerToys.DSC.exe since CI/CD machines are x64
$exePlatform = 'x64'
$exeRoot = Resolve-PlatformDirectory -Root $RepoRoot -Platform $exePlatform
$exeOutputDir = Join-Path $exeRoot $BuildConfiguration
$exePath = Join-Path $exeOutputDir 'PowerToys.DSC.exe'

Write-Host "Using x64 PowerToys.DSC.exe to generate DSC manifests for $BuildPlatform build"

if (-not (Test-Path $exePath)) {
    throw "PowerToys.DSC.exe not found at '$exePath'. Make sure it has been built first."
}

Write-Host "Using PowerToys.DSC.exe at '$exePath'."

# Output DSC manifests to the target build platform directory (x64, ARM64, etc.)
$outputRoot = Resolve-PlatformDirectory -Root $RepoRoot -Platform $BuildPlatform
if (-not (Test-Path $outputRoot)) {
    Write-Host "Creating missing platform output root at '$outputRoot'."
    New-Item -Path $outputRoot -ItemType Directory -Force | Out-Null
}

$outputDir = Join-Path $outputRoot $BuildConfiguration
if (-not (Test-Path $outputDir)) {
    Write-Host "Creating missing configuration output directory at '$outputDir'."
    New-Item -Path $outputDir -ItemType Directory -Force | Out-Null
}

# DSC v3 manifests go to DSCModules subfolder
$dscOutputDir = Join-Path $outputDir 'DSCModules'
if (-not (Test-Path $dscOutputDir)) {
    Write-Host "Creating DSCModules subfolder at '$dscOutputDir'."
    New-Item -Path $dscOutputDir -ItemType Directory -Force | Out-Null
}

Write-Host "DSC manifests will be generated to: '$dscOutputDir'"

Write-Host "Cleaning previously generated DSC manifest files from '$dscOutputDir'."
Get-ChildItem -Path $dscOutputDir -Filter 'microsoft.powertoys.*.settings.dsc.resource.json' -ErrorAction SilentlyContinue | Remove-Item -Force

$arguments = @('manifest', '--resource', 'settings', '--outputDir', $dscOutputDir)
Write-Host "Invoking DSC manifest generator: '$exePath' $($arguments -join ' ')"
& $exePath @arguments
if ($LASTEXITCODE -ne 0) {
    throw "PowerToys.DSC.exe exited with code $LASTEXITCODE"
}

$generatedFiles = Get-ChildItem -Path $dscOutputDir -Filter 'microsoft.powertoys.*.settings.dsc.resource.json' -ErrorAction Stop
if ($generatedFiles.Count -eq 0) {
    throw "No DSC manifest files were generated in '$dscOutputDir'."
}

Write-Host "Generated $($generatedFiles.Count) DSC manifest file(s):"
foreach ($file in $generatedFiles) {
    Write-Host "  - $($file.FullName)"
}
