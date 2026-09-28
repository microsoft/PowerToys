# Context Menu Manager

[GitHub tracking issue #33](https://github.com/microsoft/PowerToys/issues/33) - "Manage right-click - Customize context menu", open since 2019, the long-standing most-requested feature in the tracker.

## Overview

Context Menu Manager is a Settings UI page under **System Tools** that shows the real Windows Explorer right-click menu and lets the user enable/disable the entries behind its items - a friendly front-end over registry state a user could otherwise only edit by hand in regedit.

It lists three kinds of entries, all toggleable:

- **Classic shell extension handlers** - registered under `...\shellex\ContextMenuHandlers\<Name>`, for both the current user (`HKCU`) and all users (`HKLM`). One row per handler, however many roots it is registered under. A handler is one COM object that builds its own items at runtime (TortoiseGit's whole submenu is one handler), so it can only be toggled as a whole.
- **Classic static verbs** - `...\shell\<verb>` keys (e.g. "Open with Visual Studio", "PowerShell 7"). Items of cascading submenus declared through `ExtendedSubCommandsKey` are entries of their own.
- **Windows 11 modern (sparse-package) entries** - top-level packaged context-menu extensions, one row per package.

## Architecture

Unlike most PowerToys modules, **all real logic lives in the Settings UI process, in C#** - not in native code. There is no separate launched app and no native enumeration/registry code:

- [`ContextMenuManagerViewModel.cs`](/src/settings-ui/Settings.UI/ViewModels/ContextMenuManagerViewModel.cs) - registry enumeration (`Microsoft.Win32.Registry`), the toggle write path, and modern-package enumeration (`Windows.Management.Deployment.PackageManager`).
- [`ContextMenuEntry.cs`](/src/settings-ui/Settings.UI/ViewModels/ContextMenuEntry.cs) - the display model for one entry. Not a persisted settings object - the registry itself is the source of truth, so entries are re-enumerated live on page load and on Refresh rather than cached to a JSON file.
- [`ContextMenuManagerPage.xaml(.cs)`](/src/settings-ui/Settings.UI/SettingsXAML/Views/ContextMenuManagerPage.xaml) - the page. Confirmation dialogs (all-users writes, likely-Windows-owned entries) are handled in the code-behind, which owns the `XamlRoot` a `ContentDialog` needs.

- [`ContextMenuManagerViewModel.Preview.cs`](/src/settings-ui/Settings.UI/ViewModels/ContextMenuManagerViewModel.Preview.cs) - the page's menu, which is the whole UI: pick a target, click an item, see where it is registered and toggle it in the details pane. Every target is captured with the helper below the first time it is picked and cached until Refresh; a toggle only re-renders the cached rows (off items dim in place). Items are linked back to entries by the verb the menu reports (static verbs), by the helper's per-extension probe (shell extensions), or by label (cascading verbs); an extension's submenu items belong to the extension. Entries that are off aren't in the real menu, so they are listed under a "Turned off" caption at the end. It adds what a raw `IContextMenu` lacks: the view's View/Sort by/Refresh block and the Windows 11 packaged verbs, shown as the first menu with "Show more options" switching to the classic one.
- [`ContextMenuManager.MenuCapture`](/src/modules/ContextMenuManager/ContextMenuManager.MenuCapture) - a console helper deployed next to Settings. It builds the real menu for a sample target under `%TEMP%\PowerToys\ContextMenuManagerPreview` (`--target desktop|background|folder|file|drive`), sends `WM_INITMENUPOPUP` so lazy submenus fill, and prints the item tree with 32bpp icons as JSON. `--probe {clsid},...` also loads each listed extension on its own against the same sample and reports its top-level item texts, since the merged menu doesn't say which extension added which item. It is a separate process because building a menu loads every registered shell extension, and a crashing or hanging third-party DLL must not take Settings down (Settings kills it after 10 s). A fresh process also has no handler cache, so a capture reflects registry toggles without restarting Explorer. Handlers that check for `explorer.exe` (e.g. NVIDIA's) don't show up in it.

A minimal native [`ContextMenuManagerModuleInterface`](/src/modules/ContextMenuManager/ContextMenuManagerModuleInterface) DLL still exists, modeled on PowerPreview's toggle-only shape - not because the module needs native logic, but because the runner's `EnabledModules`/GPO/tray architecture requires every module to have a `PowertoyModuleIface`. Its `enable()`/`disable()` only flip a flag; there's no hotkey, no window and no background thread. `gpo_policy_enabled_configuration()` returns the `ConfigureEnabledUtilityContextMenuManager` policy, which the Settings page also honours by locking its enable toggle.

### Toggle mechanism

Each kind uses the mechanism Explorer itself honours (`ContextMenuEntryKind`). No key is ever deleted, so every change is reversible:

- **Handler with a CLSID default value**: the NewPlus convention (see [`new_utilities.h`](/src/modules/NewPlus/NewShellExtensionContextMenu/new_utilities.h) `disable_built_in_new_via_registry`) - overwrite the default value with a `"disabled_"`-prefixed copy of the CLSID, restore it to enable.
- **Handler whose key name is the CLSID** (default value empty or a label) and **modern packaged verbs** (manifest `Verb Clsid=...`): add/remove the CLSID under `Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked` (HKLM for all-users handlers, HKCU for packages).
- **Static verb**: add/remove the `LegacyDisable` value on the verb key.

### Enumeration scope (v1)

Six roots are walked, under both `HKCU\Software\Classes\...` and `HKLM\Software\Classes\...`:

- `*\shellex\ContextMenuHandlers`
- `Directory\shellex\ContextMenuHandlers`
- `Directory\Background\shellex\ContextMenuHandlers`
- `AllFilesystemObjects\shellex\ContextMenuHandlers`
- `Drive\shellex\ContextMenuHandlers`
- `DesktopBackground\shellex\ContextMenuHandlers`

Per-extension `SystemFileAssociations\<ext>\...` handlers are **not** walked in v1 - that tree is far larger (one subtree per file extension) for comparatively little added value over the roots above, where most third-party context-menu bloat (7-Zip, Git, WinRAR, etc.) actually registers.

### Safety

- `BuiltInHandlerDenylist` only holds `New`, which NewPlus owns. Windows' own handlers (e.g. `Sharing`, `WorkFolders`) are toggleable behind the confirmation below.
- Any handler whose CLSID resolves to a DLL under `%SystemRoot%\System32`/`SysWOW64` is still toggleable but gets an extra confirmation dialog, since legitimate third-party/AV handlers also live there.
- All-users (`HKLM`) writes require PowerToys to be running elevated (reuses the existing "Restart PowerToys as administrator" flow from the General page - no new elevation mechanism) and always show a confirmation dialog first, since the change affects every account on the machine.
- Toggling an entry shows a manual "Restart File Explorer" notice/button - Explorer caches resolved context-menu handlers per process, so a change isn't visible until it restarts. This is never automatic (restarting Explorer closes every open Explorer window).

## Known gaps / deliberately deferred

- **Icon art**: the nav item and page currently reuse the Registry Preview icon as a placeholder (`Assets/Settings/Icons/RegistryPreview.png`) - needs real icon art.
- **Dashboard tile**: not added. Follow the `GetModuleItemsHosts()` pattern in `DashboardViewModel.cs` if wanted.
- **OOBE (first-run) page**: skipped - this is an advanced/power-user feature, not onboarding-critical.
- **Command Palette integration**: `Microsoft.CmdPal.Ext.PowerToys`'s `ModuleEnablementService` (which lets Command Palette open/enable other PowerToys modules by name) doesn't know about this module yet.
- **Automated tests**: none yet. The registry round-trip and denylist behavior are best verified manually against a real registry for v1 (build, toggle a real third-party entry such as 7-Zip/Git/WinRAR, restart Explorer, confirm the entry disappears/reappears; inspect with regedit that the handler key is never deleted).
