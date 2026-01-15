# Phase 1-2: PR Collection and Milestone Assignment

## Required Variables

⚠️ **Before starting**, confirm these values with the user:

| Variable | Description | Example |
|----------|-------------|---------|
| `{{ReleaseVersion}}` | Target release version | `0.97` |
| `{{PreviousReleaseTag}}` | Previous release tag from releases page | `v0.96.1` |

**If user hasn't specified `{{ReleaseVersion}}`, ASK:** "What release version are we generating notes for? (e.g., 0.97)"

---

## Phase 1: Collect PRs

### Step 1: Get the previous release commit

1. Open the [PowerToys releases page](https://github.com/microsoft/PowerToys/releases/)
2. Find the latest release (e.g., v0.96.1, which should be at the top)
3. Copy the full commit SHA {{SHALastRelease}} (e.g., `b62f642c4f3e5d6e8f7a9c0b1d2e3f4g5h6i7j8k`)

### Step 2: Run collection script against stable branch

```powershell
# Collect PRs from previous release to current HEAD of stable branch
pwsh ./.github/skills/release-note-generation/scripts/dump-prs-since-commit.ps1 `
    -StartCommit '{{SHALastRelease}}' `
    -EndCommit HEAD `
    -Branch 'stable' `
    -OutputDir 'Generated Files/ReleaseNotes'
```

**Parameters:**
- `-StartCommit` - Previous release tag or commit SHA (exclusive - PRs after this)
- `-EndCommit` - Usually `HEAD` for current state of stable branch
- `-Branch` - Always use `stable` branch, not `main`
- `-OutputDir` - Output directory for generated files

The script detects both merge commits (`Merge pull request #12345`) and squash commits (`Feature (#12345)`).

**Output** (in `Generated Files/ReleaseNotes/`):
- `milestone_prs.json` - raw PR data
- `sorted_prs.csv` - sorted PR list with columns: Id, Title, Labels, Author, Url, Body, CopilotSummary, NeedThanks

---

## Phase 2: Assign Milestones to Collected PRs (REQUIRED)

**Before generating release notes**, ensure all collected PRs have the correct milestone assigned.

⚠️ **CRITICAL:** Do NOT proceed to labeling until all PRs have milestones assigned.

### Step 1: Check current milestone status (dry run)

```powershell
# Dry run first to see what would be changed:
pwsh ./.github/skills/release-note-generation/scripts/collect-or-apply-milestones.ps1 `
    -InputCsv 'Generated Files/ReleaseNotes/sorted_prs.csv' `
    -OutputCsv 'Generated Files/ReleaseNotes/prs_with_milestone.csv' `
    -DefaultMilestone 'PowerToys {{ReleaseVersion}}' `
    -ApplyMissing -WhatIf
```

This queries GitHub for each PR's current milestone and shows which PRs would be updated.

### Step 2: Apply milestones to PRs missing them

```powershell
# Apply for real:
pwsh ./.github/skills/release-note-generation/scripts/collect-or-apply-milestones.ps1 `
    -InputCsv 'Generated Files/ReleaseNotes/sorted_prs.csv' `
    -OutputCsv 'Generated Files/ReleaseNotes/prs_with_milestone.csv' `
    -DefaultMilestone 'PowerToys {{ReleaseVersion}}' `
    -ApplyMissing
```

**Script Behavior:**
- Queries each PR's current milestone from GitHub
- PRs that already have a milestone are **skipped** (not overwritten)
- PRs missing a milestone get the default milestone applied
- Outputs `prs_with_milestone.csv` with (Id, Milestone) columns
- Produces summary: `Updated=X Skipped=Y Failed=Z`

**Validation:** After assignment, all PRs in `prs_with_milestone.csv` should have the target milestone.

---

## Additional Commands

### Collect milestones only (no changes to GitHub)
```powershell
pwsh ./.github/skills/release-note-generation/scripts/collect-or-apply-milestones.ps1 `
    -InputCsv 'Generated Files/ReleaseNotes/sorted_prs.csv' `
    -OutputCsv 'Generated Files/ReleaseNotes/prs_with_milestone.csv'
```

### Local assignment only (fill blanks in CSV, no GitHub changes)
```powershell
pwsh ./.github/skills/release-note-generation/scripts/collect-or-apply-milestones.ps1 `
    -InputCsv 'Generated Files/ReleaseNotes/sorted_prs.csv' `
    -OutputCsv 'Generated Files/ReleaseNotes/prs_with_milestone.csv' `
    -DefaultMilestone 'PowerToys {{ReleaseVersion}}' `
    -LocalAssign
```
