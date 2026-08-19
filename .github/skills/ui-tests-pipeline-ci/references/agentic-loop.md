# Azure DevOps UI-test CI agentic loop

This is an internal post-local-validation workflow. It assumes the selected UITest project already
passed the complete local matrix required by
[ui-tests-local-vm](../../ui-tests-local-vm/SKILL.md), including the default and constrained
profiles.

## 0. Configure and authenticate the remote MCP

Use the VS Code **user-level** MCP configuration. Merge this server into the existing `servers`
object; do not delete unrelated servers:

```json
{
  "servers": {
    "azure-devops-remote": {
      "type": "http",
      "url": "https://mcp.dev.azure.com/microsoft"
    }
  },
  "inputs": []
}
```

The user file is normally `%APPDATA%\Code\User\mcp.json`. Start `azure-devops-remote` from the VS
Code MCP view and complete the Microsoft sign-in prompt. Open a new chat if the authenticated tools
do not appear in the current chat's tool catalog.

Confirm the server exposes tools for:

- Pipeline definitions, builds, logs, artifacts, and write operations.
- Test results by build ID and test results by test run ID.

Authentication must come from the remote MCP OAuth flow. Never request, print, store, or commit a
PAT, token, cookie, or credential file. If the signed-in account cannot list the `Dart` project,
stop with an FTE-access blocker.

## 1. Prove the local gate

Before touching Azure DevOps, record the local evidence for the current revision:

- The exact pushed branch and commit SHA.
- Clean x64 and ARM64 builds where applicable.
- The complete target suite on Windows 10 and Windows 11 under the default profile.
- The same complete suite under the `Constrained` profile (1 vCPU, 4 GB).
- The Windows 11 ARM64 guest on a Windows-on-ARM host when that matrix applies.
- Zero skipped, inconclusive, or not-executed tests; no evidence-export errors.

Do not substitute a narrow focused run for the required full local sign-off. If a required local
environment is unavailable, stop and ask the user before consuming CI.

Determine the exact project stem for `uiTestModules` from the current `.csproj` file name, without
`.csproj`. Examples:

```text
FancyZones.UITests.Next
FancyZonesEditor.UITests.Next
```

`uiTestModules` must never be empty. Include only the UITest project or projects currently being
worked on. Do not queue every UITest project as a convenience.

## 2. Discover the pipeline and serialize runs by branch

Use the remote MCP rather than assuming IDs:

1. List pipeline definitions in project `Dart` filtered to `UI Test Automation`.
2. Require exactly one enabled definition. Record its numeric ID. The known ID on 2026-08-17 was
   `161438`, but discovery remains authoritative.
3. List builds for that definition with `branchName=<exact refs/heads/... target ref>` and a stable
  descending `queryOrder`. Follow every returned `continuationToken` until the service returns no
  token; never infer that the branch is clear from only the first page of newer runs.
4. Inspect every returned build. Treat `NotStarted`, `InProgress`, `Postponed`, and `Cancelling` as
  active/nonterminal, and still require each run's `sourceBranch` to equal the exact target ref.

Only one `UI Test Automation` run for the target branch may be active at a time. Active runs on
other branches do not block queueing and may continue in parallel; do not adopt, monitor, or cancel
them as part of the current stabilization sequence.

Do not adopt a run merely because its `sourceBranch` matches. It is the current stabilization run
only when its `sourceVersion`, selected platforms, `uiTestModules`, `buildSource`, and reused build
ID (when applicable) exactly match the persisted checkpoint. A nonterminal same-branch run with any
other SHA or parameter remains a branch blocker, not an attempt in this sequence. If a same-branch
blocker or exact matching run exists, choose one path:

- **Wait:** Track an exact matching run, or wait for a mismatching same-branch blocker to become
  terminal without treating its result as evidence for this sequence.
