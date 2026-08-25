---
name: ui-tests-pipeline-ci
description: "Microsoft FTE-only workflow for queueing, monitoring, and stabilizing PowerToys UI Test Automation runs through an existing Azure CLI session and Azure DevOps REST APIs. Use after local default and constrained VM suites pass, when asked to run UITests CI, monitor a pipeline without repeated authentication prompts, reuse a successful product build with specificBuildId, diagnose CI-only UI test failures, download recordings/artifacts, or manage the three-run stabilization limit. Keywords: FTE, az, Azure CLI, Azure DevOps, pipeline, UI Test Automation, UITests CI, buildNow, specificBuildId, uiTestModules, failed test video, CI flake."
license: MIT
---

# PowerToys UI Tests Pipeline CI

Queue and stabilize the internal `UI Test Automation` Azure DevOps pipeline only after the target
UITest suite is proven locally. Use the existing Azure CLI sign-in plus Azure DevOps REST APIs for
discovery, preview, queueing, status, timelines, logs, tests, artifacts, and result attachments.

> [!IMPORTANT]
> **Microsoft FTE only.** This workflow requires authorized access to the `microsoft` Azure DevOps
> organization and the `Dart` project. External contributors stop after local validation and report
> CI as unavailable. Never store credentials, PATs, tokens, or internal artifact contents in the
> repository.

## When to use

Use this skill when an authorized Microsoft FTE asks to:

- Queue PowerToys UITests in the internal `UI Test Automation` pipeline.
- Monitor a UITests pipeline run or summarize its stages, tests, and artifacts.
- Iterate on a failure that passed the complete local VM matrix.
- Reuse a prior successful product build while rebuilding only one or more UITest projects.
- Find and share failed-test screenshots, logs, or recording links.

Do not use this skill for local execution. Complete
[ui-tests-local-vm](../ui-tests-local-vm/SKILL.md) first. Use
[ui-tests-migration](../ui-tests-migration/SKILL.md) for test implementation and stabilization.

## Non-negotiable gates

1. **Local first.** Do not queue CI until all required local runs are green, including full suites on
   the default and `Constrained` profiles for Windows 10 and Windows 11, plus the applicable
   architecture builds/guests required by `ui-tests-local-vm`.
2. **Pushed revision.** Queue only a pushed branch. Record its exact commit and verify the queued
   run's `sourceVersion` matches it.
3. **One run per branch.** Before queueing, discover active runs for `UI Test Automation`. Wait for
   or cancel a relevant superseded run on the target branch; runs on other branches may continue in
   parallel. Never cancel another branch's unrelated run.
4. **Always scope modules.** `uiTestModules` must be non-empty and contain the exact current UITest
   project stem, for example `[FancyZonesEditor.UITests.Next]`.
5. **Three-run ceiling.** A CI stabilization sequence may queue at most three runs total. Keep an
   attempt ledger. If run 3 is not green, stop and ask the user for assistance. Also stop when three
   consecutive runs show no stabilization progress.
6. **Evidence before edits.** Read the failed result, logs, screenshot, and recording before forming
   a fix hypothesis. Preserve assertions and classify infrastructure failures separately.
7. **Tracked runs remain unfinished work.** After queueing, persist the build ID, branch, source SHA,
   attempt number, and parameters in session/task state. Do not mark the task complete or claim a
   terminal result while that build is nonterminal. If no authenticated completion waiter exists,
   arm the one-hour scheduled continuation in the agentic loop rather than relying on a passive
   handoff. On every scheduled wake, resume, notification, or user turn that continues the tracked CI
   task, query that exact build ID before other Azure work and continue from its current state.

## Internal constants

| Setting | Value |
|---|---|
| Azure DevOps organization | `microsoft` |
| Project | `Dart` |
| Pipeline name | `UI Test Automation` |
| Current known definition ID | `161438` (discover by name each session; do not blindly hardcode) |
| Platforms | `arm64`, `x64` |
| Default booleans | `enableMsBuildCaching=false`, `useVSPreview=false`, `useLatestWebView2=false` |

## Required workflow

Read and execute [references/agentic-loop.md](./references/agentic-loop.md) from top to bottom. It
contains:

- Prompt-free Azure CLI session validation and the bundled
   [REST helper](./scripts/AzureDevOps.ps1).
- Local-signoff and active-run preflight.
- `buildNow` versus `specificBuildId` decision rules.
- Exact queue parameters and branch targeting.
- Monitoring, failure evidence, direct Azure Test attachment downloads, and recording links.
- Tracked-run continuation, one-hour scheduled polling, and completion-notification limits.
- The three-run stabilization ledger and stop conditions.

## Completion standard

A task is complete only when one of these is true:

- The run is terminal `Succeeded`, all selected tests executed, and there are no failed, aborted,
  timed-out, error, or not-executed results.
- For a monitor-only request, the run is terminal but failed, and the report includes the controlling
   failure, relevant logs, and available recording/artifact links.
- For a stabilization request, a terminal failed run is intermediate work. Continue the agentic loop
   until a later attempt succeeds, a genuine blocker prevents the next verified attempt, or the
   three-run/no-progress ceiling is reached.
- The three-run ceiling or no-progress rule was reached, and the agent stopped and asked the user for
  assistance with the full attempt ledger and evidence links.
