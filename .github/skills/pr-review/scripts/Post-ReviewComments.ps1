<#
.SYNOPSIS
    Post review findings as PR comments via GitHub API.

.DESCRIPTION
    Parses the review output files and posts findings as PR review comments.
    This provides a programmatic alternative to GitHub Copilot Code Review.

.PARAMETER PRNumber
    The PR number to post comments on.

.PARAMETER MinSeverity
    Minimum severity to post: high, medium, low, info. Default: medium.

.PARAMETER DryRun
    Show what would be posted without actually posting.

.EXAMPLE
    ./Post-ReviewComments.ps1 -PRNumber 45286 -MinSeverity medium
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [int]$PRNumber,
    
    [ValidateSet('high', 'medium', 'low', 'info')]
    [string]$MinSeverity = 'medium',
    
    [switch]$DryRun
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. "$scriptDir/IssueReviewLib.ps1"

$repoRoot = Get-RepoRoot
$reviewPath = Join-Path $repoRoot "Generated Files/prReview/$PRNumber"

function Get-SeverityLevel {
    param([string]$Severity)
    switch ($Severity.ToLower()) {
        'high' { return 3 }
        'medium' { return 2 }
        'low' { return 1 }
        'info' { return 0 }
        default { return 0 }
    }
}

function Parse-ReviewFindings {
    <#
    .SYNOPSIS
        Parse review markdown files for findings with severity.
    #>
    param(
        [string]$ReviewPath,
        [string]$MinSeverity = 'medium'
    )
    
    $minLevel = Get-SeverityLevel $MinSeverity
    $findings = @()
    
    # Get all step files
    $stepFiles = Get-ChildItem -Path $ReviewPath -Filter "*.md" | Where-Object { $_.Name -match '^\d{2}-' }
    
    foreach ($stepFile in $stepFiles) {
        $content = Get-Content $stepFile.FullName -Raw
        $stepName = $stepFile.BaseName
        
        # Parse MCP review comment blocks if present
        $mcpPattern = '```mcp-review-comment\s*\n([\s\S]*?)```'
        $mcpMatches = [regex]::Matches($content, $mcpPattern)
        
        foreach ($match in $mcpMatches) {
            $blockContent = $match.Groups[1].Value
            
            # Parse JSON-like structure
            if ($blockContent -match '"severity"\s*:\s*"(\w+)"') {
                $severity = $Matches[1]
                $severityLevel = Get-SeverityLevel $severity
                
                if ($severityLevel -ge $minLevel) {
                    $file = if ($blockContent -match '"file"\s*:\s*"([^"]+)"') { $Matches[1] } else { $null }
                    $line = if ($blockContent -match '"line"\s*:\s*(\d+)') { [int]$Matches[1] } else { $null }
                    $body = if ($blockContent -match '"body"\s*:\s*"([^"]*(?:\\.[^"]*)*)"') { 
                        $Matches[1] -replace '\\n', "`n" -replace '\\"', '"' 
                    } else { "" }
                    
                    $findings += @{
                        Step = $stepName
                        Severity = $severity
                        File = $file
                        Line = $line
                        Body = $body
                    }
                }
            }
        }
        
        # Also parse markdown-style findings (### Finding: or **Severity: X**)
        $findingPattern = '(?:###\s*Finding[:\s]+([^\n]+)\n|(?:\*\*)?Issue[:\s]+([^\n]+)(?:\*\*)?)\s*(?:\n.*?)?(?:\*\*)?Severity[:\s]+(\w+)(?:\*\*)?'
        $mdMatches = [regex]::Matches($content, $findingPattern, 'IgnoreCase')
        
        foreach ($match in $mdMatches) {
            $title = if ($match.Groups[1].Success) { $match.Groups[1].Value } else { $match.Groups[2].Value }
            $severity = $match.Groups[3].Value
            $severityLevel = Get-SeverityLevel $severity
            
            if ($severityLevel -ge $minLevel -and $title) {
                $findings += @{
                    Step = $stepName
                    Severity = $severity
                    File = $null
                    Line = $null
                    Body = "$title (from $stepName review)"
                }
            }
        }
    }
    
    return $findings
}

