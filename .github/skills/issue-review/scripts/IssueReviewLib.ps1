# IssueReviewLib.ps1 - Shared helpers for bulk issue review automation
# Part of the PowerToys GitHub Copilot/Claude Code issue review system

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

function Get-IssueTitleFromOverview {
    <#
    .SYNOPSIS
        Extract issue title from existing overview.md file.
    .DESCRIPTION
        Parses the overview.md to get the issue title without requiring GitHub CLI.
    #>
    param(
        [Parameter(Mandatory)]
        [string]$OverviewPath
    )

    if (-not (Test-Path $OverviewPath)) {
        return $null
    }

    $content = Get-Content $OverviewPath -Raw
    
    # Try to match title from Summary table: | **Title** | <title> |
    if ($content -match '\*\*Title\*\*\s*\|\s*([^|]+)\s*\|') {
        return $Matches[1].Trim()
    }
    
    # Try to match from header: # Issue #XXXX: <title>
    if ($content -match '# Issue #\d+[:\s]+(.+)$' ) {
        return $Matches[1].Trim()
    }
    
    # Try to match: # Issue #XXXX Review: <title>
    if ($content -match '# Issue #\d+ Review[:\s]+(.+)$') {
        return $Matches[1].Trim()
    }

    return $null
}

function Ensure-DirectoryExists {
    param([string]$Path)
    if (-not (Test-Path $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}
#endregion

#region GitHub Issue Query Helpers
function Get-GitHubIssues {
    <#
    .SYNOPSIS
        Query GitHub issues by label, state, and sort order.
    .PARAMETER Labels
        Comma-separated list of labels to filter by (e.g., "bug,help wanted").
    .PARAMETER State
        Issue state: open, closed, or all. Default: open.
    .PARAMETER Sort
        Sort field: created, updated, comments, reactions. Default: created.
    .PARAMETER Order
        Sort order: asc or desc. Default: desc.
    .PARAMETER Limit
        Maximum number of issues to return. Default: 100.
    .PARAMETER Repository
        Repository in owner/repo format. Default: microsoft/PowerToys.
    #>
    param(
        [string]$Labels,
        [ValidateSet('open', 'closed', 'all')]
        [string]$State = 'open',
        [ValidateSet('created', 'updated', 'comments', 'reactions')]
        [string]$Sort = 'created',
        [ValidateSet('asc', 'desc')]
        [string]$Order = 'desc',
        [int]$Limit = 100,
        [string]$Repository = 'microsoft/PowerToys'
    )

    $ghArgs = @('issue', 'list', '--repo', $Repository, '--state', $State, '--limit', $Limit)
    
    if ($Labels) {
        foreach ($label in ($Labels -split ',')) {
            $ghArgs += @('--label', $label.Trim())
        }
    }

    # Build JSON fields (use reactionGroups instead of reactions)
    $jsonFields = 'number,title,state,labels,createdAt,updatedAt,author,reactionGroups,comments'
    $ghArgs += @('--json', $jsonFields)

    Info "Querying issues: gh $($ghArgs -join ' ')"
    $result = & gh @ghArgs 2>&1

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to query issues: $result"
    }

    $issues = $result | ConvertFrom-Json

    # Sort by reactions if requested (gh CLI doesn't support this natively)
    if ($Sort -eq 'reactions') {
        $issues = $issues | ForEach-Object {
            # reactionGroups is an array of {content, users} - sum up user counts
            $totalReactions = ($_.reactionGroups | ForEach-Object { $_.users.totalCount } | Measure-Object -Sum).Sum
            if (-not $totalReactions) { $totalReactions = 0 }
            $_ | Add-Member -NotePropertyName 'totalReactions' -NotePropertyValue $totalReactions -PassThru
        }
        if ($Order -eq 'desc') {
            $issues = $issues | Sort-Object -Property totalReactions -Descending
        } else {
            $issues = $issues | Sort-Object -Property totalReactions
        }
    }

    return $issues
}

function Get-IssueDetails {
    <#
    .SYNOPSIS
        Get detailed information about a specific issue.
    #>
    param(
        [Parameter(Mandatory)]
        [int]$IssueNumber,
        [string]$Repository = 'microsoft/PowerToys'
    )

    $jsonFields = 'number,title,body,state,labels,createdAt,updatedAt,author,reactions,comments,linkedPullRequests,milestone'
    $result = gh issue view $IssueNumber --repo $Repository --json $jsonFields 2>&1

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to get issue #$IssueNumber`: $result"
    }

    return $result | ConvertFrom-Json
}
#endregion

