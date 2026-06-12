<#
.SYNOPSIS
    Generate triage reports (summary.md + per-category .md) from categorized PRs.
    Business logic returns data; formatting functions convert to markdown.

.PARAMETER InputPath
    Path to categorized-prs.json.
.PARAMETER OutputDir
    Directory for generated reports.
.PARAMETER Repository
    GitHub repository (owner/repo).
.PARAMETER IncludeDetailedReview
    Include dimension score tables and review findings per PR.
.PARAMETER PreviousInputPath
    Path to previous run's categorized-prs.json for delta comparison.
    When provided, summary.md includes sections showing what changed.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$InputPath,
    [Parameter(Mandatory)]
    [string]$OutputDir,
    [string]$Repository = 'microsoft/PowerToys',
    [switch]$IncludeDetailedReview,
    [string]$PreviousInputPath,
    [string]$LogPath
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Join-Path (Get-Location) 'Export-TriageReport.log'
}

$logDir = Split-Path -Parent $LogPath
if (-not [string]::IsNullOrWhiteSpace($logDir) -and -not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}

"[$(Get-Date -Format o)] Starting Export-TriageReport" | Out-File -FilePath $LogPath -Encoding utf8 -Append

function Write-LogHost {
    [CmdletBinding()]
    param(
        [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
        [object[]]$Object,
        [object]$ForegroundColor,
        [object]$BackgroundColor,
        [switch]$NoNewline,
        [Object]$Separator
    )

    $message = [string]::Join(' ', ($Object | ForEach-Object { [string]$_ }))
    "[$(Get-Date -Format o)] $message" | Out-File -FilePath $LogPath -Encoding utf8 -Append

    $invokeParams = @{}
    if ($PSBoundParameters.ContainsKey('ForegroundColor') -and -not [string]::IsNullOrWhiteSpace([string]$ForegroundColor)) { $invokeParams.ForegroundColor = $ForegroundColor }
    if ($PSBoundParameters.ContainsKey('BackgroundColor') -and -not [string]::IsNullOrWhiteSpace([string]$BackgroundColor)) { $invokeParams.BackgroundColor = $BackgroundColor }
    if ($NoNewline) { $invokeParams.NoNewline = $true }
    if ($PSBoundParameters.ContainsKey('Separator')) { $invokeParams.Separator = $Separator }

    Microsoft.PowerShell.Utility\Write-Host @invokeParams -Object $message
}

# ═════════════════════════════════════════════════════════════════════════
# Data definitions — category metadata
# ═════════════════════════════════════════════════════════════════════════

$script:Categories = @(
    @{ Code = 'ready-to-merge';        Name = 'Ready to Merge';        Emoji = '🟢' }
    @{ Code = 'review-concerns';       Name = 'Review Concerns';       Emoji = '🔴' }
    @{ Code = 'approved-pending-merge'; Name = 'Approved Pending Merge'; Emoji = '✅' }
    @{ Code = 'build-failures';        Name = 'Build Failures';        Emoji = '🔧' }
    @{ Code = 'fresh-awaiting-review'; Name = 'Fresh - Awaiting Review'; Emoji = '🆕' }
    @{ Code = 'in-active-review';      Name = 'In Active Review';      Emoji = '💬' }
    @{ Code = 'stale-no-review';       Name = 'Stale - No Review';     Emoji = '😶' }
    @{ Code = 'awaiting-author';       Name = 'Awaiting Author';       Emoji = '⏳' }
    @{ Code = 'stale-with-feedback';   Name = 'Stale With Feedback';   Emoji = '📝' }
    @{ Code = 'likely-abandoned';      Name = 'Likely Abandoned';      Emoji = '💀' }
    @{ Code = 'direction-unclear';     Name = 'Direction Unclear';     Emoji = '🧭' }
    @{ Code = 'design-needed';         Name = 'Design Needed';         Emoji = '📐' }
    @{ Code = 'superseded';            Name = 'Superseded';            Emoji = '🔄' }
    @{ Code = 'needs-attention';       Name = 'Needs Attention';       Emoji = '⚡' }
)

# ═════════════════════════════════════════════════════════════════════════
# Pure data functions — return objects
# ═════════════════════════════════════════════════════════════════════════

function Get-PriorityScore ([PSCustomObject]$Pr) {
    <# Weighted composite score for sorting within a category. Higher = more urgent. #>
    $score = 0
    $score += ($Pr.AgeInDays / 30) * 10             # Age weight
    $score += ($Pr.DaysSinceActivity / 14) * 15     # Staleness weight

    if ($Pr.ApprovalCount -gt 0)        { $score += 20 }
    if ($Pr.ChecksStatus -eq 'FAILURE') { $score += 15 }
    if ($Pr.ReviewData -and $Pr.ReviewData.HighSeverity -gt 0) { $score += 25 }

    $lines = $Pr.Additions + $Pr.Deletions
    if ($lines -ge 500) { $score += 10 }

    return [Math]::Round($score, 1)
}

function Get-CategoryMeta ([string]$Code) {
    <# Looks up category metadata. #>
    $match = $script:Categories | Where-Object { $_.Code -eq $Code }
    if ($match) { return $match }
    return @{ Code = $Code; Name = $Code; Emoji = '❓' }
}

function Get-SummaryData ([PSCustomObject[]]$Prs) {
    <# Aggregates summary statistics from categorized PRs. #>
    $catGroups = $Prs | Group-Object Category | Sort-Object Count -Descending

    $breakdown = @()
    foreach ($g in $catGroups) {
        $meta = Get-CategoryMeta $g.Name
        $breakdown += [PSCustomObject]@{
            Code  = $g.Name
            Name  = $meta.Name
            Emoji = $meta.Emoji
            Count = $g.Count
        }
    }

    # Critical PRs: ready-to-merge, review-concerns with high severity, build-failures
    $critical = @($Prs | Where-Object {
        $_.Category -in 'ready-to-merge', 'build-failures' -or
        ($_.Category -eq 'review-concerns' -and $_.ReviewData -and $_.ReviewData.HighSeverity -gt 0)
    } | Sort-Object { Get-PriorityScore $_ } -Descending | Select-Object -First 10)

    # Quick wins: trivial/minor effort, has approvals or review-clean
    $quickWins = @($Prs | Where-Object {
        $_.Effort -in 'trivial', 'minor' -and
        ($_.ApprovalCount -gt 0 -or $_.Tags -contains 'review-clean')
    } | Sort-Object AgeInDays | Select-Object -First 10)

    # AI coverage
    $aiCount   = @($Prs | Where-Object { $_.CategorizationSource -like 'ai-*' }).Count
    $ruleCount = @($Prs | Where-Object { $_.CategorizationSource -like 'rules*' }).Count

    return [PSCustomObject]@{
        TotalCount       = $Prs.Count
        CategoryBreakdown = $breakdown
        CriticalPrs      = $critical
        QuickWins        = $quickWins
        AiCategorized    = $aiCount
        RuleCategorized  = $ruleCount
    }
}

function Get-CategoryPrs ([PSCustomObject[]]$AllPrs, [string]$CategoryCode) {
    <# Returns PRs for a category, sorted by priority. #>
    return @($AllPrs | Where-Object { $_.Category -eq $CategoryCode } |
        Sort-Object { Get-PriorityScore $_ } -Descending)
}

# ═════════════════════════════════════════════════════════════════════════
# Delta comparison — compare current run to previous run
# ═════════════════════════════════════════════════════════════════════════

function Get-RunDelta ([PSCustomObject[]]$CurrentPrs, [PSCustomObject[]]$PreviousPrs) {
    <#
        Compares two triage runs.  Returns a delta object with:
        - NewPrs:          in current, not in previous (new or reopened)
        - ClosedPrs:       in previous, not in current (merged or closed)
        - CategoryChanges: same PR, different category
        - Unchanged:       same PR, same category
        - RecurringAction: PRs in an actionable category for 2+ runs
    #>
    $prevLookup = @{}
    foreach ($p in $PreviousPrs) { $prevLookup[[int]$p.Number] = $p }

    $curLookup = @{}
    foreach ($p in $CurrentPrs) { $curLookup[[int]$p.Number] = $p }

    $newPrs = @()
    $categoryChanges = @()
    $unchanged = @()

    foreach ($cur in $CurrentPrs) {
        $n = [int]$cur.Number
        $prev = $prevLookup[$n]
        if (-not $prev) {
            $newPrs += $cur
        } elseif ($prev.Category -ne $cur.Category) {
            $categoryChanges += [PSCustomObject]@{
                Number       = $n
                Title        = $cur.Title
                Author       = $cur.Author
                Url          = $cur.Url
                OldCategory  = $prev.Category
                NewCategory  = $cur.Category
                AgeInDays    = $cur.AgeInDays
                Signals      = $cur.Signals
            }
        } else {
            $unchanged += $cur
        }
    }

    $closedPrs = @()
    foreach ($prev in $PreviousPrs) {
        $n = [int]$prev.Number
        if (-not $curLookup[$n]) {
            $closedPrs += $prev
        }
    }

    # Actionable categories where staying put means nobody acted
    $actionableCategories = @(
        'review-concerns', 'build-failures', 'awaiting-author',
        'stale-no-review', 'stale-with-feedback', 'direction-unclear',
        'design-needed', 'needs-attention'
    )
    $recurringAction = @($unchanged | Where-Object { $_.Category -in $actionableCategories })

    return [PSCustomObject]@{
        NewPrs           = $newPrs
        ClosedPrs        = $closedPrs
        CategoryChanges  = $categoryChanges
        Unchanged        = $unchanged
        RecurringAction  = $recurringAction
        PreviousTotal    = $PreviousPrs.Count
        CurrentTotal     = $CurrentPrs.Count
    }
}

function Format-DeltaMarkdown ([PSCustomObject]$Delta, [string]$PreviousDate) {
    <# Generates delta sections for summary.md. #>
    $lines = @()

    # ── Overview ──
    $lines += "## 📊 Changes Since Last Run ($PreviousDate)"
    $lines += ''
    $diffTotal = $Delta.CurrentTotal - $Delta.PreviousTotal
    $diffSign = if ($diffTotal -ge 0) { '+' } else { '' }
    $lines += "| Metric | Count |"
    $lines += "|--------|------:|"
    $lines += "| Previous total | $($Delta.PreviousTotal) |"
    $lines += "| Current total | $($Delta.CurrentTotal) ($diffSign$diffTotal) |"
    $lines += "| New PRs | $($Delta.NewPrs.Count) |"
    $lines += "| Closed/merged | $($Delta.ClosedPrs.Count) |"
    $lines += "| Category changed | $($Delta.CategoryChanges.Count) |"
    $lines += "| Unchanged | $($Delta.Unchanged.Count) |"
    $lines += ''

    # ── Category changes ──
    if ($Delta.CategoryChanges.Count -gt 0) {
        $lines += '## 🔀 Category Changes'
        $lines += ''
        $lines += '| PR | Author | Before | After | Signals |'
        $lines += '|-----|--------|--------|-------|---------|'
        foreach ($ch in $Delta.CategoryChanges) {
            $oldMeta = Get-CategoryMeta $ch.OldCategory
            $newMeta = Get-CategoryMeta $ch.NewCategory
            $sigs = if ($ch.Signals) { $ch.Signals -join ', ' } else { '' }
            $lines += "| [#$($ch.Number)]($($ch.Url)) $($ch.Title) | @$($ch.Author) | $($oldMeta.Emoji) $($ch.OldCategory) | $($newMeta.Emoji) $($ch.NewCategory) | $sigs |"
        }
        $lines += ''
    }

    # ── New PRs ──
    if ($Delta.NewPrs.Count -gt 0) {
        $lines += '## 🆕 New PRs Since Last Run'
        $lines += ''
        $lines += '| PR | Author | Category | Age | Signals |'
        $lines += '|-----|--------|----------|----:|---------|'
        foreach ($pr in $Delta.NewPrs | Sort-Object AgeInDays) {
            $meta = Get-CategoryMeta $pr.Category
            $sigs = if ($pr.Signals) { $pr.Signals -join ', ' } else { '' }
            $lines += "| [#$($pr.Number)]($($pr.Url)) $($pr.Title) | @$($pr.Author) | $($meta.Emoji) $($pr.Category) | $($pr.AgeInDays)d | $sigs |"
        }
        $lines += ''
    }

    # ── Closed/merged ──
    if ($Delta.ClosedPrs.Count -gt 0) {
        $lines += '## ✅ Closed/Merged Since Last Run'
        $lines += ''
        $lines += '| PR | Author | Was | Age |'
        $lines += '|-----|--------|-----|----:|'
        foreach ($pr in $Delta.ClosedPrs | Sort-Object AgeInDays -Descending | Select-Object -First 20) {
            $meta = Get-CategoryMeta $pr.Category
            $lines += "| [#$($pr.Number)]($($pr.Url)) $($pr.Title) | @$($pr.Author) | $($meta.Emoji) $($pr.Category) | $($pr.AgeInDays)d |"
        }
        if ($Delta.ClosedPrs.Count -gt 20) {
            $lines += "| | | *...and $($Delta.ClosedPrs.Count - 20) more* | |"
        }
        $lines += ''
    }

    # ── Recurring action needed ──
    if ($Delta.RecurringAction.Count -gt 0) {
        $lines += '## ⚠️ Recurring — Action Still Needed'
        $lines += ''
        $lines += 'These PRs were in an actionable category last run and **still are**. Consider taking action.'
        $lines += ''
        $lines += '| PR | Author | Category | Age | Stale |'
        $lines += '|-----|--------|----------|----:|------:|'
        foreach ($pr in $Delta.RecurringAction | Sort-Object DaysSinceActivity -Descending | Select-Object -First 20) {
            $meta = Get-CategoryMeta $pr.Category
            $lines += "| [#$($pr.Number)]($($pr.Url)) $($pr.Title) | @$($pr.Author) | $($meta.Emoji) $($pr.Category) | $($pr.AgeInDays)d | $($pr.DaysSinceActivity)d |"
        }
        if ($Delta.RecurringAction.Count -gt 20) {
            $lines += "| | | *...and $($Delta.RecurringAction.Count - 20) more* | | |"
        }
        $lines += ''
    }

    return $lines -join "`n"
}

# ═════════════════════════════════════════════════════════════════════════
# Formatting functions — convert data to markdown strings
# ═════════════════════════════════════════════════════════════════════════

function Format-SummaryMarkdown ([PSCustomObject]$Summary, [string]$Repository, [string]$DeltaMarkdown) {
    <# Generates the full summary.md content. #>
    $lines = @()
    $lines += "# PR Triage Summary — $Repository"
    $lines += ''
    $lines += "**Generated:** $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    $lines += "**Total open PRs:** $($Summary.TotalCount)"
    $lines += "**AI categorized:** $($Summary.AiCategorized) | **Rule-based:** $($Summary.RuleCategorized)"
    $lines += ''

    # Delta sections go right after the header — most actionable info first
    if ($DeltaMarkdown) {
        $lines += $DeltaMarkdown
    }

    # Category breakdown table
    $lines += '## Category Breakdown'
    $lines += ''
    $lines += '| Category | Count | |'
    $lines += '|----------|------:|---|'
    foreach ($cat in $Summary.CategoryBreakdown) {
        $bar = '█' * [Math]::Min($cat.Count, 40)
        $lines += "| $($cat.Emoji) [$($cat.Name)](categories/$($cat.Code).md) | $($cat.Count) | $bar |"
    }
    $lines += ''

    # Critical PRs
    if ($Summary.CriticalPrs.Count -gt 0) {
        $lines += '## 🚨 Critical — Needs Immediate Attention'
        $lines += ''
        $lines += '| PR | Author | Category | Age | Signals |'
        $lines += '|-----|--------|----------|----:|---------|'
        foreach ($pr in $Summary.CriticalPrs) {
            $sigs = ($pr.Signals -join ', ')
            $lines += "| [#$($pr.Number)]($($pr.Url)) $($pr.Title) | @$($pr.Author) | $($pr.Category) | $($pr.AgeInDays)d | $sigs |"
        }
        $lines += ''
    }

    # Quick wins
    if ($Summary.QuickWins.Count -gt 0) {
        $lines += '## ⚡ Quick Wins'
        $lines += ''
        $lines += '| PR | Author | Effort | Approvals |'
        $lines += '|-----|--------|--------|----------:|'
        foreach ($pr in $Summary.QuickWins) {
            $lines += "| [#$($pr.Number)]($($pr.Url)) $($pr.Title) | @$($pr.Author) | $($pr.EffortLabel) | $($pr.ApprovalCount) |"
        }
        $lines += ''
    }

    # Category links
    $lines += '## Category Reports'
    $lines += ''
    foreach ($cat in $Summary.CategoryBreakdown) {
        $lines += "- $($cat.Emoji) [$($cat.Name)](categories/$($cat.Code).md) ($($cat.Count) PRs)"
    }

    return $lines -join "`n"
}

function Format-DimensionTable ([PSCustomObject]$DimScores) {
    <# Generates a dimension score table with bar charts. #>
    if (-not $DimScores) { return '' }

    $dimNames = @('review_sentiment','author_responsiveness','code_health','merge_readiness','activity_level','direction_clarity','superseded')
    $lines = @()
    $lines += '| Dimension | Score | Confidence | |'
    $lines += '|-----------|------:|-----------:|---|'

    foreach ($name in $dimNames) {
        $d = $null
        if ($DimScores -is [hashtable]) { $d = $DimScores[$name] }
        elseif ($DimScores.PSObject.Properties[$name]) { $d = $DimScores.$name }

        if ($d -and $null -ne $d.Score) {
            $filled = [Math]::Round($d.Score * 10)
            $bar    = ('█' * $filled) + ('░' * (10 - $filled))
            $label  = $name -replace '_', ' '
            $lines += "| $label | $($d.Score) | $($d.Confidence) | $bar |"
        }
    }
    return $lines -join "`n"
}

function Format-PrDetail ([PSCustomObject]$Pr, [switch]$IncludeReview) {
    <# Formats a single PR's detail section for a category report. #>
    $lines = @()
    $lines += "### [#$($pr.Number)]($($pr.Url)) $($pr.Title)"
    $lines += ''
    $lines += "**Author:** @$($pr.Author) | **Age:** $($pr.AgeInDays) days | **Last Activity:** $($pr.DaysSinceActivity) days ago"
    $lines += "**Size:** +$($pr.Additions)/-$($pr.Deletions) ($($pr.ChangedFiles) files) | **Effort:** $($pr.EffortLabel)"

    # Source badge
    $srcBadge = switch -Wildcard ($pr.CategorizationSource) {
        'ai-dimensions' { '🤖 AI (dimensions)' }
        'ai-suggested'  { '🤖 AI (suggested)' }
        'rules'         { '📏 Rules' }
        default         { $pr.CategorizationSource }
    }
    $lines += "**Categorized by:** $srcBadge (confidence: $($pr.Confidence))"

    if ($pr.Signals -and $pr.Signals.Count -gt 0) {
        $lines += "**Signals:** $($pr.Signals -join ' | ')"
    }
    if ($pr.Tags -and $pr.Tags.Count -gt 0) {
        $lines += "**Tags:** $($pr.Tags -join ', ')"
    }
    if ($pr.Labels -and $pr.Labels.Count -gt 0) {
        $lines += "**Labels:** $($pr.Labels -join ', ')"
    }

    # Discussion summary from AI
    if ($pr.DiscussionSummary) {
        $lines += ''
        $lines += "> 💬 $($pr.DiscussionSummary)"
    }

    # Superseded by
    if ($pr.SupersededBy) {
        $lines += ''
        $lines += "> 🔄 **Superseded by:** $($pr.SupersededBy)"
    }

    # Dimension scores
    if ($IncludeReview -and $pr.DimensionScores) {
        $lines += ''
        $lines += Format-DimensionTable $pr.DimensionScores
    }

    # Review findings
    if ($IncludeReview -and $pr.ReviewData -and $pr.ReviewData.HasReview) {
        $rd = $pr.ReviewData
        $lines += ''
        $lines += "**AI Review:** $($rd.TotalFindings) findings ($($rd.HighSeverity)H/$($rd.MedSeverity)M/$($rd.LowSeverity)L)"
        if ($rd.FindingSummaries -and $rd.FindingSummaries.Count -gt 0) {
            foreach ($f in $rd.FindingSummaries) { $lines += "  - $f" }
        }
    }

    $lines += ''
    $lines += '---'
    return $lines -join "`n"
}

function Format-CategoryReport ([string]$CategoryCode, [PSCustomObject[]]$Prs, [string]$Repository, [switch]$IncludeReview) {
    <# Formats a full category report page. #>
    $meta = Get-CategoryMeta $CategoryCode

    $lines = @()
    $lines += "# $($meta.Emoji) $($meta.Name)"
    $lines += ''
    $lines += "[← Back to Summary](../summary.md)"
    $lines += ''
    $lines += "**$($Prs.Count) PRs** in this category"
    $lines += ''

    # Quick table
    $lines += '| PR | Author | Age | Signals |'
    $lines += '|-----|--------|----:|---------|'
    foreach ($pr in $Prs) {
        $sigs = if ($pr.Signals) { $pr.Signals -join ', ' } else { '' }
        $lines += "| [#$($pr.Number)]($($pr.Url)) $($pr.Title) | @$($pr.Author) | $($pr.AgeInDays)d | $sigs |"
    }
    $lines += ''
    $lines += '---'
    $lines += ''

    # Detailed entries
    foreach ($pr in $Prs) {
        $lines += Format-PrDetail -Pr $pr -IncludeReview:$IncludeReview
        $lines += ''
    }

    return $lines -join "`n"
}

# ═════════════════════════════════════════════════════════════════════════
# Main — load data, compute, write files
# ═════════════════════════════════════════════════════════════════════════

$inputData = Get-Content $InputPath -Raw | ConvertFrom-Json
$prs = if ($inputData.Prs) { $inputData.Prs } else { $inputData }

if ($prs.Count -eq 0) {
    Write-LogHost 'No PRs to report on.' -ForegroundColor Yellow
    return
}

$catDir = Join-Path $OutputDir 'categories'
if (-not (Test-Path $catDir)) { New-Item -ItemType Directory -Path $catDir -Force | Out-Null }

# Load previous run for delta comparison
$deltaMarkdown = $null
if ($PreviousInputPath -and (Test-Path $PreviousInputPath)) {
    $prevData = Get-Content $PreviousInputPath -Raw | ConvertFrom-Json
    $prevPrs = if ($prevData.Prs) { $prevData.Prs } else { $prevData }
    $prevDate = if ($PreviousInputPath -match '(\d{4}-\d{2}-\d{2})') {
        $Matches[1]
    } elseif ($prevData.CategorizedAt) {
        ([DateTime]::Parse($prevData.CategorizedAt)).ToString('yyyy-MM-dd')
    } else { 'previous run' }

    $delta = Get-RunDelta -CurrentPrs $prs -PreviousPrs $prevPrs
    $deltaMarkdown = Format-DeltaMarkdown -Delta $delta -PreviousDate $prevDate

    Write-LogHost "  Delta: $($delta.NewPrs.Count) new, $($delta.ClosedPrs.Count) closed, $($delta.CategoryChanges.Count) changed, $($delta.RecurringAction.Count) recurring" -ForegroundColor Cyan
}

# Compute summary data
$summary = Get-SummaryData -Prs $prs

# Write summary.md
$summaryMd = Format-SummaryMarkdown -Summary $summary -Repository $Repository -DeltaMarkdown $deltaMarkdown
$summaryMd | Set-Content (Join-Path $OutputDir 'summary.md') -Encoding UTF8
Write-LogHost "  summary.md ($($summary.TotalCount) PRs, $($summary.CategoryBreakdown.Count) categories)" -ForegroundColor Green

# Write per-category reports
foreach ($cat in $summary.CategoryBreakdown) {
    $catPrs = Get-CategoryPrs -AllPrs $prs -CategoryCode $cat.Code
    if ($catPrs.Count -eq 0) { continue }

    $report = Format-CategoryReport -CategoryCode $cat.Code -Prs $catPrs -Repository $Repository -IncludeReview:$IncludeDetailedReview
    $report | Set-Content (Join-Path $catDir "$($cat.Code).md") -Encoding UTF8
    Write-LogHost "  categories/$($cat.Code).md ($($catPrs.Count) PRs)" -ForegroundColor Green
}

Write-LogHost "Reports generated: $OutputDir" -ForegroundColor Cyan