function Post-PRReviewComment {
    <#
    .SYNOPSIS
        Post a review comment on a PR.
    #>
    param(
        [int]$PRNumber,
        [string]$Body,
        [string]$Path,
        [int]$Line,
        [string]$CommitId
    )
    
    $payload = @{
        body = $Body
        event = "COMMENT"
    }
    
    if ($Path -and $Line -and $CommitId) {
        # Line-specific comment
        $payload["path"] = $Path
        $payload["line"] = $Line
        $payload["commit_id"] = $CommitId
    }
    
    $json = $payload | ConvertTo-Json -Compress
    $json | gh api "repos/microsoft/PowerToys/pulls/$PRNumber/reviews" --method POST --input - 2>&1
}

function Post-PRComment {
    <#
    .SYNOPSIS
        Post a general comment on a PR (not line-specific).
    #>
    param(
        [int]$PRNumber,
        [string]$Body
    )
    
    $payload = @{ body = $Body } | ConvertTo-Json -Compress
    $payload | gh api "repos/microsoft/PowerToys/issues/$PRNumber/comments" --method POST --input - 2>&1
}

# Main
try {
    if (-not (Test-Path $reviewPath)) {
        Err "Review not found at: $reviewPath"
        Err "Run pr-review skill first to generate review files."
        exit 1
    }
    
    Info "Parsing review findings from: $reviewPath"
    Info "Minimum severity: $MinSeverity"
    
    $findings = Parse-ReviewFindings -ReviewPath $reviewPath -MinSeverity $MinSeverity
    
    if ($findings.Count -eq 0) {
        Success "No findings with severity >= $MinSeverity"
        return @{ Posted = 0; Findings = @() }
    }
    
    Info "Found $($findings.Count) finding(s) to post"
    
    # Get PR head commit for line comments
    $prInfo = gh pr view $PRNumber --json headRefOid 2>$null | ConvertFrom-Json
    $headCommit = $prInfo.headRefOid
    
    # Group findings for a summary comment
    $summaryLines = @()
    $summaryLines += "## 🔍 Automated PR Review Summary"
    $summaryLines += ""
    $summaryLines += "| Step | Severity | Finding |"
    $summaryLines += "|------|----------|---------|"
    
    foreach ($finding in $findings) {
        $emoji = switch ($finding.Severity.ToLower()) {
            'high' { '🔴' }
            'medium' { '🟡' }
            'low' { '🟢' }
            default { 'ℹ️' }
        }
        $bodyPreview = if ($finding.Body.Length -gt 80) { $finding.Body.Substring(0, 77) + "..." } else { $finding.Body }
        $bodyPreview = $bodyPreview -replace '\n', ' ' -replace '\|', '/'
        $summaryLines += "| $($finding.Step) | $emoji $($finding.Severity) | $bodyPreview |"
    }
    
    $summaryLines += ""
    $summaryLines += "_Review generated by pr-review skill. See `Generated Files/prReview/$PRNumber/` for full details._"
    
    $summaryBody = $summaryLines -join "`n"
    
    if ($DryRun) {
        Warn "[DRY RUN] Would post summary comment:"
        Write-Host $summaryBody
        return @{ Posted = 0; Findings = $findings; DryRun = $true }
    }
    
    # Post summary comment
    Info "Posting summary comment..."
    $result = Post-PRComment -PRNumber $PRNumber -Body $summaryBody
    
    if ($result -match '"id"') {
        Success "Posted summary comment with $($findings.Count) finding(s)"
    } else {
        Warn "Comment posting may have failed: $result"
    }
    
    return @{
        Posted = 1
        Findings = $findings
        Summary = $summaryBody
    }
}
catch {
    Err "Error: $($_.Exception.Message)"
    exit 1
}
