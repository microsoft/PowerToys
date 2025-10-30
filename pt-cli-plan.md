# PowerToys CLI Implementation Plan

## Goal
- Deliver the `ptcli` command-line experience described in `pt-cli.md`, with `Runner` acting as the single broker for module commands.
- Provide a maintainable architecture where modules self-describe commands, and CLI clients consume a uniform JSON/NamedPipe protocol.

## Workstreams

### 1. Broker Foundation (Runner)
- **Command Registry**: Implement `IModuleCommandProvider` registration on module load and persist `CommandDescriptor` metadata (schema, elevation flag, long-running hints, docs).
- **IPC Host**: Stand up the `\\.\pipe\PowerToys.Runner.CLI` NamedPipe server; define request/response DTOs with versioning (`v` field, correlation IDs).
- **Dispatch Pipeline**: Validate module/action, apply schema validation, enforce elevation policy, and invoke `ExecuteAsync`.
- **Response Envelope**: Normalize `status` (`ok|error|accepted`), payload, and error block (`code/message/details`). Emit diagnostic logging (caller, command, latency, result).

### 2. CLI Thin Client (`ptcli`)
- **Argument Parsing**: Support `ptcli -m <module> <action> [--arg value]`, plus `--list-modules`, `--list-commands`.
- **Transport**: Serialize requests to JSON, connect to the pipe with timeout handling, and deserialize responses.
- **Output UX**: Map standard errors to friendly text, show structured results, and support optional `--json` passthrough.
- **Async Jobs**: Handle `status=accepted` by printing job IDs, exposing `ptcli job status <id>` and `ptcli job cancel <id>` commands (polling via Runner endpoints).

### 3. Module Onboarding
- **Awake**: Implement `IModuleCommandProvider` returning `set/start/stop/list` commands. Adapt current APIs or legacy triggers inside `ExecuteAsync`.
- **Workspaces**: Provide `list/apply/delete` commands; wrap existing workspace manager calls. Ensure long-running operations flag `LongRunning=true`.
- **Legacy Adapters**: For modules still using raw events/pipes, add Runner-side shims that translate command invocations while longer-term refactors are scheduled.

### 4. Capability Discovery & Help
- **Describe APIs**: Expose Runner endpoints for `modules`, `commands`, parameter schemas, and elevation requirements.
- **CLI Help**: Use discovery data to render `ptcli help`, module-specific usage, and argument hints without duplicating metadata.

### 5. Reliability, Security, Observability
- **Security**: Configure pipe DACL to restrict access to the interactive user; enforce argument length/type limits.
- **Concurrency**: Process each request on a dedicated task; delegate concurrency limits to modules. Provide cancellation tokens from Runner.
- **Tracing**: Emit structured logs/ETW for requests, errors, and long-running progress notifications.
- **Error Catalog**: Implement standardized error codes (`E_MODULE_NOT_FOUND`, `E_ARGS_INVALID`, `E_NEEDS_ELEVATION`, `E_TIMEOUT`, etc.) and map module exceptions accordingly.

### 6. Elevation & Policies
- **Elevation Flow**: Detect when commands require elevation; if Runner is not elevated, return `E_NEEDS_ELEVATION` with actionable hints. Integrate with existing elevated Runner helper when available.
- **Policy Hooks**: Add optional checks for policy/experiment gates before command execution.

### 7. Progress & Notifications
- **Progress Channel**: Support incremental JSON progress messages over the same pipe or via job polling endpoints.
- **Timeouts/Retry**: Implement configurable `timeoutMs` handling and `E_BUSY_RETRY` responses for transient module lock scenarios.

### 8. Incremental Rollout Strategy
- **Phase 1**: Ship Runner pipe host + CLI client with two flagship commands (Awake.Set, Workspaces.List); document manual enablement.
- **Phase 2**: Migrate additional modules through adapters; add help/describe surfaces and job management.
- **Phase 3**: Enforce schema validation, finalize error catalog, and wire observability dashboards.
- **Phase 4**: Deprecate direct module NamedPipe/event entry points once CLI parity is achieved.

### 9. Documentation & Maintenance
- **User Docs**: Populate `pt-cli.md` with usage examples, elevation guidance, and troubleshooting mapped to error codes.
- **Developer Guide**: Add module author instructions for implementing `IModuleCommandProvider`, including schema examples and best practices.
- **Release Checklist**: Track new commands per release, update discovery metadata, and ensure CLI integration tests cover regression cases.

## Open Questions
- What tooling will maintain JSON schemas (hand-authored vs. source-generated)?
- Should progress streaming use duplex pipe messages or a per-job polling API?
- How will elevated Runner lifecycle be managed (reuse existing helper vs. new broker)?
- Which modules are in-scope for the first public preview, and what is the rollout schedule?
