<#
.SYNOPSIS
    Enrich PRs with GitHub API data, merge AI evaluation, and categorize.
    Pure business-logic — all functions return objects, no Write-Host in helpers.

.PARAMETER InputPath
    Path to all-prs.json (from Get-OpenPrs.ps1).
.PARAMETER OutputPath
    Where to write categorized-prs.json. Default: categorized-prs.json
.PARAMETER Repository
    GitHub repo (owner/repo).  Default: microsoft/PowerToys
.PARAMETER ThrottleLimit
    Max parallel API calls.  Default: 5.
.PARAMETER AiEnrichmentPath
    Path to ai-enrichment.json (from Invoke-AiEnrichment.ps1).
#>
# NOTE: Do NOT use [CmdletBinding()] or [Parameter(Mandatory)] here.
# This script may be called from within an orchestrator job scope where
# advanced function attributes propagate ErrorActionPreference and crash.
param(
    [string]$InputPath,
    [string]$OutputPath = 'categorized-prs.json',
    [string]$Repository = 'microsoft/PowerToys',
    [int]$ThrottleLimit = 20,
    [string]$AiEnrichmentPath,
    [string]$ReviewOutputRoot = 'Generated Files/prReview',
    [string]$LogPath
)

$ErrorActionPreference = 'Stop'

# Manual validation (replacing [Parameter(Mandatory)])
if (-not $InputPath -or -not (Test-Path $InputPath)) {
    Write-Error "Invoke-PrCategorization: -InputPath is required and must exist. Got: '$InputPath'"
    return
}

if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Join-Path (Get-Location) 'Invoke-PrCategorization.log'
}

$logDir = Split-Path -Parent $LogPath
if (-not [string]::IsNullOrWhiteSpace($logDir) -and -not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}

"[$(Get-Date -Format o)] Starting Invoke-PrCategorization" | Out-File -FilePath $LogPath -Encoding utf8 -Append

function Write-LogHost {
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

$repoRoot = git rev-parse --show-toplevel 2>$null
if (-not $repoRoot) { $repoRoot = (Get-Location).Path }

$resolvedReviewOutputRoot = if ([System.IO.Path]::IsPathRooted($ReviewOutputRoot)) {
    $ReviewOutputRoot
} else {
    Join-Path $repoRoot $ReviewOutputRoot
}

# ═════════════════════════════════════════════════════════════════════════
# Pure functions — return objects, zero side effects
# ═════════════════════════════════════════════════════════════════════════

#region Review findings

function Get-ReviewFindings ([int]$PRNumber, [string]$ReviewRoot) {
    <# Parses prReview output. Returns structured review data object. #>
    $reviewDir = Join-Path $ReviewRoot $PRNumber.ToString()
    $result = [PSCustomObject]@{
        HasReview     = $false
        Signal        = $null
        HighSeverity  = 0
        MedSeverity   = 0
        LowSeverity   = 0
        TotalFindings = 0
        StepIssues    = @()
        Findings      = @()
    }

    if (-not (Test-Path $reviewDir)) { return $result }

    # Signal file
    $signalFile = Join-Path $reviewDir '.signal'
    if (Test-Path $signalFile) {
        $result.Signal = (Get-Content $signalFile -Raw).Trim()
    }

    # Overview
    $overviewPath = Join-Path $reviewDir '00-OVERVIEW.md'
    if (-not (Test-Path $overviewPath)) { return $result }
    $result.HasReview = $true

    # Parse step files for mcp-review-comment blocks
    Get-ChildItem -Path $reviewDir -Filter '*.md' -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^\d{2}-' } | ForEach-Object {
        $stepName = $_.BaseName
        $content  = Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue
        if (-not $content) { return }

        [regex]::Matches($content, '(?s)```mcp-review-comment\s*\n(.*?)```') | ForEach-Object {
            try {
                $json = $_.Groups[1].Value | ConvertFrom-Json -ErrorAction SilentlyContinue
                if ($json.severity) {
                    $result.Findings += [PSCustomObject]@{
                        Severity = $json.severity.ToLower()
                        Body     = $json.body
                        Path     = $json.path
                        Line     = $json.line
                        Step     = $stepName
                    }
                }
            } catch { }
        }
    }

    $result.TotalFindings = $result.Findings.Count
    $result.HighSeverity  = @($result.Findings | Where-Object { $_.Severity -eq 'high' }).Count
    $result.MedSeverity   = @($result.Findings | Where-Object { $_.Severity -eq 'medium' }).Count
    $result.LowSeverity   = @($result.Findings | Where-Object { $_.Severity -eq 'low' }).Count

    return $result
}

