---
name: issue-to-pr-cycle
description: End-to-end automation from issue analysis to PR creation and review. Use when asked to fix multiple issues automatically, run full issue cycle, batch process issues, automate issue resolution, create PRs for high-confidence issues, or process issues end-to-end. Orchestrates issue review, auto-fix, PR submission, and PR review in parallel batches.
license: Complete terms in LICENSE.txt
---

# Issue-to-PR Full Cycle Skill

Orchestrate the complete workflow from issue analysis to PR creation and review. Processes multiple issues in parallel with configurable confidence thresholds.

## Skill Contents

This skill is **self-contained** with all required resources:

```
.github/skills/issue-to-pr-cycle/
├── SKILL.md              # This file
├── LICENSE.txt           # MIT License
└── scripts/
    └── Start-FullIssueCycle.ps1  # Main orchestration script
```

**Note**: This skill orchestrates other skills via their PowerShell scripts:
- `issue-review` skill scripts
- `issue-fix` skill scripts
- `submit-pr` skill scripts
- `pr-review` skill scripts

## Output

The skill produces:
1. Issue review files in `Generated Files/issueReview/<issue-number>/`
2. Git worktrees with fixes at `Q:/PowerToys-xxxx/`
3. Pull requests on GitHub
4. PR review files in `Generated Files/prReview/<pr-number>/`

## When to Use This Skill

- Process multiple issues end-to-end automatically
- Batch fix high-confidence issues
- Run full automation cycle for triaged issues
- Create PRs for multiple reviewed issues
- Automate issue-to-PR workflow

## Prerequisites

- GitHub CLI (`gh`) installed and authenticated
- Copilot CLI or Claude CLI installed
- PowerShell 7+ for running scripts
- Issues already reviewed (have `Generated Files/issueReview/` data)

## Quick Start

### Option 1: Dry Run First

See what would be processed without making changes:

```powershell
# From repo root
.github/skills/issue-to-pr-cycle/scripts/Start-FullIssueCycle.ps1 `
    -MinFeasibilityScore 70 `
    -MinClarityScore 70 `
    -MaxEffortDays 10 `
    -SkipExisting `
    -DryRun
```

### Option 2: Run Full Cycle

Process all matching issues:

```powershell
.github/skills/issue-to-pr-cycle/scripts/Start-FullIssueCycle.ps1 `
    -MinFeasibilityScore 70 `
    -MinClarityScore 70 `
    -MaxEffortDays 10 `
    -SkipExisting `
    -CLIType copilot `
    -Force
```

## CLI Options

| Parameter | Description | Default |
|-----------|-------------|---------|
| `-MinFeasibilityScore` | Minimum technical feasibility score (0-100) | `70` |
| `-MinClarityScore` | Minimum requirement clarity score (0-100) | `70` |
| `-MaxEffortDays` | Maximum effort estimate in days | `10` |
| `-ExcludeIssues` | Array of issue numbers to skip | `@()` |
| `-SkipExisting` | Skip issues that already have PRs | `false` |
| `-CLIType` | AI CLI to use: `copilot` or `claude` | `copilot` |
| `-FixThrottleLimit` | Parallel limit for fix phase | `5` |
| `-PRThrottleLimit` | Parallel limit for PR phase | `5` |
| `-ReviewThrottleLimit` | Parallel limit for review phase | `3` |
| `-DryRun` | Show what would be done | `false` |
| `-Force` | Skip confirmation prompts | `false` |

## Workflow Phases

```
┌─────────────────────────────────────────────────────────────┐
│  PHASE 1: Auto-Fix Issues (Parallel)                       │
│  Uses: issue-fix skill scripts                              │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│  PHASE 2: Submit PRs (Parallel)                            │
│  Uses: submit-pr skill scripts                              │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│  PHASE 3: Review PRs (Parallel)                            │
│  Uses: pr-review skill scripts                              │
└─────────────────────────────────────────────────────────────┘
```

## Related Skills

This skill orchestrates (via PowerShell, not skill-to-skill):

| Skill | Script Location | Purpose |
|-------|-----------------|---------|
| `issue-review` | `.github/skills/issue-review/scripts/` | Analyze issues |
| `issue-fix` | `.github/skills/issue-fix/scripts/` | Create fixes |
| `submit-pr` | `.github/skills/submit-pr/scripts/` | Create PRs |
| `pr-review` | `.github/skills/pr-review/scripts/` | Review PRs |

You can use each skill independently for finer control.

## Troubleshooting

| Problem | Solution |
|---------|----------|
| No issues found | Lower score thresholds or run more issue reviews |
| All issues skipped | Remove `-SkipExisting` or check for existing PRs |
| Parallel failures | Reduce throttle limits |
