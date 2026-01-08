agent: 'agent'
model: GPT-5.1-Codex-Max
description: 'Generate a PowerToys-ready pull request description from the local diff.'
---

**Goal:** Produce a ready-to-paste PR title and description that follows PowerToys conventions by comparing the current branch against a user-selected target branch.

**Repo guardrails:**
- Treat `.github/pull_request_template.md` as the single source of truth; load it at runtime instead of embedding hardcoded content in this prompt.
- Preserve section order from the template but only surface checklist lines that are relevant for the detected changes, filling them with `[x]`/`[ ]` as appropriate.
- Cite touched paths with inline backticks, matching the guidance in `.github/copilot-instructions.md`.
- Call out test coverage explicitly: list automated tests run (unit/UI) or state why they are not applicable.

**Workflow:**
1. Determine the target branch from user context; default to `main` when no branch is supplied.
2. Run `git status --short` once to surface uncommitted files that may influence the summary.
3. Run `git diff <target-branch>...HEAD` a single time to review the detailed changes. Only when confidence stays low dig deeper with focused calls such as `git diff <target-branch>...HEAD -- <path>`.
4. From the diff, capture impacted areas, key file changes, behavioral risks, migrations, and noteworthy edge cases.
5. Confirm validation: list tests executed with results or state why tests were skipped in line with repo guidance.
6. Load `.github/pull_request_template.md`, mirror its section order, and populate it with the gathered facts. Include only relevant checklist entries, marking them `[x]/[ ]` and noting any intentional omissions as "N/A".
7. Present the filled template inside a fenced ```markdown code block with no extra commentary so it is ready to paste into a PR, clearly flagging any placeholders that still need user input.
