---
name: issue-review
description: Analyze GitHub issues for feasibility and implementation planning. Use when asked to review an issue, analyze if an issue is fixable, evaluate issue complexity, create implementation plan for an issue, triage issues, assess technical feasibility, or estimate effort for an issue. Outputs structured analysis including feasibility score, clarity score, effort estimate, and detailed implementation plan.
license: Complete terms in LICENSE.txt
---

# Issue Review Skill

Analyze GitHub issues to determine technical feasibility, requirement clarity, and create detailed implementation plans for PowerToys.

## Skill Contents

This skill is **self-contained** with all required resources:

```
.github/skills/issue-review/
├── SKILL.md              # This file
├── LICENSE.txt           # MIT License
├── scripts/
│   ├── IssueReviewLib.ps1      # Shared library functions
│   └── Start-BulkIssueReview.ps1  # Main review script
└── references/
    └── review-issue.prompt.md  # Full AI prompt template
```

## Output Directory

All generated artifacts are placed under `Generated Files/issueReview/<issue-number>/` at the repository root (gitignored).

```
Generated Files/issueReview/
└── <issue-number>/
    ├── overview.md              # High-level assessment with scores
    ├── implementation-plan.md   # Detailed step-by-step fix plan
    └── _raw-issue.json          # Cached issue data from GitHub
```

## When to Use This Skill

- Review a specific GitHub issue for feasibility
- Analyze whether an issue can be fixed by AI
- Create an implementation plan for an issue
- Triage issues by complexity and clarity
- Estimate effort for fixing an issue
- Evaluate technical requirements of an issue

## Prerequisites

- GitHub CLI (`gh`) installed and authenticated
- PowerShell 7+ for running scripts

## Required Variables

⚠️ **Before starting**, confirm `{{IssueNumber}}` with the user. If not provided, **ASK**: "What issue number should I review?"

| Variable | Description | Example |
|----------|-------------|---------|
| `{{IssueNumber}}` | GitHub issue number to analyze | `44044` |

## Workflow

### Step 1: Run Issue Review

Execute the review script (use paths relative to this skill folder):

```powershell
# From repo root
.github/skills/issue-review/scripts/Start-BulkIssueReview.ps1 -IssueNumber {{IssueNumber}}
```

This will:
1. Fetch issue details from GitHub
2. Analyze the codebase for relevant files
3. Generate `overview.md` with feasibility assessment
4. Generate `implementation-plan.md` with detailed steps

### Step 2: Review Output

Check the generated files at `Generated Files/issueReview/{{IssueNumber}}/`:

| File | Contains |
|------|----------|
| `overview.md` | Feasibility score (0-100), Clarity score (0-100), Effort estimate, Risk assessment |
| `implementation-plan.md` | Step-by-step implementation with file paths, code snippets, test requirements |

### Step 3: Interpret Scores

| Score Range | Interpretation |
|-------------|----------------|
| 80-100 | High confidence - straightforward fix |
| 60-79 | Medium confidence - some complexity |
| 40-59 | Low confidence - significant challenges |
| 0-39 | Very low - may need human intervention |

## Batch Review

To review multiple issues at once:

```powershell
.github/skills/issue-review/scripts/Start-BulkIssueReview.ps1 -IssueNumbers 44044, 32950, 45029
```

## AI Prompt Reference

For manual AI invocation, the full prompt is at:
- `references/review-issue.prompt.md` (relative to this skill folder)

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Issue not found | Verify issue number exists: `gh issue view {{IssueNumber}}` |
| No implementation plan | Issue may be unclear - check `overview.md` for clarity score |
| Script errors | Ensure you're in the PowerToys repo root |
