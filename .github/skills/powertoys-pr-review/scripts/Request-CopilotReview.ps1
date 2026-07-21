<#
.SYNOPSIS
    Request a GitHub Copilot code review on a fork PR and poll until it lands.
.DESCRIPTION
    Requests 'copilot-pull-request-reviewer[bot]' as a reviewer (the plain
    'copilot' name silently no-ops), verifies the request was accepted, then
    polls the PR reviews until a Copilot review is submitted after the request
    time or the timeout elapses.
.PARAMETER ForkRepo
    owner/PowerToys for the fork that holds the mirror PR.
.PARAMETER PRNumber
    The fork PR number to review.
.PARAMETER TimeoutMinutes
    Maximum minutes to wait for the review (default 10).
.EXAMPLE
    ./Request-CopilotReview.ps1 -ForkRepo octocat/PowerToys -PRNumber 12
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $ForkRepo,
    [Parameter(Mandatory)] [int]    $PRNumber,
    [int] $TimeoutMinutes = 10
)

$ErrorActionPreference = 'Stop'
$requestedAt = (Get-Date).ToUniversalTime()

$resp = gh api "repos/$ForkRepo/pulls/$PRNumber/requested_reviewers" `
    -X POST -f "reviewers[]=copilot-pull-request-reviewer[bot]" | ConvertFrom-Json

$requested = @($resp.requested_reviewers)
if (-not $requested -or $requested.Count -eq 0) {
    Write-Warning "Copilot review was not accepted (requested_reviewers is empty). Enable 'Copilot code review' on the fork settings, or fall back to local review."
    return [pscustomobject]@{ Available = $false; Submitted = $false }
}

Write-Host "Requested Copilot review on $ForkRepo#$PRNumber. Polling (timeout ${TimeoutMinutes}m)..."
$deadline = $requestedAt.AddMinutes($TimeoutMinutes)
while ((Get-Date).ToUniversalTime() -lt $deadline) {
    Start-Sleep -Seconds 30
    $reviews = gh api "repos/$ForkRepo/pulls/$PRNumber/reviews" | ConvertFrom-Json
    $new = $reviews | Where-Object {
        $_.user.login -eq 'copilot-pull-request-reviewer[bot]' -and
        ([datetime]$_.submitted_at).ToUniversalTime() -gt $requestedAt
    }
    if ($new) {
        Write-Host "Copilot review submitted."
        return [pscustomobject]@{ Available = $true; Submitted = $true; SubmittedAt = $requestedAt }
    }
}
Write-Warning "Timed out waiting for the Copilot review. Re-run, or check the fork PR manually."
return [pscustomobject]@{ Available = $true; Submitted = $false }
