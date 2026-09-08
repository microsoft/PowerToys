# Azure DevOps UI-test CI agentic loop

This is an internal post-local-validation workflow. It assumes the selected UITest project already
passed the complete local matrix required by
[ui-tests-local-vm](../../ui-tests-local-vm/SKILL.md), including default and constrained profiles.

## 0. Use the existing Azure CLI session

All Azure DevOps operations in this workflow use PowerShell 7, the existing Azure CLI sign-in, and
Azure DevOps REST APIs. This avoids per-call authentication prompts and works for definitions,
builds, timelines, logs, preview/queue, stage retry/cancel, test results, artifacts, and result
attachments.

### Required one-command readiness gate

Before the first Azure operation in each agent session, run the preflight in a fresh PowerShell 7
process:

```pwsh
pwsh -NoLogo -NoProfile -File `
  .github\skills\ui-tests-pipeline-ci\scripts\Test-AzureDevOpsSetup.ps1
```

Do not proceed unless it exits `0`, reports `Ready: true`, and every required check is `PASS`. The
default probe uses `refs/heads/main`, module `FancyZones.UITests.Next`, and dynamically selects one
of the ten newest completed pipeline builds for build/log/artifact checks plus the first test-bearing
build in that set for Azure Test checks. The JSON reports these separately as `ProbeBuildId` and
`ProbeTestBuildId`. It creates no build and changes no Azure or repository state.

Use explicit probe inputs when diagnosing a particular branch or known build:

```pwsh
pwsh -NoLogo -NoProfile -File `
  .github\skills\ui-tests-pipeline-ci\scripts\Test-AzureDevOpsSetup.ps1 `
  -ProbeBranch refs/heads/<branch> `
  -ProbeModule <Module.UITests> `
  -ProbeBuildId <KNOWN_COMPLETED_BUILD_ID>
