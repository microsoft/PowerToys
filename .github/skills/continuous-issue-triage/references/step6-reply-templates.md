# Step 6: Reply Templates

Generate draft replies for issues requiring human response.

## Draft Reply Location

```
Generated Files/triage-issues/current-run/draft-replies/
├── issue-12345.md    # Needs-info draft
├── issue-12346.md    # Clarification draft
├── issue-12347.md    # Close message draft
└── ...
```

## Reply Categories

| Category | Reply Type | Tone | Key Elements |
|----------|------------|------|--------------|
| Needs-Info | Question list | Friendly, helpful | Specific questions, context why needed |
| Needs-Clarification | Explanation | Educational, patient | Answer the question, link to docs |
| Closeable (fixed) | Thank you + reference | Grateful | PR link, version, appreciation |
| Closeable (duplicate) | Redirect | Brief, helpful | Link to original, explain |
| Closeable (by-design) | Explanation | Respectful | Rationale, alternatives |
| Stale-Waiting | Gentle ping | Patient | Reminder, offer to close |

## Template: Needs-Info Reply

**File**: `issue-XXXXX.md`

```markdown
Hi @{{AUTHOR}},

Thank you for reporting this issue! To help us investigate further, could you please provide the following information?

{{#IF_MISSING_REPRO}}
**Reproduction Steps**
- What exact steps lead to this issue?
- Can you provide a minimal, consistent way to reproduce it?
{{/IF_MISSING_REPRO}}

{{#IF_MISSING_VERSION}}
**Environment Details**
- PowerToys version (Settings > General > Version):
- Windows version (winver):
- Did this work in a previous version? If so, which one?
{{/IF_MISSING_VERSION}}

{{#IF_MISSING_EXPECTED}}
**Expected vs Actual Behavior**
- What did you expect to happen?
- What actually happened instead?
{{/IF_MISSING_EXPECTED}}

{{#IF_MISSING_SCREENSHOTS}}
**Visual Evidence** (if applicable)
- Could you attach a screenshot or screen recording showing the issue?
{{/IF_MISSING_SCREENSHOTS}}

{{#IF_MISSING_LOGS}}
**Diagnostic Logs**
- Please run PowerToys and reproduce the issue
- Generate a bug report: Settings > General > "Generate Bug Report"
- Attach the resulting ZIP file
{{/IF_MISSING_LOGS}}

This information will help us reproduce and fix the issue faster. Thanks!
```

## Template: Needs-Clarification Reply

**File**: `issue-XXXXX.md`

```markdown
Hi @{{AUTHOR}},

Thanks for reaching out! Let me help clarify this:

{{EXPLANATION}}

{{#IF_BY_DESIGN}}
This behavior is actually by design. Here's the reasoning:
- {{REASON_1}}
- {{REASON_2}}
{{/IF_BY_DESIGN}}

{{#IF_HOW_TO}}
Here's how you can achieve what you're looking for:
1. {{STEP_1}}
2. {{STEP_2}}
3. {{STEP_3}}
{{/IF_HOW_TO}}

{{#IF_DOCS_LINK}}
You can find more information in our documentation:
- [{{DOC_TITLE}}]({{DOC_LINK}})
{{/IF_DOCS_LINK}}

{{#IF_RELATED_ISSUE}}
There's also an existing discussion about this in #{{RELATED_NUM}} that might be helpful.
{{/IF_RELATED_ISSUE}}

{{#IF_FEATURE_REQUEST}}
If you'd like to request this as a new feature, I'd suggest:
1. Search existing issues to see if it's already requested
2. If not, open a new feature request issue with your use case

We track feature popularity through 👍 reactions, so feel free to upvote any existing requests that match your needs!
{{/IF_FEATURE_REQUEST}}

Let me know if you have any other questions!
```

## Template: Close (Fixed by PR)

**File**: `issue-XXXXX.md`

```markdown
Hi @{{AUTHOR}},

Great news! This issue has been addressed in PR #{{PR_NUM}}.

{{#IF_RELEASED}}
✅ **The fix is now available in PowerToys v{{VERSION}}**

You can update to the latest version through:
- Microsoft Store (automatic updates)
- GitHub Releases: https://github.com/microsoft/PowerToys/releases/tag/v{{VERSION}}
- WinGet: `winget upgrade Microsoft.PowerToys`
{{/IF_RELEASED}}

{{#IF_NOT_RELEASED}}
The fix has been merged and will be included in the next release (v{{NEXT_VERSION}}).

You can track the release progress in our [milestones](https://github.com/microsoft/PowerToys/milestones).
{{/IF_NOT_RELEASED}}

Thank you for reporting this issue and helping improve PowerToys! 🙏

Closing this issue as resolved. If you encounter any further problems, please don't hesitate to open a new issue.
```

## Template: Close (Duplicate)

**File**: `issue-XXXXX.md`

```markdown
Hi @{{AUTHOR}},

Thanks for reporting this! It looks like this issue is a duplicate of #{{ORIGINAL_NUM}}.

To avoid splitting the discussion, I'm closing this in favor of the original issue. Please:
- 👍 React to #{{ORIGINAL_NUM}} to show your interest
- Add any additional context or reproduction details as a comment there
- Subscribe to #{{ORIGINAL_NUM}} for updates

{{#IF_DIFFERENT_CONTEXT}}
I noticed your report includes some additional context that might be helpful. I'll add a comment to #{{ORIGINAL_NUM}} referencing this issue.
{{/IF_DIFFERENT_CONTEXT}}

Thank you for understanding!
```

