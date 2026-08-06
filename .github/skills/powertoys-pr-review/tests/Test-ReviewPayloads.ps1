$ErrorActionPreference = 'Stop'

$skillRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $skillRoot 'scripts\ReviewPayload.Common.ps1')

$script:assertions = 0

function Assert-True {
    param(
        [Parameter(Mandatory)][bool]$Condition,
        [Parameter(Mandatory)][string]$Message
    )

    $script:assertions++
    if (-not $Condition) {
        throw "Assertion failed: $Message"
    }
}

function Copy-JsonObject {
    param([Parameter(Mandatory)]$Value)

    return $Value | ConvertTo-Json -Depth 30 | ConvertFrom-Json
}

$headSha = '0123456789abcdef0123456789abcdef01234567'
$suggestionBody = @'
### Keep the value stable

**Severity:** `high`

Changing this value renumbers persisted telemetry. Preserve the existing numeric assignment so upgrades keep the same meaning.

```suggestion
    Existing = 1,
```
'@
$reviewData = [pscustomobject]@{
    schemaVersion = 2
    repository = 'microsoft/PowerToys'
    generatedAt = '2026-08-06T00:00:00Z'
    phase = 'approval'
    prs = @(
        [pscustomobject]@{
            number = 42
            phase = 'ready'
            title = 'Test PR'
            author = 'author'
            headSha = $headSha
            snapshot = [pscustomobject]@{
                updatedAt = '2026-08-06T00:00:00Z'
                issueCommentCount = 1
                reviewCommentCount = 2
                reviewCount = 3
            }
            publicPayload = [pscustomobject]@{
                contextBody = 'Please address the inline correctness issue.'
                items = @(
                    [pscustomobject]@{
                        id = 'fix-value'
                        kind = 'inline'
                        severity = 'high'
                        title = 'Keep the value stable'
                        path = 'src/Test.cs'
                        startLine = 2
                        line = 2
                        side = 'RIGHT'
                        body = $suggestionBody
                    },
                    [pscustomobject]@{
                        id = 'companion-tests'
                        kind = 'companion'
                        severity = 'medium'
                        title = 'Add regression coverage'
                        body = "### Add regression coverage`n`n**Severity:** ``medium```n`nPlease add a regression test that verifies the persisted value during upgrade."
                    }
                )
            }
            internalEvidence = [pscustomobject]@{
                validationRepository = 'https://github.com/example/PowerToys'
                worktree = 'C:\Internal'
            }
        }
    )
}

$liveData = @{
    42 = [pscustomobject]@{
        headSha = $headSha
        updatedAt = '2026-08-06T00:00:00Z'
        issueCommentCount = 1
        reviewCommentCount = 2
        reviewCount = 3
        files = @(
            [pscustomobject]@{
                filename = 'src/Test.cs'
                patch = "@@ -1,3 +1,3 @@`n context`n-old`n+    Existing = 1,`n context"
            }
        )
    }
}

$errors = @(Test-ReviewDataDocument -Document $reviewData)
Assert-True ($errors.Count -eq 0) "Valid review data should pass: $($errors -join '; ')"

$errors = @(Test-ReviewDataDocument -Document $reviewData -CheckGitHub -LiveData $liveData)
Assert-True ($errors.Count -eq 0) "Valid current diff range should pass: $($errors -join '; ')"

$invalid = Copy-JsonObject $reviewData
$invalid.prs[0].publicPayload.items[0].body = 'Prose without an apply-ready block.'
$errors = @(Test-ReviewDataDocument -Document $invalid)
Assert-True (($errors -join "`n") -match 'exactly one non-empty suggestion block') 'Missing suggestion fences must fail.'

$invalid = Copy-JsonObject $reviewData
$invalid.prs[0].publicPayload.items[0].body += "`nValidated in fork PR 99."
$errors = @(Test-ReviewDataDocument -Document $invalid)
Assert-True (($errors -join "`n") -match 'forbidden fork reference') 'Public fork references must fail.'

$invalid = Copy-JsonObject $reviewData
$invalid.prs[0].publicPayload.contextBody = '@{number=42; contextComment=hello}.contextComment'
$errors = @(Test-ReviewDataDocument -Document $invalid)
Assert-True (($errors -join "`n") -match 'serialization artifact') 'PowerShell object interpolation artifacts must fail.'