```

For the check inventory, precise capability claims, and first-time remediation, read
[setup-preflight.md](setup-preflight.md) only when setup fails or the user asks about readiness.
The preflight deliberately performs no mutation.

The agent never starts an interactive sign-in or installs tools. If preflight fails, stop and report
the exact failed check. The user performs any required setup outside the agent, then the agent reruns
the same preflight. Never pass credentials through chat or run `az login` from the agent.

### Use the REST helper after preflight

Dot-source the bundled helper for actual work only after preflight passes:

```pwsh
. .\.github\skills\ui-tests-pipeline-ci\scripts\AzureDevOps.ps1
```

The helper obtains a token for Azure DevOps resource
`499b84ac-1321-427f-aa17-267ca6975798` on each request. Azure CLI serves it from its cache and
silently refreshes it when needed. The token and authorization header stay in memory and are cleared
after every request. Repeated reads and actual pipeline queueing were verified without prompts on
2026-08-20.

Never run `az login` through an agent, request credentials, print or persist a token/header, enable
command tracing around authentication, or commit downloaded internal evidence. If
the preflight fails, ask the user to resolve its exact failed check and stop with an access blocker.
Do not fall back to another transport.

`Invoke-AzDevOpsRest` accepts a project-relative REST path and returns `{ Body, Headers }`:

```pwsh
$build = (Invoke-AzDevOpsRest -Uri '_apis/build/builds/123?api-version=7.1').Body
```

`Get-AzDevOpsPagedValues` follows every `x-ms-continuationtoken` response header. Use it whenever an
endpoint can paginate; never infer completeness from one page.

REST writes mutate Azure state. Queue, cancel, retry, or approve only when the user's CI request
authorizes that action. The absence of a per-call authentication dialog is not authorization.

## 1. Prove the local gate

Before touching Azure DevOps, record:

- Exact pushed branch and commit SHA.
- Clean x64 and ARM64 builds where applicable.
- Complete suite on Windows 10 and Windows 11 under the default profile.
- Complete suite under `Constrained` (1 vCPU, 4 GB).
- Windows 11 ARM64 guest evidence on a Windows-on-ARM host when applicable.
- Zero skipped, inconclusive, or not-executed tests and zero export errors.

Do not substitute a focused run for full sign-off. If a required local environment is unavailable,
stop and ask the user before consuming CI.

Derive `uiTestModules` from the exact `.csproj` filename without `.csproj`, for example:

```text
FancyZonesEditor.UITests.Next
```

The list must be non-empty and contain only the projects currently being changed.

## 2. Discover the pipeline and serialize the branch

Discover rather than assuming IDs:

```pwsh
$branch = 'refs/heads/<current-branch>'
$definitionName = [Uri]::EscapeDataString('UI Test Automation')
$definitions = (Invoke-AzDevOpsRest -Uri `
  "_apis/build/definitions?name=$definitionName&api-version=7.1").Body.value
$enabled = @($definitions | Where-Object queueStatus -EQ 'enabled')
if ($enabled.Count -ne 1) {
  throw "Expected one enabled UI Test Automation definition; found $($enabled.Count)."
}
$pipelineId = [int]$enabled[0].id

$encodedBranch = [Uri]::EscapeDataString($branch)
$history = Get-AzDevOpsPagedValues -Uri `
  "_apis/build/builds?definitions=$pipelineId&branchName=$encodedBranch&queryOrder=queueTimeDescending&%24top=100&api-version=7.1"
$active = @($history.Items | Where-Object status -IN @('notStarted', 'inProgress', 'postponed', 'cancelling'))
```

Require every returned build's `sourceBranch` to equal the exact target ref. Normally only one run
for that branch may be active. Other branches do not block queueing and must not be canceled.

Adopt an active run only when its source SHA, selected platforms, modules, `buildSource`, and reused
build ID exactly match the checkpoint. A mismatching same-branch run is a blocker. Wait, or cancel
only a superseded run that the user owns or explicitly asked to stop:

```pwsh
Invoke-AzDevOpsRest `
  -Uri "_apis/build/builds/${buildId}?api-version=7.1" `
  -Method Patch `
  -Body @{ status = 'cancelling' }
```

Confirm it becomes terminal `completed`/`canceled`; `cancelling` still occupies the branch slot.

A narrow exception allows parallel same-branch execution only when the user explicitly authorizes a
supplemental run for an architecture whose product build failed while another architecture remains
active. Use the same pushed SHA and modules, select only the failed architecture, and checkpoint both
build IDs. The supplemental run remains part of the attempt ledger and never erases the original
failure.

## 3. Choose `buildNow` or `specificBuildId`

| Situation | `buildSource` | `specificBuildId` |
|---|---|---|
| First run in the sequence | `buildNow` | `xxxx` |
| Product/runtime/common/pipeline files changed | `buildNow` | `xxxx` |
| Prior product build failed, was incomplete, or lacks one selected-platform artifact | `buildNow` | `xxxx` |
| Only selected UITest project files changed and every selected product artifact succeeded | `specificBuildId` | Prior numeric build ID |

For reuse, prove all of the following:

1. Previous target-branch run is terminal.
2. Product build stages succeeded for every selected platform.
3. Artifact names include normal `build-x64-Release` and/or `build-arm64-Release` artifacts as
   selected. `build-<platform>-Release-failure-<attempt>` is diagnostic and never reusable.
4. `git diff --name-only <previous-sourceVersion>..HEAD` is confined to selected UITest project
   directories. Any shared, product, pipeline, dependency, or installer change requires `buildNow`.
5. The revision is pushed.

Use numeric build IDs such as `154921069`, never display numbers such as `20260814.4`.

## 4. Preview and queue

Default parameters:

| Parameter | Value |
|---|---|
| `buildPlatforms` | `- arm64\n- x64` |
| `enableMsBuildCaching` | `false` |
| `useVSPreview` | `false` |
| `useLatestWebView2` | `false` |
| `buildSource` | Decision from section 3 |
| `specificBuildId` | `xxxx` or prior numeric build ID as a string |
| `uiTestModules` | Bracketed non-empty list, e.g. `[KeyboardManager.UITests]` |

Do not silently remove a platform. `x64` expands to Windows 10 and Windows 11 jobs; `arm64` expands
to the ARM64 job.

Preview first with the exact branch and string-valued template parameters:

```pwsh
$templateParameters = @{
  buildPlatforms = "- arm64`n- x64"
  enableMsBuildCaching = 'false'
  useVSPreview = 'false'
  useLatestWebView2 = 'false'
  buildSource = 'buildNow'
  specificBuildId = 'xxxx'
  uiTestModules = '[KeyboardManager.UITests]'
}
$request = @{
  previewRun = $true
  resources = @{ repositories = @{ self = @{ refName = $branch } } }
  templateParameters = $templateParameters
}
$preview = (Invoke-AzDevOpsRest `
  -Uri "_apis/pipelines/${pipelineId}/runs?api-version=7.1-preview.1" `
  -Method Post `
  -Body $request).Body
```

Inspect `finalYaml`. Require only the expected `Build_<platform>` and test stages, exact module
assignments, no unrequested platform, and no prior build ID for `buildNow`. Preview creates no real
build and does not consume an attempt.

Immediately repeat the full branch preflight from section 2. If clear, queue by changing only:

```pwsh
$request.previewRun = $false
$run = (Invoke-AzDevOpsRest `
  -Uri "_apis/pipelines/${pipelineId}/runs?api-version=7.1-preview.1" `
  -Method Post `
  -Body $request).Body
