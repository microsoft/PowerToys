<#
.SYNOPSIS
    Evaluates PRs using Copilot/Claude CLI via the parallel-job-orchestrator.
    Returns dimension scores per PR; category derivation happens in Step 4.

.DESCRIPTION
    For each PR, builds a categorization prompt, delegates execution to the
    parallel-job-orchestrator skill, and parses structured JSON results from
    each job's output file.  Completed PR results are cached on disk (resumable).

    DO NOT add [CmdletBinding()], [Parameter(Mandatory)], or [ValidateSet()]
    here — those attributes make the script "advanced" which propagates
    ErrorActionPreference and can crash the orchestrator's monitoring loop.

.PARAMETER InputPath
    Path to JSON with PR data (from Get-OpenPrs.ps1).
.PARAMETER OutputPath
    Where to save evaluation results.  Default: ai-enrichment.json
.PARAMETER Repository
    GitHub repo in owner/repo format.  Default: microsoft/PowerToys
.PARAMETER MaxConcurrent
    Maximum parallel AI CLI jobs.  Default: 3.
.PARAMETER InactivityTimeoutSeconds
    Kill CLI if log doesn't grow for this many seconds.  Default: 120.
.PARAMETER MaxRetryCount
    Retry attempts after inactivity kill.  Default: 2.
.PARAMETER TimeoutMin
    Legacy param — converted to InactivityTimeoutSeconds if set.  Default: 5.
.PARAMETER Force
    Re-evaluate PRs that already have results.
#>
param(
    [string]$InputPath,
    [string]$OutputPath = 'ai-enrichment.json',
    [string]$Repository = 'microsoft/PowerToys',
    [int]$MaxConcurrent = 20,
    [int]$InactivityTimeoutSeconds = 120,
    [int]$MaxRetryCount = 2,
    [int]$TimeoutMin = 5,
    [string]$CLIType = 'copilot',
    [string]$OutputRoot,
    [string]$ReviewOutputRoot = 'Generated Files/prReview',
    [string]$LogPath,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

# Manual validation
if (-not $InputPath -or -not (Test-Path $InputPath)) {
    Write-Error "Invoke-AiEnrichment: -InputPath is required and must exist. Got: '$InputPath'"
    return
}
if ($CLIType -notin 'copilot', 'claude') {
    Write-Error "Invoke-AiEnrichment: Invalid -CLIType '$CLIType'. Must be 'copilot' or 'claude'."
    return
}

# ── logging ──────────────────────────────────────────────────────────────

if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Join-Path (Get-Location) 'Invoke-AiEnrichment.log'
}

$logDir = Split-Path -Parent $LogPath
if (-not [string]::IsNullOrWhiteSpace($logDir) -and -not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}

"[$(Get-Date -Format o)] Starting Invoke-AiEnrichment" | Out-File -FilePath $LogPath -Encoding utf8 -Append

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

# ── Resolve paths ────────────────────────────────────────────────────────
$repoRoot = git rev-parse --show-toplevel 2>$null
if (-not $repoRoot) { $repoRoot = (Get-Location).Path }

$scriptDir          = Split-Path -Parent $MyInvocation.MyCommand.Path
$promptTemplatePath = Join-Path (Split-Path $scriptDir) 'references' 'categorize-pr.prompt.md'
$mcpConfigPath      = Join-Path $repoRoot '.github' 'skills' 'pr-review' 'references' 'mcp-config.json'
$tmpDir             = if ($OutputRoot) { Join-Path $OutputRoot '__tmp' } else { Join-Path $repoRoot 'Generated Files' 'pr-triage' '__tmp' }
$reviewRoot         = if ([System.IO.Path]::IsPathRooted($ReviewOutputRoot)) { $ReviewOutputRoot } else { Join-Path $repoRoot $ReviewOutputRoot }

if (-not (Test-Path $promptTemplatePath)) { throw "Prompt template not found: $promptTemplatePath" }
if (-not (Test-Path $mcpConfigPath))      { throw "MCP config not found: $mcpConfigPath" }
if (-not (Test-Path $tmpDir))             { New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null }

