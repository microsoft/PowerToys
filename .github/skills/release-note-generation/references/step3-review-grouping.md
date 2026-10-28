# Step 3: Local Agent Reviews and Grouping

## 3.0 To-do
- 3.1 Generate PR Summaries with the Local Agent
- 3.2 (Optional) Refresh PR Data
- 3.3 Group PRs by Label

## 3.1 Generate PR Summaries with the Local Agent

> ⚠️ **Do not use `mcp_github_request_copilot_review` (or any "request Copilot review" tool that calls the GitHub API).**
> When this skill is driven from a CLI / coding agent, the request is made from a bot identity and the GitHub API rejects it ("Bot reviewers cannot be requested"). The PR ends up with no Copilot review and `CopilotSummary` stays empty.
>
> Instead, **the local agent that is running this skill performs the review itself** and writes the summary directly into `sorted_prs.csv`.

For every PR listed in `Generated Files/ReleaseNotes/sorted_prs.csv` whose `CopilotSummary` is empty:

1. Fetch the PR diff using a tool that does **not** post anything back to GitHub. Any of these works:
    - `mcp_github_pull_request_read` with `method: get_diff`
    - `mcp_github_pull_request_read` with `method: get_files` (when the diff is large)
    - `gh pr diff <PR_NUMBER> --repo microsoft/PowerToys`
2. Read the PR title, body, and diff. Produce a 1–3 sentence, user-facing summary in the same style as a Copilot PR review (focus on observable behavior change, not implementation details).
3. Write the summary into the `CopilotSummary` column for that PR row in `Generated Files/ReleaseNotes/sorted_prs.csv`. Preserve all other columns and the existing row order.

**Batching guidance**

- Process PRs in the order they appear in `sorted_prs.csv`.
- Generate summaries for **all** PRs in one pass before continuing to Step 3.3, so the human reviewer can validate them together.
- For very large diffs, summarize from `get_files` (filenames + per-file patches) rather than the full diff.
- Skip PRs that already have a non-empty `CopilotSummary` (e.g. PRs where a human reviewer already pasted one). Do not overwrite existing summaries.

**Why not post the summary back to the PR?** Posting a comment from the agent's identity would not be picked up by `dump-prs-since-commit.ps1` (which only matches Copilot bot authors), and it adds noise to the PR. Writing straight into the CSV keeps the artifact self-contained.

---

## 3.2 (Optional) Refresh PR Data

Only re-run the collection script if PR metadata on GitHub has changed (new labels, retitled PRs, etc.) since Step 1.1. **Skip this step if you only want to preserve the locally generated `CopilotSummary` values from Step 3.1**, because re-running the dump will overwrite the CSV.

```powershell
pwsh ./.github/skills/release-note-generation/scripts/dump-prs-since-commit.ps1 `
    -StartCommit '{{PreviousReleaseTag}}' -Branch 'stable' `
    -OutputDir 'Generated Files/ReleaseNotes'
```

If you do refresh, redo Step 3.1 afterwards to repopulate `CopilotSummary`.

---

## 3.3 Group PRs by Label

```powershell
pwsh ./.github/skills/release-note-generation/scripts/group-prs-by-label.ps1 -CsvPath 'Generated Files/ReleaseNotes/sorted_prs.csv' -OutDir 'Generated Files/ReleaseNotes/grouped_csv'
```

Creates `Generated Files/ReleaseNotes/grouped_csv/` with one CSV per label combination.

**Validation:** The `Unlabeled.csv` file should be minimal (ideally empty). If many PRs remain unlabeled, return to Step 2 (see [step2-labeling.md](./step2-labeling.md)).

