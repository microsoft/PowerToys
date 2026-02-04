---
agent: 'agent'
description: 'Perform a comprehensive PR review with per-step Markdown and machine-readable outputs'
---

# Review Pull Request

**Goal**: Given `{{pr_number}}`, run a *one-topic-per-step* review. Write files to `Generated Files/prReview/{{pr_number}}/`. Emit machine‑readable blocks for a GitHub MCP to post review comments.

## PR selection
Resolve the target PR using these fallbacks in order:
1. Parse the invocation text for an explicit identifier (first integer following patterns such as a leading hash and digits or the text `PR:` followed by digits).
2. If no PR is found yet, locate the newest `Generated Files/prReview/_batch/batch-overview-*.md` file (highest timestamp in filename) and take the first entry in its `## PRs` list whose review folder is missing `00-OVERVIEW.md` or contains `__error.flag`.
3. If the batch file has no pending PRs, query assignments with `gh pr list --assignee @me --state open --json number,updatedAt --limit 20` and pick the most recently updated PR without a completed review folder.
4. If still unknown, run `gh pr view --json number` in the current branch and use that result when it is unambiguous.
5. If every step above fails, prompt the user for a PR number before proceeding.

## Fetch PR data with `gh`
- `gh pr view {{pr_number}} --json number,baseRefName,headRefName,baseRefOid,headRefOid,changedFiles,files`
- `gh api repos/:owner/:repo/pulls/{{pr_number}}/files?per_page=250`  # patches for line mapping

## Reusable PowerShell scripts
Scripts in `.github/review-tools/` to avoid repeated manual approvals:

| Script | Usage |
| --- | --- |
| `Get-GitHubRawFile.ps1` | Download a repository file at a given ref, optionally with line numbers. |
| `Get-GitHubPrFilePatch.ps1` | Fetch the unified diff for a specific file within a pull request via `gh api`. |
| `Get-PrIncrementalChanges.ps1` | Compare last reviewed SHA with current PR head. Returns JSON with changed files, new commits, and whether full review is needed. |
| `Test-IncrementalReview.ps1` | Test helper to preview incremental review detection for a PR. |

## Output files
Folder: `Generated Files/prReview/{{pr_number}}/`
- **Write-after-step rule:** Immediately after completing each step, persist that step's markdown file before proceeding to the next. Generate `00-OVERVIEW.md` only after all step files are complete.

## Iteration management
- Determine iteration by reading `00-OVERVIEW.md` (look for `Review iteration:`). If missing, assume `1`.
- Extract last reviewed SHA from `00-OVERVIEW.md`. If missing, this is iteration 1.
- **Incremental review**:
  1. Call `Get-PrIncrementalChanges.ps1 -PullRequestNumber {{pr_number}} -LastReviewedCommitSha {{sha}}`
  2. `NeedFullReview: true` → Review all files
  3. `IsIncremental: true` + `NeedFullReview: false` → Review only `ChangedFiles` array
  4. Empty `ChangedFiles` → Skip review, update history with "No changes"
- Increment iteration for each run and propagate to all step files.
- Preserve prior notes in `## Iteration history` sections (newest at top).
- **After completion**, update `Last reviewed SHA:` with current `headRefOid`.

### Smart step filtering (incremental reviews)
Skip steps based on changed file types:

| File pattern | Required steps | Skippable steps |
| --- | --- | --- |
| `**/*.cs`, `**/*.cpp`, `**/*.h` | Functionality, Compatibility, Performance, Security, SOLID, Repo patterns, Code comments | (depends on files) |
| `**/*.resx`, `**/Resources/*.xaml` | Localization, Globalization | Most others |
| `**/*.md` (docs) | Docs & automation | Most others (unless copilot guidance) |
| `**/*copilot*.md`, `.github/prompts/*.md` | Copilot guidance, Docs & automation | Most others |
| `**/*.csproj`, `**/*.vcxproj`, `**/packages.config` | Compatibility, Security, Repo patterns | Localization, Globalization, Accessibility |
| `**/UI/**`, `**/*View.xaml` | Accessibility, Localization | Performance (unless perf-sensitive controls) |

**Default**: If uncertain or files span multiple categories, run all applicable steps. When in doubt, be conservative and review more rather than less.

## Review steps
Execute each step by following its detailed prompt file in this folder:

| Step | Prompt file | Output file | Skip if |
| --- | --- | --- | --- |
| 01 | `01-functionality.prompt.md` | `01-functionality.md` | — |
| 02 | `02-compatibility.prompt.md` | `02-compatibility.md` | — |
| 03 | `03-performance.prompt.md` | `03-performance.md` | Only docs/resx changes |
| 04 | `04-accessibility.prompt.md` | `04-accessibility.md` | No UI changes |
| 05 | `05-security.prompt.md` | `05-security.md` | Only docs changes |
| 06 | `06-localization.prompt.md` | `06-localization.md` | No user-facing strings |
| 07 | `07-globalization.prompt.md` | `07-globalization.md` | No text/formatting |
| 08 | `08-extensibility.prompt.md` | `08-extensibility.md` | Internal changes only |
| 09 | `09-solid-design.prompt.md` | `09-solid-design.md` | Trivial changes |
| 10 | `10-repo-patterns.prompt.md` | `10-repo-patterns.md` | — |
| 11 | `11-docs-automation.prompt.md` | `11-docs-automation.md` | — |
| 12 | `12-code-comments.prompt.md` | `12-code-comments.md` | Config-only changes |
| 13 | `13-copilot-guidance.prompt.md` | `13-copilot-guidance.md` | No `*copilot*.md` or `.github/prompts/` |