function Get-FindingSummaries ([PSCustomObject[]]$Findings, [int]$MaxPerSeverity = 3) {
    <# Sorts findings by severity, truncates body, returns string array. #>
    $severityOrder = @{ 'high' = 0; 'medium' = 1; 'low' = 2 }
    $sorted = $Findings | Sort-Object { $severityOrder[$_.Severity] ?? 3 }

    $lines = @()
    $grouped = $sorted | Group-Object Severity
    foreach ($g in $grouped) {
        $items = $g.Group | Select-Object -First $MaxPerSeverity
        foreach ($f in $items) {
            $body = if ($f.Body.Length -gt 120) { $f.Body.Substring(0, 117) + '...' } else { $f.Body }
            $lines += "[$($f.Severity.ToUpper())] $body"
        }
    }
    return $lines
}

#endregion

#region Effort estimation

function Get-EffortEstimate ([PSCustomObject]$ReviewData) {
    <# Returns effort string based on review severity counts. #>
    if (-not $ReviewData.HasReview) { return 'unknown' }
    $h = $ReviewData.HighSeverity; $m = $ReviewData.MedSeverity; $l = $ReviewData.LowSeverity
    $total = $ReviewData.TotalFindings

    if ($total -eq 0)              { return 'trivial' }
    if ($h -ge 3)                  { return 'rework' }
    if ($h -ge 1 -and $m -ge 2)   { return 'major' }
    if ($h -ge 1 -or $m -ge 3)    { return 'moderate' }
    if ($m -ge 1)                  { return 'minor' }
    return 'trivial'
}

function Get-EffortLabel ([string]$Effort) {
    <# Maps effort string to human-readable label. #>
    switch ($Effort) {
        'trivial'  { '✅ Trivial (clean or near-clean)' }
        'minor'    { '🔧 Minor (small fixes needed)' }
        'moderate' { '⚠️ Moderate (several issues)' }
        'major'    { '🔴 Major (significant work)' }
        'rework'   { '🚨 Rework (fundamental problems)' }
        default    { '❓ Unknown (no review data)' }
    }
}

#endregion

#region AI dimension rules

