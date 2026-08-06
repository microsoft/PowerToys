<#
.SYNOPSIS
    Safely publish approved PowerToys PR review payloads.
.DESCRIPTION
    Validates review data and decisions, re-checks upstream freshness, creates a
    pending GitHub review, reads every rendered body back, and submits only after
    exact verification. Progress is persisted so interrupted runs resume without
    duplicating public comments.
.PARAMETER DataPath
    Path to schema-version-2 review-data.json.
.PARAMETER DecisionsPath
    Path to schema-version-2 review-decisions.json produced by the dashboard.
.PARAMETER StatePath
    Optional idempotency ledger. Defaults beside DecisionsPath.
.PARAMETER DryRun
    Build and print public posting plans without creating GitHub reviews.
.PARAMETER Offline
    With DryRun only, skip current GitHub head and diff checks. Intended for tests.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$DataPath,
    [Parameter(Mandatory)][string]$DecisionsPath,
    [string]$StatePath,
    [switch]$DryRun,
    [switch]$Offline
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ReviewPayload.Common.ps1')

if ($Offline -and -not $DryRun) {
    throw '-Offline is allowed only with -DryRun.'
}

if (-not $StatePath) {
    $StatePath = Join-Path (Split-Path -Parent $DecisionsPath) 'review-publish-state.json'
}

$reviewData = Read-JsonFile -Path $DataPath
$decisions = Read-JsonFile -Path $DecisionsPath
$reviewDataHash = Get-ReviewDataHash -Path $DataPath

$errors = [System.Collections.Generic.List[string]]::new()
foreach ($errorMessage in Test-ReviewDataDocument -Document $reviewData -CheckGitHub:(-not $Offline)) {
    $errors.Add($errorMessage)
}
foreach ($errorMessage in Test-ReviewDecisionDocument -Decisions $decisions -ReviewData $reviewData -ExpectedHash $reviewDataHash) {
    $errors.Add($errorMessage)
}
if ($errors.Count -gt 0) {
    throw ($errors -join [Environment]::NewLine)
}

$plans = @(
    foreach ($decision in @($decisions.prs)) {
        Get-ApprovedReviewPlan -ReviewData $reviewData -Decision $decision
    }
)

if ($DryRun) {
    $plans | ConvertTo-Json -Depth 20
    return
}

if (Test-Path -LiteralPath $StatePath) {
    $state = Read-JsonFile -Path $StatePath
    if ([string]$state.reviewDataHash -ne $reviewDataHash) {
        throw 'Existing publish state belongs to a different review-data.json payload.'
    }
}
else {
    $state = [pscustomobject]@{
        schemaVersion = 1
        reviewDataHash = $reviewDataHash
        prs = @()
    }
}

function Save-PublishState {
    Write-JsonFileAtomically -Path $StatePath -Value $state
}

function Get-PublishRecord {
    param([int]$PullRequestNumber)

    return @($state.prs | Where-Object number -eq $PullRequestNumber)
}

function Assert-FreshSnapshot {
    param(
        [Parameter(Mandatory)]$Plan,
        [Parameter(Mandatory)][string]$Repository,
        [Nullable[long]]$PendingReviewId
    )

    $live = Get-GitHubReviewState -Repository $Repository -PullRequestNumber $Plan.number
    if ([string]$live.headSha -ne [string]$Plan.headSha) {
        throw "PR $($Plan.number) head moved from $($Plan.headSha) to $($live.headSha)."
    }

    if ((ConvertTo-ReviewTimestamp -Value $live.updatedAt) -ne
        (ConvertTo-ReviewTimestamp -Value $Plan.snapshot.updatedAt)) {
        throw "PR $($Plan.number) activity changed: updatedAt was $($Plan.snapshot.updatedAt), now $($live.updatedAt)."
    }
    if ([int]$live.issueCommentCount -ne [int]$Plan.snapshot.issueCommentCount) {
        throw "PR $($Plan.number) activity changed: issueCommentCount was $($Plan.snapshot.issueCommentCount), now $($live.issueCommentCount)."
    }

    $expectedReviewCount = [int]$Plan.snapshot.reviewCount
    $expectedReviewCommentCount = [int]$Plan.snapshot.reviewCommentCount
    if ($null -ne $PendingReviewId) {
        $expectedReviewCount++
        $expectedReviewCommentCount += @($Plan.comments).Count
    }
    if ([int]$live.reviewCount -ne $expectedReviewCount) {
        throw "PR $($Plan.number) review activity changed: expected $expectedReviewCount reviews, found $($live.reviewCount)."
    }
    if ([int]$live.reviewCommentCount -ne $expectedReviewCommentCount) {
        throw "PR $($Plan.number) inline review activity changed: expected $expectedReviewCommentCount comments, found $($live.reviewCommentCount)."
    }
}

