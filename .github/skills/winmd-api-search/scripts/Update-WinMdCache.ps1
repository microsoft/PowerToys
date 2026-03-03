<#
.SYNOPSIS
    Generate or refresh the WinMD cache for the Agent Skill.

.DESCRIPTION
    Builds and runs the standalone cache generator to export cached JSON files
    from all WinMD metadata found in project NuGet packages and Windows SDK.

    The cache is per-package+version: if two projects reference the same
    package at the same version, the WinMD data is parsed once and shared.

    Supports single project or recursive scan of an entire repo.

.PARAMETER ProjectDir
    Path to a project directory (contains .csproj/.vcxproj), or a project file itself.
    Defaults to scanning the workspace root.

.PARAMETER Scan
    Recursively discover all .csproj/.vcxproj files under ProjectDir.

.PARAMETER OutputDir
    Path to the cache output directory. Defaults to "Generated Files\winmd-cache".

.EXAMPLE
    .\Update-WinMdCache.ps1
    .\Update-WinMdCache.ps1 -ProjectDir BlankWinUI
    .\Update-WinMdCache.ps1 -Scan -ProjectDir .
    .\Update-WinMdCache.ps1 -ProjectDir "src\MyApp\MyApp.csproj"
#>
[CmdletBinding()]
param(
    [string]$ProjectDir,
    [switch]$Scan,
    [string]$OutputDir = 'Generated Files\winmd-cache'
)

$ErrorActionPreference = 'Stop'

# Convention: skill lives at .github/skills/winmd-api-search/scripts/
# so workspace root is 4 levels up from $PSScriptRoot.
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$generatorProj = Join-Path (Join-Path $PSScriptRoot 'cache-generator') 'CacheGenerator.csproj'

# Default: if no ProjectDir, scan the workspace root
if (-not $ProjectDir) {
    $ProjectDir = $root
    $Scan = $true
}

Push-Location $root

try {
    # Detect installed .NET SDK -- require >= 8.0, prefer stable over preview
    $dotnetSdks = dotnet --list-sdks 2>$null
    $bestMajor = $dotnetSdks |
        Where-Object { $_ -notmatch 'preview|rc|alpha|beta' } |
        ForEach-Object { if ($_ -match '^(\d+)\.') { [int]$Matches[1] } } |
        Where-Object { $_ -ge 8 } |
        Sort-Object -Descending |
        Select-Object -First 1

    # Fall back to preview SDKs if no stable SDK found
    if (-not $bestMajor) {
        $bestMajor = $dotnetSdks |
            ForEach-Object { if ($_ -match '^(\d+)\.') { [int]$Matches[1] } } |
            Where-Object { $_ -ge 8 } |
            Sort-Object -Descending |
            Select-Object -First 1
    }

    if (-not $bestMajor) {
        Write-Error "No .NET SDK >= 8.0 found. Install from https://dotnet.microsoft.com/download"
        exit 1
    }

    $targetFramework = "net$bestMajor.0"
    Write-Host "Using .NET SDK: $targetFramework" -ForegroundColor Cyan

    Write-Host "Building cache generator..." -ForegroundColor Cyan
    dotnet restore $generatorProj -p:TargetFramework=$targetFramework --nologo -v q
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Restore failed"
        exit 1
    }
    dotnet build $generatorProj -c Release --nologo -v q -p:TargetFramework=$targetFramework --no-restore
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed"
        exit 1
    }

    # Run the built executable directly (avoids dotnet run target framework mismatch issues)
    $generatorDir = Join-Path $PSScriptRoot 'cache-generator'
    $exePath = Join-Path $generatorDir "bin\Release\$targetFramework\CacheGenerator.exe"
    if (-not (Test-Path $exePath)) {
        # Fallback: try dll with dotnet
        $dllPath = Join-Path $generatorDir "bin\Release\$targetFramework\CacheGenerator.dll"
        if (Test-Path $dllPath) {
            $exePath = $null
        } else {
            Write-Error "Built executable not found at: $exePath"
            exit 1
        }
    }

    $runArgs = @()
    if ($Scan) {
        $runArgs += '--scan'
    }

    $runArgs += $ProjectDir
    $runArgs += $OutputDir

    Write-Host "Exporting WinMD cache..." -ForegroundColor Cyan
    if ($exePath) {
        & $exePath @runArgs
    } else {
        dotnet $dllPath @runArgs
    }
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Cache export failed"
        exit 1
    }

    Write-Host "Cache updated at: $OutputDir" -ForegroundColor Green
} finally {
    Pop-Location
}
