# GitHub API Reference for Changelog Generation

This sub-skill provides GitHub API commands and scripts for fetching commit and PR data.

## Authentication

```powershell
# Ensure gh CLI is authenticated
gh auth status

# Or use personal access token
$headers = @{ Authorization = "token $env:GITHUB_TOKEN" }
```

## Useful API Endpoints

```powershell
# Compare two refs (tags, branches, commits)
gh api repos/microsoft/PowerToys/compare/v0.96.0...v0.96.1

# List commits with pagination
gh api "repos/microsoft/PowerToys/commits?per_page=100&page=1"

# Get PR associated with a commit
gh api repos/microsoft/PowerToys/commits/{sha}/pulls

# Get PR details
gh api repos/microsoft/PowerToys/pulls/{number}

# Get files changed in a PR
gh api repos/microsoft/PowerToys/pulls/{number}/files

# Search PRs merged in date range
gh api "search/issues?q=repo:microsoft/PowerToys+is:pr+is:merged+merged:2025-01-01..2025-01-31"
```

## Fetch Commits in Range

```powershell
$owner = "microsoft"
$repo = "PowerToys"
$startTag = "v0.96.0"
$endTag = "v0.96.1"

# Get all commits between two tags
gh api repos/$owner/$repo/compare/$startTag...$endTag --jq '.commits[] | {sha: .sha, message: .commit.message, author: .author.login, date: .commit.author.date}'
```

## Get PR Details for a Commit

```powershell
# Get PR associated with a commit SHA
gh api "repos/$owner/$repo/commits/{sha}/pulls" --jq '.[0] | {number: .number, title: .title, body: .body, user: .user.login, labels: [.labels[].name]}'

# Get full PR details
gh pr view 1234 --repo microsoft/PowerToys --json title,body,author,files,labels,mergedAt
```

## Batch Processing Script

```powershell
$startTag = "v0.96.0"
$endTag = "v0.96.1"

$commits = gh api repos/microsoft/PowerToys/compare/$startTag...$endTag --jq '.commits[].sha' | ForEach-Object {
    $sha = $_
    $prInfo = gh api "repos/microsoft/PowerToys/commits/$sha/pulls" 2>$null | ConvertFrom-Json
    if ($prInfo) {
        [PSCustomObject]@{
            SHA = $sha
            PRNumber = $prInfo[0].number
            Title = $prInfo[0].title
            Author = $prInfo[0].user.login
            Labels = $prInfo[0].labels.name -join ", "
        }
    }
}
$commits | Format-Table
```

## Rate Limiting

```powershell
# Check remaining rate limit
gh api rate_limit --jq '.rate'
# Authenticated: 5000 requests/hour
# Unauthenticated: 60 requests/hour
```

## Pagination for Large Results

```powershell
# GitHub API returns max 100 items per page
$page = 1
$allCommits = @()
do {
    $commits = gh api "repos/$owner/$repo/commits?per_page=100&page=$page" | ConvertFrom-Json
    $allCommits += $commits
    $page++
} while ($commits.Count -eq 100)
```
