<#
.SYNOPSIS
    Get unresolved review threads on a PR.

.DESCRIPTION
    Lists all unresolved review threads with their IDs, paths, and comment bodies.
    This information is needed to resolve threads via GraphQL.

.PARAMETER PRNumber
    PR number to check.

.PARAMETER JsonOutput
    Output as JSON for programmatic use.

.EXAMPLE
    ./Get-UnresolvedThreads.ps1 -PRNumber 45286

.EXAMPLE
    ./Get-UnresolvedThreads.ps1 -PRNumber 45286 -JsonOutput
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [int]$PRNumber,
    
    [switch]$JsonOutput
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $scriptDir 'IssueReviewLib.ps1')

try {
    $query = @"
query {
  repository(owner: "microsoft", name: "PowerToys") {
    pullRequest(number: $PRNumber) {
      reviewThreads(first: 100) {
        nodes {
          id
          isResolved
          path
          line
          comments(first: 1) {
            nodes {
              body
              author { login }
              createdAt
            }
          }
        }
      }
    }
  }
}
"@

    $result = gh api graphql -f query=$query 2>$null | ConvertFrom-Json
    
    if (-not $result -or -not $result.data) {
        throw "Failed to fetch PR threads"
    }
    
    $threads = $result.data.repository.pullRequest.reviewThreads.nodes
    $unresolvedThreads = $threads | Where-Object { -not $_.isResolved }
    
    if ($JsonOutput) {
        $unresolvedThreads | ConvertTo-Json -Depth 5
        return
    }
    
    if ($unresolvedThreads.Count -eq 0) {
        Write-Host "✓ No unresolved threads on PR #$PRNumber" -ForegroundColor Green
        return
    }
    
    Write-Host ""
    Write-Host "=== UNRESOLVED THREADS ON PR #$PRNumber ===" -ForegroundColor Cyan
    Write-Host ("-" * 80)
    
    foreach ($thread in $unresolvedThreads) {
        $comment = $thread.comments.nodes[0]
        $preview = if ($comment.body.Length -gt 100) { 
            $comment.body.Substring(0, 100) + "..." 
        } else { 
            $comment.body 
        }
        
        Write-Host ""
        Write-Host "Thread ID: " -NoNewline -ForegroundColor Yellow
        Write-Host $thread.id
        Write-Host "File: " -NoNewline -ForegroundColor Gray
        Write-Host "$($thread.path):$($thread.line)"
        Write-Host "Author: " -NoNewline -ForegroundColor Gray
        Write-Host $comment.author.login
        Write-Host "Comment: " -ForegroundColor Gray
        Write-Host "  $preview"
    }
    
    Write-Host ""
    Write-Host ("-" * 80)
    Write-Host "Total unresolved: $($unresolvedThreads.Count)" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "To resolve a thread:" -ForegroundColor Cyan
    Write-Host '  gh api graphql -f query=''mutation { resolveReviewThread(input: {threadId: "THREAD_ID"}) { thread { isResolved } } }'''
    
    return $unresolvedThreads
}
catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
