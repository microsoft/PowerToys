# Pull request intake

The workflow in `.github/workflows/pr-intake.yml` applies deterministic product
labels to same-repository pull requests and runs the same intake analysis on
fork and same-repository non-draft pull requests.
It does not call any AI model and does not execute any code from the pull
request head; the Node script reads all pull request data through the GitHub
API.
The workflow runs on `pull_request` and checks out the target repository's base
commit, never the PR head. Fork runs do not attempt comments or label changes.
Every completed analysis is written to the Actions job summary and logs; required
actions fail the check, while recommendations alone pass. A failed check is not
necessarily merge-blocking; repository branch rules are unchanged.

## Flow

1. Read the current PR and changed files through the GitHub API.
2. Add recognized `Product-*` labels from the historical path mapping. The
   longest path prefix wins, generic Settings/General labels are suppressed
   when a more specific product matches, and existing product labels are never
   removed.
3. Re-fetch mergeability a few
   times when GitHub still reports it as unknown so a conflicting PR is never
   treated as ready by default.
4. Deterministically validate closing issue references and merge conflicts.
5. Recommend visual evidence when a fork changes product UI files. This is
   advisory and never fails intake or adds `Needs-Author-Feedback`. Skip visual
   reminders for same-repository branches.
6. Keep a single canonical comment in sync and manage the `Ready for review`
   and `Needs-Author-Feedback` lifecycle labels.

Product labeling is additive. Intake never removes an existing product label,
and a pull request that touches multiple mapped roots receives each matching
label. Draft pull requests receive product labels but skip readiness checks.

## Comment behavior

- If there is anything for the author to act on or consider, a single canonical
  comment is posted or updated.
- If the PR is clean and a previous intake comment exists, it is replaced with a
  short all-clear note.
- If the PR is clean and no intake comment exists, nothing is posted.

Missing issue references are advisory. Explicitly invalid references, merge
conflicts and unknown mergeability block readiness. Missing screenshots do not.
Draft PR events run deterministic path labeling, remove intake
lifecycle labels, delete trusted canonical intake comments, and then stop.
Marking a draft ready triggers full intake.

On forks, suggested label additions do not fail the check. A stale
`Needs-Author-Feedback` label still requires maintainer removal because the
existing resource-management policy can close the PR after seven inactive days.
Each rerun reads the current PR, labels, files and closing references;
unchanged unresolved requirements still fail, and satisfied requirements pass.
Fork draft runs skip readiness and do not clean up labels or comments.

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
