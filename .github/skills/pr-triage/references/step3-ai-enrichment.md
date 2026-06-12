# Step 3: AI Enrichment — AI CLI per PR

`Invoke-AiEnrichment.ps1` enriches each PR with AI-derived signals by invoking the selected AI CLI (`copilot` or `claude`) with MCP tools.
It reads the full PR discussion, images, and AI code review findings (from Step 2),
then scores 7 dimensions. The actual category assignment happens in Step 4.

---

## How It Works

For each PR, the script:

1. Builds a prompt from `categorize-pr.prompt.md` with PR metadata filled in
2. Launches the selected AI CLI
3. AI reads the PR discussion via `gh pr view` and fetches images/attachments via MCP tools
4. AI returns a JSON block with 7 dimension scores

### Sequential Execution

PRs are processed one at a time (not parallel) because the AI CLI + MCP server
are stateful. The script saves results incrementally so it can resume after interruption.

### Resume & Cache

- Existing results in `ai-enrichment.json` are loaded and skipped
- Per-PR raw output is cached under `<OutputRoot>/__tmp/cat-output-{N}.txt`
- If cached output exists and parses successfully, the CLI is not re-invoked
- Pass `-Force` to re-evaluate all PRs

---

## 7 Evaluation Dimensions

Each dimension is scored 0.0–1.0 with a confidence level and reasoning string.

| Dimension | What it measures |
|-----------|-----------------|
| `review_sentiment` | How positive/negative reviewer feedback is |
| `author_responsiveness` | Is the author actively engaged? |
| `code_health` | Are there bugs, security issues, or design problems? |
| `merge_readiness` | How close to merge (approvals, CI, discussion)? |
| `activity_level` | How recently was this PR active? |
| `direction_clarity` | Do reviewers agree on the approach? |
| `superseded` | Has this PR been replaced by another? |

See [categorize-pr.prompt.md](./categorize-pr.prompt.md) for full scoring rubrics.

---

## Output

`ai-enrichment.json`:

```json
{
  "CategorizedAt": "2026-02-12T10:00:00Z",
  "Repository": "microsoft/PowerToys",
  "TotalCount": 112,
  "AiSuccessCount": 108,
  "AiFailedCount": 4,
  "Results": [
    {
      "Number": 45542,
      "Dimensions": {
        "review_sentiment": { "Score": 0.7, "Confidence": 0.85, "Reasoning": "..." },
        "author_responsiveness": { "Score": 0.5, "Confidence": 0.6, "Reasoning": "..." },
        "code_health": { "Score": 0.8, "Confidence": 0.9, "Reasoning": "..." },
        "merge_readiness": { "Score": 0.6, "Confidence": 0.75, "Reasoning": "..." },
        "activity_level": { "Score": 0.4, "Confidence": 0.95, "Reasoning": "..." },
        "direction_clarity": { "Score": 0.9, "Confidence": 0.8, "Reasoning": "..." },
        "superseded": { "Score": 0.0, "Confidence": 0.95, "Reasoning": "..." }
      },
      "SuggestedCategory": "in-active-review",
      "DiscussionSummary": "Reviewer approved with minor nits...",
      "SupersededBy": null,
      "Tags": ["review-clean"],
      "Source": "ai"
    }
  ]
}
```

PRs where `Source` is `"failed"` will fall back to rule-based categorization in Step 4.

---

## Script Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `-InputPath` | (required) | Path to `all-prs.json` |
| `-OutputPath` | `ai-enrichment.json` | Where to write results |
| `-OutputRoot` | — | Run output root for temporary/cache files |
| `-Repository` | `microsoft/PowerToys` | GitHub repo |
| `-CliType` | `copilot` | AI engine: `copilot` or `claude` |
| `-ReviewOutputRoot` | `Generated Files/prReview` | Where Step 2 review output is read from |
| `-TimeoutMin` | `5` | Per-PR timeout in minutes |
| `-Force` | `false` | Re-evaluate all PRs |

---

## Next Step

After AI enrichment, proceed to [Step 4: Categorization](./step4-categorization.md) which merges AI dimensions with GitHub API data and assigns final categories.
