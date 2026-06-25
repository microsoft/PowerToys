# New+ — module verification profile

**PT module**: `NewPlus` (Explorer right-click → "New+" submenu that creates files/folders from a user templates folder)
**Source**: `<PT-repo>\src\modules\NewPlus\` (shell ext) + `<PT-repo>\src\settings-ui\Settings.UI\ViewModels\NewPlusViewModel.cs` (Settings UI)
**Module-owned settings file**: `%LOCALAPPDATA%\Microsoft\PowerToys\NewPlus\settings.json` — **folder is `NewPlus`, NOT `New`** (matches SKILL.md pitfall #18 table). Keys: `HideFileExtension`, `HideStartingDigits`, `TemplateLocation`, `ReplaceVariables`, `BuiltInNewHidePreference`.
**Templates folder (default)**: `%LOCALAPPDATA%\Microsoft\PowerToys\NewPlus\Templates` (per `TemplateLocation`)
**Default-templates source**: `%LOCALAPPDATA%\PowerToys\WinUI3Apps\Assets\NewPlus\Templates` (also `%ProgramFiles%\PowerToys\...` on machine installs)
**Logs**: `%LOCALAPPDATA%\Microsoft\PowerToys\NewPlus\NewPlus.ShellExtension\Logs\v<ver>\log_<date>.log`
**Packaged command**: sparse MSIX `Microsoft.PowerToys.NewPlusContextMenu`; command CLSID `{FF90D477-E32A-4BE8-8CC5-A502A97F5401}`
**Named Event**: none. **DSC**: n/a.

> Read **`../references/explorer-context-menu-flow.md` first** — New+ is a Win11 packaged-IExplorerCommand context-menu module; the menu can only be eyeballed via a real synthetic right-click on an **unlocked interactive desktop**. On a locked/RDP-minimized desktop (`Test-PtDesktopInteractive=False`) all "menu appears / template appears / hidden-caption" assertions are BLK-ENV / BLK-VISUAL-RENDER, not product FAILs.

## Entry-paths (try in order)

### 1. Enable/disable + registration gate (menu presence/absence) — headless-safe
Flip `enabled.NewPlus` in master `settings.json` + `Restart-PtRunner`, **or** toggle the Settings switch (below). Observe the gate, no foreground needed:
- CLSID registered ⇒ `Test-Path "HKCU:\Software\Classes\CLSID\{FF90D477-E32A-4BE8-8CC5-A502A97F5401}"` is `True` (enabled) / `False` (disabled).
- Log lines `New+ context menu registered` / `... unregistered` + `Runtime registration completed for CLSID ...`.
- Sparse package stays `Status Ok` even when disabled (hidden dynamically — SKILL.md pitfall #17).

### 2. Settings UI toggles via UIA invoke — headless-safe, **required for template auto-copy**
`Start-Process …\PowerToys.exe --open-settings=NewPlus`, then `winapp ui invoke <btn> -w <settingsHwnd>`:
- Enable toggle: `btn-new-248c` (under `NewPlusEnableToggle`) — **the enable transition runs `CopyTemplateExamples`** (Settings-UI side, `NewPlusViewModel.IsEnabled` setter). A master-`settings.json` flip + runner restart does **NOT** copy templates.
- `btn-hidethefileexte-24a0` (Hide file extension), `btn-hideleadingdigi-24a8` (Hide leading digits). AutomationIds carry a per-session suffix — re-`inspect` to get the live id.

### 3. Synthetic right-click on the folder **BACKGROUND** (the menu-render observer) — needs unlocked desktop
New+ lives in the folder-background ("New") menu, **not** a file's context menu — so `pt-explorer-contextmenu.ps1`'s `Open-PtExplorerContextMenu` (which right-clicks a *file item*) is the wrong entry. Right-click an **empty area of the file list** instead, then expand the `New+` submenu (a separate popup window one level deeper):
```powershell
# force-foreground the CabinetWClass window, GetWindowRect, RightClick at ~45% width / 68% height (empty list area)
# -> main bg menu popup (PopupWindowSiteBridge). Then:
$np = (winapp ui search 'New+' -w $mainMenuHwnd --json).matches | ? type -eq MenuItem | select -First 1
winapp ui invoke $np.selector -w $mainMenuHwnd          # expands the New+ submenu
# the submenu is the PopupWindowSiteBridge popup that contains 'Open templates' but NOT 'Sort by'
# enumerate its MenuItems (templates are 1:1 with the Templates-folder entries) / invoke one by name
```
Template items render with **caption transforms applied** (HideFileExtension strips `.txt`; HideStartingDigits strips `01. `). Selecting a template creates it in the current folder + enters rename mode. BLK-ENV only if `Test-PtDesktopInteractive` is False. **No Explorer restart needed** for setting A/B — the handler re-reads `NewPlus\settings.json` on each menu build.

## Recipes — a control/observation map, NOT an answer key

| # | Capability | Drive (control / settings key) | Observe (where the result shows) |
|---|---|---|---|
| 1 | Menu entry present when enabled | enable (master flag + restart, or `btn-new-248c`) | CLSID registered in `HKCU\…\CLSID`, log `context menu registered`; *visible submenu* = synthetic menu only (BLK-VISUAL-RENDER if locked) |
| 2 | Menu entry absent when disabled | disable | CLSID absent, log `context menu unregistered`; package still `Status Ok` |
| 3 | Templates folder created empty | shell ext `create_folder_if_not_exist(root)` on menu build (delete folder → right-click) | folder recreated **empty** — needs synthetic menu (BLK-ENV if locked) |
| 4 | Default templates copied when empty | `CopyTemplateExamples` on Settings-UI **enable** transition (`btn-new-248c` off→on) while folder empty | Templates folder repopulated from install Assets (filesystem — headless-safe) |
| 5 | A template (file/folder) shows + creates on select | put item in Templates folder; select it in the New+ submenu | submenu item (1:1 with dir entries) + `SHFileOperation FO_COPY` to target — synthetic menu only |
| 6 | Hide file extension | `HideFileExtension` / `btn-hidethefileexte-24a0` | strips ext from **menu caption only** (`get_menu_title`, `show_extension=false`); created file keeps ext — caption is BLK-VISUAL-RENDER if locked |
| 7 | Hide starting digits/spaces/dots | `HideStartingDigits` / `btn-hideleadingdigi-24a8` | strips leading digits+separator from **both** menu caption and **created filename** (`remove_starting_digits_from_filename` via `get_menu_title` + `copy_template`); needs a digit-prefixed template + render |

> Verify a setting actually drives behavior by editing the **module-owned** `NewPlus\settings.json` (not the PT-store mirror) and relaunching; the Settings toggles round-trip into this same file.

## Common BLOCKED traps
- **Master-flip + runner restart does not copy default templates** — that's a Settings-UI action (`NewPlusViewModel.IsEnabled`). Use the UIA toggle for any template-auto-copy item.
- **Menu render is invisible without a real right-click** — packaged command is not `CoCreate`-able (`REGDB_E_CLASSNOTREGISTERED`) and not in classic `Shell.Application.Verbs()`. Locked desktop ⇒ BLK-ENV; do not substitute a CLI/back-door (there isn't one, and it'd be a false PASS).
- **No template-count observable** — `saved_number_of_templates` is an in-memory static (`new_utilities.cpp`), not registry/log.

## Fixture files needed
- A plain file (e.g. `test.txt`) and a folder-with-files to drop into Templates (template-appears items).
- A digit-prefixed template (e.g. `01. Test.txt`) to exercise Hide-starting-digits.

## Source citations
- `src/modules/NewPlus/NewShellExtensionContextMenu/template_item.cpp` — `get_menu_title` (hide-extension), `remove_starting_digits_from_filename`, `copy_object_to`.
- `src/modules/NewPlus/NewShellExtensionContextMenu/new_utilities.h` — `copy_template`, `create_folder_if_not_exist`, `get_newplus_setting_hide_*`, `register_msix_package`.
- `src/modules/NewPlus/NewShellExtensionContextMenu/shell_context_sub_menu.cpp` — `create_folder_if_not_exist(root)` + template enumeration.
- `src/settings-ui/Settings.UI/ViewModels/NewPlusViewModel.cs` — `CopyTemplateExamples` (creates dir; copies examples only when files==0 && dirs==0), called from `IsEnabled` setter / `OpenNewTemplateFolder` / `DashboardViewModel`.

## Ceiling
- Unlocked interactive desktop: **9/9 PASS** (verified 2026-06-18 via background-menu synthetic right-click + submenu expansion).
- Locked/non-interactive desktop: ~3/9 (registration-gate present/absent + template auto-copy); menu-render/select items fall to BLK-ENV/BLK-VISUAL-RENDER — re-run after unlocking.

## Don'ts
- Don't edit `…\PowerToys\New\settings.json` — wrong path; the file is under `NewPlus\`.
- Don't use `Open-PtExplorerContextMenu` (file-item right-click) for New+ — it's the folder **background** ("New") menu; right-click empty list space instead.
- Don't forget to **expand the `New+` submenu** (invoke it) before enumerating templates — they live one popup deeper than the main menu.
- Don't mark menu-render items as product FAIL on a locked desktop — it's BLK-ENV.
- Don't restart Explorer to apply a setting change — the handler re-reads `NewPlus\settings.json` per menu build.
