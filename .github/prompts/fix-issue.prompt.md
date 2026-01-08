agent: 'agent'
model: GPT-5.1-Codex-Max
description: "Execute the fix for a GitHub issue using the previously generated implementation plan. Apply code & tests directly in the repo. Output only a PR description (and optional manual steps)."
---

# DEPENDENCY
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
- Produce a PR-ready description.

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

# pr-description.md should include:
- Title: `Fix: <short summary> (#{{issue_number}})`
- What changed and why the fix works
- Files or modules touched
- Risks & mitigations (implemented)
- Tests added/updated and how to run them
- Telemetry behavior (if applicable)
- Validation / reproduction steps
- `Closes #{{issue_number}}`

# manual-steps.md (only if needed)
- List required human actions: secrets, config, approvals, missing info, or code comments requiring human decisions.

# IMPORTANT
- Apply code and tests directly; do not produce patch files.
- Follow implementation-plan.md as the source of truth.
- Insert comments for human review where a decision or input is required.
- Use repository conventions and deterministic, minimal changes.

# FINALIZE
- Write pr-description.md
- Write manual-steps.md only if needed
- Print concise success message or note items requiring human interaction
