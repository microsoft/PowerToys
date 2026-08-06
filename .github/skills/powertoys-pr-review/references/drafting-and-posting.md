# Drafting & Posting (Steps 9–10)

Everything here is **drafted only** and posted only after Step 10 approval.

## Step 9: Draft the review (context + code)

The drafted review combines the **Phase 0 context/process findings** and the **code findings** into the set of actions a maintainer would take. Depending on Phase 0's disposition (0e), the review may be primarily a request for missing context (demo/CLA/validation), a close/redirect (duplicate/superseded/compound-quality), a code-suggestion set, or a combination.

> **Gate — do not draft until the fork loop has converged.** Confirm the Step 6 *Definition of done* holds: the last freshly-requested review had **0** new comments and there are **0** unresolved Copilot threads. If not, finish the loop first (Critical Rule 11). Suggestions must reflect the *converged* net diff, not round-1 output.

**9a. Draft the context actions (from Phase 0), community PRs only.** Use the per-gate reply templates in [phase0-context-review.md](./phase0-context-review.md#reply-templates-per-gate). Batch multiple asks into one comment, lead with thanks, and only ask for things the author actually owes (0b). For members/collaborators/bots, skip 9a.

**9b. Draft the code suggestion comments.** These will be posted publicly on the original PR and seen by the author, maintainers, and community. They must be clear, professional, and educational.

Quality guidelines:
- Write as a senior reviewer helping a contributor improve their PR.
- Explain the *reasoning*, not just what to change.
- Mention the observable symptom or user-facing impact (e.g., "users would see '1 minutes ago', which is grammatically incorrect").
- If the fix removes code, explain why it is safe. If it is structural, briefly explain the design choice.
- Keep a respectful, collaborative tone.
- Keep public comments self-contained. Never mention a fork repository, fork PR, worktree, internal review loop, or private validation provenance.

### Format for each suggestion comment

````markdown
### <Concise title of the issue>

**Severity:** `low` | `medium` | `high` | `critical`

<2-4 sentences: (1) what the problem is, with user-facing symptom if applicable;
(2) why it happens (root cause); (3) what the fix does.>

```suggestion
<corrected code lines>
```

<Optional: 1 sentence noting related changes needed in other files.>
````

**Severity definitions:**
- **`critical`** — crash, data loss, security vulnerability, or broken core functionality.
- **`high`** — a bug users will likely encounter, a significant logic error, or a resource leak.
- **`medium`** — a correctness issue in edge cases, missing validation, or poor error handling.
- **`low`** — style, naming, minor optimization, or defensive coding.

### Good example

````markdown
### Singular/plural forms for relative time labels

**Severity:** `medium`

When the count is exactly 1, the current code produces grammatically incorrect
output like "1 minutes ago" because the format strings are unconditionally plural.
This adds a branch for the singular case, using dedicated resource strings that
avoid the `{0}` placeholder entirely.

```suggestion
<code>
```

> Note: this suggestion requires matching singular resource strings in the resource
> files — see companion suggestions on those files.
````

A one-line "Add singular form." with a bare suggestion block is **too terse** — always explain the why.

### Multi-suggestion line-shift safety

When posting **multiple suggestions on the same file**, applying them one at a time shifts line numbers, so a later suggestion can target stale lines and produce broken code. To prevent this:

1. **Prefer a single combined suggestion** when fixes are within ~30 lines of each other. Use `start_line`/`line` to cover the full range in one block.
2. **If fixes must be separate:** post suggestions targeting **later lines first**, and add an application note: `> If applying multiple suggestions individually, use "Commit suggestion" from bottom to top, or batch them with "Add suggestion to batch" then "Commit suggestions" to apply atomically.`
3. **When suggestions add/remove lines**, include enough surrounding context (the full syntactic block) so partial application cannot leave orphaned braces or dangling clauses.
4. **Self-contained test:** mentally apply each suggestion independently on the original code; if any would produce invalid syntax alone, expand it to include the needed context.

Each suggestion must use the exact ` ```suggestion ` format, reference the correct file/line range, be self-contained so the author can click "Commit suggestion", and cross-reference related suggestions in other files. Classify every public item as:

- **`inline`** — one apply-ready suggestion targeting the current RIGHT-side diff.
- **`companion`** — readable review-body guidance for architectural, multi-file, or out-of-diff work that cannot honestly use an apply button.
- **obsolete** — omit it from `publicPayload`; keep any audit note under `internalEvidence`.

Store author-facing text only in `publicPayload`. Store fork links, worktrees, build evidence, internal thread URLs, and convergence notes only in `internalEvidence`. Never concatenate internal evidence into a public body.

Before presenting the dashboard, run:

```powershell
./scripts/Test-ReviewData.ps1 -DataPath <path>\review-data.json -AllowIncomplete
```

Before marking a PR `ready`, run with `-CheckGitHub` so every pinned head and inline range is current.

## Step 10: Wait for approval — MANDATORY STOP

**You must stop here and wait. Do not proceed without explicit user approval.**

1. Present all drafted actions to the user. Show:
   - **Author type & the resulting bar** (full Phase 0 for community, skipped for members/bots).
   - **Phase 0 verdict** — which gates (P1–P9) tripped, the disposition (proceed / ask / align-first / recommend-close), and who owes the next action.
   - **Drafted context messages** (batched asks, redirect/close text) for community PRs.
   - Summary of code changes made and number of review iterations.
   - List of all suggested code fixes with **severity label** and file/line references.
   - The worktree path and **end-to-end testing instructions** (Step 7b) so the user can verify.
   - A **multi-suggestion warning** if there are 2+ suggestions on the same file.

   For a **single PR**, presenting this inline as text is fine. For a **multi-PR session** (2+ PRs drafted), prefer the **approval dashboard** — build a schema-version-2 `review-data.json` and launch [scripts/Show-ReviewDashboard.ps1](../scripts/Show-ReviewDashboard.ps1). The dashboard validates ready payloads, renders public items, and binds decisions to the exact data hash. Full flow and schemas: [approval-dashboard.md](./approval-dashboard.md).
2. **Stop. Do not execute any further commands against the original repo.** (When using the dashboard, launch it, tell the user the URL, and end the turn.)
3. **Wait for the user to explicitly say** "post it", "go ahead", "submit", or "approve these comments" — or, with the dashboard, to submit their decisions and type the resume phrase `pr-review: actions ready`. Then validate `review-decisions.json` and act on each PR per its `action`, selected `items`, and `instructions`.
   - "Looks good" without an explicit post instruction → ask "Shall I post this review to the original PR now?"
   - If the user asks for changes, revise and present again.
   - "Skip" / "don't post" → post nothing.
4. **Re-check the PR for new activity before posting (MANDATORY).** Between drafting and approval the PR may have moved. Re-fetch and compare against what you reviewed:
   ```powershell
   gh pr view N --repo microsoft/PowerToys --json headRefOid,commits,updatedAt
   gh api repos/microsoft/PowerToys/pulls/N/comments   # review threads
   gh api repos/microsoft/PowerToys/issues/N/comments   # conversation
   ```
   Decide by what changed:
   - **No new commits and no new threads** → post as approved.
   - **New commits (small / localized)** → re-verify each drafted suggestion against the new code (keep the old version as reference). Update the `line`/`start_line` and suggestion block where lines moved; drop any finding the author already fixed; reword where the change partially addresses a point.
   - **New commits (large / overlapping the reviewed areas)** → the reviewed diff is stale. Re-run the loop (Steps 4–7) against the new head — you can still reuse still-valid findings and the previous fork branch for reference — then re-present.
   - **New comments or review threads (no code change)** → check overlap with your drafts; drop or reword to avoid duplicating or contradicting a maintainer/author, and fold in anything that changes the disposition.
   - If anything material changed, tell the user what moved and re-confirm before posting — never silently post stale comments.
5. **Only after explicit approval AND passing the freshness re-check**, publish through the bundled safe publisher:
   ```powershell
   ./scripts/Test-ReviewData.ps1 `
     -DataPath <path>\review-data.json `
     -DecisionsPath <path>\review-decisions.json `
     -CheckGitHub

   ./scripts/Publish-ApprovedReview.ps1 `
     -DataPath <path>\review-data.json `
     -DecisionsPath <path>\review-decisions.json
   ```

   The publisher:
   - rejects stale heads, new activity, invalid diff ranges, placeholders, serialization artifacts, and any public fork/internal reference;
   - creates a **pending** review containing the approved context, companion notes, and inline suggestions;
   - reads every rendered body and target back from GitHub;
   - deletes the pending review on any mismatch;
   - submits only after exact verification; and
   - records an idempotency ledger so an interrupted run resumes without duplicate comments.

   `close` and `custom` remain explicit manual maintainer actions. The safe publisher intentionally handles only `comment`, `request-changes`, and `hold`.

**Never auto-post, auto-label, or auto-close on the original PR.** The user may want to edit the review, skip some actions, or decide not to act at all. This decision is always theirs. Closing in particular is a maintainer action — only on explicit instruction.
