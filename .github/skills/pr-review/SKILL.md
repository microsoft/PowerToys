---
name: pr-review
description: Comprehensive pull request review with multi-step analysis and comment posting. Use when asked to review a PR, analyze pull request changes, check PR for issues, post review comments, validate PR quality, run code review on a PR, or audit pull request. Generates 13 review step files covering functionality, security, performance, accessibility, and more. For FIXING PR comments, use the pr-fix skill instead.
license: Complete terms in LICENSE.txt
---

# PR Review Skill

Perform comprehensive pull request reviews with multi-step analysis covering functionality, security, performance, accessibility, localization, and more.

**Note**: This skill is for **reviewing** PRs only. To **fix** review comments, use the `pr-fix` skill.

## Critical Guidelines

### Load-on-Demand Architecture
Step prompt files are loaded **only when that step is executed** to minimize context usage:
- Read `references/review-pr.prompt.md` first for orchestration
- Load each `references/0X-*.prompt.md` only when executing that step
- Skip steps based on smart filtering (see review-pr.prompt.md)

### Mandatory External Reference Research
**Each step prompt includes an `## External references (MUST research)` section.** Before completing any step, you **MUST**:

1. **Fetch the referenced URLs** using `fetch_webpage` or equivalent
2. **Analyze PR changes against those authoritative sources**
3. **Include a `## References consulted` section** in the output file listing:
   - Which guidelines were checked
   - Any violations found with specific IDs (e.g., WCAG 1.4.3, OWASP A03, CWE-79)

| Step | Key External References |
|------|------------------------|
| 04 Accessibility | WCAG 2.1, Windows Accessibility Guidelines |
| 05 Security | OWASP Top 10, CWE Top 25, Microsoft SDL |
| 06 Localization | .NET Localization, Microsoft Style Guide |
| 07 Globalization | Unicode TR9 (BiDi), ICU Guidelines |
| 09 SOLID Design | .NET Architecture Guidelines, Design Patterns |

**Failure to research external references is a review quality violation.**

## Skill Contents

This skill is **self-contained** with all required resources:

```
.github/skills/pr-review/
├── SKILL.md              # This file
├── LICENSE.txt           # MIT License
├── scripts/
│   ├── Start-PRReviewWorkflow.ps1    # Main review script
│   ├── Post-ReviewComments.ps1       # Post comments to GitHub
│   ├── Get-GitHubPrFilePatch.ps1     # Fetch PR file diffs
│   ├── Get-GitHubRawFile.ps1         # Download repo files
│   ├── Get-PrIncrementalChanges.ps1  # Detect incremental changes
│   └── Test-IncrementalReview.ps1    # Test incremental detection
└── references/
    ├── review-pr.prompt.md           # Orchestration prompt (load first)
    ├── 01-functionality.prompt.md    # Step 01 detailed checks
    ├── 02-compatibility.prompt.md    # Step 02 detailed checks
    ├── 03-performance.prompt.md      # Step 03 detailed checks
    ├── 04-accessibility.prompt.md    # Step 04 detailed checks
    ├── 05-security.prompt.md         # Step 05 detailed checks
    ├── 06-localization.prompt.md     # Step 06 detailed checks
    ├── 07-globalization.prompt.md    # Step 07 detailed checks
    ├── 08-extensibility.prompt.md    # Step 08 detailed checks
    ├── 09-solid-design.prompt.md     # Step 09 detailed checks
    ├── 10-repo-patterns.prompt.md    # Step 10 detailed checks
    ├── 11-docs-automation.prompt.md  # Step 11 detailed checks
    ├── 12-code-comments.prompt.md    # Step 12 detailed checks
    └── 13-copilot-guidance.prompt.md # Step 13 (conditional)
```

## Output Directory

All generated artifacts are placed under `Generated Files/prReview/<pr-number>/` at the repository root (gitignored).

