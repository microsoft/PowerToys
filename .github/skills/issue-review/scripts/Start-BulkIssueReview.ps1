<#!
.SYNOPSIS
    Bulk review GitHub issues using AI CLI (Claude Code or GitHub Copilot).

.DESCRIPTION
    Queries GitHub issues by labels, state, and sort order, then kicks off parallel
    AI-powered reviews for each issue. Results are stored in Generated Files/issueReview/<number>/.

.PARAMETER Labels
    Comma-separated list of labels to filter issues (e.g., "bug,help wanted").

.PARAMETER State
    Issue state: open, closed, or all. Default: open.

.PARAMETER Sort
    Sort field: created, updated, comments, reactions. Default: created.

.PARAMETER Order
    Sort order: asc or desc. Default: desc.

.PARAMETER Limit
    Maximum number of issues to process. Default: 100.

.PARAMETER MaxParallel
    Maximum parallel review jobs. Default: 20.

.PARAMETER CLIType
    AI CLI to use: claude, gh-copilot, or vscode. Auto-detected if not specified.

.PARAMETER DryRun
    List issues without starting reviews.

.PARAMETER SkipExisting
    Skip issues that already have review files.

.PARAMETER Repository
    Repository in owner/repo format. Default: microsoft/PowerToys.

.PARAMETER TimeoutMinutes
    Timeout per issue review in minutes. Default: 30.

.EXAMPLE
    # Review all open bugs sorted by reactions
    ./Start-BulkIssueReview.ps1 -Labels "bug" -Sort reactions -Order desc

.EXAMPLE
    # Dry run to see which issues would be reviewed
    ./Start-BulkIssueReview.ps1 -Labels "help wanted" -DryRun

.EXAMPLE
    # Review top 50 issues with Claude Code, max 10 parallel
    ./Start-BulkIssueReview.ps1 -Labels "Issue-Bug" -Limit 50 -MaxParallel 10 -CLIType claude

.EXAMPLE
    # Skip already-reviewed issues
    ./Start-BulkIssueReview.ps1 -Labels "Issue-Feature" -SkipExisting

.NOTES
    Requires: GitHub CLI (gh) authenticated, and either Claude Code CLI or VS Code with Copilot.
    Results: Generated Files/issueReview/<issue_number>/overview.md and implementation-plan.md
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Labels,
    
    [ValidateSet('open', 'closed', 'all')]
    [string]$State = 'open',
    
    [ValidateSet('created', 'updated', 'comments', 'reactions')]
    [string]$Sort = 'created',
    
    [ValidateSet('asc', 'desc')]
    [string]$Order = 'desc',
    
    [int]$Limit = 1000,
    
    [int]$MaxParallel = 20,
    
    [ValidateSet('claude', 'copilot', 'gh-copilot', 'vscode', 'auto')]
    [string]$CLIType = 'auto',
    
    [switch]$DryRun,
    
    [switch]$SkipExisting,
    
    [string]$Repository = 'microsoft/PowerToys',
    
    [int]$TimeoutMinutes = 30,
    
    [int]$MaxRetries = 2,
    
    [int]$RetryDelaySeconds = 10,
    
    [switch]$Force,
    
    [switch]$Help
)

# Load library
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. "$scriptDir/IssueReviewLib.ps1"

# Show help
if ($Help) {
    Get-Help $MyInvocation.MyCommand.Path -Full
    return
}

