# Batch (parallel) review — 2+ PRs at once

When asked to review multiple PRs (e.g. "review PRs 123, 456, 789"), **review them concurrently, not one after another.** Each PR spends most of its wall-clock time *waiting* — Copilot review polling (up to ~10 min per round), local builds, rebase/push — so serializing a batch wastes almost all of that time. Overlap the waits.

Do not fall back to one-PR-at-a-time execution merely because background sub-agents are unavailable. Use the pipelined single-agent mode below so multiple PRs still have review requests and waits concurrently in flight.

## Concurrency model: one background sub-agent per PR

The orchestrator (this session) fans out **one background sub-agent per PR** and stays as the coordinator. Each sub-agent owns its PR end-to-end through the code-review engine (Steps 0–8): resume-check, mirror, worktree, rebase, build, and the **full Copilot loop to convergence** (Rule 11 — 0 new comments, 0 unresolved threads). It then returns its converged net diff + drafted suggestions + Phase 0 disposition to the orchestrator.

```
orchestrator:
  1. Parse the PR list. Run Phase-0 author-type + prerequisites ONCE up front.
  2. Sync the fork's main from upstream ONCE (do NOT let each child race on this — see Shared state).
  3. Run `Get-ReviewResumeState.ps1` for the full batch, reuse every durable trace it finds, then seed schema-version-2 review-data.json with one entry per PR at phase:"queued" and launch the dashboard.
  4. Fan out background sub-agents, capped at MAX_CONCURRENT (3–5). As each finishes, launch the next queued PR.
  5. Poll children; as each reports a phase change, update review-data.json (orchestrator is the SINGLE writer).
  6. When a child converges, record its contextComment + suggestions and set phase:"ready".
  7. When all are ready/held, present for the Step 10 approval gate (or hand to the dashboard).
```

- **Cap concurrency at 3–5.** More invites GitHub rate limits (review requests, API calls) and thrashes the machine. Launch the next PR only as a slot frees.
- **Each PR is independent on disk**: its own fork branch `pr-iterate/N` and its own worktree `C:\PowerToys-<hash>`. Git operations across PRs don't collide, so parallelism is safe.
- Use the CLI's background sub-agents (Task tool, `mode: background`) — one per PR. Give each the complete context it needs (PR number, fork config, resume-check instruction, the Rule 11 definition of done). Sub-agents are stateless; don't assume shared memory.
- Persist each worker's round, head SHA, latest review time, build result, and unresolved-thread count so a replacement session can resume it.
- Apply the suppressed-comment policy from [copilot-review-loop.md](./copilot-review-loop.md): important findings only in rounds 1–5, ignore all suppressed findings after round 5, and never treat them as an independent convergence blocker.

## Serialize the local builds

Parallel C++/MSBuild builds contend badly (CPU, disk, and shared intermediate state) and can corrupt each other. **Allow only one build at a time across the whole batch.** Give the children a simple mutex: a lock file (e.g. `<clone>\.pr-review-build.lock`) or a build queue the orchestrator hands out. A child that needs to build waits for the lock, builds, releases. Everything else (mirror, rebase, push, Copilot polling, fixing code) still runs fully in parallel — only the compile step is serialized.

## Shared state — do these ONCE, not per child

Some steps touch shared resources and must not race:

- **Fork `main` sync** (Step 2 / 2c): the orchestrator fast-forwards and pushes the fork's `main` once, up front, before any child mirrors. If every child pushes `main` concurrently you get non-fast-forward rejects and diff bloat. Children rebase their own branch onto the already-synced `origin/main`.
- **`review-data.json`**: the orchestrator is the **single writer** (Rule from [approval-dashboard.md](./approval-dashboard.md)). Children do **not** write it directly — they report status back to the orchestrator (via their turn output / background-agent messages), and the orchestrator writes atomically (`.tmp` then `Move-Item -Force`). This keeps the dashboard poller from ever seeing a half-written or conflicting file.
- **Upstream publishing**: workers never post directly. The coordinator validates the complete batch with `Test-ReviewData.ps1 -CheckGitHub`, waits for explicit approval, and uses `Publish-ApprovedReview.ps1`.

## Aggregation and the approval gate

- Collect each child's converged result: Phase 0 disposition, the net-diff suggestions (Step 9 format), and the worktree path for e2e testing.
- **Before presenting, run the stranded-loop self-audit for every PR** ([scripts/Get-UnresolvedCopilotThreads.ps1](../scripts/Get-UnresolvedCopilotThreads.ps1) — expect 0 each). Any non-zero means that PR's loop isn't done; resume it before Step 10. Parallelism must not become an excuse to present a stranded PR.
- Present all PRs together at the single Step 10 approval gate (the dashboard is the natural surface for this — it already renders one row per PR). Posting still happens only after explicit approval and the mandatory per-PR freshness re-check (Critical Rule 1).

## If sub-agents aren't available (pipelined parallel fallback)

If the environment can't spawn background sub-agents, still **pipeline the waits** in the single agent instead of fully serializing: for *all* PRs, do mirror → rebase → push → **request Copilot review** up front so every PR's ~10-min review clock runs at once; then cycle through processing whichever review returns first (fix → push → re-request), building one PR at a time. Use a `manage_schedule` monitor per PR when loops must continue across turns. At no point should an independent PR wait for another PR to converge before its own mirror/review request begins.