- **Cancel:** Cancel only a relevant superseded run on the target branch that the user owns or
  explicitly asked to stop.
  Use `pipelines_write` with `update_build_stage` and `Cancel` when the exact active stage is known.
  If the MCP cannot identify/cancel the whole active run safely, use the linked Azure DevOps UI.
  Never cancel an unrelated run on another branch. Confirm the old run reports `status=Completed`
  with `result=Canceled` before queueing. A `Cancelling` run still occupies the target branch's
  one-run slot.

A preview run does not count as active because it creates no build. An actual queued run always
counts against the three-run ceiling, including an infrastructure-failed run.

## 3. Choose `buildNow` or `specificBuildId`

Use this decision table for every actual run:

| Situation | `buildSource` | `specificBuildId` |
|---|---|---|
| First CI run for the current stabilization sequence | `buildNow` | `xxxx` |
| Product/runtime/common/pipeline files changed since the prior run | `buildNow` | `xxxx` |
| Prior product build failed, was incomplete, or lacks a selected-platform artifact | `buildNow` | `xxxx` |
| Only the current UITest project file(s) changed, and the prior product build succeeded for every selected platform | `specificBuildId` | Prior run's numeric build ID |

For `specificBuildId`, prove all of the following:

1. The previous target-branch run is terminal; it is no longer the active serialized run for that
  branch.
2. Its product build stages succeeded for `x64` and `arm64`.
3. Pipeline artifact listing contains both `build-x64-Release` and `build-arm64-Release`.
  An artifact named `build-<platform>-Release-failure-<attempt>` is diagnostic output, not a
  reusable product build. One successful platform plus one failure artifact is a partial build and
  is ineligible for `specificBuildId`.
4. `git diff --name-only <previous-sourceVersion>..HEAD` contains changes only under the selected
   UITest project directory or directories. Be strict: a product, common runtime, pipeline template,
   dependency, or installer change requires `buildNow`.
5. The new branch revision is pushed.

Azure DevOps exposes two different identifiers:

- Build ID: `154921069` (numeric API ID; use this for `specificBuildId`).
- Build number: `20260814.4` (display label; never pass this as `specificBuildId`).

A run may fail in UI tests while still providing reusable successful product artifacts. Reuse is
allowed only after the product-build checks above pass.

## 4. Set the queue parameters

Use the current branch as the pipeline version. Target it through the run resource:

```json
{
  "repositories": {
    "self": {
      "refName": "refs/heads/<current-branch>"
    }
  }
}
```

Use these template parameters unless the user explicitly requests a supported variation:

| Parameter | Value |
|---|---|
| `buildPlatforms` | `- arm64\n- x64` |
| `enableMsBuildCaching` | `false` |
| `useVSPreview` | `false` |
| `useLatestWebView2` | `false` |
| `buildSource` | `buildNow` or `specificBuildId` from section 3 |
| `specificBuildId` | `xxxx` for `buildNow`; otherwise the prior numeric build ID as a string |
| `uiTestModules` | Non-empty bracketed list, e.g. `[FancyZonesEditor.UITests.Next]` |

For two modules currently being changed:

```text
[FancyZones.UITests.Next, FancyZonesEditor.UITests.Next]
```

Do not silently remove `arm64` or `x64`. The template expands `x64` into Windows 10 and Windows 11
jobs and runs the ARM64 job separately.

Call the remote MCP pipeline write tool with:

- `action=run_pipeline`
- `project=Dart`
- the discovered pipeline ID
- `resources` containing the current branch
- the parameter strings above

First use `previewRun=true` and inspect the expanded YAML/parameters. A preview does not consume one
of the three attempts. If the preview is correct and the active-run check is still clear, queue with
`previewRun=false`. Respect the MCP host's confirmation prompt; do not bypass it.

Immediately record the returned build ID, build number, web link, branch, and source version. Query
the new build by ID and require `sourceVersion` to equal the pushed SHA. If it differs, cancel the
run before tests and stop to resolve the branch race.

