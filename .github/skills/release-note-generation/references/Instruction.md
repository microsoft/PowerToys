## Background
This document describes how to collect pull requests for a milestone, request a GitHub Copilot code review for each, and produce release‑notes summaries grouped by label.

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

## Agent‑mode execution policy (important)
- By default, do NOT run terminal commands or PowerShell scripts beside the ps1 in this folder. Perform all collection, parsing, grouping, and summarization entirely in Agent mode using available files and MCP capabilities.
- Only execute existing scripts if the user explicitly asks you to (opt‑in). Otherwise, assume the input artifacts are present in `Generated Files/ReleaseNotes/` or will be provided.
- Do NOT create new scripts unless requested and justified.

## Prerequisites
- Windows with PowerShell 7+ (pwsh)
- GitHub CLI installed and authenticated to the target repo
  - gh version that supports Copilot review requests
  - Logged in: gh auth login (ensure repo scope)
- Access to the repository configured in the scripts (default: `microsoft/PowerToys`)
- GitHub Copilot code review enabled for the org/repo (required for requesting reviews)
- 'MCP Server: github-remote' is installed, please find it at [github-mcp-server](https://github.com/github/github-mcp-server)

## Files in this repo (overview)
- `dump-prs-information.ps1`: Fetches PRs for a milestone and outputs to `Generated Files/ReleaseNotes/`
  - Outputs: `milestone_prs.json` and `sorted_prs.csv`
  - CSV columns: `Id, Title, Labels, Author, Url, Body, CopilotSummary`
- `dump-prs-since-commit.ps1`: Fetches PRs between two commits/tags, outputs to `Generated Files/ReleaseNotes/`
- `group-prs-by-label.ps1`: Groups PRs by label, outputs to `Generated Files/ReleaseNotes/grouped_csv/`
- `collect-or-apply-milestones.ps1`: Manages milestone assignments, outputs to `Generated Files/ReleaseNotes/`
- `diff_prs.ps1`: Creates an incremental CSV by diffing two CSVs (in case more PRs cherry pick to stable)
- `MemberList.md`: Internal contributors list (used to decide when to add external thanks)
- `SampleOutput.md`: Example formatting for summary content

## Step-by-step

### Step 1: Collect PRs
Run `dump-prs-information.ps1` to export PRs for the target milestone (initial run, CopilotSummary likely empty)
- Run from repo root: `pwsh ./.github/skills/release-note-generation/scripts/dump-prs-information.ps1 -Milestone 'PowerToys 0.97' -OutputDir 'Generated Files/ReleaseNotes'`
- Or use commit range (preferred for stable branch releases):
  ```powershell
  # First checkout the stable branch and pull latest
  git fetch origin stable:stable
  git checkout stable
  git reset --hard origin/stable
  
  # Then run the script from the last release tag to HEAD
  pwsh ./.github/skills/release-note-generation/scripts/dump-prs-since-commit.ps1 `
      -StartCommit v0.96.1 `
      -EndCommit HEAD `
      -OutputCsv 'Generated Files/ReleaseNotes/sorted_prs.csv' `
      -OutputJson 'Generated Files/ReleaseNotes/milestone_prs.json'
  ```
- Outputs to `Generated Files/ReleaseNotes/`: `milestone_prs.json` and `sorted_prs.csv`.

### Step 2: Assign Milestones (REQUIRED)
**Before generating release notes**, ensure all collected PRs have the correct milestone assigned on GitHub.

⚠️ **CRITICAL:** Do NOT proceed to Step 3 until all PRs have milestones assigned.

**Preferred Method - PowerShell Script:**
```powershell
# From repo root, run with -ApplyMissing to set milestones on PRs that don't have one
pwsh ./.github/skills/release-note-generation/scripts/collect-or-apply-milestones.ps1 `
    -InputCsv 'Generated Files/ReleaseNotes/sorted_prs.csv' `
    -OutputCsv 'Generated Files/ReleaseNotes/prs_with_milestone.csv' `
    -DefaultMilestone 'PowerToys 0.97' `
    -ApplyMissing

# Dry run first to see what would be changed:
pwsh ./.github/skills/release-note-generation/scripts/collect-or-apply-milestones.ps1 `
    -InputCsv 'Generated Files/ReleaseNotes/sorted_prs.csv' `
    -DefaultMilestone 'PowerToys 0.97' `
    -ApplyMissing -WhatIf
```

**Script Safeguards:**
- The script checks each PR's current milestone before updating
- PRs that already have a milestone are **skipped** (not overwritten)
- Produces a summary: `Updated=X Skipped=Y Failed=Z`
- Outputs `prs_with_milestone.csv` for tracking

**Alternative - Agent Mode (MCP Tools):**
If running in Agent mode without terminal access:
1. Read `sorted_prs.csv` and check each PR's current milestone via MCP tools
2. For PRs missing the target milestone:
   - **IMPORTANT:** Only update PRs where `milestone` is `null` - never overwrite existing milestones
   - Use `mcp_github-remote_issue_write` with `method: 'update'` and `milestone: <number>`
   - Log which PRs were updated vs skipped
3. **Confidence < 80%**: If unsure whether a PR belongs to this milestone:
   - **ASK HUMAN** with suggestion format:
   - "PR #XXXXX 'Title' - Currently no milestone. Appears to be [reason]. Suggest: [Include in 0.97 / Exclude]. Confirm?"
   - Wait for human decision before proceeding

**Why this matters:** PRs collected by commit range may not have milestones set, especially cherry-picks to stable branches.

### Step 3: Label Unlabeled PRs (REQUIRED - Agent Mode)
**Before grouping**, ensure all PRs have appropriate labels for proper categorization.

⚠️ **CRITICAL:** Do NOT proceed to Step 4 until all PRs have labels assigned. PRs without labels will end up in `Unlabeled.csv` and won't appear in the correct release note sections.

**Process:**
1. Read `sorted_prs.csv` and identify PRs with empty `Labels` column
2. For each unlabeled PR, analyze Title, Body, and changed files to determine the correct label
3. **Confidence >= 80%**: Apply the label via MCP tools
4. **Confidence < 80%**: **ASK HUMAN** with suggestion:
   - Format: "PR #XXXXX 'Title' - Suggested label: `Product-XXX` (Confidence: XX%). Reason: [explanation]. Apply? [Yes/No/Different label]"
   - Wait for human confirmation

**Common Label Mappings:**
| Keywords/Patterns | Suggested Label |
| ----------------- | --------------- |
| Advanced Paste, AP, clipboard, paste, Foundry | `Product-Advanced Paste` |
| CmdPal, Command Palette, cmdpal | `Product-Command Palette` |
| FancyZones, zones, layout | `Product-FancyZones` |
| Screen Recorder, recording, GIF, MP4 | `Product-Screen Recorder` |
| Settings, settings-ui | `Product-Settings` |
| Installer, setup, MSI, MSIX, SpareApps | `Area-Setup/Install` |
| Build, pipeline, CI/CD | `Area-Build` |
| Test, unit test, UI test | `Area-Tests` |
| Localization, loc, translation | `Area-Localization` |
| Cursor Wrap | `Product-Cursor Wrap` |
| LightSwitch | `Product-LightSwitch` |

**Validation:** After labeling, re-run grouping - the `Unlabeled.csv` should be minimal or empty.

### Step 4: Request Copilot Reviews (Agent Mode)
Request Copilot reviews for each PR listed in the CSV in Agent mode (MUST NOT generate or run any ps1)
- Must use MCP tools "MCP Server: github-remote" in current Agent mode to request Copilot reviews for all PR Ids in `sorted_prs.csv`.

### Step 5: Refresh PR Data
Run `dump-prs-information.ps1` again
- This refresh collects the latest Copilot review body into the `CopilotSummary` column in `Generated Files/ReleaseNotes/sorted_prs.csv`.
- Also captures any label changes made in Step 3.

### Step 6: Group PRs by Label
Run `group-prs-by-label.ps1` to generate grouped CSVs
- Run: `pwsh ./.github/skills/release-note-generation/scripts/group-prs-by-label.ps1 -CsvPath 'Generated Files/ReleaseNotes/sorted_prs.csv' -OutDir 'Generated Files/ReleaseNotes/grouped_csv'`
- Outputs to `Generated Files/ReleaseNotes/grouped_csv/`
- **Check:** If `Unlabeled.csv` has many PRs, return to Step 3

### Step 7: Generate Summary Markdown (Agent Mode)
Summarize PRs into per-label Markdown files in Agent mode (MUST NOT generate or run any script in terminal nor ps1)
- Read the csv files in `Generated Files/ReleaseNotes/grouped_csv/` one by one
- For each label group, create a markdown file under `Generated Files/ReleaseNotes/grouped_md/` (create if missing). File name: sanitized label group name (same pattern as CSV) with `.md` extension. Example: `Area-Build.md`.
- Each markdown file content must follow the structure below (two sections) and preserve the PR order from the source CSV.
- Do not embed PR numbers in the bullet list lines; only link them in the table.
- If re-running, overwrite existing markdown files (idempotent generation).
- After generation, you should have a 1:1 correspondence between files in `grouped_csv/` and `grouped_md/` (excluding any intentionally skipped groups—document if skipped).
- Generate the summary md file as the following instruction in two parts:
  1. Markdown list: one concise, user-facing line per PR (no deep technical jargon). Use "Verbed" + "Scenario" + "Impact" as sentence structure. Use `Title`, `Body`, and `CopilotSummary` as sources.
     - If `Author` is NOT in `**/MemberList.md`, append a "Thanks @handle!" see `**/SampleOutput.md` as example.
     - Do NOT include PR numbers or IDs in the list line; keep the PR link only in the table mentioned in 2. below, please refer to `**/SampleOutput.md` as example.
     - If confidence to have enough information for summarization according to guideline above is < 70%, write: `Human Summary Needed: <PR full link>` on that line.
  2. Three-column table (in the same PR order):
     - Column 1: The concise, user-facing summary (the "cut version")
     - Column 2: PR link
     - Column 3: Confidence (e.g., `High/Medium/Low`) and the reasoning if < 70%

### Step 8: Update README.md
According the generated `Generated Files/ReleaseNotes/grouped_md/*.md`, update the repo root's `README.md`. Here is the guideline:
 a. Replace all versioned references in `README.md`:
	 - Bump current release heading (e.g. **Version 0.xx**) by +0.01.
	 - Shift link references: previous `[github-current-release-work]` becomes old version; increment `[github-next-release-work]` to point to the following milestone.
	 - Update download asset filenames (e.g. `PowerToysSetup-0.94.0-...` → `PowerToysSetup-0.95.0-...`).
 b. Build the What's New content from `Generated Files/ReleaseNotes/grouped_md/`:
	 - Combine `Area-Build` and `Area-Tests` entries under a single `Development` subsection (keep bullet order from CSV).
	 - Each other `Product-*` group gets its own subsection titled by the module name.
	 - Order subsections alphabetically by their heading text, with **Highlights** always first and **Development** always last (e.g., Environment Variables, File Locksmith, Find My Mouse, ... , ZoomIt, Development).
	 - Copy bullet lines verbatim from the corresponding `grouped_md` files (preserve punctuation and any trailing `Thanks @handle!`). Do NOT add, remove, or re‑evaluate thanks in the README stage.
 c. Highlights: choose up to 10 bullets focused on user-visible feature additions or impactful fixes (avoid purely internal refactors). Use pattern: `Module/Feature <past-tense verb> <scenario> <impact>`.
 d. Keep wording concise (aim 1 line per bullet), no PR numbers, no deep implementation details.
 e. After updating, verify total highlight count ≤ 10 and that all internal contributors are not thanked.

## Notes and conventions
- Terminal usage: Disabled by default. Do NOT run terminal commands or ps1 scripts unless the user explicitly instructs you to.
- Do NOT generate/add new ps1 until instructed (and explain why a new script is needed).
- Label filtering in `dump-prs-information.ps1` currently keeps labels matching: `Product-*`, `Area-*`, `Github*`, `*Plugin`, `Issue-*`.
- CSV columns are single‑line (line breaks removed) for easier processing.
- Keep PRs in the same order as in `sorted_prs.csv` when building summaries.
- Sanitize filenames: replace spaces with `-`, strip or replace characters that are invalid on Windows (`<>:"/\\|?*`).