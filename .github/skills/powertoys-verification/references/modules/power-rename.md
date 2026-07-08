# PowerRename — module verification profile

**PT module**: `PowerRename` (bulk-rename UI launched via Explorer context menu on selected files/folders)
**Source**: `<PT-repo>\src\modules\PowerRename\` (PT repo)
**Settings file**: `%LOCALAPPDATA%\Microsoft\PowerToys\PowerRename\settings.json`
**Logs**: `%LOCALAPPDATA%\Microsoft\PowerToys\PowerRename\Logs\v<ver>\log_<date>.log`
**Exe**: `%LOCALAPPDATA%\PowerToys\WinUI3Apps\PowerToys.PowerRename.exe`
**Activation**: Explorer right-click → "Rename with PowerRename" (Win11 Tier-1 menu; **no classic HKCR verb on Win11**); optional global hotkey if user-configured
**Last verified**: `0.100.2.0` (2026-07-08 — full 17-item run)

## Shared mechanics

For the context-menu machinery (the two openers, packaged-menu facts, BLK-ENV, honesty guard) see
**`references/explorer-context-menu-flow.md`** + **`scripts/pt-explorer-contextmenu.ps1`**. Don't
duplicate; cite by section.

**Context-menu routing (PowerRename-specific):**
- **Caption:** `Rename with PowerRename` (Win11 Tier-1). Launched exe: `PowerToys.PowerRename.exe`.
- **Which menu:** appears on **both** the selected-file menu **and** the folder-**background** menu.
  Prefer **`Open-PtExplorerContextMenu -FileName <f>`** (COM-select a real file → Shift+F10, coordinate-free
  and with a **verifiable** target) for present/absent/icon/extended-menu assertions. The folder-background
  opener (`Open-PtBackgroundContextMenu`) also works since PR registers there, but it depends on "focus in
  the file-list + nothing selected"; use it only if a test specifically asserts background-menu presence.
  Open **one menu per fresh Explorer window**, and let the menu **settle ~0.5s** before reading (the PR entry
  populates a beat after the base verbs).

For the Win11 IExplorerCommand vs classic HKCR distinction, see `scripts/pt-shell-verbs.ps1` header — PR is **modern-menu-only on Win11**, so classic-verb enumeration via Shell.Application **will not find it**.

## Entry-paths (try in order)

### 1. Direct CLI launch with file args — PREFERRED for UI-driven tests (verified 2026-06-10)
```powershell
$tmp = New-Item -ItemType Directory -Path "$env:TEMP\pr-fixture-$(Get-Random)"
1..3 | ForEach-Object { 'x' | Set-Content "$($tmp.FullName)\file$_.txt" }

# quote each path — Start-Process -ArgumentList joins args with spaces and does NOT auto-quote,
# so a path containing a space (e.g. "Hi World.txt") gets split into invalid args → empty file list.
$files = Get-ChildItem $tmp.FullName -File | ForEach-Object { '"{0}"' -f $_.FullName }
Start-Process "$env:LOCALAPPDATA\PowerToys\WinUI3Apps\PowerToys.PowerRename.exe" -ArgumentList $files

Start-Sleep -Milliseconds 1500
Force-PtForeground -AppId PowerToys.PowerRename   # bring PR to top — else it opens BEHIND the temp Explorer window (pitfall #13); needed for recordings + reliable screenshots
# main window HWND — skip transient PopupHost/tooltip windows (see references/winapp-ui-testing.md)
$pr = (winapp ui list-windows -a PowerToys.PowerRename --json 2>$null | ConvertFrom-Json |
       Where-Object { $_.title -ne 'PopupHost' } | Select-Object -First 1).hwnd