$promptTemplate = Get-Content $promptTemplatePath -Raw
$promptTemplate = $promptTemplate -replace '^\s*```prompt\s*', ''
$promptTemplate = $promptTemplate -replace '\s*```\s*$', ''

$ClaudeEnrichmentJsonSchema = '{"type":"object","properties":{"dimensions":{"type":"object","properties":{"review_sentiment":{"type":"object","properties":{"score":{"type":"number"},"confidence":{"type":"number"},"reasoning":{"type":"string"}},"required":["score","confidence","reasoning"]},"author_responsiveness":{"type":"object","properties":{"score":{"type":"number"},"confidence":{"type":"number"},"reasoning":{"type":"string"}},"required":["score","confidence","reasoning"]},"code_health":{"type":"object","properties":{"score":{"type":"number"},"confidence":{"type":"number"},"reasoning":{"type":"string"}},"required":["score","confidence","reasoning"]},"merge_readiness":{"type":"object","properties":{"score":{"type":"number"},"confidence":{"type":"number"},"reasoning":{"type":"string"}},"required":["score","confidence","reasoning"]},"activity_level":{"type":"object","properties":{"score":{"type":"number"},"confidence":{"type":"number"},"reasoning":{"type":"string"}},"required":["score","confidence","reasoning"]},"direction_clarity":{"type":"object","properties":{"score":{"type":"number"},"confidence":{"type":"number"},"reasoning":{"type":"string"}},"required":["score","confidence","reasoning"]},"superseded":{"type":"object","properties":{"score":{"type":"number"},"confidence":{"type":"number"},"reasoning":{"type":"string"}},"required":["score","confidence","reasoning"]}},"required":["review_sentiment","author_responsiveness","code_health","merge_readiness","activity_level","direction_clarity","superseded"]},"suggested_category":{"type":"string"},"discussion_summary":{"type":"string"},"superseded_by":{"type":["string","null"]},"tags":{"type":"array","items":{"type":"string"}}},"required":["dimensions","suggested_category","discussion_summary","superseded_by","tags"]}'

# ── Resolve real copilot binary (skip the .ps1 bootstrapper) ─────────────
function Get-CopilotExecutablePath {
    $copilotCmd = Get-Command copilot -ErrorAction SilentlyContinue
    if (-not $copilotCmd) { return 'copilot' }

    if ($copilotCmd.Source -match '\.ps1$') {
        $bootstrapDir = Split-Path $copilotCmd.Source -Parent
        $savedPath    = $env:PATH
        $env:PATH     = ($env:PATH -split ';' | Where-Object { $_ -ne $bootstrapDir }) -join ';'
        $realCmd      = Get-Command copilot -ErrorAction SilentlyContinue
        $env:PATH     = $savedPath
        if ($realCmd) { return $realCmd.Source }
    }

    return $copilotCmd.Source
}

$CopilotExe = if ($CLIType -eq 'copilot') { Get-CopilotExecutablePath } else { $null }

# ── Dimension names — single source of truth ─────────────────────────────
$DimensionNames = @(
    'review_sentiment'
    'author_responsiveness'
    'code_health'
    'merge_readiness'
    'activity_level'
    'direction_clarity'
    'superseded'
)

# ═════════════════════════════════════════════════════════════════════════
# Pure functions — return objects, no Write-Host
# ═════════════════════════════════════════════════════════════════════════

