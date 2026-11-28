---
agent: 'agent'
description: 'Execute the fix for a GitHub issue using the previously generated implementation plan'
---

# Fix GitHub Issue

## Dependencies
Source review prompt (for generating the implementation plan if missing):
- .github/prompts/review-issue.prompt.md

Required plan file (single source of truth):
- Generated Files/issueReview/{{issue_number}}/implementation-plan.md

## Dependency Handling
1) If `implementation-plan.md` exists → proceed.
2) If missing → run the review prompt:
   - Invoke: `.github/prompts/review-issue.prompt.md`
   - Pass: `issue_number={{issue_number}}`
   - Then re-check for `implementation-plan.md`.
3) If still missing → stop and generate:
   - `Generated Files/issueFix/{{issue_number}}/manual-steps.md` containing:
     “implementation-plan.md not found; please run .github/prompts/review-issue.prompt.md for #{{issue_number}}.”

# GOAL
For **#{{issue_number}}**:
- Use implementation-plan.md as the single authority.
- Apply code and test changes directly in the repository.
- Draft the PR body: fill in only the agent sections and leave the human sections for the author (see AGENTS.md "Pull request descriptions (hard rule)").

# OUTPUT FILES
1) Generated Files/issueFix/{{issue_number}}/pr-description.md
2) Generated Files/issueFix/{{issue_number}}/manual-steps.md   # only if human interaction or external setup is required

# EXECUTION RULES
1) Read implementation-plan.md and execute:
   - Layers & Files → edit/create as listed
   - Pattern Choices → follow repository conventions
   - Fundamentals (perf, security, compatibility, accessibility)
   - Logging & Exceptions
   - Telemetry (only if explicitly included in the plan)
   - Risks & Mitigations
   - Tests to Add
2) Locate affected files via `rg` or `git grep`.
3) Add/update tests to enforce the fixed behavior.
4) If any ambiguity exists, add:
// TODO(Human input needed): <clarification needed>
5) Verify locally: build & tests run successfully.

# pr-description.md (PR body draft; the human sections stay empty)
Start from `.github/pull_request_template.md` and keep its headings and comments. Follow AGENTS.md "Pull request descriptions (hard rule)":
1. At the top, add an HTML comment for the author: "Write the Summary and What I verified sections in your own words, tick the Author checklist yourself, and review every change." Include the suggested title in it: `Fix: <short summary> (#{{issue_number}})`.
2. Leave **Summary**, **What I verified**, and the **Author checklist** empty. Do not draft text for them.
3. Fill in the agent sections with facts you verified:
   - Linked issues: `Closes: #{{issue_number}}`, plus `Relates to:` lines for other issues the change really touches
   - PR checklist: tick a box only when the diff or a command proves it, and add a one-line evidence pointer (test result, `.resw` path, doc path)
   - Automated checks: the exact build and test commands you ran, with their results (passed or failed)
4. Put helpful context for the author ONLY inside the collapsed block at the end, as terse bullets:
   ```markdown
   <details>
   <summary>AI-generated notes</summary>

   - Files or modules touched
   - What changed and why the fix works
   - Risks & mitigations (implemented)
   - Telemetry behavior (if applicable)
   - Reproduction steps you used, so the author can verify the fix

   </details>
   ```

# manual-steps.md (only if needed)
- List required human actions: secrets, config, approvals, missing info, or code comments requiring human decisions.

# IMPORTANT
- Apply code and tests directly; do not produce patch files.
- Follow implementation-plan.md as the source of truth.
- Insert comments for human review where a decision or input is required.
- Use repository conventions and deterministic, minimal changes.

# FINALIZE
- Write pr-description.md (agent sections filled with verified facts; human sections empty; longer prose only inside the `AI-generated notes` block)
- Write manual-steps.md only if needed
- Print concise success message or note items requiring human interaction, and remind the user to write the Summary and What I verified sections in their own words and to review every change