winapp ui inspect -w $pr --depth 5 -i 2>$null | Out-String | Select-String 'CheckBox "file\d\.txt"'
# Expect 3 hits (file1/2/3.txt, [on] by default)
```
Bypasses the context menu entirely; same code path inside the exe (it parses argv as the file list). **Use for every UI-driven option/regex/preview test** (Recipes 3–11 below).

### 2. Synthetic right-click + Invoke-PtContextMenuItem — for "menu entry present/absent" assertions (Recipes 1–2)
Use the openers from `references/explorer-context-menu-flow.md`. **Prefer selecting a real file** — it gives
a deterministic, verifiable target and opens **coordinate-free** (COM-select → Shift+F10). PowerRename also
appears on the folder background, so `Open-PtBackgroundContextMenu` is a valid alternative, but the file
opener is more robust. The menu-presence assertion is the ONE thing the CLI back-door cannot prove (it works
even if the entry is correctly hidden — the false-positive trap in that doc).

```powershell
. "$skill\scripts\pt-explorer-contextmenu.ps1"
# Disposable fixtures folder (same convention as Entry-path 1)
$fx = New-Item -ItemType Directory -Path "$env:TEMP\pr-fixture-$(Get-Random)"
'x' | Set-Content "$($fx.FullName)\a.txt"
# Open Explorer on it and grab its CabinetWClass HWND
$hwnd  = Open-PtExplorerWindow -Path $fx.FullName
$menu  = Open-PtExplorerContextMenu -ExplorerHwnd $hwnd -FileName 'a.txt'   # COM-select + Shift+F10 (coordinate-free)
Start-Sleep -Milliseconds 500                              # let the menu settle (PR populates after base verbs)
$items = Get-PtContextMenuItems -MenuHwnd $menu             # returns MenuItem name strings
$has   = $items | Where-Object { $_ -match 'Rename with PowerRename' }
# assert $has -> entry present
```

### 3. Shell COM classic verb (does NOT work on Win11 stock install)
```powershell
Invoke-PtShellVerb -Path "$($fx.FullName)\a.txt" -NamePattern 'PowerRename'  # -> False (reuses $fx from Entry-path 2)
```
Returns False on Win11 because PT registers PR only via IExplorerCommand, not as a classic HKCR shell verb. **Use only for negative checks** (and prefer the synthetic-menu enumeration above, which observes the actual Tier-1 menu).

## Recipes — a capability → control map (NOT a mini-checklist)

> **What this is (and isn't):** it maps each PowerRename *capability* to **which control drives it** (AutomationId / settings key) — nothing more. It is **not** the checklist: the checklist owns the *inputs* AND the *expected result*; this table only says *how to reach & poke the control*. **Where/how to read the outcome** is in the Read-out notes below — no expected values live here. That keeps the table stable across checklist rewordings; only a real UI redesign should force an edit.

| # | Capability | Control — how to drive it (AutomationId / settings key) |
|---|---|---|
| 1 | Context-menu entry present when enabled, gone when disabled | Settings-UI enable toggle: `Set-PtModuleEnabledViaSettingsUI -PageTag PowerRename -EnabledKey PowerRename` (see `references/explorer-context-menu-flow.md`) |
| 2 | Shell integration: menu **placement** + **icon** (4-combination matrix) | **Placement:** *Show PowerRename in* dropdown → "Default and extended" (`ExtendedContextMenuOnly=false`) vs "Extended only" (`=true`). **Icon:** expand that row's ˅ → **"Hide icon in context menu"** checkbox (`ShowIcon`/`bool_show_icon_on_menu`, **inverted**: checked = hide). Both persist to `power-rename-settings.json`, re-read per menu build (no Explorer restart). |
| 3 | Any search/replace option toggle (regex, match-all, case-sensitive, autocomplete, last-use) | `winapp ui invoke checkBox_regex` / `checkBox_matchAll` / `checkBox_case` (etc.); persists to `power-rename-settings.json` |
| 4 | Case conversion — lowercase / UPPERCASE / Title Case / Capitalize (mutually exclusive) | `toggleButton_lowerCase` / `upperCase` / `titleCase` / `capitalize` (single-select) |
| 5 | Which item types get renamed — include/exclude Files, Folders, Subfolders | `toggleButton_includeFiles` / `includeFolders` / `includeSubfolders` |
| 6 | Apply to whole "name + extension" / name only / extension only | `comboBox_renameParts` → items `itm-filenameonly` / `itm-extensiononly` (default = filename + extension) |
| 7 | Enumerate items — inserts an incrementing counter | `toggleButton_enumItems`; Replace accepts `${start=,increment=,padding=}`. **Prerequisite: a matching Search** — enumeration only substitutes on matched rows (empty Search → no rename, Apply disabled); use `.*` with regex on to rewrite the whole name. |
| 8 | Datetime tokens | Replace accepts `$DD` `$MMMM` `$YYYY` `$hh` `$mm` `$ss` `$fff` |
| 9 | Boost library — extended/Perl regex (e.g. lookbehind) beyond .NET | `UseBoostLib` key in `power-rename-settings.json` — read at launch, so relaunch PR after toggling |
| 10 | Per-row include/exclude in the preview | invoke a preview row's checkbox to uncheck it |
| 11 | Filter preview / select-all (NOT a column-header click — `TxtBlock_Original`/`TxtBlock_Renamed` are non-interactive labels) | `btn-filter-XXXX` → `button_showAll` / `button_showRenamed`; `checkBox_selectAll` |

> **Read-out notes** — where/how to read each outcome (the *expected value* always lives in the checklist, never here):
> - **Preview Renamed column** (rows 3–10): the Renamed value is the row's **last** `lbl-…` Text — `winapp ui inspect <itm-row> -w $hwnd --depth 4` (no `-i`; the first `lbl-…` is the Original). Or `winapp ui get-property <chkId> --property HelpText` with the **resolved** CheckBox id (e.g. `chk-0-2806` from `winapp ui search '<orig-name>' -w $hwnd --json`) — not the numeric Name (`0`) nor the `itm-…` id (both return `null`). Src: `ExplorerItem.xaml` `AutomationProperties.HelpText`→Renamed.
> - **Preview must be populated first** (rows 7–11): after setting Search/Replace, wait ~500 ms for the regex engine before reading rows or invoking row/filter controls.
> - **Menu presence** (rows 1–2): read the synthetic menu with `Get-PtContextMenuItems` (see `references/explorer-context-menu-flow.md`). Locked desktop → `BLK-ENV`.
> - **Icon, per menu** (row 2): modern tier-1 → the PR `MenuItem` gains/loses a UIA child `Image` (via `inspect`); legacy/extended `#32768` → the icon is a Win32 `MIIM_BITMAP` **invisible to UIA**, so pixel-check the row's icon gutter in a **screenshot** (not OCR). Src `PowerRenameContextMenu/dllmain.cpp GetIcon` (modern), `PowerRenameExt.cpp` `MIIM_BITMAP` (legacy).
> - **Placement** (row 2): "Extended only" → PR **absent from tier-1**, **present under a genuine Shift+right-click** (`CMF_EXTENDEDVERBS`) — NOT the non-Shift "Show more options" (`#32768`) menu.
> - **Enumerate behavior** (row 7): the counter advances **only for rows the Search matched** (`enumIndex++` on replacement; empty Search returns early — `PowerRenameRegEx.cpp`); non-matching rows don't consume an index. Formula: `start + index*increment` (0-based), zero-padded to `padding` digits (`lib/Enumerating.h`).

