# Command Palette integration

## Actions on built-in app and file results

The native CmdPal Apps and Indexer providers now include **Open in Try Run**
(**在 Try Run 中打开** in Simplified Chinese) in the selected result's action menu.
The default Enter/Open action stays unchanged. This applies to app search,
indexed file search, direct-path file results and the folder browser.

- For Win32 apps, the action uses CmdPal's resolved executable path and shortcut
  arguments. EXE, PowerShell, CMD and BAT are supported. It never activates the
  shortcut through the host shell. UWP/AUMID and URL activation are not supported.
- Packaged desktop applications, including Windows Notepad, also expose the
  action when their manifest declares `Windows.FullTrustApplication` and a local
  executable within the package. The manifest's EXE is passed to Try Run directly.
  Apps that require package activation rather than direct EXE startup may still
  fail during execution; the launcher does not fall back to host activation.
  **Known limitation:** Notepad 11.2501.31.0 starts a child process but exits with
  `0xC0000409` under the pinned MXC policy on this machine. See the diagnostics
  section below; menu availability does not mean runtime compatibility.
- Files and folders open the existing input-selection workflow. A single EXE
  selects the application; scripts and supporting files use copied inputs.
  A document without an entry point still needs the user to choose a program.
  Argument paths do not grant access to their host files automatically.
- The Try Run setup page opens first. Imported shortcut arguments are expanded
  for review. The user chooses permissions and selects **Run**. Existing MXC
  execution, blocked-access review, output, errors and result export are reused.

### Build and try the native menus

1. Build `TryRun.slnx` with the MXC options in the README.
2. Build the changed CmdPal host from this branch, using the repository build
   script in `src/modules/cmdpal/Microsoft.CmdPal.UI`:

   ```powershell
   ../../../../tools/build/build.ps1 -Platform x64 -Configuration Debug -ExtraArgs @('/restore')
   ```

   Launch that build through its Visual Studio startup project after a successful
   build. A previously installed CmdPal binary cannot show these source changes.
3. CmdPal resolves `PowerToys.TryRun.exe` in its adjacent `TryRun` directory, its
   own directory, or the `TryRun` / `TryRun-Policies` directory beside `WinUI3Apps`
   in the PowerToys output root. For another output location, set
   `POWERTOYS_TRYRUN_APP` to the absolute Try Run executable path in the environment
   used to start CmdPal. An invalid explicit override produces an error.
   Visual Studio's extra `CmdPal\AppX` deployment directory is recognized too.
4. Search for a desktop app, select it, and press
   **Ctrl+K** to open its action menu. Choose **Open in
   Try Run**, check the prefilled program and arguments, then select **Run**.
   For an execution smoke test use `C:\Windows\System32\winver.exe`. Notepad can
   test the menu and configuration handoff but currently fails isolated startup.
5. Repeat with a local `.ps1` or `.cmd` file, and with a folder containing a script.
   Opening the action alone must not execute anything. Running a script that
   writes `result.txt` should show its output and copied results in Try Run.

This native action needs no development-extension registration. The optional
extension below provides additional top-level commands.

### Native handoff contract and verification

The two applications compile the same small handoff source in `Shared/`.
The versioned `--cmdpal-stdin` message contains one local path and argument data;
it cannot supply policies, elevation, a host-shell command or automatic execution.
Its bounded JSON parser is AOT compatible. Shortcut arguments are parsed with
Windows argument quoting, then shown in Try Run. Empty arguments are preserved
until the argument text is edited. The Explorer selection protocol is unchanged.

`cmdpal/Tests/Microsoft.CmdPal.TryRun.UnitTests` compiles the production context
command and handoff against the repository's pinned published SDK, so its checks
do not require a native CmdPal host build. Build it with the repository script
and `/restore`, then use `vstest.console.exe`; set `POWERTOYS_TRYRUN_APP` for the
real-window handoff test. The normal Apps and Indexer test projects also contain
result-menu regression tests. Try Run's tests cover prefilled arguments, unchanged
permissions, configuration-only navigation, and 3,000 mutated handoff messages.

