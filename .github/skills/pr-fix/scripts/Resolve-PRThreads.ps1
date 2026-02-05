<#
.SYNOPSIS
    Resolve all unresolved review threads for a PR.

.PARAMETER PRNumber
    PR number to resolve.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [int]$PRNumber
)

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')
Set-Location $repoRoot

$threads = gh api graphql -f query="query { repository(owner:\"microsoft\",name:\"PowerToys\") { pullRequest(number:$PRNumber) { reviewThreads(first:100) { nodes { id isResolved } } } } }" --jq '.data.repository.pullRequest.reviewThreads.nodes[] | select(.isResolved==false) | .id'

foreach ($threadId in $threads) {
    gh api graphql -f query="mutation { resolveReviewThread(input:{threadId:\"$threadId\"}) { thread { isResolved } } }" | Out-Null
}

$threadsAfter = gh api graphql -f query="query { repository(owner:\"microsoft\",name:\"PowerToys\") { pullRequest(number:$PRNumber) { reviewThreads(first:100) { nodes { id isResolved } } } } }" --jq '.data.repository.pullRequest.reviewThreads.nodes[] | select(.isResolved==false) | .id'

if ($threadsAfter) {
    Write-Warning "Unresolved threads remain for PR #$PRNumber"
} else {
    Write-Host "All threads resolved for PR #$PRNumber"
}