```

Record `id`, `name`, web link, branch, resolved repository version, and echoed parameters. Query the
returned build ID and require `sourceVersion` to equal the pushed SHA. If it differs, cancel before
tests and stop.

Queue and active-run discovery are not atomic. Immediately list every active exact-branch run again,
following all continuation tokens. The oldest matching run owns the normal branch slot. Cancel a
younger run created by this agent when safe; never cancel someone else's run. Every actual queue,
including a reconciled duplicate, counts in the ledger.

### Persist the checkpoint

Store in session/task state, never the repository:

```text
Pipeline: UI Test Automation / <definition ID>
Build: <build ID> / <build number> / <web link>
Branch: <exact refs/heads/... ref>
Source SHA: <40-character commit>
Attempt: <n>/3
Build source: <buildNow|specificBuildId> [reused build ID]
Platforms: <selected platforms>
Modules: [<exact project stems>]
State: <notStarted|inProgress|...>
```

After any interruption or user turn, query every exact checkpointed build ID before other Azure
work. Never rediscover by adopting the newest run.

### Agent-owned foreground completion waiter

After queueing and checkpointing, invoke the bundled waiter synchronously in the foreground:

```pwsh
pwsh -NoLogo -NoProfile -File `
  .github\skills\ui-tests-pipeline-ci\scripts\Wait-AzureDevOpsBuild.ps1 `
  -BuildId <BUILD_ID> `
  -ExpectedBranch <EXACT_REFS_HEADS_BRANCH> `
  -ExpectedSourceVersion <40_CHARACTER_SOURCE_SHA>
