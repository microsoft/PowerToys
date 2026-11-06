# Errors and validation

The consumer owns prompts, progress, cancellation, and saving. Do not launch
unsolicited script UI.

- Validate semantic preconditions before side effects; schemas cannot validate a service/document.
- Stdout contains only the declared result; stderr contains actionable, non-secret diagnostics.
  Exit nonzero on failure, never return empty/fabricated success.
- PowerShell: `$ErrorActionPreference = 'Stop'`; immediately check `$LASTEXITCODE`
  for native tools. Avoid incidental pipeline output.
- Python: `subprocess.run(..., check=True, timeout=...)`; catch expected failures
  only to add context, then exit nonzero/re-raise. Preserve unexpected tracebacks.
- Bound I/O/waits. Launching a detached process is not completion.
- Publish only existing, closed, unique workspace outputs. Clean only your own
  temporary files; keep returned files alive for the consumer.
- Prefer idempotent side effects. Report partial completion; timeout/cancellation
  does not prove rollback. Check actual state before any approved retry.

## Verify at the right layer

Discovery is non-executing validation, not proof of behavior. Inspect Host exit,
stderr, schemas, and configuration errors. A partial catalogue or parsed JSON
response can coexist with failure.

After consent/trust, test through the Host with `--consumer command-line`.
Check actual output/side effects plus relevant boundaries, malformed domain
input, dependency failure, encoding, and paths. Verify the consumer separately.
Do not disrupt real services or weaken policy/trust to manufacture test cases.

| Symptom | Check |
|---|---|
| Missing script | Consumer installed/enabled; current input/output compatibility, counts/types, runtime, configuration errors |
| Missing option | Defaults and consumer collection; never invent values or use `Read-Host` |
| Trust/content refusal | User review/approval of current code and descriptor; `--no-consent` refuses, not bypasses |
| Policy/enablement refusal | Explain allowed configuration path; never edit JSON/registry or change caller ID to escape |
| Missing interpreter/tool/import | Actual Windows/distro/image dependencies; no implicit install/fallback |
| Invalid/empty output | Literal text versus JSON envelope, stdout contamination, MIME/array shape, existing files; AP rejects empty results |
| Access/network/desktop denied | Runtime/sandbox feasibility, not implicit policy changes |
| Timeout/cancel/partial export | Report known state and published files; no automatic retry |
| Managed runtime not ready | Settings recovery guidance; no automatic WSL restart/replay |

Activity (`activity list`/app tab) stores bounded invocation metadata, not
stdout/stderr. Interrupted means neither confirmed cancellation nor rollback.
Keep test diagnostics separately without sensitive input or credentials.
