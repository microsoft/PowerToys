# Try Run (experimental prototype)

Try a PowerShell script in an MXC ProcessContainer, inspect the output, and keep
selected results separately from the original files. This is an opt-in developer
prototype, not an installed PowerToy or a security boundary. MXC currently warns
that its profiles are not security boundaries.

## Milestones

Complete and validate each milestone before starting the next.

1. **Execution foundation — implemented and validated**: standalone window, Windows/privilege checks, isolated
   worker using the MXC C# SDK, bounded output, timeout and explicit stop; unit tests
   and a trusted-script smoke test.
2. **File workflow — not started**: copy selected input into a temporary workspace, reject links
   and oversized inputs, compare results, export to a new directory, clean up;
   filesystem unit tests and fuzz targets.
3. **Command Palette entry — not started**: an explicitly enabled developer command opens the
   window, without changing ordinary Run or existing module settings; build and
   launch validation.

Installer, Runner/GPO registration, automatic write-back, GUI executables,
network access, additional runtimes, and permission-learning UI are outside this
prototype. Production integration needs the normal PowerToys dependency, signing,
privacy and security reviews and resolution of MXC's preview limitations.

## Dependency

Use the separate Microsoft MXC checkout at commit
`4a941b0b` (the full revision is recorded in `mxc-version.txt`). Pass its absolute
root as `MxcRoot` when building the worker. The managed SDK and native libraries
are built together; do not mix releases. Rust 1.93 and the Windows C++ build tools
are required. No Node runtime is required. See the root `NOTICE.md` entry.

The prototype runs Windows PowerShell from the Windows system directory with
`-NoProfile -NonInteractive`. It never runs the script directly when MXC is
missing or unavailable. Only Windows 11 24H2+ is supported. Elevated sessions
are refused. Script text and output are not sent to telemetry.

PowerShell requires Windows UI initialization. The fixed policy therefore allows
windows and uses MXC's documented `Desktop` UI compatibility setting; clipboard,
input injection, system-settings changes and desktop system control stay blocked.
The window permissions are shown before execution. This is not a no-GUI policy.

## Validation record

Build from this directory using the repository scripts (replace the MXC path):

```powershell
& ..\..\..\tools\build\build.ps1 -Platform x64 -Configuration Debug -Path . -ExtraArgs /p:MxcRoot=C:/source/mxc
```

Run `x64\Debug\TryRun\PowerToys.TryRun.exe` from the repository root. The worker
and its matching native libraries must remain in the adjacent `Worker` directory.
The application does not need to run as administrator.

Build tests first using the command above, then run `vstest.console.exe` from the
same terminal. The integration tests run only when the worker path is supplied:

```powershell
$env:POWERTOYS_TRYRUN_WORKER = 'C:/source/PowerToys/x64/Debug/TryRun/Worker/PowerToys.TryRun.Worker.exe'
& '<Visual Studio>/Common7/IDE/CommonExtensions/Microsoft/TestWindow/vstest.console.exe' '../../../x64/Debug/tests/TryRun.UnitTests/TryRun.UnitTests.dll' /Platform:x64
```

Before enabling Run, the application executes a fixed, harmless workspace
write/read check. MXC platform discovery alone does not validate file grants.
Try Run never prepares the host automatically, never retries scripts with broader
permissions, and exits before running user code if it cannot enter the workspace.

The sandbox starts in a PowerShell drive named `TryRun:\`, rooted at the granted
workspace. Use relative paths, for example `Set-Content result.txt hello` and
`Get-Content result.txt`. This drive exists only inside the child PowerShell
session. It lets PowerShell normalize paths without inspecting ungranted parent
directories such as `C:\Users`.

Sessions resolve their newly created directory through a Windows file handle
before passing paths to MXC. Packaged development hosts can redirect AppData
creation to a package-private directory while reads of an existing parent use a
merged view. Resolving the unique session directory keeps the worker's grant and
the actual files aligned without changing host permissions.

Windows PowerShell may use Constrained Language under local security policy.
Try Run preserves that policy. The smoke tests use normal cmdlets, not unrestricted
.NET method invocation. PowerShell CLIXML diagnostics are decoded as bounded XML
with DTD/entity resolution disabled, and displayed as text.

The fuzz target is built with the solution. Its OneFuzz entry is disabled until
the production fuzzing owner and service configuration are assigned; no remote
fuzzing job is submitted by building this prototype.

Current verification: the standalone x64 Debug projects build with exit code 0.
All 24 tests pass: 16 unit cases, including the local fuzz smoke test (2,004
seeded/random inputs), and 8 opt-in native integration tests. The native tests
cover the startup workspace probe, file creation and relative navigation,
cross-session write denial, environment isolation, timeout, stop, and bounded
output. Results are in
`x64/Debug/tests/TryRun.UnitTests/TestResults/milestone1-fixed.trx`.
The first full PowerToys essentials build is blocked by missing MSVC Spectre
libraries in this development environment; the standalone prototype does not
require that full build. ARM64 has not been validated on hardware.

If an older window reports **Restricted workspace access is unavailable** with
Run disabled, save its script, close it, rebuild, and reopen the application.
The fixed version should show **Ready. Scripts run only when you choose Run.**
Try `Set-Content result.txt hello; Get-Content result.txt`; expect `hello` in the
output and exit code 0. Setting the time limit to 2 seconds and running
`Start-Sleep -Seconds 10` should report that the time limit was reached.
