# Phase 3: Label Unlabeled PRs

**Before grouping**, ensure all PRs have appropriate labels for categorization.

⚠️ **CRITICAL:** Do NOT proceed to grouping until all PRs have labels assigned. PRs without labels will end up in `Unlabeled.csv` and won't appear in the correct release note sections.

## Step 1: Identify unlabeled PRs (Agent Mode)

Read `sorted_prs.csv` and identify PRs with empty or missing `Labels` column.

For each unlabeled PR, analyze:
- **Title** - Often contains module name or feature
- **Body** - PR description with context
- **CopilotSummary** - AI-generated summary of changes

## Step 2: Suggest labels (Agent Mode)

For each unlabeled PR, suggest an appropriate label based on the content analysis.

**Output:** Create `Generated Files/ReleaseNotes/prs_label_review.md` with the following format:

```markdown
# PR Label Review

Generated: YYYY-MM-DD HH:mm:ss

## Summary
- Total unlabeled PRs: X
- High confidence: X
- Medium confidence: X  
- Low confidence: X

---

## PRs Needing Review (sorted by confidence, low first)

| PR | Title | Suggested Label | Confidence | Reason |
|----|-------|-----------------|------------|--------|
| [#12347](url) | Some generic fix | ??? | Low | Unclear from content |
| [#12346](url) | Update dependencies | `Area-Build` | Medium | Body mentions NuGet packages |
| [#12345](url) | Fix FancyZones crash | `Product-FancyZones` | High | Title mentions FancyZones |
```

Sort by confidence (low first) so human reviews uncertain ones first.

## Step 3: Human review

Present the suggestions to human for approval. Human may:
- Approve suggested label
- Specify a different label
- Skip the PR (leave unlabeled)

## Step 4: Apply approved labels

After human approval, create a CSV file `prs_to_label.csv` with approved labels:

```csv
Id,Label
12345,Product-FancyZones
12346,Area-Build
```

Then run the apply script:

```powershell
# Dry run first
pwsh ./.github/skills/release-note-generation/scripts/apply-labels.ps1 `
    -InputCsv 'Generated Files/ReleaseNotes/prs_to_label.csv' -WhatIf

# Apply for real
pwsh ./.github/skills/release-note-generation/scripts/apply-labels.ps1 `
    -InputCsv 'Generated Files/ReleaseNotes/prs_to_label.csv'
```

## Step 5: Re-run collection

After applying labels, re-run the collection script to update `sorted_prs.csv`:

```powershell
pwsh ./.github/skills/release-note-generation/scripts/dump-prs-since-commit.ps1 `
    -StartCommit '{{PreviousReleaseTag}}' -EndCommit HEAD -Branch 'stable' `
    -OutputDir 'Generated Files/ReleaseNotes'
```

---

## Common Label Mappings

| Keywords/Patterns | Suggested Label |
| ----------------- | --------------- |
| Advanced Paste, AP, clipboard, paste | `Product-Advanced Paste` |
| CmdPal, Command Palette, cmdpal | `Product-Command Palette` |
| FancyZones, zones, layout | `Product-FancyZones` |
| ZoomIt, zoom, screen annotation | `Product-ZoomIt` |
| Settings, settings-ui, Quick Access, flyout | `Product-Settings` |
| Installer, setup, MSI, MSIX, WiX | `Area-Setup/Install` |
| Build, pipeline, CI/CD, msbuild | `Area-Build` |
| Test, unit test, UI test, fuzz | `Area-Tests` |
| Localization, loc, translation, resw | `Area-Localization` |
| Foundry, AI, LLM | `Product-Advanced Paste` (AI features) |
| Mouse Without Borders, MWB | `Product-Mouse Without Borders` |
| PowerRename, rename, regex | `Product-PowerRename` |
| Peek, preview, file preview | `Product-Peek` |
| Image Resizer, resize | `Product-Image Resizer` |
| LightSwitch, theme, dark mode | `Product-LightSwitch` |
| Quick Accent, accent, diacritics | `Product-Quick Accent` |
| Awake, keep awake, caffeine | `Product-Awake` |
| ColorPicker, color picker, eyedropper | `Product-ColorPicker` |
| Hosts, hosts file | `Product-Hosts` |
| Keyboard Manager, remap | `Product-Keyboard Manager` |
| Mouse Highlighter | `Product-Mouse Highlighter` |
| Mouse Jump | `Product-Mouse Jump` |
| Find My Mouse | `Product-Find My Mouse` |
| Mouse Pointer Crosshairs | `Product-Mouse Pointer Crosshairs` |
| Shortcut Guide | `Product-Shortcut Guide` |
| Text Extractor, OCR, PowerOCR | `Product-Text Extractor` |
| Workspaces | `Product-Workspaces` |
| File Locksmith | `Product-File Locksmith` |
| Crop And Lock | `Product-CropAndLock` |
| Environment Variables | `Product-Environment Variables` |
| New+ | `Product-New+` |

## Label Filtering Rules

The grouping script keeps labels matching these patterns:
- `Product-*`
- `Area-*`
- `Github*`
- `*Plugin`
- `Issue-*`

Other labels are ignored for grouping purposes.
