---
name: pr-review
description: Comprehensive pull request review with multi-step analysis and comment posting. Use when asked to review a PR, analyze pull request changes, check PR for issues, post review comments, validate PR quality, run code review on a PR, or audit pull request. Generates 13 review step files covering functionality, security, performance, accessibility, and more.
license: Complete terms in LICENSE.txt
---

# PR Review Skill

Perform comprehensive pull request reviews with multi-step analysis covering functionality, security, performance, accessibility, localization, and more.

## Skill Contents

This skill is **self-contained** with all required resources:

```
.github/skills/pr-review/
├── SKILL.md              # This file
├── LICENSE.txt           # MIT License
├── scripts/
│   ├── Start-PRReviewWorkflow.ps1    # Main review script
│   ├── Get-GitHubPrFilePatch.ps1     # Fetch PR file diffs
│   ├── Get-GitHubRawFile.ps1         # Download repo files
│   ├── Get-PrIncrementalChanges.ps1  # Detect incremental changes
│   └── Test-IncrementalReview.ps1    # Test incremental detection
└── references/
    ├── review-pr.prompt.md           # Full review prompt
    └── fix-pr-active-comments.prompt.md  # Comment fix prompt
```

## Output Directory

All generated artifacts are placed under `Generated Files/prReview/<pr-number>/` at the repository root (gitignored).

```
Generated Files/prReview/
└── <pr-number>/
    ├── 00-OVERVIEW.md           # Summary with all findings
    ├── 01-functionality.md      # Functional correctness
    ├── 02-compatibility.md      # Breaking changes, versioning
    ├── 03-performance.md        # Performance implications
    ├── 04-accessibility.md      # A11y compliance
    ├── 05-security.md           # Security concerns
    ├── 06-localization.md       # L10n readiness
    ├── 07-globalization.md      # G11n considerations
    ├── 08-extensibility.md      # API/extension points
    ├── 09-solid-design.md       # SOLID principles
    ├── 10-repo-patterns.md      # PowerToys conventions
    ├── 11-docs-automation.md    # Documentation coverage
    ├── 12-code-comments.md      # Code comment quality
    └── 13-copilot-guidance.md   # (if applicable)
```

## When to Use This Skill

- Review a specific pull request
- Analyze PR changes for quality issues
- Post review comments on a PR
- Validate PR against PowerToys standards
- Run comprehensive code review

## Prerequisites

- GitHub CLI (`gh`) installed and authenticated
- PowerShell 7+ for running scripts
- GitHub MCP configured (for posting comments)

## Required Variables

⚠️ **Before starting**, confirm `{{PRNumber}}` with the user. If not provided, **ASK**: "What PR number should I review?"

| Variable | Description | Example |
|----------|-------------|---------|
| `{{PRNumber}}` | Pull request number to review | `45234` |

## Workflow

### Step 1: Run PR Review

Execute the review workflow (use paths relative to this skill folder):

```powershell
# From repo root
.github/skills/pr-review/scripts/Start-PRReviewWorkflow.ps1 -PRNumbers {{PRNumber}} -CLIType copilot
```

This will:
1. Optionally assign GitHub Copilot as reviewer
2. Fetch PR diff and changed files
3. Generate 13 review step files
4. Post findings as review comments

### Step 2: Review Output

Check the generated files at `Generated Files/prReview/{{PRNumber}}/`

## CLI Options

| Parameter | Description | Default |
|-----------|-------------|---------|
| `-PRNumbers` | PR number(s) to review | From worktrees |
| `-CLIType` | AI CLI to use: `copilot` or `claude` | `copilot` |
| `-MinSeverity` | Min severity to post: `high`, `medium`, `low`, `info` | `medium` |
| `-SkipAssign` | Skip assigning Copilot as reviewer | `false` |
| `-SkipReview` | Skip the review step | `false` |
| `-SkipFix` | Skip the fix step | `false` |
| `-MaxParallel` | Maximum parallel jobs | `3` |
| `-Force` | Skip confirmation prompts | `false` |

## AI Prompt References

For manual AI invocation, prompts are at:
- `references/review-pr.prompt.md` - Full review instructions
- `references/fix-pr-active-comments.prompt.md` - Comment fix instructions

## Troubleshooting

| Problem | Solution |
|---------|----------|
| PR not found | Verify PR number: `gh pr view {{PRNumber}}` |
| Review incomplete | Check `_copilot-review.log` for errors |
| Comments not posted | Ensure GitHub MCP is configured |
