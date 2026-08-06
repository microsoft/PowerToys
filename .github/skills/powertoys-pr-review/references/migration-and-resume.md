# Migrating Active Reviews to This Skill Version

Teammates using an older skill should stop before it posts malformed or unvalidated payloads. Existing code fixes and review-loop progress are durable in their own PowerToys repository and can be resumed.

## Message to teammates

Send:

> Please stop the current PR-review session now without posting, deleting, re-mirroring, rebasing, or closing anything. Update to the latest `powertoys-pr-review` skill, start a new Copilot session, and ask it to continue the same upstream PR numbers. The new skill will discover existing `pr-iterate/<number>` branches, review PRs, local worktrees, Copilot review rounds, commits, and unresolved threads, then resume instead of starting over. Old dashboard decisions are not safe to publish; the new session will regenerate schema-version-2 review data and ask for approval again.

## Resume workflow

1. Abort the old Copilot session. Do not delete its branch, worktree, review PR, commits, replies, or resolved threads.
2. Update the skill from the remote branch.
3. Start a new session in the teammate's PowerToys clone.
4. Run:
   ```powershell
   ./scripts/Get-ReviewResumeState.ps1 -PRNumber 43741,49427 -AsJson
   ```
5. Resume each PR according to `resumeAction`:

| `resumeAction` | Continue at |
| --- | --- |
| `fresh-mirror` | Step 1 |
| `push-and-create-review-pr` | Push the existing local review branch, then create its review PR without remirroring |
| `create-review-pr` | Create the review PR from the existing branch; do not overwrite the branch |
| `reopen-or-create-review-pr` | Reopen the matching review PR or create a new one from its existing branch |
| `create-worktree` | Step 2 using the existing review branch |
| `resume-review-loop` | Steps 5–6 using the discovered latest review timestamp and unresolved threads |
| `rebuild-and-draft` | Rebuild, rerun the final zero-comment check if needed, then regenerate the public payload |

6. Rebuild after every cross-session resume. Build success is not treated as durable unless the new session verifies it.
7. Discard old `review-data.json` and `review-decisions.json` files. Regenerate schema-version-2 data from the current head and run `Test-ReviewData.ps1 -CheckGitHub`.

## What is preserved

- review branch and commits;
- review PR and its current head;
- Copilot review timestamps and rounds;
- replies and resolved/unresolved review threads; and
- a matching local worktree, when it still exists.

Session-local dashboard state, unpublished prose drafts, and prior build claims are not trusted. Reconstruct and validate them.