> **Mapping process**: read the actual checklist item → find its capability row → drive the named control (discovering control IDs at runtime where the row says so) → read the outcome via the Read-out notes → assert against the checklist's *own* expected value. No row matches ⇒ a NEW capability: drive it ad-hoc, then add a row (capability + control only).



## Fixture files needed

In a workspace `fixtures/` folder:
- `a.txt`, `b.txt`, `c.txt` — multi-select
- `IMG_001.png`, `IMG_002.png`, `IMG_003.png` — regex capture
- subfolder `subdir/` with 2 inner files — folder/subfolder exclusion
- `Foo_A_A_A.txt` — match-all
- `MIXED.txt` — case-sensitive

Always copy fixtures to a disposable temp folder before running actual rename operations.

## BLOCKED traps

- **TWO settings files — PR reads `power-rename-settings.json`, NOT `settings.json`** (verified 2026-06-10). `%LOCALAPPDATA%\Microsoft\PowerToys\PowerRename\` holds both: (1) `settings.json` = PT-store, keys `bool_mru_enabled`/`bool_persist_input`/`bool_show_icon_on_menu`/`bool_show_extended_menu`/`bool_use_boost_lib`/`int_max_mru_size` (what `Get-PtModuleSettings` + the Settings UI bind to); (2) `power-rename-settings.json` = the module's own store, keys `ShowIcon`/`ExtendedContextMenuOnly`/`PersistState`/`MRUEnabled`/`MaxMRUSize`/`UseBoostLib` — **this is the file the PR UI exe and the context-menu COM handlers actually read at launch** (`lib/Settings.cpp` `CSettings::Load→ParseJson`). The runner (`dll/dllmain.cpp`, settings-changed handler) syncs PT-store→module-store only on a Settings-UI *change event*; the PT-store file can sit stale for days. **To drive ShowIcon / ExtendedContextMenuOnly / MRUEnabled / PersistState / UseBoostLib deterministically, edit `power-rename-settings.json` directly, then restore.** The **context-menu** settings (ShowIcon, ExtendedContextMenuOnly) are re-read **per menu build** - no Explorer restart, just re-open the menu; the **PR UI-exe** settings (MRUEnabled, PersistState, UseBoostLib) are read at launch, so relaunch `PowerToys.PowerRename.exe`. Map (settings.json key → user-facing control): ShowIcon→"Hide icon in context menu" checkbox (**inverted**; inside the *Show PowerRename in* expander), ExtendedContextMenuOnly→"Show PowerRename in" dropdown ("Extended context menu only"), MRUEnabled→autocomplete, PersistState→"Show values from last use", UseBoostLib→"Use Boost library". MRU values live in `search-mru.json`/`replace-mru.json`; last-used (persist) in `power-rename-last-run-data.json`.
- **Opening the extended / legacy menus** — the classic `#32768` ("Show more options") menu IS winapp-enumerable (`Open-PtShowMoreOptionsMenu` → `Get-PtContextMenuItems`; see `references/explorer-context-menu-flow.md` → "Reading the legacy menu"), **but it does not pass `CMF_EXTENDEDVERBS`**, so it is the **wrong observer for "extended-only"** (PR is correctly absent there). To see the extended entry, open a **genuine Shift+right-click** menu: `Open-PtShiftRightClickMenu -ExplorerHwnd <h> -FileName <f>` (Shift held around an element-resolved right-click on the COM-selected file → `FindWindow('#32768')`), then `Get-PtContextMenuItems`. *(What to assert lives in Read-out → Placement.)* Src: `PowerRenameContextMenu/dllmain.cpp` `GetState` returns `ECS_HIDDEN` (hides it from Tier-1); `PowerRenameExt.cpp` `QueryContextMenu` returns `E_FAIL` unless `CMF_EXTENDEDVERBS`.
- **PR registers on the directory *background* menu too**, but menu targeting (prefer selecting a real file over the background menu; one menu per fresh window; settle ~0.5s) is already covered once in "Context-menu routing" + Entry-path 2 above — don't re-derive it here.
- **`set-value` on search/replace DOES fire the preview** (TextChanged works, unlike CmdPal) — Apply button enabling/disabling is a reliable match/no-match signal. The search/replace Edit AutomationIds are random per launch (`txt-textbox-XXXX`); discover them each launch by name (`Edit "Search for"` / `Edit "Replace with"`).
- **Disk mutation is real** — run renames against `$env:TEMP\pr-test-<random>`, not real fixtures.
- **Foreground + window hygiene (pitfall #13):** the PR window (and Settings) can open **behind** the temp Explorer window you opened for the context-menu test, so a recording/screenshot shows the wrong window. **(a)** After `Start-Process …PowerToys.PowerRename.exe` (or opening Settings), call `Force-PtForeground -AppId PowerToys.PowerRename` (or `-AppId PowerToys.Settings`) before observing. **(b)** Since each menu test opens a **fresh** Explorer window, close them **per item** so they don't stack in the foreground: `(New-Object -ComObject Shell.Application).Windows() | Where-Object { $_.LocationURL -match 'pr-fixture|pr-enum' } | ForEach-Object { $_.Quit() }` (match your disposable-fixture folder name so you never close the user's Explorer windows).
- **COM cache staleness** when re-checking verbs after enable/disable — call `Reset-PtShellComCache` from `scripts/pt-shell-verbs.ps1`.
- **Don't** try `Invoke-PtShellVerb 'PowerRename'` — returns False on Win11 (no classic registration). Use synthetic menu via `Invoke-PtContextMenuItem` or direct-CLI.
- **Don't skip the synthetic-menu test for menu-presence** — the CLI back-door false-PASSes when the entry is correctly hidden (see `references/explorer-context-menu-flow.md`).

## Source citations

- `src\modules\PowerRename\dllmain.cpp` — IExplorerCommand registration (no classic HKCR shadow on Win11).
- `src\modules\PowerRename\PowerRenameUILib\` — XAML for main PR window (toggle/checkbox AutomationIds).
- `src\modules\PowerRename\PowerRenameLib\Settings.cpp` — settings.json schema canonical property names.
