<#
.SYNOPSIS
    Collect existing PR milestones or (optionally) assign/apply a milestone to missing PRs in one script.

.DESCRIPTION
    This unified script merges the behaviors of the previous add-milestone-column (collector) and
    set-milestones-missing (remote updater) scripts.

    Modes (controlled by switches):
      1. Collect (default)  – For each PR Id in the input CSV, queries GitHub for the current milestone and
                              outputs a two-column CSV (Id,Milestone) leaving blanks where none are set.
      2. LocalAssign        – Same as Collect, but for rows that end up blank assigns the value of -DefaultMilestone
                              in memory (does NOT touch GitHub). Useful for quickly preparing a fully populated CSV.
      3. ApplyMissing       – After determining which PRs have no milestone, call GitHub API to set their milestone
                              to -DefaultMilestone. Requires milestone to already exist (open). Network + write.

    You can combine LocalAssign and ApplyMissing: the remote update uses the existing live state; LocalAssign only
    affects the output CSV/pipeline objects.

.PARAMETER InputCsv
    Source CSV with at least an Id column. Default: sorted_prs.csv

.PARAMETER OutputCsv
    Destination CSV for collected (and optionally locally assigned) milestones. Default: prs_with_milestone.csv

.PARAMETER Repo
    GitHub repository (owner/name). Default: microsoft/PowerToys

.PARAMETER DefaultMilestone
    Milestone title used when -LocalAssign or -ApplyMissing is specified. Default: 'PowerToys 0.97'

.PARAMETER Offline
    Skip ALL GitHub lookups / updates. Implies Collect-only with all Milestone cells blank (unless LocalAssign).

.PARAMETER LocalAssign
    Populate empty Milestone cells in the output with -DefaultMilestone (does not modify GitHub).

.PARAMETER ApplyMissing
    For PRs which currently have no milestone (live on GitHub), set them to -DefaultMilestone via Issues API.

.PARAMETER WhatIf
    Dry run for ApplyMissing: show intended remote changes without performing PATCH requests.

.EXAMPLE
    # Collect only
    pwsh ./collect-or-apply-milestones.ps1

.EXAMPLE
    # Collect and fill blanks locally in the output only
    pwsh ./collect-or-apply-milestones.ps1 -LocalAssign

.EXAMPLE
    # Collect and remotely apply milestone to missing PRs
    pwsh ./collect-or-apply-milestones.ps1 -ApplyMissing

.EXAMPLE
    # Dry run remote application
    pwsh ./collect-or-apply-milestones.ps1 -ApplyMissing -WhatIf

.EXAMPLE
    # Offline local assignment
    pwsh ./collect-or-apply-milestones.ps1 -Offline -LocalAssign -DefaultMilestone 'PowerToys 0.96'

.NOTES
    Requires gh CLI unless -Offline AND -ApplyMissing not specified.
    Remote apply path queries milestones to resolve numeric ID.
