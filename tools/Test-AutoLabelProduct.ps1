<#
.SYNOPSIS
    Test the auto-label-product workflow logic locally against real issues.

.DESCRIPTION
    Fetches issues with "Needs-Triage" label but no "Product-*" label from the
    PowerToys repo and simulates what the GitHub Action would do (without actually
    applying labels). This lets you validate the mapping and AI inference before
    merging the workflow.

.PARAMETER Apply
    Actually apply the labels via `gh issue edit`. Requires gh auth.

.PARAMETER Limit
    Number of issues to process (default: 10).

.EXAMPLE
    # Dry run - see what would happen
    .\Test-AutoLabelProduct.ps1

.EXAMPLE
    # Apply labels to first 5 issues
    .\Test-AutoLabelProduct.ps1 -Apply -Limit 5

.NOTES
    Prerequisites:
    - gh CLI authenticated: `gh auth login`
    - PowerShell 7+
#>

param(
    [switch]$Apply,
    [int]$Limit = 10
)

$ErrorActionPreference = 'Stop'

# ─── Mapping (must match the workflow) ────────────────────────────────────────
$AREA_TO_LABEL = @{
    'Advanced Paste'                   = 'Product-Advanced Paste'
    'Always on Top'                    = 'Product-Always On Top'
    'Awake'                            = 'Product-Awake'
    'ColorPicker'                      = 'Product-Color Picker'
    'Command not found'                = 'Product-CommandNotFound'
    'Command Palette'                  = 'Product-Command Palette'
    'Crop and Lock'                    = 'Product-CropAndLock'
    'Environment Variables'            = 'Product-Environment Variables'
    'FancyZones'                       = 'Product-FancyZones'
    'FancyZones Editor'                = 'Product-FancyZones'
    'File Locksmith'                   = 'Product-File Locksmith'
    'File Explorer: Preview Pane'      = 'Product-File Explorer'
    'File Explorer: Thumbnail preview' = 'Product-File Explorer'
    'Hosts File Editor'                = 'Product-Hosts File Editor'
    'Image Resizer'                    = 'Product-Image Resizer'
    'Keyboard Manager'                 = 'Product-Keyboard Shortcut Manager'
    'Light Switch'                     = 'Product-LightSwitch'
    'Mouse Utilities'                  = 'Product-Mouse Utilities'
    'Mouse Without Borders'            = 'Product-Mouse Without Borders'
    'New+'                             = 'Product-New+'
    'Peek'                             = 'Product-Peek'
    'Power Display'                    = 'Product-PowerDisplay'
    'PowerRename'                      = 'Product-PowerRename'
    'PowerToys Run'                    = 'Product-PowerToys Run'
    'Quick Accent'                     = 'Product-Quick Accent'
    'Registry Preview'                 = 'Product-Registry Preview'
    'Screen ruler'                     = 'Product-Screen Ruler'
    'Settings'                         = 'Product-Settings'
    'Shortcut Guide'                   = 'Product-Shortcut Guide'
    'TextExtractor'                    = 'Product-Text Extractor'
    'Workspaces'                       = 'Product-Workspaces'
    'ZoomIt'                           = 'Product-ZoomIt'
    'General'                          = 'Product-General'
    'Grab And Move'                    = 'Product-Grab And Move'
}

# Non-product areas (no label applied, AI fallback triggers)
$NON_PRODUCT_AREAS = @('Installer', 'System tray interaction', 'Welcome / PowerToys Tour window')

# ─── Fetch issues ────────────────────────────────────────────────────────────
Write-Host "`n🔍 Fetching issues with 'Needs-Triage' and no 'Product-*' label (limit: $Limit)..." -ForegroundColor Cyan

# gh search finds issues with Needs-Triage; we filter out those that already have Product- labels
$ghStderrPath = [System.IO.Path]::GetTempFileName()
try {
    $issuesJson = gh issue list --repo microsoft/PowerToys --label "Needs-Triage" --limit 100 --json number,title,body,labels --state open 2> $ghStderrPath
    $ghExitCode = $LASTEXITCODE
    $ghErrorOutput = Get-Content -Path $ghStderrPath -Raw
}
finally {
    Remove-Item -Path $ghStderrPath -ErrorAction SilentlyContinue
}