function Get-CategoryFromDimensions ([hashtable]$Dims, [string]$SuggestedCategory) {
    <#
        Derives a triage category from AI evaluation dimension scores.
        Uses rules R-AI-1 through R-AI-15 in priority order.
        Returns @{ Category; Confidence; Source }.

        Design principles:
        - Superseded/abandoned checked first (terminal states trump quality signals)
        - Positive outcomes next (ready, approved) — with code-health guards
        - Technical blockers (build failures) — no sentiment gate
        - Human blockers (review concerns, direction) — split rs vs ch
        - Activity-based buckets last (stale, fresh, active)
        - Neutral band (0.45–0.55) instead of float equality on 0.5
        - Enrichment cross-check happens in the caller after this returns
    #>
    # Helper to safely get a dimension score
    $s = { param([string]$Name) if ($Dims[$Name]) { $Dims[$Name].Score } else { 0.5 } }

    $sup   = & $s 'superseded'
    $mr    = & $s 'merge_readiness'
    $rs    = & $s 'review_sentiment'
    $ch    = & $s 'code_health'
    $ar    = & $s 'author_responsiveness'
    $al    = & $s 'activity_level'
    $dc    = & $s 'direction_clarity'

    # Neutral band — AI rarely returns exactly 0.5; treat 0.45–0.55 as "no signal"
    $isNeutral = { param([double]$v) $v -ge 0.45 -and $v -le 0.55 }

    # Get average confidence
    $allConf = @($Dims.Values | ForEach-Object { $_.Confidence } | Where-Object { $_ })
    $avgConf = if ($allConf.Count -gt 0) { ($allConf | Measure-Object -Average).Average } else { 0.5 }

    # ── Terminal states ──────────────────────────────────────────────────

    # R-AI-1: Superseded — binary signal, highest priority
    if ($sup -ge 0.7)                { return @{ Category = 'superseded';      Confidence = $avgConf; Source = 'ai-dimensions' } }

    # R-AI-2: Likely abandoned — dead PR; no point evaluating quality
    if ($al -le 0.2 -and $ar -le 0.2) { return @{ Category = 'likely-abandoned'; Confidence = $avgConf; Source = 'ai-dimensions' } }

    # ── Positive outcomes ────────────────────────────────────────────────

    # R-AI-3: Ready to merge — all three quality gates high
    if ($mr -ge 0.8 -and $rs -ge 0.7 -and $ch -ge 0.7) { return @{ Category = 'ready-to-merge'; Confidence = $avgConf; Source = 'ai-dimensions' } }

    # R-AI-4: Approved pending merge — positive reviews + moderate readiness + acceptable code health
    if ($rs -ge 0.7 -and $mr -ge 0.5 -and $mr -lt 0.8 -and $ch -ge 0.4) { return @{ Category = 'approved-pending-merge'; Confidence = $avgConf; Source = 'ai-dimensions' } }

    # ── Technical blockers ───────────────────────────────────────────────

    # R-AI-5: Build failures — low code health + low merge readiness (no sentiment gate)
    if ($ch -le 0.3 -and $mr -le 0.3) { return @{ Category = 'build-failures'; Confidence = $avgConf; Source = 'ai-dimensions' } }

    # ── Human blockers ───────────────────────────────────────────────────

    # R-AI-6: Review concerns — reviewers explicitly flagged problems
    if ($rs -le 0.3)                  { return @{ Category = 'review-concerns'; Confidence = $avgConf; Source = 'ai-dimensions' } }

    # R-AI-7: Design needed — direction very unclear
    if ($dc -le 0.3)                  { return @{ Category = 'design-needed';   Confidence = $avgConf; Source = 'ai-dimensions' } }

    # R-AI-8: Direction unclear — moderate confusion + lukewarm reviews
    if ($dc -le 0.5 -and $rs -le 0.5) { return @{ Category = 'direction-unclear'; Confidence = $avgConf; Source = 'ai-dimensions' } }

    # R-AI-9: Low code health — AI sees quality issues reviewers may have missed
    # Separate from R-AI-6 (reviewer pushback) — lower confidence since reviewers aren't complaining
    if ($ch -le 0.3)                  { return @{ Category = 'review-concerns'; Confidence = [Math]::Max($avgConf - 0.1, 0.1); Source = 'ai-dimensions' } }

    # ── Author responsiveness ────────────────────────────────────────────

    # R-AI-10: Awaiting author — unresponsive but PR still has activity from others
    if ($ar -le 0.3 -and $al -ge 0.3) { return @{ Category = 'awaiting-author'; Confidence = $avgConf; Source = 'ai-dimensions' } }

    # ── Activity-based buckets (use neutral band) ────────────────────────

    # R-AI-11: Stale with feedback — low activity, has reviewer signal (not neutral)
    if ($al -le 0.3 -and -not (& $isNeutral $rs)) { return @{ Category = 'stale-with-feedback'; Confidence = $avgConf; Source = 'ai-dimensions' } }

    # R-AI-12: Stale, no review — low activity, neutral sentiment (nobody looked)
    if ($al -le 0.3 -and (& $isNeutral $rs))      { return @{ Category = 'stale-no-review';     Confidence = $avgConf; Source = 'ai-dimensions' } }

    # R-AI-13: Fresh, awaiting review — high activity, neutral sentiment
    if ($al -ge 0.6 -and (& $isNeutral $rs))       { return @{ Category = 'fresh-awaiting-review'; Confidence = $avgConf; Source = 'ai-dimensions' } }

    # R-AI-14: In active review — moderate+ activity with reviewer engagement
    if ($al -ge 0.4 -and $rs -ge 0.5)              { return @{ Category = 'in-active-review';   Confidence = $avgConf; Source = 'ai-dimensions' } }

    # R-AI-15: Fallback — use AI suggestion or needs-attention
    if ($SuggestedCategory) { return @{ Category = $SuggestedCategory; Confidence = [Math]::Max($avgConf - 0.1, 0.1); Source = 'ai-suggested' } }
    return @{ Category = 'needs-attention'; Confidence = 0.3; Source = 'ai-fallback' }
}