#region Main Script
try {
    # Get repo root
    $repoRoot = Get-RepoRoot
    Info "Repository root: $repoRoot"

    # Detect or validate CLI
    if ($CLIType -eq 'auto') {
        $cli = Get-AvailableCLI
        if (-not $cli) {
            throw "No AI CLI found. Please install Claude Code CLI or GitHub Copilot CLI extension."
        }
        $CLIType = $cli.Type
        Info "Auto-detected CLI: $($cli.Name)"
    }

    # Query issues
    Info "`nQuerying issues with filters:"
    Info "  Labels: $(if ($Labels) { $Labels } else { '(none)' })"
    Info "  State: $State"
    Info "  Sort: $Sort $Order"
    Info "  Limit: $Limit"
    
    $issues = Get-GitHubIssues -Labels $Labels -State $State -Sort $Sort -Order $Order -Limit $Limit -Repository $Repository

    if ($issues.Count -eq 0) {
        Warn "No issues found matching the criteria."
        return
    }

    Info "`nFound $($issues.Count) issues"

    # Filter out existing reviews if requested
    if ($SkipExisting) {
        $originalCount = $issues.Count
        $issues = $issues | Where-Object {
            $result = Get-IssueReviewResult -IssueNumber $_.number -RepoRoot $repoRoot
            -not ($result.HasOverview -and $result.HasImplementationPlan)
        }
        $skipped = $originalCount - $issues.Count
        if ($skipped -gt 0) {
            Info "Skipping $skipped issues with existing reviews"
        }
    }

    if ($issues.Count -eq 0) {
        Warn "All issues already have reviews. Nothing to do."
        return
    }

    # Display issue list
    Info "`nIssues to review:"
    Info ("-" * 80)
    foreach ($issue in $issues) {
        $labels = ($issue.labels | ForEach-Object { $_.name }) -join ', '
        $reactions = if ($issue.reactions) { $issue.reactions.totalCount } else { 0 }
        Info ("#{0,-6} {1,-50} [👍{2}] [{3}]" -f $issue.number, ($issue.title.Substring(0, [Math]::Min(50, $issue.title.Length))), $reactions, $labels)
    }
    Info ("-" * 80)

    if ($DryRun) {
        Warn "`nDry run mode - no reviews started."
        Info "Would review $($issues.Count) issues with CLI: $CLIType"
        return
    }

    # Confirm before proceeding (skip if -Force)
    if (-not $Force) {
        $confirm = Read-Host "`nProceed with reviewing $($issues.Count) issues using $CLIType? (y/N)"
        if ($confirm -notmatch '^[yY]') {
            Info "Cancelled."
            return
        }
    } else {
        Info "`nProceeding with $($issues.Count) issues (Force mode)"
    }

    # Create output directory
    $genFiles = Get-GeneratedFilesPath -RepoRoot $repoRoot
    Ensure-DirectoryExists -Path (Join-Path $genFiles 'issueReview')

    # Start parallel reviews
    Info "`nStarting bulk review..."
    Info "  Max retries: $MaxRetries (delay: ${RetryDelaySeconds}s)"
    $startTime = Get-Date

    $results = Start-ParallelIssueReviews `
        -Issues $issues `
        -MaxParallel $MaxParallel `
        -CLIType $CLIType `
        -RepoRoot $repoRoot `
        -TimeoutMinutes $TimeoutMinutes `
        -MaxRetries $MaxRetries `
        -RetryDelaySeconds $RetryDelaySeconds

    $duration = (Get-Date) - $startTime

    # Summary
    Info "`n" + ("=" * 80)
    Info "BULK REVIEW COMPLETE"
    Info ("=" * 80)
    Info "Total issues:    $($results.Total)"
    Success "Succeeded:       $($results.Succeeded.Count)"
    if ($results.Failed.Count -gt 0) {
        Err "Failed:          $($results.Failed.Count)"
        Err "Failed issues:   $($results.Failed -join ', ')"
        Info ""
        Info "Failed Issue Details:"
        Info ("-" * 40)
        foreach ($failedItem in $results.FailedDetails) {
            Err "  #$($failedItem.IssueNumber) (attempts: $($failedItem.Attempts)):"
            $errorLines = ($failedItem.Error -split "`n" | Select-Object -First 5) -join "`n    "
            Err "    $errorLines"
        }
        Info ("-" * 40)
    }
    Info "Duration:        $($duration.ToString('hh\:mm\:ss'))"
    Info "Output:          $genFiles/issueReview/"
    Info ("=" * 80)

    # Return results for pipeline
    return $results
}
catch {
    Err "Error: $($_.Exception.Message)"
    exit 1
}
#endregion