```

For an explicitly authorized supplemental architecture run, pass both checkpointed IDs to one
invocation, for example `-BuildId 123456789,123456790`. Every ID must share the expected branch and
source SHA.

Run the setup preflight first. Keep the waiter attached to the active agent turn; do not use an
async/background mode, end the response, or call `task_complete` while it runs. The waiter validates
identity on every read, polls every 120 seconds, requests system sleep prevention, and returns on
terminal status, timeout, or a genuine query failure.

Read the waiter's JSON-lines `Event`: `progress` is a successful nonterminal read, `query-error` is a
retryable read failure, `terminal` means every build completed, and `timeout` ends the bounded wait.
Require at least one `progress` or `terminal` record before attributing a nonzero exit to Azure DevOps;
otherwise diagnose the waiter invocation itself. On `terminal`, immediately query timeline, logs,
tests, and artifacts and apply the completion standard. If it times out while builds are progressing,
invoke another bounded foreground wait. For authentication or repeated query errors, rerun the setup
preflight and follow its blocker rules. After a failed terminal run, diagnose, fix, rerun local gates,
queue the next authorized attempt, and invoke a new waiter.

This mechanism relies on a client that keeps a synchronous shell tool call attached to the active
agent turn. Copilot CLI in autopilot mode supports that execution model. If the current client cannot
do so, state that limitation before queueing.

## 5. Monitor exact builds

Use these endpoints through `Invoke-AzDevOpsRest`:

| Evidence | REST path |
|---|---|
| Build status/source | `_apis/build/builds/<BUILD_ID>?api-version=7.1` |
| Stage/job timeline and issues | `_apis/build/builds/<BUILD_ID>/timeline?api-version=7.1` |
| Log metadata | `_apis/build/builds/<BUILD_ID>/logs?api-version=7.1` |
| Narrow log content | `_apis/build/builds/<BUILD_ID>/logs/<LOG_ID>?startLine=<N>&endLine=<N>&api-version=7.1` |
| Pipeline artifacts | `_apis/build/builds/<BUILD_ID>/artifacts?api-version=7.1` |
| Test runs for build | `_apis/test/runs?buildUri=vstfs%3A%2F%2F%2FBuild%2FBuild%2F<BUILD_ID>&api-version=7.1` |
| Paged test results | `_apis/test/Runs/<RUN_ID>/results?%24top=1000&%24skip=<N>&api-version=7.1` |

Read build status, then timeline, newest log timestamps, relevant narrow logs, every non-passing test
result, and artifacts. Page test results by `$skip` until fewer than 1,000 remain. Classify every
outcome other than `Passed`, including failed, aborted, error, timeout, not-executed, inconclusive,
blocked, warning, not-applicable, and paused.

Do not busy-poll. Fresh timeline/log timestamps are progress.

| REST status/result | Action |
|---|---|
| `notStarted`, `inProgress`, `postponed`, `cancelling` | Continue monitoring exact ID |
| `completed` + `succeeded` | Verify selected tests executed and every result passed |
| `completed` + `partiallySucceeded` | Treat as failure until warnings/results are understood |
| `completed` + `failed` | Diagnose logs, tests, screenshots, and recordings |
| `completed` + `canceled` | Record who/why; not a product/test failure |
| `completed` + `abandoned` | Infrastructure/administrative termination |

### Retry or cancel one stage

Use the YAML stage key such as `Build_x64`, not its display name. Retry only a terminal failed stage:

```pwsh
$stageRefName = 'Build_x64'
Invoke-AzDevOpsRest `
  -Uri "_apis/build/builds/${buildId}/stages/${stageRefName}?api-version=7.1-preview.1" `
  -Method Patch `
  -Body @{ state = 'retry'; forceRetryAllJobs = $false }
```

REST success is not proof that retry started. Re-read the timeline until the stage/job attempt
increments and becomes pending/in-progress. Retry materialization can be delayed. Do not queue a
supplemental run until that check establishes the update was rejected or remained a no-op.

Use `state = 'cancel'` to cancel one known stage. Use the build PATCH from section 2 for the whole
run. Re-read status after every mutation.

## 6. Diagnose failures and collect evidence

First determine whether selected test stages ran. An empty failed-result query can also mean a
product build failed and dependent tests were skipped.

### Product build failed before tests

For each selected platform:

1. Read stage/job timeline issues as routing data.
2. Locate `Build Release_<platform>` and read a narrow log tail around the first `##[error]`, compiler
   error, MSBuild error summary, test failure, or nonzero exit. Report the first actionable cause,
   not the later generic task-exit message.
3. Compare artifacts:
   - `build-<platform>-Release` is the normal product artifact.
   - `build-<platform>-Release-failure-<attempt>` is diagnostics only.
4. Confirm dependent test stages are skipped.
5. State that no screenshot/video exists when no test ran.
6. Never reuse a partial product build with `specificBuildId`.

Download a useful failure artifact to a temporary directory with the already-installed extension:

```pwsh
az pipelines runs artifact download `
  --organization https://dev.azure.com/microsoft `
  --project Dart `
  --run-id <BUILD_ID> `
  --artifact-name <FAILURE_ARTIFACT_NAME> `
  --path <TEMP_DIRECTORY> `
  --only-show-errors
