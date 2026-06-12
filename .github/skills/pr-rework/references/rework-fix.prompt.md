---
description: 'Fix PR review findings locally without posting or pushing'
name: 'rework-fix'
agent: 'agent'
argument-hint: 'PR number and findings file'
---

# Rework Fix — Local PR Quality Pass

## Mission
Address review findings from a local pr-review pass. Apply targeted code fixes, then build and run unit tests. All changes stay local — no commits, no pushes, no GitHub comment posting.

## Scope & Preconditions
- You are working in a git worktree checked out to the PR branch.
- A findings file (JSON) lists the issues to address, each with severity, file, line, and description.
- Only implement fixes for findings with severity >= the configured minimum.
- Do NOT commit, push, post comments, or resolve GitHub threads.

## Inputs
- Required: `${input:pr_number}` — PR number for context
- Required: `${input:findings_file}` — Path to `findings.json` with parsed review findings
- Optional: `${input:build_errors}` — Path to build error log from a previous failed build
- Optional: `${input:test_failures}` — Path to test failure log from a previous failed test run
- Optional: `${input:min_severity}` — Minimum severity to fix (`high`, `medium`, `low`). Default: `medium`

## Findings JSON Schema
```json
[
  {
    "id": "F-001",
    "step": "01-functionality",
    "severity": "high",
    "file": "src/modules/Foo/Bar.cs",
    "line": 42,
    "endLine": 50,
    "title": "Null reference in error path",
    "description": "The catch block accesses `result.Value` without null check...",
    "suggestedFix": "Add null guard before accessing .Value"
  }
]
```

## Workflow

### Phase 1: Understand Context
1. Read the PR diff to understand overall changes: `git diff origin/main --stat`
   (Use two-dot `git diff origin/main`, NOT three-dot `origin/main...HEAD` — changes may be uncommitted.
   Always use `origin/main` instead of `main` to avoid stale local refs.)
2. Read the findings file to understand what needs fixing.
3. If build errors or test failures are provided, read those too — they take priority.

### Phase 2: Fix Build Errors (if any)
If `${input:build_errors}` is provided:
1. Read the build error log.
2. Fix each compilation error.
3. These take absolute priority over review findings.

### Phase 3: Fix Test Failures (if any)
If `${input:test_failures}` is provided:
1. Read the test failure log.
2. Fix each failing test — either fix the code under test or update the test if the new behavior is intentional.
3. Test failures take priority over review findings (except build errors).

### Phase 4: Fix Review Findings
For each finding in `${input:findings_file}` with severity >= `${input:min_severity}`:
1. Read the target file and understand the context around the reported line.
2. Determine the appropriate fix:
   - **Simple fix**: Apply the code change directly.
   - **Complex refactor**: Write a brief note explaining why it was deferred (do not change code).
3. Apply the fix with minimal edits — do not refactor surrounding code.
4. Track what was fixed and what was deferred.

### Phase 5: Build Verification
1. Identify changed project files: `git diff --name-only | Select-String '\.(csproj|vcxproj)$'`
2. If specific projects changed, build them: `tools/build/build.cmd -Path <project-dir>`
3. Otherwise, build from the changed source directories.
4. Check exit code — 0 means success.
5. If build fails, read the error log and fix the issues, then rebuild.
6. Repeat up to 3 build-fix attempts.

### Phase 6: Unit Test Verification
1. Find test projects related to the changed code:
   - Look for sibling or nearby `*UnitTests` or `*Tests` projects.
   - Match by product code prefix (e.g., changes to `FancyZones` → look for `FancyZonesUnitTests`).
2. Build the test project if found.
3. Run the tests using `vstest.console.exe` or the test runner available.
4. If tests fail, analyze failures and fix — either the production code or the test expectations.
5. Repeat up to 2 test-fix attempts.

### Phase 7: Summary
Write a brief summary to stdout listing:
- Findings fixed (with IDs)
- Findings deferred (with reasons)
- Build result (pass/fail)
- Test result (pass/fail/skipped)

## Output Expectations
- Code changes applied in the worktree (not committed).
- Build passes (exit code 0).
- Unit tests pass (or no test project found).
- No commits, no pushes, no GitHub API calls.

## Quality Rules
- Follow existing code style (`.editorconfig`, `.clang-format`, XamlStyler).
- Do not introduce new warnings.
- Do not add noisy logging in hot paths.
- Keep changes minimal and targeted to the finding.
