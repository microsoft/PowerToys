---
name: powertoys-pr-review
description: End-to-end PR review for the microsoft/PowerToys repository, usable by any teammate on any PR. Runs a CONTEXT/PROCESS review (things Copilot code review will not catch: CLA, demo/recording for user-visible changes, author self-validation, provenance, duplicates, scope, CI/spell-check, template) — focused on community-contributor PRs — in parallel with a CODE review (mirror to a personal fork, iterate GitHub Copilot review, fix, build locally). Drafts review actions grounded in real PowerToys review conventions, then stops for explicit approval before anything is posted. Use when asked to review a PR, continue a review, self-check your own PR, or draft suggested-fix comments for a PowerToys pull request.
license: Complete terms in LICENSE.txt
---

# PowerToys PR Review

Review a `microsoft/PowerToys` pull request the way a maintainer would, given a PR number `N`. The review has **two halves that run in parallel**:

1. **Context & process review (Phase 0)** — the checks Copilot code review can never do: is the CLA signed, is there a demo for a user-visible change, did the author validate it, is it original work, a duplicate, in-scope, CI-green, and template-complete? Aimed at **community-contributor** PRs and often the deciding factor. See [references/phase0-context-review.md](./references/phase0-context-review.md).
2. **Code review (Steps 0–10)** — mirror the PR to the reviewer's personal fork, iterate GitHub Copilot review to convergence, fix valid issues, build locally, and draft suggested-fix comments.

**Never post anything to the original PR without explicit user approval (Step 10).**

## When to Use This Skill

- "Review PR N" / "review this PowerToys PR" / "continue the review for PR N"
- Self-check your own PowerToys PR before a maintainer sees it
- Draft inline code suggestions (click-to-commit) with severity for a PR
- Decide the process disposition (proceed / ask / align / recommend-close) for a community PR

## Review Modes

| Mode | Trigger | What changes |
| --- | --- | --- |
| **Standard review** | "review PR N" | Phase 0 (context) **and** Steps 0–10 (code) in parallel. |
| **Self-check** | The PR author is the current user (own fork owner) | **Skip fork-mirroring** (Steps 1–2c) — you already own the branch; review/build in the author's own worktree. Run Phase 0 on yourself to pre-empt what maintainers will demand. |

## Prerequisites (verify on first run)

Run [scripts/Test-Prerequisites.ps1](./scripts/Test-Prerequisites.ps1) to check all four at once. Detail and setup guidance: [references/prerequisites.md](./references/prerequisites.md).

| # | Prerequisite | Quick check |
| --- | --- | --- |
| 1 | Personal fork of `microsoft/PowerToys` | `gh repo list --fork` includes `<owner>/PowerToys` |
| 2 | Local clone with a remote pointing to the fork | a `PowerToys` clone with a non-`microsoft` remote |
| 3 | GitHub Copilot code review enabled on the fork | first review request returns a non-empty `requested_reviewers` |
| 4 | Visual Studio 2022 (Desktop C++ + .NET desktop) | `vswhere.exe -latest` resolves an install |

If a prerequisite is missing, guide the user through setup ([references/prerequisites.md](./references/prerequisites.md)) before proceeding. Prerequisite 3 must be enabled by the user in the fork's settings; for 4, offer to install the tools via winget.

## Critical Rules

1. **Never post comments, suggestions, reviews, approvals, labels, or close actions on the original PR** until the user gives **explicit written approval** in Step 10. This includes `gh pr review`, `gh pr close`, `gh pr edit --add-label`, or any command targeting `microsoft/PowerToys`. Violating this is a critical failure.
2. **Never approve the original PR** — even after user approval. Draft suggestions/requests/dispositions only; the human makes the final call.
3. **Run Phase 0 in parallel with the code review** for community PRs. A perfectly-coded PR can still need a demo, CLA, validation, or a close. Context findings are first-class output.
4. **Respect author type, and do not over-gate.** Skip Phase 0 process nags for members/collaborators/bots; hold community contributors to the gates but stay welcoming. Most well-formed PRs trip *no* gate — do not invent friction.
5. **Never use `#<number>` format** in any fork PR title or description — it notifies the referenced upstream thread. Use plain numbers.
6. **Skip fork-mirroring in self-check mode** (author is the fork owner): review and build in the author's own worktree; Steps 1–2c do not apply.
7. All code work happens on the fork and locally until final approval.
8. The local worktree must build successfully before a deep code review is marked complete.
9. **Always sync the fork's main before creating or updating the fork PR** to prevent diff bloat from a stale base (Step 2c).
10. **Resume, do not restart (Step 0).** Before mirroring, check for a prior interrupted run on PR `N` and pick up from the matching step instead of re-mirroring. Re-mirroring clobbers prior commits and review replies.
11. **The fork Copilot loop (Steps 4–8) is not optional and is independent of the posting decision.** "Just show me" / "do not post" changes only **Step 10**. You must still drive the fork loop to convergence: fix valid issues, commit, push, reply-and-resolve every Copilot thread, and re-request review until a freshly-requested review returns **zero** new comments and there are **zero** unresolved Copilot threads. Final suggestions come from the *converged* net diff, never raw round-1 output.
12. **Never leave a fork PR stranded at round 1 — especially in a batch.** Drive each PR to convergence before starting the next, or set a `manage_schedule` monitor per PR to keep the loop going across turns. A half-finished loop is not "done".

