---
agent: 'agent'
description: 'Generate a PowerToys-ready pull request description from the local diff'
---

# Generate PR Summary

**Goal:** Produce a ready-to-paste PR title and description that lets reviewers immediately understand what changed and the new expected behavior, using the current branch's diff against a user-selected target branch.

**Repo guardrails:**
- Load [the PR template](../pull_request_template.md) at runtime as the single source of truth. Preserve every heading, comment, checklist item, and their order; change checkbox states only when supported by evidence. Leave inapplicable items unchecked and explain them briefly as N/A without removing them.
- Use [the existing PR title convention](create-commit-title.prompt.md#pr-title-convention-when-asked), naming the affected area and concrete observable change rather than the work performed.
- Cite review-critical paths with inline backticks, as required by `AGENTS.md`; do not inventory every changed file.

**Reviewer-first content:**
- Open with the concrete problem and new expected behavior. For documentation or tooling changes, describe what contributors will do differently.
- Include only details needed to review behavior, contracts, risks, or non-obvious tradeoffs. Explain a decision only when its rationale helps assess the current change; omit investigation history, rejected attempts, and implementation diaries.
- Put each fact in its most useful template section once. Do not restate the diff file by file or repeat the summary in the detailed description.
- Summarize validation as the behavior or scope checked, its result, and material failures or gaps. State when checks were not run or evidence is unavailable; never infer success. Omit raw logs, workstation paths, and build/test command dumps. If the caller or template requires exact commands, retain them in collapsed details or linked evidence.
- For multi-layer or cross-boundary changes, include an architecture diagram in the detailed description showing affected components, boundaries, flow, and the changed path. Use source-backed names and distinguish changed from unchanged behavior. Multiple files alone do not imply multiple layers.
- For UX changes, include relevant before/after screenshots or a short video showing the changed interaction. Use actual captured media, not a sketch presented as proof. Explicitly flag missing required diagrams or media as outstanding evidence; do not claim the PR is review-ready without it. Do not add irrelevant visuals for documentation-only or single-layer, non-UX changes.

**Opening example:**
- Avoid: "Updated three files after investigating several approaches; build commands are listed below."
- Prefer: "FancyZones now keeps the selected layout after a monitor reconnects, instead of resetting it to the default."

**Workflow:**
1. Determine the target branch from user context; default to `main` when no branch is supplied.
2. Run `git status --short` once to surface uncommitted files that may influence the summary.
3. Run `git diff <target-branch>...HEAD` a single time to review the detailed changes. Only when confidence stays low dig deeper with focused calls such as `git diff <target-branch>...HEAD -- <path>`.
4. Identify the problem, expected behavior, and review-critical risks or contracts. Inspect source as needed to ground explanations and any required diagram.
5. Confirm validation results and required visual evidence from available records or the user. Mark unknowns explicitly rather than inventing tests, results, or media.
6. Populate the full runtime template using the reviewer-first content rules above.
7. Skim as a reviewer: can the title and opening answer what changed and what to expect? Does each remaining sentence help review the code or assess the evidence? Remove repetition and process narration; verify template fidelity and required visuals.
8. Present the PR title above the filled template, with the body inside a fenced Markdown code block and no extra commentary. Clearly flag unresolved placeholders and evidence gaps within the body.
