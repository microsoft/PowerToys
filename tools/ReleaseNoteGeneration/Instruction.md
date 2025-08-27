## Background
This document describes how to collect pull requests for a milestone, request a GitHub Copilot code review for each, and produce release‑notes summaries grouped by label.

## Prerequisites
- Windows with PowerShell 7+ (pwsh)
- GitHub CLI installed and authenticated to the target repo
  - gh version that supports Copilot review requests
  - Logged in: gh auth login (ensure repo scope)
- Access to the repository configured in the scripts (default: `microsoft/PowerToys`)
- GitHub Copilot code review enabled for the org/repo (required for requesting reviews)
- 'MCP Server: github-remote' is installed, please find it at [github-mcp-server](https://github.com/github/github-mcp-server)

## Files in this repo (overview)
- `dump-prs-information.ps1`: Fetches PRs for a milestone and outputs `milestone_prs.json` and `sorted_prs.csv`
  - CSV columns: `Id, Title, Labels, Author, Url, Body, CopilotSummary`
- `diff_prs.ps1`: Creates an incremental CSV by diffing two CSVs (in case more PRs cherry pick to stable)
- `MemberList.md`: Internal contributors list (used to decide when to add external thanks)
- `SampleOutput.md`: Example formatting for summary content

## Step-by-step
1) Run `dump-prs-information.ps1` to export PRs for the target milestone (initial run, CopilotSummary likely empty)
	- Open `dump-prs-information.ps1` and set:
	  - `$repo` (e.g., `microsoft/PowerToys`)
	  - `$milestone` (milestone title exactly as in GitHub, e.g., `PowerToys 0.94`)
	- Run the script in PowerShell; it will generate `milestone_prs.json` and `sorted_prs.csv`.

2) Request Copilot reviews for each PR listed in the CSV in Agent mode (don't generate any ps1)
	- Use MCP tools "MCP Server: github-remote" in current Agent mode to request Copilot reviews for all PR Ids in `sorted_prs.csv`.

3) After Copilot reviews complete, run `dump-prs-information.ps1` again
	- This refresh collects the latest Copilot review body into the `CopilotSummary` column in `sorted_prs.csv`.

4) (Optional) Use `diff_prs.ps1` for incremental rounds
	- Provide a prior CSV (`-BaseCsv`) and the latest CSV (`-AllCsv`) to produce an incremental CSV (`-OutCsv`).

5) Summarize PRs into per‑label Markdown files in Agent mode (Don't generate any ps1)
	- Group by the `Labels` column. For each unique label combination, create `{Label1}-{Label2}-Summary.md`.
	  - If multiple labels exist, join with `-` in alphabetical order. Remove or replace invalid filename characters (e.g., `/` `:` `?`).
	  - If a PR has no matching labels after filtering, put it in `Unlabeled-Summary.md`.
	- Each file has two parts:
	  1. Markdown list: one concise, user‑facing line per PR (no deep technical jargon). Use `Title`, `Body`, and `CopilotSummary` as sources.
		  - If `Author` is not in `MemberList.md`, append a "Thanks @handle!" per `SampleOutput.md`.
		  - If confidence is < 70%, write: `Human Summary Needed: <PR full link>`.
	  2. Three‑column table (in the same PR order):
		  - Column 1: The concise, user‑facing summary (the "cut version")
		  - Column 2: PR link
		  - Column 3: Confidence (e.g., `High/Medium/Low`) and the reason if < 70%

## Notes and conventions
- Do NOT generate/add new ps1 until instruct or ask why it need to add new ps1.
- Label filtering in `dump-prs-information.ps1` currently keeps labels matching: `Product-*`, `Area-*`, `Github*`, `*Plugin`, `Issue-*`.
- CSV columns are single‑line (line breaks removed) for easier processing.
- Keep PRs in the same order as in `sorted_prs.csv` when building summaries.
- Sanitize filenames: replace spaces with `-`, strip or replace characters that are invalid on Windows (`<>:"/\\|?*`).