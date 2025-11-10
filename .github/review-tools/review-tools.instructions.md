---
description: PowerShell scripts for efficient PR reviews in PowerToys repository
applyTo: '**'
---

# PR Review Tools - Reference Guide

PowerShell scripts to support efficient and incremental pull request reviews in the PowerToys repository.

## Quick Start

### Prerequisites
- PowerShell 7+ (or Windows PowerShell 5.1+)
- GitHub CLI (`gh`) installed and authenticated (`gh auth login`)
- Access to the PowerToys repository

### Testing Your Setup

Run the full test suite (recommended):
```powershell
cd "d:\PowerToys-00c1\.github\review-tools"
.\Run-ReviewToolsTests.ps1
```

Expected: 9-10 tests passing

### Individual Script Tests

**Test incremental change detection:**
```powershell
.\Get-PrIncrementalChanges.ps1 -PullRequestNumber 42374
```
Expected: JSON output showing review analysis

**Preview incremental review:**
```powershell
.\Test-IncrementalReview.ps1 -PullRequestNumber 42374
```
Expected: Analysis showing current vs last reviewed SHA

**Migrate existing reviews:**
```powershell
# Migrate specific PRs
.\Migrate-ReviewToIncrementalFormat.ps1 -PullRequestNumbers 42374,42658

# Or migrate all
.\Migrate-ReviewToIncrementalFormat.ps1
```
Expected: "Added metadata" or "Already has review metadata"

**Fetch file content:**
```powershell
.\Get-GitHubRawFile.ps1 -FilePath "README.md" -GitReference "main"
```
Expected: README content displayed

**Get PR file patch:**
```powershell
.\Get-GitHubPrFilePatch.ps1 -PullRequestNumber 42374 -FilePath ".github/actions/spell-check/expect.txt"
```
Expected: Unified diff output

## Available Scripts

### Get-GitHubRawFile.ps1

Downloads and displays file content from a GitHub repository at a specific git reference.

**Purpose:** Retrieve baseline file content for comparison during PR reviews.

**Parameters:**
- `FilePath` (required): Relative path to file in repository
- `GitReference` (optional): Git ref (branch, tag, SHA). Default: "main"
- `RepositoryOwner` (optional): Repository owner. Default: "microsoft"
- `RepositoryName` (optional): Repository name. Default: "PowerToys"
- `ShowLineNumbers` (switch): Prefix each line with line number
- `StartLineNumber` (optional): Starting line number when using `-ShowLineNumbers`. Default: 1

**Usage:**
```powershell
.\Get-GitHubRawFile.ps1 -FilePath "src/runner/main.cpp" -GitReference "main" -ShowLineNumbers
```

### Get-GitHubPrFilePatch.ps1

Fetches the unified diff (patch) for a specific file in a pull request.

**Purpose:** Get the exact changes made to a file in a PR for detailed review.

**Parameters:**
- `PullRequestNumber` (required): Pull request number
- `FilePath` (required): Relative path to file in the PR
- `RepositoryOwner` (optional): Repository owner. Default: "microsoft"
- `RepositoryName` (optional): Repository name. Default: "PowerToys"

**Usage:**
```powershell
.\Get-GitHubPrFilePatch.ps1 -PullRequestNumber 42374 -FilePath "src/modules/cmdpal/main.cpp"
```

**Output:** Unified diff showing changes made to the file.

### Get-PrIncrementalChanges.ps1

Compares the last reviewed commit with the current PR head to identify incremental changes.

**Purpose:** Enable efficient incremental reviews by detecting what changed since the last review iteration.

**Parameters:**
- `PullRequestNumber` (required): Pull request number
- `LastReviewedCommitSha` (optional): SHA of the commit that was last reviewed. If omitted, assumes first review.
- `RepositoryOwner` (optional): Repository owner. Default: "microsoft"
- `RepositoryName` (optional): Repository name. Default: "PowerToys"

**Usage:**
```powershell
.\Get-PrIncrementalChanges.ps1 -PullRequestNumber 42374 -LastReviewedCommitSha "abc123def456"
```