#region CLI Detection and Execution
function Get-AvailableCLI {
    <#
    .SYNOPSIS
        Detect which AI CLI is available: GitHub Copilot CLI or Claude Code.
    .OUTPUTS
        Returns object with: Name, Command, PromptArg
    #>
    
    # Check for standalone GitHub Copilot CLI (copilot command)
    $copilotCLI = Get-Command 'copilot' -ErrorAction SilentlyContinue
    if ($copilotCLI) {
        return @{
            Name = 'GitHub Copilot CLI'
            Command = 'copilot'
            Args = @('-p')  # Non-interactive prompt mode
            Type = 'copilot'
        }
    }

    # Check for Claude Code CLI
    $claudeCode = Get-Command 'claude' -ErrorAction SilentlyContinue
    if ($claudeCode) {
        return @{
            Name = 'Claude Code CLI'
            Command = 'claude'
            Args = @()
            Type = 'claude'
        }
    }

    # Check for GitHub Copilot CLI via gh extension
    $ghCopilot = Get-Command 'gh' -ErrorAction SilentlyContinue
    if ($ghCopilot) {
        $copilotCheck = gh extension list 2>&1 | Select-String -Pattern 'copilot'
        if ($copilotCheck) {
            return @{
                Name = 'GitHub Copilot CLI (gh extension)'
                Command = 'gh'
                Args = @('copilot', 'suggest')
                Type = 'gh-copilot'
            }
        }
    }

    # Check for VS Code CLI with Copilot
    $code = Get-Command 'code' -ErrorAction SilentlyContinue
    if ($code) {
        return @{
            Name = 'VS Code (Copilot Chat)'
            Command = 'code'
            Args = @()
            Type = 'vscode'
        }
    }

    return $null
}

