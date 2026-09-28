# Context Menu Manager

[GitHub tracking issue #33](https://github.com/microsoft/PowerToys/issues/33) - "Manage right-click - Customize context menu", open since 2019, the long-standing most-requested feature in the tracker.

## Overview

Context Menu Manager is a Settings UI page under **System Tools** that lists Windows Explorer right-click context-menu entries and lets the user enable/disable each one - a friendly front-end over registry state a user could otherwise only edit by hand in regedit.

It lists three kinds of entries, all toggleable:

- **Classic shell extension handlers** - registered under `...\shellex\ContextMenuHandlers\<Name>`, for both the current user (`HKCU`) and all users (`HKLM`). One row per handler, however many roots it is registered under. A handler is one COM object that builds its own items at runtime (TortoiseGit's whole submenu is one handler), so it can only be toggled as a whole and its individual items can't be listed without loading the DLL.
- **Classic static verbs** - `...\shell\<verb>` keys (e.g. "Open with Visual Studio", "PowerShell 7"). Cascading submenus declared through `ExtendedSubCommandsKey` are listed as indented `Parent > Child` rows below their parent.
- **Windows 11 modern (sparse-package) entries** - top-level packaged context-menu extensions, one row per package.

## Architecture

Unlike most PowerToys modules, **all real logic lives in the Settings UI process, in C#** - not in native code. There is no separate launched app and no native enumeration/registry code:

- [`ContextMenuManagerViewModel.cs`](/src/settings-ui/Settings.UI/ViewModels/ContextMenuManagerViewModel.cs) - registry enumeration (`Microsoft.Win32.Registry`), the toggle write path, and modern-package enumeration (`Windows.Management.Deployment.PackageManager`).
- [`ContextMenuEntry.cs`](/src/settings-ui/Settings.UI/ViewModels/ContextMenuEntry.cs) - the display model for one entry. Not a persisted settings object - the registry itself is the source of truth, so entries are re-enumerated live on page load and on Refresh rather than cached to a JSON file.
- [`ContextMenuManagerPage.xaml(.cs)`](/src/settings-ui/Settings.UI/SettingsXAML/Views/ContextMenuManagerPage.xaml) - the page. Confirmation dialogs (all-users writes, likely-Windows-owned entries) are handled in the code-behind, which owns the `XamlRoot` a `ContentDialog` needs.

A minimal native [`ContextMenuManagerModuleInterface`](/src/modules/ContextMenuManager/ContextMenuManagerModuleInterface) DLL still exists, modeled on PowerPreview's toggle-only shape - not because the module needs native logic, but because the runner's `EnabledModules`/GPO/tray architecture requires every module to have a `PowertoyModuleIface`. Its `enable()`/`disable()` only flip a flag; there's no hotkey, no window, no background thread, and no dedicated GPO policy for v1 (`gpo_policy_enabled_configuration()` returns `gpo_rule_configured_not_configured`).

### Toggle mechanism

Each kind uses the mechanism Explorer itself honours (`ContextMenuEntryKind`). No key is ever deleted, so every change is reversible:

- **Handler with a CLSID default value**: the NewPlus convention (see [`new_utilities.h`](/src/modules/NewPlus/NewShellExtensionContextMenu/new_utilities.h) `disable_built_in_new_via_registry`) - overwrite the default value with a `"disabled_"`-prefixed copy of the CLSID, restore it to enable.
- **Handler whose key name is the CLSID** (default value empty or a label) and **modern packaged verbs** (manifest `Verb Clsid=...`): add/remove the CLSID under `Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked` (HKLM for all-users handlers, HKCU for packages).
- **Static verb**: add/remove the `LegacyDisable` value on the verb key.

### Enumeration scope (v1)

Five roots are walked, under both `HKCU\Software\Classes\...` and `HKLM\Software\Classes\...`:

- `*\shellex\ContextMenuHandlers`
- `Directory\shellex\ContextMenuHandlers`
- `Directory\Background\shellex\ContextMenuHandlers`
- `AllFilesystemObjects\shellex\ContextMenuHandlers`
- `Drive\shellex\ContextMenuHandlers`

Per-extension `SystemFileAssociations\<ext>\...` handlers are **not** walked in v1 - that tree is far larger (one subtree per file extension) for comparatively little added value over the five roots above, where most third-party context-menu bloat (7-Zip, Git, WinRAR, etc.) actually registers.

### Safety

- `BuiltInHandlerDenylist` only holds `New`, which NewPlus owns. Windows' own handlers (e.g. `Sharing`, `WorkFolders`) are toggleable behind the confirmation below.
- Any handler whose CLSID resolves to a DLL under `%SystemRoot%\System32`/`SysWOW64` is still toggleable but gets an extra confirmation dialog, since legitimate third-party/AV handlers also live there.
- All-users (`HKLM`) writes require PowerToys to be running elevated (reuses the existing "Restart PowerToys as administrator" flow from the General page - no new elevation mechanism) and always show a confirmation dialog first, since the change affects every account on the machine.
- Toggling an entry shows a manual "Restart File Explorer" notice/button - Explorer caches resolved context-menu handlers per process, so a change isn't visible until it restarts. This is never automatic (restarting Explorer closes every open Explorer window).

## Known gaps / deliberately deferred

- **Icon art**: the nav item and page currently reuse the Registry Preview icon as a placeholder (`Assets/Settings/Icons/RegistryPreview.png`) - needs real icon art.
- **Dashboard tile**: not added. Follow the `GetModuleItemsHosts()` pattern in `DashboardViewModel.cs` if wanted.
- **OOBE (first-run) page**: skipped - this is an advanced/power-user feature, not onboarding-critical.
- **GPO policy**: module-enable via `EnabledModules.ContextMenuManager` only, no dedicated ADMX policy yet.
- **Command Palette integration**: `Microsoft.CmdPal.Ext.PowerToys`'s `ModuleEnablementService` (which lets Command Palette open/enable other PowerToys modules by name) doesn't know about this module yet.
- **Automated tests**: none yet. The registry round-trip and denylist behavior are best verified manually against a real registry for v1 (build, toggle a real third-party entry such as 7-Zip/Git/WinRAR, restart Explorer, confirm the entry disappears/reappears; inspect with regedit that the handler key is never deleted).