## Template: Close (By Design / Won't Fix)

**File**: `issue-XXXXX.md`

```markdown
Hi @{{AUTHOR}},

Thank you for taking the time to report this and share your feedback.

After reviewing this issue, we've determined that this behavior is **{{RESOLUTION_TYPE}}**.

{{#IF_BY_DESIGN}}
### Why This Is By Design

{{RATIONALE}}

This design choice was made because:
- {{REASON_1}}
- {{REASON_2}}
{{/IF_BY_DESIGN}}

{{#IF_WONT_FIX}}
### Why We're Not Addressing This

{{RATIONALE}}

We've decided not to implement this change because:
- {{REASON_1}}
- {{REASON_2}}
{{/IF_WONT_FIX}}

{{#IF_WORKAROUND}}
### Workaround

In the meantime, you might try:
{{WORKAROUND}}
{{/IF_WORKAROUND}}

{{#IF_ALTERNATIVE}}
### Alternative Approaches

You might consider:
- {{ALTERNATIVE_1}}
- {{ALTERNATIVE_2}}
{{/IF_ALTERNATIVE}}

We appreciate your understanding. If you have additional context that might change our assessment, please let us know!
```

## Template: Stale-Waiting Ping

**File**: `issue-XXXXX.md`

```markdown
Hi @{{AUTHOR}},

We haven't heard back from you in a while. Are you still experiencing this issue?

{{#IF_WAITING_FOR_INFO}}
We're still waiting for the additional information requested above to help investigate this issue.
{{/IF_WAITING_FOR_INFO}}

{{#IF_WAITING_FOR_CONFIRMATION}}
Could you confirm if the suggested solution worked for you?
{{/IF_WAITING_FOR_CONFIRMATION}}

If we don't hear back within the next {{DAYS}} days, we'll close this issue to keep our backlog manageable. You're always welcome to reopen it or create a new issue if the problem persists.

Thanks for your understanding! 🙏
```

## Template: Closed Issue with New Comment

**File**: `issue-XXXXX.md`

```markdown
Hi @{{COMMENTER}},

Thanks for your comment! This issue was closed {{TIME_AGO}} because {{CLOSE_REASON}}.

{{#IF_SAME_ISSUE}}
If you're experiencing the same issue and it's not resolved, please open a new issue with:
- Your PowerToys version
- Steps to reproduce
- Any error messages or screenshots

This helps us track and prioritize effectively.
{{/IF_SAME_ISSUE}}

{{#IF_QUESTION}}
Regarding your question:
{{ANSWER}}
{{/IF_QUESTION}}

{{#IF_DIFFERENT_ISSUE}}
It sounds like you might be experiencing a different issue. Please open a new issue with details about your specific problem so we can help you better.
{{/IF_DIFFERENT_ISSUE}}
```

## Draft Generation Logic

```powershell
function New-DraftReply {
    param(
        [hashtable]$Issue,
        [string]$Category,
        [hashtable]$AnalysisData
    )
    
    $draftPath = "Generated Files/triage-issues/current-run/draft-replies/issue-$($Issue.number).md"
    
    switch ($Category) {
        "needs-info" {
            $content = New-NeedsInfoDraft -Issue $Issue -Missing $AnalysisData.missingItems
        }
        "needs-clarification" {
            $content = New-ClarificationDraft -Issue $Issue -QuestionType $AnalysisData.questionType
        }
        "closeable" {
            $content = New-CloseDraft -Issue $Issue -CloseReason $AnalysisData.closeReason
        }
        "stale-waiting" {
            $content = New-StalePingDraft -Issue $Issue -DaysWaiting $AnalysisData.daysWaiting
        }
        default {
            return $null  # No draft needed
        }
    }
    
    # Add metadata header
    $header = @"
---
issue: $($Issue.number)
title: $($Issue.title)
category: $Category
generated: $(Get-Date -Format "o")
status: draft
---

"@
    
    ($header + $content) | Set-Content $draftPath
    return $draftPath
}
```

## Draft Review Checklist

Before posting any draft:

- [ ] Read the full issue context
- [ ] Check for recent comments not in analysis
- [ ] Personalize if needed (remove boilerplate feel)
- [ ] Verify links work
- [ ] Ensure tone is appropriate
- [ ] Remove any placeholder text (`{{...}}`)

## Posting Drafts

```bash
# Post a single draft
gh issue comment 12345 --body-file "Generated Files/triage-issues/current-run/draft-replies/issue-12345.md"

# Add label if needed
gh issue edit 12345 --add-label "Needs-Author-Feedback"

# Close with message
gh issue close 12345 --comment "$(cat draft-replies/issue-12345.md)"
```

## Best Practices

1. **Never auto-post**: Always human review before posting
2. **Be empathetic**: Remember there's a person on the other side
3. **Be specific**: Generic responses feel dismissive
4. **Provide value**: Every reply should move the issue forward
5. **Link resources**: Documentation, related issues, PRs
6. **Thank contributors**: Acknowledge their time and effort
