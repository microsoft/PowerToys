---
agent: 'ReviewCommunityPR'
description: 'Triage a community PR, request GitHub Copilot cloud review, process comments locally (auto-fix easy ones, escalate hard ones), build-verify, and iterate'
tools: ['execute', 'read', 'edit', 'search', 'web', 'github/*']
argument-hint: 'PR number (e.g., #45234 or 45234)'
---

# Review Community PR

Review a community-contributed PR using GitHub Copilot cloud review. This prompt invokes the `ReviewCommunityPR` agent.

## What It Does

Given a PR number, this workflow:

1. **Triages the PR** — checks if it's ready for review (skips drafts, WIP, incomplete PRs; flags early-stage feature PRs)
2. **Requests Copilot cloud review** — assigns GitHub Copilot as a reviewer on the PR
3. **Waits for Copilot comments** — polls until Copilot posts its review
4. **Categorizes comments** — separates easy (auto-fixable) from hard (needs human decision)
5. **Auto-fixes easy comments** — applies straightforward fixes without asking
6. **Builds the project** — runs `build-essentials` then `build` to verify fixes don't break anything
7. **Stops for hard comments** — presents complex comments for your review and guidance
8. **Iterates** — pushes fixes and requests Copilot re-review (up to 3 cycles)
9. **Generates outputs**:
   - `copilot-comments.md` — Categorized Copilot review comments per iteration
   - `fix-summary.md` — Record of all fixes applied (auto + human-guided)
   - `build-report.md` — Build status and actions taken
   - `final-summary.md` — Complete review record across all iterations

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