function Assert-PendingReview {
    param(
        [Parameter(Mandatory)]$Plan,
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][long]$ReviewId
    )

    $review = Invoke-GhGet -Endpoint "repos/$Repository/pulls/$($Plan.number)/reviews/$ReviewId"
    $actualBody = if ($null -eq $review.body) { '' } else { [string]$review.body }
    if ($actualBody -ne [string]$Plan.body) {
        throw "PR $($Plan.number) pending review body changed during GitHub rendering."
    }
    if ([string]$review.commit_id -ne [string]$Plan.headSha) {
        throw "PR $($Plan.number) pending review targets commit $($review.commit_id), not $($Plan.headSha)."
    }
    foreach ($errorMessage in Get-PublicTextErrors -Text $actualBody -Label "PR $($Plan.number) rendered review body") {
        throw $errorMessage
    }

    $actualComments = @(Get-GhPagedItems -Endpoint "repos/$Repository/pulls/$($Plan.number)/reviews/$ReviewId/comments")
    if ($actualComments.Count -ne @($Plan.comments).Count) {
        throw "PR $($Plan.number) pending review comment count does not match the approved payload."
    }

    for ($index = 0; $index -lt $actualComments.Count; $index++) {
        $actual = $actualComments[$index]
        $expected = @($Plan.comments)[$index]
        foreach ($propertyName in @('path', 'line', 'side', 'body')) {
            if ([string]$actual.$propertyName -ne [string]$expected.$propertyName) {
                throw "PR $($Plan.number) pending comment $index changed property $propertyName during GitHub rendering."
            }
        }

        $expectedStartLine = if ($null -eq $expected.start_line) { '' } else { [string]$expected.start_line }
        $actualStartLine = if ($null -eq $actual.start_line) { '' } else { [string]$actual.start_line }
        if ($expectedStartLine -ne $actualStartLine) {
            throw "PR $($Plan.number) pending comment $index changed start_line during GitHub rendering."
        }

        foreach ($errorMessage in Get-PublicTextErrors -Text ([string]$actual.body) -Label "PR $($Plan.number) rendered comment $index") {
            throw $errorMessage
        }
    }

    return $review
}

function Find-MatchingPendingReview {
    param(
        [Parameter(Mandatory)]$Plan,
        [Parameter(Mandatory)][string]$Repository
    )

    $login = (& gh api user --jq '.login').Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($login)) {
        throw 'Could not resolve the authenticated GitHub login.'
    }

    $matches = @(
        Get-GhPagedItems -Endpoint "repos/$Repository/pulls/$($Plan.number)/reviews" |
            Where-Object {
                $_.user.login -eq $login -and
                $_.state -eq 'PENDING' -and
                $_.commit_id -eq $Plan.headSha -and
                [string]$_.body -eq [string]$Plan.body
            }
    )
    $verifiedMatches = @(
        foreach ($match in $matches) {
            try {
                Assert-PendingReview -Plan $Plan -Repository $Repository -ReviewId ([long]$match.id) | Out-Null
                $match
            }
            catch {
                continue
            }
        }
    )

    if ($verifiedMatches.Count -gt 1) {
        throw "PR $($Plan.number) has multiple matching pending reviews; resolve them manually."
    }

    return @($verifiedMatches | Select-Object -First 1)
}