```
Generated Files/prReview/
└── <pr-number>/
    ├── 00-OVERVIEW.md           # Summary with all findings
    ├── 01-functionality.md      # Functional correctness
    ├── 02-compatibility.md      # Breaking changes, versioning
    ├── 03-performance.md        # Performance implications
    ├── 04-accessibility.md      # A11y compliance
    ├── 05-security.md           # Security concerns
    ├── 06-localization.md       # L10n readiness
    ├── 07-globalization.md      # G11n considerations
    ├── 08-extensibility.md      # API/extension points
    ├── 09-solid-design.md       # SOLID principles
    ├── 10-repo-patterns.md      # PowerToys conventions
    ├── 11-docs-automation.md    # Documentation coverage
    ├── 12-code-comments.md      # Code comment quality
    └── 13-copilot-guidance.md   # (if applicable)
```

## When to Use This Skill

- Review a specific pull request
- Analyze PR changes for quality issues
- Post review comments on a PR
- Validate PR against PowerToys standards
- Run comprehensive code review

## Prerequisites

- GitHub CLI (`gh`) installed and authenticated
- PowerShell 7+ for running scripts
- GitHub MCP configured (for posting comments)

## Required Variables

⚠️ **For single PR review**, confirm `{{PRNumber}}` with the user. For batch modes, see "Batch Review Modes" below.

| Variable | Description | Example |
|----------|-------------|---------|
| `{{PRNumber}}` | Pull request number to review | `45234` |

## Workflow

### Single PR Review

Execute the review workflow for a specific PR:

```powershell
# From repo root
.github/skills/pr-review/scripts/Start-PRReviewWorkflow.ps1 -PRNumbers {{PRNumber}} -CLIType copilot -SkipAssign -SkipFix -Force
```

### Batch Review Modes

Review multiple PRs with a single command:

```powershell
# Review ALL open non-draft PRs in the repository
.github/skills/pr-review/scripts/Start-PRReviewWorkflow.ps1 -AllOpen -SkipAssign -SkipFix -Force

# Review only PRs assigned to me
.github/skills/pr-review/scripts/Start-PRReviewWorkflow.ps1 -Assigned -SkipAssign -SkipFix -Force

# Review ALL open PRs, skip those already reviewed
.github/skills/pr-review/scripts/Start-PRReviewWorkflow.ps1 -AllOpen -SkipExisting -SkipAssign -SkipFix -Force

# Limit batch size
.github/skills/pr-review/scripts/Start-PRReviewWorkflow.ps1 -AllOpen -Limit 50 -SkipExisting -Force
```

### Background Batch Review (Recommended for Large Batches)

For reviewing many PRs, generate a standalone batch script and run it in background:

```powershell
# Step 1: Generate the batch script
.github/skills/pr-review/scripts/Start-PRReviewWorkflow.ps1 -AllOpen -SkipExisting -GenerateBatchScript -Force

# Step 2: Run in background (minimized window)
Start-Process pwsh -ArgumentList '-File', 'Generated Files/prReview/_batch-review.ps1' -WindowStyle Minimized

# Or run interactively to see progress
pwsh -File "Generated Files/prReview/_batch-review.ps1"
```

The batch script:
- Processes PRs sequentially (more reliable than parallel)
- Skips already-reviewed PRs automatically
- Shows progress as `[N/Total] PR #XXXXX`
- Logs copilot output to `_copilot.log` in each PR folder
- Reports failed PRs at the end

### Step 2: Review Output

Check the generated files at `Generated Files/prReview/{{PRNumber}}/`

## CLI Options