if ($ghExitCode -ne 0) {
    Write-Host "❌ Failed to fetch issues. Ensure 'gh auth login' is done." -ForegroundColor Red
    if (-not [string]::IsNullOrWhiteSpace($ghErrorOutput)) {
        Write-Host $ghErrorOutput
    }
    exit 1
}

if (-not [string]::IsNullOrWhiteSpace($ghErrorOutput)) {
    Write-Host "⚠️ gh emitted stderr output while fetching issues:" -ForegroundColor Yellow
    Write-Host $ghErrorOutput
}

$issues = $issuesJson | ConvertFrom-Json

# Filter: only issues WITHOUT any Product-* label
$issues = $issues | Where-Object {
    $labels = $_.labels | ForEach-Object { $_.name }
    -not ($labels | Where-Object { $_ -like 'Product-*' })
} | Select-Object -First $Limit

Write-Host "📋 Found $($issues.Count) issues to process.`n" -ForegroundColor Green

# ─── Process each issue ──────────────────────────────────────────────────────
$results = @()

foreach ($issue in $issues) {
    $body = $issue.body
    $title = $issue.title
    $number = $issue.number

    Write-Host "--- Issue #${number}: ${title} ---" -ForegroundColor Yellow

    # Parse "Area(s) with issue?" field
    $selectedAreas = @()
    if ($body -match '### Area\(s\) with issue\?\s*\r?\n\r?\n([\s\S]*?)(?=\r?\n\r?\n###|\s*$)') {
        $areaText = $Matches[1].Trim()
        $selectedAreas = $areaText -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ }
    }

    if ($selectedAreas.Count -eq 0) {
        Write-Host "  ⚠️  No 'Area(s) with issue?' field found in body." -ForegroundColor DarkYellow
    } else {
        Write-Host "  📌 Areas selected: $($selectedAreas -join ', ')" -ForegroundColor DarkCyan
    }

    # Resolve labels
    $resolvedLabels = @()
    $unmapped = @()

    foreach ($area in $selectedAreas) {
        if ($AREA_TO_LABEL.ContainsKey($area)) {
            $resolvedLabels += $AREA_TO_LABEL[$area]
        } elseif ($area -notin $NON_PRODUCT_AREAS) {
            $unmapped += $area
        }
    }
    $resolvedLabels = $resolvedLabels | Sort-Object -Unique

    if ($unmapped.Count -gt 0) {
        Write-Host "  ⚠️  Unmapped areas (need mapping update): $($unmapped -join ', ')" -ForegroundColor DarkYellow
    }

    if ($resolvedLabels.Count -eq 0) {
        Write-Host "  🤖 No deterministic match → AI inference would trigger in workflow" -ForegroundColor Magenta
    } else {
        Write-Host "  ✅ Would apply: $($resolvedLabels -join ', ')" -ForegroundColor Green
    }

    # Apply if requested
    if ($Apply -and $resolvedLabels.Count -gt 0) {
        foreach ($label in $resolvedLabels) {
            Write-Host "  🏷️  Applying label: $label" -ForegroundColor White
            gh issue edit $number --repo microsoft/PowerToys --add-label $label 2>&1 | Out-Null
            if ($LASTEXITCODE -ne 0) {
                Write-Host "  ❌ Failed to apply '$label' (may not exist in repo)" -ForegroundColor Red
            }
        }
    }

    $results += [PSCustomObject]@{
        Issue   = $number
        Title   = $title.Substring(0, [Math]::Min(60, $title.Length))
        Areas   = ($selectedAreas -join ', ')
        Labels  = ($resolvedLabels -join ', ')
        NeedsAI = ($resolvedLabels.Count -eq 0)
    }
    Write-Host ""
}

# ─── Summary ─────────────────────────────────────────────────────────────────
Write-Host "`n═══ SUMMARY ═══" -ForegroundColor Cyan
$results | Format-Table -AutoSize -Wrap

$aiNeeded = ($results | Where-Object { $_.NeedsAI }).Count
$mapped = ($results | Where-Object { -not $_.NeedsAI }).Count
Write-Host "Deterministic: $mapped | AI fallback needed: $aiNeeded | Total: $($results.Count)" -ForegroundColor Cyan

if (-not $Apply) {
    Write-Host "`n💡 This was a DRY RUN. Use -Apply to actually add labels." -ForegroundColor Yellow
}
