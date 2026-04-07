---
agent: 'agent'
description: 'Review a community bug-fix PR: 7-dimension review, review→fix loop, build verification, and GitHub suggested changes'
tools: ['execute', 'read', 'edit', 'search', 'web', 'github/*']
argument-hint: 'PR number (e.g., #45234 or 45234)'
---

# Review Community Bug-Fix PR

**Goal**: Given `{{pr_number}}`, run a review→fix loop: review across 7 dimensions, fix high/medium issues, re-review until clean, verify build, then generate GitHub suggested changes and verification instructions.

**Output folder**: `Generated Files/communityPrReview/{{pr_number}}/`

## PR Selection

Resolve the target PR:
1. Parse the invocation text for an explicit PR number (first integer following `#` or `PR`).
2. If not found, run `gh pr view --json number` for the current branch.
3. If still unknown, **ASK** the user.

## Phase 1: Understand the PR

### 1.1 Fetch PR Metadata
```
gh pr view {{pr_number}} --json number,title,body,author,baseRefName,headRefName,baseRefOid,headRefOid,labels,url,state
```

### 1.2 Identify the Bug
- Parse the PR description for linked issue references (`Fixes #XXXX`, `Closes #XXXX`)
- If linked issue found, fetch it: `gh issue view <issue_number> --json title,body,labels`
- Understand: what is the bug? what is the expected behavior? what is the actual behavior?

### 1.3 Fetch Changed Files
```
gh pr diff {{pr_number}}
```
Also fetch the file list:
```
gh pr view {{pr_number}} --json files
```

### 1.4 Record Original Head SHA
Save the PR's head commit SHA as `originalHeadSha` — this is the baseline for generating suggested changes later.
```powershell
$originalHeadSha = (gh pr view {{pr_number}} --json headRefOid --jq .headRefOid)
```

### 1.5 Checkout and Initial Build
```powershell
gh pr checkout {{pr_number}}
git submodule update --init --recursive
tools\build\build-essentials.cmd
tools\build\build.cmd
```
If the initial build fails, try merging main:
```powershell
git fetch origin main
git merge origin/main --no-edit
tools\build\build.cmd
```
Record the build result. Even if it fails, proceed to code review.

## Phase 2: Review→Fix Loop (max 3 iterations)

For each iteration, perform the code review then fix. Track `iteration` starting at 1.

### Step 2a: Code Review (7 Dimensions)

For each dimension below, analyze ALL changed files and produce findings. Skip a dimension only if no changed files are relevant to it.

### Dimension 1: Correctness
- Does the fix actually solve the reported bug?
- Are all code paths to the bug covered?
- Are edge cases handled (null, empty, boundary values, concurrent access)?
- Could the fix introduce new bugs or regressions?
- Are tests updated/added to cover the fix?
- Is the fix complete or only partial?

### Dimension 2: Security
- Is user input validated before use?
- Are file paths canonicalized to prevent traversal?
- Are shell commands avoided or properly escaped?
- Is elevation (UAC) used only when necessary and scoped minimally?
- Are credentials, tokens, or PII never logged or exposed?
- For native code: buffer overflow prevention, format string safety, P/Invoke correctness?
- Are IPC messages validated before processing?
- Reference: OWASP Top 10, CWE Top 25, Microsoft SDL

### Dimension 3: Performance
- Are hot paths (hooks, tight loops, event handlers) kept efficient?
- No unnecessary allocations in frequently called code?
- Are async patterns used correctly (no sync-over-async, proper cancellation)?
- Are collections appropriately sized?
- Are expensive operations (file I/O, registry, network) minimized?
- No logging in performance-critical paths?

### Dimension 4: Reliability
- Are errors handled gracefully (try/catch, HRESULT checks, null guards)?
- Are resources properly disposed (IDisposable, COM objects, handles)?
- Are race conditions prevented (thread safety, locking)?
- Are event subscriptions balanced (subscribe ↔ unsubscribe)?
- Does the fix handle process/module lifecycle correctly?
- Are retries and timeouts appropriate?

### Dimension 5: Design
- Is the fix appropriately scoped (not over-engineered)?
- Does it follow SOLID principles?
- Is the abstraction level appropriate?
- Could the fix be simpler while still being correct?
- Are there any code smells introduced (magic numbers, god methods, deep nesting)?
- Is the fix in the right layer/module?

### Dimension 6: Compatibility
- Are there breaking changes to public APIs or IPC contracts?
- Is backward compatibility maintained for settings/config files?
- Does the fix work across supported Windows versions (10 1803+)?
- Are there implications for the installer or upgrade path?
- If modifying shared code (`src/common/`), is ABI stability preserved?

