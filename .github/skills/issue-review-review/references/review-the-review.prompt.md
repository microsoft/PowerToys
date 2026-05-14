---
agent: 'agent'
description: 'Meta-review of issue-review outputs: validate scores, check implementation plan quality, produce feedback'
---

# Review the Review — Meta-Analysis of Issue Review Quality

## Goal
For issue **#{{issue_number}}**, validate the existing `issue-review` outputs and produce:
1) `Generated Files/issueReviewReview/{{issue_number}}/reviewTheReview.md`

## Inputs

You MUST have these files available before starting:
- `Generated Files/issueReview/{{issue_number}}/overview.md` — The original review scores and assessment
- `Generated Files/issueReview/{{issue_number}}/implementation-plan.md` — The original implementation plan
- The original GitHub issue data (fetch via `gh issue view {{issue_number}}`)

If a feedback file from a previous iteration exists, also read it:
- `Generated Files/issueReviewReview/{{issue_number}}/reviewTheReview.md` — Previous meta-review feedback (check if iteration > 1)

## Process

### Step 1: Gather Context

1. **Read the original issue**: `gh issue view {{issue_number}} --json number,title,body,author,createdAt,updatedAt,state,labels,milestone,reactions,comments,linkedPullRequests`
2. **Read overview.md**: Parse all scores (Business Importance, Community Excitement, Technical Feasibility, Requirement Clarity, Overall Priority, Effort Estimate)
3. **Read implementation-plan.md**: Parse all sections (Problem Framing, Layers & Files, Pattern Choices, Fundamentals, Task Breakdown)
4. **Examine the actual codebase**: Use `rg`/`git grep`/`find` to verify file paths mentioned in the implementation plan actually exist
5. **Check for similar past fixes**: Search for related PRs and how they were implemented

### Step 2: Validate Scores

For EACH score dimension, evaluate whether the score matches the evidence:

#### A) Business Importance Score Validation
- Does the score align with the issue's labels (priority/security/regression)?
- Is the milestone/roadmap impact correctly assessed?
- Are customer/contract impacts properly weighted?

#### B) Community Excitement Score Validation
- Count actual 👍/❤️ reactions and compare against the score
- Verify comment count and unique participant count
- Check if recent activity assessment is accurate
- Verify duplicate/related issue count

#### C) Technical Feasibility Score Validation
- **CRITICAL**: Verify that files mentioned in the plan actually exist in the repo
- Check if the proposed changes follow existing patterns (use `rg` to find similar patterns)
- Assess whether risk factors (perf/security/compat) are properly identified
- Verify testability claims by checking if test infrastructure exists for the affected module

#### D) Requirement Clarity Score Validation
- Does the issue actually contain clear repro steps?
- Are non-functional requirements (perf/security/i18n/a11y) addressed?
- Are acceptance criteria defined or at least inferable?

### Step 3: Validate Implementation Plan

For EACH section of the implementation plan:

#### Problem Framing
- Is the problem correctly understood?
- Are scope boundaries reasonable?
- Is current vs expected behavior accurately described?

#### Layers & Files
- **CRITICAL**: Do ALL referenced files/directories exist? Run `test -f <path>` or `ls <path>` for each one
- Are the file paths using correct casing and separators?
- Are all affected layers identified (UI/domain/data/infra/build)?
- Are any files missing that should be modified?

#### Pattern Choices
- Do the suggested patterns match what the repo actually uses?
- Use `rg` to find 2-3 examples of the suggested pattern in the codebase
- If a new pattern is suggested, is the justification sound?

#### Fundamentals
- Are performance concerns addressed for the specific module?
- Are security implications properly assessed?
- Is i18n/l10n handled (check for hardcoded strings)?
- Is accessibility considered (keyboard nav, screen readers)?

#### Task Breakdown
- Can an AI agent actually execute each task as written?
- Are the steps in the right order (dependencies respected)?
- Are test requirements specified for each task?
- Is the human-vs-agent ownership realistic?

### Step 4: Check for Red Flags