function Get-AiReviewSummary ([int]$PRNumber) {
    $reviewDir = Join-Path $reviewRoot $PRNumber.ToString()
    if (-not (Test-Path $reviewDir)) { return 'No AI code review available for this PR.' }

    $overviewPath = Join-Path $reviewDir '00-OVERVIEW.md'
    if (-not (Test-Path $overviewPath)) { return 'AI review directory exists but no overview found.' }

    $overview = Get-Content $overviewPath -Raw
    $findings = @()
    Get-ChildItem -Path $reviewDir -Filter '*.md' -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^\d{2}-' } | ForEach-Object {
        $content = Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue
        if ($content -match 'mcp-review-comment') {
            [regex]::Matches($content, '(?s)```mcp-review-comment\s*\n(.*?)```') | ForEach-Object {
                try {
                    $json = $_.Groups[1].Value | ConvertFrom-Json -ErrorAction SilentlyContinue
                    if ($json.severity) { $findings += "- [$($json.severity.ToUpper())] $($json.body)" }
                } catch { }
            }
        }
    }

    return "**Overview:**`n$overview`n`n**Findings ($($findings.Count) total):**`n$($findings -join "`n")"
}

function Build-PrPrompt ([PSCustomObject]$Pr) {
    $e = $Pr.Enrichment
    $now       = Get-Date
    $createdAt = [DateTime]::Parse($Pr.CreatedAt)
    $ageInDays = [Math]::Floor(($now - $createdAt).TotalDays)

    $timestamps = @($Pr.UpdatedAt)
    if ($e) { $timestamps += $e.LastCommentAt, $e.LastCommitAt }
    $lastActivity = $timestamps |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        ForEach-Object { [DateTime]::Parse($_) } |
        Sort-Object -Descending | Select-Object -First 1
    $daysSinceActivity = if ($lastActivity) { [Math]::Floor(($now - $lastActivity).TotalDays) } else { $ageInDays }

    $authorLastStr = if ($e) { $e.AuthorLastActivityAt } else { $null }
    $authorLast = if ($authorLastStr) { [DateTime]::Parse($authorLastStr) } else { $null }
    $daysSinceAuthor = if ($authorLast) { [Math]::Floor(($now - $authorLast).TotalDays) } else { $ageInDays }

    $replacements = @{
        '{{PR_NUMBER}}'                  = $Pr.Number
        '{{PR_TITLE}}'                   = ($Pr.Title -replace '[{}]', '')
        '{{PR_AUTHOR}}'                  = $Pr.Author
        '{{PR_URL}}'                     = $Pr.Url
        '{{AGE_DAYS}}'                   = $ageInDays
        '{{DAYS_SINCE_ACTIVITY}}'        = $daysSinceActivity
        '{{DAYS_SINCE_AUTHOR_ACTIVITY}}' = $daysSinceAuthor
        '{{ADDITIONS}}'                  = $Pr.Additions
        '{{DELETIONS}}'                  = $Pr.Deletions
        '{{CHANGED_FILES}}'              = $Pr.ChangedFiles
        '{{LABELS}}'                     = $(if ($Pr.Labels) { $Pr.Labels -join ', ' } else { '(none)' })
        '{{LINKED_ISSUES}}'              = $(if ($Pr.LinkedIssues) { $Pr.LinkedIssues -join ', ' } else { '(none)' })
        '{{IS_DRAFT}}'                   = $Pr.IsDraft
        '{{APPROVAL_COUNT}}'             = $(if ($e) { $e.ApprovalCount } else { 'UNKNOWN' })
        '{{CHANGES_REQUESTED_COUNT}}'    = $(if ($e) { $e.ChangesRequestedCount } else { 'UNKNOWN' })
        '{{CHECKS_STATUS}}'              = $(if ($e -and $e.ChecksStatus) { $e.ChecksStatus } else { 'UNKNOWN' })
        '{{FAILING_CHECKS}}'             = $(if ($e -and $e.FailingChecks) { $e.FailingChecks -join ', ' } else { '(none)' })
        '{{MERGEABLE}}'                  = $(if ($Pr.Mergeable) { $Pr.Mergeable } else { 'UNKNOWN' })
        '{{AI_REVIEW_SUMMARY}}'          = (Get-AiReviewSummary -PRNumber ([int]$Pr.Number))
        '{{EXTRACT_FOLDER}}'             = ((Join-Path $tmpDir "pr-$($Pr.Number)") -replace '\\', '/')
    }

    $prompt = $promptTemplate
    foreach ($kv in $replacements.GetEnumerator()) {
        $prompt = $prompt -replace [regex]::Escape($kv.Key), $kv.Value
    }
    return $prompt
}

function ConvertFrom-AiResponse ([string]$RawOutput) {
    if ([string]::IsNullOrWhiteSpace($RawOutput)) { return $null }

    try {
        $root = $RawOutput | ConvertFrom-Json -ErrorAction Stop
        if ($root.dimensions) {
            # Direct dimensions object (e.g. raw JSON response)
            $RawOutput = ($root | ConvertTo-Json -Depth 20)
        } elseif ($root.structured_output -and $root.structured_output.dimensions) {
            # Claude CLI --output-format json --json-schema puts the result
            # in "structured_output", not "result".
            $RawOutput = ($root.structured_output | ConvertTo-Json -Depth 20)
        } elseif ($root.result -and ($root.result -is [string]) -and $root.result.Length -gt 0) {
            $RawOutput = [string]$root.result
        } elseif ($root.content -and $root.content.Count -gt 0) {
            $textParts = @()
            foreach ($c in $root.content) {
                if ($c.text) { $textParts += [string]$c.text }
            }
            if ($textParts.Count -gt 0) { $RawOutput = ($textParts -join "`n") }
        }
    } catch { }

    $jsonMatch = [regex]::Match($RawOutput, '(?s)```json?\s*\n(\{.*\})\s*\n```')
    if ($jsonMatch.Success) { $jsonText = $jsonMatch.Groups[1].Value }
    else {
        $jsonMatch = [regex]::Match($RawOutput, '(?s)(\{[^{}]*"dimensions"\s*:\s*\{.*\})')
        if (-not $jsonMatch.Success) { return $null }
        $jsonText = $jsonMatch.Value
    }

    try {
        $parsed = $jsonText | ConvertFrom-Json -ErrorAction Stop
        if (-not $parsed.dimensions) { return $null }

        $dims = @{}
        foreach ($name in $DimensionNames) {
            $d = $parsed.dimensions.$name
            if ($d -and $null -ne $d.score) {
                $dims[$name] = [PSCustomObject]@{
                    Score      = [Math]::Round([double]$d.score, 2)
                    Confidence = if ($d.confidence) { [Math]::Round([double]$d.confidence, 2) } else { 0.5 }
                    Reasoning  = if ($d.reasoning)  { [string]$d.reasoning } else { '' }
                }
            }
        }
        if ($dims.Count -eq 0) { return $null }

        return [PSCustomObject]@{
            Dimensions        = $dims
            SuggestedCategory = $parsed.suggested_category
            DiscussionSummary = $parsed.discussion_summary
            SupersededBy      = $parsed.superseded_by
            Tags              = @($parsed.tags)
        }
    } catch { return $null }
}

