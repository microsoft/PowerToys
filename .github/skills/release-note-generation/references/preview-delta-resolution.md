# Preview delta resolution

Preview notes describe the target candidate relative to the latest published stable or preview release that predates the candidate queue time.

## Same lineage

When previous commit `P` is an ancestor of target commit `T`, use `P..T`. All represented PRs are added and none are removed.

If `T` is an ancestor of `P`, stop: the candidate is an automatic rollback relative to an already published release.

## Branch transition

When `P` and `T` diverge:

```text
M = merge-base(P, T)
Previous side = M..P
Target side   = M..T
Added         = Target identities - Previous identities
Removed       = Previous identities - Target identities
```

Compare each side against the other commit's full reachable history, not only
the commits after the merge base. Stable cherry-picks can appear after the
merge base even when the same PR was merged into `main` earlier. Equal PR
identities or equivalent patches anywhere in the opposite history cancel.
Aggregate merge commits that only promote an ancestor of the opposite branch
also cancel; they are not independent release-note changes.

Resolve commit identity in this order:

1. Squash subject ending in `(#<number>)`.
2. Merge subject containing `Merge pull request #<number>`.
3. GitHub-associated PR for the commit.
4. `cherry picked from commit <sha>` and the source commit's PR.
5. Stable patch ID shared across the two sides.
6. Unattributed commit identity.

PR number is the semantic identity. Equal PR numbers cancel even when cherry-picking changed the SHA.

Do not invent attribution for aggregate promotion commits. If a promotion manifest is unavailable, preserve unresolved commits in `unattributed-commits.json` and surface them in the final review section.
