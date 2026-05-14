---
description: 'Perform a local-only PR review using git diff (no GitHub API for file content)'
name: 'rework-local-review'
agent: 'agent'
argument-hint: 'PR number, output directory'
---

# Local PR Review (Worktree-Based)

**Goal**: Review code changes in a local worktree using `git diff` as the data source.
Write per-step Markdown files with machine-readable finding blocks.

> **Key difference from `review-pr.prompt.md`**: This prompt reads code from the
> local worktree via `git diff` and `cat`/`Get-Content`, NOT from GitHub API.
> It does NOT post comments, resolve threads, or call any GitHub API.

## Inputs
- `${input:pr_number}` — PR number (for labeling only)
- `${input:output_dir}` — Directory to write step files into
- `${input:iteration}` — Iteration number for this review cycle
- `${input:previous_findings}` — (optional) Path to previous iteration's `findings.json`

## How to get the changed files

**USE THESE LOCAL COMMANDS — NOT GitHub API:**

```bash
# Summary of what changed (origin/main vs working tree, includes uncommitted fixes)
git diff origin/main --stat

# Full diff for review (includes uncommitted changes)
git diff origin/main

# List only changed file names
git diff origin/main --name-only

# Diff for a specific file
git diff origin/main -- path/to/file.cs

# Read the current file content (working tree version, latest)
cat path/to/file.cs          # or Get-Content path/to/file.cs

# Read the base version for comparison
git show origin/main:path/to/file.cs
```

> **Why `origin/main` instead of `main`?**
> The local `main` ref may be stale (not fetched recently). Using `origin/main`
> ensures we always diff against the latest remote main.

> **Why two-dot diff and NOT three-dot (`origin/main...HEAD`)?**
> After each fix iteration, changes are left uncommitted in the working tree.
> Three-dot diff only shows committed changes and would miss the fixes.
> Two-dot diff compares origin/main directly against the working tree, which always
> reflects the latest state.

**NEVER USE:**
- `gh pr view` / `gh api` for fetching file content or patches
- `Get-GitHubRawFile.ps1` or `Get-GitHubPrFilePatch.ps1`
- `Get-PrIncrementalChanges.ps1`
- Any `https://raw.githubusercontent.com/` URLs

## Output files
Folder: `${input:output_dir}/`

Write each step file immediately after completing the step. Generate `00-OVERVIEW.md` last.

## Smart step filtering
Determine which steps to run based on changed file types:

| File pattern | Required steps | Skippable steps |
| --- | --- | --- |
| `*.cs`, `*.cpp`, `*.h` | 01-Functionality, 02-Compatibility, 03-Performance, 05-Security, 09-SOLID, 10-Repo patterns, 12-Code comments | — |
| `*.resx`, `Resources/*.xaml` | 06-Localization, 07-Globalization | Most others |
| `*.md` (docs only) | 11-Docs & automation | Most others |
| `*copilot*.md`, `.github/prompts/*.md` | 13-Copilot guidance, 11-Docs & automation | Most others |
| `*.csproj`, `*.vcxproj`, `packages.config` | 02-Compatibility, 05-Security, 10-Repo patterns | 06, 07, 04 |
| `UI/**`, `*View.xaml` | 04-Accessibility, 06-Localization | 03 (unless perf-sensitive) |

Default: run all applicable steps when unsure.

## Review steps
Use the same checklists from the pr-review skill step prompt files. For each step:

1. Read the relevant checklist from `.github/skills/pr-review/references/NN-<step>.prompt.md`
2. Analyze the local diff (`git diff origin/main`) against that checklist
3. Write findings to `${input:output_dir}/NN-<step>.md`

| Step | Checklist source | Output file |
| --- | --- | --- |
| 01 | `.github/skills/pr-review/references/01-functionality.prompt.md` | `01-functionality.md` |
| 02 | `.github/skills/pr-review/references/02-compatibility.prompt.md` | `02-compatibility.md` |
| 03 | `.github/skills/pr-review/references/03-performance.prompt.md` | `03-performance.md` |
| 04 | `.github/skills/pr-review/references/04-accessibility.prompt.md` | `04-accessibility.md` |
| 05 | `.github/skills/pr-review/references/05-security.prompt.md` | `05-security.md` |
| 06 | `.github/skills/pr-review/references/06-localization.prompt.md` | `06-localization.md` |
| 07 | `.github/skills/pr-review/references/07-globalization.prompt.md` | `07-globalization.md` |
| 08 | `.github/skills/pr-review/references/08-extensibility.prompt.md` | `08-extensibility.md` |
| 09 | `.github/skills/pr-review/references/09-solid-design.prompt.md` | `09-solid-design.md` |
| 10 | `.github/skills/pr-review/references/10-repo-patterns.prompt.md` | `10-repo-patterns.md` |
| 11 | `.github/skills/pr-review/references/11-docs-automation.prompt.md` | `11-docs-automation.md` |
| 12 | `.github/skills/pr-review/references/12-code-comments.prompt.md` | `12-code-comments.md` |
| 13 | `.github/skills/pr-review/references/13-copilot-guidance.prompt.md` | `13-copilot-guidance.md` |

## Incremental review (iteration 2+)
When `${input:previous_findings}` is provided:

1. Read the previous findings JSON to understand what was already flagged.
2. Check if those findings have been fixed in the current code (via `git diff`).
3. Focus review effort on:
   - Files that were modified since the last iteration (new uncommitted changes)
   - Areas adjacent to previous findings
   - Any new issues introduced by fixes
4. In each step file, note which previous findings are now resolved.

To detect local changes since the last fix pass:
```bash
# Show uncommitted changes (what the fix pass modified)
git diff --name-only

# Show uncommitted changes with stat
git diff --stat
```

## Finding format
Use `mcp-review-comment` blocks for machine-readable findings (same format as pr-review):

````md
```mcp-review-comment
{
  "file": "src/modules/Foo/Bar.cs",
  "line": 42,
  "endLine": 50,
  "severity": "high",
  "title": "Null reference in error path",
  "body": "Problem → Why it matters → Concrete fix suggestion.",
  "suggestedFix": "Add null guard before accessing .Value",
  "tags": ["functionality", "pr-${input:pr_number}"]
}
```
````

Severity levels:
- **high**: Code doesn't work, crashes, data loss, security vulnerability
- **medium**: Edge cases broken, degraded experience, incomplete implementation
- **low**: Minor issues, suboptimal but working, style concerns
- **info**: Suggestions, not blocking

## Overview file template (`00-OVERVIEW.md`)
```md
# Local Review — PR #${input:pr_number}
**Review iteration:** ${input:iteration}
**Changed files:** <count from git diff --name-only main>
**High severity issues:** <count>

## Review mode
Local worktree review (no GitHub API)

## Step results
01 Functionality — <OK|Issues|Skipped>
02 Compatibility — <OK|Issues|Skipped>
...through 13

## Findings summary
| ID | Severity | File | Line | Title |
|----|----------|------|------|-------|
| F-001 | high | src/... | 42 | ... |
```

## Constraints
- **Read-only review** — do NOT modify any code
- **No GitHub API** — all data comes from local git commands
- **No posting** — do NOT post comments to GitHub
- **No MCP comment blocks execution** — write them in files for parsing, but do not execute them
