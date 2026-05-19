---
name: release-note-generation
description: Toolkit for generating PowerToys release notes from GitHub milestone PRs or commit ranges. Use when asked to create release notes, summarize milestone PRs, generate changelog, prepare release documentation, generate PR review summaries locally for release notes, update README for a new release, manage PR milestones, collect PRs between commits/tags, or prepare release assets (download installers and compute installer hashes).
license: Complete terms in LICENSE.txt
---

# Release Note Generation Skill

Generate professional release notes for PowerToys milestones by collecting merged PRs, summarizing each PR with the local CLI agent, grouping by label, and producing user-facing summaries.

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
- Generate per-PR review summaries locally for release-notes copy
- Assign milestones to PRs missing them
- Collect PRs between two commits/tags
- Update README.md for a new version
- Prepare GitHub release assets (download installers/symbols + compute hashes)

## Prerequisites

- **GitHub CLI (`gh`) installed and authenticated** — The collection script uses `gh pr view` and `gh api graphql` to fetch PR metadata and co-author information. Run `gh auth status` to verify; if not logged in, run `gh auth login` first. See [Step 1.0.0](./references/step1-collection.md) for details.
- MCP Server: github-mcp-server installed (used to fetch PR diffs/files for the local-agent review step)
- For [prepare-release-assets.ps1](./scripts/prepare-release-assets.ps1) only: **Azure CLI** authenticated against the Microsoft tenant (`az login`) with the `azure-devops` extension; access to the `microsoft/Dart` ADO project

## Required Variables

⚠️ **Before starting**, confirm `{{ReleaseVersion}}` with the user. If not provided, **ASK**: "What release version are we generating notes for? (e.g., 0.98)"

| Variable | Description | Example |
|----------|-------------|---------|
| `{{ReleaseVersion}}` | Target release version | `0.98` |

## Workflow Overview

```
┌────────────────────────────────┐
│ 1.0 Verify gh auth + MemberList │
└────────────────────────────────┘
              ↓
┌────────────────────────────────┐
│ 1.1 Collect PRs (stable range) │
└────────────────────────────────┘
              ↓
┌────────────────────────────────┐
│ 1.2 Assign Milestones           │
└────────────────────────────────┘
              ↓
┌────────────────────────────────┐
│ 2.1–2.4 Label PRs (auto+human)  │
└────────────────────────────────┘
              ↓
┌────────────────────────────────┐
│ 3.1 Local-agent PR summaries    │
│ (writes CopilotSummary)         │
└────────────────────────────────┘
              ↓
┌────────────────────────────────┐
│ 3.2 (Optional) Refresh PR data  │
└────────────────────────────────┘
              ↓
┌────────────────────────────────┐
│ 3.3 Group by label              │
│ (grouped_csv)                   │
└────────────────────────────────┘
              ↓
┌────────────────────────────────┐
│ 4.1 Summarize (grouped_md)      │
└────────────────────────────────┘
              ↓
┌────────────────────────────────┐
│ 4.2 Final notes (v{VERSION}.md) │
└────────────────────────────────┘
```

| Step | Action | Details |
|------|--------|---------|
| 1.0 | Verify prerequisites | `gh auth status` must pass; generate MemberList.md |
| 1.1 | Collect PRs | From previous release tag on `stable` branch → `sorted_prs.csv` |
| 1.2 | Assign Milestones | Ensure all PRs have correct milestone |
| 2.1–2.4 | Label PRs | Auto-suggest + human label low-confidence |
| 3.1–3.3 | Reviews & Grouping | Local agent summarizes each PR diff into `CopilotSummary` → (optional refresh) → group by label |
| 4.1–4.2 | Summaries & Final | Generate grouped summaries, then consolidate |

## Detailed workflow docs

Do not read all steps at once—only read the step you are executing.

- [Step 1: Collection & Milestones](./references/step1-collection.md)
- [Step 2: Labeling PRs](./references/step2-labeling.md)
- [Step 3: Reviews & Grouping](./references/step3-review-grouping.md)
- [Step 4: Summarization](./references/step4-summarization.md)


## Available Scripts

| Script | Purpose |
|--------|---------|
| [dump-prs-since-commit.ps1](./scripts/dump-prs-since-commit.ps1) | Fetch PRs between commits/tags |
| [group-prs-by-label.ps1](./scripts/group-prs-by-label.ps1) | Group PRs into CSVs |
| [collect-or-apply-milestones.ps1](./scripts/collect-or-apply-milestones.ps1) | Assign milestones |
| [diff_prs.ps1](./scripts/diff_prs.ps1) | Incremental PR diff |
| [prepare-release-assets.ps1](./scripts/prepare-release-assets.ps1) | Download installers + symbols from an ADO build, compute SHA256, emit the "Installer Hashes" markdown table for the GitHub release page |

## References

- [Sample Output](./references/SampleOutput.md) - Example summary formatting
- [Detailed Instructions](./references/Instruction.md) - Legacy full documentation

## Conventions

- **Terminal usage**: Disabled by default; only run scripts when user explicitly requests
- **Batch generation**: Generate ALL grouped_md files in one pass, then human reviews
- **PR order**: Preserve order from `sorted_prs.csv` in all outputs
- **Label filtering**: Keeps `Product-*`, `Area-*`, `GitHub*`, `*Plugin`, `Issue-*`

## Troubleshooting

| Issue | Solution |
|-------|----------|
| `gh` command not found | Install GitHub CLI and add to PATH |
| No PRs returned | Verify milestone title matches exactly |
| Empty `CopilotSummary` for many PRs | Run Step 3.1 (local-agent summaries). Do **not** use `mcp_github_request_copilot_review` from a CLI/coding agent — the GitHub API rejects bot-initiated review requests, so the column will stay empty. |
| Many unlabeled PRs | Return to labeling step before grouping |
| `prepare-release-assets.ps1` fails with "Failed to acquire ADO access token" | Run `az login` and ensure you have access to the `microsoft/Dart` ADO project |
