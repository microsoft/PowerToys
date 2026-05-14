---
name: pr-triage
description: Toolkit for triaging, categorizing, and prioritizing open pull requests. Use when asked to triage PRs, categorize stale PRs, prioritize pending reviews, identify abandoned PRs, suggest PR actions, find PRs needing attention, generate PR triage reports, analyze PR backlogs, or recommend next steps for pending PRs. Supports categorization by staleness, review status, build failures, design gaps, and suggests actionable next steps including reviewer assignment.
license: Complete terms in LICENSE.txt
---

# PR Triage Skill

Triage, categorize, and prioritize open pull requests. Generate reports with recommended actions per category.

## What to Do

Before running, confirm scope with the end-user first:

- Which PR numbers should be triaged?
- Which AI engine should be used (`copilot` or `claude`)?
- Should step 2 reviews be reused (`-SkipReview`) or regenerated?

Then run the orchestrator:

```powershell
.github/skills/pr-triage/scripts/Start-PrTriage.ps1 -PRNumbers <N1,N2,...>
```

It runs 5 steps sequentially, writing results to `<OutputRoot>/<date>/<label>/` (default `Generated Files/pr-triage/<date>/<label>/`). Re-running resumes from where it left off.

### Options

| Parameter | Default | Description |
|-----------|---------|-------------|
| `-Repository` | `microsoft/PowerToys` | GitHub repo to triage |
| `-PRNumbers` | — | PR numbers to triage (**required**) |
| `-ThrottleLimit` | `5` | Max concurrent parallel jobs |
| `-RunDate` | today | Date folder name (YYYY-MM-DD) |
| `-CliType` | `copilot` | AI engine: `copilot` or `claude` |
| `-RunLabel` | engine name | Subfolder label under run date |
| `-OutputRoot` | `Generated Files/pr-triage` | Root folder for triage run outputs |
| `-ReviewOutputRoot` | `Generated Files/prReview` | Folder used by Step 2 PR reviews |
| `-LogPath` | `triage.log` under run folder | Main orchestration log file (step logs are also created) |
| `-Force` | `false` | Re-run even if output exists |
| `-SkipAiEnrichment` | `false` | Skip step 3 (use rules only) |
| `-SkipReview` | `false` | Skip step 2 and reuse existing review outputs |

### Pipeline

```
1. Collect      → all-prs.json            (Get-OpenPrs.ps1)
2. Review       → prReview/<N>/           (Start-PRReviewWorkflow.ps1, parallel)
3. AI Enrich    → ai-enrichment.json      (Invoke-AiEnrichment.ps1, sequential)
4. Categorize   → categorized-prs.json    (Invoke-PrCategorization.ps1, parallel enrichment)
5. Report       → summary.md + cats/      (Export-TriageReport.ps1)
```

Each step produces a file. If the file exists on re-run, the step is skipped. Delete the file (or pass `-Force`) to redo it. Step 2 delegates to the pr-review skill. Step 3 can be skipped with `-SkipAiEnrichment` (Step 4 falls back to rule-based categorization).

### Delta Tracking

Step 5 automatically compares against the most recent previous run. The summary shows: new PRs, closed/merged PRs, category changes, and recurring action items still unresolved. Pass `-PreviousInputPath` to `Export-TriageReport.ps1` to compare against a specific run.

### Check Progress

```powershell
.github/skills/pr-triage/scripts/Get-TriageProgress.ps1 -Detailed
```

### Execution & Monitoring Rules

This pipeline takes **20–60 minutes** for 10 PRs. The agent MUST:

1. **Launch as a detached process** — VS Code terminal idle detection kills background processes after ~60s. Use `Start-Process -WindowStyle Hidden` with `Tee-Object` to a log file.
2. **Poll the log file every 60–120 seconds** until the pipeline prints the final `Triage complete!` line in `triage.log`.
3. **Track all 5 steps** — do NOT report success after Step 2 finishes; continue monitoring through Steps 3–5.
4. **On process death**, check `triage.log` and orchestrator logs, clean up partial output, and relaunch automatically.
5. **Report final results** only after `summary.md` is written — include category breakdown and quick-wins table.

## Step References (load per step)

| Step | Reference | Script |
|------|-----------|--------|
| 1 | [Collection](./references/step1-collection.md) | `Get-OpenPrs.ps1` |
| 2 | [Review](./references/step2-review.md) | `Start-PRReviewWorkflow.ps1` |
| 3 | [AI Enrichment](./references/step3-ai-enrichment.md) | `Invoke-AiEnrichment.ps1` |
| 4 | [Categorization](./references/step4-categorization.md) | `Invoke-PrCategorization.ps1` |
| 5 | [Reporting](./references/step5-reporting.md) | `Export-TriageReport.ps1` |

Read each reference only when executing that step.

## Scripts

| Script | Purpose |
|--------|---------|
| [Start-PrTriage.ps1](./scripts/Start-PrTriage.ps1) | **Run this** — full pipeline |
| [Get-TriageProgress.ps1](./scripts/Get-TriageProgress.ps1) | Check run status |
| [Get-OpenPrs.ps1](./scripts/Get-OpenPrs.ps1) | Step 1: Collect |
| [Invoke-AiEnrichment.ps1](./scripts/Invoke-AiEnrichment.ps1) | Step 3: AI enrichment (dimension scoring) |
| [Invoke-PrCategorization.ps1](./scripts/Invoke-PrCategorization.ps1) | Step 4: Categorize |
| [Export-TriageReport.ps1](./scripts/Export-TriageReport.ps1) | Step 5: Report |
| [Get-PrDetails.ps1](./scripts/Get-PrDetails.ps1) | Utility: detailed PR enrichment |
| [Get-ReviewerSuggestions.ps1](./scripts/Get-ReviewerSuggestions.ps1) | Utility: suggest reviewers |

## Dependencies

| Skill | Used For |
|-------|----------|
| `parallel-job-orchestrator` | Parallel AI CLI execution in Step 2 (reviews) and Step 3 (enrichment) |
| `pr-review` | Step 2 PR reviews — delegates to `Start-PRReviewWorkflow.ps1` |

Both `Start-PRReviewWorkflow.ps1` and `Invoke-AiEnrichment.ps1` delegate parallel
execution to the shared orchestrator. Do NOT introduce custom `ForEach-Object -Parallel`,
`Start-Job`, or `Start-Process` patterns — use the orchestrator instead.

## Post-Execution Review

After each triage run, review results and tighten instructions when needed:

1. Verify run artifacts under `Generated Files/pr-triage/<date>/<label>/` are complete.
2. Validate category distribution and sampled actions in `summary.md` for plausibility.
3. Compare `ai-enrichment.json` success/failure counts and investigate unusual failure spikes.
4. If criteria or prompts produced noisy outcomes, update the relevant step reference in [references](./references).
5. If script parameters or behavior changed, keep this `SKILL.md` options/workflow in sync in the same PR.
