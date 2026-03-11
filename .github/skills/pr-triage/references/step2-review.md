# Step 2: Review — Detailed PR Reviews via pr-review Skill

`Start-PrTriage.ps1` delegates to the **pr-review** skill's `Start-PRReviewWorkflow.ps1` for every PR collected in Step 1.

---

## Delegation

The orchestrator passes the PR numbers collected in Step 1:

```powershell
$reviewPrNumbers = ($allPrsData.Prs | ForEach-Object { [int]$_.Number })

& "$skillRoot/../pr-review/scripts/Start-PRReviewWorkflow.ps1" `
    -PRNumbers $reviewPrNumbers `
    -CLIType $CliType `
    -OutputRoot $ReviewOutputRoot `
    -MaxParallel $ThrottleLimit
```

Review output lands in `$ReviewOutputRoot/{N}/` (owned by pr-review skill).

For the review-pr prompt specification, see [review-pr.prompt.md](../pr-review/references/review-pr.prompt.md).

## Next Step

After reviews complete, proceed to [Step 3: Categorization](./step3-categorization.md) to enrich, classify, and score PRs (incorporating review results).

---

## Action Templates by Category

The review skill produces detailed findings. The templates below describe the **recommended follow-up actions** per category.

### Ready to Merge (`ready-to-merge`)

**Who**: Maintainer with merge permissions

```markdown
- [ ] Verify CI is still green
- [ ] Check for any last-minute comments
- [ ] Merge PR using squash/rebase per repo convention
- [ ] Delete branch if from fork
```

### Build Failures (`build-failures`)

**Who**: PR author (if minor), or maintainer (if author inactive)

**Minor failures** (lint, style, warnings):
```markdown
- [ ] Review failing checks: {list failing checks}
- [ ] Fix: {specific fix suggestion}
- [ ] Consider: Maintainer quick-fix if author inactive 30+ days
```

**Major failures** (compile, tests):
```markdown
- [ ] Identify root cause from CI logs
- [ ] Comment on PR with specific failure details
- [ ] If author inactive: Consider closing with "please reopen when fixed"
```

### Stale — No Review (`stale-no-review`)

**Who**: Maintainer to assign reviewer

```markdown
- [ ] Assign reviewer: {suggested_reviewer} (based on: {reason})
- [ ] Alternative reviewers: {list}
- [ ] Set review deadline: {date}
- [ ] If no reviewer available: Post call for reviewers
```

### Awaiting Author (`awaiting-author`)

**Who**: PR author, or maintainer to close

**Author responsive recently**:
```markdown
- [ ] Ping author: @{author} — friendly reminder about pending changes
- [ ] Summarize outstanding items
- [ ] Set response deadline: {date}
```

**Author unresponsive 30+ days**:
```markdown
- [ ] Post closing notice: "Closing due to inactivity. Please reopen when ready."
- [ ] Close PR
- [ ] Add label: stale-closed
```

**Author unresponsive 60+ days**:
```markdown
- [ ] Close PR with thank-you message
- [ ] If changes valuable: Consider maintainer takeover PR
```

### Direction Unclear (`direction-unclear`)

**Who**: Maintainer or technical lead

```markdown
- [ ] Identify conflicting viewpoints
- [ ] Schedule decision meeting or async discussion
- [ ] Post decision summary to PR
- [ ] Update PR with clear next steps
```

### Design Needed (`design-needed`)

**Who**: PR author with guidance from maintainer

```markdown
- [ ] Request design document covering:
  - Problem statement
  - Proposed solution
  - Alternatives considered
  - Impact on existing functionality
- [ ] Suggest design reviewers
- [ ] Set design deadline
- [ ] Consider: Close PR, request design-first approach
```

### Issue Mismatch (`issue-mismatch`)

**Who**: PR author with maintainer clarification

```markdown
- [ ] Clarify mismatch between issue and PR
- [ ] Options:
  1. Update PR to address issue correctly
  2. Close PR and suggest correct approach
  3. Create new issue for what PR actually fixes
```

### Needs Attention (`needs-attention`)

**Who**: Maintainer to investigate

```markdown
- [ ] Review detailed findings from pr-review output
- [ ] Determine appropriate category after analysis
- [ ] Document findings in triage report
- [ ] Assign specific action based on review results
```

---

## Reviewer Suggestion Algorithm

```powershell
function Get-SuggestedReviewers {
    param($pr)

    $suggestions = @()

    # 1. CODEOWNERS matches
    $codeowners = Get-CodeownersForFiles $pr.changedFiles
    if ($codeowners) {
        $suggestions += @{
            User       = $codeowners[0]
            Reason     = "CODEOWNERS for $($pr.changedFiles[0])"
            Confidence = "High"
        }
    }

    # 2. Recent reviewers of area
    $areaLabel = $pr.labels | Where-Object { $_ -match "^Area-" } | Select-Object -First 1
    if ($areaLabel) {
        $recentReviewers = Get-RecentReviewers -Area $areaLabel -Days 90 -Limit 3
        $suggestions += $recentReviewers | ForEach-Object {
            @{ User = $_; Reason = "Recently reviewed $areaLabel PRs"; Confidence = "Medium" }
        }
    }

    # 3. File history (git blame)
    $topCommitters = Get-TopCommitters -Files $pr.changedFiles -Limit 3
    $suggestions += $topCommitters | ForEach-Object {
        @{ User = $_; Reason = "Frequent committer to changed files"; Confidence = "Medium" }
    }

    # Deduplicate and exclude PR author
    $suggestions |
        Where-Object { $_.User -ne $pr.author } |
        Sort-Object -Property Confidence -Descending |
        Select-Object -First 5
}
```

## Batch Actions

Group similar actions for efficiency:

```markdown
## Batch: Assign Reviewers (8 PRs)

| PR | Suggested Reviewer | Reason | Alt 1 | Alt 2 |
|----|-------------------|--------|-------|-------|
| #12345 | @reviewer1 | CODEOWNERS | @r2 | @r3 |
| #12400 | @reviewer1 | Area-FancyZones | @r2 | @r4 |
```

## Review Output

Each PR reviewed by the pr-review skill produces:

```
Generated Files/prReview/{N}/
├── 00-OVERVIEW.md
├── 01-ANALYSIS.md
└── …
```

The triage summary in `summary.md` can link to these for deep-dive details.
