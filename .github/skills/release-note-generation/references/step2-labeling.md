# Step 2: Label Unlabeled PRs

## 2.0 To-do
- 2.1 Identify unlabeled PRs (Agent Mode)
- 2.2 Suggest labels (Agent Mode)
- 2.3 Human label low-confidence PRs
- 2.4 Recheck labels, delete Unlabeled.csv, and re-collect

**Before grouping**, ensure all PRs have appropriate labels for categorization.

⚠️ **CRITICAL:** Do NOT proceed to grouping until all PRs have labels assigned. PRs without labels will end up in `Unlabeled.csv` and won't appear in the correct release note sections.

## 2.1 Identify unlabeled PRs (Agent Mode)

Read `sorted_prs.csv` and identify PRs with empty or missing `Labels` column.

For each unlabeled PR, analyze:
- **Title** - Often contains module name or feature
- **Body** - PR description with context
- **CopilotSummary** - AI-generated summary of changes

## 2.2 Suggest labels (Agent Mode)

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
```

Sort by confidence (low first) so human reviews uncertain ones first.

After writing `prs_label_review.md`, **generate `prs_to_label.csv`, apply labels, and re-run collection** so the CSV/labels stay in sync:

```powershell
# Generate CSV from suggestions (agent)
# Apply labels
pwsh ./.github/skills/release-note-generation/scripts/apply-labels.ps1 `
    -InputCsv 'Generated Files/ReleaseNotes/prs_to_label.csv'

# Refresh collection
pwsh ./.github/skills/release-note-generation/scripts/dump-prs-since-commit.ps1 `
    -StartCommit '{{PreviousReleaseTag}}' -Branch 'stable' `
    -OutputDir 'Generated Files/ReleaseNotes'
```

## 2.3 Human label low-confidence PRs

Ask the human to label **low-confidence** PRs directly (in GitHub). Skip any they decide not to label.

## 2.4 Recheck labels, delete Unlabeled.csv, and re-collect

Recheck that all PRs now have labels. Delete `Unlabeled.csv` (if present), then re-run the collection script to update `sorted_prs.csv`:

```powershell
# Remove stale unlabeled output if it exists
Remove-Item 'Generated Files/ReleaseNotes/Unlabeled.csv' -ErrorAction SilentlyContinue
```

```powershell
pwsh ./.github/skills/release-note-generation/scripts/dump-prs-since-commit.ps1 `
    -StartCommit '{{PreviousReleaseTag}}' -Branch 'stable' `
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
- `GitHub*`
- `*Plugin`
- `Issue-*`

Other labels are ignored for grouping purposes.
