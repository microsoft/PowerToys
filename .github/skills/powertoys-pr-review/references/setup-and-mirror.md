# Setup & Mirror (Steps 0–2c)

The mirroring engine. In **self-check mode** skip Steps 1–2c — you already own the branch; build and review in the author's own worktree.

## Step 0: Resume check

A review may have been interrupted partway through. **Before mirroring, detect whether this skill already ran on PR `N` and pick up from where it stopped** instead of starting over (re-mirroring would clobber commits/replies from the prior run). Skip this step in self-check mode.

**0a. Detect prior artifacts** for PR `N`:

```powershell
# Fork PR mirrored for this PR? (Step 1 creates a "[PR N] ..." PR in the fork)
gh pr list --repo <FORK_REPO> --state all --search "[PR N] in:title" --json number,title,state,headRefName,url

# Fork branch pushed? (Step 1 pushes pr-iterate/N to the fork)
gh api "repos/<FORK_REPO>/git/ref/heads/pr-iterate/N" --jq '.ref' 2>$null

# Local worktree already created for the branch? (Step 2)
git -C <CLONE_PATH> worktree list | Select-String "pr-iterate/N"
```

If a fork PR exists, judge whether the Copilot **review loop** is finished with [scripts/Get-UnresolvedCopilotThreads.ps1](../scripts/Get-UnresolvedCopilotThreads.ps1) (unresolved Copilot threads must be 0, and the newest commit must not post-date the newest Copilot review).

**0b. Classify the prior state and jump** (state it to the user first, e.g. "Found an existing fork PR 123 for PR N with 2 unresolved Copilot threads — resuming the review loop"):

| Prior state | Evidence | Resume at |
| --- | --- | --- |
| **None** | No `[PR N]` fork PR and no `pr-iterate/N` fork branch | Step 1 (fresh mirror) |
| **Branch pushed, no fork PR** | `pr-iterate/N` ref exists, no `[PR N]` PR | Step 1 (create the fork PR from the existing branch) |
| **Fork PR exists, no local worktree** | `[PR N]` PR exists, `git worktree list` has no `pr-iterate/N` | Step 2 (create worktree), then re-sync per Step 2b before building |
| **Worktree exists, never built / build was red** | Worktree present, no prior successful build | Step 2b (re-sync) → Step 3 (build) |
| **Loop unfinished** | Fork PR has 1+ unresolved Copilot thread, or newest commit post-dates newest Copilot review | Step 5/6 (resume the loop; set `last_review_timestamp` to the newest already-processed review so resolved threads are not re-fixed) |
| **Loop clean, built** | Zero unresolved Copilot threads and worktree builds | Step 8–9 (summarize + draft the review) |

**0c. Re-sync before continuing.** Whenever you resume onto an existing branch/worktree, first bring the fork's `main` and the PR branch up to date (Step 2 sync + Step 2b rebase) so you do not build or review against stale `main`. If a rebase conflicts and you are not confident, stop and ask rather than guessing.

## Step 1: Mirror the PR

Given PR number `N` from `microsoft/PowerToys`:

1. Fetch PR details: `gh pr view N --repo microsoft/PowerToys --json title,body,headRefName,headRepository,headRepositoryOwner,number,commits,files`
2. Fetch linked issue(s): parse issue references from the body, then `gh issue view <issue_number> --repo microsoft/PowerToys --json title,body`.
3. Create the same branch in the fork:
   ```powershell
   git fetch origin pull/N/head:pr-iterate/N
   git push <FORK_REMOTE> pr-iterate/N
   ```
4. Create a PR in the fork (`<FORK_REPO>`):
   - **Title**: `[PR N] <original title>` (number without `#`).
   - **Description**: sanitized summary combining the original description and linked issue(s) — **replace every `#<number>` with plain text** like "PR 12345" — plus a header `> Mirrored from microsoft/PowerToys PR N for review iteration`.
   - Base branch: `main`.
   - **Label**: apply `pr-review` so this skill's fork PRs are distinguishable from those created by other skills (e.g. `powertoys-issue-to-pr`) in the same fork. Ensure the label exists first (idempotent):
   ```powershell
   gh label create pr-review --repo <FORK_REPO> --color 1D76DB --description "Fork PR created by the powertoys-pr-review skill" --force
   gh pr create --repo <FORK_REPO> --head pr-iterate/N --base main --title "[PR N] <sanitized title>" --body "<sanitized description>" --label pr-review
   ```

