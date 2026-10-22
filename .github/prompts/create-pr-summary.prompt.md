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

**Inputs:**
- Use the target branch from user context, defaulting to `main`.
- Inspect `git status --short` and `git diff <target-branch>...HEAD`; read source or validation records as needed.

**Output:** Present the title above the filled template, with the body inside a fenced Markdown code block and no extra commentary. Flag unresolved placeholders within the body.
