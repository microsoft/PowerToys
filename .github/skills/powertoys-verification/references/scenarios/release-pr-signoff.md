# Scenario B — Release / hotfix PR sign-off

**Use when:** the task asks you to verify the PRs that shipped in a release or hotfix, against an
**already-installed** build — e.g. "sign off 0.100.1", "verify the 14 PRs in this draft release",
"validate the hotfix PRs". You derive each PR's checklist from its own description/diff, then drive
it like one checklist item.

## Bits under test

```
BITS: installed shipped artifact <version> (read-only)
```

Same immutability rule as Scenario A — you are validating the **shipped artifact**, not source.
Forbidden / allowed list is in `index.md` → "bits contract" and `../pre-flight.md` §Hard rules.
The classic failure this scenario exists to catch (e.g. an installer that dropped a `Manifests\`
subfolder) only surfaces if you run the **documented user flow** and report FAIL when it doesn't
produce the claimed outcome — never paper over it by editing install state.

---

## Step 0 — Acquire the PR set (discovery model)

A full release can carry ~100 PRs; a hotfix ~10–25. Blindly looping 100 is the wrong default.
Resolve the set with the **smallest, most explicit source available**, then apply the size gate.

### Input modes (prefer the most explicit one the caller gave you)

| Mode | Source | How to enumerate |
|---|---|---|
| **1. Explicit set** *(preferred)* | PR numbers, a sign-off doc, or a draft/published release whose notes list the PRs | Parse the PR numbers directly. For a release: `gh api repos/microsoft/PowerToys/releases/<id>` → extract `#NNNNN` from `.body`. |
| **2. Discoverable source** | A milestone, a release tag, or a commit/tag range | `gh pr list --repo microsoft/PowerToys --search "milestone:0.X" --state merged --limit 200 --json number,title,labels,files` · or for a range: `git log <tagA>..<tagB> --oneline` then extract `(#NNNNN)` merge refs. |
| **3. Caller defers entirely** | "verify the latest release" with no list | Resolve to a concrete release/milestone first (mode 1/2). If you cannot, **ask** rather than guess. |

### Size gate (the guardrail)

Let `N` = enumerated candidate count, `MAX_AUTO = 25` (hotfix-sized; tune per request).

- **`N ≤ MAX_AUTO`** → proceed: derive + verify each PR (this is the hotfix case — doing all is valid).
- **`N > MAX_AUTO`** → **STOP. Do not blind-loop.** Present the candidate list grouped by
  module/area with counts, and require the caller to **scope** before deep-verifying: by module,
  by label (e.g. `Needs-Verification`, priority), or an explicit subset. Confirm, then verify the
  scoped set. **Depth beats throughput** — deeply verifying 20 PRs and leaving a clear queue of the
  rest beats 100 shallow "source-verified" reports.

### Pre-filter non-runtime PRs

Before counting against the gate, set aside PRs with **no user-observable runtime surface** — list
them as `exempt` (not BLOCKED, not PASS) with the reason, don't spend drive cycles on them:

- docs-only, CI/pipeline/build-only, dependency bumps, localization/translation-only,
  release-signing/installer-plumbing with nothing to click.

Detect via labels and changed-file paths (`gh pr view N --json files,labels`). Everything else
(anything a user opens/toggles/presses/types/previews) goes into the deep-verify set and is subject
to the live-drive floor below.

---

## Step 1 — Per PR: derive the checklist, then drive it

For each PR in the (possibly scoped) set:

```powershell
gh pr view  <N> --repo microsoft/PowerToys --json title,body,files,labels,state,mergedAt
gh pr diff  <N> --repo microsoft/PowerToys      # read the diff — it tells you what actually changed
```

Turn the description + diff into **1–3 concrete checklist items** (the observable claim(s) the PR
makes), then drive each with the `SKILL.md` §2 bucket selector and classify with `SKILL.md` §3.
If, after the description AND the diff, you still cannot tell what to verify → **FAIL
(cause=checklist)** ("Don't know what to test"), quoting the ambiguity — do not guess.

You may `grep`/`view` a **read-only** local clone of `microsoft/PowerToys` (on `stable` or the
release tag) for source context (XAML AutomationIds, .cs referenced by the PR). You do **not** run
code from the clone against the installed build — that would be Scenario C.

### Live-drive floor (anti-shallow-verification)

If the PR has a verb a real user performs (open/toggle/press/drag/right-click/type/preview/paste/
invoke/install/pin/search/record/scroll), the steps table MUST contain **≥4 `winapp ui …` rows**
and **≥1 `winapp ui screenshot` of the post-state**. A PR with a user-visible surface and zero
`winapp ui …` rows is **not validated**. "Source verified; live deferred" is a weasel-word that
downgrades the verdict (see `index.md` → verdict vocabulary).

## Step 2 — Per-PR artifacts + report

One folder per PR: `{Module}-PR{Number}/` (e.g. `CmdPal-PR48689/`). All screenshots and the PR's
`report.md` go there. The verdict lives **inside** `report.md`, not in the folder name. Use the
`../reporting-format.md` per-item table; the `winapp invoke` column is a hard contract — a literal
`winapp ui …` command or `—` (never a `Select-String`/`gh`/`Test-Path` in that column).

## Step 3 — Roll-up

Top-level summary table: PR · Module · verdict · one-line evidence, plus the `exempt` and
(if `N > MAX_AUTO`) the un-scoped **queue** of PRs not yet verified. Include the `BITS:` line and a
§G retrospective (run friction). Restore all mutated state and confirm the runner is healthy before
declaring done.

## Worked reference

The 0.100.1 14-PR sign-off (8 PASS / 6 BLOCKED) is a canonical Scenario-B run: derived each PR's
checklist from its release-notes line + diff, drove the installed bits, and blocked the
hardware/visual-only items (DDC/CI, dual-GPU, audio, transient capture overlays) with named
reasons.