Flag these issues if found:
- 🔴 **Ghost files**: Implementation plan references files that don't exist
- 🔴 **Wrong patterns**: Suggested approach contradicts existing codebase patterns
- 🔴 **Missing tests**: No test plan for behavior changes
- 🔴 **Score inflation**: Scores are ≥20 points higher than evidence supports
- 🔴 **Score deflation**: Scores are ≥20 points lower than evidence supports
- 🟡 **Incomplete coverage**: Missing fundamentals (security, i18n, a11y)
- 🟡 **Vague tasks**: Task breakdown has steps that are too broad to execute
- 🟡 **Missing dependencies**: Task order doesn't respect build/import dependencies

## Output: reviewTheReview.md

Generate the following structure:

```markdown
# Review-Review: Issue #{{issue_number}}

**Review Quality Score: X/100**
**Iteration: N**
**Verdict: PASS / NEEDS_IMPROVEMENT / FAIL**

## Executive Summary

Brief (2-3 sentences) on whether the original review is trustworthy and actionable.

## Score Validation

| Dimension | Original Score | Validated Score | Delta | Assessment |
|-----------|---------------|-----------------|-------|------------|
| Business Importance | X/100 | Y/100 | ±Z | ✅ Accurate / ⚠️ Inflated / ⚠️ Deflated |
| Community Excitement | X/100 | Y/100 | ±Z | ✅ / ⚠️ |
| Technical Feasibility | X/100 | Y/100 | ±Z | ✅ / ⚠️ |
| Requirement Clarity | X/100 | Y/100 | ±Z | ✅ / ⚠️ |
| Overall Priority | X/100 | Y/100 | ±Z | ✅ / ⚠️ |

### Score Details

For each dimension where delta ≥ 10 points:
- What evidence was missed or misinterpreted
- What the correct assessment should be
- Specific data points supporting the correction

## Implementation Plan Validation

### Files Verification

| File Path | Exists? | Correct? | Notes |
|-----------|---------|----------|-------|
| `src/modules/...` | ✅/❌ | ✅/⚠️ | ... |

### Pattern Verification

| Suggested Pattern | Used in Repo? | Examples Found | Assessment |
|-------------------|---------------|----------------|------------|
| ... | ✅/❌ | `src/...`, `src/...` | ✅ Correct / ⚠️ Wrong pattern |

### Task Breakdown Assessment

| Task # | Executable by Agent? | Issues | Corrective Action |
|--------|---------------------|--------|-------------------|
| 1 | ✅/⚠️/❌ | ... | ... |

## Red Flags Found

List any 🔴 or 🟡 flags with evidence.

## Corrective Feedback for Re-Review

**IF quality score < 90, provide specific instructions for issue-review to fix:**

### Scores to Adjust
- Dimension X: Change from Y to Z because [evidence]

### Implementation Plan Corrections
- File path corrections: [list]
- Missing files to add: [list]
- Pattern corrections: [list]
- Task breakdown fixes: [list]

### Missing Coverage
- Add section on: [topic]
- Expand analysis of: [topic]

## Quality Score Breakdown

| Dimension | Score | Weight | Weighted |
|-----------|-------|--------|----------|
| Score Accuracy | X/100 | 30% | X |
| Implementation Correctness | X/100 | 25% | X |
| Risk Assessment | X/100 | 15% | X |
| Completeness | X/100 | 15% | X |
| Actionability | X/100 | 15% | X |
| **Total** | | | **X/100** |
```

## Important Rules

1. **Be evidence-based**: Every correction must cite specific files, lines, or data
2. **Verify file existence**: ALWAYS run `test -f` or `ls` for paths in the implementation plan
3. **Check patterns**: Use `rg` to find at least 2 examples of any suggested pattern
4. **Don't be a rubber stamp**: If the review looks perfect, still verify the top 3 most impactful claims
5. **Actionable feedback**: Every issue found must include a specific correction, not just "this is wrong"
6. **Score honestly**: The quality score should reflect real issues found, not just gut feeling