Each step prompt file includes:
- Detailed checklist of concerns for that step
- Severity guidelines
- Output file template
- PowerToys-specific checks

## Overview file (`00-OVERVIEW.md`) template
```md
# PR Review Overview — (populate with PR identifier)
**Review iteration:** ITERATION
**Changed files:** <n> | **High severity issues:** <count>

## Review metadata
**Last reviewed SHA:** <headRefOid from gh pr view>
**Last review timestamp:** <ISO8601 timestamp>
**Review mode:** <Full|Incremental (N files changed since iteration X)>
**Base ref:** <baseRefName>
**Head ref:** <headRefName>

## Step results
Write lines like: `01 Functionality — <OK|Issues|Skipped> (see 01-functionality.md)` … through step 13.
Mark steps as "Skipped" when using incremental review smart filtering.

## Iteration history
- Maintain subsections titled `### Iteration N` mirroring the per-step convention with concise deltas and cross-links to the relevant step files.
- For incremental reviews, list the specific files that changed and which commits were added.
```

## Line numbers & multi‑file issues
- Map head‑side lines from `patch` hunks (`@@ -a,b +c,d @@` → new lines `+c..+c+d-1`).
- For cross‑file issues: set the primary `"file"`, list others in `"related_files"`, and name them in `"body"`.

## Posting (for MCP)
- Parse all ```mcp-review-comment``` blocks across step files and post as PR review comments.
- If posting isn’t available, still write all files.

## Constraint
Read/analyze only; don't modify code. Keep comments small, specific, and fix‑oriented.

**Testing**: Use `.github/review-tools/Test-IncrementalReview.ps1 -PullRequestNumber 42374` to preview incremental detection before running full review.

## Scratch cache for large PRs

Create a local scratch workspace to progressively summarize diffs and reload state across runs.

### Paths
- Root: `Generated Files/prReview/{{pr_number}}/__tmp/`
- Files:
  - `index.jsonl` — append-only JSON Lines index of artifacts.
  - `todo-queue.json` — pending items (files/chunks/steps).
  - `rollup-<step>-v<N>.md` — iterative per-step aggregates.
  - `file-<hash>.txt` — optional saved chunk text (when needed).

### JSON schema (per line in `index.jsonl`)
```json
{"type":"chunk|summary|issue|crosslink",
 "path":"relative/file.ext","chunk_id":"f-12","step":"functionality|compatibility|...",
 "base_sha":"...", "head_sha":"...", "range":[start,end], "version":1,
 "notes":"short text or key:value map", "created_utc":"ISO8601"}
```

### Phases (stateful; resume-safe)
0. **Discover** PR + SHAs: `gh pr view <PR> --json baseRefName,headRefName,baseRefOid,headRefOid,files`.
1. **Chunk** each changed file (head): split into ~300–600 LOC or ~4k chars; stable `chunk_id` = hash(path+start).
   - Save `chunk` records. Optionally write `file-<hash>.txt` for expensive chunks.
2. **Summarize** per chunk: intent, APIs, risks per TODO step; emit `summary` records (≤600 tokens each).
3. **Issues**: convert findings to machine-readable blocks and emit `issue` records (later rendered to step MD).
4. **Rollups**: build/update `rollup-<step>-v<N>.md` from `summary`+`issue`. Keep prior versions.
5. **Finalize**: write per-step files + `00-OVERVIEW.md` from rollups. Post comments via MCP if available.

### Re-use & token limits
- Always **reload** `index.jsonl` first; skip chunks with same `head_sha` and `range`.
- **Incremental review optimization**: When `Get-PrIncrementalChanges.ps1` returns a subset of changed files, load only chunks from those files. Reuse existing chunks/summaries for unchanged files.
- Prefer re-summarizing only changed chunks; merge chunk summaries → file summaries → step rollups.
- When context is tight, load only the minimal chunk text (or its saved `file-<hash>.txt`) needed for a comment.

### Original vs diff
- Fetch base content when needed: prefer `git show <baseRefName>:<path>`; fallback `gh api repos/:owner/:repo/contents/<path>?ref=<base_sha>` (base64).
- Use patch hunks from `gh api .../pulls/<PR>/files` to compute **head** line numbers.

### Queue-driven loop
- Seed `todo-queue.json` with all changed files.
- Process: chunk → summarize → detect issues → roll up.
- Append to `index.jsonl` after each step; never rewrite previous lines (append-only).

### Hygiene
- `__tmp/` is implementation detail; do not include in final artifacts.
- It is safe to delete to force a clean pass; the next run rebuilds it.