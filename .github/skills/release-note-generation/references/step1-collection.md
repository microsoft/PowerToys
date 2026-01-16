# Step 1: Collection and Milestones

## 1.0 To-do
- 1.0.1 Generate MemberList.md (REQUIRED)
- 1.1 Collect PRs
- 1.2 Assign Milestones (REQUIRED)

## Required Variables

⚠️ **Before starting**, confirm these values with the user:

| Variable | Description | Example |
|----------|-------------|---------|
| `{{ReleaseVersion}}` | Target release version | `0.97` |
| `{{PreviousReleaseTag}}` | Previous release tag from releases page | `v0.96.1` |

**If user hasn't specified `{{ReleaseVersion}}`, ASK:** "What release version are we generating notes for? (e.g., 0.97)"

**`{{PreviousReleaseTag}}` is derived from the releases page, not user input.** Use the latest published release tag (top of the page). You will use its tag name and tag commit SHA in Step 1.

---

## 1.0.1 Generate MemberList.md (REQUIRED)

Create `Generated Files/ReleaseNotes/MemberList.md` from the **PowerToys core team** section in [COMMUNITY.md](../../../COMMUNITY.md).

Rules:
- One GitHub username per line, **no** `@` prefix.
- Use the usernames exactly as listed in the core team section.
- Do not include former team members or other sections.

Example (format only):
```
example-user
another-user
```

---

## 1.1 Collect PRs

### 1.1.1 Get the previous release commit

1. Open the [PowerToys releases page](https://github.com/microsoft/PowerToys/releases/)
2. Find the latest release (e.g., v0.96.1, which should be at the top)
3. Set `{{PreviousReleaseTag}}` to that tag name (e.g., `v0.96.1`)
4. Copy the full tag commit SHA as `{{SHALastRelease}}`


**If the release SHA is not in your branch history:** Use the helper script to find an equivalent commit on the target branch by matching the commit title:

```powershell
pwsh ./.github/skills/release-note-generation/scripts/find-commit-by-title.ps1 `
    -Commit '{{SHALastRelease}}' `
    -Branch 'stable'
```

### 1.1.2 Run collection script against stable branch

```powershell
# Collect PRs from previous release to current HEAD of stable branch
pwsh ./.github/skills/release-note-generation/scripts/dump-prs-since-commit.ps1 `
    -StartCommit '{{SHALastRelease}}' `
    -Branch 'stable' `
    -OutputDir 'Generated Files/ReleaseNotes'
```

**Parameters:**
- `-StartCommit` - Previous release tag or commit SHA (exclusive)
- `-Branch` - Always use `stable` branch, not `main` (script uses `origin/stable` as the end ref)
- `-EndCommit` - Optional override if you need a custom end ref
- `-OutputDir` - Output directory for generated files

**Reliability check:** If the script reports “No commits found”, the stable branch has not moved since the last release. In that case, either:
- Confirm this is expected and stop (no new release notes), or
- Re-run against `main` to gather pending changes for the next release cycle.

The script detects both merge commits (`Merge pull request #12345`) and squash commits (`Feature (#12345)`).

**Output** (in `Generated Files/ReleaseNotes/`):
- `milestone_prs.json` - raw PR data
- `sorted_prs.csv` - sorted PR list with columns: Id, Title, Labels, Author, Url, Body, CopilotSummary, NeedThanks

---

## 1.2 Assign Milestones (REQUIRED)

**Before generating release notes**, ensure all collected PRs have the correct milestone assigned.

⚠️ **CRITICAL:** Do NOT proceed to labeling until all PRs have milestones assigned.

### 1.2.1 Check current milestone status (dry run)

```powershell
# Dry run first to see what would be changed:
pwsh ./.github/skills/release-note-generation/scripts/collect-or-apply-milestones.ps1 `
    -InputCsv 'Generated Files/ReleaseNotes/sorted_prs.csv' `
    -OutputCsv 'Generated Files/ReleaseNotes/prs_with_milestone.csv' `
    -DefaultMilestone 'PowerToys {{ReleaseVersion}}' `
    -ApplyMissing -WhatIf
```

This queries GitHub for each PR's current milestone and shows which PRs would be updated.

### 1.2.2 Apply milestones to PRs missing them

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