#endregion

#region Deterministic fallback rules

function Get-CategoryFromRules ([PSCustomObject]$Pr, [PSCustomObject]$Enrichment) {
    <#
        Deterministic rule-based categorization when AI evaluation is not available.
        Returns @{ Category; Confidence; Source }.
    #>
    $e   = $Enrichment
    $now = Get-Date
    $age = [Math]::Floor(($now - [DateTime]::Parse($Pr.CreatedAt)).TotalDays)

    $lastActivity = @($Pr.UpdatedAt, $e.LastCommentAt, $e.LastCommitAt) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        ForEach-Object { [DateTime]::Parse($_) } |
        Sort-Object -Descending | Select-Object -First 1
    $daysSinceActivity = if ($lastActivity) { [Math]::Floor(($now - $lastActivity).TotalDays) } else { $age }

    $authorLast = if ($e.AuthorLastActivityAt) { [DateTime]::Parse($e.AuthorLastActivityAt) } else { $null }
    $daysSinceAuthor = if ($authorLast) { [Math]::Floor(($now - $authorLast).TotalDays) } else { $age }

    $hasApprovals   = $e.ApprovalCount -gt 0
    $hasChangesReq  = $e.ChangesRequestedCount -gt 0
    $ciGreen        = $e.ChecksStatus -eq 'SUCCESS'
    $ciFailing      = $e.ChecksStatus -eq 'FAILURE'
    $hasComments    = $e.CommentCount -gt 0
    $hasReviews     = $hasApprovals -or $hasChangesReq

    # Rule 1: Approved + CI green + no unresolved objections
    if ($hasApprovals -and $ciGreen -and -not $hasChangesReq -and $Pr.Mergeable -eq 'MERGEABLE') {
        return @{ Category = 'ready-to-merge'; Confidence = 0.8; Source = 'rules' }
    }
    # Rule 2: CI failing
    if ($ciFailing) {
        return @{ Category = 'build-failures'; Confidence = 0.85; Source = 'rules' }
    }
    # Rule 3: Approved but CI not green
    if ($hasApprovals -and -not $ciFailing -and -not $hasChangesReq) {
        return @{ Category = 'approved-pending-merge'; Confidence = 0.7; Source = 'rules' }
    }
    # Rule 4: Changes requested + author silent > 14 days
    if ($hasChangesReq -and $daysSinceAuthor -ge 14) {
        return @{ Category = 'awaiting-author'; Confidence = 0.75; Source = 'rules' }
    }
    # Rule 5: Likely abandoned (90+ days no activity)
    if ($daysSinceActivity -ge 90) {
        return @{ Category = 'likely-abandoned'; Confidence = 0.8; Source = 'rules' }
    }
    # Rule 6: Stale with feedback (30+ days, has reviews)
    if ($daysSinceActivity -ge 30 -and $hasReviews) {
        return @{ Category = 'stale-with-feedback'; Confidence = 0.65; Source = 'rules' }
    }
    # Rule 7: Stale, no review (30+ days, no reviews)
    if ($daysSinceActivity -ge 30 -and -not $hasReviews) {
        return @{ Category = 'stale-no-review'; Confidence = 0.65; Source = 'rules' }
    }
    # Rule 8: Changes requested, author responding
    if ($hasChangesReq -and $daysSinceAuthor -lt 14) {
        return @{ Category = 'review-concerns'; Confidence = 0.6; Source = 'rules' }
    }
    # Rule 9: Fresh PR, no reviews yet (< 7 days)
    if ($age -le 7 -and -not $hasReviews) {
        return @{ Category = 'fresh-awaiting-review'; Confidence = 0.7; Source = 'rules' }
    }
    # Rule 10: In active review (recent activity + has comments/reviews)
    if ($daysSinceActivity -le 7 -and ($hasComments -or $hasReviews)) {
        return @{ Category = 'in-active-review'; Confidence = 0.6; Source = 'rules' }
    }
    # Rule 11: Medium age, no review
    if (-not $hasReviews -and $age -gt 7 -and $age -le 30) {
        return @{ Category = 'stale-no-review'; Confidence = 0.5; Source = 'rules' }
    }
    # Rule 12: Has comments but not formally reviewed
    if ($hasComments -and -not $hasReviews -and $daysSinceActivity -le 14) {
        return @{ Category = 'in-active-review'; Confidence = 0.4; Source = 'rules' }
    }
    # Rule 13: Fallback
    return @{ Category = 'needs-attention'; Confidence = 0.3; Source = 'rules-fallback' }
}

