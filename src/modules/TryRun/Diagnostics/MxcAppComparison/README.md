# MXC application comparison driver

Developer-only, bounded comparisons of the Try Run worker, direct MXC .NET SDK,
and native MXC CLI. Use a trusted installed application when investigating local
compatibility. This tool is not included in the product build or installer.

The deployed worker supplies the pinned SDK/runtime binaries. TryRun.Core is
used to create and clean up private sessions, encode the worker request and
identify the child process. The direct SDK execution path does not call the Try
Run worker or its execution/configuration-mapping implementation; it deserializes
the previously requested policy snapshot and calls `MxcSandbox.Spawn` itself.

From this directory:

```powershell
../../../../../tools/build/build.ps1 -Platform x64 -Configuration Debug -ExtraArgs @('/restore')
```

For another runtime location, pass `/p:MxcRuntimeRoot=<absolute worker directory>`
at build time. The driver checks that the supplied worker's SDK/FFI hashes match
its own copies, and selects that native library directory for the process.

After a successful build, from the repository root:

```powershell
$app = Join-Path (Get-AppxPackage Microsoft.WindowsNotepad).InstallLocation 'Notepad\Notepad.exe'
$worker = (Resolve-Path 'x64/Debug/TryRun-Policies/Worker').ProviderPath
$report = Join-Path (Get-Location) ('x64/Debug/NotepadComparison-' + [Guid]::NewGuid().ToString('N'))
& './x64/Debug/tests/MxcAppComparison/MxcAppComparison.exe' $worker $app $report
```

An optional fourth argument selects one case: `worker`, `sdk-identical`,
`sdk-no-capture`, `sdk-default-environment`, `sdk-inherit-private-environment`,
`sdk-package-readonly`, `sdk-package-readonly-inherit`, `sdk-ime`,
`cli-identical`, or `sdk-winver-control`.

Each case has a 12-second sandbox time limit. A visible direct-SDK child window
is stopped by the driver after its PID/creation-time identity is checked. No
keyboard or mouse input is sent. Captures stay in block mode. The driver refuses
network access, DACL mutation, learning mode, extra capabilities or writable
paths outside its own Work/Temp directories. It refuses an existing report
directory. Read-only dependency roots come from installed-package metadata.

`results.json` is written after every case. A zero driver exit code means there
was no harness error; inspect each case's `ExitCode`, `ExitHex`, `TimedOut` and
`WindowObserved` to determine application behavior. `WindowObserved: null` means
that entry point was not polled for windows. Expected application incompatibility
can therefore coexist with a successful diagnostic run.

The captures and snapshots intentionally retain local resource identifiers.
They are not uploaded automatically. Session directories are cleaned up, so
replay through the driver rather than directly replaying a stale JSON file.

See [the recorded Notepad comparison](../../NOTEPAD-MXC-COMPARISON.md).
