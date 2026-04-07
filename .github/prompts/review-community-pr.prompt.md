---
agent: 'ReviewCommunityPR'
description: 'Review a community bug-fix PR with review→fix loop, build verification, and GitHub suggested changes'
tools: ['execute', 'read', 'edit', 'search', 'web', 'github/*']
argument-hint: 'PR number (e.g., #45234 or 45234)'
---

# Review Community PR

Review a community-contributed bug-fix PR. This prompt invokes the `ReviewCommunityPR` agent.

## What It Does

Given a PR number, this workflow:

1. **Understands the bug** — reads the PR and linked issue
2. **Reviews the code** across 7 dimensions: correctness, security, performance, reliability, design, compatibility, and repo patterns
3. **Fixes issues** — applies fixes for high/medium findings, verifies build, re-reviews (up to 3 iterations)
4. **Generates suggested changes** — diffs the fixed code against the original PR and formats as GitHub ` ```suggestion ` blocks
5. **Generates outputs**:
   - `suggested-changes.md` — GitHub suggested changes ready to post as PR review comments
   - `review-comments.md` — Full review findings per iteration
   - `fix-summary.md` — Record of all fixes applied
   - `build-report.md` — Build status and actions taken
   - `verification-guide.md` — Step-by-step E2E verification instructions

## Usage

Provide a PR number:

```
Review community PR #45234
```

Or run the script directly:

```powershell
.github/skills/community-pr-review/scripts/Start-CommunityPRReview.ps1 -PRNumber 45234
```

## Output Location

`Generated Files/communityPrReview/<PR>/`
