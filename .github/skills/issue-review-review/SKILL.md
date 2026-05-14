---
name: issue-review-review
description: Meta-review of issue-review outputs to validate scoring accuracy and implementation plan quality. Use when asked to verify an issue review, validate review scores, check if implementation plan is sound, audit issue analysis quality, second-opinion on issue feasibility, or ensure review consistency. Outputs a quality score (0-100) and corrective feedback that feeds back into issue-review for re-analysis.
license: Complete terms in LICENSE.txt
---

# Issue Review Review Skill

Validate the quality of `issue-review` outputs by cross-checking scores against evidence, verifying implementation plan correctness, and producing actionable feedback. When the quality score is below 90, the feedback is fed back into `issue-review` to re-run the analysis with corrections.

## Skill Contents

This skill is **self-contained** with all required resources:

```
.github/skills/issue-review-review/
├── SKILL.md                    # This file
├── LICENSE.txt                 # MIT License
├── scripts/
│   ├── Start-IssueReviewReview.ps1         # Main review-review script
│   ├── Start-IssueReviewReviewParallel.ps1 # Parallel runner
│   └── IssueReviewLib.ps1                  # Shared library functions
└── references/
    ├── review-the-review.prompt.md  # AI prompt for meta-review
    └── mcp-config.json              # MCP configuration
```

## Output Directory

All generated artifacts are placed under `Generated Files/issueReviewReview/<issue-number>/` at the repository root (gitignored).

```
Generated Files/issueReviewReview/
└── <issue-number>/
    ├── reviewTheReview.md       # Meta-review with quality score and feedback
    ├── .signal                  # Completion signal for orchestrator
    └── iteration-<N>/           # Previous iteration outputs (if looped)
        └── reviewTheReview.md
```

## Signal File

On completion, a `.signal` file is created for orchestrator coordination:

```json
{
  "status": "success",
  "issueNumber": 45363,
  "timestamp": "2026-02-04T10:05:23Z",
  "qualityScore": 85,
  "iteration": 1,
  "outputs": ["reviewTheReview.md"],
  "needsReReview": true
}
```

Status values: `success`, `failure`

Key fields:
- `qualityScore` (0-100): Overall quality of the original review
- `iteration`: Which review-review pass this is (1, 2, 3...)
- `needsReReview`: `true` if score < 90, meaning `issue-review` should re-run with feedback

## When to Use This Skill

- Validate that an issue review's scores match the evidence
- Check if an implementation plan is technically sound
- Verify that short-term and long-term fix strategies are correct
- Audit review quality before sending issues to `issue-fix`
- Second-opinion on feasibility and clarity assessments
- Quality gate in the issue-to-PR cycle automation

## Prerequisites

- GitHub CLI (`gh`) installed and authenticated
- PowerShell 7+ for running scripts
- Issue must be reviewed first (use `issue-review` skill)
- Copilot CLI or Claude CLI installed

## Required Variables

⚠️ **Before starting**, confirm `{{IssueNumber}}` with the user. If not provided, **ASK**: "What issue number should I review-review?"

| Variable | Description | Example |
|----------|-------------|---------|
| `{{IssueNumber}}` | GitHub issue number whose review to validate | `44044` |

## Workflow

### Step 1: Ensure Issue Is Reviewed

The issue must already have `Generated Files/issueReview/{{IssueNumber}}/overview.md` and `implementation-plan.md`. If not, run `issue-review` first.

### Step 2: Run Review-Review

```powershell
# From repo root
.github/skills/issue-review-review/scripts/Start-IssueReviewReview.ps1 -IssueNumber {{IssueNumber}}
```

This will:
1. Read the original issue from GitHub
2. Read the existing `overview.md` and `implementation-plan.md`
3. Cross-check scores against evidence in the issue
4. Validate implementation plan against codebase
5. Generate `reviewTheReview.md` with quality score and feedback

### Step 3: Check Quality Score

Read the signal file at `Generated Files/issueReviewReview/{{IssueNumber}}/.signal`:

| Quality Score | Action |
|---------------|--------|
| 90-100 | ✅ Review is high quality — proceed to `issue-fix` |
| 70-89 | ⚠️ Review needs improvement — re-run `issue-review` with feedback |
| 50-69 | 🔶 Review has significant issues — re-run with feedback, may need 2 iterations |
| 0-49 | 🔴 Review is poor — re-run with feedback, consider manual review |

### Step 4: Feed Back to Issue-Review (if score < 90)

If `needsReReview` is `true`, re-run issue-review with the feedback file:

```powershell
# Re-run issue-review with feedback from review-review
.github/skills/issue-review/scripts/Start-BulkIssueReview.ps1 -IssueNumber {{IssueNumber}} -FeedbackFile "Generated Files/issueReviewReview/{{IssueNumber}}/reviewTheReview.md" -Force
```

Then re-run the review-review to check if quality improved:

```powershell
.github/skills/issue-review-review/scripts/Start-IssueReviewReview.ps1 -IssueNumber {{IssueNumber}} -Force
```

### Step 5: Loop Until Quality ≥ 90

The orchestrator (`issue-to-pr-cycle`) will loop Steps 2-4 until either:
- Quality score ≥ 90, OR
- Maximum iterations reached (default: 3)

## Batch Review-Review

To review-review multiple issues at once:

```powershell
.github/skills/issue-review-review/scripts/Start-IssueReviewReviewParallel.ps1 -IssueNumbers 44044,32950,45029 -ThrottleLimit 5 -Force
```

## CLI Options

### Start-IssueReviewReview.ps1

| Parameter | Description | Default |
|-----------|-------------|---------|
| `-IssueNumber` | Issue number to review-review | (required) |
| `-CLIType` | AI CLI: `copilot` or `claude` | `copilot` |
| `-Model` | Copilot model to use | (auto) |
| `-Force` | Skip confirmation prompts | `$false` |
| `-DryRun` | Show what would be done | `$false` |

### Start-IssueReviewReviewParallel.ps1

| Parameter | Description | Default |
|-----------|-------------|---------|
| `-IssueNumbers` | Array of issue numbers | (required) |
| `-ThrottleLimit` | Max parallel tasks | `5` |
| `-CLIType` | AI CLI type | `copilot` |
| `-Model` | Copilot model to use | (auto) |
| `-Force` | Skip confirmation prompts | `$false` |

## Quality Dimensions Checked

The meta-review evaluates these dimensions:

| Dimension | What It Checks | Weight |
|-----------|---------------|--------|
| Score Accuracy | Do scores match the evidence cited? | 30% |
| Implementation Correctness | Are the right files/patterns identified? | 25% |
| Risk Assessment | Are risks properly identified and mitigated? | 15% |
| Completeness | Are all aspects covered (perf, security, a11y, i18n)? | 15% |
| Actionability | Can an AI agent execute the plan as written? | 15% |

## AI Prompt Reference

The full prompt template is at [references/review-the-review.prompt.md](./references/review-the-review.prompt.md).