The missing MSVC Spectre prerequisites have now been installed. The Apps and
Indexer test projects build successfully with exit code 0. For these projects,
use `/restore`, `/p:EnableMSTestRunner=false` and
`/p:CopyLocalLockFileAssemblies=true` when building for `vstest.console.exe`.
This supplies the test host while preserving the repository's default test setup.

The packaged-desktop/AppX fix passes **37/37** checks:

- `tryrun-packaged-context.trx`: 11 native provider tests, including an installed
  packaged desktop application's manifest, package-path validation, ordinary
  app results, file search and folder browsing.
- `packaged-handoff.trx`: 26 command/protocol tests, including both normal and
  `AppX` deployment layouts and a real configuration-window handoff.

The tested `Microsoft.CmdPal.Common.dll` and `Microsoft.CmdPal.Ext.Apps.dll` were
copied into this workspace's registered `Microsoft.CommandPalette.Dev` AppX
directory after backing up the previous files. Their hashes match the tested
binaries. The development host was restarted through `x-cmdpal://background`.
No UI input automation was used for this update; the complete menu walkthrough
remains a manual check. Future Visual Studio builds/deployments include the fix
from source normally.

Previous x64 Debug validation: `TryRun.slnx`, including the standalone native-action
test project, builds with exit code 0. `cmdpal-native-final.trx` passes **78/78**:
25 context-command/handoff checks and 53 Try Run, Explorer, file-access, selection
and setup/results regressions. This includes a real context-command invocation
opening Try Run without executing the selected script, and the Unicode fuzz
regression. Results are in `x64/Debug/tests/TryRun.UnitTests/TestResults/`.
The Apps/Indexer provider tests were not included in that earlier count; they
are covered by the newer native-provider run above.

### Protected installation folders and packaged-app diagnostics

The subsequent [direct MXC comparison](NOTEPAD-MXC-COMPARISON.md) reproduces the
Notepad failure through both the SDK and native CLI, documents environment and
read-only dependency controls, and identifies the CLI's existing `packaged_app`
diagnostic that is absent from the streaming wait result.

The old `Could not safely open a workspace entry` error was reproduced while
resolving Notepad's default `$app` read-only grant. The executable was readable,
but the copied-workspace directory validator attempted to open its protected
`C:\Program Files\WindowsApps` ancestor and received Windows error 5.

The worker now derives that read-only grant through the selected executable's
physical path and validates its immediate installation directory. It does not
grant its ancestors. Copied inputs and explicit/writable policy paths continue
to use the ancestor-locking validator. Filesystem failures include the failed
path and the underlying Windows error code.

After this fix, Notepad's policy generation succeeds. Its actual MXC startup
still exits with `0xC0000409`. Native block-mode capture recorded denied access
to Windows app activation-store files and AppModel registry state. These records
identify a compatibility problem to investigate, not permission to grant writes
to system folders. No extra system grants, permissive mode, host ACL changes or
host activation fallback were applied. Failed Windows applications now report
the hexadecimal exit code and, when native evidence is present, an app-activation
compatibility explanation.

The unsuccessful startup evidence is retained in `packaged-startup-diagnostics.trx`.
`InstalledApplicationTests.PackagedApplicationOpensItsOwnWindowAndCanBeStopped`
is explicitly ignored as a known compatibility failure, with the runnable probe
retained for future MXC work. Its environment inputs are
`POWERTOYS_TRYRUN_GUI_TESTS=1` and `POWERTOYS_TRYRUN_PACKAGED_APP` set to the
package's Notepad EXE. The passing policy test uses the same executable via
`--describe-policy` and verifies that only its installation directory is read-only.

The standalone solution and tests built successfully for x64 Debug. The final
`workspace-fix-final.trx` run passes 43 checks and skips the one known Notepad
compatibility probe. Coverage includes the protected installation-directory
grant, error reporting, copied-file/link safeguards, read-only file grants,
Windows policy mapping, and actual `winver.exe` / `findstr.exe` runs.
The complete tested worker bundle was updated in `TryRun-Policies\Worker`, with
the previous bundle retained under `WorkerUpdateBackups`. Existing Try Run
windows use the new worker on their next run. The complete rebuilt application
is also available in `TryRun-WorkspaceFix`.

