<#
.SYNOPSIS
    Count unresolved Copilot review threads on a fork PR.
.DESCRIPTION
    Returns the number of review threads whose first comment is authored by
    'copilot-pull-request-reviewer' and that are not yet resolved. Used to decide
    whether the review loop is finished (expect 0) and to detect a stranded loop
    when resuming an interrupted session.
.PARAMETER ForkOwner
    The fork owner login (the repo is assumed to be <owner>/PowerToys).
.PARAMETER PRNumber
    The fork PR number.
.EXAMPLE
    ./Get-UnresolvedCopilotThreads.ps1 -ForkOwner octocat -PRNumber 12
    Returns an integer count; 0 means the loop has converged.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $ForkOwner,
    [Parameter(Mandatory)] [int]    $PRNumber
)

$ErrorActionPreference = 'Stop'

$query = @"
{ repository(owner:"$ForkOwner",name:"PowerToys"){ pullRequest(number:$PRNumber){ reviewThreads(first:100){ nodes{ isResolved comments(first:1){ nodes{ author{ login } } } } } } } }
"@

$count = gh api graphql -f query=$query --jq '[.data.repository.pullRequest.reviewThreads.nodes[] | select(.comments.nodes[0].author.login=="copilot-pull-request-reviewer") | select(.isResolved==false)] | length'

[int]$count