#endregion

#region Build categorized PR object

function New-CategorizedPr {
    param(
        [PSCustomObject]$Pr,
        [PSCustomObject]$Enrichment,
        [PSCustomObject]$ReviewData,
        [hashtable]$CatResult,
        [PSCustomObject]$AiEval
    )

    $now = Get-Date
    $age = [Math]::Floor(($now - [DateTime]::Parse($Pr.CreatedAt)).TotalDays)

    $lastActivity = @($Pr.UpdatedAt, $Enrichment.LastCommentAt, $Enrichment.LastCommitAt) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        ForEach-Object { [DateTime]::Parse($_) } |
        Sort-Object -Descending | Select-Object -First 1
    $daysSinceActivity = if ($lastActivity) { [Math]::Floor(($now - $lastActivity).TotalDays) } else { $age }

    $effort = Get-EffortEstimate -ReviewData $ReviewData
    $findingSummaries = Get-FindingSummaries -Findings $ReviewData.Findings

    # Signals for quick scanning
    $signals = @()
    if ($Enrichment.ApprovalCount -gt 0)         { $signals += "✅$($Enrichment.ApprovalCount) approvals" }
    if ($Enrichment.ChangesRequestedCount -gt 0)  { $signals += "❌$($Enrichment.ChangesRequestedCount) changes requested" }
    if ($Enrichment.ChecksStatus -eq 'FAILURE')   { $signals += '🔴 CI failing' }
    if ($Enrichment.ChecksStatus -eq 'SUCCESS')   { $signals += '🟢 CI passing' }
    if ($ReviewData.HighSeverity -gt 0)           { $signals += "🔥$($ReviewData.HighSeverity) high-sev" }
    if ($daysSinceActivity -ge 30)                { $signals += "💤 $daysSinceActivity days stale" }

    # Tags from AI + computed
    $tags = @()
    if ($AiEval -and $AiEval.Tags) { $tags += $AiEval.Tags }
    if ($Pr.Additions + $Pr.Deletions -ge 500) { $tags += 'large-pr' }
    if ($ReviewData.HighSeverity -gt 0)         { $tags += 'review-high-severity' }
    if ($ReviewData.HasReview -and $ReviewData.TotalFindings -eq 0) { $tags += 'review-clean' }
    $tags = @($tags | Select-Object -Unique)

    return [PSCustomObject]@{
        Number               = $Pr.Number
        Title                = $Pr.Title
        Author               = $Pr.Author
        Url                  = $Pr.Url
        AgeInDays            = $age
        DaysSinceActivity    = $daysSinceActivity
        Category             = $CatResult.Category
        Confidence           = [Math]::Round($CatResult.Confidence, 2)
        CategorizationSource = $CatResult.Source
        Signals              = $signals
        Tags                 = $tags
        Effort               = $effort
        EffortLabel          = Get-EffortLabel $effort
        DimensionScores      = if ($AiEval) { $AiEval.Dimensions } else { $null }
        DiscussionSummary    = if ($AiEval) { $AiEval.DiscussionSummary } else { $null }
        SupersededBy         = if ($AiEval) { $AiEval.SupersededBy } else { $null }
        Labels               = $Pr.Labels
        LinkedIssues         = $Pr.LinkedIssues
        Additions            = $Pr.Additions
        Deletions            = $Pr.Deletions
        ChangedFiles         = $Pr.ChangedFiles
        ChecksStatus         = $Enrichment.ChecksStatus
        FailingChecks        = $Enrichment.FailingChecks
        ApprovalCount        = $Enrichment.ApprovalCount
        ChangesRequestedCount = $Enrichment.ChangesRequestedCount
        ReviewData           = [PSCustomObject]@{
            HasReview     = $ReviewData.HasReview
            Signal        = $ReviewData.Signal
            HighSeverity  = $ReviewData.HighSeverity
            MedSeverity   = $ReviewData.MedSeverity
            LowSeverity   = $ReviewData.LowSeverity
            TotalFindings = $ReviewData.TotalFindings
            FindingSummaries = $findingSummaries
        }
    }
}

