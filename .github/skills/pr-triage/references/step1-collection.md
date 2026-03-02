# Step 1: Collection - Fetch Selected PRs

Collect the user-selected pull requests from the repository with core metadata needed for triage.

## First ask the user for scope

Before running collection, confirm:

- PR numbers to triage
- AI engine preference (`copilot` or `claude`)
- Whether to reuse existing review output (`-SkipReview`) or regenerate

## Data Collection

### Primary Query

Use the `Get-OpenPrs.ps1` script with explicit PR numbers:

```powershell
.github/skills/pr-triage/scripts/Get-OpenPrs.ps1 `
  -Repository microsoft/PowerToys `
  -PRNumbers 45234,45235,45236 `
  -OutputPath all-prs.json
```

### Additional Metadata Per PR

For each PR, also fetch:

```powershell
# Check status (CI/checks)
gh pr checks $prNumber --json name,state,conclusion

# Get linked issues
gh pr view $prNumber --json closingIssuesReferences
```

## Output Schema

Save to `all-prs.json`:

```json
{
  "collectedAt": "2026-02-04T10:30:00Z",
  "totalCount": 47,
  "prs": [
    {
      "number": 12345,
      "title": "Fix crash in FancyZones",
      "author": "contributor123",
      "createdAt": "2025-12-15T08:00:00Z",
      "updatedAt": "2026-01-20T14:30:00Z",
      "ageInDays": 51,
      "daysSinceUpdate": 15,
      "baseRefName": "main",
      "headRefName": "fix/fancyzones-crash",
      "labels": ["bug", "Area-FancyZones"],
      "assignees": [],
      "reviewRequests": [],
      "isDraft": false,
      "mergeable": "MERGEABLE",
      "additions": 45,
      "deletions": 12,
      "changedFiles": 3,
      "linkedIssues": [9876],
      "checksStatus": "PENDING"
    }
  ]
}
```

## Scope model

Collection is intentionally PR-number driven to keep triage reproducible and side-by-side comparable across engines.

## Calculated Fields

The script computes these derived fields:

| Field | Calculation |
|-------|-------------|
| `ageInDays` | `(Now - createdAt).TotalDays` |
| `daysSinceUpdate` | `(Now - updatedAt).TotalDays` |
| `sizeCategory` | XS (<10), S (<50), M (<200), L (<500), XL (500+) |
| `checksStatus` | Aggregate: PASSING, FAILING, PENDING, NONE |

## Error Handling

- **Rate limiting**: If `gh` returns 403, wait and retry with exponential backoff
- **Missing fields**: Use null/defaults for optional fields
- **Large repos**: Use pagination (`--limit` + `--cursor`) for 500+ PRs

## Next Step

After collection, proceed to [Step 2: Review](./step2-review.md) to run detailed AI reviews on all collected PRs.
