# AI-assisted pull request triage

The workflow in `.github/workflows/pr-intake.md` combines deterministic pull
request checks with one bounded GitHub Copilot pass.

## Flow

1. Read the current PR through the GitHub API and collect bounded metadata,
   changed-file facts, selected patch excerpts, and existing visual evidence.
2. Ask Copilot for a factual summary and a visual-evidence classification:
   `REQUIRED`, `RECOMMENDED`, or `NOT_NEEDED`.
3. Hash the exact evidence supplied to Copilot and reject stale output when the
   PR changes before publication.
4. Deterministically validate closing issue references, merge conflicts, draft
   state, and whether requested visual evidence is present.
5. Update one canonical comment and manage only `Needs-Review` and
   `Needs-Author-Feedback`.

Missing issue references are advisory. Explicitly invalid references, merge
conflicts, and missing required visual evidence block readiness. Draft PRs do
not receive `Needs-Author-Feedback` solely because they are drafts.

The existing resource-management policy closes PRs that retain
`Needs-Author-Feedback` for seven inactive days. PR synchronization is handled
by this workflow rather than the legacy policy responder.

Run the focused tests with:

```console
node --test .github\scripts\pr-intake\tests\pr-intake.test.mjs
```