function New-EvalResult ([int]$Number, $AiResult, [string]$Source) {
    return [PSCustomObject]@{
        Number            = $Number
        Dimensions        = if ($AiResult) { $AiResult.Dimensions }        else { @{} }
        SuggestedCategory = if ($AiResult) { $AiResult.SuggestedCategory } else { $null }
        Tags              = if ($AiResult) { $AiResult.Tags }              else { @() }
        DiscussionSummary = if ($AiResult) { $AiResult.DiscussionSummary } else { $null }
        SupersededBy      = if ($AiResult) { $AiResult.SupersededBy }      else { $null }
        Source            = $Source
    }
}

function New-FallbackAiResult ([PSCustomObject]$Pr, [string]$Reason) {
    $normalizedReason = if ([string]::IsNullOrWhiteSpace($Reason)) { 'AI output not parseable' } else { $Reason }
    $dims = @{}
    foreach ($name in $DimensionNames) {
        $dims[$name] = [PSCustomObject]@{
            Score      = 0.5
            Confidence = 0.2
            Reasoning  = "Fallback value: $normalizedReason"
        }
    }

    return [PSCustomObject]@{
        Dimensions        = $dims
        SuggestedCategory = $null
        DiscussionSummary = "Fallback enrichment used for PR #$($Pr.Number): $normalizedReason"
        SupersededBy      = $null
        Tags              = @('ai-fallback')
    }
}

function New-EvalOutput ([hashtable]$Results, [string]$RepoName) {
    return [PSCustomObject]@{
        CategorizedAt   = (Get-Date).ToString('o')
        Repository      = $RepoName
        AiEngine        = $CLIType
        TotalCount      = $Results.Count
        AiSuccessCount  = @($Results.Values | Where-Object { $_.Source -eq 'ai' }).Count
        AiFallbackCount = @($Results.Values | Where-Object { $_.Source -eq 'fallback' }).Count
        AiFailedCount   = @($Results.Values | Where-Object { $_.Source -eq 'failed' }).Count
        Results         = @($Results.Values | Sort-Object Number)
    }
}

