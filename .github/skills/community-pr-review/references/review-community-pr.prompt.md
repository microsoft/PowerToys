---
agent: 'agent'
description: 'Review a community bug-fix PR: 7-dimension code review, build verification, and verification guide'
tools: ['execute', 'read', 'edit', 'search', 'web', 'github/*']
argument-hint: 'PR number (e.g., #45234 or 45234)'
---

# Review Community Bug-Fix PR

**Goal**: Given `{{pr_number}}`, perform a focused bug-fix PR review across 7 dimensions, verify the build, and generate GitHub-ready review comments plus end-to-end verification instructions.

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

## Phase 2: Code Review (7 Dimensions)

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

## Phase 3: Build Verification

### 3.1 Checkout the PR
```powershell
gh pr checkout {{pr_number}}
```

### 3.2 Initialize and Build
```powershell
git submodule update --init --recursive
tools\build\build-essentials.cmd
tools\build\build.cmd
```

### 3.3 On Build Failure
If the build fails (non-zero exit code):

1. **Read the error log**: Find `build.*.errors.log` in the build directory
2. **Try merging main**: 
   ```powershell
   git fetch origin main
   git merge origin/main --no-edit
   ```
   Then rebuild. This is often effective for old PRs.
3. **Analyze remaining errors**: If merge didn't help, categorize errors:
   - Missing NuGet packages → run `build-essentials.cmd` again
   - API changes in dependencies → note for author
   - Merge conflicts → note for author
   - Code errors in PR → note in build report
4. **Record everything**: Every action taken goes into `build-report.md`

### 3.4 Build Report
Write `Generated Files/communityPrReview/{{pr_number}}/build-report.md`:
```markdown
# Build Report — PR #{{pr_number}}

## Build Status: SUCCESS | FAILURE | SUCCESS_AFTER_MERGE

## Environment
- Branch: <head branch>
- Base: <base branch>
- Head SHA: <sha>
- Build date: <ISO timestamp>

## Build Steps
1. <Step taken> — <result>
2. <Step taken> — <result>

## Actions Taken to Fix Build (if any)
- <action 1>
- <action 2>

## Remaining Build Errors (if any)
```<error details>```

## Suggestions for Author
- <suggestion for the PR author if build issues need their attention>
```

## Phase 4: Verification Guide

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

## Phase 5: Generate Review Comments

Write `Generated Files/communityPrReview/{{pr_number}}/review-comments.md`:

Structure each finding as a standalone GitHub review comment:

```markdown
# Review Comments — PR #{{pr_number}}

## Summary
- **High severity**: <count>
- **Medium severity**: <count>
- **Low severity**: <count>
- **Info**: <count>
- **Build status**: <SUCCESS | FAILURE | SUCCESS_AFTER_MERGE>

## Overall Assessment
<2-3 sentence overall assessment of the PR quality>

---

### [HIGH] Correctness — `path/to/file.ext`:42-48

<Clear description of the issue>

**Suggestion:**
```<language>
<code suggestion>
```

---

### [MEDIUM] Security — `path/to/file.ext`:15

<Clear description of the issue>

**Suggestion:**
<actionable suggestion>

---

(... more comments ...)

---

## Build Notes
<any build-related comments for the author>

## Verification Instructions
See `verification-guide.md` for detailed E2E verification steps.
```

If the review finds no issues, write:
```markdown
# Review Comments — PR #{{pr_number}}

## Summary
- No issues found across all 7 review dimensions
- **Build status**: SUCCESS

## Overall Assessment
This PR looks good. The fix is correct, well-scoped, and follows repo patterns.
No security, performance, or reliability concerns identified.

## Verification Instructions
See `verification-guide.md` for detailed E2E verification steps.
```

## Phase 6: Signal File

Write `Generated Files/communityPrReview/{{pr_number}}/.signal`:
```json
{
  "status": "success|partial|failure",
  "prNumber": {{pr_number}},
  "reviewFindings": { "high": 0, "medium": 0, "low": 0, "info": 0 },
  "buildStatus": "success|failure|success_after_merge",
  "buildActions": ["list of actions taken"],
  "timestamp": "<ISO timestamp>"
}
```

## Constraints

- **Read/analyze only** for code review — do not modify the PR's source code
- **Build verification may modify** the worktree (merge main, fix build config) but these changes are NOT pushed
- Keep comments specific, actionable, and fix-oriented
- Reference file paths and line numbers in all findings
- Stop and present results when human interaction is needed
