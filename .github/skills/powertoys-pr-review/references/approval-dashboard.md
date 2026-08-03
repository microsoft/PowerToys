# Approval Dashboard (optional presentation surface for Step 10)

For a **multi-PR review session**, presenting every drafted comment as plain text is hard to act on. The approval dashboard renders all reviewed PRs as an interactive local web page so the user can, at a glance, approve, hold, edit, or partially post each PR's actions — then walk away and come back to a clear view.

It is **optional**. For a single PR, the plain-text presentation in [drafting-and-posting.md](./drafting-and-posting.md) is fine. Use the dashboard when you have drafted actions for **2+ PRs**, or whenever the user asks for the UI.

The dashboard **never calls GitHub**. It only records the human decision. Posting stays with the agent so the Step 10 freshness re-check and exact payloads are preserved.

## Live status tracker (launch at the *start* of the batch)

Because the page already runs a local server, use it as a **live progress tracker** during the review, not just an approval surface at the end. Launch it as soon as you know the batch of PRs (even before any review has converged), seeding `review-data.json` with one entry per PR at `phase: "queued"`. The page polls **`GET /status`** every ~2.5s, which re-reads `review-data.json` fresh, so as you advance each PR the sidebar dots, per-PR status line, and header summary update on their own — the user can start the run, walk away, and come back to a clear picture of what finished and what's still working.

Give each PR three live fields and set a top-level `phase`:

| Field | Meaning |
| --- | --- |
| `phase` (per PR) | `queued` → `mirroring` → `building` → `reviewing` → `drafting` → `ready` (done, awaiting approval) / `held` / `error`. Anything not in {`ready`,`held`,`queued`,`error`} renders as an amber pulsing "in progress" dot. |
| `loop` | Current fork Copilot review round (int). Shown as "round N". |
| `waitingOn` | Short string for what the PR is blocked on right now (e.g. `"Copilot review"`, `"local build"`). |
| `phase` (top level) | Overall batch phase, shown in the header. Set to `reviewing` while any PR is in progress; anything else once all are `ready`/`held`. |

**You are the single writer of `review-data.json`; the page only reads it.** Write it **atomically** so the poller never sees a half-written file: write `review-data.json.tmp`, then `Move-Item -Force` over the real file. Update it each time a PR changes phase (and bump `loop`/`waitingOn`). When a PR's review converges, flip its `phase` to `ready` and fill in `contextComment` + `suggestions` — they appear in the detail pane immediately.

The page preserves user edits across polls: once the user changes any control or edits the drafted comment for a PR, that PR's detail pane stops auto-re-rendering (an `edited ✓` marker shows in the sidebar), so live updates to *other* PRs never clobber in-progress decisions. **Submit decisions** pulses (and warns on click if any PR is still in progress) once every PR is out of `queued`/in-progress.

## Flow

1. **At the start of a multi-PR batch**, write a **review-data.json** with one entry per PR (schema below), each at `phase: "queued"`, and launch the dashboard (step 2). Update the file atomically as each PR progresses (see *Live status tracker* above). By the time the fork loop has converged for a PR (Critical Rule 11 — 0 new comments, 0 unresolved Copilot threads), you have filled in its `contextComment` and code suggestions and set its `phase` to `ready`.
2. Launch the dashboard (it opens the browser and keeps serving):
   ```powershell
   ./scripts/Show-ReviewDashboard.ps1 -DataPath <path>\review-data.json -Port 8787
   ```
   For an unattended session, launch it detached so it survives the turn; tell the user the URL and note the PID so it can be stopped later.
3. **Tell the user** the URL (`http://localhost:8787`), then **stop and end the turn** — this is the Step 10 mandatory stop. The user reviews at their own pace, sets a disposition per PR, toggles individual suggestions to post/hold (**all suggestions, including low severity, default to post** — the user unchecks any to hold), optionally edits the drafted comment, and can type free-text instructions (e.g. "also ask for a demo", "rebase first then give me build + e2e steps", "hold the low-severity ones"). Each PR's detail pane shows a **Run &amp; verify (already built)** block whose Launch line is the concrete `<worktree>\x64\Debug\PowerToys.exe` (auto-derived from the PR's `worktree`, since Step 7 already built it). Clicking **Submit decisions** writes `review-decisions.json` next to the data file and shows the resume phrase.
4. The user returns to the Copilot session and types the resume phrase: **`pr-review: actions ready`**.
5. The agent reads `review-decisions.json`, and for each PR **runs the mandatory freshness re-check** (Step 10.4) before doing anything, then executes only the approved actions with the Step 10 posting commands.

