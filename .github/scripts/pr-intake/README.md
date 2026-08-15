# Pull request intake

The workflow in `.github/workflows/pr-intake.yml` runs deterministic pull
request intake checks on non-draft pull requests. It does not call any AI model
and does not execute any code from the pull request head; the Node script reads
all pull request data through the GitHub API.

## Flow

1. Read the current PR through the GitHub API. Mergeability is re-fetched a few
   times when GitHub still reports it as unknown so a conflicting PR is never
   treated as ready by default.
2. Deterministically validate closing issue references, merge conflicts, and
   whether visual evidence is present.
3. Require visual evidence only when the changed paths touch product UI files.
4. Keep a single canonical comment in sync and manage only the
   `Ready for review` and `Needs-Author-Feedback` labels.

## Comment behavior

- If there is anything for the author to act on or consider, a single canonical
  comment is posted or updated.
- If the PR is clean and a previous intake comment exists, it is replaced with a
  short all-clear note.
- If the PR is clean and no intake comment exists, nothing is posted.

Missing issue references are advisory. Explicitly invalid references, merge
conflicts, unknown mergeability, and missing required visual evidence block
readiness. Draft PR events skip normal intake; conversion to draft runs only
lifecycle-label cleanup, and marking a draft ready triggers full intake.

The existing resource-management policy closes PRs that retain
`Needs-Author-Feedback` for seven inactive days. PR synchronization is handled
by this workflow rather than the legacy policy responder.

## Robustness limits

- Closing references parsed from the untrusted PR body are capped
  (`MAX_CLOSING_REFERENCES`) and verified with bounded concurrency
  (`CLOSING_VERIFY_CONCURRENCY`) so a crafted body cannot exhaust the API rate
  limit.

Run the focused tests with:

```console
node --test .github\scripts\pr-intake\tests\pr-intake.test.mjs
```
