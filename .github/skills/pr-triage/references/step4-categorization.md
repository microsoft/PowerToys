# Step 4: Categorization

**Script:** `Invoke-PrCategorization.ps1`

Loads AI dimension scores from Step 3, enriches each PR with live GitHub API data,
and assigns one of 14 triage categories.

## Inputs

| Source | File | Contents |
|--------|------|----------|
| Step 1 | `all-prs.json` | Base PR metadata (number, title, author, labels, dates, diff stats) |
| Step 2 | `Generated Files/prReview/<N>/` | Per-PR review output (`.signal`, `00-OVERVIEW.md`, step files with `mcp-review-comment` blocks) |
| Step 3 | `ai-enrichment.json` | Per-PR dimension scores, suggested category, tags, discussion summary |

## Phase 1 — Parallel Enrichment

Each PR is enriched via `gh api` in parallel (`ForEach-Object -Parallel`).
The parallel block cannot call module functions, so API calls are inlined.

| API call | Data extracted |
|----------|----------------|
| `repos/{repo}/pulls/{n}/reviews` | `ApprovalCount`, `ChangesRequestedCount`, `ReviewerLogins` |
| `repos/{repo}/issues/{n}/comments` | `CommentCount`, `LastCommentAt`, `AuthorLastActivityAt` |
| `repos/{repo}/commits/{ref}/check-runs` | `ChecksStatus` (SUCCESS/FAILURE/PENDING/UNKNOWN), `FailingChecks` |
| `repos/{repo}/pulls/{n}/commits` | `LastCommitAt` |

Results are stored in a `ConcurrentDictionary[int, PSObject]`.

## Phase 2 — Categorization

After enrichment, each PR is categorized sequentially.
The script also loads review findings from Step 2 (`Get-ReviewFindings`).

### Priority: AI dimensions → AI suggestion → deterministic rules

1. **If AI dimensions exist** (from `ai-enrichment.json`): use `Get-CategoryFromDimensions`
2. **Otherwise**: fall back to `Get-CategoryFromRules` (deterministic)

### AI Dimension Rules (`Get-CategoryFromDimensions`)

Applied in priority order. First match wins.
Dimension abbreviations: `sup` = superseded, `mr` = merge_readiness, `rs` = review_sentiment,
`ch` = code_health, `ar` = author_responsiveness, `al` = activity_level, `dc` = direction_clarity.
Missing dimensions default to 0.5. Neutral band: 0.45–0.55 treated as "no signal" (replaces fragile `== 0.5`).

| Rule | Condition | Category |
|------|-----------|----------|
| R-AI-1 | `sup ≥ 0.7` | `superseded` |
| R-AI-2 | `al ≤ 0.2 AND ar ≤ 0.2` | `likely-abandoned` |
| R-AI-3 | `mr ≥ 0.8 AND rs ≥ 0.7 AND ch ≥ 0.7` | `ready-to-merge` |
| R-AI-4 | `rs ≥ 0.7 AND 0.5 ≤ mr < 0.8 AND ch ≥ 0.4` | `approved-pending-merge` |
| R-AI-5 | `ch ≤ 0.3 AND mr ≤ 0.3` | `build-failures` |
| R-AI-6 | `rs ≤ 0.3` | `review-concerns` |
| R-AI-7 | `dc ≤ 0.3` | `design-needed` |
| R-AI-8 | `dc ≤ 0.5 AND rs ≤ 0.5` | `direction-unclear` |
| R-AI-9 | `ch ≤ 0.3` | `review-concerns` (reduced confidence) |
| R-AI-10 | `ar ≤ 0.3 AND al ≥ 0.3` | `awaiting-author` |
| R-AI-11 | `al ≤ 0.3 AND rs outside neutral band` | `stale-with-feedback` |
| R-AI-12 | `al ≤ 0.3 AND rs in neutral band` | `stale-no-review` |
| R-AI-13 | `al ≥ 0.6 AND rs in neutral band` | `fresh-awaiting-review` |
| R-AI-14 | `al ≥ 0.4 AND rs ≥ 0.5` | `in-active-review` |
| R-AI-15 | *(none matched)* | AI's `suggested_category` or `needs-attention` |

Design principles:
- Terminal states first (superseded, abandoned) — no point evaluating quality on dead PRs
- Positive outcomes next (ready, approved) — with code-health guards
- Technical blockers (build failures) — no sentiment gate
- Human blockers split: reviewer pushback (R-AI-6) vs AI-detected code issues (R-AI-9, lower confidence)
- Activity buckets use neutral band instead of fragile float equality

**Enrichment cross-check** (applied after dimension rules in the caller):
- `ready-to-merge` + CI actually FAILURE → `build-failures` (source: `ai-corrected`)
- `ready-to-merge` + changes requested > 0 → `review-concerns` (source: `ai-corrected`)

