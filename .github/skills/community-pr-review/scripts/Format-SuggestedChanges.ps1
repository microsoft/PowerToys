<#
.SYNOPSIS
    Generate GitHub suggested changes from the diff between original PR and fixed code.

.DESCRIPTION
    Compares the current worktree state (with fixes applied) against the original PR
    head commit. For each changed hunk, generates a GitHub review comment with a
    ```suggestion``` code block that the PR author can apply directly.

.PARAMETER PRNumber
    The PR number (required).

.PARAMETER OriginalSha
    The original HEAD SHA of the PR before fixes were applied.
    If not provided, reads from the .signal file.

.PARAMETER OutputDir
    Directory containing review outputs. Default: Generated Files/communityPrReview/<PR>

.PARAMETER MinContextLines
    Number of context lines around each change. Default: 3.

.EXAMPLE
    ./Format-SuggestedChanges.ps1 -PRNumber 45234 -OriginalSha abc123

.EXAMPLE
    ./Format-SuggestedChanges.ps1 -PRNumber 45234
#>
param(
    [int]$PRNumber,
    [string]$OriginalSha,
    [string]$OutputDir,
    [int]$MinContextLines = 3,
    [switch]$Help
)

$ErrorActionPreference = 'Stop'

if ($Help) {
    Get-Help $MyInvocation.MyCommand.Path -Full
    return
}

if (-not $PRNumber -or $PRNumber -eq 0) {
    Write-Error 'Format-SuggestedChanges: -PRNumber is required.'
    return
}

# Load helpers
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. "$scriptDir/ReviewLib.ps1"

$repoRoot = Get-RepoRoot

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot "Generated Files/communityPrReview/$PRNumber"
}

# Resolve original SHA from signal file if not provided
if ([string]::IsNullOrWhiteSpace($OriginalSha)) {
    $signalPath = Join-Path $OutputDir '.signal'
    if (Test-Path $signalPath) {
        $signal = Get-Content $signalPath -Raw | ConvertFrom-Json
        $OriginalSha = $signal.originalHeadSha
    }
    if ([string]::IsNullOrWhiteSpace($OriginalSha)) {
        Write-Error 'Format-SuggestedChanges: -OriginalSha is required (or must be in .signal file).'
        return
    }
}

Info "Generating suggested changes for PR #$PRNumber"
Info "Original SHA: $OriginalSha"
Info "Current HEAD: $(git rev-parse HEAD)"

# Get the diff between original PR and current state
$diffOutput = git diff $OriginalSha HEAD --unified=$MinContextLines --no-color 2>&1
if (-not $diffOutput) {
    Info "No changes between original PR and current state."
    $noChangesReport = @"
# Suggested Changes — PR #$PRNumber

No code changes were needed. The review found no high/medium issues requiring fixes.
"@
    $noChangesReport | Set-Content (Join-Path $OutputDir 'suggested-changes.md') -Force
    return
}

# Parse the unified diff into per-file, per-hunk suggestions
$suggestions = [System.Collections.Generic.List[hashtable]]::new()
$currentFile = $null
$currentHunk = $null
$oldLines = @()
$newLines = @()
$hunkStartLine = 0

function Flush-Hunk {
    if ($null -eq $script:currentFile -or $null -eq $script:currentHunk) { return }
    if (($script:oldLines.Count -eq 0) -and ($script:newLines.Count -eq 0)) { return }

    # Build the suggestion text (what the new code should be)
    $suggestionText = ($script:newLines -join "`n")

    $script:suggestions.Add(@{
        File        = $script:currentFile
        StartLine   = $script:hunkStartLine
        EndLine     = $script:hunkStartLine + $script:oldLines.Count - 1
        OldCode     = ($script:oldLines -join "`n")
        NewCode     = $suggestionText
        LineCount   = $script:oldLines.Count
    })

    $script:oldLines = @()
    $script:newLines = @()
}

foreach ($line in ($diffOutput -split "`n")) {
    if ($line -match '^diff --git a/(.+) b/(.+)$') {
        Flush-Hunk
        $currentFile = $Matches[2]
        $currentHunk = $null
        continue
    }

    if ($line -match '^@@ -(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@') {
        Flush-Hunk
        $currentHunk = $true
        $hunkStartLine = [int]$Matches[2]
        $oldLines = @()
        $newLines = @()
        $lineCounter = [int]$Matches[2]
        continue
    }

    if ($null -eq $currentHunk) { continue }

    # Track context and changes within a hunk
    if ($line.StartsWith('+')) {
        # Added line (new code)
        $newLines += $line.Substring(1)
    }
    elseif ($line.StartsWith('-')) {
        # Removed line (old code)
        if ($oldLines.Count -eq 0) {
            $hunkStartLine = $lineCounter
        }
        $oldLines += $line.Substring(1)
    }
    else {
        # Context line — if we have accumulated changes, flush them
        if ($oldLines.Count -gt 0 -or $newLines.Count -gt 0) {
            Flush-Hunk
        }
        $lineCounter++
        # Context lines are part of both old and new
        $newLines = @()
        $oldLines = @()
        continue
    }
}
Flush-Hunk

# Generate the suggested-changes.md
$output = [System.Text.StringBuilder]::new()
[void]$output.AppendLine("# Suggested Changes — PR #$PRNumber")
[void]$output.AppendLine("")
[void]$output.AppendLine("These changes address review findings. Each suggestion can be applied directly on GitHub.")
[void]$output.AppendLine("")
[void]$output.AppendLine("## Summary")
[void]$output.AppendLine("- **Total suggestions**: $($suggestions.Count)")
$fileGroups = $suggestions | Group-Object { $_.File }
[void]$output.AppendLine("- **Files affected**: $($fileGroups.Count)")
[void]$output.AppendLine("")

$suggestionNum = 0
foreach ($group in $fileGroups) {
    [void]$output.AppendLine("---")
    [void]$output.AppendLine("")
    [void]$output.AppendLine("## ``$($group.Name)``")
    [void]$output.AppendLine("")

    foreach ($suggestion in $group.Group) {
        $suggestionNum++
        $lineRange = if ($suggestion.StartLine -eq $suggestion.EndLine) {
            "line $($suggestion.StartLine)"
        } else {
            "lines $($suggestion.StartLine)-$($suggestion.EndLine)"
        }

        [void]$output.AppendLine("### Suggestion $suggestionNum ($lineRange)")
        [void]$output.AppendLine("")
        [void]$output.AppendLine("``````suggestion")
        [void]$output.AppendLine($suggestion.NewCode)
        [void]$output.AppendLine("``````")
        [void]$output.AppendLine("")
    }
}

# Also generate a machine-readable JSON for posting via API
$jsonSuggestions = $suggestions | ForEach-Object {
    @{
        path       = $_.File
        start_line = $_.StartLine
        end_line   = $_.EndLine
        body       = "``````suggestion`n$($_.NewCode)`n``````"
    }
}

$output.ToString() | Set-Content (Join-Path $OutputDir 'suggested-changes.md') -Force
$jsonSuggestions | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $OutputDir 'suggested-changes.json') -Force

Success "Generated $($suggestions.Count) suggestions across $($fileGroups.Count) files."
Info "Output: $(Join-Path $OutputDir 'suggested-changes.md')"
Info "JSON:   $(Join-Path $OutputDir 'suggested-changes.json')"

return @{
    SuggestionCount = $suggestions.Count
    FileCount       = $fileGroups.Count
    Suggestions     = $suggestions
}
