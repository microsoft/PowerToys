# Command Palette integration

## Actions on built-in app and file results

The native CmdPal Apps and Indexer providers now include **Open in Try Run**
(**在 Try Run 中打开** in Simplified Chinese) in the selected result's action menu.
The default Enter/Open action stays unchanged. This applies to app search,
indexed file search, direct-path file results and the folder browser.

- For Win32 apps, the action uses CmdPal's resolved executable path and shortcut
  arguments. EXE, PowerShell, CMD and BAT are supported. It never activates the
  shortcut through the host shell. UWP/AUMID and URL activation are not supported.
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
4. Search for a Win32 app, select it, and open its action menu. Choose **Open in
   Try Run**, check the prefilled program and arguments, then select **Run**.
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

The native CmdPal build is currently blocked by missing MSVC Spectre libraries
(`MSB8040` in `version.vcxproj` and `Microsoft.CommandPalette.Extensions.vcxproj`).
The native provider tests and complete CmdPal menu walkthrough require those
prerequisites. No compiler security setting was disabled to bypass this failure.

Current x64 Debug validation: `TryRun.slnx`, including the standalone native-action
test project, builds with exit code 0. `cmdpal-native-final.trx` passes **78/78**:
25 context-command/handoff checks and 53 Try Run, Explorer, file-access, selection
and setup/results regressions. This includes a real context-command invocation
opening Try Run without executing the selected script, and the Unicode fuzz
regression. Results are in `x64/Debug/tests/TryRun.UnitTests/TestResults/`.
The Apps/Indexer provider menu tests are not included in this count; their normal
build still needs the missing native prerequisites.

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
