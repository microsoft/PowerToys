# Step 5: Reporting — Generate Triage Reports

`Export-TriageReport.ps1` produces a human-readable summary and per-category markdown files from `categorized-prs.json`. This is the final step — AI enrichment (Step 3) and categorization (Step 4) are already complete.

---

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `-InputPath` | string | Yes | Path to `categorized-prs.json` from Step 4 |
| `-OutputDir` | string | Yes | Run output directory (e.g., `Generated Files/pr-triage/2026-02-12`) |
| `-Repository` | string | Yes | GitHub repo in `owner/repo` format |
| `-IncludeDetailedReview` | switch | No | Include AI review details in category reports |
| `-PreviousInputPath` | string | No | Path to a previous run's `categorized-prs.json` for delta comparison |

## Report Structure

```
Generated Files/pr-triage/{date}/
├── summary.md                           # Executive summary with deltas
├── categorized-prs.json                 # Machine-readable data (from Step 4)
├── all-prs.json                         # Raw collection data (from Step 1)
├── ai-enrichment.json                   # AI dimension scores (from Step 3)
└── categories/
    ├── fresh-awaiting-review.md
    ├── in-active-review.md
    ├── approved-pending-merge.md
    ├── review-concerns.md
    ├── build-failures.md
    ├── awaiting-author.md
    ├── stale-with-feedback.md
    ├── stale-no-review.md
    ├── direction-unclear.md
    ├── design-needed.md
    ├── likely-abandoned.md
    └── superseded.md
```

## Summary Sections

The `summary.md` report contains these sections in order:

### 1. Header

```markdown
# PR Triage Summary — microsoft/PowerToys

**Generated:** {timestamp}
**Total open PRs:** {n}
**AI categorized:** {n} | **Rule-based:** {n}
```

### 2. Delta Sections (when `-PreviousInputPath` provided)

Four delta sections appear after the header — see [Delta Tracking](#delta-tracking) below.

### 3. Category Breakdown

Bar-chart table linking to per-category reports:

```markdown
| Category | Count | |
|----------|------:|---|
| 🆕 [Fresh - Awaiting Review](categories/fresh-awaiting-review.md) | 30 | █████ |
| 🔧 [Build Failures](categories/build-failures.md) | 20 | ███ |
| ...
```

### 4. 🚨 Critical — Needs Immediate Attention

PRs with high-severity AI review findings, long staleness, or both:

```markdown
| PR | Author | Category | Age | Signals |
|-----|--------|----------|----:|---------|
```

### 5. ⚡ Quick Wins

PRs tagged as `quick-win` by the AI enrichment, sorted by effort level:

```markdown
| PR | Author | Effort | Approvals |
|-----|--------|--------|----------:|
```

### 6. Category Reports (links)

Footer with links to all per-category detail files.

---

## Delta Tracking

When `-PreviousInputPath` is supplied, the report compares the current run against a previous run to show what changed. The orchestrator (`Start-PrTriage.ps1`) automatically finds the most recent previous run folder.

### How It Works

1. **`Get-RunDelta`** compares current vs previous `categorized-prs.json` by PR number
2. **`Format-DeltaMarkdown`** generates four markdown sections from the delta

### Delta Sections

#### 📊 Changes Since Last Run

Overview counts: previous total, current total (with delta), new PRs, closed/merged, category changes, unchanged, recurring action items.

```markdown
| Metric | Count |
|--------|------:|
| Previous total | 119 |
| Current total | 112 (-7) |
| New PRs | 3 |
| Closed/merged | 10 |
| Category changed | 103 |
| Unchanged | 6 |
```

#### 🔀 Category Changes

PRs that changed category between runs. Shows before→after with signal icons:

```markdown
| PR | Author | Before | After | Signals |
|-----|--------|--------|-------|---------|
| [#45506](...) | @jiripolasek | 💬 in-active-review | ✅ approved-pending-merge | ✅1 approvals |
```

#### 🆕 New PRs Since Last Run

PRs present in current but absent from previous:

```markdown
| PR | Author | Category | Age | Signals |
|-----|--------|----------|----:|---------|
```

#### ✅ Closed/Merged Since Last Run

PRs in previous but absent from current (merged or closed):

```markdown
| PR | Author | Was | Age |
|-----|--------|-----|----:|
```

#### ⚠️ Recurring — Action Still Needed

PRs stuck in the same actionable category across both runs. These are items where suggested actions from the previous triage have **not** been taken.

Actionable categories: `review-concerns`, `build-failures`, `awaiting-author`, `stale-no-review`, `stale-with-feedback`, `direction-unclear`, `design-needed`, `needs-attention`.

### Orchestrator Integration

`Start-PrTriage.ps1` automatically finds the previous run:

```powershell
$prevRun = Get-ChildItem $triageRoot -Directory |
    Where-Object { $_.Name -match '^\d{4}-\d{2}-\d{2}$' -and $_.Name -lt $RunDate } |
    Sort-Object Name -Descending | Select-Object -First 1
```

It passes `-PreviousInputPath` pointing to the previous run's `categorized-prs.json`.

---

## Category Report Template

Each `categories/{name}.md` file contains:

1. **Category header** with count
2. **PR table** with columns: PR link+title, author, age, signals (approvals, CI status, high-severity count, staleness)
3. **Per-PR detail blocks** (when `-IncludeDetailedReview` is set): AI review findings, discussion summary, dimension scores

## Report Freshness

- Reports are point-in-time snapshots
- Generation timestamp is shown prominently
- Delta sections highlight what changed since the last run
- Recommend running triage at least weekly

## Done

This is the final pipeline step. Open `summary.md` to review the triage results.
