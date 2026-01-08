agent: 'agent'
model: GPT-5.1-Codex-Max
description: 'Resolve Code scanning / check-spelling comments on the active PR.'
---

**Goal:** Clear every outstanding GitHub pull request comment created by the `Code scanning / check-spelling` workflow by explicitly allowing intentional terms.

**Guardrails:**
- Update only discussion threads authored by `github-actions` or `github-actions[bot]` that mention `Code scanning results / check-spelling`.
- Resolve findings solely by editing `.github/actions/spell-check/expect.txt`; reuse existing entries.
- Leave all other files and topics untouched.

**Prerequisites:**
- Install GitHub CLI if it is not present: `winget install GitHub.cli`.
- Run `gh auth login` once before the first CLI use.

**Workflow:**
1. Determine the active pull request with a single `gh pr view --json number` call (default to the current branch).
2. Fetch all PR discussion data once via `gh pr view --json comments,reviews` and filter to check-spelling comments authored by `github-actions` or `github-actions[bot]` that are not minimized; when several remain, process only the most recent comment body.
3. For each flagged token, review `.github/actions/spell-check/expect.txt` for an equivalent term (for example an existing lowercase variant); when found, reuse that normalized term rather than adding a new entry, even if the flagged token differs only by casing. Only add a new entry after confirming no equivalent already exists.
4. Add any remaining missing token to `.github/actions/spell-check/expect.txt`, keeping surrounding formatting intact.