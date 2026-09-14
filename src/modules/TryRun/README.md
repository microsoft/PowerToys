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
2. **File workflow — implemented and validated**: copy selected input into a temporary workspace, reject links
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

### File workflow

1. Choose **Add files** or **Add folder**. Files are copied under their original
   names; a folder keeps its top-level name and contents. For example, selecting
   `notes.txt` makes it available as `TryRun:\notes.txt`. Selecting `Documents`
   makes its files available under `TryRun:\Documents\`. Duplicate top-level
   names are rejected rather than renamed or overwritten.
2. Enter a script using relative paths and choose **Run**. Every run gets fresh
   copies; changes to a previous run are not reused. Inputs are opened read-only.
3. Open **Review file changes**. SHA-256 content comparison labels each file as
   added, modified, deleted, or unchanged. Selecting a row shows the before/after
   text, limited to the first 8 KiB. UTF-8 and BOM-marked UTF-16 are supported;
   binary or unsupported encodings show an explanation instead. This is a file
   comparison with side-by-side previews, not a line-level diff editor.
4. New and modified files are checked by default. Check any unchanged files you
   also want, then choose **Export checked files** and a destination parent.
   Export creates a new `TryRun results <timestamp>-<id>` directory, preserves
   relative paths, and never overwrites existing files or applies deletions to
   originals. **Open export folder** opens the completed result location.
5. Export before starting another run or closing the window. A prompt protects
   unexported new/modified results. A failed or timed-out script can still have
   useful partial results; review them before exporting. **Refresh review** can
   retry a stopped review without running the script again.

Input and result scans allow at most 1,000 files/folders, 16 path components,
240 characters per relative path, 32 MiB per file, and 100 MiB total file content.
Links, junctions, hard-linked files, network/device paths, alternate-stream path
syntax, and reserved Windows names are rejected. Directory handles are held
during traversal and copying, and file handles deny writes while content is read.
Export rechecks the reviewed contents before copying. These are import/review/
export limits, not a disk quota on the running script.

Only primary file contents are copied; ACLs, alternate streams and original
timestamps are not carried over. Empty folders are copied on import but do not
have result rows and are not exported. Rename appears as deletion plus addition.
A canceled or failed export may leave partial copies in its newly created
directory; the error identifies it. Try Run never recursively deletes the
user-selected export destination. Unexported session files are cleaned up when
the window closes or a new run begins.

For a quick manual test, create `notes.txt` containing `original`, add it, then run:

```powershell
Set-Content notes.txt changed -NoNewline
Set-Content new.txt created -NoNewline
```

Expect one modified file and one added file, with `original`/`changed` in the
before/after preview. Export both and confirm the selected original still reads
`original`, while the new results folder contains `changed` and `created`.

### Build and tests

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
All 46 tests pass: 36 unit cases, including local fuzz smoke tests (2,004 protocol
inputs, 1,007 path/preview inputs and 48 file round trips), and 10 opt-in native
integration tests. The native tests
cover the startup workspace probe, file creation and relative navigation,
cross-session write denial, environment isolation, timeout, stop, and bounded
output, the complete file workflow, and export of partial results after timeout.
Results are in
`x64/Debug/tests/TryRun.UnitTests/TestResults/milestone2-full.trx`.
The M2 window was also checked for successful startup (**Ready**, Run enabled),
the input-file dialog, and the review/preview layout. Native file execution and
export are covered by integration tests; the complete sequence of GUI clicks
has not been automated.
The first full PowerToys essentials build is blocked by missing MSVC Spectre
libraries in this development environment; the standalone prototype does not
require that full build. ARM64 has not been validated on hardware.

If an older window reports **Restricted workspace access is unavailable** with
Run disabled, save its script, close it, rebuild, and reopen the application.
The fixed version should show **Ready. Scripts run only when you choose Run.**
Try `Set-Content result.txt hello; Get-Content result.txt`; expect `hello` in the
output and exit code 0. Setting the time limit to 2 seconds and running
`Start-Sleep -Seconds 10` should report that the time limit was reached.
