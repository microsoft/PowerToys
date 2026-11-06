# Make failure explicit without corrupting results

Design this together with input/output, not after writing the happy path.
The script reports its operation; the Host validates/trust-gates execution; the
consumer owns progress, cancellation UI, parameter prompts, and result saving.
Do not add an unsolicited popup or launch another UI from a background script.

## Script rules

- Validate task-specific preconditions before side effects: provider/profile,
  allowed input content, destination assumptions, dependencies, and API responses.
  Host schema validation is not semantic validation of a document or service.
- Reserve stdout for the declared result. Text output is literal text, not
  `{"text":"..."}`; files/media output is only the corresponding JSON object.
  No-result actions should write no stdout. Put useful, non-secret diagnostics
  on stderr; never print passwords, tokens, full input documents, or credential
  command lines.
- Exit zero only when the requested operation succeeded. On failure, write an
  actionable explanation to stderr and exit nonzero. Do not return an empty
  string, `{}`, or a fabricated successful result instead of reporting failure.
- In PowerShell, set `$ErrorActionPreference = 'Stop'` for cmdlet failures.
  External executables still require an immediate `$LASTEXITCODE` check; do not
  assume the preference handles native failures on every supported version.
  Avoid accidental pipeline output from helper calls.
- In Python, catch expected operational exceptions when adding useful context;
  write to `sys.stderr` and use `sys.exit(1)` or re-raise. Do not catch everything
  and continue. Preserve tracebacks when an unexpected failure needs diagnosis.
- Bound external I/O and waits; report unavailable services. Do not launch
  detached work and claim success merely because the process started.
- Use unique temporary/workspace outputs. Publish the result only after all
  intended output files exist. Clean up only your own temporary files, never
  delete returned files before the consumer reads them.
- For side effects, prefer idempotent "ensure state". Report partial completion
  and do not automatically retry after timeout/cancellation or an ambiguous
  failure; work may already have happened.

For example, this is a failure path, not a substitute for the requested task:

```powershell
try {
    # Perform the task with terminating cmdlet errors and check native exit codes.
}
catch {
    [Console]::Error.WriteLine("The requested operation failed: " + $_.Exception.Message)
    exit 1
}
```

```python
try:
    # Perform the task; use subprocess.run(..., check=True, timeout=...) for tools.
    ...
except (OSError, ValueError) as error:
    print(f"The requested operation failed: {error}", file=sys.stderr)
    sys.exit(1)
```

Review exception messages for sensitive data before exposing them. Adapt catches
to the actual API's failure types; don't wrap unrelated code just to suppress it.

## Separate validation from execution

Before requesting a test, inspect the generated code and descriptor, then
validate discovery with `--include-disabled --include-unconfigured`. Nonzero
Host exits and registry warnings on stderr are failures to investigate, even
when a partial catalogue or result JSON exists. Never infer successful execution
from an exit-zero discovery command.

After explicit execution consent and normal trust requirements, test through the
Host using `--consumer command-line`, not by running the script/interpreter
directly. Inspect the process exit code, stdout, stderr, output shape, and actual
result. A structured transform response can accompany a nonzero exit; parsing
JSON is not proof of success. Verify the selected consumer separately.

Choose relevant cases rather than executing destructive scenarios:
normal input; empty/malformed domain content; boundary file counts/types; missing
dependencies or external-service failure; and correct encoding/path behavior.
Never weaken policy, change trust, interrupt a real service, or perform a host
side effect just to create a failure fixture.

## Diagnose the right layer

| Symptom | Check and next action |
|---|---|
| Script absent from a consumer | Consumer installation/enablement, compatible current input, output support, file/media bounds, runtime, and configuration errors. Management discovery is not the consumer's matching predicate. |
| Missing required option | Descriptor defaults and the actual consumer's parameter collection. Do not silently invent a value or add `Read-Host`. |
| Trust refusal or content changed | Stop; have the user review and explicitly approve the current code/descriptor. `--no-consent` means refuse rather than prompt, never bypass trust. |
| Disabled module/integration or managed policy | Show the reason and permitted Settings path. Do not edit JSON/registry or switch consumer IDs to escape a gate. |
| Interpreter/import/tool not found | Match Windows, user WSL distribution, or managed image and its dependencies. Do not auto-install or fall back to another environment. |
| Invalid output or empty AP result | Remove diagnostics from stdout; check literal text versus structured file/media JSON, MIME/array shape, existing output paths, and useful result content. |
| Access/network/desktop denied | Compare the task with global sandbox restrictions; propose a feasible alternative, not a silent policy change. |
| Timeout, cancellation, or partial export | Report what is known, retain published results, and check actual state before any user-approved retry. |
| Managed Linux image/session not ready | Use Settings readiness/recovery guidance. Do not automatically restart the user's WSL distributions or replay the script. |

The standalone editor's Activity tab and `activity list` show bounded invocation
metadata, not captured stdout/stderr. An interrupted Host is reported as
Interrupted, not proof of cancellation or rollback. Retain the current test's
diagnostics as needed, without logging sensitive content or claiming Activity
contains details it does not store.