$repository = [string]$reviewData.repository
foreach ($plan in $plans) {
    if ($plan.action -eq 'hold') {
        continue
    }

    if ($plan.action -in @('close', 'custom')) {
        throw "PR $($plan.number) action '$($plan.action)' requires explicit manual handling; the safe publisher posts reviews only."
    }

    if ($plan.action -notin @('comment', 'request-changes')) {
        throw "PR $($plan.number) has unsupported publish action '$($plan.action)'."
    }

    if ([string]::IsNullOrWhiteSpace([string]$plan.body) -and @($plan.comments).Count -eq 0) {
        continue
    }

    $records = @(Get-PublishRecord -PullRequestNumber $plan.number)
    if ($records.Count -gt 1) {
        throw "Publish state contains duplicate records for PR $($plan.number)."
    }

    $record = if ($records.Count -eq 1) {
        $records[0]
    }
    else {
        [pscustomobject]@{
            number = $plan.number
            status = 'new'
            pendingReviewId = $null
            submittedReviewId = $null
            url = $null
        }
    }

    if ($record.status -eq 'submitted') {
        continue
    }

    if ($null -eq $record.pendingReviewId) {
        $orphanedPending = @(Find-MatchingPendingReview -Plan $plan -Repository $repository)
        if ($orphanedPending.Count -eq 1) {
            $record.pendingReviewId = [long]$orphanedPending[0].id
            $record.status = 'pending'
            $record.url = [string]$orphanedPending[0].html_url
        }
        else {
            Assert-FreshSnapshot -Plan $plan -Repository $repository
            $payload = [ordered]@{
                commit_id = $plan.headSha
                comments = @($plan.comments)
            }
            if (-not [string]::IsNullOrWhiteSpace([string]$plan.body)) {
                $payload.body = [string]$plan.body
            }

            $pending = Invoke-GhJsonInput -Endpoint "repos/$repository/pulls/$($plan.number)/reviews" -Payload $payload
            $record.pendingReviewId = [long]$pending.id
            $record.status = 'pending'
            $record.url = [string]$pending.html_url
        }
        if ($records.Count -eq 0) {
            $state.prs = @($state.prs) + $record
        }
        Save-PublishState
    }

    try {
        $currentReview = Assert-PendingReview -Plan $plan -Repository $repository -ReviewId ([long]$record.pendingReviewId)
    }
    catch {
        Invoke-GhJsonInput -Endpoint "repos/$repository/pulls/$($plan.number)/reviews/$($record.pendingReviewId)" -Payload @{} -Method DELETE | Out-Null
        $record.pendingReviewId = $null
        $record.status = 'verification-failed'
        Save-PublishState
        throw
    }

    if ($currentReview.state -ne 'PENDING') {
        if ($currentReview.state -notin @('COMMENTED', 'CHANGES_REQUESTED')) {
            throw "PR $($plan.number) review $($record.pendingReviewId) is in unexpected state '$($currentReview.state)'."
        }

        $record.submittedReviewId = [long]$currentReview.id
        $record.status = 'submitted'
        $record.url = [string]$currentReview.html_url
        Save-PublishState
        continue
    }

    Assert-FreshSnapshot -Plan $plan -Repository $repository -PendingReviewId ([long]$record.pendingReviewId)

    $submitPayload = @{ event = $plan.event }
    if (-not [string]::IsNullOrWhiteSpace([string]$plan.body)) {
        $submitPayload.body = [string]$plan.body
    }
    $submitted = Invoke-GhJsonInput -Endpoint "repos/$repository/pulls/$($plan.number)/reviews/$($record.pendingReviewId)/events" -Payload $submitPayload
    $record.submittedReviewId = [long]$submitted.id
    $record.status = 'submitted'
    $record.url = [string]$submitted.html_url
    Save-PublishState
}

$state | ConvertTo-Json -Depth 20