**Output:** JSON object with detailed change analysis:
```json
{
  "PullRequestNumber": 42374,
  "CurrentHeadSha": "xyz789abc123",
  "LastReviewedSha": "abc123def456",
  "IsIncremental": true,
  "NeedFullReview": false,
  "ChangedFiles": [
    {
      "Filename": "src/modules/cmdpal/main.cpp",
      "Status": "modified",
      "Additions": 15,
      "Deletions": 8,
      "Changes": 23
    }
  ],
  "NewCommits": [
    {
      "Sha": "def456",
      "Message": "Fix memory leak",
      "Author": "John Doe",
      "Date": "2025-11-07T10:30:00Z"
    }
  ],
  "Summary": "Incremental review: 1 new commit(s), 1 file(s) changed since SHA abc123d"
}
```

**Scenarios Handled:**
- **No LastReviewedCommitSha**: Returns `NeedFullReview: true` (first review)
- **SHA matches current HEAD**: Returns empty `ChangedFiles` (no changes)
- **Force-push detected**: Returns `NeedFullReview: true` (SHA not in history)
- **Incremental changes**: Returns list of changed files and new commits

### Test-IncrementalReview.ps1

Helper script to test and preview incremental review detection before running the full review.

**Purpose:** Validate incremental review functionality and preview what changed.

**Parameters:**
- `PullRequestNumber` (required): Pull request number
- `RepositoryOwner` (optional): Repository owner. Default: "microsoft"
- `RepositoryName` (optional): Repository name. Default: "PowerToys"

**Usage:**
```powershell
.\Test-IncrementalReview.ps1 -PullRequestNumber 42374
```

**Output:** Colored console output showing:
- Current and last reviewed SHAs
- Whether incremental review is possible
- List of new commits and changed files
- Recommended review strategy

### Migrate-ReviewToIncrementalFormat.ps1

One-time migration script to add review metadata to existing review folders.

**Purpose:** Enable incremental review functionality for existing PR reviews by adding metadata sections.

**Parameters:**
- `PullRequestNumbers` (optional): Array of PR numbers to migrate. If omitted, migrates all reviews.
- `ReviewsFolderPath` (optional): Path to reviews folder. Default: "Generated Files/prReview"
- `RepositoryOwner` (optional): Repository owner. Default: "microsoft"
- `RepositoryName` (optional): Repository name. Default: "PowerToys"

**Usage:**
```powershell
# Migrate all existing reviews
.\Migrate-ReviewToIncrementalFormat.ps1

# Migrate specific PRs
.\Migrate-ReviewToIncrementalFormat.ps1 -PullRequestNumbers 42374,42658,42762
```

**What it does:**
- Scans existing `00-OVERVIEW.md` files
- Fetches current PR state from GitHub
- Adds `## Review metadata` section with current HEAD SHA
- Skips reviews that already have metadata

Run this once to enable incremental reviews on existing review folders.

## Workflow Integration

These scripts integrate with the PR review prompt (`.github/prompts/review-pr.prompt.md`).

### Typical Review Flow

1. **Initial Review (Iteration 1)**
   - Review prompt processes the PR
   - Creates `Generated Files/prReview/{PR}/00-OVERVIEW.md`
   - Includes review metadata section with current HEAD SHA

2. **Subsequent Reviews (Iteration 2+)**
   - Review prompt reads `00-OVERVIEW.md` to get last reviewed SHA
   - Calls `Get-PrIncrementalChanges.ps1` to detect what changed
   - If incremental:
     - Reviews only changed files
     - Skips irrelevant review steps (e.g., skip Localization if no `.resx` files changed)
   - Uses `Get-GitHubPrFilePatch.ps1` to get patches for changed files
   - Updates `00-OVERVIEW.md` with new SHA and iteration number

### Manual Testing Workflow

Preview changes before review:
```powershell
# Check what changed in PR #42374 since last review
.\Test-IncrementalReview.ps1 -PullRequestNumber 42374

# Get incremental changes programmatically
$changes = .\Get-PrIncrementalChanges.ps1 -PullRequestNumber 42374 -LastReviewedCommitSha "abc123" | ConvertFrom-Json

if (-not $changes.NeedFullReview) {
    Write-Host "Only need to review $($changes.ChangedFiles.Count) files"
    
    # Review each changed file
    foreach ($file in $changes.ChangedFiles) {
        Write-Host "Reviewing $($file.Filename)..."
        .\Get-GitHubPrFilePatch.ps1 -PullRequestNumber 42374 -FilePath $file.Filename
    }
}
```

## Error Handling and Troubleshooting

### Common Requirements

All scripts:
- Exit with code 1 on error
- Write detailed error messages to stderr
- Require `gh` CLI to be installed and authenticated

