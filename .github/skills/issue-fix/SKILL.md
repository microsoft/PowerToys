---
name: issue-fix
description: Automatically fix GitHub issues and create PRs. Use when asked to fix an issue, implement a feature from an issue, auto-fix an issue, apply implementation plan, create code changes for an issue, resolve a GitHub issue, or submit a PR for an issue. Creates isolated git worktree, applies AI-generated fixes, commits changes, and creates pull requests.
license: Complete terms in LICENSE.txt
---

# Issue Fix Skill

Automatically fix GitHub issues by creating isolated worktrees, applying AI-generated code changes, and creating pull requests - the complete issue-to-PR workflow.

## Skill Contents

This skill is **self-contained** with all required resources:

```
.github/skills/issue-fix/
├── SKILL.md              # This file
├── LICENSE.txt           # MIT License
├── scripts/
│   ├── Start-IssueAutoFix.ps1  # Main fix script (creates worktree, applies fix)
│   ├── Start-IssueFixParallel.ps1 # Parallel runner (single terminal)
│   ├── Get-WorktreeStatus.ps1  # Worktree status helper
│   ├── Submit-IssueFix.ps1     # Commit and create PR
│   └── IssueReviewLib.ps1      # Shared helpers
└── references/
    ├── fix-issue.prompt.md         # AI prompt for fixing
    ├── create-commit-title.prompt.md  # AI prompt for commit messages
    ├── create-pr-summary.prompt.md    # AI prompt for PR descriptions
    └── mcp-config.json             # MCP configuration
```

## Output

- **Worktrees**: Created at drive root level `Q:/PowerToys-xxxx/`
- **PRs**: Created on GitHub linking to the original issue
- **Signal file**: `Generated Files/issueFix/<issue>/.signal`

## Signal File

On completion, a `.signal` file is created for orchestrator coordination:

```json
{
  "status": "success",
  "issueNumber": 45363,
  "timestamp": "2026-02-04T10:05:23Z",
  "worktreePath": "Q:/PowerToys-ab12"
}
```

Status values: `success`, `failure`

## When to Use This Skill

- Fix a specific GitHub issue automatically
- Implement a feature described in an issue
- Apply an existing implementation plan
- Create code changes and submit PR for an issue
- Auto-fix high-confidence issues end-to-end

## Prerequisites

- GitHub CLI (`gh`) installed and authenticated
- Issue must be reviewed first (use `issue-review` skill)
- PowerShell 7+ for running scripts
- Copilot CLI or Claude CLI installed

## Required Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `{{IssueNumber}}` | GitHub issue number to fix | `44044` |

## Workflow

### Step 1: Ensure Issue is Reviewed

If not already reviewed, use the `issue-review` skill first.

### Step 2: Run Auto-Fix

```powershell
# Create worktree and apply fix
.github/skills/issue-fix/scripts/Start-IssueAutoFix.ps1 -IssueNumber {{IssueNumber}} -CLIType copilot -Force
```

This will:
1. Create a new git worktree with branch `issue/{{IssueNumber}}`
2. Copy the review files to the worktree
3. Launch Copilot CLI to implement the fix
4. Build and verify the changes

### Step 3: Submit PR

```powershell
# Commit changes and create PR
.github/skills/issue-fix/scripts/Submit-IssueFix.ps1 -IssueNumber {{IssueNumber}} -CLIType copilot -Force
```

This will:
1. Generate AI commit message
2. Commit all changes
3. Push to origin
4. Create PR with AI-generated description
5. Link PR to issue with "Fixes #{{IssueNumber}}"

### One-Step Alternative

To fix AND submit in one command:

```powershell
.github/skills/issue-fix/scripts/Start-IssueAutoFix.ps1 -IssueNumber {{IssueNumber}} -CLIType copilot -CreatePR -Force
```

## CLI Options

### Start-IssueAutoFix.ps1

| Parameter | Description | Default |
|-----------|-------------|---------|
| `-IssueNumber` | Issue to fix | Required |
| `-CLIType` | AI CLI: `copilot` or `claude` | `copilot` |
| `-CreatePR` | Auto-create PR after fix | `false` |
| `-SkipWorktree` | Fix in current repo (no worktree) | `false` |
| `-Force` | Skip confirmation prompts | `false` |

### Submit-IssueFix.ps1

| Parameter | Description | Default |
|-----------|-------------|---------|
| `-IssueNumber` | Issue to submit | Required |
| `-CLIType` | AI CLI: `copilot`, `claude`, `manual` | `copilot` |
| `-Draft` | Create as draft PR | `false` |
| `-SkipCommit` | Skip commit (changes already committed) | `false` |
| `-Force` | Skip confirmation prompts | `false` |

## Batch Processing

Fix multiple issues:

```powershell
# Fix multiple issues (creates worktrees, applies fixes)
.github/skills/issue-fix/scripts/Start-IssueAutoFix.ps1 -IssueNumbers 44044, 32950 -CLIType copilot -Force

# Submit all fixed issues as PRs
.github/skills/issue-fix/scripts/Submit-IssueFix.ps1 -CLIType copilot -Force
```

## Parallel Execution (IMPORTANT)

**DO NOT** spawn separate terminals for each issue. Use the dedicated scripts:

```powershell
# Run fixes in parallel (single terminal)
.github/skills/issue-fix/scripts/Start-IssueFixParallel.ps1 -IssueNumbers 28726,13336,27507,3054,37800 -CLIType copilot -ThrottleLimit 5 -Force

# Check worktree status
.github/skills/issue-fix/scripts/Get-WorktreeStatus.ps1
```

This allows:
- Tracking all jobs in one place
- Waiting for completion with proper synchronization
- Controlling parallelism with `-ThrottleLimit`
- Combined output visibility

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Worktree already exists | Use existing worktree or `git worktree remove <path>` |
| No implementation plan | Use `issue-review` skill first |
| Build failures | Check build logs, may need manual intervention |
| PR already exists | Script will skip, check existing PR |
| CLI not found | Install Copilot CLI |

## PR Creation Requirements (CRITICAL)

**NEVER create PRs with placeholder/stub code.** Every PR must have:

1. **Real implementation** - Actual working code that addresses the issue
2. **Proper title** - Follow `create-commit-title.prompt.md` (Conventional Commits)
3. **Full description** - Follow `create-pr-summary.prompt.md` based on actual diff

### PR Title Format (Conventional Commits)
```
feat(module): add feature description
fix(module): fix bug description
docs(module): update documentation
```

### PR Description Must Include
- Summary of changes (from actual diff)
- `Fixes #IssueNumber` link
- Checklist items marked appropriately
- Validation steps performed

**Example of BAD PR (never do this):**
```
Title: fix: address issue #12345
Body: Fixes #12345
Code: class Fix12345 { public void Apply() { } }  // EMPTY STUB!
```

**Example of GOOD PR:**
```
Title: feat(peek): add symbolic link resolution for PDF/HTML files
Body: ## Summary
Adds SymlinkResolver helper to resolve symlinks...
[Full description based on create-pr-summary.prompt.md]
```

## Related Skills

| Skill | Purpose |
|-------|---------|
| `issue-review` | Review issues, generate implementation plans |
| `pr-review` | Review the created PR |
| `pr-fix` | Fix PR review comments |
