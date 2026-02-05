---
name: submit-pr
description: Commit changes and create pull requests for fixed issues. Use when asked to create a PR, submit changes, commit fixes, push changes for an issue, create pull request from worktree, or finalize issue fix. Generates AI-assisted commit messages and PR descriptions following PowerToys conventions.
license: Complete terms in LICENSE.txt
---

# Submit PR Skill

Commit changes from issue worktrees and create pull requests with AI-generated titles and descriptions following PowerToys conventions.

## Skill Contents

This skill is **self-contained** with all required resources:

```
.github/skills/submit-pr/
├── SKILL.md              # This file
├── LICENSE.txt           # MIT License
├── scripts/
│   └── Submit-IssueFixes.ps1    # Main submit script
└── references/
    ├── create-commit-title.prompt.md  # Commit title rules
    └── create-pr-summary.prompt.md    # PR description template
```

## Output

PRs are created on GitHub with:
- Conventional commit title (e.g., `fix(fancyzones): resolve editor crash on multi-monitor`)
- Description following `.github/pull_request_template.md`
- Auto-linked to the original issue via `Fixes #{{IssueNumber}}`

## When to Use This Skill

- Create a PR for a fixed issue
- Commit and push changes from a worktree
- Submit changes after using `issue-fix` skill
- Generate PR title and description
- Finalize an issue fix with a pull request

## Prerequisites

- GitHub CLI (`gh`) installed and authenticated
- Changes made in an issue worktree (from `issue-fix` skill)
- PowerShell 7+ for running scripts

## Required Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `{{IssueNumber}}` | Issue number(s) to submit | `44044` or `44044, 32950` |

## Workflow

### Step 1: Verify Changes Exist

Check that the worktree has uncommitted or unpushed changes:

```powershell
# List issue worktrees
git worktree list | Select-String "issue/"

# Check status in a worktree
cd Q:/PowerToys-xxxx
git status
```

### Step 2: Submit PR

Execute the submit script (use paths relative to this skill folder):

```powershell
# From repo root
.github/skills/submit-pr/scripts/Submit-IssueFixes.ps1 -IssueNumbers {{IssueNumber}} -CLIType copilot
```

This will:
1. Generate a commit title using AI (following conventional commits)
2. Stage and commit all changes
3. Push the branch to origin
4. Generate a PR description using AI
5. Create the PR on GitHub

### Step 3: Review Created PR

The script outputs the PR URL. Review it on GitHub.

## CLI Options

| Parameter | Description | Default |
|-----------|-------------|---------|
| `-IssueNumbers` | Issue number(s) to submit | All worktrees |
| `-CLIType` | AI CLI to use: `copilot`, `claude`, or `manual` | `copilot` |
| `-TargetBranch` | Base branch for PR | `main` |
| `-Draft` | Create as draft PR | `false` |
| `-Force` | Skip confirmation prompts | `false` |
| `-DryRun` | Show what would be done | `false` |

## PR Title Format

Titles follow conventional commits (see `references/create-commit-title.prompt.md`):

```
<type>(<scope>): <description>
```

| Type | When to use |
|------|-------------|
| `fix` | Bug fixes |
| `feat` | New features |
| `docs` | Documentation only |
| `refactor` | Code restructuring |

## AI Prompt References

For manual AI invocation, prompts are at:
- `references/create-commit-title.prompt.md` - Commit title generation
- `references/create-pr-summary.prompt.md` - PR description generation

## Troubleshooting

| Problem | Solution |
|---------|----------|
| No changes to commit | Verify fix was applied, check `git status` |
| PR already exists | Script will skip and report existing PR URL |
| Push rejected | Pull latest changes or force push with `--force-with-lease` |
