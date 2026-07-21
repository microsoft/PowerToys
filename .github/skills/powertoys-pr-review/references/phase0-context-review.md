# Phase 0: Context & Process Review

The half Copilot code review cannot do. It reads the **PR description, the full conversation timeline, the CI/checks status (build + spell-check), and the linked issue** and checks whether the PR clears PowerToys' *process* bar. Run it in parallel with the code review (Steps 0–10). Everything Phase 0 produces is **drafted only** — nothing is posted before Step 10.

## 0a. Who is this for? (author type gates the whole phase)

Phase 0 is calibrated for **community contributors**. Detect the author type first — the list endpoint does not expose it for pulls, so use the pulls API. Run [scripts/Get-PRContext.ps1](../scripts/Get-PRContext.ps1) or:

```powershell
gh api repos/microsoft/PowerToys/pulls/N --jq '{author: .user.login, assoc: .author_association, draft: .draft, additions, deletions, changedFiles: .changed_files}'
```

| `author_association` | Treatment |
| --- | --- |
| `MEMBER`, `COLLABORATOR`, `OWNER` | **Skip Phase 0 process nags.** They know the conventions; only surface a genuine blocker (e.g., CLA edge case, obviously missing demo on a big UX change). Never lecture a maintainer about the template. |
| `CONTRIBUTOR`, `FIRST_TIME_CONTRIBUTOR`, `NONE` | **Full Phase 0.** Be strict but welcoming — thank first-time contributors before asking for anything. |
| Author is the fork owner (self-check) | Run Phase 0 on yourself so you fix these before a maintainer has to ask. |
| Author is `Copilot` / a bot | Out of scope for these rules — do not apply Phase 0 process gates. |

> **Calibration (from ~280 sampled community PRs):** most well-formed PRs trigger *nothing* here. Of merged community PRs, ~83% had a linked issue and merged with **zero** process asks. Do not invent friction — only draft an action when a gate below is genuinely tripped.

## 0b. Read the conversation before writing anything

The most common review mistake is re-asking for something already provided. Before drafting any action:

1. **Read the whole timeline top to bottom.** Note every maintainer request and whether the author answered it.
2. **Determine who owes the next action.** Only draft an author-facing ask if *the author* owes the next move. If a maintainer owes a reply, say so and stop — do not nag the author.
3. **Follow the linked issue as context, not a gate.** Open the referenced issue; if it was closed *as a duplicate*, follow to the canonical issue. A missing/closed issue is **context**, never an automatic blocker.
4. **Do not re-litigate design in the abstract.** Product-fit/UX disagreements are in-review discussions — raise them as a Note and keep reviewing; they do not gate the code review.

## 0c. The gates

Each gate has a **severity** and a backing evidence set (real PRs). Severities:

- **Blocker** — do not invest in a deep code review yet / cannot merge until resolved. Draft the ask and recommend pausing.
- **Ask** — request the missing thing from the author; forward progress pauses on them, but it is not a rejection.
- **Note** — surface for discussion; does not gate anything.

