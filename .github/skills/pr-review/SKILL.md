---
name: pr-review
description: Comprehensive pull request review with multi-step analysis and comment posting. Use when asked to review a PR, analyze pull request changes, check PR for issues, post review comments, validate PR quality, run code review on a PR, or audit pull request. Generates 13 review step files covering functionality, security, performance, accessibility, and more. For FIXING PR comments, use the pr-fix skill instead.
license: Complete terms in LICENSE.txt
---

# PR Review Skill

**Review** PRs only. To **fix** review comments, use `pr-fix`.

## What to Do

Run the review script with the PR number(s):

```powershell
.github/skills/pr-review/scripts/Start-PRReviewWorkflow.ps1 -PRNumbers <N>
```

The script spawns Copilot CLI, which follows [review-pr.prompt.md](./references/review-pr.prompt.md) to execute 13 review steps and write results to `Generated Files/prReview/<N>/`.

### Options

| Parameter | Description | Default |
|-----------|-------------|---------|
| `-PRNumbers` | PR number(s) **(required)** | — |
| `-CLIType` | `copilot` or `claude` | `copilot` |
| `-Model` | Model override | (default) |
| `-MinSeverity` | Min severity to post: `high` / `medium` / `low` / `info` | `medium` |
| `-MaxConcurrent` | Max parallel review jobs (via orchestrator) | `4` |
| `-InactivityTimeoutSeconds` | Kill CLI if log doesn't grow | `60` |
| `-MaxRetryCount` | Retry attempts after inactivity kill | `3` |
| `-OutputRoot` | Review output root folder | `Generated Files/prReview` |
| `-LogPath` | Workflow log file path | `Start-PRReviewWorkflow.log` |
| `-Force` | Re-review PRs that already have output | `false` |
| `-DryRun` | Preview without executing | `false` |

Completed reviews are auto-skipped. Use `-Force` to redo.

### If You ARE the Reviewer

When running inside Copilot CLI (i.e. you were spawned by the script), follow [review-pr.prompt.md](./references/review-pr.prompt.md) directly. It tells you:

1. Fetch PR data with `gh`
2. Execute each step by loading its prompt file on-demand
3. Write each step's output to `Generated Files/prReview/<N>/XX-name.md`
4. Update `.signal` after every step
5. Generate `00-OVERVIEW.md` after all steps

Each step prompt also has `## External references (MUST research)` — fetch those URLs and include a `## References consulted` section citing specific violation IDs (WCAG 1.4.3, OWASP A03, etc.).

### Step Prompts (loaded on-demand)

| Step | Prompt | Focus |
|------|--------|-------|
| 01 | [Functionality](./references/01-functionality.prompt.md) | Correctness, edge cases |
| 02 | [Compatibility](./references/02-compatibility.prompt.md) | Breaking changes, versioning |
| 03 | [Performance](./references/03-performance.prompt.md) | Perf implications, async |
| 04 | [Accessibility](./references/04-accessibility.prompt.md) | WCAG 2.1, a11y |
| 05 | [Security](./references/05-security.prompt.md) | OWASP, CWE, SDL |
| 06 | [Localization](./references/06-localization.prompt.md) | L10n readiness |
| 07 | [Globalization](./references/07-globalization.prompt.md) | BiDi, ICU, date/time |
| 08 | [Extensibility](./references/08-extensibility.prompt.md) | Plugin API, SemVer |
| 09 | [SOLID Design](./references/09-solid-design.prompt.md) | Design principles |
| 10 | [Repo Patterns](./references/10-repo-patterns.prompt.md) | PowerToys conventions |
| 11 | [Docs & Automation](./references/11-docs-automation.prompt.md) | Documentation |
| 12 | [Code Comments](./references/12-code-comments.prompt.md) | Comment quality |
| 13 | [Copilot Guidance](./references/13-copilot-guidance.prompt.md) | Agent/prompt files |

## Scripts

| Script | Purpose |
|--------|---------|
| [Start-PRReviewWorkflow.ps1](./scripts/Start-PRReviewWorkflow.ps1) | Orchestrator — run this |
| [Post-ReviewComments.ps1](./scripts/Post-ReviewComments.ps1) | Post comments to GitHub |
| [Get-GitHubPrFilePatch.ps1](./scripts/Get-GitHubPrFilePatch.ps1) | Fetch PR file diffs |
| [Get-GitHubRawFile.ps1](./scripts/Get-GitHubRawFile.ps1) | Download repo files at a ref |
| [Get-PrIncrementalChanges.ps1](./scripts/Get-PrIncrementalChanges.ps1) | Detect changes since last review |
| [Test-IncrementalReview.ps1](./scripts/Test-IncrementalReview.ps1) | Preview incremental detection |

## Execution & Monitoring Rules

Batch reviews take **5–30 minutes** depending on PR count and complexity. The agent MUST:

1. **Launch as a detached process** for batch runs (>2 PRs) — VS Code terminal idle detection kills background processes. Use `Start-Process -WindowStyle Hidden` with `Tee-Object` to a log file.
2. **Poll the orchestrator log every 60–120 seconds** until all jobs report `Completed`, `Failed`, or `Abandoned`.
3. **Do NOT exit or ask the user to check back** — keep monitoring until the orchestrator finishes.
4. **On process death**, check the orchestrator log, clean up partial output, and relaunch automatically.
5. **Report final results** with a table showing per-PR status, exit codes, and retry counts.

## Post-Execution Review

After each run, quickly validate quality and update guidance when needed:

1. Confirm outputs exist under the configured `-OutputRoot` for each PR.
2. Spot-check `00-OVERVIEW.md` and 2-3 step files for correctness and completeness.
3. If repeated gaps are found, refine the relevant prompt in [references](./references).
4. If behavior changed, update this file’s Options/Workflow docs in the same change.
5. Record concrete examples of failures to prevent repeating ambiguous guidance.

## Dependencies

This skill depends on the **parallel-job-orchestrator** skill for batch execution.
The runner script (`Invoke-PRReviewSimpleRunner.ps1`) builds job definitions and
delegates to `parallel-job-orchestrator/scripts/Invoke-SimpleJobOrchestrator.ps1`
for queuing, monitoring, retry, and cleanup. Do NOT use `Start-Job`,
`ForEach-Object -Parallel`, or `Start-Process` directly.

## Related Skills

| Skill | Purpose |
|-------|---------|
| `parallel-job-orchestrator` | Parallel execution engine (REQUIRED for batch runs) |
| `pr-fix` | Fix review comments after this skill identifies issues |
| `issue-to-pr-cycle` | Full orchestration (review → fix loop) |