# ═════════════════════════════════════════════════════════════════════════
# Main — build job definitions → orchestrator → parse results
# ═════════════════════════════════════════════════════════════════════════

$inputData = Get-Content $InputPath -Raw | ConvertFrom-Json
$prs = if ($inputData.Prs) { $inputData.Prs } else { $inputData }

# Resume: load existing
$allResults = @{}
if (-not $Force -and (Test-Path $OutputPath)) {
    try {
        (Get-Content $OutputPath -Raw | ConvertFrom-Json).Results |
            ForEach-Object { $allResults[[int]$_.Number] = $_ }
        Write-LogHost "  Resumed: $($allResults.Count) existing results" -ForegroundColor DarkGray
    } catch { }
}

$prsToProcess = @($prs | Where-Object { $Force -or -not $allResults.ContainsKey([int]$_.Number) })
Write-LogHost "AI Evaluation: $($prs.Count) total, $($prsToProcess.Count) to process, $($prs.Count - $prsToProcess.Count) skipped" -ForegroundColor Cyan

if ($prsToProcess.Count -eq 0) {
    Write-LogHost '  Nothing to do' -ForegroundColor Green
    return
}

# Build all prompts upfront
$promptMap = @{}
foreach ($pr in $prsToProcess) { $promptMap[[int]$pr.Number] = Build-PrPrompt -Pr $pr }

# ── Build orchestrator job definitions ──────────────────────────────────

$jobDefs = @(foreach ($pr in $prsToProcess) {
    $n = [int]$pr.Number
    $prompt = $promptMap[$n]
    $flatPrompt = ($prompt -replace "[\r\n]+", ' ').Trim()

    $prOutputDir = Join-Path $tmpDir "enrich-$n"
    New-Item -ItemType Directory -Path $prOutputDir -Force | Out-Null

    if ($CLIType -eq 'copilot') {
        $logFile = Join-Path $prOutputDir "_copilot-enrich.log"
        $cliArgs = @(
            '--additional-mcp-config', "@$mcpConfigPath",
            '-p', $flatPrompt,
            '--yolo', '--no-custom-instructions', '-s', '--agent', 'TriagePR'
        )

        @{
            Label               = "enrich-pr-$n"
            ExecutionParameters = @{
                JobName    = "enrich-pr-$n"
                Command    = $CopilotExe
                Arguments  = $cliArgs
                WorkingDir = $repoRoot
                OutputDir  = $prOutputDir
                LogPath    = $logFile
            }
            MonitorFiles = @($logFile)
            CleanupTask  = $null
        }
    }
    else {
        $debugFile = Join-Path $prOutputDir "_claude-debug.log"
        $logFile   = Join-Path $prOutputDir "_claude-enrich.log"
        $cliArgs = @(
            '-p', $flatPrompt,
            '--dangerously-skip-permissions',
            '--output-format', 'json',
            '--json-schema', $ClaudeEnrichmentJsonSchema,
            '--debug', 'all', '--debug-file', $debugFile,
            '--agent', 'TriagePR'
        )

        @{
            Label               = "enrich-pr-$n"
            ExecutionParameters = @{
                JobName    = "enrich-pr-$n"
                Command    = 'claude'
                Arguments  = $cliArgs
                WorkingDir = $repoRoot
                OutputDir  = $prOutputDir
                LogPath    = $logFile
            }
            MonitorFiles = @($debugFile)
            CleanupTask  = {
                param($Tracker)
                $outDir = $Tracker.ExecutionParameters.OutputDir
                $dbg = Join-Path $outDir '_claude-debug.log'
                if (Test-Path $dbg) {
                    $fi = [System.IO.FileInfo]::new($dbg)
                    if ($fi.Length -gt 0) {
                        $sizeMB = [math]::Round($fi.Length / 1MB, 1)
                        Remove-Item $dbg -Force
                        Write-Host "[$($Tracker.Label)] Cleaned debug log (${sizeMB} MB)"
                    }
                }
                # Claude CLI auto-creates a 0-byte 'latest' marker file — remove it.
                $latest = Join-Path $outDir 'latest'
                if (Test-Path $latest) { Remove-Item $latest -Force }
            }
        }
    }
})

