---
description: 'Fix active pull request comments with scoped changes'
name: 'fix-pr-active-comments'
agent: 'agent'
argument-hint: 'PR number or active PR URL'
---

# Fix Active PR Comments

## Mission
Resolve active pull request comments by applying only simple fixes. For complex refactors, write a plan instead of changing code.

## Scope & Preconditions
- You must have an active pull request context or a provided PR number.
- Only implement simple changes. Do not implement large refactors.
- If required context is missing, request it and stop.

## Inputs
- Required: ${input:pr_number:PR number or URL}
- Optional: ${input:comment_scope:files or areas to focus on}
- Optional: ${input:fixing_guidelines:additional fixing guidelines from the user}

## Workflow
1. Locate all active (unresolved) PR review comments for the given PR.
2. For each comment, classify the change scope:
   - Simple change: limited edits, localized fix, low risk, no broad redesign.
   - Large refactor: multi-file redesign, architecture change, or risky behavior change.
3. For each large refactor request:
   - Do not modify code.
   - Write a planning document to Generated Files/prReview/${input:pr_number}/fixPlan/.
4. For each simple change request:
   - Implement the fix with minimal edits.
   - Run quick checks if needed.
   - Commit and push the change.
5. For comments that seem invalid, unclear, or not applicable (even if simple):
   - Do not change code.
   - Add the item to a summary table in Generated Files/prReview/${input:pr_number}/fixPlan/overview.md.
   - Consult back to the end user in a friendly, polite tone.
6. Respond to each comment that you fixed:
   - Reply in the active conversation.
   - Use a polite or friendly tone.
   - Keep the response under 200 words.
   - Resolve the comment after replying.

## Output Expectations
- Simple fixes: code changes committed and pushed.
- Large refactors: a plan file saved to Generated Files/prReview/${input:pr_number}/fixPlan/.
- Invalid or unclear comments: captured in Generated Files/prReview/${input:pr_number}/fixPlan/overview.md.
- Each fixed comment has a reply under 200 words and is resolved.

## Plan File Template
Use this template for each large refactor item:

# Fix Plan: <short title>

## Context
- Comment link:
- Impacted areas:

## Overview Table Template
Use this table in Generated Files/prReview/${input:pr_number}/fixPlan/overview.md:

| Comment link | Summary | Reason not applied | Suggested follow-up |
| --- | --- | --- | --- |
|  |  |  |  |

## Quality Assurance
- Verify plan file path exists.
- Ensure no code changes were made for large refactor items.
- Confirm replies are under 200 words and comments are resolved.
