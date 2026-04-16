---
name: community-pr-review
description: Triage and review community PRs using GitHub Copilot cloud review. Use when asked to review a community PR, triage a PR for review readiness, request Copilot review on GitHub, process Copilot review comments locally, auto-fix simple review findings, or iterate on PR review feedback.
license: Complete terms in LICENSE.txt
---

# Community PR Review Skill

**Triage**, **request Copilot cloud review**, **process comments locally**, **build-verify**, and **iterate** on community PRs. Auto-fixes straightforward findings, escalates complex decisions to the maintainer.

## Skill Contents

```
.github/skills/community-pr-review/
├── SKILL.md                              # This file
├── LICENSE.txt                           # MIT License
├── scripts/
│   ├── Start-CommunityPRReview.ps1       # Main orchestrator (loop-aware)
│   ├── Format-SuggestedChanges.ps1       # Generate GitHub suggestion blocks from diff (fallback)
│   └── ReviewLib.ps1                     # Shared helpers
└── references/
    ├── review-community-pr.prompt.md     # Detailed review prompt
    └── review-dimensions.md              # Review criteria reference (for manual use)
```

## When to Use This Skill

- Triage a PR to decide if it's ready for code review
- Review a community-contributed PR using GitHub Copilot cloud review
- Process Copilot review comments: auto-fix easy ones, escalate hard ones
- Verify a PR builds cleanly after applying fixes
- Iterate: push fixes and re-request Copilot review until clean

## Prerequisites

- GitHub CLI (`gh`) installed and authenticated
- GitHub Copilot code review enabled for the repository
- PowerShell 7+
- Visual Studio 2022 17.4+ or Visual Studio 2026 (for build verification)
- Git submodules initialized (`git submodule update --init --recursive`)

## Quick Start

### Option A: Use the agent directly

Invoke the `ReviewCommunityPR` agent with a PR number:
```
Review community PR #45234
```

### Option B: Run the orchestrator script

```powershell
.github/skills/community-pr-review/scripts/Start-CommunityPRReview.ps1 -PRNumber 45234
```

### Option C: Use the prompt

Run `.github/prompts/review-community-pr.prompt.md` with a PR number.

## Workflow Overview

### Phase 1: Triage the PR
Evaluate whether the PR is appropriate for review:
- **Skip** drafts, closed/merged PRs, WIP/experimental PRs
- **Flag** early-stage feature PRs, very large PRs, PRs with merge conflicts
- **Confirm** with user before proceeding

### Phase 2: Setup Local Worktree
1. Determine fork vs same-repo: `gh pr view <PR> --json isCrossRepository,...`
2. Fork: `tools/build/New-WorktreeFromFork.ps1 -Spec <user>:<branch>`
3. Same-repo: `tools/build/New-WorktreeFromBranch.ps1 -Branch <branch>`
4. Initialize submodules

### Phase 3: Request GitHub Copilot Cloud Review
Assign Copilot as a reviewer on the PR via the GitHub API:
```powershell
$repo = (gh repo view --json nameWithOwner --jq '.nameWithOwner')
gh api "repos/$repo/pulls/<PR>/requested_reviewers" -X POST -f 'reviewers[]=copilot'
```

### Phase 4: Wait for and Fetch Comments
Poll until Copilot posts a review, then fetch all inline comments.

### Phase 5: Categorize and Fix

```
┌──────────────────────────────┐
│  Fetch Copilot review        │
│  comments from GitHub        │
└──────────┬───────────────────┘
           │
           ▼
┌──────────────────────────────┐
│  Categorize each comment     │
│  as EASY or HARD             │
└──────────┬───────────────────┘
           │
    ┌──────┴──────┐
    │             │
    ▼             ▼
┌─────────┐  ┌──────────────┐
│  EASY   │  │  HARD        │
│  Auto-  │  │  Present to  │
│  fix    │  │  user, STOP  │
└────┬────┘  └──────┬───────┘
     │               │
     ▼               │ (user provides guidance)
┌──────────────┐     │
│  Build check │     ▼
│  (essentials │  ┌──────────────┐
│   + modules) │  │  Apply user  │
└──────┬───────┘  │  decisions   │
       │          └──────┬───────┘
       │                 │
       ▼                 ▼
┌──────────────────────────────┐
│  Push fixes, request         │
│  Copilot re-review           │
└──────────┬───────────────────┘
           │
       (loop back, max 3x)
```

**Easy comments** (auto-fix): style fixes, missing null checks, unused imports, simple refactors, concrete Copilot suggestions.

**Hard comments** (need human): architecture changes, logic changes, performance trade-offs, multi-file changes, security decisions, ambiguous suggestions.

### Phase 6: Build Verification
After fixes, build in the worktree:
```powershell
Push-Location $prWorktree
tools\build\build-essentials.cmd
tools\build\build.cmd
Pop-Location
```
Fix simple build-breaking changes automatically (max 3 attempts).

### Phase 7: Push and Iterate
Push fixes → request Copilot re-review → repeat (max 3 full iterations).

## Output

All outputs go to `Generated Files/communityPrReview/<PR>/`:

| File | Description |
|------|-------------|
| `copilot-comments.md` | Categorized Copilot review comments per iteration |
| `fix-summary.md` | Record of all fixes applied (auto + human-guided) |
| `build-report.md` | Build status, errors encountered, fix-up actions taken |
| `final-summary.md` | Complete review record across all iterations |
| `.signal` | Completion signal for tooling |

### Signal File Format

```json
{
  "status": "success",
  "prNumber": 45234,
  "iterations": 2,
  "copilotComments": { "total": 12, "autoFixed": 8, "humanGuided": 3, "skipped": 1 },
  "buildStatus": "success",
  "timestamp": "2026-04-14T10:05:23Z"
}
```

Status values: `success`, `partial` (review done, build failed), `failure`

## Related Skills

| Skill | Purpose |
|-------|---------|
| `pr-fix` | Fix review comments after review identifies issues |
