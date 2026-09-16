# Notepad / MXC comparison — 2026-09-16

## Conclusion

The tested packaged Notepad fails outside the Try Run execution worker as well:
both a direct MXC .NET `Spawn`/`WaitAsync` call and the native `wxc-exec` CLI
return `0xC0000409`. This rules out CmdPal selection, the Try Run window, and
worker message handling as necessary causes of this failure.

The failure persists with the backend's default environment, with inherited
defaults plus the private profile, without denial capture, with read-only access
to the complete installed package and its dependencies, and with IME enabled.
The positive control, `winver.exe`, creates an observable window belonging to
the sandbox process and is then deliberately stopped.

The current ProcessContainer / Windows packaged-application compatibility path
is the remaining investigation area. These tests do not prove compatibility or
incompatibility for every MSIX application, every policy, or other MXC backends.
They also do not establish which individual denied access causes the abort.
An unrestricted host launch was not performed by this diagnostic run, and the
user's confirmation of normal host startup is still pending. If Notepad also
fails outside containment, its host installation must be investigated separately.

## Reproduction identity

- Windows 11 25H2, build **26200.9448**; the process reports OS version 10.0.26200.0.
- Notepad package **Microsoft.WindowsNotepad_11.2501.31.0_x64__8wekyb3d8bbwe**.
- Executable: `Notepad\Notepad.exe` inside that installed package.
- MXC checkout: **3eef7d60ce35d4d0ba568ddd0a9108beadb35b9a**; native version **0.8.0**.
- .NET SDK SHA-256: `8537A2C29472D2E7E289B5EBCC5B4364D91F9296B7D49B077061142D4134DBEE`.
- Native FFI SHA-256: `9328E81A1ECDD7CFDACB9AF74C2ED6D7A2D4C8E2DFE0DEEA677F84731E7A5B8A`.

The comparison driver verifies that its SDK and native library bytes match the
deployed worker. Each case gets a fresh private Try Run session. Direct calls
use the worker's `--describe-policy` output to obtain the same effective policy,
containment settings, working-directory choice and explicit environment.
Session paths and the capture output destination necessarily differ between runs.

## Results

| Case | Change from the baseline | Result |
|---|---|---|
| Try Run worker | Existing execution path | `0xC0000409` |
| Direct MXC SDK | Bypass worker execution and message handling | `0xC0000409`, no window observed |
| Direct SDK, capture off | Remove denial capture only | `0xC0000409`, no window observed |
| Direct SDK, default environment | Omit the explicit environment block | `0xC0000409`, no window observed |
| Direct SDK, inherited/private environment | Layer private profile overrides on backend defaults | `0xC0000409`, no window observed |
| Direct SDK, package read-only | Add installed package root, VCLibs, XAML and resource-package roots as read-only | `0xC0000409`, no window observed |
| Direct SDK, package read-only + inherited environment | Combine the preceding two changes | `0xC0000409`, no window observed |
| Direct SDK, IME | Enable only input-method access | `0xC0000409`, no window observed |
| Native MXC CLI | Equivalent schema-0.8 process/container configuration; validated with `--dry-run` first | `0xC0000409`, explicit packaged-app diagnostic |
| Direct SDK, winver | Substitute a normal Windows EXE and its installation-directory grant | Window observed; deliberately stopped (`0xFFFFFFFF`) |

All cases retain network denial, clipboard denial, no input injection, no host
system-settings access, no DACL mutation, and writable access only to their
private Work/Temp directories. No permissive learning/audit mode was used.
Read-only package additions and IME changes belong only to their named diagnostic
cases; they were not added to Try Run's defaults.

The `0xFFFFFFFF` positive-control result is the driver's explicit stop after
seeing a window, not an application-startup failure. The worker and CLI cases
record exit outcomes; window polling is performed only for direct SDK cases.
The diagnostic program returning zero means the experiments completed, not that
the target application succeeded.

## Diagnostic propagation finding

The native CLI emits a packaged-app explanation containing:

> Packaged apps cannot be launched inside a sandboxed container.

The relevant MXC implementation is:

- `src/backends/appcontainer/common/src/launch_diagnostics.rs`:
  `diagnose_process_exit`, `check_exe_heuristics`, and `is_packaged_app`.
- `src/backends/appcontainer/common/src/base_container_runner.rs`:
  `diagnose_exit` delegates to that shared diagnostic.
- `src/core/wxc_common/src/sandbox_process.rs`:
  the execute/finish adapter calls `diagnose_exit` for a nonzero exit and adds the
  explanation to the response's stderr/error message.
- `src/ffi/mxc_ffi/src/streaming.rs` and
  `sdk/dotnet/Microsoft.Mxc.Sdk/SandboxWaitResult.cs`:
  the spawn/wait path returns the exit code and timeout state without that
  post-exit diagnostic. Output metadata exposes capture results, not a launch
  diagnostic field.

The packaged-app detector is a **post-failure path heuristic** checking for a
`WindowsApps` path component. Its wording should not be treated as a formal
compatibility matrix for all MXC backends. Here it agrees with the real installed
package metadata and the repeated failures, and explains why the CLI gives a
more useful message than the streaming SDK path used by Try Run.

The block-mode captures also contain activation-store and AppModel registry
denials. They are useful evidence, but not a reason to grant broad system writes.
The native CLI's uninstall advice was not executed; the host's Notepad
installation and security settings were left in place.

## Artifacts and replay

The committed driver is in [Diagnostics/MxcAppComparison](Diagnostics/MxcAppComparison/README.md).
It was built with the repository build script, x64 Debug, exit code 0.

Local results:

- `x64/Debug/NotepadComparison-20260916-091635/`: the original nine-case matrix,
  including requests, effective policy snapshots, native captures, CLI validation,
  raw CLI diagnostics and `results.json`.
- `x64/Debug/NotepadImeFinal-20260916-092550/`: the completed IME comparison.

An earlier IME probe hit a diagnostic-observer race after the child exited.
The observer was fixed to tolerate exit between PID validation and lookup; the
completed rerun above records the actual MXC exit outcome. That infrastructure
failure is not counted as an application result.

Sessions are removed after each case. Saved request files are evidence and
contain the old session paths; use the driver to generate fresh paths for replay.
Reports contain local paths and user identifiers, so review them before sharing
outside this machine. No report or crash dump was uploaded.

## Recommended follow-up

1. Preserve MXC's post-exit diagnostic through the streaming SDK/FFI contract,
   then show it in Try Run. Keep the actual exit code and native evidence.
2. Present packaged-app compatibility before execution so an available context
   action is not mistaken for a validated runtime combination.
3. Investigate package activation/registry behavior at the ProcessContainer/OS
   layer, or separately evaluate a VM-based Windows backend. Do not attempt to
   make the demo work by allowing writes to the drive root or app repository.

No MXC implementation or production Try Run runtime policy was changed by this
comparison work.
