---
agent: 'agent'
description: 'Review exactly one PR - sequential steps, one output file per step'
---

# Review Pull Request

Review PR `{{pr_number}}`. Read/analyze only. Never modify code.

Execute the numbered phases below **in order**. Do not skip ahead.

---

## Phase 1 - Fetch PR data

```bash
gh pr view {{pr_number}} --json number,title,baseRefName,headRefName,baseRefOid,headRefOid,changedFiles,files
gh api repos/:owner/:repo/pulls/{{pr_number}}/files?per_page=250
```

Save `headRefOid` - you will need it in Phase 6.

## Phase 2 - Determine review mode

Check if `Generated Files/prReview/{{pr_number}}/00-OVERVIEW.md` exists.

- **Not found** - This is iteration 1. Full review. Go to Phase 3.
- **Found** - Extract `Last reviewed SHA:` from it, then run:
  ```powershell
  .github/skills/pr-review/scripts/Get-PrIncrementalChanges.ps1 `
      -PullRequestNumber {{pr_number}} `
      -LastReviewedCommitSha <extracted_sha>
  ```
  - `NeedFullReview: true` - Full review of all files.
  - `ChangedFiles` non-empty - Incremental review of only those files.
  - `ChangedFiles` empty - No changes. Write "No changes" to overview, stop.

Increment the iteration number from the existing overview (or start at 1).

## Phase 3 - Decide which steps to run

Using the changed file list from Phase 1 (full) or Phase 2 (incremental), match against these rules. When in doubt, **include the step**.

| Changed files match | Steps to run | Steps safe to skip |
|---|---|---|
| `*.cs`, `*.cpp`, `*.h` | 01 02 03 05 09 10 12 | - |
| `*.resx`, `Resources/*.xaml` | 06 07 | 03 04 05 08 09 |
| `*.md` (docs only) | 11 | 03 04 05 06 07 08 09 12 |
| `*copilot*.md`, `.github/prompts/*` | 13 11 | most others |
| `*.csproj`, `*.vcxproj`, `packages.config` | 02 05 10 | 04 06 07 |
| `UI/**`, `*View.xaml` | 04 06 | - |
| Mixed / uncertain | **All steps** | none |

Steps 01, 02, 10, 11 always run unless the change is trivially irrelevant.

## Phase 4 - Execute review steps

For each step that applies, **in order 01 through 13**:

1. **Read** the step prompt file from this folder (e.g. `01-functionality.prompt.md`)
2. **Analyze** the PR changes against that prompt's checklist
3. **Fetch external references** listed in the prompt's `## External references (MUST research)` section. Include a `## References consulted` section citing specific IDs (WCAG 1.4.3, OWASP A03, CWE-79, etc.)
4. **Write** the output file to `Generated Files/prReview/{{pr_number}}/01-functionality.md`
5. **Update** `.signal`: append step name to `completedSteps`, set `lastStep`, refresh `lastUpdated`. For skipped steps, append to `skippedSteps` instead.

**Do not batch.** Write each file immediately after completing that step before starting the next.

### Step table

| Step | Prompt file | Output file |
|---|---|---|
| 01 | `01-functionality.prompt.md` | `01-functionality.md` |
| 02 | `02-compatibility.prompt.md` | `02-compatibility.md` |
| 03 | `03-performance.prompt.md` | `03-performance.md` |
| 04 | `04-accessibility.prompt.md` | `04-accessibility.md` |
| 05 | `05-security.prompt.md` | `05-security.md` |
| 06 | `06-localization.prompt.md` | `06-localization.md` |
| 07 | `07-globalization.prompt.md` | `07-globalization.md` |
| 08 | `08-extensibility.prompt.md` | `08-extensibility.md` |
| 09 | `09-solid-design.prompt.md` | `09-solid-design.md` |
| 10 | `10-repo-patterns.prompt.md` | `10-repo-patterns.md` |
| 11 | `11-docs-automation.prompt.md` | `11-docs-automation.md` |
| 12 | `12-code-comments.prompt.md` | `12-code-comments.md` |
| 13 | `13-copilot-guidance.prompt.md` | `13-copilot-guidance.md` |

### Line mapping for review comments

Map head-side lines from patch hunks: `@@ -a,b +c,d @@` means new lines `c` through `c+d-1`. For cross-file issues, set the primary `"file"` and list others in `"related_files"`.

## Phase 5 - Write overview

After all step files are written, generate `Generated Files/prReview/{{pr_number}}/00-OVERVIEW.md`:

```md
# PR Review Overview - PR #{{pr_number}}: <title>
**Review iteration:** <N>
**Changed files:** <count> | **High severity issues:** <count>

## Review metadata
**Last reviewed SHA:** <headRefOid>
**Last review timestamp:** <ISO8601>
**Review mode:** <Full | Incremental (N files changed since iteration X)>
**Base ref:** <baseRefName>
**Head ref:** <headRefName>

## Step results
01 Functionality - <OK | Issues | Skipped> (see 01-functionality.md)
02 Compatibility - ...
... through 13.

## Iteration history
### Iteration <N>
<summary of this review pass>
```

For incremental reviews, list the specific files that changed and which commits were added.

## Phase 6 - Finalize

1. Update `.signal`: set `status` to `"success"` (or `"failure"`), `lastStep` to `"00-OVERVIEW"`, add `timestamp`.
2. Update `Last reviewed SHA:` in `00-OVERVIEW.md` with `headRefOid` from Phase 1.

## Phase 7 - Post comments (if MCP available)

Parse all `mcp-review-comment` fenced blocks across step files and post as PR review comments. If posting is not available, skip. The files are the primary output.

---

## Helper scripts

Located in `.github/skills/pr-review/scripts/`. Use these instead of raw `gh` commands when they fit:

| Script | When to use |
|---|---|
| `Get-GitHubRawFile.ps1` | Need to read a file at a specific ref with line numbers |
| `Get-GitHubPrFilePatch.ps1` | Need the unified diff for one file in the PR |
| `Get-PrIncrementalChanges.ps1` | Phase 2: determine if incremental review is needed |

## .signal file format

The outer script creates the initial `.signal` and writes the final one. Your job is to **update it after each step** in Phase 4:

```json
{
  "status": "in-progress",
  "prNumber": 45234,
  "totalSteps": 13,
  "completedSteps": ["01-functionality", "02-compatibility"],
  "skippedSteps": ["13-copilot-guidance"],
  "lastStep": "02-compatibility",
  "lastUpdated": "2026-02-04T10:03:12Z",
  "startedAt": "2026-02-04T10:00:05Z"
}
```
