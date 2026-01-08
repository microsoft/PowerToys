agent: 'agent'
model: GPT-5.1-Codex-Max
description: "gh-driven PR review; per-step Markdown + machine-readable outputs"
---

# PR Review — gh + stepwise

**Goal**: Given `{{pr_number}}`, run a *one-topic-per-step* review. Write files to `Generated Files/prReview/{{pr_number}}/` (replace `{{pr_number}}` with the integer). Emit machine‑readable blocks for a GitHub MCP to post review comments.

## PR selection
Resolve the target PR using these fallbacks in order:
1. Parse the invocation text for an explicit identifier (first integer following patterns such as a leading hash and digits or the text `PR:` followed by digits).
2. If no PR is found yet, locate the newest `Generated Files/prReview/_batch/batch-overview-*.md` file (highest timestamp in filename, fallback newest mtime) and take the first entry in its `## PRs` list whose review folder is missing `00-OVERVIEW.md` or contains `__error.flag`.
3. If the batch file has no pending PRs, query assignments with `gh pr list --assignee @me --state open --json number,updatedAt --limit 20` and pick the most recently updated PR that does not already have a completed review folder.
4. If still unknown, run `gh pr view --json number` in the current branch and use that result when it is unambiguous.
5. If every step above fails, prompt the user for a PR number before proceeding.

## Fetch PR data with `gh`
- `gh pr view {{pr_number}} --json number,baseRefName,headRefName,baseRefOid,headRefOid,changedFiles,files`
- `gh api repos/:owner/:repo/pulls/{{pr_number}}/files?per_page=250`  # patches for line mapping

### Incremental review workflow
1. **Check for existing review**: Read `Generated Files/prReview/{{pr_number}}/00-OVERVIEW.md`
2. **Extract state**: Parse `Last reviewed SHA:` from review metadata section
3. **Detect changes**: Run `Get-PrIncrementalChanges.ps1 -PullRequestNumber {{pr_number}} -LastReviewedCommitSha {{sha}}`
4. **Analyze result**:
   - `NeedFullReview: true` → Review all files in the PR
   - `NeedFullReview: false` and `IsIncremental: true` → Review only files in `ChangedFiles` array
   - `ChangedFiles` is empty → No changes, skip review (update iteration history with "No changes since last review")
5. **Apply smart filtering**: Use the file patterns in smart step filtering table to skip irrelevant steps
6. **Update metadata**: After completing review, save current `headRefOid` as `Last reviewed SHA:` in `00-OVERVIEW.md`

### Reusable PowerShell scripts
Scripts live in `.github/review-tools/` to avoid repeated manual approvals during PR reviews:

| Script | Usage |
| --- | --- |
| `.github/review-tools/Get-GitHubRawFile.ps1` | Download a repository file at a given ref, optionally with line numbers. |
| `.github/review-tools/Get-GitHubPrFilePatch.ps1` | Fetch the unified diff for a specific file within a pull request via `gh api`. |
| `.github/review-tools/Get-PrIncrementalChanges.ps1` | Compare last reviewed SHA with current PR head to identify incremental changes. Returns JSON with changed files, new commits, and whether full review is needed. |
| `.github/review-tools/Test-IncrementalReview.ps1` | Test helper to preview incremental review detection for a PR. Use before running full review to see what changed. |

Always prefer these scripts (or new ones added under `.github/review-tools/`) over raw `gh api` or similar shell commands so the review flow does not trigger interactive approval prompts.

## Output files
Folder: `Generated Files/prReview/{{pr_number}}/`
Files: `00-OVERVIEW.md`, `01-functionality.md`, `02-compatibility.md`, `03-performance.md`, `04-accessibility.md`, `05-security.md`, `06-localization.md`, `07-globalization.md`, `08-extensibility.md`, `09-solid-design.md`, `10-repo-patterns.md`, `11-docs-automation.md`, `12-code-comments.md`, `13-copilot-guidance.md` *(only if guidance md exists).* 
- **Write-after-step rule:** Immediately after completing each TODO step, persist that step's markdown file before proceeding to the next. Generate `00-OVERVIEW.md` only after every step file has been refreshed for the current run.