Write-LogHost "`nBuilt $($jobDefs.Count) enrichment job(s)" -ForegroundColor Cyan
$jobDefs | ForEach-Object { Write-LogHost "  $($_.Label)" -ForegroundColor Gray }

# ── Run orchestrator ────────────────────────────────────────────────────

$orchestratorPath = Join-Path $scriptDir '..\..\parallel-job-orchestrator\scripts\Invoke-SimpleJobOrchestrator.ps1'
if (-not (Test-Path $orchestratorPath)) {
    throw "Orchestrator not found: $orchestratorPath"
}

$savedEAP = $ErrorActionPreference
$ErrorActionPreference = 'Continue'

$orchResults = & $orchestratorPath `
    -JobDefinitions $jobDefs `
    -MaxConcurrent $MaxConcurrent `
    -InactivityTimeoutSeconds $InactivityTimeoutSeconds `
    -MaxRetryCount $MaxRetryCount `
    -PollIntervalSeconds 5 `
    -LogDir $tmpDir

$ErrorActionPreference = $savedEAP

# ── Parse results from output files ────────────────────────────────────

$prLookup = @{}
foreach ($pr in $prsToProcess) { $prLookup[[int]$pr.Number] = $pr }

foreach ($r in $orchResults) {
    # Extract PR number from label (e.g. "enrich-pr-45601" → 45601)
    if ($r.Label -notmatch '(\d+)$') { continue }
    $n = [int]$Matches[1]
    $pr = $prLookup[$n]
    if (-not $pr) { continue }

    $prOutputDir = Join-Path $tmpDir "enrich-$n"

    if ($r.Status -eq 'Completed') {
        # Read the CLI output from the log file
        $logFile = $r.LogPath
        $outputFile = Join-Path $tmpDir "cat-output-$n.txt"

        $raw = $null
        if ($logFile -and (Test-Path $logFile)) {
            $raw = Get-Content $logFile -Raw -ErrorAction SilentlyContinue
        }

        if ($raw) {
            $raw | Set-Content $outputFile -Encoding UTF8 -ErrorAction SilentlyContinue
        }

        $parsed = if ($raw) { ConvertFrom-AiResponse -RawOutput $raw } else { $null }

        if ($parsed) {
            $allResults[$n] = New-EvalResult -Number $n -AiResult $parsed -Source 'ai'
            $scores = ($DimensionNames | ForEach-Object {
                $d = $parsed.Dimensions[$_]; if ($d) { "$($_.Substring(0,4))=$($d.Score)" }
            }) -join ' '
            Write-LogHost "  ✓ #$n $scores" -ForegroundColor Green
        }
        else {
            $fallback = New-FallbackAiResult -Pr $pr -Reason 'CLI completed but no parseable AI response'
            $allResults[$n] = New-EvalResult -Number $n -AiResult $fallback -Source 'fallback'
            Write-LogHost "  ⚠ #$n FALLBACK: no parseable response" -ForegroundColor Yellow
        }
    }
    else {
        $fallback = New-FallbackAiResult -Pr $pr -Reason "Job status: $($r.Status)"
        $allResults[$n] = New-EvalResult -Number $n -AiResult $fallback -Source 'fallback'
        Write-LogHost "  ⚠ #$n FALLBACK: $($r.Status)" -ForegroundColor Yellow
    }
}

# Final save
$output = New-EvalOutput -Results $allResults -RepoName $Repository
$output | ConvertTo-Json -Depth 10 | Set-Content $OutputPath -Encoding UTF8
Write-LogHost "Done: $($output.AiSuccessCount) succeeded, $($output.AiFallbackCount) fallback, $($output.AiFailedCount) failed → $OutputPath" -ForegroundColor Cyan

# Cleanup temp
Get-ChildItem $tmpDir -File -Filter 'cat-*' -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
Get-ChildItem $tmpDir -Directory -Filter 'enrich-*' -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
Get-ChildItem $tmpDir -Directory -Filter 'pr-*' -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