> **Critical Rule 5:** never use `#<number>` in a fork PR title/body — it notifies the upstream thread. Use plain numbers.

## Step 2: Create local worktree

1. `cd <CLONE_PATH>`
2. Sync the clone's main with upstream first, then push it to the fork (run [scripts/Sync-ForkMain.ps1](../scripts/Sync-ForkMain.ps1) or):
   ```powershell
   git fetch origin main
   git checkout main
   git merge origin/main --ff-only
   git push <FORK_REMOTE> main
   ```
3. Create the worktree with the repo's existing script:
   ```powershell
   .\tools\build\New-WorktreeFromBranch.ps1 -Branch pr-iterate/N
   ```
4. Note the worktree path from the output (typically `C:\PowerToys-XXXX`).
5. Initialize submodules in the new worktree:
   ```powershell
   cd <worktree-path>
   git submodule update --init --recursive
   ```

## Step 2b: Sync the PR branch with latest main

**Mandatory before any build or review.** The PR branch must be rebased on the latest main to avoid stale-dependency issues and to ensure Copilot reviews against current main.

1. Rebase in the worktree:
   ```powershell
   cd <worktree-path>
   git fetch origin main
   git rebase origin/main
   ```
2. If the rebase conflicts:
   - **Resolve if confident** — you understand both sides and can preserve the PR's intent plus main's changes, regardless of file count.
   - **Stop if uncertain** — if any conflict needs a design decision you cannot make confidently, **stop and ask the user**, explaining the conflict and the decision needed.
   - Then `git rebase --continue`.
3. Update submodules after rebase: `git submodule update --init --recursive`.
4. Force-push the rebased branch to the fork:
   ```powershell
   git push <FORK_REMOTE> pr-iterate/N --force-with-lease
   ```
   If force-push is blocked by repo rules, push to a new branch name (e.g., `pr-iterate/N-v2`) and update the fork PR accordingly.
5. **Re-sync the fork's main** (rebase moved the branch forward, but the fork's main may be stale):
   ```powershell
   cd <CLONE_PATH>
   git push <FORK_REMOTE> main
   ```
   Without this, the fork PR diff includes every commit between old-main and new-main as "changed files".

## Step 2c: Validate the fork PR diff matches the original scope

Prevents meaningless Copilot reviews caused by diff bloat. After rebase and push, the fork PR diff may include hundreds of unrelated files if the fork's `main` is behind upstream.

1. Get the original PR's changed files (ground truth): `gh pr view N --repo microsoft/PowerToys --json files --jq '.files[].path'`.
2. Get the fork PR's changed files: `gh pr view <fork_pr_number> --repo <FORK_REPO> --json files --jq '.files[].path'`.
3. The fork PR should contain **only** files from the original PR (possibly fewer if conflicts were resolved differently, but never MORE unrelated files).
4. **If the fork PR has significantly more files** (e.g., 10+ extra files not in the original):
   a. Sync the fork's main (run [Sync-ForkMain.ps1](../scripts/Sync-ForkMain.ps1)).
   b. Close the stale fork PR: `gh pr close <fork_pr_number> --repo <FORK_REPO> --comment "Closing: diff was bloated due to stale base. Recreating after syncing main."`
   c. Force-push the rebased branch (or a new `-v2` branch if force-push is blocked).
   d. Create a new fork PR against the now-synced main (Step 1's `gh pr create`, including `--label pr-review`).
   e. Re-validate: repeat 1–3.
5. Only proceed to Step 3 (build) and Step 4 (review) once the diff is validated.

**Why this matters:** Copilot reviews ALL files in the PR diff. If the fork PR shows 110 files changed when the original touches 3, Copilot wastes its review capacity on unrelated code and produces useless comments.
