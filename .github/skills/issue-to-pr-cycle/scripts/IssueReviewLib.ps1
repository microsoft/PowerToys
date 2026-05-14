# IssueReviewLib.ps1 - Helpers for full issue-to-PR cycle workflow
# Part of the PowerToys GitHub Copilot/Claude Code issue review system
# This is a trimmed version with only what issue-to-pr-cycle needs

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
#endregion

#region Issue Review Results Helpers
function Get-HighConfidenceIssues {
    <#
    .SYNOPSIS
        Find issues with high confidence for auto-fix based on review results.
    .PARAMETER RepoRoot
        Repository root path.
    .PARAMETER MinFeasibilityScore
        Minimum Technical Feasibility score (0-100). Default: 70.
    .PARAMETER MinClarityScore
        Minimum Requirement Clarity score (0-100). Default: 60.
    .PARAMETER MaxEffortDays
        Maximum effort estimate in days. Default: 2 (S = Small).
    .PARAMETER FilterIssueNumbers
        Optional array of issue numbers to filter to. If specified, only these issues are considered.
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
        
        # Skip if filter is specified and this issue is not in the filter list
        if ($FilterIssueNumbers.Count -gt 0 -and $issueNum -notin $FilterIssueNumbers) {
            return
        }
        
        $overviewPath = Join-Path $_.FullName 'overview.md'
        $implPlanPath = Join-Path $_.FullName 'implementation-plan.md'

        if (-not (Test-Path $overviewPath) -or -not (Test-Path $implPlanPath)) {
            return
        }

        # Parse overview.md to extract scores
        $overview = Get-Content $overviewPath -Raw

        # Extract scores using regex (looking for score table or inline scores)
        $feasibility = 0
        $clarity = 0
        $effortDays = 999

        # Try to extract from At-a-Glance Score Table
        if ($overview -match 'Technical Feasibility[^\d]*(\d+)/100') {
            $feasibility = [int]$Matches[1]
        }
        if ($overview -match 'Requirement Clarity[^\d]*(\d+)/100') {
            $clarity = [int]$Matches[1]
        }
        # Match effort formats like "0.5-1 day", "1-2 days", "2-3 days" - extract the upper bound
        if ($overview -match 'Effort Estimate[^|]*\|\s*[\d.]+(?:-(\d+))?\s*days?') {
            if ($Matches[1]) {
                $effortDays = [int]$Matches[1]
            } elseif ($overview -match 'Effort Estimate[^|]*\|\s*(\d+)\s*days?') {
                $effortDays = [int]$Matches[1]
            }
        }
        # Also check for XS/S sizing in the table
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