function Invoke-AIReview {
    <#
    .SYNOPSIS
        Invoke AI CLI to review a single issue.
    .PARAMETER IssueNumber
        The issue number to review.
    .PARAMETER RepoRoot
        Repository root path.
    .PARAMETER CLIType
        CLI type: 'claude', 'copilot', 'gh-copilot', or 'vscode'.
    .PARAMETER WorkingDirectory
        Working directory for the CLI command.
    #>
    param(
        [Parameter(Mandatory)]
        [int]$IssueNumber,
        [Parameter(Mandatory)]
        [string]$RepoRoot,
        [ValidateSet('claude', 'copilot', 'gh-copilot', 'vscode')]
        [string]$CLIType = 'copilot',
        [string]$WorkingDirectory
    )

    if (-not $WorkingDirectory) {
        $WorkingDirectory = $RepoRoot
    }

    $promptFile = Join-Path $RepoRoot '.github/prompts/review-issue.prompt.md'
    if (-not (Test-Path $promptFile)) {
        throw "Prompt file not found: $promptFile"
    }

    # Prepare the prompt with issue number substitution
    $promptContent = Get-Content $promptFile -Raw
    $promptContent = $promptContent -replace '\{\{issue_number\}\}', $IssueNumber

    # Create temp prompt file
    $tempPromptDir = Join-Path $env:TEMP "issue-review-$IssueNumber"
    Ensure-DirectoryExists -Path $tempPromptDir
    $tempPromptFile = Join-Path $tempPromptDir "prompt.md"
    $promptContent | Set-Content -Path $tempPromptFile -Encoding UTF8

    # Build the prompt text for CLI
    $promptText = "Review GitHub issue #$IssueNumber following the template in .github/prompts/review-issue.prompt.md. Generate overview.md and implementation-plan.md in 'Generated Files/issueReview/$IssueNumber/'"

    switch ($CLIType) {
        'copilot' {
            # GitHub Copilot CLI (standalone copilot command)
            # Use --yolo for full permissions (--allow-all-tools --allow-all-paths --allow-all-urls)
            # Use -s (silent) for cleaner output in batch mode
            # Enable ALL GitHub MCP tools (issues, PRs, repos, etc.) + github-artifacts for images/attachments
            # MCP config path relative to repo root for github-artifacts tools
            $mcpConfig = '@.github/skills/issue-review/references/mcp-config.json'
            $args = @(
                '--additional-mcp-config', $mcpConfig,  # Load github-artifacts MCP for image/attachment analysis
                '-p', $promptText,  # Non-interactive prompt mode (exits after completion)
                '--yolo',           # Enable all permissions for automated execution
                '-s',               # Silent mode - output only agent response
                '--enable-all-github-mcp-tools',  # Enable ALL GitHub MCP tools (issues, PRs, search, etc.)
                '--allow-tool', 'github-artifacts'  # Also enable our custom github-artifacts MCP
            )
            
            return @{
                Command = 'copilot'
                Arguments = $args
                WorkingDirectory = $WorkingDirectory
                IssueNumber = $IssueNumber
            }
        }
        'claude' {
            # Claude Code CLI
            $args = @(
                '--print',  # Non-interactive mode
                '--dangerously-skip-permissions',
                '--prompt', $promptText
            )
            
            return @{
                Command = 'claude'
                Arguments = $args
                WorkingDirectory = $WorkingDirectory
                IssueNumber = $IssueNumber
            }
        }
        'gh-copilot' {
            # GitHub Copilot CLI via gh
            $args = @(
                'copilot', 'suggest',
                '-t', 'shell',
                "Review GitHub issue #$IssueNumber and generate analysis files"
            )
            
            return @{
                Command = 'gh'
                Arguments = $args
                WorkingDirectory = $WorkingDirectory
                IssueNumber = $IssueNumber
            }
        }
        'vscode' {
            # VS Code with Copilot - open with prompt
            $args = @(
                '--new-window',
                $WorkingDirectory,
                '--goto', $tempPromptFile
            )
            
            return @{
                Command = 'code'
                Arguments = $args
                WorkingDirectory = $WorkingDirectory
                IssueNumber = $IssueNumber
            }
        }
    }
}
#endregion