| ID | Gate | Trips when | Severity | Evidence |
| --- | --- | --- | --- | --- |
| **P1** | **CLA signed** | The `license/cla` check is failing / the policy bot is unsatisfied. | **Blocker** (merge) | 48810, 48809, 48659 |
| **P2** | **Demo for user-visible change** | The PR changes UI/UX/observable behavior and neither the description nor the thread has a screenshot or recording. **Skip for pure internal/bug-fix changes** whose effect is captured by a linked issue + tests. | **Judgement call** — decide as the maintainer/PM would: if a short demo would genuinely help understand or trust a user-visible change, request it (with approval); otherwise let it go. Not a hard block. | 49027, 48603, 48958, 46803, 48660, 48647, 47235, 46747, 48810 |
| **P3** | **Author self-validation** | **Conservative — do not fire by default.** Only when the PR *already* looks likely AI-generated (see P5) **or** the CI build is largely failing in a way that suggests the author never built it locally (genuine build breaks, *not* an out-of-date branch). For an ordinary contribution, do **not** ask the author to prove they ran it just because they did not say so — that reads as rude and accusatory. | **Ask** (only when triggered as above) | 48957, 48603, 48650 |
| **P4** | **CI green (build + spell-check)** | Required checks are red. For **spell-check (`check-spelling`)**, the fix is usually to add legitimate words to `.github/actions/spell-check/allow/*.txt` (e.g. `code.txt`); for build failures the author must fix them. **Repo-specific:** the Azure Pipelines build does **not** auto-run on external-fork PRs — it stays pending until an authorized maintainer comments `/azp run`. So a *missing* build result is not a failure; do not read it as one (if you are a maintainer, comment `/azp run` to trigger it). | **Ask** | 46684, 48810, 49027, 49356 |
| **P5** | **Provenance / originality** | The diff is largely another author's work (misattributed commits) **or** looks like unchecked machine output (obviously not built or run by a human before submission). | **Blocker** if misattributed; **Note** if just looks unchecked | 48930, 48929, 48932 |
| **P6** | **Duplicate / superseded** | Another open PR or a canonical issue already covers this. | **Blocker** (align first) | 48930 (dup of 48603), 49239, 47405 |
| **P7** | **Scope / atomicity** | Unrelated changes are bundled into one PR. | **Ask** (split) | 48929 |
| **P8** | **Draft / WIP state** | Clearly in-progress work is opened as ready-for-review instead of a Draft PR, or has sat in Draft for months. | **Note** (ask to mark Draft) / stale-close if abandoned | 48932, 44150 |
| **P9** | **PR template / description completeness** | The template (Summary, Closes #, Validation Steps) is empty or ignored. **Not a standalone blocker** — weight it only as part of the compound signal below. | **Note** | 48930, 48659 |

> **Not a gate: product-fit / design.** Whether a feature *should* exist, or a UX/design direction, is a product-owner call — not something this review enforces. If you have a design thought, raise it once as an in-thread Note and keep reviewing; never let it block or gate the code review.

**Compound-quality signal (recommend closing).** No single soft gate closes a PR, but maintainers *do* close when several stack up: template ignored **and** no author validation **and** unrelated changes **and** no prior agreement **and** poor quality. When three or more of {P5-unchecked, P7, P8, P9, no-validation} co-occur on a low-quality community PR, draft a respectful close recommendation rather than a deep code review. (Evidence: 48927, 48929, 48932, 48938, 48939.)

## 0d. Stale / no-response disposition

When **the author owes the next action** (a maintainer already asked for a demo, validation, rebase, etc.) and has been silent for a while, the standard PowerToys disposition is a **polite close that invites re-opening** — not a hard rejection. Recommend it (do not apply it without approval). Evidence: 48660, 47235, 46863, 46747, 48603, 48669, 48021, 44150.

## 0e. Draft the disposition

Combine the gate results into one of:

- **Proceed** — no gate tripped (or only Notes). Go straight to code-review output.
- **Ask** — draft the specific request(s) below; note the review pauses on the author.
- **Align first** — duplicate/superseded/out-of-scope: draft a redirect to the canonical PR/issue.
- **Recommend close** — compound-quality or long-stale-with-author-owing: draft the polite close.

Always recommend, never auto-apply — labels/closes are maintainer actions gated by Step 10.

## Reply templates (per gate)

Keep them short, specific, and friendly; thank first-time contributors first.

- **P1 CLA:** "Thanks for the PR! Before we can merge, please accept the CLA by commenting `@microsoft-github-policy-service agree` (make sure it is tagged exactly). Let me know once that is done."
- **P2 Demo:** "Thanks! Could you add a short screen recording (or before/after screenshots) of the new behavior to the PR description? We will not be able to continue the review without seeing it in action."
- **P3 Self-validation:** "Please confirm you have built and run this change locally, and include a quick note (or the recording above) of your manual validation steps."
- **P4 CI:** "The `<check>` check is red — could you take a look? (For spell-check, add any legitimate new words to `.github/actions/spell-check/allow/code.txt`.)"
- **P5 Provenance:** "Some of these commits appear to originate from PR `<other>` by another author. Could you clarify the authorship here before we continue?"
- **P6 Duplicate:** "This looks like it overlaps with PR `<other>` which is already in progress — let's consolidate there. Closing in favor of that; feel free to contribute on that thread."
- **P7 Scope:** "This PR bundles an unrelated change (`<what>`). Could you split that into its own PR so we can keep this one atomic?"
- **P8 Draft:** "If this is still a work in progress, could you convert it to a Draft PR so we know when it is ready for review?"
- **P9 Template:** "Could you fill out the PR template — a Summary, the `Closes #` issue, and your Validation Steps? It helps us track the work."
- **Product-fit (Note, in-thread, not a gate):** "One design thought: `<concern>`. Not blocking, and ultimately a product call — flagging for discussion. Continuing the code review in parallel."
- **Stale close (0d):** "Since there has been no response to the request above, I am closing this for now. Please feel free to re-open (or ping this thread) when you pick it back up. Thanks for the contribution!"

## Tips for a stronger context review

- **Lead with thanks**, especially for first-time contributors — every maintainer in the samples did.
- **Batch your asks** into one comment (CLA + demo + validation together) instead of drip-feeding — see 48810/48659 where reviewers combined them.
- **Quote the exact thing you need** ("a screen recording of the new behavior", "your Validation Steps") — vague asks stall.
- **A pipeline/CI screenshot is not a demo** — insist on the actual UI behavior (48810).
- **Members' small fixes need nothing here** — skip straight to code.
- **Labels are not enforced in this repo.** Existing labels can be a useful hint about triage, but they may be incomplete or stale — treat them as a reference, never as a gate.
- **The smooth path looks like:** clear Summary, `Closes #<issue>`, atomic scope, a recording if it is user-visible, and author-confirmed local validation. When you see that, say so and move on.
