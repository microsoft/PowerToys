# Try Run (experimental prototype)

Try a PowerShell script in an MXC ProcessContainer, inspect the output, and keep
selected results separately from the original files. This is an opt-in developer
prototype, not an installed PowerToy or a security boundary. MXC currently warns
that its profiles are not security boundaries.

## Milestones

Complete and validate each milestone before starting the next.

1. **Execution foundation — implemented, native verification blocked**: standalone window, Windows/privilege checks, isolated
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
The AppContainer + DACL backend may need the elevated setup documented in MXC
`docs/host-prep.md`, but that is not a confirmed remedy for the native-backend
failure described below. Do not change host ACLs based solely on this failure.

Windows PowerShell may use Constrained Language under local security policy.
Try Run preserves that policy. The smoke tests use normal cmdlets, not unrestricted
.NET method invocation. PowerShell CLIXML diagnostics are decoded as bounded XML
with DTD/entity resolution disabled, and displayed as text.

The fuzz target is built with the solution. Its OneFuzz entry is disabled until
the production fuzzing owner and service configuration are assigned; no remote
fuzzing job is submitted by building this prototype.

Current verification: the standalone x64 Debug solution builds with exit code 0.
All 16 unit tests pass, including the local fuzz smoke test (2,004 seeded/random
inputs). The opt-in native workspace test fails with exit code 125: the wrapper
cannot enter the explicitly granted workspace and stops before the user script.
Results are under `x64/Debug/tests/TryRun.UnitTests/TestResults/` as
`milestone1-units.trx` and `milestone1-native.trx`.
The first full PowerToys essentials build is blocked by missing MSVC Spectre
libraries in this development environment. Milestone 1's real workspace test is
blocked by native filesystem access on the current host. Do not consider the
milestone complete or advance to file import/export before that test passes.

On this host (Windows reports 26200; `processmodel.dll` is 10.0.26100.9444), MXC
reports BaseContainer/PSEC support and no DACL augmentation requirement. A CLI
reproduction with `fallback.allowDaclMutation=false` still receives access denied
when writing to the explicitly granted scratch directory. The older SBOX path
also reproduces it. A policy-only volume-root read grant and a separate low-label
diagnostic scratch directory did not resolve it. No system-drive root permissions
were changed. This is evidence of an unresolved native access failure, not proof
of an OS defect or a reason to grant broad filesystem access.