$invalid = Copy-JsonObject $reviewData
$invalid.prs[0].publicPayload.contextBody = 'Validated locally at C:\PowerToys-review-42\x64\Debug\PowerToys.exe.'
$errors = @(Test-ReviewDataDocument -Document $invalid)
Assert-True (($errors -join "`n") -match 'local validation provenance|internal local path') 'Internal local validation paths must fail.'

$invalid = Copy-JsonObject $reviewData
$invalid.prs[0].publicPayload.items[0].line = 20
$invalid.prs[0].publicPayload.items[0].startLine = 20
$errors = @(Test-ReviewDataDocument -Document $invalid -CheckGitHub -LiveData $liveData)
Assert-True (($errors -join "`n") -match 'current RIGHT-side diff hunk') 'Out-of-diff ranges must fail.'

$staleLiveData = @{ 42 = Copy-JsonObject $liveData[42] }
$staleLiveData[42].headSha = 'ffffffffffffffffffffffffffffffffffffffff'
$errors = @(Test-ReviewDataDocument -Document $reviewData -CheckGitHub -LiveData $staleLiveData)
Assert-True (($errors -join "`n") -match 'head moved') 'Stale heads must fail.'

$decisions = [pscustomobject]@{
    schemaVersion = 2
    reviewDataHash = 'data-hash'
    submittedAt = '2026-08-06T00:01:00Z'
    prs = @(
        [pscustomobject]@{
            number = 42
            headSha = $headSha
            action = 'comment'
            postContext = $true
            contextBody = 'Please address the inline correctness issue.'
            items = [pscustomobject]@{
                'fix-value' = 'post'
                'companion-tests' = 'post'
            }
            instructions = ''
        }
    )
}

$errors = @(Test-ReviewDecisionDocument -Decisions $decisions -ReviewData $reviewData -ExpectedHash 'data-hash')
Assert-True ($errors.Count -eq 0) "Valid decisions should pass: $($errors -join '; ')"

$invalidDecisions = Copy-JsonObject $decisions
$invalidDecisions.prs[0].action = 'approve'
$errors = @(Test-ReviewDecisionDocument -Decisions $invalidDecisions -ReviewData $reviewData -ExpectedHash 'data-hash')
Assert-True (($errors -join "`n") -match 'cannot approve|unsupported action') 'Approve actions must fail.'

$errors = @(Test-ReviewDecisionDocument -Decisions $decisions -ReviewData $reviewData -ExpectedHash 'other-hash')
Assert-True (($errors -join "`n") -match 'does not match') 'Decision hash mismatches must fail.'

$plan = Get-ApprovedReviewPlan -ReviewData $reviewData -Decision $decisions.prs[0]
$serializedPlan = $plan | ConvertTo-Json -Depth 20
Assert-True ($plan.comments.Count -eq 1) 'The plan should contain the selected inline comment.'
Assert-True ($plan.body -match 'regression test') 'The plan should include the selected companion note.'
Assert-True ($serializedPlan -notmatch 'example/PowerToys|C:\\\\Internal') 'Internal evidence must never enter the public plan.'

$unicodeBody = 'Use [\x0C\x85] rather than control characters in the regex source.'
$roundTrip = ([pscustomobject]@{ body = $unicodeBody } | ConvertTo-Json | ConvertFrom-Json).body
Assert-True ($roundTrip -ceq $unicodeBody) 'Unicode-safe escape text must round-trip exactly.'
Assert-True (
    (ConvertTo-ReviewTimestamp -Value ([datetimeoffset]'2026-08-06T00:00:00Z')) -eq '2026-08-06T00:00:00.0000000+00:00'
) 'Review timestamps must use a stable UTC representation.'

