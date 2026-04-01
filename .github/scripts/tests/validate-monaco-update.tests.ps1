<#
.SYNOPSIS
    Validates that a Monaco Editor update was performed correctly.

.DESCRIPTION
    Runs a series of checks against the Monaco Editor files in the repository
    to ensure the update is valid and no regressions were introduced.

    Tests:
      - loader.js exists and contains version info
      - monaco_languages.json is valid JSON with expected structure
      - All expected built-in Monaco languages are present
      - All PowerToys custom languages are registered
      - Custom language extension mappings are present
      - Monaco directory structure is intact
      - No empty/corrupt core files

.PARAMETER RepoRoot
    The root of the PowerToys repository. Defaults to the repo root
    relative to this script.

.EXAMPLE
    ./validate-monaco-update.tests.ps1
    ./validate-monaco-update.tests.ps1 -RepoRoot "C:\src\PowerToys"
#>
[CmdletBinding()]
param(
    [Parameter()]
    [string]$RepoRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not $RepoRoot) {
    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot ".." ".." "..")).Path
}

$monacoDir = Join-Path $RepoRoot "src" "Monaco"
$monacoSrcDir = Join-Path $monacoDir "monacoSRC"
$minDir = Join-Path $monacoSrcDir "min"
$loaderJsPath = Join-Path $minDir "vs" "loader.js"
$languagesJsonPath = Join-Path $monacoDir "monaco_languages.json"
$specialLangsPath = Join-Path $monacoDir "monacoSpecialLanguages.js"
$customLangsDir = Join-Path $monacoDir "customLanguages"

$testsPassed = 0
$testsFailed = 0
$testResults = @()

