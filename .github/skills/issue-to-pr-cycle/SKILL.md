---
name: issue-to-pr-cycle
description: End-to-end orchestration from issue analysis to PR creation and review. This skill is the ORCHESTRATION BRAIN that invokes other skills via CLI and performs VS Code MCP operations directly.
license: Complete terms in LICENSE.txt
---

# Issue-to-PR Full Cycle Skill

**ORCHESTRATION BRAIN** - coordinates other skills and performs VS Code MCP operations.

## Skill Contents

```
.github/skills/issue-to-pr-cycle/
├── SKILL.md              # This file (orchestration brain)
├── LICENSE.txt           # MIT License
└── scripts/
    ├── Get-CycleStatus.ps1     # Check status of issues/PRs
    ├── IssueReviewLib.ps1      # Shared helpers
    └── Start-FullIssueCycle.ps1 # Legacy script (phases A-C)
```

**Orchestrates these skills:**
| Skill | Purpose |
|-------|---------|
| `issue-review` | Analyze issues, generate implementation plans |
| `issue-fix` | Create worktrees, apply fixes, create PRs |
| `pr-review` | Comprehensive PR review (13 steps) |
| `pr-fix` | Fix review comments, resolve threads |

## Prerequisites

- GitHub CLI (`gh`) installed and authenticated
- Copilot CLI or Claude CLI installed
- PowerShell 7+
- VS Code with MCP tools (for write operations)

## Required Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `{{IssueNumbers}}` | Issue numbers to process | `45363, 45364` |
| (or) `{{PRNumbers}}` | PR numbers for review/fix loop | `45365, 45366` |

## How This Skill Works

The orchestrator:
1. **Invokes skills via CLI** - kicks off `copilot` CLI (not `gh copilot`) to run each skill
2. **Runs in parallel** - use PowerShell 7 `ForEach-Object -Parallel` in SINGLE terminal
3. **Waits for signals** - polls for `.signal` files indicating completion
4. **Performs VS Code MCP directly** - for operations that require write access (request reviewer, resolve threads)

## Quality Gates (CRITICAL)

**Every PR must pass these quality checks before creation:**

1. **Real Implementation** - NO placeholder/stub code
   - Files must contain actual working code
   - Empty classes like `class FixXXX { }` are FORBIDDEN
   
2. **Proper PR Title** - Follow Conventional Commits
   - Use `.github/prompts/create-commit-title.prompt.md`
   - Format: `feat(module): description` or `fix(module): description`
   - NEVER use generic titles like "fix: address issue #12345"

3. **Full PR Description** - Based on actual diff
   - Use `.github/prompts/create-pr-summary.prompt.md`
   - Run `git diff main...HEAD` to analyze changes
   - Fill PR template with real information

4. **Build Verification** - Code must compile
   - Run `tools/build/build.cmd` in worktree
   - Exit code 0 = success

### Checking Worktree Quality

```powershell
# Check if worktree has real implementation (not stubs)
$files = git diff main --name-only
foreach ($file in $files) {
    if ($file -match "src/common/fixes/Fix\d+\.cs") {
        Write-Error "STUB FILE DETECTED: $file - Need real implementation"
    }
}
```

## Signal Files

Each skill produces a `.signal` file when complete:

| Skill | Signal Location | Status Values |
|-------|-----------------|---------------|
| `issue-review` | `Generated Files/issueReview/<issue>/.signal` | `success`, `failure` |
| `issue-fix` | `Generated Files/issueFix/<issue>/.signal` | `success`, `failure` |
| `pr-review` | `Generated Files/prReview/<pr>/.signal` | `success`, `failure` |
| `pr-fix` | `Generated Files/prFix/<pr>/.signal` | `success`, `partial`, `failure` |

