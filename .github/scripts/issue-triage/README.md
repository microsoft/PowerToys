# AI-assisted issue triage

The workflow in `.github/workflows/issue-triage.md` maintains one canonical
triage comment when an issue is opened or its original title/body is edited. It
combines deterministic preprocessing with one bounded GitHub Copilot pass.

## Rules

- Parse the issue template, PowerToys version, product area, reproduction
  quality, language, and diagnostic-report requirement before AI runs.
- Compare the reported version with the latest stable PowerToys release.
  Older versions receive a non-blocking update-and-retest recommendation.
- Retrieve a bounded set of older candidate issues using product labels,
  technical identifiers, and focused title/body searches.
- Ask Copilot only to summarize the issue, judge supplied duplicate candidates,
  interpret sanitized diagnostics, and classify the author-written language.
- Maintain one marked comment with separate sections for the issue author and
  the PowerToys team.
- Mention the author once and list only needed or recommended actions.
- Apply `Needs-Author-Feedback` when blocking information or an English
  translation is required. Removing the label disables scheduled closure.
- Add a matching primary `Product-*` label without removing existing product or
  maintainer labels.
- Never add, remove, or otherwise manage version labels.
- Submit duplicate closure as a native GitHub suggestion. A maintainer must
  accept or decline it; acceptance closes the issue as a duplicate and links
  it to the selected canonical issue.
- Never close an issue directly from the model output.
- Skip closed issues before agent execution and recheck their state immediately
  before publishing. Never reopen a closure that cannot be attributed to the
  workflow's duplicate-suggestion request.

## Reproduction and diagnostics

- Concrete actions plus an observed result are sufficient.
- Passive or intermittent failures can be sufficient when the timing/trigger
  and observed failure are clear.
- Non-English reproduction steps are reassessed after translation rather than
  treated as missing.
- Diagnostic reports are required for crashes, hangs, startup/load failures,
  installation/update failures, performance failures, and system-integration
  failures.
- Reports are optional for clear UI/visual defects and recommended for other
  actionable bugs.
- Report ZIP files are validated for path traversal, encryption, archive size,
  file count, and decompressed size. Only bounded redacted evidence reaches
  Copilot; raw archives are deleted and never uploaded as artifacts.

## Author-feedback lifecycle

`.github/policies/resourceManagement.yml` closes open issues and pull requests
that retain `Needs-Author-Feedback` for seven days without activity.

- An author comment removes `Needs-Author-Feedback` and returns the item to
  team triage.
- A push to a non-draft PR reruns PR intake, which recalculates
  `Needs-Author-Feedback`.
- Manual label removal immediately makes the item ineligible for scheduled
  closure.

## Cost and safety controls

- The `small` model alias is limited to five turns and 10 AI credits per run.
- The workflow subscribes only to issue creation and edits to the original
  issue; comments and reopen events do not trigger it.
- Per-user rate limits, daily AI-credit limits, and per-issue concurrency bound
  repeated issue creation or edits.
- The agent has no general shell or GitHub API tools. Its only shell command is
  the structured safe-output CLI proxy, and Copilot's file-write tool is
  explicitly denied to work around gh-aw v0.86.2 treating `edit: false` as
  writable.
- Threat detection fails closed; publication requires an explicit successful
  detection result.
- The publishing job rebuilds evidence from the current issue and accepts only
  deterministic product-label candidates, duplicate candidates, hashes, and
  classifications. Stale or manipulated model output fails before any write.
- A separate validated safe-output job owns comment, label, and
  duplicate-suggestion writes.

## Retired automation

- The GitHub Models-based automatic issue deduplicator is removed.
- The GitHub Models-based issue/PR area labeler is removed. This workflow
  replaces issue labeling; deterministic changed-path product labeling for pull
  requests is handled by `.github/workflows/pr-intake.yml`.
- The Azure Pipelines XAML Styler verification step is removed. The local
  `.pipelines/applyXamlStyling.ps1` developer tool remains available.

Run the focused tests with:

```console
python -m unittest discover .github\scripts\issue-triage\tests -v
```
