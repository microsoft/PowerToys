## Background
This document describes how to collect pull requests for a milestone, request a GitHub Copilot code review for each, and produce release‑notes summaries grouped by label.

## Agent‑mode execution policy (important)
- By default, do NOT run terminal commands or PowerShell scripts beside the ps1 in this folder. Perform all collection, parsing, grouping, and summarization entirely in Agent mode using available files and MCP capabilities.
- Only execute existing scripts if the user explicitly asks you to (opt‑in). Otherwise, assume the input artifacts (milestone_prs.json, sorted_prs.csv, grouped_csv/*) are present or will be provided.
- Do NOT create new scripts unless requested and justified.

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
1) run `dump-prs-information.ps1` to export PRs for the target milestone (initial run, CopilotSummary likely empty)
	- Open `dump-prs-information.ps1` and set:
	  - `$repo` (e.g., `microsoft/PowerToys`)
	  - `$milestone` (milestone title exactly as in GitHub, e.g., `PowerToys 0.94`)
	- run the script in PowerShell; it will generate `milestone_prs.json` and `sorted_prs.csv`.

2) Request Copilot reviews for each PR listed in the CSV in Agent mode (MUST NOT generate or run any ps1)
	- Use MCP tools "MCP Server: github-remote" in current Agent mode to request Copilot reviews for all PR Ids in `sorted_prs.csv`.

3) run `dump-prs-information.ps1` again
	- This refresh collects the latest Copilot review body into the `CopilotSummary` column in `sorted_prs.csv`.

4) run `group-prs-by-label.ps1` to generate `grouped_csv/`

5) Summarize PRs into per‑label Markdown files in Agent mode (MUST NOT generate or run any script in terminal nor ps1)
    - Read the the csv files in the folder grouped_csv one by one
	- Generate the summary md file as the following instruciton in two parts:
	  1. Markdown list: one concise, user‑facing line per PR (no deep technical jargon). Use "Verbed" + "Scenario" + "Impact" as setence structure. Use `Title`, `Body`, and `CopilotSummary` as sources.
		  - If `Author` is NOT in `MemberList.md`, append a "Thanks @handle!" see `SampleOutput.md` as example.
		  - Do NOT include PR numbers or IDs in the list line; keep the PR link only in the table mentioned in 2. below, please refer to `SampleOutput.md` as example.
		  - If confidence to have enough information for summarization according to guideline above is < 70%, write: `Human Summary Needed: <PR full link>` on that line.
	  2. Three‑column table (in the same PR order):
		  - Column 1: The concise, user‑facing summary (the "cut version")
		  - Column 2: PR link
		  - Column 3: Confidence (e.g., `High/Medium/Low`) and the reason if < 70%

## Notes and conventions
- Terminal usage: Disabled by default. Do NOT run terminal commands or ps1 scripts unless the user explicitly instructs you to.
- Do NOT generate/add new ps1 until instructed (and explain why a new script is needed).
- Label filtering in `dump-prs-information.ps1` currently keeps labels matching: `Product-*`, `Area-*`, `Github*`, `*Plugin`, `Issue-*`.
- CSV columns are single‑line (line breaks removed) for easier processing.
- Keep PRs in the same order as in `sorted_prs.csv` when building summaries.
- Sanitize filenames: replace spaces with `-`, strip or replace characters that are invalid on Windows (`<>:"/\\|?*`).