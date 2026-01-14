---
name: release-note-generation
description: Toolkit for generating PowerToys release notes from GitHub milestone PRs or commit ranges. Use when asked to create release notes, summarize milestone PRs, generate changelog, prepare release documentation, request Copilot reviews for PRs, update README for a new release, manage PR milestones, or collect PRs between commits/tags. Supports PR collection by milestone or commit range, milestone assignment, grouping by label, summarization with external contributor attribution, and README version bumping.
license: Complete terms in LICENSE.txt
---

# Release Note Generation Skill

Generate professional release notes for PowerToys milestones by collecting merged PRs, requesting Copilot code reviews, grouping by label, and producing user-facing summaries.

## Output Directory

All generated artifacts are placed under `Generated Files/ReleaseNotes/` at the repository root. This folder is gitignored.

**Structure:**
```
Generated Files/
└── ReleaseNotes/
    ├── milestone_prs.json      # Raw PR data from GitHub
    ├── sorted_prs.csv          # Sorted PR list with Copilot summaries
    ├── prs_with_milestone.csv  # Milestone assignment tracking
    ├── grouped_csv/            # PRs grouped by label (one CSV per label)
    └── grouped_md/             # Generated markdown summaries per label
```

## When to Use This Skill

- User asks to generate release notes for a milestone
- User wants to summarize PRs merged in a release
- User needs to prepare changelog documentation
- User wants to request Copilot reviews for milestone PRs
- User asks to update README.md for a new version release
- User needs to create What's New content from merged PRs
- User wants to group PRs by label/area for release documentation
- User needs to collect PRs between two commits or tags
- User wants to assign milestones to PRs that are missing them
- User needs to create incremental PR lists (cherry-picks to stable)

## Agent-Mode Execution Policy (IMPORTANT)

- **By default, do NOT run terminal commands or PowerShell scripts** unless user explicitly requests
- Perform all collection, parsing, grouping, and summarization in Agent mode using MCP capabilities
- Only execute existing scripts if the user explicitly asks (opt-in)
- Do NOT create new scripts unless requested and justified

## Prerequisites