```

Do not install tools or commit evidence. Inspect text logs first; open `.binlog` only with an already
available Structured Log Viewer.

### UI test stages ran

1. Query all non-passing results and preserve `(runId, resultId)` because titles repeat by platform.
2. Resolve platform from the owning job/log, never result order.
3. Read error, stack, duration, assembly, and `Standard_Console_Output.log`.
4. Open failure screenshot and `recording_*.mp4` before changing code.
5. Apply the authoritative-signal analysis from the local VM loop.

Construct an attachments-pane link for every failed result:

```text
https://microsoft.visualstudio.com/Dart/_build/results?buildId=<BUILD_ID>&view=ms.vss-test-web.build-test-results-tab&runId=<RUN_ID>&resultId=<RESULT_ID>&paneView=attachments
```

### Download result attachments without browser help

Azure Test result attachments are separate from pipeline artifacts. List and download them directly:

```pwsh
$buildId = <BUILD_ID>
$runId = <RUN_ID>
$resultId = <RESULT_ID>
$destination = Join-Path $env:TEMP "PowerToys-CI-$buildId-$runId-$resultId"
New-Item -ItemType Directory -Path $destination -Force | Out-Null

$resultBase = "_apis/test/Runs/${runId}/Results/${resultId}"
$attachments = (Invoke-AzDevOpsRest `
  -Uri "$resultBase/attachments?api-version=7.1-preview.1").Body.value
$selected = @($attachments | Where-Object {
  $_.fileName -eq 'Standard_Console_Output.log' -or
  $_.fileName -like 'failure-*.png' -or
  $_.fileName -like 'recording_*.mp4'
})
if (-not ($selected.fileName -contains 'Standard_Console_Output.log')) {
  throw 'Standard_Console_Output.log is missing from the failed result.'
}

foreach ($attachment in $selected) {
  $target = Join-Path $destination ([IO.Path]::GetFileName([string]$attachment.fileName))
  Invoke-AzDevOpsRest `
    -Uri "$resultBase/Attachments/$($attachment.id)?api-version=7.1-preview.1" `
    -OutFile $target | Out-Null
  [pscustomobject]@{
    AttachmentId = $attachment.id
    FileName = Split-Path $target -Leaf
    Bytes = (Get-Item $target).Length
    Sha256 = (Get-FileHash $target -Algorithm SHA256).Hash
    Path = $target
  }
}
```

Read the console log and relevant product logs directly, inspect visual evidence, and record bytes
plus SHA-256. Never ask the user to fetch files that this path can download.

Pipeline artifact metadata contains `resource.downloadUrl`; include useful authenticated links in
reports. Do not confuse multi-gigabyte product artifacts with Azure Test recordings.

## 7. Iterate, maximum three runs

Maintain this ledger:

| Attempt | Build ID / number | Source SHA | Build source | Product build | Tests | Failure signature | Evidence links | Progress |
|---|---|---|---|---|---|---|---|---|
| 1/3 | | | `buildNow` | | | | | Baseline |
| 2/3 | | | | | | | | |
| 3/3 | | | | | | | | |

Every actual queued build counts, including infrastructure failures and canceled duplicates. Preview
runs do not. A supplemental architecture run also counts unless the user explicitly authorizes an
exception after seeing the ledger.

Before another attempt:

1. State one falsifiable hypothesis from logs and visual evidence.
2. Make the smallest relevant fix without weakening assertions.
3. Build and rerun the focused test locally.
4. Rerun affected full default and constrained suites.
5. Commit and push.
6. Confirm current run terminal or explicitly handle the supplemental-architecture exception.
7. Re-evaluate section 3.

Progress means fewer failures/platforms, the original test passing, a later authoritative state, or
a broad timeout narrowed to actionable evidence. Error-text churn is not progress. After run 3 or
three no-progress runs, stop and ask the user unless they explicitly authorize an exception.

## 8. Report the result

Include:

- Build link, numeric ID, display number, pipeline ID, branch, and exact SHA.
- Attempt number, `buildSource`, reused ID, platforms, and modules.
- Current/terminal status and per-platform counts.
- Every non-passing test and first actionable root-cause line.
- One attachments link per failed result, or an explicit no-test/no-video statement.
- Useful pipeline artifact links without confusing them with recordings.
- Whether next action is wait, local fix, retry, success, or escalation.