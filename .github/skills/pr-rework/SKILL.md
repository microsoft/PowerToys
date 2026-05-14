---
name: pr-rework
description: Iteratively rework pull requests to production quality using local worktrees. Use when asked to polish a PR, iterate on PR quality, rework a PR locally, fix and re-review a PR until clean, prepare PR for merge, loop review-fix on a PR, or bring multiple PRs to merge-ready state. Creates worktrees, runs pr-review locally (no GitHub posting), applies pr-fix for medium+ issues, builds and runs unit tests, and loops until no actionable findings remain. Supports multiple PRs in parallel with full crash-resume.
license: Complete terms in LICENSE.txt
---

# PR Rework Skill

Iteratively rework pull requests to production quality entirely locally. Creates a worktree per PR, runs review → fix → build/test → re-review loops until the PR is clean, then asks the human to push.

**Key difference from `pr-review` + `pr-fix`**: This skill keeps everything local — no comments posted, no pushes, no thread resolution. The human decides when to push.

### Why a separate local-review prompt?

The standard `pr-review` prompt (`review-pr.prompt.md`) fetches file content and patches from the **GitHub API** (`gh pr view`, `gh api .../pulls/N/files`, `Get-GitHubRawFile.ps1`). This works for remote reviews but **breaks in the rework loop**: after iteration 1 fixes files locally, the remote PR hasn't changed, so pr-review would re-fetch the same stale code and produce identical findings forever.

`rework-local-review.prompt.md` uses `git diff main` (two-dot) and local file reads instead, so it always sees the latest worktree state including uncommitted fix changes. Two-dot diff is critical: three-dot (`main...HEAD`) only shows committed changes and would miss uncommitted fixes from previous iterations. It reuses the same per-step checklists (01-functionality through 13) from pr-review.

## Skill Contents

```
.github/skills/pr-rework/
├── SKILL.md                          # This file
├── LICENSE.txt                       # MIT License
├── references/
│   ├── mcp-config.json               # MCP configuration
│   ├── rework-local-review.prompt.md # AI prompt for LOCAL review (git diff, no GitHub API)
│   └── rework-fix.prompt.md          # AI prompt for local fix pass
└── scripts/
    ├── Start-PRRework.ps1            # Main single-PR orchestrator (review→fix→test loop)
    ├── Start-PRReworkParallel.ps1    # Multi-PR parallel launcher
    ├── Get-PRReworkStatus.ps1        # Check rework state for all PRs
    └── IssueReviewLib.ps1            # Shared helpers (copy)
```

## Output Directory

All artifacts are placed under `Generated Files/prRework/<pr-number>/` (gitignored).

```
Generated Files/prRework/
└── <pr-number>/
    ├── .state.json             # Resumable state (iteration, phase, worktree path)
    ├── worktree-info.json      # Worktree path + branch mapping
    ├── iteration-1/
    │   ├── review/             # pr-review output (00-OVERVIEW.md, step files)
    │   ├── findings.json       # Parsed medium+ findings from review
    │   ├── fix.log             # Copilot CLI fix output
    │   ├── build.log           # Build output
    │   └── test.log            # Unit test output
    ├── iteration-2/
    │   └── ...                 # Same structure per iteration
    ├── summary.md              # Final human-readable summary of all changes
    └── .signal                 # Completion signal for orchestrator
```

## Signal File

```json
{
  "status": "success",
  "prNumber": 45365,
  "timestamp": "2026-02-10T10:05:23Z",
  "iterations": 3,
  "finalFindingsCount": 0,
  "worktreePath": "Q:/PowerToys-ab12"
}
```

Status values: `success` (no findings remain), `max-iterations` (hit limit but improved), `failure`

## State File (`.state.json`) — Crash Resume

```json
{
  "prNumber": 45365,
  "branch": "feature/my-pr",
  "worktreePath": "Q:/PowerToys-ab12",
  "currentIteration": 2,
  "currentPhase": "fix",
  "maxIterations": 5,
  "phaseHistory": [
    { "iteration": 1, "phase": "review", "status": "done", "timestamp": "..." },
    { "iteration": 1, "phase": "fix", "status": "done", "findingsFixed": 4, "timestamp": "..." },
    { "iteration": 1, "phase": "build", "status": "done", "exitCode": 0, "timestamp": "..." },
    { "iteration": 1, "phase": "test", "status": "done", "passed": 42, "failed": 0, "timestamp": "..." },
    { "iteration": 2, "phase": "review", "status": "done", "timestamp": "..." },
    { "iteration": 2, "phase": "fix", "status": "in-progress", "timestamp": "..." }
  ],
  "startedAt": "2026-02-10T10:00:00Z",
  "lastUpdatedAt": "2026-02-10T10:15:00Z"
}
```

On resume, the script reads `.state.json`, finds the last `in-progress` phase, and restarts from there.

## When to Use This Skill

- Polish a PR before requesting human review
- Iterate review/fix cycles locally without posting to GitHub
- Bring multiple PRs to merge-ready quality in parallel
- Prepare PRs for final human sign-off
- Run quality gate loop: review → fix → build → test → repeat

## Prerequisites

- GitHub CLI (`gh`) installed and authenticated
- Copilot CLI or Claude CLI installed
- PowerShell 7+
- PR must be open (not draft)
- `tools/build/WorktreeLib.ps1` available (for worktree management)

## Required Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `{{PRNumbers}}` | One or more PR numbers to rework | `45365, 45366` |

## Workflow