| Parameter | Description | Default |
|-----------|-------------|---------|
| `-PRNumbers` | PR number(s) to review | From worktrees |
| `-AllOpen` | Review ALL open non-draft PRs | `false` |
| `-Assigned` | Review PRs assigned to current user | `false` |
| `-Limit` | Max PRs to fetch for batch modes | `100` |
| `-SkipExisting` | Skip PRs with completed reviews | `false` |
| `-GenerateBatchScript` | Generate standalone script for background execution | `false` |
| `-CLIType` | AI CLI to use: `copilot` or `claude` | `copilot` |
| `-MinSeverity` | Min severity to post: `high`, `medium`, `low`, `info` | `medium` |
| `-SkipAssign` | Skip assigning Copilot as reviewer | `false` |
| `-SkipReview` | Skip the review step | `false` |
| `-SkipFix` | Skip fix step (recommended - use `pr-fix` skill instead) | `false` |
| `-MaxParallel` | Maximum parallel jobs | `3` |
| `-Force` | Skip confirmation prompts | `false` |

**Note**: The `-SkipFix` option is kept for backward compatibility. For fixing PR comments, use the dedicated `pr-fix` skill which provides better control over the fix/resolve loop.

## AI Prompt References

### Orchestration (load first)
- `references/review-pr.prompt.md` - Main orchestration with PR selection, iteration management, smart filtering

### Step Prompts (load on-demand per step)
Each step prompt contains:
- Detailed checklist of concerns (15-25 items)
- PowerToys-specific checks
- Severity guidelines
- Output file template
- **External references (MUST research)** section

| Step | Prompt File | External References |
|------|-------------|---------------------|
| 01 | `01-functionality.prompt.md` | C# Guidelines, .NET API Design |
| 02 | `02-compatibility.prompt.md` | Windows Versions, .NET Breaking Changes |
| 03 | `03-performance.prompt.md` | .NET Performance, Async Best Practices |
| 04 | `04-accessibility.prompt.md` | **WCAG 2.1**, Windows Accessibility |
| 05 | `05-security.prompt.md` | **OWASP Top 10**, **CWE Top 25**, SDL |
| 06 | `06-localization.prompt.md` | .NET Localization, MS Style Guide |
| 07 | `07-globalization.prompt.md` | Unicode BiDi, ICU, Date/Time Formatting |
| 08 | `08-extensibility.prompt.md` | Plugin Architecture, SemVer |
| 09 | `09-solid-design.prompt.md` | SOLID Principles, Clean Architecture |
| 10 | `10-repo-patterns.prompt.md` | PowerToys docs (architecture, style, logging) |
| 11 | `11-docs-automation.prompt.md` | MS Writing Style, XML Docs |
| 12 | `12-code-comments.prompt.md` | XML Documentation, Comment Conventions |
| 13 | `13-copilot-guidance.prompt.md` | Agent Skills Spec, Prompt Engineering |

### Fix Prompt
- `references/fix-pr-active-comments.prompt.md` - Address active review comments

## Troubleshooting

| Problem | Solution |
|---------|----------|
| PR not found | Verify PR number: `gh pr view {{PRNumber}}` |
| Review incomplete | Check `_copilot-review.log` for errors |
| Comments not posted | Use VS Code MCP tools (Copilot CLI is read-only) |
| Missing `## References consulted` | Re-run step with external reference research |
| Cannot resolve comments | Use `gh api graphql` with resolveReviewThread mutation |

## ⚠️ VS Code Agent Operations

**Copilot CLI's MCP is read-only.** These operations require VS Code MCP tools:

| Operation | VS Code MCP Tool |
|-----------|------------------|
| Assign Copilot reviewer | `mcp_github_request_copilot_review` |
| Post review comments | `mcp_github_pull_request_review_write` |
| Add line-specific comments | `mcp_github_add_comment_to_pending_review` |
| Resolve threads | `gh api graphql` with `resolveReviewThread` |

### Resolve Review Thread Example

```powershell
# Get unresolved threads
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

# Resolve a specific thread
gh api graphql -f query='
  mutation {
    resolveReviewThread(input: {threadId: "{{threadId}}"}) {
      thread { isResolved }
    }
  }
'
```
