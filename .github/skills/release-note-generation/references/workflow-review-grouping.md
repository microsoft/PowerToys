# Phase 4-6: Copilot Reviews and Grouping

## Phase 4: Request Copilot Reviews (Agent Mode)

Use MCP tools to request Copilot reviews for all PRs in `Generated Files/ReleaseNotes/sorted_prs.csv`:

- Use `mcp_github_request_copilot_review` for each PR ID
- Do NOT generate or run scripts for this step

---

## Phase 5: Refresh PR Data

Re-run the collection script to capture Copilot review summaries into the `CopilotSummary` column:

```powershell
pwsh ./.github/skills/release-note-generation/scripts/dump-prs-since-commit.ps1 `
    -StartCommit '{{PreviousReleaseTag}}' -EndCommit HEAD -Branch 'stable' `
    -OutputDir 'Generated Files/ReleaseNotes'
```

---

## Phase 6: Group PRs by Label

```powershell
pwsh ./.github/skills/release-note-generation/scripts/group-prs-by-label.ps1 -CsvPath 'Generated Files/ReleaseNotes/sorted_prs.csv' -OutDir 'Generated Files/ReleaseNotes/grouped_csv'
```

Creates `Generated Files/ReleaseNotes/grouped_csv/` with one CSV per label combination.

**Validation:** The `Unlabeled.csv` file should be minimal (ideally empty). If many PRs remain unlabeled, return to Phase 3 (see [workflow-labeling.md](./workflow-labeling.md)).