#endregion

# ═════════════════════════════════════════════════════════════════════════
# Main — orchestrate enrichment and categorization
# ═════════════════════════════════════════════════════════════════════════

$inputData = Get-Content $InputPath -Raw | ConvertFrom-Json
$prs = if ($inputData.Prs) { $inputData.Prs } else { $inputData }

# Load AI evaluation results if available
$aiLookup = @{}
if ($AiEnrichmentPath -and (Test-Path $AiEnrichmentPath)) {
    $aiData = Get-Content $AiEnrichmentPath -Raw | ConvertFrom-Json
    foreach ($r in $aiData.Results) {
        $aiLookup[[int]$r.Number] = $r
    }
    Write-LogHost "Loaded $($aiLookup.Count) AI evaluation results" -ForegroundColor Cyan
}

Write-LogHost "Categorizing $($prs.Count) PRs (throttle: $ThrottleLimit)..." -ForegroundColor Cyan

# ── Parallel enrichment ─────────────────────────────────────────────────
# We enrich all PRs first (parallel GitHub API calls) then categorize.

$enriched = [System.Collections.Concurrent.ConcurrentDictionary[int, PSCustomObject]]::new()
$total = $prs.Count

$prs | ForEach-Object -ThrottleLimit $ThrottleLimit -Parallel {
    $pr = $_
    $n  = [int]$pr.Number
    $repo = $using:Repository
    $dict = $using:enriched

    # Inline enrichment (cannot call module functions from -Parallel)
    $owner, $repoName = $repo -split '/'

    $reviews = @()
    try { $reviews = gh api "repos/$repo/pulls/$n/reviews" --paginate 2>$null | ConvertFrom-Json } catch { }
    $approvals       = @($reviews | Where-Object { $_.state -eq 'APPROVED' }).Count
    $changesReq      = @($reviews | Where-Object { $_.state -eq 'CHANGES_REQUESTED' }).Count
    $reviewerLogins  = @($reviews | Where-Object { $_.state -in 'APPROVED','CHANGES_REQUESTED' } | ForEach-Object { $_.user.login } | Select-Object -Unique)

    $comments = @()
    try { $comments = gh api "repos/$repo/issues/$n/comments" --paginate 2>$null | ConvertFrom-Json } catch { }

    $lastCommentAt = $null; $authorLastAt = $null
    if ($comments.Count -gt 0) {
        $lastCommentAt = ($comments | Sort-Object created_at -Descending | Select-Object -First 1).created_at
        $ac = @($comments | Where-Object { $_.user.login -eq $pr.Author })
        if ($ac.Count -gt 0) { $authorLastAt = ($ac | Sort-Object created_at -Descending | Select-Object -First 1).created_at }
    }

    $checksStatus = 'UNKNOWN'; $failingChecks = @()
    try {
        $cj = gh api "repos/$repo/commits/$($pr.HeadRefName)/check-runs" 2>$null | ConvertFrom-Json
        if ($cj.check_runs) {
            $failed  = @($cj.check_runs | Where-Object { $_.conclusion -eq 'failure' })
            $pending = @($cj.check_runs | Where-Object { $_.status -ne 'completed' })
            $failingChecks = @($failed | ForEach-Object { $_.name })
            if ($failed.Count -gt 0) { $checksStatus = 'FAILURE' }
            elseif ($pending.Count -gt 0) { $checksStatus = 'PENDING' }
            else { $checksStatus = 'SUCCESS' }
        }
    } catch { }

    $lastCommitAt = $null
    try {
        $commits = gh api "repos/$repo/pulls/$n/commits" --paginate 2>$null | ConvertFrom-Json
        if ($commits.Count -gt 0) {
            $lastCommitAt = ($commits | Sort-Object { $_.commit.committer.date } -Descending | Select-Object -First 1).commit.committer.date
        }
    } catch { }

    $enrichObj = [PSCustomObject]@{
        ApprovalCount         = $approvals
        ChangesRequestedCount = $changesReq
        ReviewerLogins        = $reviewerLogins
        CommentCount          = $comments.Count
        ChecksStatus          = $checksStatus
        FailingChecks         = $failingChecks
        LastCommentAt         = $lastCommentAt
        LastCommitAt          = $lastCommitAt
        AuthorLastActivityAt  = $authorLastAt
    }

    $dict[$n] = $enrichObj
}

