---
name: issue-to-pr-cycle
description: End-to-end orchestration from issue analysis to PR creation and review. Use when asked to fix multiple issues automatically, run full issue cycle, batch process issues, automate issue resolution, create PRs for high-confidence issues, or process issues end-to-end. This skill is the ORCHESTRATION BRAIN that coordinates other skills and VS Code MCP tools.
license: Complete terms in LICENSE.txt
---

# Issue-to-PR Full Cycle Skill

**ORCHESTRATION BRAIN** for the complete workflow from issue analysis to PR creation and review.

## ⚠️ Critical Architecture

### Main Repo vs Worktrees

| Location | Purpose | Branch |
|----------|---------|--------|
| **Main repo** (`Q:\PowerToys`) | Orchestration brain, stays clean | `main` |
| **Worktrees** (`Q:\PowerToys-<hash>\`) | Isolated issue work | `issue/<number>` |

**RULE**: The main repo (`Q:\PowerToys`) should ALWAYS stay on `main` branch. ALL issue work happens in worktrees.

### Worktree Path Pattern

```
Q:\PowerToys-<hash>\
  where <hash> = first 4 hex chars of MD5(branch-name)
  
Example: issue/44044 → Q:\PowerToys-ab12\
```

### How to Find Worktrees

```powershell
# From main repo
. tools/build/WorktreeLib.ps1
Get-WorktreeEntries  # Returns { Path, Branch } for all worktrees

# Or directly
git worktree list --porcelain
```

### Write Operations Require VS Code MCP

This skill **orchestrates** other skills and **directly uses VS Code MCP tools** for write operations:

| Operation Type | Execution Method |
|----------------|------------------|
| **Parallel CLI operations** | PowerShell scripts kick off Copilot/Claude CLI |
| **Status checking** | PowerShell scripts (read-only) |
| **GitHub write operations** | VS Code MCP tools (assign reviewer, post comments, resolve comments) |

**WHY**: Copilot CLI's MCP is **read-only**. Only VS Code's MCP tools can perform write operations like:
- Assigning Copilot as PR reviewer
- Posting review comments
- Resolving review comments

## Skill Contents

```
.github/skills/issue-to-pr-cycle/
├── SKILL.md                    # This file (orchestration brain)
├── LICENSE.txt                 # MIT License
└── scripts/
    ├── Get-CycleStatus.ps1     # Check status of issues/PRs (read-only)
    ├── IssueReviewLib.ps1      # Shared helpers
    └── Start-FullIssueCycle.ps1 # Legacy script (phases A-C only)
```

**Orchestrates these skills** (each produces specific outputs):

| Skill | Input | Output Location | Creates |
|-------|-------|-----------------|---------|
| `issue-review` | Issue # | `Generated Files/issueReview/<issue>/` | `overview.md`, `implementation-plan.md` |
| `issue-fix` | Issue # | Worktree at `Q:\PowerToys-<hash>\` | Code changes, commits, PRs |
| `pr-review` | PR # | `Generated Files/prReview/<pr>/` | `00-OVERVIEW.md`, `01-*.md`...`13-*.md` |
| `pr-fix` | PR # | Changes in worktree | Code fixes, resolved threads |

## Workflow Overview

```
┌─────────────────────────────────────────────────────────────────────────┐
│  PHASE A: Review Issues (issue-review skill via CLI)                    │
│  Location: Main repo (Q:\PowerToys)                                     │
│  Skill: issue-review                                                    │
│  Script: issue-review/scripts/Start-BulkIssueReview.ps1                │
│  Output: Generated Files/issueReview/<issue>/overview.md               │
└─────────────────────────────────────────────────────────────────────────┘
                                    ↓
┌─────────────────────────────────────────────────────────────────────────┐
│  CHECK: Get-CycleStatus.ps1 -CheckAll                                  │
│  Decision: Which issues are high-confidence?                            │
└─────────────────────────────────────────────────────────────────────────┘
                                    ↓
┌─────────────────────────────────────────────────────────────────────────┐
│  PHASE B: Fix Issues & Create PRs (issue-fix skill via CLI)            │
│  Location: WORKTREES created at Q:\PowerToys-<hash>\                   │
│  Skill: issue-fix                                                       │
│  Script: issue-fix/scripts/Start-IssueAutoFix.ps1 -CreatePR            │
│  Process:                                                               │
│    1. Creates worktree: Q:\PowerToys-<hash>\ with branch issue/<num>   │
│    2. Copies Generated Files to worktree                                │
│    3. Applies AI fix in worktree                                        │
│    4. Commits & pushes from worktree                                    │
│    5. Creates PR on GitHub                                              │
│  Output: Worktree with code changes + Open PR                           │
└─────────────────────────────────────────────────────────────────────────┘
                                    ↓
┌─────────────────────────────────────────────────────────────────────────┐
│  PHASE C: Initial PR Review (pr-review skill via CLI)                  │
│  Location: Can run from main repo (reads PR diff from GitHub)          │
│  Skill: pr-review                                                       │
│  Script: pr-review/scripts/Start-PRReviewWorkflow.ps1 -SkipFix         │
│  Output: Generated Files/prReview/<pr>/ in MAIN REPO                   │
└─────────────────────────────────────────────────────────────────────────┘
                                    ↓
┌─────────────────────────────────────────────────────────────────────────┐
│  PHASE D: Review/Fix Loop (VS Code Agent Orchestrated)                 │
│  Location: Fixes applied in WORKTREES, reviews in MAIN REPO            │
│                                                                         │
│  PARALLEL: {                                                            │
│    D1. Request Copilot review [VS Code MCP - async]                    │
│    D2. Invoke pr-review skill [CLI - generates review files]           │
│  }                                                                      │
│  SEQUENTIAL: {                                                          │
│    D3. Check review results [Read files + GraphQL]                     │
│    D4. Post review comments [VS Code MCP]                              │
│    D5. Invoke pr-fix skill IN WORKTREE [CLI - applies fixes]           │
│    D6. Resolve comments [VS Code MCP / GraphQL]                        │
│    D7. Re-check → Loop to D2 if issues remain                          │
│  }                                                                      │
└─────────────────────────────────────────────────────────────────────────┘
```

## Key Knowledge: Mapping Issues to Worktrees

The orchestration brain MUST know where each issue's worktree is:

```powershell
# Load worktree library
. Q:\PowerToys\tools\build\WorktreeLib.ps1

# Get all worktrees
$worktrees = Get-WorktreeEntries
# Returns: @(
#   @{ Path = "Q:\PowerToys-ab12"; Branch = "issue/44044" },
#   @{ Path = "Q:\PowerToys-cd34"; Branch = "issue/32950" },
#   ...
# )

# Find worktree for specific issue
$issueNum = 44044
$wt = $worktrees | Where-Object { $_.Branch -like "issue/$issueNum*" }
$worktreePath = $wt.Path  # e.g., "Q:\PowerToys-ab12"

# Find PR for worktree branch
$prInfo = gh pr list --head $wt.Branch --json number,url,state | ConvertFrom-Json
$prNumber = $prInfo[0].number
```

## Quick Start: Check Current Status

Always start by checking status:

```powershell
# Check all issues and PRs
.github/skills/issue-to-pr-cycle/scripts/Get-CycleStatus.ps1 -CheckAll

# Check specific PRs
.github/skills/issue-to-pr-cycle/scripts/Get-CycleStatus.ps1 -PRNumbers 45286, 45285
```

## Phase D: Review/Fix Loop (DETAILED)

This phase orchestrates **skills** (not just scripts) and requires the **VS Code agent** for write operations.

### Parallel Execution Model

```
┌─────────────────────────────────────────────────────────────────┐
│  D1 + D2 run IN PARALLEL (both are async review operations)    │
│                                                                 │
│  D1: Copilot Review [VS Code MCP] ──┐                          │
│                                      ├──→ Both produce reviews │
│  D2: Agent Review [pr-review skill]─┘                          │
└─────────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────────┐
│  D3: Check results from BOTH reviews                           │
│  D4: Post comments [VS Code MCP]                               │
└─────────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────────┐
│  D5: Fix comments [pr-fix skill]                               │
│  D6: Resolve threads [VS Code MCP / GraphQL]                   │
│  D7: Loop back to D2 if issues remain                          │
└─────────────────────────────────────────────────────────────────┘
```

### Step D1: Assign Copilot as Reviewer (Async)

**Use VS Code MCP tool** - this runs asynchronously in the background:

```
mcp_github_request_copilot_review:
  owner: "microsoft"
  repo: "PowerToys"  
  pullNumber: {{PRNumber}}
```

**Don't wait** - proceed to D2 immediately.

### Step D2: Invoke pr-review Skill (Parallel with D1)

**Invoke the `pr-review` skill** to perform comprehensive review. The skill:
1. Loads `pr-review/references/review-pr.prompt.md` for orchestration
2. Executes 13 review steps (functionality, security, accessibility, etc.)
3. Generates review files in `Generated Files/prReview/{{PRNumber}}/`
4. May also post comments directly via GitHub MCP

**⚠️ Important**: The CLI prompt must be explicit about file creation. Use this format:

```powershell
$prompt = @"
Review PR #{{PRNumber}} following .github/skills/pr-review/references/review-pr.prompt.md

CRITICAL: You MUST create files in 'Generated Files/prReview/{{PRNumber}}/' folder.
- Create 01-functionality.md after step 1
- Create 02-compatibility.md after step 2  
- Continue for all applicable steps
- Create 00-OVERVIEW.md at the end

Do NOT skip file creation. The review is incomplete without the files.
"@
copilot -p $prompt --yolo
```

Or use the workflow script (which includes explicit prompting):
```powershell
.github/skills/pr-review/scripts/Start-PRReviewWorkflow.ps1 `
    -PRNumbers {{PRNumber}} `
    -CLIType copilot `
    -SkipAssign `
    -SkipFix `
    -Force
```

**Expected output files:**
```
Generated Files/prReview/{{PRNumber}}/
├── 00-OVERVIEW.md           # Summary with all findings
├── 01-functionality.md      # Functional correctness
├── 02-compatibility.md      # Breaking changes
├── 03-performance.md        # Performance
├── 05-security.md           # Security
├── 09-solid-design.md       # SOLID principles
├── 10-repo-patterns.md      # PowerToys conventions
├── 11-docs-automation.md    # Documentation
└── 12-code-comments.md      # Code comments
```

**Skill reference**: See `.github/skills/pr-review/SKILL.md` for full details.

### Step D3: Check Review Results (BOTH sources)

After D1 and D2 complete, check results from **multiple sources**:

**Source 1: Local review files (REQUIRED)**
```powershell
# Verify review files were created
$reviewPath = "Generated Files/prReview/{{PRNumber}}"
if (Test-Path "$reviewPath/00-OVERVIEW.md") {
    Get-Content "$reviewPath/00-OVERVIEW.md"
    # Parse severity counts from the overview
} else {
    Write-Error "Review files not created - D2 failed!"
}
```

**Source 2: GitHub review threads**
```powershell
# Check GitHub for review comments (from D1 Copilot review AND D2 skill)
gh api graphql -f query='
  query {
    repository(owner: "microsoft", name: "PowerToys") {
      pullRequest(number: {{PRNumber}}) {
        reviewThreads(first: 50) {
          nodes { 
            id 
            isResolved 
            path 
            line 
            comments(first: 1) { 
              nodes { body author { login } } 
            } 
          }
        }
      }
    }
  }
'
```

**Decision point:**
- If no `00-OVERVIEW.md` exists → D2 failed, must retry
- If `HighSeverityCount` = 0 AND `UnresolvedThreadCount` = 0 → PR is clean, done!
- If issues found → Continue to D4

### Step D4: Post Review Comments (VS Code MCP)

**VS Code agent** reads review files and posts comments:

1. Read `Generated Files/prReview/{{PRNumber}}/00-OVERVIEW.md`
2. For each high/medium issue, create line-specific comments:

```
# Step 1: Create pending review
mcp_github_pull_request_review_write:
  method: "create"
  owner: "microsoft"
  repo: "PowerToys"
  pullNumber: {{PRNumber}}

# Step 2: Add line-specific comments
mcp_github_add_comment_to_pending_review:
  owner: "microsoft"
  repo: "PowerToys"
  pullNumber: {{PRNumber}}
  path: "path/to/file.cs"
  line: 42
  body: "Issue description and suggested fix..."
  subjectType: "LINE"
  side: "RIGHT"

# Step 3: Submit the review
mcp_github_pull_request_review_write:
  method: "submit_pending"
  owner: "microsoft"
  repo: "PowerToys"
  pullNumber: {{PRNumber}}
  event: "COMMENT"  # or "REQUEST_CHANGES" if not own PR
  body: "Summary of issues found..."
```

### Step D5: Invoke pr-fix Skill IN WORKTREE

**CRITICAL**: The pr-fix skill MUST run in the issue's worktree, NOT the main repo.

**Step 5a: Find the worktree for this PR's issue**
```powershell
# Get PR info to find the branch
$prInfo = gh pr view {{PRNumber}} --json headRefName | ConvertFrom-Json
$branch = $prInfo.headRefName  # e.g., "issue/44044"

# Find the worktree
. Q:\PowerToys\tools\build\WorktreeLib.ps1
$wt = Get-WorktreeEntries | Where-Object { $_.Branch -eq $branch }
$worktreePath = $wt.Path  # e.g., "Q:\PowerToys-ab12"
```

**Step 5b: Invoke pr-fix skill in the worktree**

The skill:
1. Loads `pr-fix/references/fix-pr-comments.prompt.md`
2. Reads unresolved review threads from GitHub
3. Applies AI-generated fixes to address each comment **in the worktree**
4. Commits and pushes the fixes

```powershell
# Script kicks off Copilot/Claude CLI in the WORKTREE directory
Push-Location $worktreePath
.github/skills/pr-fix/scripts/Start-PRFix.ps1 -PRNumber {{PRNumber}} -CLIType copilot -Force
Pop-Location
```

Or directly with copilot CLI:
```powershell
Push-Location $worktreePath
$prompt = "Fix PR #{{PRNumber}} review comments following .github/skills/pr-fix/references/fix-pr-comments.prompt.md"
copilot -p $prompt --yolo
Pop-Location
```

**Skill reference**: See `.github/skills/pr-fix/SKILL.md` for full details.

### Step D6: Resolve Comments (VS Code Agent)

After fixes are pushed, **VS Code agent** resolves the threads:

```powershell
# Get unresolved thread IDs
$threads = gh api graphql -f query='
  query {
    repository(owner: "microsoft", name: "PowerToys") {
      pullRequest(number: {{PRNumber}}) {
        reviewThreads(first: 50) {
          nodes { id isResolved }
        }
      }
    }
  }
' --jq '.data.repository.pullRequest.reviewThreads.nodes[] | select(.isResolved == false) | .id'

# Resolve each thread
foreach ($threadId in $threads) {
    gh api graphql -f query="mutation { resolveReviewThread(input: {threadId: `"$threadId`"}) { thread { isResolved } } }"
}
```

### Step D7: Re-check and Loop

```powershell
# Check if any issues remain
.github/skills/issue-to-pr-cycle/scripts/Get-CycleStatus.ps1 -PRNumbers {{PRNumber}}
```

**Loop conditions:**
- If `UnresolvedThreadCount` > 0 → Go back to D2 (re-review the fixes)
- If clean (0 unresolved) → ✅ Done!
- If max iterations reached (recommend 3) → Flag for human review

### Loop Iteration Tracking

Track iterations to prevent infinite loops:

```
Iteration 1: D1→D2→D3→D4→D5→D6→D7 (6 issues found, fixed)
Iteration 2: D2→D3→D4→D5→D6→D7 (2 issues found from fix, fixed)
Iteration 3: D2→D3→D7 (0 issues) → DONE ✅
```

**Note**: D1 (Copilot review) only runs on first iteration since it's for initial review.

## VS Code MCP Tools Reference

| Tool | Purpose |
|------|---------|
| `mcp_github_request_copilot_review` | Assign Copilot as PR reviewer |
| `mcp_github_pull_request_review_write` | Create/submit PR reviews |
| `mcp_github_add_comment_to_pending_review` | Add line-specific review comments |
| `mcp_github_issue_write` | Update issue status |
| `gh api graphql` (terminal) | Resolve review threads |

## Anti-Patterns (Don't Do This)

❌ **Don't** run `Start-FullIssueCycle.ps1` for Phase D (review/fix loop)
❌ **Don't** try to resolve comments via Copilot CLI (read-only MCP)
❌ **Don't** try to assign reviewers via Copilot CLI
❌ **Don't** run multiple PR review/fix loops in parallel (overwhelms CLI)

## Correct Pattern

✅ Use scripts for **parallel CLI operations** (review, fix)
✅ Use **VS Code MCP tools** for GitHub write operations
✅ Use **Get-CycleStatus.ps1** to check completion
✅ **Orchestrate step-by-step** with decision points
✅ Process PRs **sequentially** through review/fix loop

## Related Skills

| Skill | Purpose |
|-------|---------|
| `issue-review` | Analyze issues, generate implementation plans |
| `issue-fix` | Create fixes AND submit PRs |
| `pr-review` | Review PRs, post comments |
| `pr-fix` | Fix PR comments, resolve threads |

## Troubleshooting

| Problem | Solution |
|---------|----------|
| "Cannot resolve comment" | Use VS Code MCP or `gh api graphql`, not Copilot CLI |
| "Cannot assign reviewer" | Use `mcp_github_request_copilot_review` in VS Code |
| Review files not created | Check `Generated Files/prReview/<pr>/_copilot-review.log` |
| Max iterations reached | Flag for human review || Copilot CLI not available | Use VS Code Agent Fallback (see below) |

## VS Code Agent Fallback (No CLI)

When Copilot/Claude CLI is not available, the **VS Code agent** can perform the review/fix loop directly:

### Fallback D2: VS Code Agent Reviews PR

Instead of invoking pr-review skill via CLI, the VS Code agent:
1. Fetches PR diff: `gh pr diff {{PRNumber}}`
2. Analyzes code for issues (functionality, security, style, etc.)
3. Creates review files manually or posts comments directly

### Fallback D5: VS Code Agent Fixes Comments

Instead of invoking pr-fix skill via CLI, the VS Code agent:
1. Reads unresolved threads: `gh api graphql` query
2. Fetches current file content
3. Applies fixes using VS Code edit tools
4. Commits via `mcp_github_push_files` or `gh` commands

### Example Fallback Loop

```
ITERATION 1:
  D1: mcp_github_request_copilot_review (async)
  D2-FALLBACK: VS Code agent reviews PR diff manually
  D3: Check unresolved threads (gh api graphql)
  D4: Post comments (mcp_github_pull_request_review_write)
  D5-FALLBACK: VS Code agent applies fixes (read_file, replace_string_in_file)
  D6: Push fixes (mcp_github_push_files)
  D6: Resolve threads (gh api graphql mutation)
  D7: Re-check → Loop if needed
```

This fallback ensures the orchestration works even without CLI tools installed.