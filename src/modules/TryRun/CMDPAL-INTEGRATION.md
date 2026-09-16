# Command Palette entry

Try Run is exposed through a standard out-of-process Command Palette extension.
The development package is opt-in and separate from PowerToys installation/GPO
registration. It reuses the published Command Palette SDK version already pinned
by this repository's extension template.

## Use

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

## Registration and lifecycle

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

## Validation

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
