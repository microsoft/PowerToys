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

## Workflow Overview

```
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
│ 3.1 Request Reviews (Copilot)  │
└────────────────────────────────┘
              ↓
┌────────────────────────────────┐
│ 3.2 Refresh PR data             │
│ (CopilotSummary)                │
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
| 1.1 | Collect PRs | From previous release tag on `stable` branch → `sorted_prs.csv` |
| 1.2 | Assign Milestones | Ensure all PRs have correct milestone |
| 2.1–2.4 | Label PRs | Auto-suggest + human label low-confidence |
| 3.1–3.3 | Reviews & Grouping | Request Copilot reviews → refresh → group by label |
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