Write-LogHost "  Enriched $($enriched.Count)/$total PRs" -ForegroundColor Green

# ── Categorize each PR ──────────────────────────────────────────────────

$categorized = @()
$done = 0

foreach ($pr in $prs) {
    $n = [int]$pr.Number
    $done++

    $e = $enriched[$n]
    if (-not $e) {
        # Should not happen but handle gracefully
        $e = [PSCustomObject]@{
            ApprovalCount = 0; ChangesRequestedCount = 0; ReviewerLogins = @()
            CommentCount = 0; ChecksStatus = 'UNKNOWN'; FailingChecks = @()
            LastCommentAt = $null; LastCommitAt = $null; AuthorLastActivityAt = $null
        }
    }

    $reviewData = Get-ReviewFindings -PRNumber $n -ReviewRoot $resolvedReviewOutputRoot
    $aiEval = $aiLookup[$n]

    # Determine category: AI dimensions > AI suggestion > rules
    if ($aiEval -and $aiEval.Dimensions -and ($aiEval.Dimensions -is [hashtable] -and $aiEval.Dimensions.Count -gt 0 -or $aiEval.Dimensions.PSObject.Properties.Count -gt 0)) {
        # Convert PSCustomObject dimensions to hashtable if needed
        $dims = @{}
        if ($aiEval.Dimensions -is [hashtable]) {
            $dims = $aiEval.Dimensions
        } else {
            foreach ($prop in $aiEval.Dimensions.PSObject.Properties) {
                $dims[$prop.Name] = $prop.Value
            }
        }
        $catResult = Get-CategoryFromDimensions -Dims $dims -SuggestedCategory $aiEval.SuggestedCategory

        # Cross-check: enrichment hard facts override obvious AI misjudgments
        if ($catResult.Category -eq 'ready-to-merge' -and $e.ChecksStatus -eq 'FAILURE') {
            $catResult = @{ Category = 'build-failures'; Confidence = 0.9; Source = 'ai-corrected' }
        } elseif ($catResult.Category -eq 'ready-to-merge' -and $e.ChangesRequestedCount -gt 0) {
            $catResult = @{ Category = 'review-concerns'; Confidence = 0.7; Source = 'ai-corrected' }
        }
    } else {
        $catResult = Get-CategoryFromRules -Pr $pr -Enrichment $e
    }

    $catPr = New-CategorizedPr -Pr $pr -Enrichment $e -ReviewData $reviewData -CatResult $catResult -AiEval $aiEval
    $categorized += $catPr

    Write-LogHost "  [$done/$total] #$n → $($catResult.Category) ($($catResult.Source))" -ForegroundColor Gray
}

# ── Write output ────────────────────────────────────────────────────────

$output = [PSCustomObject]@{
    CategorizedAt  = (Get-Date).ToString('o')
    Repository     = $Repository
    TotalCount     = $categorized.Count
    CategoryCounts = @{}
    Prs            = $categorized
}

# Build category counts
$categorized | Group-Object Category | ForEach-Object {
    $output.CategoryCounts[$_.Name] = $_.Count
}

$output | ConvertTo-Json -Depth 10 | Set-Content $OutputPath -Encoding UTF8
Write-LogHost "Done: $($categorized.Count) PRs categorized → $OutputPath" -ForegroundColor Cyan