The active-run check and queue request are not atomic. Immediately after queueing, list all
nonterminal runs with `branchName=<exact ref>` and the same stable descending `queryOrder`, following
every `continuationToken` until null. Inspect every returned run's source version and template
parameters. If more than one exists, the oldest matching run owns the branch slot. If the run just
returned to this agent is younger, cancel that run, confirm it is terminal, and then either track the
older run when it exactly matches the checkpoint or wait for it as a blocker. If this agent's run is
the oldest, never cancel it merely because a younger run appeared and never cancel the younger run
owned by someone else; stop and ask the user to resolve the duplicate. If safe whole-run
cancellation is unavailable, stop and ask the user to resolve it in Azure DevOps. Every actual
queued run still consumes one attempt, including a duplicate canceled by this reconciliation.

### Persist the tracked-run checkpoint

Once a real run is queued, store this checkpoint in the agent's session/task state, never in the
repository:

```text
Pipeline: UI Test Automation / <definition ID>
Build: <build ID> / <build number> / <web link>
Branch: <exact refs/heads/... ref>
Source SHA: <40-character commit>
Attempt: <n>/3
Build source: <buildNow|specificBuildId> [reused build ID]
Platforms: arm64, x64
Modules: [<exact project stems>]
State: <NotStarted|InProgress|...>
```

Treat a nonterminal tracked run as unfinished work. Do not call a task-complete tool, remove the
monitoring item from the task list, or claim a terminal result. After any interruption, context
compaction, tool notification, or user message, the first Azure operation must query the
checkpoint's exact build ID. Do not rediscover the run by taking the latest build, because another
branch may have queued in parallel.

The remote Azure DevOps MCP status operations are pull-only: skill text cannot wake an agent after
the agent has ended its turn. True automatic continuation requires a host-provided, authenticated
completion primitive that blocks or subscribes to the exact build ID (for example, a future
`wait_for_completion` MCP action) and resumes the pending tool call when the build becomes terminal.
When such a primitive is available, arm exactly one waiter after verifying the source SHA, keep the
task non-final, and resume at section 5 when it fires.

When no completion primitive exists, do not claim that monitoring will resume automatically and do
not install a CLI, create a PAT, or add a webhook as a workaround. Preserve the checkpoint and keep
the monitoring task active. A concise pending-status handoff may end the current turn; state that
automatic wake-up is unavailable and re-enter section 5 from the exact build ID on the next agent
turn. This handoff is not task completion. Within an active turn, requery only after meaningful
stage progress; never busy-poll.

## 5. Monitor one run to completion

Keep one tracked build ID for this branch. Do not start another monitor or queue operation for that
branch in parallel. Runs on unrelated branches may continue independently.

Use the remote MCP in this order:

1. `pipelines_build` `list` by the exact build ID for status, result, branch, and source version.
2. `pipelines_build` `get_status` for issues and report metadata.
3. `pipelines_build_log` `list` to see job activity and newest log timestamps.
4. `pipelines_build_log` `get_content` with narrow line ranges for the relevant job or failure tail.
5. `testplan_show_test_results_from_build_id` for every non-passing outcome: `Failed`, `Aborted`,
   `Error`, `Timeout`, `NotExecuted`, `Inconclusive`, `Blocked`, `Warning`, `NotApplicable`, and
   `Paused`. Query all results when supported and classify every outcome other than `Passed`.
6. `pipelines_artifact` `list` for published pipeline artifacts and their authenticated links.

The build-level test tool enriches only the first 1,000 results per test run. When a run reaches that
limit or an error/stack field is missing, call `testplan_test_run` with `action=get_results`, the
returned `runId`, and `skip`/`top` pagination until all results are classified.

Do not busy-poll. Requery after meaningful stage progress or on the next agent turn. A long artifact
download with fresh log timestamps is progress, not a hang.

Status rules:

| State | Action |
|---|---|
| `NotStarted`, `InProgress`, `Postponed`, `Cancelling` | Continue monitoring; do not queue another run for the same branch |
| `Completed` + `Succeeded` | Verify selected tests actually executed and no non-passing outcomes exist |
| `Completed` + `PartiallySucceeded` | Treat as failure until every warning and test outcome is understood |
| `Completed` + `Failed` | Diagnose from tests, logs, screenshots, and recordings |
| `Completed` + `Canceled` | Record who/why; do not call it a product or test failure |
| `Completed` + `Abandoned` | Record as infrastructure/administrative termination; do not reuse it as a successful product build |

