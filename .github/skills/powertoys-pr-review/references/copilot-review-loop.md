# Copilot Review Loop (Steps 4–8)

The fork Copilot loop is **not optional** and is independent of the posting decision (Critical Rule 11). Drive it to convergence before drafting Step 9.

## Step 4: Request Copilot review

Run [scripts/Request-CopilotReview.ps1](../scripts/Request-CopilotReview.ps1), or:

1. Request Copilot as reviewer on the fork PR:
   ```powershell
   gh api repos/<FORK_REPO>/pulls/<fork_pr_number>/requested_reviewers -X POST -f "reviewers[]=copilot-pull-request-reviewer[bot]"
   ```
   The reviewer name **must** be `copilot-pull-request-reviewer[bot]` (not plain `copilot`, which silently returns 200 with an empty `requested_reviewers`). The `--add-reviewer copilot` flag also does not work for bot accounts.
2. **Verify** the response's `requested_reviewers` array is non-empty. If empty, Copilot review is unavailable → see [Fallback: local review](#fallback-local-review).
3. Wait for the review to complete — poll every 30–60 seconds for up to 10 minutes: `gh api repos/<FORK_REPO>/pulls/<fork_pr_number>/reviews`, looking for a new review from `copilot-pull-request-reviewer[bot]` with `submitted_at` after the request time.

### Fallback: local review

If Copilot review cannot be enabled, first guide the user to turn it on (see [prerequisites.md](./prerequisites.md#3-copilot-code-review-on-the-fork)). If they choose local review instead:

- Launch a local code-review sub-agent against the changed files in the worktree.
- Focus on correctness, PowerToys style conventions, potential bugs, performance, and edge cases.
- Treat the local agent's findings the same as Copilot comments for the rest of the loop.

## Step 5: Process review comments — fix, push, reply, resolve

For each review comment from Copilot:

1. **Fetch new comments**: `gh api repos/<FORK_REPO>/pulls/<fork_pr_number>/comments`, filtered to `copilot-pull-request-reviewer[bot]` and newer than the last round.
2. **Assess validity** — is the suggestion correct, in-scope (on files this PR changed, not pre-existing issues), and safe?
3. **If valid — actually fix the code.** Open the referenced file and make the change. Do not just acknowledge — fix it.
4. **If invalid / not applicable** — note a clear reason for the reply (e.g., "intentional because...", "out of scope for this fix because...").
5. **After processing all comments in the round:**
   ```powershell
   cd <worktree-path>
   git add -A
   git commit -m "Address review round N: <brief summary of fixes>"
   git push <FORK_REMOTE> pr-iterate/N
   ```
   Then reply to each comment **and** resolve it:
   ```powershell
   # Reply
   gh api repos/<FORK_REPO>/pulls/<fork_pr_number>/comments/<comment_id>/replies -f body="Fixed in <commit-sha>: <brief description>"
   # Resolve the thread
   gh api graphql -f query='mutation { resolveReviewThread(input: {threadId: "<thread_node_id>"}) { thread { isResolved } } }'
   ```
   Every comment must be **both** replied to and resolved. If `resolveReviewThread` is unavailable, try `minimizeComment` with `classifier: RESOLVED` using the comment's `node_id`.

## Step 6: Iterate the loop

**The most critical step. You must actually loop — do not stop after one round unless there are zero new comments.**

```
round = 1
last_review_timestamp = <timestamp before first request>

LOOP:
  1. Request Copilot review (Step 4).
  2. Wait for a new review with submitted_at > last_review_timestamp (poll up to 10 min).
  3. Fetch comments created after last_review_timestamp from copilot-pull-request-reviewer[bot].
  4. If ZERO new comments -> EXIT LOOP -> Step 7.
  5. For each new comment: assess; if valid+in-scope, fix in the worktree.
  6. If any fixes were made: git add -A; commit; push to the fork branch.
  7. For each comment: reply with the fix (or the reason) AND resolve the thread.
  8. last_review_timestamp = now; round += 1.
  9. Termination:
       - round > 10 AND no medium/high severity comments remaining -> EXIT LOOP.
       - round > 20 -> EXIT LOOP (leave a note that manual review may be needed).
       - Otherwise -> GOTO 1.
END LOOP
```

**Rules for the loop:**
- **Push before re-requesting** — Copilot reviews the latest pushed code.
- **Re-request after every push** — Copilot will not auto-review.
- **Track timestamps** — only process comments newer than the last request; do not re-process old ones.
- **If Copilot repeats a fixed concern**, reply "Already addressed in commit `<sha>`" and resolve; do not re-fix.
- **Out-of-scope comments** (files this PR did not modify): reply "Not applicable: this file is not modified by this PR" and resolve; do not fix.

**Definition of done (all must hold before leaving Step 6):**
1. The most recent **freshly-requested** Copilot review returned **zero** new inline comments.
2. **Zero** unresolved Copilot review threads remain (every thread replied-to and resolved).
3. The worktree builds (Step 7) — modulo documented environmental failures.

Before moving on (to the next PR, to Step 9, or to ending the turn), run the stranded-loop self-audit with [scripts/Get-UnresolvedCopilotThreads.ps1](../scripts/Get-UnresolvedCopilotThreads.ps1). Expect 0; a non-zero count means resume the loop (fix/resolve/re-request) — do not present yet.

## Step 8: Summarize the converged net diff

1. `git log --oneline main..pr-iterate/N` — review all commits made during the loop.
2. Produce a clean diff of the net changes relative to the original PR's base: `git diff main...pr-iterate/N`.
3. Distinguish meaningful changes from noise (back-and-forth later reverted). If some iterations added code that does not contribute, consider squashing or reverting those specific changes.
4. Keep only changes that fix genuine issues. The Step 9 suggestions are drawn from this converged net diff — never from raw round-1 Copilot output.
