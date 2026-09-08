---
name: ui-tests-pipeline-ci
description: "Microsoft FTE-only workflow for validating setup, queueing, autonomously waiting for, and stabilizing PowerToys UI Test Automation through an existing Azure CLI session and Azure DevOps REST APIs. Use after local VM suites pass, when asked to run UITests CI, perform a setup preflight/readiness check, diagnose repeated az login prompts or 401/403 permission failures, reuse a successful build, inspect recordings/artifacts, or manage the three-run limit. Includes an agent-owned foreground completion waiter. Keywords: FTE, az, Azure CLI, Azure DevOps, pipeline, UI Test Automation, UITests CI, buildNow, specificBuildId, uiTestModules, CI flake."
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
- Validate Azure CLI and Azure DevOps readiness before queueing or after a `401`/`403` response.
- Monitor a UITests pipeline run or summarize its stages, tests, and artifacts.
- Iterate on a failure that passed the complete local VM matrix.
- Reuse a prior successful product build while rebuilding only one or more UITest projects.
- Find and share failed-test screenshots, logs, or recording links.

Do not use this skill for local execution. Complete
[ui-tests-local-vm](../ui-tests-local-vm/SKILL.md) first. Use
[ui-tests-migration](../ui-tests-migration/SKILL.md) for test implementation and stabilization.

## Non-negotiable gates

1. **Setup preflight first.** Before the first Azure operation in a session, run
   [Test-AzureDevOpsSetup.ps1](./scripts/Test-AzureDevOpsSetup.ps1) and require `Ready=true` with
   every required check `PASS`. It performs reads and a non-mutating pipeline preview only. Re-run it
   after account changes or any `401`/`403` response.
2. **Local first.** Do not queue CI until all required local runs are green, including full suites on
   the default and `Constrained` profiles for Windows 10 and Windows 11, plus the applicable
   architecture builds/guests required by `ui-tests-local-vm`.
3. **Pushed revision.** Queue only a pushed branch. Record its exact commit and verify the queued
   run's `sourceVersion` matches it.
4. **One run per branch.** Before queueing, discover active runs for `UI Test Automation`. Wait for
   or cancel a relevant superseded run on the target branch; runs on other branches may continue in
   parallel. Never cancel another branch's unrelated run.
5. **Always scope modules.** `uiTestModules` must be non-empty and contain the exact current UITest
   project stem, for example `[FancyZonesEditor.UITests.Next]`.
6. **Three-run ceiling.** A CI stabilization sequence may queue at most three runs total. Keep an
   attempt ledger. If run 3 is not green, stop and ask the user for assistance. Also stop when three
   consecutive runs show no stabilization progress.
7. **Evidence before edits.** Read the failed result, logs, screenshot, and recording before forming
   a fix hypothesis. Preserve assertions and classify infrastructure failures separately.
8. **Tracked runs remain unfinished work.** After queueing, persist the build ID, branch, source SHA,
   attempt number, and parameters in session/task state. Do not mark the task complete or claim a
   terminal result while that build is nonterminal. Immediately run
   [Wait-AzureDevOpsBuild.ps1](./scripts/Wait-AzureDevOpsBuild.ps1) synchronously in the foreground,
   bound to the exact build ID, branch, and source SHA. Keep the same agent turn alive until the
   waiter returns, then verify the terminal result and continue stabilization without user input.
   Do not end the turn or call `task_complete` while the waiter runs.

## Internal constants

| Setting | Value |
|---|---|
| Azure DevOps organization | `microsoft` |
| Project | `Dart` |
| Pipeline name | `UI Test Automation` |
| Current known definition ID | `161438` (discover by name each session; do not blindly hardcode) |
| Azure DevOps token resource | `499b84ac-1321-427f-aa17-267ca6975798` |
| Required setup check | `scripts/Test-AzureDevOpsSetup.ps1` |
| Required completion waiter | `scripts/Wait-AzureDevOpsBuild.ps1` |
| Platforms | `arm64`, `x64` |
| Default booleans | `enableMsBuildCaching=false`, `useVSPreview=false`, `useLatestWebView2=false` |

## Required workflow

Read and execute [references/agentic-loop.md](./references/agentic-loop.md) from top to bottom. It
contains:

- The required prompt-free
   [setup preflight](./scripts/Test-AzureDevOpsSetup.ps1) and bundled
   [REST helper](./scripts/AzureDevOps.ps1).
- Local-signoff and active-run preflight.
- `buildNow` versus `specificBuildId` decision rules.
- Exact queue parameters and branch targeting.
- Monitoring, failure evidence, direct Azure Test attachment downloads, and recording links.
- Agent-owned foreground completion waiting and truthful client capability limits.
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