## Optional top-level development extension

Try Run is exposed through a standard out-of-process Command Palette extension.
The development package is opt-in and separate from PowerToys installation/GPO
registration. It reuses the published Command Palette SDK version already pinned
by this repository's extension template.

### Use

1. Build `TryRun.slnx` with the existing MXC options. The extension is produced in
   `<TryRun output>\CmdPal` beside the application and worker directories.
2. In an ordinary PowerShell session, register the development extension:

   ```powershell
   ./Set-CmdPalIntegration.ps1 -Action Register -TryRunDirectory 'C:\Users\shuaiyuan\source\PowerToys\x64\Debug\TryRun-Policies'
   ```

   Windows Developer Mode must already permit loose package registration. The
   script does not enable Developer Mode or request elevation. It registers only
   `PowerToys.TryRun.CmdPal.Dev` for the current user, then checks discovery and COM
   activation from the desktop's Explorer context. Keep a File Explorer window open.
3. Open Command Palette and search **Try Run**. The main command opens Try Run's
   existing configuration window. Choose the application, inputs and permissions,
   then select **Run** in that window.
4. **Try Run a file** opens a CmdPal page where you can paste one absolute local
   file/folder path. Surrounding double quotes are accepted. Select its result to
   open the existing selection workflow. Set program arguments in Try Run, not in
   the path query.

If CmdPal was already open, reload its extensions if the commands do not appear.
The integration does not replace ordinary CmdPal Run/search results and adds no
global fallback command. It does not inspect contents while searching, run pasted
shell text, select a more permissive policy or execute a workload automatically.
Errors starting Try Run are shown as a CmdPal toast and keep the palette open.

### Registration and lifecycle

`Set-CmdPalIntegration.ps1` also supports:

- **Status**: verifies this output directory is registered, discovers the extension
  in `com.microsoft.commandpalette`, activates `IExtension` and reads its commands.
- **LaunchTest**: performs the same protocol checks and invokes the registered
  **Try Run** command through COM. This opens configuration; it does not run a task.
- **Unregister**: removes only the matching development package. It leaves Try Run,
  its Explorer integration and its output files in place.

Close Try Run and disable/reload its CmdPal extension before rebuilding binaries
that are in use. Register the new output directory if you move the build.

The fixed development identity is `PowerToys.TryRun.CmdPal.Dev`, publisher
`CN=PowerToys.TryRun.Development`, COM class
`E6E62235-B207-45CC-92A3-ABCB37AD63B1`. The provider ID and top-level command IDs are
stable. Only the extension directory is registered as a development package; the
launcher resolves the sibling `PowerToys.TryRun.exe` without searching PATH.

Selections use the existing bounded UTF-8 `--selection-stdin` protocol. Paths are
data, not command-line fragments. The same MainWindow configuration, policies,
MXC worker, output/file review and access-grant retry UI handle all execution.

### Previous extension validation

The x64 Debug extension, standalone Try Run solution and test project built with
exit code 0. All 18 new tests passed in `cmdpal-integration.trx`: command discovery,
dynamic path queries, invalid queries, missing-application toasts, cancellation,
2,000 mutated query inputs and real empty/selected-file window handoffs. The latter
verify that opening configuration does not execute the selected script.

The final focused regression in `cmdpal-regression.trx` passed all 43 checks with
no skips or failures, including the new commands and the existing Explorer,
file-access retry and configuration/results workflows.

Development registration, desktop extension-catalog discovery and real COM
activation succeeded. `LaunchTest` invoked the registered command and opened a
Try Run window from the desktop context.

No installed CmdPal host was found in the checked package catalog, Start apps or
usual installation/output locations. The actual CmdPal search UI has therefore
not been exercised here. The general build-essentials command also encountered the
existing missing Visual C++ Spectre-library prerequisites in runner dependencies;
the independently built extension does not require rebuilding the CmdPal host.

This is development registration, not a signed production MSIX or an addition to
the PowerToys release installer. Production packaging remains separate work.
