# Generating User-Facing Descriptions

This sub-skill explains how to transform technical PR/commit info into user-friendly changelog entries.

## Information Sources (Priority Order)

1. **PR Title** - Often the best summary, already user-facing
2. **PR Description/Body** - Look for "What does this PR do?" or summary sections
3. **Commit Message** - First line is usually descriptive
4. **Changed Files** - Infer the impact from what was modified
5. **PR Labels** - Indicate type (bug, feature, enhancement)

## Data Collection Command

```powershell
# Get all relevant info for a PR
$prNumber = 12345
$prData = gh pr view $prNumber --repo microsoft/PowerToys --json title,body,author,files,labels,mergedAt

# Parse the data
$pr = $prData | ConvertFrom-Json
Write-Host "Title: $($pr.title)"
Write-Host "Author: $($pr.author.login)"
Write-Host "Labels: $($pr.labels.name -join ', ')"
Write-Host "Files changed: $($pr.files.Count)"
Write-Host "Body preview: $($pr.body.Substring(0, [Math]::Min(500, $pr.body.Length)))"
```

## Transformation Rules

| Source Info | Transformation | Example Output |
|-------------|----------------|----------------|
| PR Title: "Fix null reference in FancyZones editor" | Describe the fix from user perspective | "Fixed a crash that could occur when editing zone layouts." |
| PR Title: "Add support for XYZ format" | State the new capability | "Added support for XYZ format in File Explorer preview." |
| PR Title: "[FancyZones] Refactor grid logic" | Check if user-visible; if not → Development section | (Development) "Refactored FancyZones grid logic for improved maintainability." |
| PR with label "bug" | Frame as a fix | "Fixed an issue where..." |
| PR with label "enhancement" | Frame as improvement | "Improved..." or "Enhanced..." |

## Description Generation Process

```
┌────────────────────────────────────────────────────────────────┐
│ INPUT: PR #12345                                               │
│ Title: "Fix Awake timer drift after system sleep"              │
│ Body: "The timer was resetting incorrectly when the system     │
│        resumed from sleep, causing the countdown to be wrong." │
│ Author: @daverayment                                           │
│ Labels: [bug, Product-Awake]                                   │
└────────────────────────────────────────────────────────────────┘
                              ▼
┌────────────────────────────────────────────────────────────────┐
│ ANALYSIS:                                                      │
│ 1. Is it user-facing? YES (timer behavior affects users)       │
│ 2. Category: Bug fix                                           │
│ 3. Module: Awake (from label + file paths)                     │
│ 4. Author in core team? NO → needs attribution                 │
└────────────────────────────────────────────────────────────────┘
                              ▼
┌────────────────────────────────────────────────────────────────┐
│ OUTPUT:                                                        │
│ "The Awake countdown timer now stays accurate over long        │
│  periods. Thanks [@daverayment](https://github.com/daverayment)!" │
└────────────────────────────────────────────────────────────────┘
```

## Writing Style Guidelines

**DO:**
- Start with what changed or what users can now do
- Use active voice: "Added...", "Fixed...", "Improved..."
- Be specific about the benefit to users
- Keep it concise (1-2 sentences max)
- Include link to documentation if it's a new feature

**DON'T:**
- Use technical jargon users won't understand
- Reference internal code names, file names, or class names
- Say "Fixed bug" without explaining what was wrong
- Include PR numbers in the description (they're tracked separately)

## Example Transformations

| Technical PR Title | User-Facing Description |
|--------------------|------------------------|
| "Fix NRE in FZEditor when zones is null" | "Fixed a crash that could occur when opening the FancyZones editor with no saved layouts." |
| "Add ExifTool integration for PowerRename" | "PowerRename can now extract and use photo metadata (EXIF, XMP) in renaming patterns like `%Camera`, `%Lens`, and `%ExposureTime`." |
| "Perf: Reduce memory allocation in PT Run" | "Improved PowerToys Run startup performance and reduced memory usage." |
| "Update Newtonsoft.Json to 13.0.3" | (Development) "Updated Newtonsoft.Json dependency." OR skip if no user impact |
| "Add unit tests for color picker" | SKIP - not user-facing |
| "Fix typo in settings UI" | "Fixed a typo in the Settings interface." (only if visible to users) |

## Context-Aware Description

Sometimes you need to read the PR body or changed files to understand the impact:

```powershell
# If PR title is unclear, check the body for context
$prBody = gh pr view 12345 --repo microsoft/PowerToys --json body --jq '.body'

# Look for common patterns in PR descriptions:
# - "## Summary" or "## Description"
# - "This PR fixes/adds/improves..."
# - "Before/After" comparisons
# - Screenshots (indicate UI changes)

# Check what files changed to understand scope
$files = gh pr view 12345 --repo microsoft/PowerToys --json files --jq '.files[].path'
# If mostly .xaml files → UI change
# If mostly .cs/.cpp in one module → module-specific change
# If in src/common/ → potentially affects multiple modules
```

## Handling Ambiguous PRs

If a PR's impact is unclear:
1. **Check the linked issue** - often has user-reported symptoms
2. **Look at file changes** - understand what was modified
3. **Check PR comments** - may have discussion about user impact
4. **When in doubt** - put in Development section or ask for clarification