A green product build is not a green UI-test run. Success requires terminal `Succeeded`, selected
test execution, and no failed, aborted, timed-out, error, skipped, inconclusive, or not-executed
results.

## 6. Diagnose failures and link recordings

First determine whether the selected test stages actually ran. An empty failed-test query can mean
"no failures," but it can also mean a product build failed and every dependent test stage was
skipped.

### Product build failed before tests

For `buildNow`, classify each selected platform independently:

1. Read `pipelines_build get_status` for the platform/stage issue summary. Treat it as routing data;
  long issue text can be truncated, so the build log remains authoritative.
2. List build logs, locate the failed `Build Release_<platform>` job, and read a narrow tail around
  the first `##[error]`, MSBuild `N Error(s)` summary, or nonzero process exit. Report the first
  actionable compiler/MSBuild/tool error, not the later generic task-exit message. Do not promote
  ordinary warnings or final log-parser noise into the root cause.
3. List pipeline artifacts and compare names:
   - `build-<platform>-Release` means the normal product artifact was published.
   - `build-<platform>-Release-failure-<System.JobAttempt>` means the build job failed or was
    canceled and published output under its failed/canceled publication path.
4. Confirm dependent test-stage logs say `SucceededNode() = False` or otherwise show the stage was
   skipped. Do not report skipped-by-dependency tests as passing or failing tests.
5. Report that no test screenshot/video exists when no test result ran. Link the failure artifact
   and the failing build log instead.
6. Do not reuse that build ID with `specificBuildId` unless every selected platform has the normal
   successful product artifact. A successful ARM64 build does not compensate for a failed x64 build,
   or vice versa.

Failure-artifact size and contents depend on when the job failed because the pipeline publishes the
job output directory. Enumerate its relative tree before drawing conclusions. Look first under
`logs/` for MSBuild binary logs such as `build-*-main.binlog`, text error/warning logs, or other
tool-specific diagnostics. A failure artifact may contain only a `.binlog`, or it may also contain
partial staged outputs from a later failure. Never treat either shape as a reusable product artifact.

Use the artifact's MCP `resource.downloadUrl` in the report. The MCP `download` action is capped at
0.5 GB and may still return a transport error. If that happens, provide the authenticated
`downloadUrl`; when the Azure DevOps CLI plus its `azure-devops` extension are already installed and
authorized, an FTE may inspect it read-only in a temporary directory with:

```pwsh
az pipelines runs artifact download `
  --organization https://dev.azure.com/microsoft `
  --project Dart `
  --run-id <BUILD_ID> `
  --artifact-name <FAILURE_ARTIFACT_NAME> `
  --path <TEMP_DIRECTORY>
```

Never install extra diagnostic tools or commit downloaded artifacts as part of this workflow. Open
`.binlog` files with an already available MSBuild Structured Log Viewer; otherwise use the pipeline
log's first actionable error and give the user the binlog artifact link.

### UI test stages ran

Start with test results, not the top-level build message:

1. Query all non-passing results from the build.
2. Group by `(runId, resultId)`, then by failure signature. A title can repeat across platform jobs.
3. Read `errorMessage`, stack trace, duration, test assembly, and the relevant `Run UI Tests` log.
4. Open the screenshot and `recording_*.mp4` attachment before changing code.
5. Apply the same authoritative-signal analysis used by the local VM loop.

### Azure Test attachment links

The `.Next` harness calls `TestContext.AddResultFile`; `PublishTestResults@2` publishes those files as
**Azure Test result attachments**. They are not normally listed by `pipelines_artifact`.

The build-result MCP returns `runId` and result `id`. Construct one attachments-pane link for every
failed result:

