---
name: community-pr-review
description: Review community bug-fix PRs with 7-dimension code review, automated review→fix loop, build verification, and GitHub suggested changes generation. Use when asked to review a community PR, check a bug fix PR, verify a PR builds, generate review comments, generate suggested changes, or audit a community contribution.
license: Complete terms in LICENSE.txt
---

# Community PR Review Skill

**Review**, **fix**, and **build-verify** community bug-fix PRs via an automated review→fix loop. Produces GitHub suggested changes, a build report, and end-to-end verification instructions.

## Skill Contents

```
.github/skills/community-pr-review/
├── SKILL.md                              # This file
├── LICENSE.txt                           # MIT License
├── scripts/
│   ├── Start-CommunityPRReview.ps1       # Main orchestrator (loop-aware)
│   ├── Build-PRBranch.ps1                # Build verification helper
│   ├── Format-SuggestedChanges.ps1       # Generate GitHub suggestion blocks from diff
│   └── ReviewLib.ps1                     # Shared helpers
└── references/
    ├── review-community-pr.prompt.md     # Detailed review prompt with loop protocol
    └── review-dimensions.md              # Review criteria reference
```

## When to Use This Skill

- Review a community-contributed bug-fix PR
- Verify a PR builds cleanly against current main
- Generate review comments ready to post on GitHub
- Get end-to-end verification instructions for a bug fix
- Audit a community contribution before merging

## Prerequisites

- GitHub CLI (`gh`) installed and authenticated
- PowerShell 7+
- Visual Studio 2022 17.4+ or Visual Studio 2026 (for build verification)
- Git submodules initialized (`git submodule update --init --recursive`)

## Quick Start

### Option A: Run the orchestrator script

```powershell
.github/skills/community-pr-review/scripts/Start-CommunityPRReview.ps1 -PRNumber 45234
```

### Option B: Use the agent directly

Invoke the `ReviewCommunityPR` agent with a PR number:
```
Review community PR #45234
```

### Option C: Use the prompt

Run `.github/prompts/review-community-pr.prompt.md` with a PR number.

## CLI Options

| Parameter | Description | Default |
|-----------|-------------|---------|
| `-PRNumber` | PR number to review **(required)** | — |
| `-MaxIterations` | Max review→fix loop iterations | `3` |
| `-SkipBuild` | Skip build verification | `false` |
| `-OutputRoot` | Output root folder | `Generated Files/communityPrReview` |
| `-Force` | Re-review PRs that already have output | `false` |

## Workflow

### Phase 1: Understand the PR
1. Fetch PR metadata via `gh pr view`
2. Read linked issue to understand the bug
3. Fetch full diff and changed file list
4. Record original PR head SHA as baseline

### Phase 2: Checkout and Initial Build
1. `gh pr checkout <PR>`
2. `tools/build/build-essentials.cmd` → `tools/build/build.cmd`
3. If build fails, try merging main and rebuilding

### Phase 3: Review→Fix Loop (max 3 iterations)

```
┌─────────────────────────┐
│  Code Review             │ ← 7 dimensions: correctness, security, perf, ...
│  Write review-comments   │   Each finding includes ```suggestion``` block
└──────────┬──────────────┘
           │
     High/Medium findings?
           │
    ┌──────┴──────┐
    │ YES         │ NO → Exit loop
    ▼             │
┌──────────────┐  │
│  Apply Fixes │  │ ← Fix high/medium findings in worktree
│  Build Check │  │ ← Verify build passes
│  Record in   │  │
│  fix-summary │  │
└──────┬───────┘  │
       │          │
       ▼          │
   (loop back     │
    to review)    │
       │          │
    Max 3x ───────┘
```

Review across 7 dimensions:

| # | Dimension | Key Checks |
|---|-----------|------------|
| 1 | **Correctness** | Fix solves the bug, edge cases handled, no regressions |
| 2 | **Security** | Input validation, no injection, safe elevation, memory safety |
| 3 | **Performance** | No hot-path regressions, efficient patterns, proper async |
| 4 | **Reliability** | Error handling, race conditions, resource disposal, null checks |
| 5 | **Design** | SOLID principles, appropriate scope, no over-engineering |
| 6 | **Compatibility** | No breaking changes, backward compat, API stability |
| 7 | **Repo Patterns** | PowerToys conventions, style, module interface compliance |

Each finding includes: file, line range, severity, dimension, and a ` ```suggestion ` code block with replacement code.

### Phase 4: Generate Suggested Changes
Compare worktree (with all fixes) against original PR head SHA.
Format each changed hunk as a GitHub suggested change with ` ```suggestion ` blocks.

### Phase 5: Verification Guide
Generate step-by-step instructions:
- How to reproduce the original bug
- How to verify the fix works
- Expected vs actual behavior
- Edge cases to test
- Regression areas to smoke-test

## Output

All outputs go to `Generated Files/communityPrReview/<PR>/`:

| File | Description |
|------|-------------|
| `review-comments.md` | Review findings per iteration with ` ```suggestion ` blocks |
| `fix-summary.md` | Record of all fixes applied across iterations |
| `suggested-changes.md` | Final GitHub suggested changes (diff between original and fixed) |
| `suggested-changes.json` | Machine-readable suggestions for API posting |
| `build-report.md` | Build status, errors encountered, fix-up actions taken |
| `verification-guide.md` | E2E verification steps and expected behavior |
| `.signal` | Completion signal for tooling |

### Signal File Format

```json
{
  "status": "success",
  "prNumber": 45234,
  "originalHeadSha": "abc123...",
  "iterations": 2,
  "reviewFindings": { "high": 0, "medium": 0, "low": 1, "info": 3 },
  "fixesApplied": 2,
  "suggestedChanges": 2,
  "buildStatus": "success",
  "buildActions": [],
  "timestamp": "2026-04-07T10:05:23Z"
}
```

Status values: `success`, `partial` (review done, build failed), `failure`

## If You ARE the Reviewer

When running inside Copilot CLI or Claude CLI (i.e., you were invoked by the orchestrator), follow [review-community-pr.prompt.md](./references/review-community-pr.prompt.md) directly. It tells you:

1. Fetch PR data and record original head SHA
2. Checkout and do initial build
3. Review→fix loop (max 3 iterations):
   - Review across all 7 dimensions with ` ```suggestion ` blocks
   - Apply fixes for high/medium findings
   - Re-review until clean
4. Generate suggested changes from diff
5. Write build-report.md
6. Generate verification-guide.md
7. Write `.signal`

See [review-dimensions.md](./references/review-dimensions.md) for detailed criteria per dimension.

## Related Skills

| Skill | Purpose |
|-------|---------|
| `pr-review` | Full 13-step PR review (more comprehensive, less bug-fix focused) |
| `pr-fix` | Fix review comments after review identifies issues |
