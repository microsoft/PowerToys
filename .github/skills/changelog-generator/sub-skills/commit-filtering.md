# Commit Filtering Rules

This sub-skill defines rules for filtering commits to include in the changelog.

## Understanding the Branch Model

PowerToys uses a release branch model where fixes are cherry-picked from main:

```
main:     A---B---C---D---E---F---G---H  (HEAD)
               \
release:        X---Y---Z  (v0.96.1 tag)
                    ↑
            (X, Y are cherry-picks of C, E from main)
```

**Key insight:** When comparing `v0.96.1...main`:
- The release tag (v0.96.1) is on a **different branch** than main
- GitHub compare finds the merge-base and returns commits on main after that point
- Commits C and E appear in the results even though they were cherry-picked to release as X and Y
- **The SHAs are different**, so SHA-based filtering won't work!

## ⚠️ CRITICAL: Filter by PR Number, Not SHA

Since cherry-picks have different SHAs, you **MUST** check by PR number:

```powershell
# Extract PR number from commit message and check if it exists in the release tag
$prNumber = "43785"
$startTag = "v0.96.1"

# Search the release branch for this PR number in commit messages
$cherryPicked = git log $startTag --oneline --grep="#$prNumber"
if ($cherryPicked) {
    Write-Host "SKIP: PR #$prNumber was cherry-picked to $startTag"
} else {
    Write-Host "INCLUDE: PR #$prNumber is new since $startTag"
}
```

## Complete Filtering Workflow

```powershell
$startTag = "v0.96.1"   # Release tag (on release branch)
$endRef = "main"         # Target (main branch)

# Step 1: Get all commits from main since the merge-base with release tag
$commits = gh api "repos/microsoft/PowerToys/compare/$startTag...$endRef" `
    --jq '.commits[] | {sha: .sha, message: .commit.message}' | ConvertFrom-Json

# Step 2: Build list of PR numbers already in the release tag
$releasePRs = git log $startTag --oneline | Select-String -Pattern '#(\d+)' -AllMatches | 
    ForEach-Object { $_.Matches.Groups[1].Value } | Sort-Object -Unique

Write-Host "PRs already in $startTag : $($releasePRs.Count)"

# Step 3: Filter commits - skip if PR was cherry-picked to release
$newCommits = @()
foreach ($commit in $commits) {
    if ($commit.message -match '#(\d+)') {
        $prNumber = $matches[1]
        if ($releasePRs -contains $prNumber) {
            Write-Host "SKIP: PR #$prNumber already in $startTag (cherry-picked)"
            continue
        }
    }
    $newCommits += $commit
}

Write-Host "New commits to process: $($newCommits.Count)"
```

## Why SHA-Based Methods Don't Work Here

| Method | Works for same branch? | Works for cross-branch (cherry-picks)? |
|--------|------------------------|----------------------------------------|
| `git merge-base --is-ancestor` | ✅ Yes | ❌ No - different SHAs |
| `git tag --contains` | ✅ Yes | ❌ No - tag is on different branch |
| GitHub Compare API | ✅ Yes | ❌ No - returns commits by SHA |
| **PR number matching** | ✅ Yes | ✅ **Yes** |

## Skip Rules Summary

| Priority | Condition | Action |
|----------|-----------|--------|
| 1 | PR number found in `git log $startTag --grep="#$prNumber"` | **SKIP** - cherry-picked |
| 2 | Same PR number already processed in this run | **SKIP** - duplicate |
| 3 | Bot author (dependabot, etc.) | **SKIP** - unless user-visible |
| 4 | Internal-only change (CI, tests, refactor) | Move to **Development** section |

## User-Facing vs Non-User-Facing

**Include in changelog:**
- New features and capabilities
- Bug fixes that affect users
- UI/UX improvements
- Performance improvements users would notice
- Breaking changes or behavior modifications
- Security fixes

**Exclude from changelog (put in Development section):**
- Internal refactoring
- CI/CD changes
- Code style fixes
- Test additions/modifications
- Documentation-only changes
- Dependency updates (unless user-visible impact)
