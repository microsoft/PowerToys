```prompt
# PR Triage Evaluation

You are evaluating a pull request for the **PowerToys** repository.
Read ALL information below — metadata, discussion, images, AI code review — then score each evaluation dimension.

## The PR

- **PR #{{PR_NUMBER}}**: {{PR_TITLE}}
- **Author**: @{{PR_AUTHOR}}
- **URL**: {{PR_URL}}
- **Age**: {{AGE_DAYS}} days
- **Days since last activity**: {{DAYS_SINCE_ACTIVITY}}
- **Days since author last active**: {{DAYS_SINCE_AUTHOR_ACTIVITY}}
- **Size**: +{{ADDITIONS}} / -{{DELETIONS}} ({{CHANGED_FILES}} files)
- **Labels**: {{LABELS}}
- **Linked issues**: {{LINKED_ISSUES}}
- **Is draft**: {{IS_DRAFT}}

### Review Status
- **Human approvals**: {{APPROVAL_COUNT}}
- **Changes requested**: {{CHANGES_REQUESTED_COUNT}}
- **CI status**: {{CHECKS_STATUS}}
- **Failing checks**: {{FAILING_CHECKS}}
- **Mergeable**: {{MERGEABLE}}

### AI Code Review Summary
{{AI_REVIEW_SUMMARY}}

## Your Tasks

### Step 1: Read the PR discussion

Use the GitHub MCP tools to understand the FULL context:

1. **Fetch images and attachments** from PR #{{PR_NUMBER}} in `microsoft/PowerToys`:
   - Use `github_issue_images` tool with owner=`microsoft`, repo=`PowerToys`, issueNumber={{PR_NUMBER}}
   - Use `github_issue_attachments` tool with owner=`microsoft`, repo=`PowerToys`, issueNumber={{PR_NUMBER}}, extractFolder=`{{EXTRACT_FOLDER}}`

2. **Read the full discussion** using:
   ```
   gh pr view {{PR_NUMBER}} --repo microsoft/PowerToys --json body,comments,reviews,reviewRequests
   ```

Pay attention to:
- What do reviewers actually think? Read between the lines.
- Does anyone say the fix **doesn't work** or is **broken**?
- Has the author said they'll open a **replacement PR**?
- Is there **disagreement** about the approach?
- Are reviewers asking for **fundamental redesign** vs minor tweaks?
- Are there **images** showing bugs, test results, or UI changes?

### Step 2: Score each dimension

Evaluate these 7 dimensions based on everything you read. Each dimension is independent.

## Evaluation Dimensions

### 1. `review_sentiment` — What do reviewers think?
How positive or negative is the overall reviewer sentiment?
- **1.0** = Enthusiastic approval, "LGTM", "great work"
- **0.7** = Positive, minor nits only
- **0.5** = Mixed — some approve, some have concerns
- **0.3** = Negative — significant objections raised
- **0.0** = Hostile rejection or "this doesn't work at all"
- If no reviews exist, score **0.5** with low confidence.

### 2. `author_responsiveness` — Is the author engaged?
How actively is the author responding to feedback and keeping the PR moving?
- **1.0** = Responding promptly, pushing fixes, actively engaged
- **0.7** = Responding but slowly
- **0.5** = Unclear or no feedback to respond to yet
- **0.3** = Asked to make changes, hasn't responded in a while
- **0.0** = Author has gone silent for weeks, or explicitly abandoned

### 3. `code_health` — Is the code ready?
Based on AI review findings AND human review comments, how healthy is the code?
- **1.0** = Clean — no issues found by AI or humans
- **0.7** = Minor issues only (style, naming, small improvements)
- **0.5** = Moderate concerns — some functional issues but fixable
- **0.3** = Serious problems — bugs, security issues, or design flaws found
- **0.0** = Fundamentally broken — "this doesn't work" confirmed in discussion

### 4. `merge_readiness` — How close to merge?
Considering approvals, CI, discussion, and code health — how merge-ready is this?
- **1.0** = Ready to merge right now — approved, CI green, no objections
- **0.7** = Almost ready — just needs final approval or CI to finish
- **0.5** = Needs some work but on the right track
- **0.3** = Significant work remaining — redesign, major fixes, or blocked
- **0.0** = Not mergeable — should be closed, superseded, or fundamentally reworked

### 5. `activity_level` — How alive is this PR?
How actively is this PR being worked on?
- **1.0** = Active discussion/commits in the last few days
- **0.7** = Some activity in the last 1-2 weeks
- **0.5** = Last activity was 2-4 weeks ago
- **0.3** = Stale — no activity for 1-2 months
- **0.0** = Dead — no activity for 3+ months, likely abandoned

### 6. `direction_clarity` — Is there agreement on approach?
Do reviewers and the author agree on the direction/design of this PR?
- **1.0** = Clear agreement — everyone aligned on approach
- **0.7** = Mostly aligned, minor design suggestions
- **0.5** = Some open questions about approach, not yet resolved
- **0.3** = Significant disagreement — conflicting reviewer opinions
- **0.0** = Fundamental disagreement or no one knows what this should look like

### 7. `superseded` — Has this been replaced?
Is there evidence this PR has been replaced by another PR or is obsolete?
- **1.0** = Explicitly superseded — author or maintainer links to replacement PR
- **0.7** = Author said "I'll open a new PR" or "this approach won't work"
- **0.3** = Hints of replacement but not confirmed
- **0.0** = No evidence of replacement

## Output Format

Respond with ONLY a JSON block (no other text):

```json
{
  "dimensions": {
    "review_sentiment": {
      "score": 0.7,
      "confidence": 0.85,
      "reasoning": "One reviewer approved with minor nits, no objections raised."
    },
    "author_responsiveness": {
      "score": 0.5,
      "confidence": 0.6,
      "reasoning": "No feedback to respond to yet — PR is new with no review comments."
    },
    "code_health": {
      "score": 0.7,
      "confidence": 0.8,
      "reasoning": "AI review found 2 low-severity style issues. No functional problems."
    },
    "merge_readiness": {
      "score": 0.5,
      "confidence": 0.75,
      "reasoning": "Has 1 approval but CI is still running. No blockers in discussion."
    },
    "activity_level": {
      "score": 0.8,
      "confidence": 0.95,
      "reasoning": "Last commit was 3 days ago, author responded to comment yesterday."
    },
    "direction_clarity": {
      "score": 0.9,
      "confidence": 0.8,
      "reasoning": "Reviewers agree this is the right approach, small scope."
    },
    "superseded": {
      "score": 0.0,
      "confidence": 0.95,
      "reasoning": "No mention of replacement PR in discussion."
    }
  },
  "suggested_category": "approved-pending-merge",
  "discussion_summary": "2-3 sentence summary of the key discussion points and any images you saw.",
  "superseded_by": null,
  "tags": ["tag1", "tag2"]
}
```

### Category Reference (for your `suggested_category` field)
| Category | Typical dimension pattern |
|----------|--------------------------|
| `ready-to-merge` | High merge_readiness, high review_sentiment, high code_health |
| `review-concerns` | Low code_health or low review_sentiment due to issues found |
| `approved-pending-merge` | High review_sentiment, moderate merge_readiness (CI pending) |
| `build-failures` | CI failing (from metadata), otherwise healthy |
| `fresh-awaiting-review` | High activity, no reviews yet, young PR |
| `in-active-review` | Active discussion, reviews happening |
| `stale-no-review` | Low activity, no reviews |
| `awaiting-author` | Low author_responsiveness, changes requested |
| `stale-with-feedback` | Low activity, has reviews but no resolution |
| `likely-abandoned` | Very low activity + very low author_responsiveness |
| `direction-unclear` | Low direction_clarity |
| `design-needed` | Low direction_clarity + large scope |
| `needs-attention` | Doesn't fit patterns above |
| `superseded` | High superseded score |

### Critical Rules
- If discussion says "this doesn't work" → `code_health` must be ≤ 0.2 and `merge_readiness` must be ≤ 0.2.
- If author says "I'll open a new PR" → `superseded` must be ≥ 0.7.
- Read images! Screenshots might show the fix doesn't work.

### Valid tags
`quick-win`, `large-pr`, `external-contributor`, `rescue-candidate`, `recommend-close`, `review-clean`, `review-high-severity`, `review-quality`, `review-design`, `review-security`, `has-replacement-pr`, `fix-does-not-work`, `author-unresponsive`.
```
