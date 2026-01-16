# Step 3: Copilot Reviews and Grouping

## 3.0 To-do
- 3.1 Request Copilot Reviews (Agent Mode)
- 3.2 Refresh PR Data
- 3.3 Group PRs by Label

## 3.1 Request Copilot Reviews (Agent Mode)

Use MCP tools to request Copilot reviews for all PRs in `Generated Files/ReleaseNotes/sorted_prs.csv`:

- Use `mcp_github_request_copilot_review` for each PR ID
- Do NOT generate or run scripts for this step

---

## 3.2 Refresh PR Data

Re-run the collection script to capture Copilot review summaries into the `CopilotSummary` column:

```powershell
pwsh ./.github/skills/release-note-generation/scripts/dump-prs-since-commit.ps1 `
    -StartCommit '{{PreviousReleaseTag}}' -Branch 'stable' `
    -OutputDir 'Generated Files/ReleaseNotes'
```

---

## 3.3 Group PRs by Label

```powershell
pwsh ./.github/skills/release-note-generation/scripts/group-prs-by-label.ps1 -CsvPath 'Generated Files/ReleaseNotes/sorted_prs.csv' -OutDir 'Generated Files/ReleaseNotes/grouped_csv'
```

Creates `Generated Files/ReleaseNotes/grouped_csv/` with one CSV per label combination.

**Validation:** The `Unlabeled.csv` file should be minimal (ideally empty). If many PRs remain unlabeled, return to Step 2 (see [step2-labeling.md](./step2-labeling.md)).