```text
https://microsoft.visualstudio.com/Dart/_build/results?buildId=<BUILD_ID>&view=ms.vss-test-web.build-test-results-tab&runId=<RUN_ID>&resultId=<RESULT_ID>&paneView=attachments
```

Use a descriptive link label containing platform or run ID and test title, for example:

```markdown
[Win11 run 1625600849 - CreateWithDefaultName video/attachments](https://microsoft.visualstudio.com/Dart/_build/results?buildId=154921069&view=ms.vss-test-web.build-test-results-tab&runId=1625600849&resultId=100011&paneView=attachments)
```

Do not guess the platform from result ordering. Resolve it from the test job/log when possible; if
it remains unknown, label the link with the test run ID. Verify that the attachment pane contains a
recording before calling the link a video link. If capture failed, state that explicitly and still
link the screenshot/log attachments that exist.

### Pipeline artifact links

`pipelines_artifact list` returns `resource.downloadUrl`. Include those authenticated URLs when the
artifact itself is useful. Do not tell users to download a 16+ GB `build-*-Release` artifact to find
a test recording; recordings live under Azure Test attachments. The MCP artifact download operation
is capped at 0.5 GB, so provide its `downloadUrl` rather than attempting oversized downloads.

### Observed example: run `20260814.4`

Build `154921069` demonstrates the actual evidence shape:

- Six FancyZones Editor Create tests failed in each of three platform test runs: 18 failed result
  records total.
- Each record included `runId`, result `id`, title, error, stack, duration, and assembly.
- The errors grouped into missing `Save` and `Cancel` buttons.
- `pipelines_artifact list` contained only the large x64/ARM64 product artifacts.
- The recording and screenshot for each failure are reached through that result's Azure Test
  attachments-pane link.

This is why failure reports must preserve `(runId, resultId)` instead of deduplicating only by test
title.

## 7. Iterate, with a hard maximum of three runs

Maintain this ledger from the first queued run:

| Attempt | Build ID / number | Source SHA | Build source | Product build | Tests | Failure signature | Video links | Progress |
|---|---|---|---|---|---|---|---|---|
| 1/3 | | | `buildNow` | | | | | Baseline |
| 2/3 | | | | | | | | |
| 3/3 | | | | | | | | |

Every actual queued build consumes one attempt, including a run that fails before tests. Preview
runs do not count.

Before another attempt:

1. Name one falsifiable failure hypothesis from logs plus visual evidence.
2. Make the smallest relevant test fix; do not weaken assertions or change unrelated tests.
3. Rebuild and rerun the affected focused test locally.
4. Rerun the required full default and constrained local suites affected by the change.
5. Commit and push the revision.
6. Wait for or cancel the current CI run and confirm it is terminal.
7. Re-evaluate `buildNow` versus `specificBuildId` using section 3.

Count stabilization progress only when evidence improves, for example:

- Fewer failed tests or platforms.
- The original failing test now passes.
- The run reaches a later authoritative product/test state.
- A broad timeout becomes a narrower actionable assertion with new evidence.

Changing error text without improving the authoritative state is not progress.

After attempt 3, stop and ask the user for assistance if the run is not fully green, even if partial
progress occurred. Also stop if three consecutive runs show no progress. Report the ledger, exact
failure signatures, local evidence, build/test links, and every available recording link. Never
quietly queue attempt 4.

## 8. Report the result

Every status report must include:

- Build link, build ID, and display build number.
- Pipeline name/definition ID.
- Branch and exact source SHA.
- Attempt number out of 3.
- `buildSource`, reused numeric build ID if any, platforms, and `uiTestModules`.
- Current/terminal status and result.
- Per-platform test counts and non-passing tests.
- Root-cause groups with the first actionable error line.
- One Azure Test video/attachments link per failed result, or an explicit "tests did not run; no
  video exists" statement for a product-build failure.
- Relevant pipeline artifact links from MCP, without confusing product artifacts with recordings.
- Whether the next action is wait, local fix, retry, success, or user escalation.
