# IssueReviewLib.ps1 - Helpers for issue auto-fix workflow
# Part of the PowerToys GitHub Copilot/Claude Code issue review system
# This is a trimmed version with only what issue-fix needs

#region Console Output Helpers
function Info { param([string]$Message) Write-Host $Message -ForegroundColor Cyan }
function Warn { param([string]$Message) Write-Host $Message -ForegroundColor Yellow }
function Err  { param([string]$Message) Write-Host $Message -ForegroundColor Red }
function Success { param([string]$Message) Write-Host $Message -ForegroundColor Green }
#endregion

#region Repository Helpers
function Get-RepoRoot {
    $root = git rev-parse --show-toplevel 2>$null
    if (-not $root) { throw 'Not inside a git repository.' }
    return (Resolve-Path $root).Path
}

function Get-GeneratedFilesPath {
    param([string]$RepoRoot)
    return Join-Path $RepoRoot 'Generated Files'
}

function Get-IssueReviewPath {
    param(
        [string]$RepoRoot,
        [int]$IssueNumber
    )
    $genFiles = Get-GeneratedFilesPath -RepoRoot $RepoRoot
    return Join-Path $genFiles "issueReview/$IssueNumber"
}

function Ensure-DirectoryExists {
    param([string]$Path)
    if (-not (Test-Path $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}
#endregion

#region CLI Detection
function Get-AvailableCLI {
    <#
    .SYNOPSIS
        Detect which AI CLI is available: GitHub Copilot CLI or Claude Code.
    #>
    
    # Check for standalone GitHub Copilot CLI
    $copilotCLI = Get-Command 'copilot' -ErrorAction SilentlyContinue
    if ($copilotCLI) {
        return @{ Name = 'GitHub Copilot CLI'; Command = 'copilot'; Type = 'copilot' }
    }

    # Check for Claude Code CLI
    $claudeCode = Get-Command 'claude' -ErrorAction SilentlyContinue
    if ($claudeCode) {
        return @{ Name = 'Claude Code CLI'; Command = 'claude'; Type = 'claude' }
    }

    # Check for GitHub Copilot CLI via gh extension
    $ghCopilot = Get-Command 'gh' -ErrorAction SilentlyContinue
    if ($ghCopilot) {
        $copilotCheck = gh extension list 2>&1 | Select-String -Pattern 'copilot'
        if ($copilotCheck) {
            return @{ Name = 'GitHub Copilot CLI (gh extension)'; Command = 'gh'; Type = 'gh-copilot' }
        }
    }

    # Check for VS Code CLI
    $code = Get-Command 'code' -ErrorAction SilentlyContinue
    if ($code) {
        return @{ Name = 'VS Code (Copilot Chat)'; Command = 'code'; Type = 'vscode' }
    }

    return $null
}
#endregion

#region Issue Review Results Helpers
function Get-IssueReviewResult {
    <#
    .SYNOPSIS
        Check if an issue has been reviewed and get its results.
    #>
    param(
        [Parameter(Mandatory)]
        [int]$IssueNumber,
        [Parameter(Mandatory)]
        [string]$RepoRoot
    )

    $reviewPath = Get-IssueReviewPath -RepoRoot $RepoRoot -IssueNumber $IssueNumber
    
    $result = @{
        IssueNumber = $IssueNumber
        Path = $reviewPath
        HasOverview = $false
        HasImplementationPlan = $false
        OverviewPath = $null
        ImplementationPlanPath = $null
    }

    $overviewPath = Join-Path $reviewPath 'overview.md'
    $implPlanPath = Join-Path $reviewPath 'implementation-plan.md'

    if (Test-Path $overviewPath) {
        $result.HasOverview = $true
        $result.OverviewPath = $overviewPath
    }

    if (Test-Path $implPlanPath) {
        $result.HasImplementationPlan = $true
        $result.ImplementationPlanPath = $implPlanPath
    }

    return $result
}

function Get-HighConfidenceIssues {
    <#
    .SYNOPSIS
        Find issues with high confidence for auto-fix based on review results.
    #>
    param(
        [Parameter(Mandatory)]
        [string]$RepoRoot,
        [int]$MinFeasibilityScore = 70,
        [int]$MinClarityScore = 60,
        [int]$MaxEffortDays = 2,
        [int[]]$FilterIssueNumbers = @()
    )

    $genFiles = Get-GeneratedFilesPath -RepoRoot $RepoRoot
    $reviewDir = Join-Path $genFiles 'issueReview'

    if (-not (Test-Path $reviewDir)) {
        return @()
    }

    $highConfidence = @()

    Get-ChildItem -Path $reviewDir -Directory | ForEach-Object {
        $issueNum = [int]$_.Name
        
        if ($FilterIssueNumbers.Count -gt 0 -and $issueNum -notin $FilterIssueNumbers) {
            return
        }
        
        $overviewPath = Join-Path $_.FullName 'overview.md'
        $implPlanPath = Join-Path $_.FullName 'implementation-plan.md'

        if (-not (Test-Path $overviewPath) -or -not (Test-Path $implPlanPath)) {
            return
        }

        $overview = Get-Content $overviewPath -Raw

        $feasibility = 0
        $clarity = 0
        $effortDays = 999

        if ($overview -match 'Technical Feasibility[^\d]*(\d+)/100') {
            $feasibility = [int]$Matches[1]
        }
        if ($overview -match 'Requirement Clarity[^\d]*(\d+)/100') {
            $clarity = [int]$Matches[1]
        }
        if ($overview -match 'Effort Estimate[^|]*\|\s*[\d.]+(?:-(\d+))?\s*days?') {
            if ($Matches[1]) {
                $effortDays = [int]$Matches[1]
            } elseif ($overview -match 'Effort Estimate[^|]*\|\s*(\d+)\s*days?') {
                $effortDays = [int]$Matches[1]
            }
        }
        if ($overview -match 'Effort Estimate[^|]*\|[^|]*\|\s*(XS|S)\b') {
            if ($Matches[1] -eq 'XS') { $effortDays = 1 } else { $effortDays = 2 }
        } elseif ($overview -match 'Effort Estimate[^|]*\|[^|]*\(XS\)') {
            $effortDays = 1
        } elseif ($overview -match 'Effort Estimate[^|]*\|[^|]*\(S\)') {
            $effortDays = 2
        }

        if ($feasibility -ge $MinFeasibilityScore -and 
            $clarity -ge $MinClarityScore -and 
            $effortDays -le $MaxEffortDays) {
            
            $highConfidence += @{
                IssueNumber = $issueNum
                FeasibilityScore = $feasibility
                ClarityScore = $clarity
                EffortDays = $effortDays
                OverviewPath = $overviewPath
                ImplementationPlanPath = $implPlanPath
            }
        }
    }

    return $highConfidence | Sort-Object -Property FeasibilityScore -Descending
}
#endregion

#region Release & PR Status Helpers
function Get-PRReleaseStatus {
    <#
    .SYNOPSIS
        Check if a PR has been merged and released.
    .DESCRIPTION
        Queries GitHub to determine:
        1. If the PR is merged
        2. What release (if any) contains the merge commit
    .OUTPUTS
        @{
            PRNumber = <int>
            IsMerged = $true | $false
            MergeCommit = <commit sha or $null>
            ReleasedIn = <version string or $null>  # e.g., "v0.90.0"
            IsReleased = $true | $false
        }
    #>
    param(
        [Parameter(Mandatory)]
        [int]$PRNumber,
        [string]$Repo = 'microsoft/PowerToys'
    )

    $result = @{
        PRNumber = $PRNumber
        IsMerged = $false
        MergeCommit = $null
        ReleasedIn = $null
        IsReleased = $false
    }

    try {
        # Get PR details from GitHub
        $prJson = gh pr view $PRNumber --repo $Repo --json state,mergeCommit,mergedAt 2>$null
        if (-not $prJson) {
            return $result
        }

        $pr = $prJson | ConvertFrom-Json

        if ($pr.state -eq 'MERGED' -and $pr.mergeCommit) {
            $result.IsMerged = $true
            $result.MergeCommit = $pr.mergeCommit.oid

            # Check which release tags contain this commit
            # Use git tag --contains to find tags that include the merge commit
            $tags = git tag --contains $result.MergeCommit 2>$null
            
            if ($tags) {
                # Filter to release tags (v0.XX.X pattern) and get the earliest one
                $releaseTags = $tags | Where-Object { $_ -match '^v\d+\.\d+\.\d+$' } | Sort-Object
                if ($releaseTags) {
                    $result.ReleasedIn = $releaseTags | Select-Object -First 1
                    $result.IsReleased = $true
                }
            }
        }
    }
    catch {
        # Silently fail - will return default "not merged" status
    }

    return $result
}

function Get-LatestRelease {
    <#
    .SYNOPSIS
        Get the latest release version of PowerToys.
    #>
    param(
        [string]$Repo = 'microsoft/PowerToys'
    )

    try {
        $releaseJson = gh release view --repo $Repo --json tagName 2>$null
        if ($releaseJson) {
            $release = $releaseJson | ConvertFrom-Json
            return $release.tagName
        }
    }
    catch {
        # Fallback: try to get from git tags
        $latestTag = git describe --tags --abbrev=0 2>$null
        if ($latestTag) {
            return $latestTag
        }
    }
    return $null
}
#endregion

#region Implementation Plan Analysis
function Get-ImplementationPlanStatus {
    <#
    .SYNOPSIS
        Parse implementation-plan.md to determine the recommended action.
    .DESCRIPTION
        Reads the implementation plan and extracts the status/recommendation.
        For "already resolved" issues, also checks if the fix has been released.
        Returns an object indicating what action should be taken.
    .OUTPUTS
        @{
            Status = 'AlreadyResolved' | 'FixedButUnreleased' | 'NeedsClarification' | 'Duplicate' | 'WontFix' | 'ReadyToImplement' | 'Unknown'
            Action = 'CloseIssue' | 'AddComment' | 'LinkDuplicate' | 'ImplementFix' | 'Skip'
            Reason = <string explaining why>
            RelatedPR = <PR number if already fixed>
            ReleasedIn = <version if released, e.g., "v0.90.0">
            DuplicateOf = <issue number if duplicate>
            CommentText = <suggested comment if applicable>
        }
    #>
    param(
        [Parameter(Mandatory)]
        [string]$ImplementationPlanPath,
        [switch]$SkipReleaseCheck
    )

    $result = @{
        Status = 'Unknown'
        Action = 'Skip'
        Reason = 'Could not determine status from implementation plan'
        RelatedPR = $null
        ReleasedIn = $null
        DuplicateOf = $null
        CommentText = $null
    }

    if (-not (Test-Path $ImplementationPlanPath)) {
        $result.Reason = 'Implementation plan file not found'
        return $result
    }

    $content = Get-Content $ImplementationPlanPath -Raw

    # Check for ALREADY RESOLVED status
    if ($content -match '(?i)STATUS:\s*ALREADY\s+RESOLVED' -or 
        $content -match '(?i)⚠️\s*STATUS:\s*ALREADY\s+RESOLVED' -or
        $content -match '(?i)This issue has been fixed by' -or
        $content -match '(?i)No implementation work is needed') {
        
        # Try to extract the PR number
        $prNumber = $null
        if ($content -match '\[PR #(\d+)\]' -or $content -match 'PR #(\d+)' -or $content -match '/pull/(\d+)') {
            $prNumber = [int]$Matches[1]
            $result.RelatedPR = $prNumber
        }

        # Check if the fix has been released
        if ($prNumber -and -not $SkipReleaseCheck) {
            $prStatus = Get-PRReleaseStatus -PRNumber $prNumber
            
            if ($prStatus.IsReleased) {
                # Fix is released - safe to close
                $result.Status = 'AlreadyResolved'
                $result.Action = 'CloseIssue'
                $result.ReleasedIn = $prStatus.ReleasedIn
                $result.Reason = "Issue fixed by PR #$prNumber, released in $($prStatus.ReleasedIn)"
                $result.CommentText = @"
This issue has been fixed by PR #$prNumber and is available in **$($prStatus.ReleasedIn)**.

Please update to the latest version. If you're still experiencing this issue after updating, please reopen with additional details.
"@
            }
            elseif ($prStatus.IsMerged) {
                # PR merged but not yet released - add comment but don't close
                $result.Status = 'FixedButUnreleased'
                $result.Action = 'AddComment'
                $result.Reason = "Issue fixed by PR #$prNumber, but not yet released"
                $result.CommentText = @"
This issue has been fixed by PR #$prNumber, which has been merged but **not yet released**.

The fix will be available in the next PowerToys release. You can:
- Wait for the next official release
- Build from source to get the fix immediately

We'll close this issue once the fix is released.
"@
            }
            else {
                # PR exists but not merged - treat as ready to implement (PR might have been reverted)
                $result.Status = 'ReadyToImplement'
                $result.Action = 'ImplementFix'
                $result.Reason = "PR #$prNumber exists but is not merged - may need reimplementation"
            }
        }
        elseif ($prNumber) {
            # Skip release check requested or no PR number - assume it's resolved
            $result.Status = 'AlreadyResolved'
            $result.Action = 'CloseIssue'
            $result.Reason = 'Issue has already been fixed'
            $result.CommentText = "This issue has been fixed by PR #$prNumber. Closing as resolved."
        }
        else {
            # No PR number found - just mark as resolved with generic message
            $result.Status = 'AlreadyResolved'
            $result.Action = 'CloseIssue'
            $result.Reason = 'Issue appears to have been resolved'
            $result.CommentText = "Based on analysis, this issue appears to have already been resolved. Please verify and reopen if the issue persists."
        }
        
        return $result
    }

    # Check for DUPLICATE status
    if ($content -match '(?i)STATUS:\s*DUPLICATE' -or 
        $content -match '(?i)This is a duplicate of' -or
        $content -match '(?i)duplicate of #(\d+)') {
        
        $result.Status = 'Duplicate'
        $result.Action = 'LinkDuplicate'
        $result.Reason = 'Issue is a duplicate'
        
        # Try to extract the duplicate issue number
        if ($content -match 'duplicate of #(\d+)' -or $content -match '#(\d+)') {
            $result.DuplicateOf = [int]$Matches[1]
            $result.CommentText = "This appears to be a duplicate of #$($result.DuplicateOf)."
        }
        
        return $result
    }

    # Check for NEEDS CLARIFICATION status
    if ($content -match '(?i)STATUS:\s*NEEDS?\s+CLARIFICATION' -or 
        $content -match '(?i)STATUS:\s*NEEDS?\s+MORE\s+INFO' -or
        $content -match '(?i)cannot proceed without' -or
        $content -match '(?i)need(?:s)? more information') {
        
        $result.Status = 'NeedsClarification'
        $result.Action = 'AddComment'
        $result.Reason = 'Issue needs more information from reporter'
        
        # Try to extract what information is needed
        if ($content -match '(?i)(?:need(?:s)?|require(?:s)?|missing)[:\s]+([^\n]+)') {
            $result.CommentText = "Additional information is needed to proceed with this issue: $($Matches[1].Trim())"
        } else {
            $result.CommentText = "Could you please provide more details about this issue? Specifically, steps to reproduce and expected vs actual behavior would help."
        }
        
        return $result
    }

    # Check for WONT FIX / NOT FEASIBLE status
    if ($content -match '(?i)STATUS:\s*(?:WONT?\s+FIX|NOT\s+FEASIBLE|REJECTED)' -or 
        $content -match '(?i)(?:not|cannot be) (?:feasible|implemented)' -or
        $content -match '(?i)recommend(?:ed)?\s+(?:to\s+)?close') {
        
        $result.Status = 'WontFix'
        $result.Action = 'AddComment'
        $result.Reason = 'Issue is not feasible or recommended to close'
        
        # Try to extract the reason
        if ($content -match '(?i)(?:because|reason|due to)[:\s]+([^\n]+)') {
            $result.CommentText = "After analysis, this issue cannot be implemented: $($Matches[1].Trim())"
        }
        
        return $result
    }

    # Check for external dependency / blocked status
    if ($content -match '(?i)STATUS:\s*BLOCKED' -or 
        $content -match '(?i)blocked by' -or
        $content -match '(?i)depends on external' -or
        $content -match '(?i)waiting for upstream') {
        
        $result.Status = 'Blocked'
        $result.Action = 'AddComment'
        $result.Reason = 'Issue is blocked by external dependency'
        
        return $result
    }

    # Check for READY TO IMPLEMENT (positive signals)
    if ($content -match '(?i)## \d+\)\s*Task Breakdown' -or
        $content -match '(?i)implementation steps' -or
        $content -match '(?i)## Layers & Files' -or
        ($content -match '(?i)Feasibility' -and $content -notmatch '(?i)not\s+feasible')) {
        
        $result.Status = 'ReadyToImplement'
        $result.Action = 'ImplementFix'
        $result.Reason = 'Implementation plan is ready'
        
        return $result
    }

    # Default: if we have a detailed plan, assume it's ready
    if ($content.Length -gt 500 -and $content -match '(?i)##') {
        $result.Status = 'ReadyToImplement'
        $result.Action = 'ImplementFix'
        $result.Reason = 'Implementation plan appears complete'
    }

    return $result
}

function Invoke-ImplementationPlanAction {
    <#
    .SYNOPSIS
        Execute the recommended action from the implementation plan analysis.
    .DESCRIPTION
        Based on the status from Get-ImplementationPlanStatus, takes appropriate action:
        - CloseIssue: Closes the issue with a comment
        - AddComment: Adds a comment to the issue
        - LinkDuplicate: Marks as duplicate
        - ImplementFix: Returns $true to indicate code fix should proceed
        - Skip: Returns $false
    .OUTPUTS
        @{
            ActionTaken = <string describing what was done>
            ShouldProceedWithFix = $true | $false
            Success = $true | $false
        }
    #>
    param(
        [Parameter(Mandatory)]
        [int]$IssueNumber,
        [Parameter(Mandatory)]
        [hashtable]$PlanStatus,
        [switch]$DryRun
    )

    $result = @{
        ActionTaken = 'None'
        ShouldProceedWithFix = $false
        Success = $true
    }

    switch ($PlanStatus.Action) {
        'ImplementFix' {
            $result.ActionTaken = 'Proceeding with code fix'
            $result.ShouldProceedWithFix = $true
            Info "[Issue #$IssueNumber] Status: $($PlanStatus.Status) - $($PlanStatus.Reason)"
        }
        
        'CloseIssue' {
            $result.ActionTaken = "Closing issue: $($PlanStatus.Reason)"
            Info "[Issue #$IssueNumber] $($PlanStatus.Status): $($PlanStatus.Reason)"
            
            if (-not $DryRun) {
                $comment = $PlanStatus.CommentText
                if (-not $comment) {
                    $comment = "Closing based on automated analysis: $($PlanStatus.Reason)"
                }
                
                try {
                    # Check if issue is already closed
                    $issueState = gh issue view $IssueNumber --json state 2>$null | ConvertFrom-Json
                    if ($issueState.state -eq 'CLOSED') {
                        Info "[Issue #$IssueNumber] Already closed, skipping"
                        $result.ActionTaken = "Already closed"
                        return $result
                    }
                    
                    # Close the issue with comment (single operation to avoid duplicates)
                    gh issue close $IssueNumber --reason "completed" --comment $comment 2>&1 | Out-Null
                    
                    Success "[Issue #$IssueNumber] ✓ Closed with comment"
                }
                catch {
                    Err "[Issue #$IssueNumber] Failed to close: $($_.Exception.Message)"
                    $result.Success = $false
                }
            } else {
                Info "[Issue #$IssueNumber] (DryRun) Would close with: $($PlanStatus.CommentText)"
            }
        }
        
        'AddComment' {
            $result.ActionTaken = "Adding comment: $($PlanStatus.Reason)"
            Info "[Issue #$IssueNumber] $($PlanStatus.Status): $($PlanStatus.Reason)"
            
            if (-not $DryRun -and $PlanStatus.CommentText) {
                try {
                    gh issue comment $IssueNumber --body $PlanStatus.CommentText 2>&1 | Out-Null
                    Success "[Issue #$IssueNumber] ✓ Comment added"
                }
                catch {
                    Err "[Issue #$IssueNumber] Failed to add comment: $($_.Exception.Message)"
                    $result.Success = $false
                }
            } else {
                Info "[Issue #$IssueNumber] (DryRun) Would comment: $($PlanStatus.CommentText)"
            }
        }
        
        'LinkDuplicate' {
            $result.ActionTaken = "Marking as duplicate of #$($PlanStatus.DuplicateOf)"
            Info "[Issue #$IssueNumber] Duplicate of #$($PlanStatus.DuplicateOf)"
            
            if (-not $DryRun -and $PlanStatus.DuplicateOf) {
                try {
                    gh issue close $IssueNumber --reason "not_planned" --comment "Closing as duplicate of #$($PlanStatus.DuplicateOf)" 2>&1 | Out-Null
                    Success "[Issue #$IssueNumber] ✓ Closed as duplicate"
                }
                catch {
                    Err "[Issue #$IssueNumber] Failed to close as duplicate: $($_.Exception.Message)"
                    $result.Success = $false
                }
            }
        }
        
        'Skip' {
            $result.ActionTaken = "Skipped: $($PlanStatus.Reason)"
            Warn "[Issue #$IssueNumber] Skipping: $($PlanStatus.Reason)"
        }
    }

    return $result
}
#endregion

#region Worktree Integration
function Copy-IssueReviewToWorktree {
    <#
    .SYNOPSIS
        Copy the Generated Files for an issue to a worktree.
    #>
    param(
        [Parameter(Mandatory)]
        [int]$IssueNumber,
        [Parameter(Mandatory)]
        [string]$SourceRepoRoot,
        [Parameter(Mandatory)]
        [string]$WorktreePath
    )

    $sourceReviewPath = Get-IssueReviewPath -RepoRoot $SourceRepoRoot -IssueNumber $IssueNumber
    $destReviewPath = Get-IssueReviewPath -RepoRoot $WorktreePath -IssueNumber $IssueNumber

    if (-not (Test-Path $sourceReviewPath)) {
        throw "Issue review files not found at: $sourceReviewPath"
    }

    Ensure-DirectoryExists -Path $destReviewPath

    Copy-Item -Path "$sourceReviewPath\*" -Destination $destReviewPath -Recurse -Force

    Info "Copied issue review files to: $destReviewPath"
    
    return $destReviewPath
}
#endregion
