---
name: ui-tests-pipeline-ci
description: "Microsoft FTE-only workflow for queueing, monitoring, and stabilizing PowerToys UI Test Automation runs in Azure DevOps through the remote Azure DevOps MCP. Use after local default and constrained VM suites pass, when asked to run UITests CI, monitor an Azure pipeline, reuse a successful product build with specificBuildId, diagnose CI-only UI test failures, inspect recordings/artifacts, or manage the three-run stabilization limit. Keywords: FTE, Azure DevOps, pipeline, UI Test Automation, UITests CI, buildNow, specificBuildId, uiTestModules, failed test video, CI flake."
license: MIT
---

# PowerToys UI Tests Pipeline CI

Queue and stabilize the internal `UI Test Automation` Azure DevOps pipeline only after the target
UITest suite is proven locally. Use the remote Azure DevOps MCP for discovery, queueing, status,
logs, tests, and artifacts.

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
3. **One run at a time.** Before queueing, discover active runs for `UI Test Automation`. Wait for the
   current run to finish or cancel the relevant superseded run; never overlap runs and never cancel
   another person's unrelated run.
4. **Always scope modules.** `uiTestModules` must be non-empty and contain the exact current UITest
   project stem, for example `[FancyZonesEditor.UITests.Next]`.
5. **Three-run ceiling.** A CI stabilization sequence may queue at most three runs total. Keep an
   attempt ledger. If run 3 is not green, stop and ask the user for assistance. Also stop when three
   consecutive runs show no stabilization progress.
6. **Evidence before edits.** Read the failed result, logs, screenshot, and recording before forming
   a fix hypothesis. Preserve assertions and classify infrastructure failures separately.

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

- Remote MCP setup and authentication.
- Local-signoff and active-run preflight.
- `buildNow` versus `specificBuildId` decision rules.
- Exact queue parameters and branch targeting.
- Monitoring, failure evidence, and recording links.
- The three-run stabilization ledger and stop conditions.

## Completion standard

A task is complete only when one of these is true:

- The run is terminal `Succeeded`, all selected tests executed, and there are no failed, aborted,
  timed-out, error, or not-executed results.
- The run is terminal but failed, and the report includes the controlling failure, relevant logs,
  available recording/artifact links, and the next locally verified hypothesis.
- The three-run ceiling or no-progress rule was reached, and the agent stopped and asked the user for
  assistance with the full attempt ledger and evidence links.
