---
name: release-note-generation
description: Toolkit for generating PowerToys release notes from GitHub milestone PRs or commit ranges. Use when asked to create release notes, summarize milestone PRs, generate changelog, prepare release documentation, request Copilot reviews for PRs, update README for a new release, manage PR milestones, or collect PRs between commits/tags. Supports PR collection by milestone or commit range, milestone assignment, grouping by label, summarization with external contributor attribution, and README version bumping.
license: Complete terms in LICENSE.txt
---

# Release Note Generation Skill

Generate professional release notes for PowerToys milestones by collecting merged PRs, requesting Copilot code reviews, grouping by label, and producing user-facing summaries.

## Output Directory

All generated artifacts are placed under `Generated Files/ReleaseNotes/` at the repository root (gitignored).

```
Generated Files/ReleaseNotes/
├── milestone_prs.json           # Raw PR data from GitHub
├── sorted_prs.csv               # Sorted PR list with Copilot summaries
├── prs_with_milestone.csv       # Milestone assignment tracking
├── grouped_csv/                 # PRs grouped by label (one CSV per label)
├── grouped_md/                  # Generated markdown summaries per label
└── v{VERSION}-release-notes.md  # Final consolidated release notes
```

## When to Use This Skill

- Generate release notes for a milestone
- Summarize PRs merged in a release
- Request Copilot reviews for milestone PRs
- Assign milestones to PRs missing them
- Collect PRs between two commits/tags
- Update README.md for a new version

## Prerequisites

- GitHub CLI (`gh`) installed and authenticated
- MCP Server: github-mcp-server installed
- GitHub Copilot code review enabled for the org/repo

## Required Variables

⚠️ **Before starting**, confirm `{{ReleaseVersion}}` with the user. If not provided, **ASK**: "What release version are we generating notes for? (e.g., 0.98)"

| Variable | Description | Example |
|----------|-------------|---------|
| `{{ReleaseVersion}}` | Target release version | `0.98` |
| `{{PreviousReleaseTag}}` | Previous release tag | `v0.97.0` |

## Workflow Overview

| Step | Action | Details |
|------|--------|---------|
| 1 | **Collect PRs** | From previous release tag on `stable` branch → `sorted_prs.csv` |
| 2 | **Assign Milestones** | Ensure all PRs have correct milestone |
| 3 | **Label PRs** | Ensure all PRs have `Product-*` or `Area-*` labels |
| 4 | **Request Copilot Reviews** | Via MCP tools for all PRs |
| 5 | **Refresh & Group** | Re-collect to get summaries, then group by label |
| 6 | **Generate Summaries** | Create `grouped_md/*.md` files (batch, no pausing) |
| 7 | **Produce Final Notes** | Consolidate into `v{VERSION}-release-notes.md` |

**Detailed workflow docs:**
- [Collection & Milestones](./references/workflow-collection.md) - Steps 1-2
- [Labeling PRs](./references/workflow-labeling.md) - Step 3, label mappings
- [Reviews & Grouping](./references/workflow-review-grouping.md) - Steps 4-5
- [Summarization](./references/workflow-summarization.md) - Steps 6-7, output format

## Quick Commands

**Collect PRs from previous release:**
```powershell
pwsh ./.github/skills/release-note-generation/scripts/dump-prs-since-commit.ps1 `
    -StartCommit '{{SHALastRelease}}' -EndCommit HEAD -Branch 'stable' `
    -OutputDir 'Generated Files/ReleaseNotes'
```

**Assign milestones (dry run first):**
```powershell
pwsh ./.github/skills/release-note-generation/scripts/collect-or-apply-milestones.ps1 `
    -InputCsv 'Generated Files/ReleaseNotes/sorted_prs.csv' `
    -DefaultMilestone 'PowerToys {{ReleaseVersion}}' -ApplyMissing -WhatIf
```

**Group PRs by label:**
```powershell
pwsh ./.github/skills/release-note-generation/scripts/group-prs-by-label.ps1 -CsvPath 'Generated Files/ReleaseNotes/sorted_prs.csv' -OutDir 'Generated Files/ReleaseNotes/grouped_csv'
```

## Available Scripts

| Script | Purpose |
|--------|---------|
| [dump-prs-since-commit.ps1](./scripts/dump-prs-since-commit.ps1) | Fetch PRs between commits/tags |
| [group-prs-by-label.ps1](./scripts/group-prs-by-label.ps1) | Group PRs into CSVs |
| [collect-or-apply-milestones.ps1](./scripts/collect-or-apply-milestones.ps1) | Assign milestones |
| [diff_prs.ps1](./scripts/diff_prs.ps1) | Incremental PR diff |

## References

- [Member List](./references/MemberList.md) - Internal contributors (no thanks needed)
- [Sample Output](./references/SampleOutput.md) - Example summary formatting
- [Detailed Instructions](./references/Instruction.md) - Legacy full documentation

## Conventions

- **Terminal usage**: Disabled by default; only run scripts when user explicitly requests
- **Batch generation**: Generate ALL grouped_md files in one pass, then human reviews
- **PR order**: Preserve order from `sorted_prs.csv` in all outputs
- **Label filtering**: Keeps `Product-*`, `Area-*`, `Github*`, `*Plugin`, `Issue-*`

## Troubleshooting

| Issue | Solution |
|-------|----------|
| `gh` command not found | Install GitHub CLI and add to PATH |
| No PRs returned | Verify milestone title matches exactly |
| Empty CopilotSummary | Request Copilot reviews first, then re-run dump |
| Many unlabeled PRs | Return to labeling step before grouping |
