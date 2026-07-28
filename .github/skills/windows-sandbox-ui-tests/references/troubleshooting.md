# Troubleshooting Windows Sandbox UI tests

Classify the failure boundary before changing tests.

| Symptom | Boundary | Action |
|---|---|---|
| Black window, connection lost, no `progress.json` | Guest desktop never logged on | Treat as transient infrastructure, not a test failure. Stop the exact environment/session, wait for disposal, and retry Start-menu activation before sharing. Leave the persistent Store broker running. |
| `0x80070520` from `ExistingLogin` | No active guest user session | The controller stops a guest after 20 seconds without login and retries clean startup three times by default. Avoid split `wsb start` + `wsb connect` and pre-login large mappings; adjust `-LoginTimeoutSeconds`/`-StartupAttempts` only when needed. |
| "too many sessions established" | Overlapping guests/mapped shares | Enforce singleton execution; `wsb list --raw`, stop stale IDs, close their remote-session windows. |
| `wsb connect` never returns | Connection client owns the visible session | Do not synchronously redirect `wsb connect`; the default controller does not need it. |
| Sandbox starts but desktop stays clean | Guest command not dispatched or exited | Check request path, dynamic share, `ExistingLogin`, and first progress marker. Export guest process list with `wsb exec`. |
| `progress.json` sharing violation | Host/guest read-write race | Retry mapped JSON reads/writes; telemetry failure must not abort the test suite. |
| Zero tests, MTP exit code 8 | Invalid filter | Qualify display names: `Name=My.Test`, not `My.Test`. Verify with `--list-tests --filter ...`. |
| WebView/Monaco preview stays `Loading` | Missing WebView2 runtime | Stage signed `MicrosoftEdgeWebview2Setup.exe`, install `/silent /install`, then rerun. |
| Suite runs beyond its expected scope | Unbounded test or repeated readiness waits | Set guest/host limits for the selected scope, inspect live transcript/processes, and use shorter values for focused diagnosis. |
| UIA/input failures only in Sandbox | Integrity, foreground, first-run, or display difference | Read TRX diagnostics, foreground PID/title/elevation, module state, and `ui-tests-migration` CI-stability guidance. |
| Visual hash below threshold but functional workflow passed | Display/compositor mismatch | Preserve baseline/threshold, export both images, report resolution/DPI/theme/foreground and similarity as `ENVIRONMENT` or real visual failure. |
| Sandbox gone but warning window remains | Orphan `WindowsSandboxRemoteSession.exe` | Stop only the process associated with the completed/failed run; verify `wsb list` is empty. |
| Test processes use more CPUs than expected | Affinity disabled, invalid mask, or brokered process | Check `status.json` affinity fields and guest process affinities. Use `-ProcessorAffinityMask 0x3`; note that unrelated OS/shell-brokered processes are outside the runner tree. |
| Guest desktop is not 1920x1080 | Host work area too small, resize disabled, or RDP viewport did not converge | Check controller resize messages and `status.json`. Use `-DesktopWidth 1920 -DesktopHeight 1080`; both `0` disables sizing. Resize runs only after successful login. |
| Retained run reports missing/mismatched staging | Wrong Sandbox ID, exchange, or old manifest | Use the ID returned by the `-KeepSandbox` run and the same exchange. Let component hashes refresh changed archives; use a fresh run if the manifest predates reuse support. |
| Retained run hides a clean-profile failure | Guest settings/cache persisted between iterations | Treat reuse as an inner loop only; rerun in a fresh Sandbox for final validation. |

## Triage commands

### Host state

```pwsh
wsb list --raw

Get-CimInstance Win32_Process |
  Where-Object Name -match 'WindowsSandbox|vmmemWindowsSandbox' |
  Select-Object ProcessId,ParentProcessId,Name,CreationDate,CommandLine

Get-WinEvent -LogName 'Microsoft-Windows-Host-Network-Service-Admin' -MaxEvents 50 |
  Select-Object TimeCreated,Id,LevelDisplayName,Message
```

### Guest process snapshot

Once `ExistingLogin` works, use `wsb exec` to write diagnostics into the mapped result folder. Encode
complex PowerShell to avoid nested quoting errors:

```pwsh
$script = @'
Get-CimInstance Win32_Process |
  Where-Object Name -match 'UITests|PowerToys|winapp|explorer|powershell' |
  Select-Object Name,ProcessId,ParentProcessId,CreationDate,CommandLine |
  ConvertTo-Json -Depth 3 |
  Set-Content C:\SandboxExchange\diagnostics\processes.json -Encoding utf8
'@

$encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($script))
wsb exec --id $sandboxId --run-as ExistingLogin `
  --command "powershell.exe -NoProfile -EncodedCommand $encoded" --raw
```

### Live transcript

The guest template writes locally while tests run, then copies the transcript to the host. For a
suspected hang, copy the live file through `wsb exec` before terminating the test host:

```pwsh
wsb exec --id $sandboxId --run-as ExistingLogin --command `
  'cmd.exe /d /c copy /y C:\PowerToysSandboxRun\sandbox-ui-tests.log C:\SandboxExchange\diagnostics\live.log' --raw
```

Terminate only the test process after collecting evidence. Let the guest runner's `finally` export
status and artifacts, then stop the Sandbox.

## Visual-test caveat

Microsoft documents no direct Sandbox window-size configuration. The controller now drives the RDP
viewport indirectly and verifies 1920x1080 by default. For visual tests:

1. Log monitor geometry and DPI.
2. Require exact foreground ownership before capture.
3. Preserve composed WinUI/WebView capture behavior.
4. Export baseline and test images.
5. Keep valid per-platform baselines and the established similarity threshold.
6. Use a fixed CI/VM environment for final visual sign-off when Sandbox cannot match it.

Sandbox remains valuable for functional readiness and UIA workflows even when its pixels are not the
release visual authority.