### Dimension 7: Repo Patterns
- Does the code follow PowerToys naming conventions?
- Is the style consistent with surrounding code (check `.editorconfig`, `.clang-format`)?
- Are new strings localized (`.resx` files)?
- Is logging following the PowerToys pattern (spdlog for C++, Logger for C#)?
- Are module interface contracts preserved?
- Is the PR atomic (one logical change)?

### Step 2b: Write Review Comments

Write findings to `Generated Files/communityPrReview/{{pr_number}}/review-comments.md`.

For **high** and **medium** findings, ALWAYS include a ` ```suggestion ` block with replacement code:

```markdown
# Review Comments — PR #{{pr_number}} — Iteration {{iteration}}

## Summary
- **High severity**: <count>
- **Medium severity**: <count>
- **Low severity**: <count>
- **Info**: <count>

## Overall Assessment
<2-3 sentence assessment>

---

### [HIGH] Correctness — `path/to/file.ext`:42-48

<Clear description of the issue>

` ```suggestion `
<replacement code that fixes the issue>
` ``` `

---

### [MEDIUM] Security — `path/to/file.ext`:15-18

<Clear description of the issue>

` ```suggestion `
<replacement code that fixes the issue>
` ``` `

---
```

### Step 2c: Check Exit Condition

**Exit the loop if ANY of these are true:**
- No **high** or **medium** severity findings in this iteration
- This is iteration 3 (maximum reached)
- All high/medium findings are architectural or require author decision (cannot be auto-fixed)

If exiting → go to Phase 3.

### Step 2d: Apply Fixes

For each **high** and **medium** finding from Step 2b:
1. Open the file at the referenced line range
2. Apply the fix from the ` ```suggestion ` block (or implement the intent if suggestion is conceptual)
3. Keep fixes minimal — only what's needed

After all fixes:
- Run `tools\build\build.cmd`
- If build fails, read `build.*.errors.log` and fix build errors (max 3 attempts)
- Record all fixes in `fix-summary.md`

### Step 2e: Increment and Loop

Increment `iteration` and go back to Step 2a to re-review the fixed code.

---

## Phase 3: Generate Suggested Changes

After the review→fix loop completes, generate GitHub suggested changes from the diff:

```powershell
# Compare worktree (with fixes) against original PR head
git diff <originalHeadSha> HEAD
```

For each changed hunk, write a suggested change using GitHub's native format.

Write `Generated Files/communityPrReview/{{pr_number}}/suggested-changes.md`:

```markdown
# Suggested Changes — PR #{{pr_number}}

These changes address review findings from {{iteration}} review→fix iteration(s).
Each suggestion uses GitHub's suggested changes format — post as PR review comments.

## Summary
- **Total suggestions**: <count>
- **Files affected**: <count>
- **Iterations needed**: <count>

## Fixes Applied
<Brief list of what each fix addresses>

---

## `path/to/file.ext`

### Suggestion 1 (lines X-Y)
**Addresses:** [SEVERITY] Dimension — <brief finding description>

` ```suggestion `
<replacement code>
` ``` `

---
```

If no fixes were needed (clean review), write:
```markdown
# Suggested Changes — PR #{{pr_number}}

No code changes needed. The review found no high/medium issues.
```

## Phase 4: Build Report

Write `Generated Files/communityPrReview/{{pr_number}}/build-report.md`:
```markdown
# Build Report — PR #{{pr_number}}

## Build Status: SUCCESS | FAILURE | SUCCESS_AFTER_MERGE

## Environment
- Branch: <head branch>
- Base: <base branch>
- Head SHA: <sha>
- Build date: <ISO timestamp>
- Review iterations: <count>

## Build Steps
1. <Step taken> — <result>
2. <Step taken> — <result>

## Actions Taken to Fix Build (if any)
- <action 1>
- <action 2>

## Remaining Build Errors (if any)
` ``` `
<error details>
` ``` `

## Suggestions for Author
- <suggestion for the PR author if build issues need their attention>
```

## Phase 5: Verification Guide

Write `Generated Files/communityPrReview/{{pr_number}}/verification-guide.md`:

```markdown
# Verification Guide — PR #{{pr_number}}

## Bug Summary
<1-2 sentence description of the bug from the linked issue>

## Steps to Reproduce the Original Bug
1. <step>
2. <step>
3. <step>

## How to Verify the Fix
1. <step>
2. <step>
3. <step>

## Expected Behavior After Fix
- <what should happen>

## Edge Cases to Test
- <edge case 1>
- <edge case 2>

## Regression Areas to Smoke-Test
- <area 1 — why it might be affected>
- <area 2 — why it might be affected>

## Module/Feature Affected
- **Module**: <module name>
- **Settings path**: <if applicable>
- **Hotkey**: <if applicable>
```

## Phase 6: Signal File

Write `Generated Files/communityPrReview/{{pr_number}}/.signal`:
```json
{
  "status": "success|partial|failure",
  "prNumber": {{pr_number}},
  "originalHeadSha": "<original PR head SHA before any fixes>",
  "iterations": <number of review-fix iterations>,
  "reviewFindings": { "high": 0, "medium": 0, "low": 0, "info": 0 },
  "fixesApplied": <count of fixes applied>,
  "suggestedChanges": <count of GitHub suggestions generated>,
  "buildStatus": "success|failure|success_after_merge",
  "buildActions": ["list of actions taken"],
  "timestamp": "<ISO timestamp>"
}
```

## Constraints

- Keep fixes minimal — only address high/medium review findings
- **Build verification may modify** the worktree (merge main, apply fixes) but these changes are NOT pushed
- All fixes are presented as **suggested changes** for the PR author to accept
- Keep comments specific, actionable, and fix-oriented
- Reference file paths and line numbers in all findings
- Stop and present results when human interaction is needed
- Maximum 3 review→fix iterations — report remaining issues if loop doesn't converge