> The resume phrase is just a convention so you know to read the decisions file. The user can also simply say "go" / "post them". If `review-decisions.json.submitted` exists and is newer than the data file, the decisions are ready.

## review-data.json schema

```jsonc
{
  "generatedAt": "2026-08-03 15:58",
  "phase": "reviewing",                 // live: overall batch phase (reviewing until all PRs ready/held)
  "prs": [
    {
      "number": 43741,                 // upstream PR number
      "repo": "microsoft/PowerToys",
      "url": "https://github.com/microsoft/PowerToys/pull/43741",
      "title": "AdvancedPaste: Paste As Keystrokes",
      "author": "tonur",
      "phase": "ready",                 // live: queued|mirroring|building|reviewing|drafting|ready|held|error
      "loop": 4,                        // live: current fork Copilot review round
      "waitingOn": "",                  // live: what this PR is blocked on right now
      "assoc": "FIRST_TIME_CONTRIBUTOR",// author_association (drives the community bar)
      "firstTimer": true,
      "forkPr": "fork PR #153",         // optional label
      "forkPrUrl": "https://github.com/<owner>/PowerToys/pull/153",
      "worktree": "C:\\PowerToys-90d8", // the PR's build worktree; dashboard derives the Launch path from it
      "status": "reviewed-pending-approval",
      "disposition": "Request changes", // Phase 0 verdict (0e)
      "context": "niels9001 asked for a re-review; CI re-triggered.",
      "phase0Note": "CLA on file. Demo requested. CI green. In-scope. No duplicate.",
      "contextComment": "Thanks @tonur! ...", // drafted batched asks / summary (editable in UI)
      "exePath": "",                    // optional; only if the built exe isn't <worktree>\x64\Debug\PowerToys.exe
      "testInstructions": "Enable Advanced Paste, bind the hotkey, paste multi-line text, confirm keystroke output.", // run-and-verify steps (app already built in Step 7 — don't say "build")
      "suggestions": [
        {
          "id": "s1",                   // stable id used in the decisions file
          "severity": "critical",       // critical | high | medium | low
          "title": "Mocks don't implement new members — UnitTests won't compile",
          "file": "src/.../IntegrationTestUserSettings.cs",
          "line": 40,                   // 0 if not line-anchored
          "verified": true,             // spot-verified against the real diff
          "body": "Explanation ... \n\n```suggestion\n<corrected code>\n```"
        }
      ]
    }
  ]
}
```

`body` uses the same format as [drafting-and-posting.md](./drafting-and-posting.md#format-for-each-suggestion-comment) — prose plus a fenced ` ```suggestion ` block. The dashboard highlights the suggestion fence.

## review-decisions.json schema (produced by the page)

```jsonc
{
  "submittedAt": "2026-08-03T08:10:00Z",
  "prs": [
    {
      "number": 43741,
      "prAction": "request-changes",   // see table below
      "postContext": true,             // include the drafted comment
      "contextComment": "...",         // possibly edited by the user
      "suggestions": { "s1": "post", "s2": "post", "s5": "hold" },
      "instructions": "also ask for a demo recording"
    }
  ]
}
```

| `prAction` | The agent does |
| --- | --- |
| `post-subset` | Post the `post` suggestions (and the drafted comment if `postContext`) as inline comments + a COMMENTED review. |
| `request-changes` | Same, but submit the review with event `REQUEST_CHANGES`. |
| `approve` | Submit an APPROVE review (rare for community PRs — confirm intent). |
| `hold` | Post nothing for this PR now. |
| `close` | **Maintainer action** — only if the user explicitly chose it; close/redirect with a respectful message. |
| `custom` | Follow `instructions` verbatim. |

Always honor `instructions` in addition to `prAction` (e.g. "hold the low-severity ones", "rebase first", "confirm CLA before merge"). If `instructions` asks for build/e2e steps, provide them from the PR's `worktree` and `testInstructions` rather than posting.

## After reading decisions — still mandatory

For every PR the user asked to post, run the **Step 10.4 freshness re-check** first (new commits / new threads since the review). If the PR moved, reconcile per that step before posting — the decisions were made against the drafted snapshot, not necessarily the current head.

## Alternative binding (not default)

Instead of the resume-phrase handoff, each button could POST an instruction that the server turns into a fresh `copilot -p "..."` invocation. This is **not** the default: a fresh headless agent lacks this session's context (drafted payloads, the fork worktree, the freshness logic) and can race with the running review. Prefer the decisions-file handoff, which keeps the human-in-the-loop and all Step 10 safeguards in the original session.