### Common Issues

**Error: "gh not found"**
- **Solution**: Install GitHub CLI from https://cli.github.com/ and run `gh auth login`

**Error: "Failed to query GitHub API"**
- **Solution**: Verify `gh` authentication with `gh auth status`
- **Solution**: Check PR number exists and you have repository access

**Error: "PR not found"**
- **Solution**: Verify the PR number is correct and still exists
- **Solution**: Ensure repository owner and name are correct

**Error: "SHA not found" or "Force-push detected"**
- **Explanation**: Last reviewed SHA no longer exists in branch history (force-push occurred)
- **Solution**: A full review is required; incremental review not possible

**Tests show "FAIL" but functionality works**
- **Explanation**: Some tests may show exit code failures even when logic is correct
- **Solution**: Check test output message - if it says "Correctly detected", functionality is working

**Error: "Could not find insertion point"**
- **Explanation**: Overview file doesn't have expected "**Changed files:**" line
- **Solution**: Verify overview file format is correct or regenerate it

### Verification Checklist

After setup, verify:
- [ ] `Run-ReviewToolsTests.ps1` shows 9+ tests passing
- [ ] `Get-PrIncrementalChanges.ps1` returns valid JSON
- [ ] `Test-IncrementalReview.ps1` analyzes a PR without errors
- [ ] `Migrate-ReviewToIncrementalFormat.ps1` adds metadata successfully
- [ ] `Get-GitHubRawFile.ps1` downloads files correctly
- [ ] `Get-GitHubPrFilePatch.ps1` retrieves patches correctly

## Best Practices

### For Review Authors

1. **Always run migration first**: Before using incremental reviews, run `Migrate-ReviewToIncrementalFormat.ps1` on existing reviews
2. **Test before full review**: Use `Test-IncrementalReview.ps1` to preview changes
3. **Check for force-push**: Review the analysis output - force-pushes require full reviews
4. **Smart step filtering**: Skip review steps for file types that didn't change

### For Script Users

1. **Use absolute paths**: When specifying folders, use absolute paths to avoid ambiguity
2. **Check exit codes**: Scripts exit with code 1 on error - check `$LASTEXITCODE` in automation
3. **Parse JSON output**: Use `ConvertFrom-Json` to work with structured output from `Get-PrIncrementalChanges.ps1`
4. **Handle empty results**: Check `ChangedFiles.Count` before iterating

### Performance Tips

1. **Batch operations**: When reviewing multiple PRs, collect all PR numbers and process in batch
2. **Cache raw files**: Download baseline files once and reuse for multiple comparisons
3. **Filter early**: Use incremental detection to skip unnecessary file reviews
4. **Parallel processing**: Consider processing independent PRs in parallel

## Integration with AI Review Systems

These tools are designed to work with AI-powered review systems:

1. **Copilot Instructions**: This file serves as reference documentation for GitHub Copilot
2. **Structured Output**: JSON output from scripts is easily parsed by AI systems
3. **Incremental Intelligence**: AI can focus on changed files for more efficient reviews
4. **Metadata Tracking**: Review iterations are tracked for context-aware suggestions

### Example AI Integration

```powershell
# Get incremental changes
$analysis = .\Get-PrIncrementalChanges.ps1 -PullRequestNumber $PR | ConvertFrom-Json

# Feed to AI review system
$reviewPrompt = @"
Review the following changed files in PR #$PR:
$($analysis.ChangedFiles | ForEach-Object { "- $($_.Filename) ($($_.Status))" } | Out-String)

Focus on incremental changes only. Previous review was at SHA $($analysis.LastReviewedSha).
"@

# Execute AI review with context
Invoke-AIReview -Prompt $reviewPrompt -Files $analysis.ChangedFiles
```

## Support and Further Information

For detailed script documentation, use PowerShell's help system:
```powershell
Get-Help .\Get-PrIncrementalChanges.ps1 -Full
Get-Help .\Test-IncrementalReview.ps1 -Detailed
Get-Help .\Migrate-ReviewToIncrementalFormat.ps1 -Examples
```

Related documentation:
- `.github/prompts/review-pr.prompt.md` - Complete review workflow guide
- `doc/devdocs/` - PowerToys development documentation
- GitHub CLI documentation: https://cli.github.com/manual/

For issues or questions, refer to the PowerToys contribution guidelines.