#region Parallel Job Management
function Start-ParallelIssueReviews {
    <#
    .SYNOPSIS
        Start parallel issue reviews with throttling.
    .PARAMETER Issues
        Array of issue objects to review.
    .PARAMETER MaxParallel
        Maximum number of parallel jobs. Default: 20.
    .PARAMETER CLIType
        CLI type to use for reviews.
    .PARAMETER RepoRoot
        Repository root path.
    .PARAMETER TimeoutMinutes
        Timeout per issue in minutes. Default: 30.
    .PARAMETER MaxRetries
        Maximum number of retries for failed issues. Default: 2.
    .PARAMETER RetryDelaySeconds
        Delay between retries in seconds. Default: 10.
    #>
    param(
        [Parameter(Mandatory)]
        [array]$Issues,
        [int]$MaxParallel = 20,
        [ValidateSet('claude', 'copilot', 'gh-copilot', 'vscode')]
        [string]$CLIType = 'copilot',
        [Parameter(Mandatory)]
        [string]$RepoRoot,
        [int]$TimeoutMinutes = 30,
        [int]$MaxRetries = 2,
        [int]$RetryDelaySeconds = 10
    )

    $totalIssues = $Issues.Count
    $completed = 0
    $failed = @()
    $succeeded = @()
    $retryQueue = [System.Collections.Queue]::new()

    Info "Starting parallel review of $totalIssues issues (max $MaxParallel concurrent, $MaxRetries retries)"
    
    # Use PowerShell jobs for parallelization
    $jobs = @()
    $issueQueue = [System.Collections.Queue]::new($Issues)

    while ($issueQueue.Count -gt 0 -or $jobs.Count -gt 0 -or $retryQueue.Count -gt 0) {
        # Process retry queue when main queue is empty
        if ($issueQueue.Count -eq 0 -and $retryQueue.Count -gt 0 -and $jobs.Count -lt $MaxParallel) {
            $retryItem = $retryQueue.Dequeue()
            Warn "🔄 Retrying issue #$($retryItem.IssueNumber) (attempt $($retryItem.Attempt + 1)/$($MaxRetries + 1))"
            Start-Sleep -Seconds $RetryDelaySeconds
            $issueQueue.Enqueue(@{ number = $retryItem.IssueNumber; _retryAttempt = $retryItem.Attempt + 1 })
        }

        # Start new jobs up to MaxParallel
        while ($jobs.Count -lt $MaxParallel -and $issueQueue.Count -gt 0) {
            $issue = $issueQueue.Dequeue()
            $issueNum = $issue.number
            $retryAttempt = if ($issue._retryAttempt) { $issue._retryAttempt } else { 0 }
            
            $attemptInfo = if ($retryAttempt -gt 0) { " (retry $retryAttempt)" } else { "" }
            Info "Starting review for issue #$issueNum$attemptInfo ($($totalIssues - $issueQueue.Count)/$totalIssues)"
            
            $job = Start-Job -Name "Issue-$issueNum" -ScriptBlock {
                param($IssueNumber, $RepoRoot, $CLIType)
                
                Set-Location $RepoRoot
                
                # Import the library in the job context
                . "$RepoRoot/.github/review-tools/IssueReviewLib.ps1"
                
                try {
                    $reviewCmd = Invoke-AIReview -IssueNumber $IssueNumber -RepoRoot $RepoRoot -CLIType $CLIType
                    
                    # Execute the command using invocation operator (works for .ps1 scripts and executables)
                    Set-Location $reviewCmd.WorkingDirectory
                    $argList = $reviewCmd.Arguments
                    
                    # Capture both stdout and stderr for better error reporting
                    $output = & $reviewCmd.Command @argList 2>&1
                    $exitCode = $LASTEXITCODE
                    
                    # Get last 20 lines of output for error context
                    $outputLines = $output | Out-String
                    $lastLines = ($outputLines -split "`n" | Select-Object -Last 20) -join "`n"
                    
                    # Check if output files were created (success indicator)
                    $overviewPath = Join-Path $RepoRoot "Generated Files/issueReview/$IssueNumber/overview.md"
                    $implPlanPath = Join-Path $RepoRoot "Generated Files/issueReview/$IssueNumber/implementation-plan.md"
                    $filesCreated = (Test-Path $overviewPath) -and (Test-Path $implPlanPath)
                    
                    return @{
                        IssueNumber = $IssueNumber
                        Success = ($exitCode -eq 0) -or $filesCreated
                        ExitCode = $exitCode
                        FilesCreated = $filesCreated
                        Output = $lastLines
                        Error = if ($exitCode -ne 0 -and -not $filesCreated) { "Exit code: $exitCode`n$lastLines" } else { $null }
                    }
                }
                catch {
                    return @{
                        IssueNumber = $IssueNumber
                        Success = $false
                        ExitCode = -1
                        FilesCreated = $false
                        Output = $null
                        Error = $_.Exception.Message
                    }
                }
            } -ArgumentList $issueNum, $RepoRoot, $CLIType
            
            $jobs += @{
                Job = $job
                IssueNumber = $issueNum
                StartTime = Get-Date
                RetryAttempt = $retryAttempt
            }
        }

        # Check for completed jobs
        $completedJobs = @()
        foreach ($jobInfo in $jobs) {
            $job = $jobInfo.Job
            $issueNum = $jobInfo.IssueNumber
            $startTime = $jobInfo.StartTime
            $retryAttempt = $jobInfo.RetryAttempt
            
            if ($job.State -eq 'Completed') {
                $result = Receive-Job -Job $job
                Remove-Job -Job $job -Force
                
                if ($result.Success) {
                    Success "✓ Issue #$issueNum completed (files created: $($result.FilesCreated))"
                    $succeeded += $issueNum
                    $completed++
                } else {
                    # Check if we should retry
                    if ($retryAttempt -lt $MaxRetries) {
                        $errorPreview = if ($result.Error) { ($result.Error -split "`n" | Select-Object -First 3) -join " | " } else { "Unknown error" }
                        Warn "⚠ Issue #$issueNum failed (will retry): $errorPreview"
                        $retryQueue.Enqueue(@{ IssueNumber = $issueNum; Attempt = $retryAttempt; LastError = $result.Error })
                    } else {
                        $errorMsg = if ($result.Error) { $result.Error } else { "Exit code: $($result.ExitCode)" }
                        Err "✗ Issue #$issueNum failed after $($retryAttempt + 1) attempts:"
                        Err "  Error: $errorMsg"
                        $failed += @{ IssueNumber = $issueNum; Error = $errorMsg; Attempts = $retryAttempt + 1 }
                        $completed++
                    }
                }
                $completedJobs += $jobInfo
            }
            elseif ($job.State -eq 'Failed') {
                $jobError = $job.ChildJobs[0].JobStateInfo.Reason.Message
                Remove-Job -Job $job -Force
                
                if ($retryAttempt -lt $MaxRetries) {
                    Warn "⚠ Issue #$issueNum job crashed (will retry): $jobError"
                    $retryQueue.Enqueue(@{ IssueNumber = $issueNum; Attempt = $retryAttempt; LastError = $jobError })
                } else {
                    Err "✗ Issue #$issueNum job failed after $($retryAttempt + 1) attempts: $jobError"
                    $failed += @{ IssueNumber = $issueNum; Error = $jobError; Attempts = $retryAttempt + 1 }
                    $completed++
                }
                $completedJobs += $jobInfo
            }
            elseif ((Get-Date) - $startTime -gt [TimeSpan]::FromMinutes($TimeoutMinutes)) {
                Stop-Job -Job $job -ErrorAction SilentlyContinue
                Remove-Job -Job $job -Force
                
                if ($retryAttempt -lt $MaxRetries) {
                    Warn "⏱ Issue #$issueNum timed out after $TimeoutMinutes min (will retry)"
                    $retryQueue.Enqueue(@{ IssueNumber = $issueNum; Attempt = $retryAttempt; LastError = "Timeout after $TimeoutMinutes minutes" })
                } else {
                    Err "⏱ Issue #$issueNum timed out after $($retryAttempt + 1) attempts"
                    $failed += @{ IssueNumber = $issueNum; Error = "Timeout after $TimeoutMinutes minutes"; Attempts = $retryAttempt + 1 }
                    $completed++
                }
                $completedJobs += $jobInfo
            }
        }

        # Remove completed jobs from active list
        $jobs = $jobs | Where-Object { $_ -notin $completedJobs }

        # Brief pause to avoid tight loop
        if ($jobs.Count -gt 0) {
            Start-Sleep -Seconds 2
        }
    }

    # Extract just issue numbers for the failed list
    $failedNumbers = $failed | ForEach-Object { $_.IssueNumber }

    return @{
        Total = $totalIssues
        Succeeded = $succeeded
        Failed = $failedNumbers
        FailedDetails = $failed
    }
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
        # Also check for XS/S sizing in the table (e.g., "| XS |" or "| S |" or "(XS)" or "(S)")
        if ($overview -match 'Effort Estimate[^|]*\|[^|]*\|\s*(XS|S)\b') {
            # XS = 1 day, S = 2 days
            if ($Matches[1] -eq 'XS') {
                $effortDays = 1
            } else {
                $effortDays = 2
            }
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

#region Worktree Integration
function Copy-IssueReviewToWorktree {
    <#
    .SYNOPSIS
        Copy the Generated Files for an issue to a worktree.
    .PARAMETER IssueNumber
        The issue number.
    .PARAMETER SourceRepoRoot
        Source repository root (main repo).
    .PARAMETER WorktreePath
        Destination worktree path.
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

    # Copy all files from the issue review folder
    Copy-Item -Path "$sourceReviewPath\*" -Destination $destReviewPath -Recurse -Force

    Info "Copied issue review files to: $destReviewPath"
    
    return $destReviewPath
}
#endregion

# Note: This script is dot-sourced, not imported as a module.
# All functions above are available after: . "path/to/IssueReviewLib.ps1"