#>
[CmdletBinding()] param(
    [Parameter(Mandatory=$false)][string]$InputCsv = 'sorted_prs.csv',
    [Parameter(Mandatory=$false)][string]$OutputCsv = 'prs_with_milestone.csv',
    [Parameter(Mandatory=$false)][string]$Repo = 'microsoft/PowerToys',
    [Parameter(Mandatory=$false)][string]$DefaultMilestone = 'PowerToys 0.97',
    [switch]$Offline,
    [switch]$LocalAssign,
    [switch]$ApplyMissing,
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
function Write-Info($m){ Write-Host "[info] $m" -ForegroundColor Cyan }
function Write-Warn($m){ Write-Host "[warn] $m" -ForegroundColor Yellow }
function Write-Err($m){ Write-Host "[error] $m" -ForegroundColor Red }

if (-not (Test-Path -LiteralPath $InputCsv)) { Write-Err "Input CSV not found: $InputCsv"; exit 1 }
$rows = Import-Csv -LiteralPath $InputCsv
if (-not $rows) { Write-Warn "Input CSV has no rows."; @() | Export-Csv -NoTypeInformation -LiteralPath $OutputCsv; exit 0 }
if (-not ($rows[0].PSObject.Properties.Name -contains 'Id')) { Write-Err "Input CSV missing 'Id' column."; exit 1 }

$needGh = (-not $Offline) -and ($ApplyMissing -or -not $Offline)
if ($needGh -and -not (Get-Command gh -ErrorAction SilentlyContinue)) { Write-Err "GitHub CLI 'gh' not found. Use -Offline or install gh."; exit 1 }

# Step 1: Collect current milestone titles
$milestoneCache = @{}
$collected = New-Object System.Collections.Generic.List[object]
$idx = 0
foreach ($row in $rows) {
    $idx++
    $id = $row.Id
    if (-not $id) { Write-Warn "Row $idx missing Id; skipping"; continue }
    $ms = ''
    if (-not $Offline) {
        if ($milestoneCache.ContainsKey($id)) { $ms = $milestoneCache[$id] }
        else {
            try {
                $json = gh pr view $id --repo $Repo --json milestone 2>$null | ConvertFrom-Json
                if ($json -and $json.milestone -and $json.milestone.title) { $ms = $json.milestone.title }
            } catch {
                Write-Warn "Failed to fetch PR #$id milestone: $_"
            }
            $milestoneCache[$id] = $ms
        }
    }
    $collected.Add([PSCustomObject]@{ Id = $id; Milestone = $ms }) | Out-Null
}

# Step 2: Remote apply (if requested)
$applySummary = @()
if ($ApplyMissing) {
    if ($Offline) { Write-Err "Cannot use -ApplyMissing with -Offline."; exit 1 }
    Write-Info "Resolving milestone id for '$DefaultMilestone' ..."
    $milestonesRaw = gh api repos/$Repo/milestones --paginate --jq '.[] | {number,title,state}'
    $msObj = $milestonesRaw | ConvertFrom-Json | Where-Object { $_.title -eq $DefaultMilestone -and $_.state -eq 'open' } | Select-Object -First 1
    if (-not $msObj) { Write-Err "Milestone '$DefaultMilestone' not found/open."; exit 1 }
    $msNumber = $msObj.number
    $targets = $collected | Where-Object { [string]::IsNullOrWhiteSpace($_.Milestone) }
    Write-Info ("ApplyMissing: {0} PR(s) without milestone." -f $targets.Count)
    foreach ($t in $targets) {
        $id = $t.Id
        try {
            # Verify still missing live
            $current = gh pr view $id --repo $Repo --json milestone --jq '.milestone.title // ""'
            if ($current) {
                $applySummary += [PSCustomObject]@{ Id=$id; Action='Skip (already has)'; Milestone=$current; Status='OK' }
                continue
            }
            if ($WhatIf) {
                $applySummary += [PSCustomObject]@{ Id=$id; Action='Would set'; Milestone=$DefaultMilestone; Status='DRY RUN' }
                continue
            }
            gh api -X PATCH -H 'Accept: application/vnd.github+json' repos/$Repo/issues/$id -f milestone=$msNumber | Out-Null
            $applySummary += [PSCustomObject]@{ Id=$id; Action='Set'; Milestone=$DefaultMilestone; Status='OK' }
            # Reflect in collected object for CSV output if not LocalAssign already doing so
            $t.Milestone = $DefaultMilestone
        } catch {
            $errText = $_ | Out-String
            $applySummary += [PSCustomObject]@{ Id=$id; Action='Failed'; Milestone=$DefaultMilestone; Status=$errText.Trim() }
            Write-Warn ("Failed to set milestone for PR #{0}: {1}" -f $id, ($errText.Trim()))
        }
    }
}

# Step 3: Local assignment (purely for output) AFTER remote so remote actual result not overwritten accidentally
if ($LocalAssign) {
    foreach ($item in $collected) {
        if ([string]::IsNullOrWhiteSpace($item.Milestone)) { $item.Milestone = $DefaultMilestone }
    }
}

# Step 4: Export CSV
$collected | Export-Csv -LiteralPath $OutputCsv -NoTypeInformation -Encoding UTF8
Write-Info ("Wrote collected CSV -> {0}" -f (Resolve-Path -LiteralPath $OutputCsv))

# Step 5: Summaries
if ($ApplyMissing) {
    $updated = ($applySummary | Where-Object { $_.Action -eq 'Set' }).Count
    $skipped = ($applySummary | Where-Object { $_.Action -like 'Skip*' }).Count
    $failed  = ($applySummary | Where-Object { $_.Action -eq 'Failed' }).Count
    Write-Info ("ApplyMissing summary: Updated={0} Skipped={1} Failed={2}" -f $updated, $skipped, $failed)
}

# Emit objects (final collected set)
return $collected