### Single PR

```powershell
# Rework a single PR with default settings
.github/skills/pr-rework/scripts/Start-PRRework.ps1 -PRNumber 45365 -CLIType copilot -Force

# With model override and custom max iterations
.github/skills/pr-rework/scripts/Start-PRRework.ps1 -PRNumber 45365 -CLIType copilot -Model claude-opus-4.6 -MaxIterations 5 -Force
```

### Multiple PRs in Parallel

```powershell
# Rework 3 PRs with throttle limit
.github/skills/pr-rework/scripts/Start-PRReworkParallel.ps1 -PRNumbers 45365,45366,45367 -CLIType copilot -Model claude-opus-4.6 -ThrottleLimit 2 -Force
```

### Check Status

```powershell
# See rework state for all PRs
.github/skills/pr-rework/scripts/Get-PRReworkStatus.ps1
```

### Resume After Crash

The same command resumes from where it left off (reads `.state.json`):

```powershell
# Automatically resumes from last checkpoint
.github/skills/pr-rework/scripts/Start-PRRework.ps1 -PRNumber 45365 -CLIType copilot -Force
```

Use `-Fresh` to discard previous state and start over:

```powershell
.github/skills/pr-rework/scripts/Start-PRRework.ps1 -PRNumber 45365 -CLIType copilot -Fresh -Force
```

## Loop Logic

```
Phase 0 — BUILD ESSENTIALS: One-time tools/build/build-essentials.cmd (NuGet restore)

for each iteration (1..MaxIterations):
  Phase 1 — REVIEW:  Run pr-review locally (git diff main, no GitHub API)
  Phase 2 — PARSE:   Extract medium+ severity findings → findings.json
  Phase 3 — CHECK:   If 0 actionable findings → DONE (success)
  Phase 4 — FIX:     Run Copilot CLI with rework-fix.prompt.md in worktree
  Phase 5 — BUILD:   Run tools/build/build.cmd for ALL changed projects
  Phase 6 — TEST:    Discover & run related unit tests
  → next iteration (build/test failures fed as context to next fix)

FINAL VERIFICATION — One extra review-only pass after last iteration:
  If 0 findings → DONE (success)
  Otherwise → status max-iterations with remaining count
```

Key details:
- **Two-dot diff** (`git diff main`) used everywhere — includes uncommitted fix changes
- **Multi-project build** — ALL changed `.csproj`/`.vcxproj` directories are built, not just the first
- **Cross-iteration error feedback** — build/test failures from iteration N are fed to iteration N+1's fix prompt
- **Final verification** — prevents silent regressions from the last fix pass

## CLI Options

### Start-PRRework.ps1

| Parameter | Description | Default |
|-----------|-------------|---------|
| `-PRNumber` | PR number to rework | (required) |
| `-CLIType` | `copilot` or `claude` | `copilot` |
| `-Model` | Copilot CLI model override | `claude-opus-4.6` |
| `-MaxIterations` | Max review/fix loops | `5` |
| `-MinSeverity` | Minimum severity to fix: `high`, `medium`, `low` | `medium` |
| `-ReviewTimeoutMin` | Timeout for review CLI call (minutes) | `10` |
| `-FixTimeoutMin` | Timeout for fix CLI call (minutes) | `15` |
| `-Force` | Skip confirmation prompts | `false` |
| `-Fresh` | Discard previous state, start over | `false` |
| `-SkipTests` | Skip unit test phase | `false` |

### Start-PRReworkParallel.ps1

| Parameter | Description | Default |
|-----------|-------------|---------|
| `-PRNumbers` | Array of PR numbers | (required) |
| `-ThrottleLimit` | Max concurrent rework jobs | `2` |
| `-CLIType` | `copilot` or `claude` | `copilot` |
| `-Model` | Copilot CLI model override | `claude-opus-4.6` |
| `-MaxIterations` | Max loops per PR | `5` |
| `-MinSeverity` | Minimum severity to fix | `medium` |
| `-ReviewTimeoutMin` | Timeout for review CLI call (minutes) | `10` |
| `-FixTimeoutMin` | Timeout for fix CLI call (minutes) | `15` |
| `-Force` | Skip confirmation | `false` |
| `-Fresh` | Start all PRs fresh | `false` |
| `-SkipTests` | Skip unit test phase | `false` |

## Timeout Handling

Each Copilot CLI invocation has a process-level timeout (configurable, default 10 min for review, 15 min for fix). If the CLI hangs:
1. The process is killed after timeout
2. The phase is marked `timeout` in `.state.json`
3. On resume, the timed-out phase is retried

## Integration with Other Skills

| Skill | Integration |
|-------|-------------|
| `pr-review` | Review prompt files are reused; output goes to local iteration folder instead of `Generated Files/prReview/` |
| `pr-fix` | Fix prompt is adapted for local-only operation (no thread resolution, no push) |
| `issue-to-pr-cycle` | Can invoke `pr-rework` as a post-fix quality gate |

## Quality Gate: Build + Test

After every fix pass, the script:

1. **Builds** the changed projects:
   - Detects changed `.csproj`/`.vcxproj` files from `git diff`
   - Runs `tools/build/build.cmd` scoped to those projects
   - Checks exit code 0

2. **Runs unit tests**:
   - Discovers test projects by product code prefix
   - Looks for `*UnitTests` sibling folders
   - Runs via `dotnet vstest`
   - Reports pass/fail count

If build or tests fail, the failure details are fed back to the next fix iteration.