## Iteration management
- Determine the current review iteration by reading `00-OVERVIEW.md` (look for `Review iteration:`). If missing, assume iteration `1`.
- Extract the last reviewed SHA from `00-OVERVIEW.md` (look for `Last reviewed SHA:` in the review metadata section). If missing, this is iteration 1.
- **Incremental review detection**:
  1. Call `.github/review-tools/Get-PrIncrementalChanges.ps1 -PullRequestNumber {{pr_number}} -LastReviewedCommitSha {{last_sha}}` to get delta analysis.
  2. Parse the JSON result to determine if incremental review is possible (`IsIncremental: true`, `NeedFullReview: false`).
  3. If force-push detected or first review, proceed with full review of all changed files.
  4. If incremental, review only the files listed in `ChangedFiles` array and apply smart step filtering (see below).
- Increment the iteration for each review run and propagate the new value to all step files and the overview.
- Preserve prior iteration notes by keeping/expanding an `## Iteration history` section in each markdown file, appending the newest summary under `### Iteration <N>`.
- Summaries should capture key deltas since the previous iteration so reruns can pick up context quickly.
- **After review completion**, update `Last reviewed SHA:` in `00-OVERVIEW.md` with the current `headRefOid` and update the timestamp.

### Smart step filtering (incremental reviews only)
When performing incremental review, skip steps that are irrelevant based on changed file types:

| File pattern | Required steps | Skippable steps |
| --- | --- | --- |
| `**/*.cs`, `**/*.cpp`, `**/*.h` | Functionality, Compatibility, Performance, Security, SOLID, Repo patterns, Code comments | (depends on files) |
| `**/*.resx`, `**/Resources/*.xaml` | Localization, Globalization | Most others |
| `**/*.md` (docs) | Docs & automation | Most others (unless copilot guidance) |
| `**/*copilot*.md`, `.github/prompts/*.md` | Copilot guidance, Docs & automation | Most others |
| `**/*.csproj`, `**/*.vcxproj`, `**/packages.config` | Compatibility, Security, Repo patterns | Localization, Globalization, Accessibility |
| `**/UI/**`, `**/*View.xaml` | Accessibility, Localization | Performance (unless perf-sensitive controls) |

**Default**: If uncertain or files span multiple categories, run all applicable steps. When in doubt, be conservative and review more rather than less.

## TODO steps (one concern each)
1) Functionality
2) Compatibility
3) Performance
4) Accessibility
5) Security
6) Localization
7) Globalization
8) Extensibility
9) SOLID principles
10) Repo patterns
11) Docs & automation coverage for the changes
12) Code comments
13) Copilot guidance (conditional): if changed folders contain `*copilot*.md` or `.github/prompts/*.md`, review diffs **against** that guidance and write `13-copilot-guidance.md` (omit if none).

## Per-step file template (use verbatim)
```md
# <STEP TITLE>
**PR:** (populate with PR identifier) — Base:<baseRefName> Head:<headRefName>
**Review iteration:** ITERATION

## Iteration history
- Maintain subsections titled `### Iteration N` in reverse chronological order (append the latest at the top) with 2–4 bullet highlights.

### Iteration ITERATION
- <Latest key point 1>
- <Latest key point 2>

## Checks executed
- List the concrete checks for *this step only* (5–10 bullets).

## Findings
(If none, write **None**. Defaults have one or more blocks:)

```mcp-review-comment
{"file":"relative/path.ext","start_line":123,"end_line":125,"severity":"high|medium|low|info","tags":["<step-slug>","pr-tag-here"],"related_files":["optional/other/file1"],"body":"Problem → Why it matters → Concrete fix. If spans multiple files, name them here."}
```
Use the second tag to encode the PR number.

```
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