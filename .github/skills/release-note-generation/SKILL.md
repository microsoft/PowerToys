---
name: release-note-generation
description: Toolkit for generating PowerToys stable or preview release notes from GitHub milestones, commit ranges, or Azure DevOps release-candidate builds. Use when asked to create release notes, summarize milestone PRs, generate changelog, prepare a draft preview release, calculate PR deltas across main and stable, update release documentation, manage PR milestones, or prepare and validate release assets.
license: Complete terms in LICENSE.txt
---

# Release Note Generation Skill

Generate professional PowerToys release notes by collecting merged PRs, summarizing each PR with the local CLI agent, grouping by label, and producing user-facing summaries. Stable and preview releases share the same PR metadata, attribution, grouping, and formatting rules.

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

Preview-release runs use an isolated subdirectory:

```text
Generated Files/ReleaseNotes/preview-<buildId>/
├── release-context.json
├── delta-commits.json
├── delta-prs.json
├── removed-prs.json
├── unattributed-commits.json
├── MemberList.md
├── milestone_prs.json
├── sorted_prs.csv
├── release-notes.md
├── hashes.md
├── release-manifest.json        # Local audit artifact; never uploaded
├── assets-manifest.json         # Local asset inventory; never uploaded
└── final-review.md
```

## When to Use This Skill

- Generate release notes for a milestone
- Summarize PRs merged in a release
- Generate per-PR review summaries locally for release-notes copy
- Assign milestones to PRs missing them
- Collect PRs between two commits/tags
- Update README.md for a new version
- Prepare GitHub release assets (download installers/symbols + compute hashes)
- Prepare a complete draft preview release from an ADO build URL or build ID
- Compare preview contents across `main` and `stable` branch transitions

## Prerequisites

- **GitHub CLI (`gh`) installed and authenticated** — The collection script uses `gh pr view` and `gh api graphql` to fetch PR metadata and co-author information. Run `gh auth status` to verify; if not logged in, run `gh auth login` first. See [Step 1.0.0](./references/step1-collection.md) for details.
- MCP Server: github-mcp-server installed (used to fetch PR diffs/files for the local-agent review step)
- For preview releases and [prepare-release-assets.ps1](./scripts/prepare-release-assets.ps1): **Azure CLI** authenticated against the Microsoft tenant (`az login`) with the `azure-devops` extension; access to the `microsoft/Dart` ADO project

## Required Variables

For a stable release, confirm `{{ReleaseVersion}}` with the user before starting.
For a preview release, do not request a version: derive it from the candidate ADO build.

| Variable | Description | Example |
|----------|-------------|---------|
| `{{ReleaseVersion}}` | Target release version | `0.98` |

Preview mode instead requires one ADO build URL or numeric build ID. It derives the version, source commit, branch, and previous release without asking the user.

## Scenario routing

Read [the scenario index](./references/scenarios/index.md), then follow only the selected scenario:

- [Stable release](./references/scenarios/stable-release.md) for milestone- or version-based release notes.
- [Preview release](./references/scenarios/preview-release.md) for an autonomous ADO-build-to-draft workflow.

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
| [get-release-build-metadata.ps1](./scripts/get-release-build-metadata.ps1) | Resolve and validate the candidate build identity, version, channel, intent, and source commit |
| [get-previous-published-release.ps1](./scripts/get-previous-published-release.ps1) | Select the latest published stable or preview release that predates the candidate queue time |
| [get-preview-release-delta.ps1](./scripts/get-preview-release-delta.ps1) | Calculate semantic added/removed PRs between exact release commits |
| [collect-pr-metadata.ps1](./scripts/collect-pr-metadata.ps1) | Normalize GitHub metadata for an explicit set of PR numbers |
| [new-preview-release-manifest.ps1](./scripts/new-preview-release-manifest.ps1) | Create the auditable build, baseline, and semantic-delta release manifest |
| [upsert-draft-preview-release.ps1](./scripts/upsert-draft-preview-release.ps1) | Create or update a draft prerelease without exposing a publish operation |
| [verify-draft-preview-release.ps1](./scripts/verify-draft-preview-release.ps1) | Verify draft flags, target commit, managed body, and uploaded assets |

## References

- [Sample Output](./references/SampleOutput.md) - Example summary formatting
- [Detailed Instructions](./references/Instruction.md) - Legacy full documentation

## Conventions

- **Terminal usage**: Disabled by default; only run scripts when user explicitly requests
- **Preview automation**: An explicit request to prepare a preview release, or invocation by the Prepare Preview Release agent, authorizes the canonical preview scripts
- **Preview manifests**: Keep `release-manifest.json` and `assets-manifest.json` in the local audit package; do not upload either file as a GitHub release asset
- **Preview note layout**: Place the `Installer Hashes` section immediately after the title and short public introduction, before `Highlights` and all change sections
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
| Candidate has no `release-metadata.json` | The metadata resolver uses pipeline-log fallback; ambiguous or conflicting values stop the run |
| First preview after switching branches has unexpected changes | Review `removed-prs.json` and `unattributed-commits.json`; see [preview delta resolution](./references/preview-delta-resolution.md) |