Confidence = average of all dimension confidence scores.
Source = `ai-dimensions` (R-AI-1–14), `ai-suggested` (R-AI-15 with suggestion), `ai-fallback`, or `ai-corrected`.

### Deterministic Fallback Rules (`Get-CategoryFromRules`)

Used when AI categorization is unavailable. Based on enrichment data and PR dates.

| Rule | Condition | Category | Confidence |
|------|-----------|----------|------------|
| 1 | Approved + CI green + mergeable + no changes requested | `ready-to-merge` | 0.80 |
| 2 | CI failing | `build-failures` | 0.85 |
| 3 | Approved + CI not failing + no changes requested | `approved-pending-merge` | 0.70 |
| 4 | Changes requested + author silent ≥ 14 days | `awaiting-author` | 0.75 |
| 5 | No activity ≥ 90 days | `likely-abandoned` | 0.80 |
| 6 | No activity ≥ 30 days + has reviews | `stale-with-feedback` | 0.65 |
| 7 | No activity ≥ 30 days + no reviews | `stale-no-review` | 0.65 |
| 8 | Changes requested + author responded within 14 days | `review-concerns` | 0.60 |
| 9 | Age ≤ 7 days + no reviews | `fresh-awaiting-review` | 0.70 |
| 10 | Activity ≤ 7 days + has comments or reviews | `in-active-review` | 0.60 |
| 11 | No reviews + age 7–30 days | `stale-no-review` | 0.50 |
| 12 | Has comments + no formal reviews + activity ≤ 14 days | `in-active-review` | 0.40 |
| 13 | *(none matched)* | `needs-attention` | 0.30 |

## Phase 3 — Assembly

`New-CategorizedPr` merges all data into the final per-PR object:

### Review Findings (`Get-ReviewFindings`)

Parses Step 2 prReview output for each PR:
- Reads `.signal` file for review signal
- Parses `mcp-review-comment` JSON blocks from step markdown files
- Counts findings by severity (high / medium / low)

### Effort Estimation (`Get-EffortEstimate`)

| Condition | Effort |
|-----------|--------|
| No review data | `unknown` |
| 0 findings | `trivial` |
| ≥ 3 high | `rework` |
| ≥ 1 high + ≥ 2 medium | `major` |
| ≥ 1 high OR ≥ 3 medium | `moderate` |
| ≥ 1 medium | `minor` |
| Else | `trivial` |

### Signals and Tags

**Signals** (quick-scan indicators):
- ✅ N approvals, ❌ N changes requested, 🔴 CI failing, 🟢 CI passing, 🔥 N high-sev, 💤 N days stale

**Tags** (from AI + computed):
- AI-supplied tags from Step 3
- `large-pr` if additions + deletions ≥ 500
- `review-high-severity` if any high-severity findings
- `review-clean` if review exists with 0 findings

## Output

`categorized-prs.json` — top-level schema:

```json
{
  "CategorizedAt": "ISO-8601",
  "Repository": "microsoft/PowerToys",
  "TotalCount": 42,
  "CategoryCounts": { "ready-to-merge": 3, "review-concerns": 8, ... },
  "Prs": [ ... ]
}
```

Per-PR fields:

| Field | Source |
|-------|--------|
| `Number`, `Title`, `Author`, `Url`, `Labels`, `LinkedIssues`, `Additions`, `Deletions`, `ChangedFiles` | Step 1 |
| `AgeInDays`, `DaysSinceActivity` | Computed from dates |
| `Category`, `Confidence`, `CategorizationSource` | Phase 2 |
| `Signals`, `Tags`, `Effort`, `EffortLabel` | Phase 3 |
| `DimensionScores`, `DiscussionSummary`, `SupersededBy` | Step 3 (AI) |
| `ChecksStatus`, `FailingChecks`, `ApprovalCount`, `ChangesRequestedCount` | Phase 1 enrichment |
| `ReviewData` | Step 2 review findings (severity counts, signal, summaries) |

## 14 Categories

| Category | Description |
|----------|-------------|
| `ready-to-merge` | Approved, CI green, no blockers |
| `approved-pending-merge` | Approved but CI pending or minor gap |
| `build-failures` | CI failing |
| `review-concerns` | Reviewers flagged issues |
| `design-needed` | Needs design discussion |
| `direction-unclear` | Purpose or approach unclear |
| `awaiting-author` | Waiting for author response |
| `fresh-awaiting-review` | New PR, no reviews yet |
| `in-active-review` | Active discussion happening |
| `stale-with-feedback` | Inactive, has reviewer feedback |
| `stale-no-review` | Inactive, never reviewed |
| `likely-abandoned` | No activity for extended period |
| `superseded` | Replaced by another PR |
| `needs-attention` | Fallback — doesn't fit other categories |

## Next Step

→ [Step 5: Reporting](./step5-reporting.md)