## Phase 0: Context & Process Review

The half Copilot code review cannot do. It reads the PR description, the full conversation timeline, the CI/checks status, and the linked issue, then decides whether the PR clears PowerToys' *process* bar. Everything Phase 0 produces is **drafted only**. Full rules, the P1–P9 gate table, the compound-quality close signal, and per-gate reply templates are in **[references/phase0-context-review.md](./references/phase0-context-review.md)**.

Disposition outcomes (Phase 0 → one of):

- **Proceed** — no gate tripped (or only Notes). Go to the code-review output.
- **Ask** — draft the specific request(s); review pauses on the author.
- **Align first** — duplicate/superseded/out-of-scope: draft a redirect to the canonical PR/issue.
- **Recommend close** — compound-quality or long-stale-with-author-owing: draft a polite close.

Always recommend, never auto-apply — labels/closes are maintainer actions gated by Step 10.

## Code Review Workflow

These steps are the code-review engine. In **self-check mode** skip Steps 1–2c.

| Step | Action | Reference |
| --- | --- | --- |
| 0 | **Resume check** — detect a prior interrupted run on PR `N` and jump to the right step | [setup-and-mirror.md](./references/setup-and-mirror.md#step-0-resume-check) |
| 1 | **Mirror** the PR to the personal fork (sanitize `#refs`) | [setup-and-mirror.md](./references/setup-and-mirror.md#step-1-mirror-the-pr) |
| 2 / 2b / 2c | **Worktree**, rebase on latest `main`, validate the fork diff matches the original scope | [setup-and-mirror.md](./references/setup-and-mirror.md#step-2-create-local-worktree) |
| 3 | **Build locally (Debug)** so `x64/Debug/PowerToys.exe` runs the change | [build-and-test.md](./references/build-and-test.md) |
| 4 | **Request Copilot review** on the fork PR | [copilot-review-loop.md](./references/copilot-review-loop.md#step-4-request-copilot-review) |
| 5–6 | **The loop** — fix, push, reply-and-resolve, re-request until convergence | [copilot-review-loop.md](./references/copilot-review-loop.md#step-6-iterate-the-loop) |
| 7 / 7b | **Final build** of the full module chain + end-to-end test instructions | [build-and-test.md](./references/build-and-test.md#step-7-final-build) |
| 8 | **Summarize** the converged net diff | [copilot-review-loop.md](./references/copilot-review-loop.md#step-8-summarize) |
| 9 | **Draft the review** — context asks + code suggestions with severity | [drafting-and-posting.md](./references/drafting-and-posting.md#step-9-draft-the-review) |
| 10 | **Wait for approval**, freshness re-check, then post the approved actions | [drafting-and-posting.md](./references/drafting-and-posting.md#step-10-wait-for-approval) |

Convergence (Definition of done for Steps 5–6): the most recent freshly-requested Copilot review returned **zero** new inline comments, **zero** unresolved Copilot threads remain, and the worktree builds. Do not draft Step 9 suggestions until this holds.

## Fork Configuration

Auto-detect these at the start of each session with [scripts/Get-ForkConfig.ps1](./scripts/Get-ForkConfig.ps1); verify on first run. Nothing is tied to a specific account — `gh api user` resolves the current teammate.

| Placeholder | Meaning | Example |
| --- | --- | --- |
| `<FORK_OWNER>` | The reviewer's GitHub login | `your-username` |
| `<FORK_REPO>` | The reviewer's fork | `your-username/PowerToys` |
| `<FORK_REMOTE>` | Git remote pointing at the fork (default `fork`) | `fork` |
| `<CLONE_PATH>` | Main PowerToys clone directory | `C:\PowerToys` |

## Available Scripts

| Script | Purpose |
| --- | --- |
| [Test-Prerequisites.ps1](./scripts/Test-Prerequisites.ps1) | Check fork, clone, Copilot review, and VS build tools |
| [Get-ForkConfig.ps1](./scripts/Get-ForkConfig.ps1) | Resolve fork owner/repo/remote and clone path |
| [Get-PRContext.ps1](./scripts/Get-PRContext.ps1) | Fetch author, association, size, and labels to calibrate Phase 0 |
| [Request-CopilotReview.ps1](./scripts/Request-CopilotReview.ps1) | Request Copilot as reviewer and poll until the review posts |
| [Get-UnresolvedCopilotThreads.ps1](./scripts/Get-UnresolvedCopilotThreads.ps1) | Count unresolved Copilot threads (stranded-loop / resume check) |
| [Sync-ForkMain.ps1](./scripts/Sync-ForkMain.ps1) | Fast-forward the clone's `main` from upstream and push it to the fork |

## References

- [phase0-context-review.md](./references/phase0-context-review.md) — the context/process gates, dispositions, and reply templates
- [prerequisites.md](./references/prerequisites.md) — fork, clone, Copilot review, and VS build-tool setup
- [setup-and-mirror.md](./references/setup-and-mirror.md) — Steps 0–2c: resume, mirror, worktree, rebase, diff validation
- [copilot-review-loop.md](./references/copilot-review-loop.md) — Steps 4–8: request, fix/push/resolve loop, summarize
- [build-and-test.md](./references/build-and-test.md) — Steps 3, 7, 7b: local build, module chain, end-to-end tests
- [drafting-and-posting.md](./references/drafting-and-posting.md) — Steps 9–10: suggestion format, freshness re-check, posting
