---
name: changelog-generator
description: Automatically creates user-facing changelogs from git commits by analyzing commit history, categorizing changes, and transforming technical commits into clear, customer-friendly release notes. Turns hours of manual changelog writing into minutes of automated generation.
---

# Changelog Generator

This skill transforms technical git commits into polished, user-friendly changelogs that your customers and users will actually understand and appreciate.

## When to Use This Skill

- Preparing release notes for a new version
- Generating changelog between two tags/commits
- Creating draft release notes from recent commits

## Quick Start

```
Create a changelog from commits since v0.96.1
```

```
Create release notes for version 0.97.0 starting from tag v0.96.1
```

---

## Workflow Overview

```
┌──────────────┐    ┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│ 1. Fetch     │───▶│ 2. Filter    │───▶│ 3. Categorize│───▶│ 4. Generate  │
│ Commits      │    │ & Dedupe     │    │ by Module    │    │ Descriptions │
└──────────────┘    └──────────────┘    └──────────────┘    └──────────────┘
       │                   │                   │                   │
       ▼                   ▼                   ▼                   ▼
  📄 github-api      📄 commit-         📄 module-         📄 user-facing-
     reference.md       filtering.md       mapping.md         description.md
```

## Sub-Skills Reference

This skill is composed of the following sub-skills. 

**⚠️ IMPORTANT: Do NOT read all sub-skills at once!**
- Only read a sub-skill when you reach that step in the workflow
- This saves context window and improves accuracy
- Use `read_file` to load a sub-skill only when needed

| Step | Sub-Skill | When to Read |
|------|-----------|--------------|
| Fetch data | [github-api-reference.md](sub-skills/github-api-reference.md) | When fetching commits/PRs |
| Filter commits | [commit-filtering.md](sub-skills/commit-filtering.md) | When checking if commit should be skipped |
| Categorize | [module-mapping.md](sub-skills/module-mapping.md) | When determining which module a PR belongs to |
| Generate text | [user-facing-description.md](sub-skills/user-facing-description.md) | When writing the changelog entry text |
| Attribution | [contributor-attribution.md](sub-skills/contributor-attribution.md) | When checking if author needs thanks |
| Large releases | [progress-tracking.md](sub-skills/progress-tracking.md) | Only if processing 50+ commits |

---

## Step-by-Step Summary

### Step 1: Get Commit Range
```powershell
# Count commits between tags
gh api repos/microsoft/PowerToys/compare/v0.96.0...v0.96.1 --jq '.commits | length'
```
👉 Details: [github-api-reference.md](sub-skills/github-api-reference.md)

### Step 2: Filter Commits
- Skip commits already in start tag
- Skip cherry-picks and backports
- Deduplicate by PR number

👉 Details: [commit-filtering.md](sub-skills/commit-filtering.md)

### Step 3: For Each PR, Generate Entry
1. Get PR title, body, files, labels
2. Determine module from file paths or labels
3. Check if user-facing (skip internal changes)
4. Transform to user-friendly description
5. Add contributor attribution if needed

👉 Details: [user-facing-description.md](sub-skills/user-facing-description.md), [module-mapping.md](sub-skills/module-mapping.md), [contributor-attribution.md](sub-skills/contributor-attribution.md)

### Step 4: Checkpoint (if 50+ commits)
- Save progress after every 15-20 commits
- Track processed PRs for deduplication
- Enable resume from interruption

👉 Details: [progress-tracking.md](sub-skills/progress-tracking.md)

### Step 5: Format Output
```markdown
## ✨ What's new
**Version X.XX (Month Year)**

**✨ Highlights**
 - [Most impactful change 1]
 - [Most impactful change 2]

### [Module Name - Alphabetical]
 - [Description]. Thanks [@contributor](https://github.com/contributor)!

### Development
 - [Internal changes]
```

---

## Example Output

```markdown
## ✨ What's new
**Version 0.96.1 (December 2025)**

**✨ Highlights**
 - Advanced Paste now supports multiple AI providers.
 - PowerRename can extract photo metadata for renaming.

### Advanced Paste
 - Added support for Azure OpenAI, Google Gemini, Mistral, and more.

### Awake
 - The countdown timer now stays accurate over long periods. Thanks [@daverayment](https://github.com/daverayment)!

### Development
 - Resolved build warnings in Command Palette projects.
```

---

## Tips

1. **Propose highlights** after all entries are generated
2. **Check PR body** when title is unclear
3. **Thank external contributors** - see [contributor-attribution.md](sub-skills/contributor-attribution.md)
4. **Use progress tracking** for large releases - see [progress-tracking.md](sub-skills/progress-tracking.md)
5. **Save output** to `release-change-note-draft.md`

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Lost progress mid-generation | Read `release-notes-progress.md` to resume |
| Duplicate entries | Check processed PR list, dedupe by PR number |
| Commit already released | Use `git merge-base --is-ancestor` to verify |
| API rate limited | Check `gh api rate_limit`, wait or use token |

👉 See sub-skills for detailed troubleshooting.