Signal format:
```json
{
  "status": "success",
  "issueNumber": 45363,
  "timestamp": "2026-02-04T10:05:23Z"
}
```

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  ORCHESTRATOR (this skill, VS Code agent)                                   │
│                                                                             │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐        │
│  │ issue-review│  │ issue-fix   │  │ pr-review   │  │ pr-fix      │        │
│  │ (CLI)       │  │ (CLI)       │  │ (CLI)       │  │ (CLI)       │        │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘        │
│         │                │                │                │                │
│         ▼                ▼                ▼                ▼                │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  Signal Files (Generated Files/*/.signal)                           │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                             │
│  VS Code MCP Operations (orchestrator executes directly):                   │
│  - mcp_github_request_copilot_review                                        │
│  - gh api graphql (resolve threads)                                         │
│  - Post review comments                                                     │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Workflow

### Phase A: Issue Review

Use the orchestration script instead of inline commands:

```powershell
.github/skills/issue-to-pr-cycle/scripts/Start-FullIssueCycle.ps1 -IssueNumbers 45363,45364
```

### Phase B: Issue Fix

Use the parallel runner script:

```powershell
.github/skills/issue-fix/scripts/Start-IssueFixParallel.ps1 -IssueNumbers 45363,45364 -CLIType copilot -ThrottleLimit 5 -Force
```

### Phase C: PR Review

Use the pr-review script for each PR, or run the full cycle script to orchestrate:

```powershell
.github/skills/pr-review/scripts/Start-PRReviewWorkflow.ps1 -PRNumber 45392
```

### Phase D: Review/Fix Loop (VS Code Agent Orchestrated)

This phase requires the VS Code agent to:

**D1: Request Copilot review (VS Code MCP)**
```
mcp_github_request_copilot_review:
  owner: microsoft
  repo: PowerToys
  pullNumber: {{PRNumber}}
```

**D2: Invoke pr-review skill (CLI, parallel)**
```powershell
gh copilot -p "Run skill pr-review for PR #{{PRNumber}}"
# Wait for: Generated Files/prReview/{{PRNumber}}/.signal
```

**D3: Check results**
- Read `Generated Files/prReview/{{PRNumber}}/00-OVERVIEW.md`
- Query unresolved threads via GraphQL

**D4: Post comments (VS Code MCP) - if medium+ severity**

**D5: Invoke pr-fix skill in WORKTREE (CLI)**
```powershell
# Find worktree for this PR's branch
$branch = (gh pr view {{PRNumber}} --json headRefName -q .headRefName)
$worktree = git worktree list --porcelain | Select-String "worktree.*$branch" | ...

# Run fix in worktree
cd $worktreePath
gh copilot -p "Run skill pr-fix for PR #{{PRNumber}}"
# Wait for: Generated Files/prFix/{{PRNumber}}/.signal
```

**D6: Resolve threads (VS Code MCP)**
```powershell
# Get thread IDs
gh api graphql -f query='query { repository(owner:"microsoft",name:"PowerToys") { 
  pullRequest(number:{{PRNumber}}) { reviewThreads(first:50) { nodes { id isResolved } } } 
} }'

# Resolve each (VS Code agent executes this)
gh api graphql -f query='mutation { resolveReviewThread(input:{threadId:"{{ID}}"}) { thread { isResolved } } }'
```

**D7: Loop**
- If unresolved issues remain → go to D2
- If all clear → done

## Timeout Handling

Default timeout: 10 minutes per skill invocation.

If no signal file appears within timeout:
1. Check if the skill process is still running
2. If hung, terminate and mark as `timeout`
3. Log failure and continue with other items

## Parallel Execution (CRITICAL)

**DO NOT spawn separate terminals for each operation.** Use the dedicated scripts to run parallel work from a single terminal:

```powershell
# Issue fixes in parallel
.github/skills/issue-fix/scripts/Start-IssueFixParallel.ps1 -IssueNumbers 28726,13336,27507,3054,37800 -CLIType copilot -Model gpt-5.2-codex -ThrottleLimit 5 -Force

# PR fixes in parallel
.github/skills/pr-fix/scripts/Start-PRFixParallel.ps1 -PRNumbers 45256,45257,45285,45286 -CLIType copilot -Model gpt-5.2-codex -ThrottleLimit 3 -Force
```

## Worktree Mapping

The orchestrator must track which worktree belongs to which issue/PR:

```powershell
# Get all worktrees
$worktrees = git worktree list --porcelain | Select-String "worktree|branch" | 
    ForEach-Object { $_.Line }

# Parse into mapping
# Q:\PowerToys-ab12 → issue/44044
# Q:\PowerToys-cd34 → issue/32950

# Find worktree for issue
$issueNum = 45363
$worktreeLine = git worktree list | Select-String "issue/$issueNum"
$worktreePath = ($worktreeLine -split '\s+')[0]
```

## When to Use This Skill

- Process multiple issues end-to-end
- Automate the full issue → PR → review → fix cycle
- Batch process high-confidence issues
- Run continuous review/fix loops until clean