$now = [datetime]'2026-08-06T00:00:00Z'
Assert-True (
    (Get-ReviewResumeAction -BranchExists $false -ReviewPullRequestExists $false -WorktreePath '' -CopilotReviewCount 0 -UnresolvedThreadCount 0 -LatestCopilotReviewAt $null -NewestCommitAt $null) -eq 'fresh-mirror'
) 'Missing durable traces should start a fresh mirror.'
Assert-True (
    (Get-ReviewResumeAction -BranchExists $false -LocalBranchExists $true -ReviewPullRequestExists $false -WorktreePath 'C:\Review' -CopilotReviewCount 0 -UnresolvedThreadCount 0 -LatestCopilotReviewAt $null -NewestCommitAt $null) -eq 'push-and-create-review-pr'
) 'Local-only progress must be pushed and reused instead of remirrored.'
Assert-True (
    (Get-ReviewResumeAction -BranchExists $true -ReviewPullRequestExists $false -WorktreePath '' -CopilotReviewCount 0 -UnresolvedThreadCount 0 -LatestCopilotReviewAt $null -NewestCommitAt $null) -eq 'create-review-pr'
) 'An existing branch must be reused when creating the review PR.'
Assert-True (
    (Get-ReviewResumeAction -BranchExists $true -ReviewPullRequestExists $true -ReviewPullRequestOpen $false -WorktreePath 'C:\Review' -CopilotReviewCount 2 -UnresolvedThreadCount 0 -LatestCopilotReviewAt $now -NewestCommitAt $now.AddMinutes(-1)) -eq 'reopen-or-create-review-pr'
) 'A closed review PR must be reopened or recreated before resuming.'
Assert-True (
    (Get-ReviewResumeAction -BranchExists $true -ReviewPullRequestExists $true -WorktreePath '' -CopilotReviewCount 1 -UnresolvedThreadCount 0 -LatestCopilotReviewAt $now -NewestCommitAt $now.AddMinutes(-1)) -eq 'create-worktree'
) 'An existing review PR without a worktree should resume at worktree creation.'
Assert-True (
    (Get-ReviewResumeAction -BranchExists $true -ReviewPullRequestExists $true -WorktreePath 'C:\Review' -CopilotReviewCount 2 -UnresolvedThreadCount 1 -LatestCopilotReviewAt $now -NewestCommitAt $now.AddMinutes(-1)) -eq 'resume-review-loop'
) 'Unresolved threads must resume the loop.'
Assert-True (
    (Get-ReviewResumeAction -BranchExists $true -ReviewPullRequestExists $true -WorktreePath 'C:\Review' -CopilotReviewCount 2 -UnresolvedThreadCount 0 -LatestCopilotReviewAt $now -NewestCommitAt $now.AddMinutes(-1)) -eq 'rebuild-and-draft'
) 'A clean durable loop should rebuild before drafting.'

$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ("review-payload-tests-{0}" -f [guid]::NewGuid())
New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null
try {
    $dataPath = Join-Path $temporaryDirectory 'review-data.json'
    $decisionPath = Join-Path $temporaryDirectory 'review-decisions.json'
    $statePath = Join-Path $temporaryDirectory 'state.json'
    Write-JsonFileAtomically -Path $dataPath -Value $reviewData
    $decisions.reviewDataHash = Get-ReviewDataHash -Path $dataPath
    Write-JsonFileAtomically -Path $decisionPath -Value $decisions

    $dryRunOutput = & (Join-Path $skillRoot 'scripts\Publish-ApprovedReview.ps1') -DataPath $dataPath -DecisionsPath $decisionPath -DryRun -Offline
    $dryRunPlan = $dryRunOutput | ConvertFrom-Json
    Assert-True ($dryRunPlan.comments.Count -eq 1) 'Publisher dry-run should preserve the validated inline payload.'
    Assert-True (($dryRunOutput -join "`n") -notmatch 'example/PowerToys|C:\\\\Internal') 'Publisher dry-run must exclude internal evidence.'

    $state = [pscustomobject]@{
        schemaVersion = 1
        reviewDataHash = $decisions.reviewDataHash
        prs = @([pscustomobject]@{ number = 42; status = 'pending'; pendingReviewId = 123 })
    }
    Write-JsonFileAtomically -Path $statePath -Value $state
    $resumed = Read-JsonFile -Path $statePath
    $resumed.prs[0].status = 'submitted'
    Write-JsonFileAtomically -Path $statePath -Value $resumed
    Assert-True ((Read-JsonFile -Path $statePath).prs[0].status -eq 'submitted') 'Atomic state must preserve partial-run resume progress.'
}
finally {
    if (Test-Path -LiteralPath $statePath) {
        Remove-Item -LiteralPath $statePath
    }
    if (Test-Path -LiteralPath $decisionPath) {
        Remove-Item -LiteralPath $decisionPath
    }
    if (Test-Path -LiteralPath $dataPath) {
        Remove-Item -LiteralPath $dataPath
    }
    if (Test-Path -LiteralPath $temporaryDirectory) {
        Remove-Item -LiteralPath $temporaryDirectory
    }
}

Write-Host "Review payload tests passed ($script:assertions assertions)." -ForegroundColor Green
