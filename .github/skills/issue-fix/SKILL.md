---
name: issue-fix
description: Automatically fix GitHub issues using AI-assisted code generation. Use when asked to fix an issue, implement a feature from an issue, auto-fix an issue, apply implementation plan, create code changes for an issue, or resolve a GitHub issue. Creates isolated git worktree and applies AI-generated fixes based on the implementation plan.
license: Complete terms in LICENSE.txt
---

# Issue Fix Skill

Automatically fix GitHub issues by creating isolated worktrees and applying AI-generated code changes based on implementation plans.

## Skill Contents

This skill is **self-contained** with all required resources:

```
.github/skills/issue-fix/
├── SKILL.md              # This file
├── LICENSE.txt           # MIT License
├── scripts/
│   └── Start-IssueAutoFix.ps1  # Main fix script
└── references/
    └── fix-issue.prompt.md     # Full AI prompt template
```

## Output Directory

Worktrees are created at the drive root level:

```
Q:/PowerToys-xxxx/           # Worktree for issue (xxxx = short hash)
├── Generated Files/
│   └── issueReview/
│       └── <issue-number>/  # Copied from main repo
│           ├── overview.md
│           └── implementation-plan.md
└── <normal repo structure>
```

## When to Use This Skill

- Fix a specific GitHub issue automatically
- Implement a feature described in an issue
- Apply an existing implementation plan
- Create code changes for an issue
- Auto-fix high-confidence issues
- Resolve issues that have been reviewed

## Prerequisites

- GitHub CLI (`gh`) installed and authenticated
- Issue must be reviewed first (use `issue-review` skill)
- PowerShell 7+ for running scripts
- Copilot CLI or Claude CLI installed

## Required Variables

⚠️ **Before starting**, confirm `{{IssueNumber}}` with the user. If not provided, **ASK**: "What issue number should I fix?"

| Variable | Description | Example |
|----------|-------------|---------|
| `{{IssueNumber}}` | GitHub issue number to fix | `44044` |

## Workflow

### Step 1: Ensure Issue is Reviewed

If not already reviewed, use the `issue-review` skill first.

### Step 2: Run Auto-Fix

Execute the fix script (use paths relative to this skill folder):

```powershell
# From repo root
.github/skills/issue-fix/scripts/Start-IssueAutoFix.ps1 -IssueNumber {{IssueNumber}} -CLIType copilot
```

This will:
1. Create a new git worktree with branch `issue/{{IssueNumber}}`
2. Copy the review files to the worktree
3. Launch Copilot CLI to implement the fix
4. Build and verify the changes

### Step 3: Verify Changes

Navigate to the worktree and review:

```powershell
# List worktrees
git worktree list

# Check changes in the worktree
cd Q:/PowerToys-xxxx
git diff
git status
```

## CLI Options

| Parameter | Description | Default |
|-----------|-------------|---------|
| `-IssueNumber` | Issue to fix | Required |
| `-CLIType` | AI CLI to use: `copilot` or `claude` | `copilot` |
| `-Force` | Skip confirmation prompts | `false` |

## Batch Fix

To fix multiple issues:

```powershell
.github/skills/issue-fix/scripts/Start-IssueAutoFix.ps1 -IssueNumbers 44044, 32950 -CLIType copilot -Force
```

## After Fixing

Once the fix is complete, use the `submit-pr` skill to create a PR.

## AI Prompt Reference

For manual AI invocation, the full prompt is at:
- `references/fix-issue.prompt.md` (relative to this skill folder)

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Worktree already exists | Use existing worktree or delete with `git worktree remove <path>` |
| No implementation plan | Use `issue-review` skill first |
| Build failures | Check build logs, may need manual intervention |
| CLI not found | Install Copilot CLI |