function Assert-Test {
    param(
        [string]$Name,
        [scriptblock]$Test
    )

    try {
        $result = & $Test
        if ($result -eq $false) {
            throw "Assertion returned false"
        }
        $script:testsPassed++
        $script:testResults += [PSCustomObject]@{ Name = $Name; Status = "PASS"; Error = $null }
        Write-Host "  [PASS] $Name" -ForegroundColor Green
    }
    catch {
        $script:testsFailed++
        $script:testResults += [PSCustomObject]@{ Name = $Name; Status = "FAIL"; Error = $_.Exception.Message }
        Write-Host "  [FAIL] $Name" -ForegroundColor Red
        Write-Host "         $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "=== Monaco Editor Update Validation ===" -ForegroundColor Cyan
Write-Host "Repository root: $RepoRoot"
Write-Host ""

# ─── Test Group 1: Directory Structure ────────────────────────────────
Write-Host "--- Directory Structure ---" -ForegroundColor Yellow

Assert-Test "Monaco directory exists" {
    Test-Path $monacoDir
}

Assert-Test "monacoSRC directory exists" {
    Test-Path $monacoSrcDir
}

Assert-Test "min directory exists" {
    Test-Path $minDir
}

Assert-Test "vs subdirectory exists" {
    Test-Path (Join-Path $minDir "vs")
}

Assert-Test "editor directory exists" {
    Test-Path (Join-Path $minDir "vs" "editor")
}

Assert-Test "basic-languages directory exists" {
    Test-Path (Join-Path $minDir "vs" "basic-languages")
}

Assert-Test "base directory exists" {
    Test-Path (Join-Path $minDir "vs" "base")
}

Assert-Test "language directory exists" {
    Test-Path (Join-Path $minDir "vs" "language")
}

Assert-Test "customLanguages directory exists" {
    Test-Path $customLangsDir
}

# ─── Test Group 2: Core Files ─────────────────────────────────────────
Write-Host "`n--- Core Files ---" -ForegroundColor Yellow

Assert-Test "loader.js exists" {
    Test-Path $loaderJsPath
}

Assert-Test "loader.js is not empty" {
    (Get-Item $loaderJsPath).Length -gt 0
}

Assert-Test "loader.js contains version string" {
    $content = Get-Content $loaderJsPath -Raw
    $content -match 'Version:\s*\d+\.\d+\.\d+'
}

Assert-Test "editor.main.js exists" {
    Test-Path (Join-Path $minDir "vs" "editor" "editor.main.js")
}

Assert-Test "editor.main.js is not empty" {
    (Get-Item (Join-Path $minDir "vs" "editor" "editor.main.js")).Length -gt 0
}

Assert-Test "editor.main.css exists" {
    Test-Path (Join-Path $minDir "vs" "editor" "editor.main.css")
}

Assert-Test "monacoSpecialLanguages.js exists" {
    Test-Path $specialLangsPath
}

Assert-Test "generateLanguagesJson.html exists" {
    Test-Path (Join-Path $monacoDir "generateLanguagesJson.html")
}

Assert-Test "index.html exists" {
    Test-Path (Join-Path $monacoDir "index.html")
}

Assert-Test "customTokenThemeRules.js exists" {
    Test-Path (Join-Path $monacoDir "customTokenThemeRules.js")
}

# ─── Test Group 3: monaco_languages.json ──────────────────────────────
Write-Host "`n--- monaco_languages.json ---" -ForegroundColor Yellow

Assert-Test "monaco_languages.json exists" {
    Test-Path $languagesJsonPath
}

Assert-Test "monaco_languages.json is not empty" {
    (Get-Item $languagesJsonPath).Length -gt 0
}

$languagesJson = $null
Assert-Test "monaco_languages.json is valid JSON" {
    $script:languagesJson = Get-Content $languagesJsonPath -Raw | ConvertFrom-Json
    $null -ne $script:languagesJson
}

Assert-Test "JSON has 'list' property" {
    $null -ne $languagesJson.list
}

Assert-Test "Language list is a non-empty array" {
    $languagesJson.list.Count -gt 0
}

# Minimum expected languages from built-in Monaco
# These are a core subset that should always be present
$expectedBuiltinLanguages = @(
    "plaintext", "javascript", "typescript", "html", "css", "json",
    "xml", "markdown", "yaml", "python", "java", "csharp", "cpp",
    "go", "rust", "ruby", "php", "sql", "shell", "powershell",
    "dockerfile", "bat", "fsharp", "lua", "r", "swift", "kotlin",
    "scala", "perl", "dart", "ini", "vb"
)

Assert-Test "Minimum language count check (at least 80 languages)" {
    $languagesJson.list.Count -ge 80
}

$languageIds = $languagesJson.list | ForEach-Object { $_.id }

foreach ($lang in $expectedBuiltinLanguages) {
    Assert-Test "Built-in language '$lang' is present" {
        $lang -in $languageIds
    }
}

# ─── Test Group 4: PowerToys Custom Languages ─────────────────────────
Write-Host "`n--- PowerToys Custom Languages ---" -ForegroundColor Yellow

# Custom languages defined in monacoSpecialLanguages.js
$expectedCustomLanguages = @(
    @{ Id = "reg"; Extensions = @(".reg") },
    @{ Id = "gitignore"; Extensions = @(".gitignore") },
    @{ Id = "srt"; Extensions = @(".srt") }
)

foreach ($custom in $expectedCustomLanguages) {
    Assert-Test "Custom language '$($custom.Id)' is registered" {
        $custom.Id -in $languageIds
    }

    foreach ($ext in $custom.Extensions) {
        Assert-Test "Custom language '$($custom.Id)' has extension '$ext'" {
            $lang = $languagesJson.list | Where-Object { $_.id -eq $custom.Id }
            if ($null -eq $lang) { throw "Language not found" }
            $ext -in $lang.extensions
        }
    }
}

# Custom language definition files exist
$expectedCustomFiles = @("reg.js", "gitignore.js", "srt.js")
foreach ($file in $expectedCustomFiles) {
    Assert-Test "Custom language file '$file' exists" {
        Test-Path (Join-Path $customLangsDir $file)
    }
}

# ─── Test Group 5: Custom Language Extensions ─────────────────────────
Write-Host "`n--- Custom Language Extensions ---" -ForegroundColor Yellow

$expectedExtensions = @(
    @{ Id = "cppExt"; Extensions = @(".ino", ".pde") },
    @{ Id = "xmlExt"; Extensions = @(".wsdl", ".csproj", ".vcxproj", ".vbproj", ".fsproj", ".resx", ".resw") },
    @{ Id = "txtExt"; Extensions = @(".sln", ".log") },
    @{ Id = "razorExt"; Extensions = @(".razor") },
    @{ Id = "vbExt"; Extensions = @(".vbs") },
    @{ Id = "iniExt"; Extensions = @(".inf") },
    @{ Id = "shellExt"; Extensions = @(".ksh", ".zsh", ".bsh") }
)

foreach ($ext in $expectedExtensions) {
    Assert-Test "Extension mapping '$($ext.Id)' is registered" {
        $ext.Id -in $languageIds
    }

    # Spot-check at least one extension from each mapping
    $firstExt = $ext.Extensions[0]
    Assert-Test "Extension mapping '$($ext.Id)' has extension '$firstExt'" {
        $lang = $languagesJson.list | Where-Object { $_.id -eq $ext.Id }
        if ($null -eq $lang) { throw "Language not found" }
        $firstExt -in $lang.extensions
    }
}

# ─── Test Group 6: Language Entry Structure ───────────────────────────
Write-Host "`n--- Language Entry Structure ---" -ForegroundColor Yellow

Assert-Test "Every language has an 'id' field" {
    $missing = @($languagesJson.list | Where-Object { -not $_.id -or $_.id.Trim() -eq "" })
    $missing.Count -eq 0
}

Assert-Test "No duplicate language IDs" {
    $ids = $languagesJson.list | ForEach-Object { $_.id }
    $uniqueIds = $ids | Select-Object -Unique
    $ids.Count -eq $uniqueIds.Count
}

Assert-Test "JSON language with extensions has array-type extensions" {
    $withExtensions = @($languagesJson.list | Where-Object {
        ($_.PSObject.Properties.Name -contains "extensions") -and ($null -ne $_.extensions)
    })
    $invalid = @($withExtensions | Where-Object { $_.extensions -isnot [array] })
    $invalid.Count -eq 0
}

# ─── Test Group 7: Version Consistency ────────────────────────────────
Write-Host "`n--- Version Consistency ---" -ForegroundColor Yellow

Assert-Test "All NLS files in editor directory have matching versions" {
    $editorDir = Join-Path $minDir "vs" "editor"
    $nlsFiles = Get-ChildItem -Path $editorDir -Filter "editor.main.nls*.js" -ErrorAction SilentlyContinue
    if ($nlsFiles.Count -eq 0) { throw "No NLS files found" }

    $loaderContent = Get-Content $loaderJsPath -Raw
    $null = $loaderContent -match 'Version:\s*(\d+\.\d+\.\d+)'
    $loaderVersion = $Matches[1]

    foreach ($nlsFile in $nlsFiles) {
        $content = Get-Content $nlsFile.FullName -Raw -ErrorAction SilentlyContinue
        if ($content -and $content -match 'Version:\s*(\d+\.\d+\.\d+)') {
            if ($Matches[1] -ne $loaderVersion) {
                throw "Version mismatch in $($nlsFile.Name): expected $loaderVersion, found $($Matches[1])"
            }
        }
    }
    $true
}

# ─── Summary ──────────────────────────────────────────────────────────
Write-Host ""
Write-Host "=== Test Summary ===" -ForegroundColor Cyan
Write-Host "Passed: $testsPassed" -ForegroundColor Green
Write-Host "Failed: $testsFailed" -ForegroundColor $(if ($testsFailed -gt 0) { "Red" } else { "Green" })
Write-Host "Total:  $($testsPassed + $testsFailed)"

if ($testsFailed -gt 0) {
    Write-Host "`nFailed tests:" -ForegroundColor Red
    $testResults | Where-Object { $_.Status -eq "FAIL" } | ForEach-Object {
        Write-Host "  - $($_.Name): $($_.Error)" -ForegroundColor Red
    }
    exit 1
}

Write-Host "`nAll tests passed!" -ForegroundColor Green
exit 0
