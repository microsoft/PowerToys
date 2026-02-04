---
name: pr-fix
description: Fix active PR review comments and resolve threads. Use when asked to fix PR comments, address review feedback, resolve review threads, implement PR fixes, or handle review iterations. Works with VS Code MCP tools to resolve GitHub threads after fixes are applied.
license: Complete terms in LICENSE.txt
---

# PR Fix Skill

Fix active pull request review comments and resolve threads. This skill handles the **fix** part of the PR review cycle, separate from the review itself.

## ⚠️ Critical Architecture

This skill requires **both** CLI scripts AND VS Code MCP tools:

| Operation | Execution Method |
|-----------|------------------|
| Apply code fixes | Copilot/Claude CLI via script |
| Resolve review threads | **VS Code Agent** via `gh api graphql` |
| Check status | Script (read-only) |

**WHY**: Copilot CLI's MCP is **read-only**. Only VS Code can resolve threads.

## Skill Contents

```
.github/skills/pr-fix/
├── SKILL.md                    # This file
├── LICENSE.txt                 # MIT License
├── references/
│   ├── fix-pr-comments.prompt.md   # AI prompt for fixing comments
│   └── mcp-config.json             # MCP configuration
└── scripts/
    ├── Start-PRFix.ps1             # Main fix script
    ├── Get-UnresolvedThreads.ps1   # Get threads needing resolution
    └── IssueReviewLib.ps1          # Shared helpers
```

## When to Use This Skill

- Fix active review comments on a PR
- Address reviewer feedback
- Resolve review threads after fixing
- Run the fix portion of review/fix loop
- Implement changes requested in PR reviews

## Prerequisites

- GitHub CLI (`gh`) installed and authenticated
- Copilot CLI or Claude CLI installed
- PowerShell 7+
- PR has active review comments to fix

## Required Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `{{PRNumber}}` | Pull request number to fix | `45286` |

## Workflow

### Step 1: Check Unresolved Threads

```powershell
# See what needs to be fixed
.github/skills/pr-fix/scripts/Get-UnresolvedThreads.ps1 -PRNumber {{PRNumber}}
```

### Step 2: Run Fix (CLI Script)

```powershell
# Apply AI-generated fixes to address comments
.github/skills/pr-fix/scripts/Start-PRFix.ps1 -PRNumber {{PRNumber}} -CLIType copilot -Force
```

### Step 3: Resolve Threads (VS Code Agent)

After fixes are pushed, **you (the VS Code agent) must resolve threads**:

```powershell
# Get unresolved thread IDs
gh api graphql -f query='
  query {
    repository(owner: "microsoft", name: "PowerToys") {
      pullRequest(number: {{PRNumber}}) {
        reviewThreads(first: 50) {
          nodes { id isResolved path line }
        }
      }
    }
  }
' --jq '.data.repository.pullRequest.reviewThreads.nodes[] | select(.isResolved == false)'
```

```powershell
# Resolve each thread
gh api graphql -f query='
  mutation {
    resolveReviewThread(input: {threadId: "{{threadId}}"}) {
      thread { isResolved }
    }
  }
'
```

### Step 4: Verify All Resolved

```powershell
# Confirm no unresolved threads remain
.github/skills/pr-fix/scripts/Get-UnresolvedThreads.ps1 -PRNumber {{PRNumber}}
```

## CLI Options

| Parameter | Description | Default |
|-----------|-------------|---------|
| `-PRNumber` | PR number to fix | Required |
| `-CLIType` | AI CLI: `copilot` or `claude` | `copilot` |
| `-Force` | Skip confirmation prompts | `false` |
| `-DryRun` | Show what would be done | `false` |

## Review/Fix Loop Integration

This skill is typically used with `pr-review` in a loop:

```
┌─────────────────┐
│  pr-review      │  ← Generate review, post comments
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  pr-fix         │  ← Fix comments, resolve threads
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Check status   │  ← Any threads unresolved?
└────────┬────────┘
         │
    ┌────┴────┐
    │ YES     │ NO
    ▼         ▼
 (loop)    ✓ Done
```

## VS Code Agent Operations

These operations **must** be done by the VS Code agent (not scripts):

| Operation | Method |
|-----------|--------|
| Resolve thread | `gh api graphql` with `resolveReviewThread` mutation |
| Unresolve thread | `gh api graphql` with `unresolveReviewThread` mutation |

### Batch Resolve All Threads

```powershell
# Get all unresolved thread IDs and resolve them
$threads = gh api graphql -f query='
  query {
    repository(owner: "microsoft", name: "PowerToys") {
      pullRequest(number: {{PRNumber}}) {
        reviewThreads(first: 100) {
          nodes { id isResolved }
        }
      }
    }
  }
' --jq '.data.repository.pullRequest.reviewThreads.nodes[] | select(.isResolved == false) | .id'

foreach ($threadId in $threads) {
    gh api graphql -f query="mutation { resolveReviewThread(input: {threadId: `"$threadId`"}) { thread { isResolved } } }"
}
```

## Troubleshooting

| Problem | Solution |
|---------|----------|
| "Cannot resolve thread" | Use VS Code agent, not Copilot CLI |
| Fix not applied | Check worktree is on correct branch |
| Thread ID not found | Re-fetch threads, ID may have changed |
| Fix pushed but thread unresolved | Must explicitly resolve via GraphQL |

## Related Skills

| Skill | Purpose |
|-------|---------|
| `pr-review` | Review PR, generate findings, post comments |
| `issue-fix` | Fix issues and create PRs |
| `issue-to-pr-cycle` | Full orchestration |