- Windows with PowerShell 7+ (pwsh)
- GitHub CLI (`gh`) installed and authenticated with repo read/write access
- Git available in working directory (for commit-range based collection)
- MCP Server: github-remote installed ([github-mcp-server](https://github.com/github/github-mcp-server))
- GitHub Copilot code review enabled for the org/repo
- Access to target repository (default: `microsoft/PowerToys`)

## Step-by-Step Workflow

### Phase 1: Collect PRs

**Option A - By Milestone:**
```powershell
pwsh ./.github/skills/release-note-generation/scripts/dump-prs-information.ps1 -Milestone 'PowerToys 0.97' -OutputDir 'Generated Files/ReleaseNotes'
```

**Option B - By Commit Range:**
```powershell
pwsh ./.github/skills/release-note-generation/scripts/dump-prs-since-commit.ps1 -StartCommit <sha/tag> -EndCommit HEAD -OutputDir 'Generated Files/ReleaseNotes'
```

Detects both merge commits (`Merge pull request #12345`) and squash commits (`Feature (#12345)`).

**Output** (in `Generated Files/ReleaseNotes/`):

- `milestone_prs.json` - raw PR data
- `sorted_prs.csv` - sorted PR list with columns: Id, Title, Labels, Author, Url, Body, CopilotSummary

### Phase 2: Assign Milestones to Collected PRs (REQUIRED)

**Before generating release notes**, ensure all collected PRs have the correct milestone assigned.

⚠️ **CRITICAL:** Do NOT proceed to Phase 3 until all PRs have milestones assigned.

**Preferred Method - PowerShell Script:**
```powershell
# Dry run first to see what would be changed:
pwsh ./.github/skills/release-note-generation/scripts/collect-or-apply-milestones.ps1 `
    -InputCsv 'Generated Files/ReleaseNotes/sorted_prs.csv' `
    -OutputCsv 'Generated Files/ReleaseNotes/prs_with_milestone.csv' `
    -DefaultMilestone 'PowerToys 0.97' `
    -ApplyMissing -WhatIf

# Apply for real:
pwsh ./.github/skills/release-note-generation/scripts/collect-or-apply-milestones.ps1 `
    -InputCsv 'Generated Files/ReleaseNotes/sorted_prs.csv' `
    -OutputCsv 'Generated Files/ReleaseNotes/prs_with_milestone.csv' `
    -DefaultMilestone 'PowerToys 0.97' `
    -ApplyMissing
```

**Script Safeguards:**
- Checks each PR's current milestone before updating
- PRs that already have a milestone are **skipped** (not overwritten)
- Produces summary: `Updated=X Skipped=Y Failed=Z`

**Alternative - Agent Mode (MCP Tools):**
If running without terminal access:
1. Read `sorted_prs.csv` and check each PR's current milestone via MCP tools
2. For PRs missing the target milestone:
   - **IMPORTANT:** Only update PRs where `milestone` is `null` - never overwrite existing milestones
   - Use `mcp_github-remote_issue_write` with `method: 'update'` and `milestone: <number>`
3. **Confidence < 80%**: If unsure whether a PR belongs to this milestone:
   - **ASK HUMAN** with suggestion: "PR #XXXXX 'Title' appears to be [reason]. Suggest: [Include/Exclude]. Confirm?"

**Validation:** After assignment, all PRs in `sorted_prs.csv` should have the target milestone on GitHub.

### Phase 3: Label Unlabeled PRs (REQUIRED - Agent Mode)

**Before grouping**, ensure all PRs have appropriate labels for categorization.

⚠️ **CRITICAL:** Do NOT proceed to Phase 4 until all PRs have labels assigned. PRs without labels will end up in `Unlabeled.csv` and won't appear in the correct release note sections.

**Steps:**
1. Read `sorted_prs.csv` and identify PRs with empty or missing `Labels` column
2. For each unlabeled PR, analyze Title, Body, and file paths changed to suggest labels:
   - **Product labels**: `Product-Advanced Paste`, `Product-Command Palette`, `Product-FancyZones`, `Product-Screen Recorder`, etc.
   - **Area labels**: `Area-Build`, `Area-Tests`, `Area-Setup/Install`, `Area-Localization`, etc.
3. **Confidence >= 80%**: Apply the suggested label via MCP tools (`mcp_github-remote_issue_write` with `labels` parameter)
4. **Confidence < 80%**: **ASK HUMAN** with suggestion:
   - Format: "PR #XXXXX 'Title' - Suggested label: `Product-XXX` (Confidence: XX%). Reason: [explanation]. Apply? [Yes/No/Different label]"
   - Wait for human confirmation before applying
5. Update `sorted_prs.csv` with the new labels or re-run collection script

**Common Label Mappings:**

| Keywords/Patterns | Suggested Label |
| ----------------- | --------------- |
| Advanced Paste, AP, clipboard, paste | `Product-Advanced Paste` |
| CmdPal, Command Palette, cmdpal | `Product-Command Palette` |
| FancyZones, zones, layout | `Product-FancyZones` |
| Screen Recorder, recording, GIF, MP4 | `Product-Screen Recorder` |
| Settings, settings-ui | `Product-Settings` |
| Installer, setup, MSI, MSIX | `Area-Setup/Install` |
| Build, pipeline, CI/CD | `Area-Build` |
| Test, unit test, UI test | `Area-Tests` |
| Localization, loc, translation | `Area-Localization` |
| Foundry, AI, LLM | `Product-Advanced Paste` (AI features) |

### Phase 4: Request Copilot Reviews (Agent Mode)

Use MCP tools to request Copilot reviews for all PRs in `Generated Files/ReleaseNotes/sorted_prs.csv`:

- Use `mcp_github-remote_request_copilot_review` for each PR ID
- Do NOT generate or run scripts for this step

### Phase 5: Refresh PR Data

Re-run the collection script to capture Copilot review summaries into the `CopilotSummary` column.

### Phase 6: Group PRs by Label

```powershell
pwsh ./.github/skills/release-note-generation/scripts/group-prs-by-label.ps1 -CsvPath 'Generated Files/ReleaseNotes/sorted_prs.csv' -OutDir 'Generated Files/ReleaseNotes/grouped_csv'
```

Creates `Generated Files/ReleaseNotes/grouped_csv/` with one CSV per label combination.

**Validation:** The `Unlabeled.csv` file should be minimal (ideally empty). If many PRs remain unlabeled, return to Phase 3.

### Phase 7: Generate Summary Markdown (Agent Mode)

For each CSV in `Generated Files/ReleaseNotes/grouped_csv/`, create a markdown file in `Generated Files/ReleaseNotes/grouped_md/`:

**Structure per file:**
1. **Bullet list** - one concise, user-facing line per PR:
   - Use "Verbed + Scenario + Impact" sentence structure
   - Source from Title, Body, and CopilotSummary
   - If Author NOT in [MemberList.md](./references/MemberList.md), append "Thanks @handle!"
   - Do NOT include PR numbers in bullet lines
   - If confidence < 70%, write: `Human Summary Needed: <PR full link>`

2. **Three-column table** (same PR order):
   - Column 1: Concise summary
   - Column 2: PR link
   - Column 3: Confidence level (High/Medium/Low) with reasoning if < 70%

### Phase 8: Update README.md

Using generated `Generated Files/ReleaseNotes/grouped_md/*.md` files:

1. **Version references:**
   - Bump version heading (e.g., `**Version 0.94**` → `**Version 0.95**`)
   - Update milestone link references
   - Update download asset filenames

2. **Build What's New content:**
   - Combine `Area-Build` and `Area-Tests` under **Development** subsection
   - Each `Product-*` group gets its own subsection by module name
   - Order alphabetically, **Highlights** first, **Development** last

3. **Highlights section:**
   - Select up to 10 bullets focused on user-visible features/impactful fixes
   - Avoid internal refactors
   - Pattern: `Module/Feature <past-tense verb> <scenario> <impact>`

## Available Scripts

| Script | Purpose | Output |
| ------ | ------- | ------ |
| [dump-prs-information.ps1](./scripts/dump-prs-information.ps1) | Fetch milestone PRs with Copilot summaries | `Generated Files/ReleaseNotes/` |
| [dump-prs-since-commit.ps1](./scripts/dump-prs-since-commit.ps1) | Fetch PRs between two commits/tags | `Generated Files/ReleaseNotes/` |
| [group-prs-by-label.ps1](./scripts/group-prs-by-label.ps1) | Group PRs by label into separate CSVs | `Generated Files/ReleaseNotes/grouped_csv/` |
| [diff_prs.ps1](./scripts/diff_prs.ps1) | Create incremental CSV from two exports | `Generated Files/ReleaseNotes/` |
| [collect-or-apply-milestones.ps1](./scripts/collect-or-apply-milestones.ps1) | Collect/assign milestones to PRs | `Generated Files/ReleaseNotes/` |
| [add-milestone-column.ps1](./scripts/add-milestone-column.ps1) | Add milestone column to PR CSV | `Generated Files/ReleaseNotes/` |
| [set-milestones-missing.ps1](./scripts/set-milestones-missing.ps1) | Assign milestone to PRs missing one | Summary table |

## Milestone Management

### Collect Existing Milestones

```powershell
pwsh ./.github/skills/release-note-generation/scripts/collect-or-apply-milestones.ps1 -OutputCsv 'Generated Files/ReleaseNotes/prs_with_milestone.csv'
```

### Assign Milestones to Missing PRs

```powershell
# Dry run first
pwsh ./.github/skills/release-note-generation/scripts/collect-or-apply-milestones.ps1 `
    -InputCsv 'Generated Files/ReleaseNotes/sorted_prs.csv' `
    -DefaultMilestone 'PowerToys 0.97' `
    -ApplyMissing -WhatIf

# Apply for real
pwsh ./.github/skills/release-note-generation/scripts/collect-or-apply-milestones.ps1 `
    -InputCsv 'Generated Files/ReleaseNotes/sorted_prs.csv' `
    -OutputCsv 'Generated Files/ReleaseNotes/prs_with_milestone.csv' `
    -DefaultMilestone 'PowerToys 0.97' `
    -ApplyMissing
```

### Local-Only Assignment (CSV output only, no GitHub changes)

```powershell
pwsh ./.github/skills/release-note-generation/scripts/collect-or-apply-milestones.ps1 `
    -InputCsv 'Generated Files/ReleaseNotes/sorted_prs.csv' `
    -LocalAssign -DefaultMilestone 'PowerToys 0.97'
```

## Creating Incremental Release Notes

For cherry-picks to stable branches:

```powershell
# Compare previous export with current
pwsh ./.github/skills/release-note-generation/scripts/diff_prs.ps1 -BaseCsv 'Generated Files/ReleaseNotes/sorted_prs_prev.csv' -AllCsv 'Generated Files/ReleaseNotes/sorted_prs.csv' -OutCsv 'Generated Files/ReleaseNotes/sorted_prs_incremental.csv'
```

## References

- [Member List](./references/MemberList.md) - Internal contributors (no thanks needed)
- [Sample Output](./references/SampleOutput.md) - Example formatting for summaries
- [Detailed Instructions](./references/Instruction.md) - Complete workflow documentation

## Conventions

- **Output directory**: All artifacts go to `Generated Files/ReleaseNotes/` (gitignored)
- **Terminal usage**: Disabled by default. Only run scripts when user explicitly requests
- **PR order**: Preserve order from `sorted_prs.csv` in all outputs
- **Label filtering**: Keeps `Product-*`, `Area-*`, `Github*`, `*Plugin`, `Issue-*`
- **Filename sanitization**: Replace spaces with `-`, remove invalid Windows characters
- **Idempotent generation**: Re-running overwrites existing files
- **CSV columns**: Single-line (line breaks removed) for easier processing

## Troubleshooting

| Issue | Solution |
| ----- | -------- |
| `gh` command not found | Install GitHub CLI and add to PATH |
| No PRs returned | Verify milestone title matches exactly (case-sensitive) |
| Empty CopilotSummary | Request Copilot reviews first, then re-run dump script |
| Missing contributor | Check MemberList.md for typos in GitHub handles |
| Milestone not found | Ensure milestone exists and is open on GitHub |
| No commits in range | Verify StartCommit and EndCommit are valid refs |
| PR pattern not matched | Script detects merge (`#12345`) and squash (`(#12345)`) patterns |
