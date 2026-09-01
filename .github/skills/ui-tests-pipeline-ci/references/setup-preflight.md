# Azure DevOps setup preflight

Read this reference only when `Test-AzureDevOpsSetup.ps1` fails or the user asks what the readiness
check proves. The normal CI workflow needs only the invocation and pass criterion in
[agentic-loop.md](agentic-loop.md).

## Capability checks

| Check | What a pass proves |
|---|---|
| `PowerShell7` | The script is running on supported PowerShell 7+ semantics. |
| `AzureCLI` | `az` is installed, executable, and reports a parseable version. |
| `AzureDevOpsExtension` | The `azure-devops` CLI extension is installed, so artifact-download commands are available. |
| `CachedSignInAndToken` | The existing account can mint an Azure DevOps resource token without prompting. |
| `ProjectRead` | The identity can access organization `microsoft` and project `Dart`. |
| `PipelineDefinitionRead` | The enabled `UI Test Automation` definition is visible and uniquely resolved. |
| `BuildRead`, `TimelineRead`, `BuildLogsRead`, `ArtifactsRead` | Build diagnostics and pipeline artifacts are readable. |
| `TestRunsRead`, `TestResultsRead`, `TestAttachmentsRead` | Azure Test evidence endpoints are readable. An attachment count of zero is still a successful permission check. |
| `PipelinePreview` | The Run Pipeline API accepts the identity, branch, parameters, and template expansion. `id=-1` proves no build was created. |
| `RepeatedPromptFreeRead` | A second token-backed call completes without another authentication prompt. |

The preflight deliberately does **not** create a run, cancel a build, or retry/cancel a stage. Those
mutations consume resources or change tracked work and are verified only when the user's request
authorizes the real operation. After every authorized write, re-read the build/timeline and require
the expected state transition; a successful HTTP response alone is not proof that a delayed stage
retry materialized.

Do not use `az devops security permission show` or Azure DevOps Graph-user enumeration as the setup
gate. Resolving the current Graph descriptor can require the unrelated `ReadExtended Users`
permission, which many valid pipeline users do not have. A failure there does not mean pipeline
access is missing. The endpoint capability checks above are the authoritative, least-privilege
readiness proof.

## Failure remediation

The agent never starts an interactive sign-in or installs tools. If preflight fails, stop and report
the exact failed check. The user performs any required setup outside the agent, then the agent reruns
the same preflight.

| Failed check | Required remediation |
|---|---|
| `PowerShell7` | Install/use PowerShell 7 and invoke the script with `pwsh`, not Windows PowerShell. |
| `AzureCLI` | Install Azure CLI and open a new shell where `az version` succeeds. |
| `AzureDevOpsExtension` | User runs `az extension add --name azure-devops`, then reruns preflight. |
| `CachedSignInAndToken` | User signs into the Microsoft tenant with Azure CLI outside the agent. Never pass credentials through chat or run `az login` from the agent. |
| `ProjectRead` | Confirm the signed-in identity is a Microsoft FTE with access to `microsoft/Dart`. A valid token alone is insufficient. |
| `PipelineDefinitionRead` | Confirm project access and that `UI Test Automation` still exists and is enabled. |
| Build/log/artifact/test read | Request the missing Azure DevOps project/build/test permission; do not weaken evidence requirements. |
| `PipelinePreview` | Confirm the branch exists, the probe module is valid, and the identity can use/queue pipeline `UI Test Automation`. No run was created. |

No `az devops configure` defaults, PAT, `AZURE_DEVOPS_EXT_PAT`, service connection secret, or local
credential file is required. Every helper call supplies organization/project explicitly and obtains
the Azure DevOps token from the existing Azure CLI cache. Rerun preflight after switching accounts,
after token-cache changes, or immediately after any `401`/`403` response.
