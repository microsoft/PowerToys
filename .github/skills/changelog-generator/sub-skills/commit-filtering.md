# Commit Filtering Rules

This sub-skill defines rules for filtering commits to include in the changelog.

## Filter Out Commits Already in Start Tag

**CRITICAL: Skip commits that are already included in the start tag to avoid duplicates.**

This happens when:
- Cherry-picks from main to stable branch
- Backported fixes that were already released
- Commits included in a previous release

### Method 1: Git Merge-Base (Local, Most Reliable)

```powershell
$startTag = "v0.96.0"
$commitSha = "abc1234"

# Check if commit is ancestor of (already included in) start tag
# Returns exit code 0 if true, 1 if false
git merge-base --is-ancestor $commitSha $startTag
if ($LASTEXITCODE -eq 0) {
    Write-Host "SKIP: Commit $commitSha already in $startTag"
} else {
    Write-Host "PROCESS: Commit $commitSha is new"
}
```

### Method 2: GitHub API Compare (Auto-Filtered)

```powershell
# The compare API already handles this - it returns only commits 
# that are in the "head" but NOT in the "base"
$compare = gh api repos/microsoft/PowerToys/compare/v0.96.0...v0.96.1 | ConvertFrom-Json

# These commits are guaranteed to be NEW (not in v0.96.0)
$newCommits = $compare.commits
Write-Host "Found $($newCommits.Count) new commits not in start tag"
```

### Method 3: Check Tags Containing Commit

```powershell
$commitSha = "abc1234"
$tagsContaining = git tag --contains $commitSha
if ($tagsContaining -match "v0\.96\.0") {
    Write-Host "SKIP: Commit already released in v0.96.0"
}
```

## Batch Filter Script

```powershell
$startTag = "v0.96.0"
$endTag = "v0.96.1"

# Get commits from compare (already filtered - only new commits)
$newCommits = gh api repos/microsoft/PowerToys/compare/$startTag...$endTag --jq '.commits[] | {sha: .sha, message: .commit.message}' | ConvertFrom-Json

# Additional validation: filter out any merge commits that reference old PRs
$filteredCommits = $newCommits | Where-Object {
    $sha = $_.sha
    # Double-check: ensure commit is not ancestor of start tag
    git merge-base --is-ancestor $sha $startTag 2>$null
    $LASTEXITCODE -ne 0  # Keep only if NOT an ancestor
}

Write-Host "After filtering: $($filteredCommits.Count) commits to process"
```

## Skip Rules Summary

| Condition | Action |
|-----------|--------|
| `git merge-base --is-ancestor $sha $startTag` returns 0 | SKIP - already in start tag |
| Commit message contains "cherry-pick" + old PR number | SKIP - likely backport |
| PR was merged before start tag date | SKIP - old PR |
| Same PR number already processed | SKIP - duplicate |

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
