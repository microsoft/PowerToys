# PowerRename — module verification profile

**PT module**: `PowerRename` (bulk-rename UI launched via Explorer context menu on selected files/folders)
**Source**: `<PT-repo>\src\modules\PowerRename\` (PT repo)
**Settings file**: `%LOCALAPPDATA%\Microsoft\PowerToys\PowerRename\settings.json`
**Logs**: `%LOCALAPPDATA%\Microsoft\PowerToys\PowerRename\Logs\v<ver>\log_<date>.log`
**Exe**: `%LOCALAPPDATA%\PowerToys\WinUI3Apps\PowerToys.PowerRename.exe`
**Activation**: Explorer right-click → "Rename with PowerRename" (Win11 Tier-1 menu; **no classic HKCR verb on Win11**); optional global hotkey if user-configured
**DSC resource**: `Microsoft.PowerToys/PowerRenameSettings`

## Shared mechanics

For the context-menu machinery (the two openers, packaged-menu facts, BLK-ENV, honesty guard) see
**`references/explorer-context-menu-flow.md`** + **`scripts/pt-explorer-contextmenu.ps1`**. Don't
duplicate; cite by section.

**Context-menu routing (PowerRename-specific):**
- **Caption:** `Rename with PowerRename` (Win11 Tier-1). Launched exe: `PowerToys.PowerRename.exe`.
- **Which menu:** appears on **both** the selected-file menu **and** the folder-**background** menu.
  Prefer **`Open-PtBackgroundContextMenu`** (coordinate-free Shift+F10 — can't miss a row or hit the
  preview pane) for present/absent/icon/extended-menu assertions. Use `Open-PtExplorerContextMenu
  -FileName <f>` only if a test specifically needs the entry on a *file* item.

For the Win11 IExplorerCommand vs classic HKCR distinction, see `scripts/pt-shell-verbs.ps1` header — PR is **modern-menu-only on Win11**, so classic-verb enumeration via Shell.Application **will not find it**.

## Entry-paths (try in order)

### 1. Direct CLI launch with file args — PREFERRED for UI-driven tests (verified 2026-06-10)
```powershell
$tmp = New-Item -ItemType Directory -Path "$env:TEMP\pr-fixture-$(Get-Random)"
1..3 | ForEach-Object { 'x' | Set-Content "$($tmp.FullName)\file$_.txt" }

Start-Process "$env:LOCALAPPDATA\PowerToys\WinUI3Apps\PowerToys.PowerRename.exe" `
    -ArgumentList "$($tmp.FullName)\file1.txt","$($tmp.FullName)\file2.txt","$($tmp.FullName)\file3.txt"

Start-Sleep -Milliseconds 1500
$pr = (winapp ui list-windows -a PowerToys.PowerRename 2>$null | Out-String) -split "`r?`n" |
      ForEach-Object { if ($_ -match 'HWND (\d+):') { [int64]$matches[1] } } | Select-Object -First 1
winapp ui inspect -w $pr --depth 5 -i 2>$null | Out-String | Select-String 'CheckBox "file\d\.txt"'
# Expect 3 hits (file1/2/3.txt, [on] by default)
```
Bypasses the context menu entirely; same code path inside the exe (it parses argv as the file list). **Use for every UI-driven option/regex/preview test** (Recipes 4-12 below).

### 2. Synthetic right-click + Invoke-PtContextMenuItem — for "menu entry present/absent" assertions (Recipes 1-3)
Use the openers from `references/explorer-context-menu-flow.md`. PowerRename appears on the folder
**background** menu too, so prefer the coordinate-free background opener (no file needed, can't miss a
row / hit the preview pane). The menu-presence assertion is the ONE thing the CLI back-door cannot
prove (it works even if the entry is correctly hidden — the false-positive trap in that doc).

```powershell
. "$skill\scripts\pt-explorer-contextmenu.ps1"
# Disposable fixtures folder (same convention as Entry-path 1)
$fx = New-Item -ItemType Directory -Path "$env:TEMP\pr-fixture-$(Get-Random)"
'x' | Set-Content "$($fx.FullName)\a.txt"
# Open Explorer on it and grab its CabinetWClass HWND
$hwnd  = Open-PtExplorerWindow -Path $fx.FullName
$menu  = Open-PtBackgroundContextMenu -ExplorerHwnd $hwnd   # coordinate-free; no file needed
$items = Get-PtContextMenuItems -MenuHwnd $menu             # returns MenuItem name strings
$has   = $items | Where-Object { $_ -match 'Rename with PowerRename' }
# assert $has -> entry present
```

### 3. Shell COM classic verb (does NOT work on Win11 stock install)
```powershell
Invoke-PtShellVerb -Path "$($fx.FullName)\a.txt" -NamePattern 'PowerRename'  # -> False (reuses $fx from Entry-path 2)
```
Returns False on Win11 because PT registers PR only via IExplorerCommand, not as a classic HKCR shell verb. **Use only for negative checks** (and prefer the synthetic-menu enumeration above, which observes the actual Tier-1 menu).

## Recipes — a control/observation map, NOT a per-test-case answer key

> **What this table is (and isn't):** it maps each PowerRename *capability* to **which control drives it** (AutomationId / settings key) and **where the result shows up**. It deliberately does **NOT** prescribe specific Search/Replace inputs or expected-output assertions — those are the agent's job to design from the actual checklist item at runtime. Keeping it input/assertion-free means the table survives checklist-wording changes; only a real UI redesign (renamed/moved control) should force an edit here (as happened to rows 5 & 12 in build 0.100.0).

| # | Capability | Drive (control / settings key) | Observe (where the result shows) |
|---|---|---|---|
| 1 | Context-menu entry present when enabled, gone when disabled | **Settings-UI toggle** via `Set-PtModuleEnabledViaSettingsUI -PageTag PowerRename -EnabledKey PowerRename` (see `references/explorer-context-menu-flow.md` → "Enabling / disabling the module"); synthetic menu (entry-path 2) to observe | `Get-PtContextMenuItems` includes / excludes "Rename with PowerRename". Locked desktop → `BLK-ENV` |
| 2 | "Show icon on context menu" | `ShowIcon` in `power-rename-settings.json` + relaunch | menu entry shows icon vs text-only (screenshot); or HKCR `Icon` |
| 3 | "Appear only in extended menu" | `ExtendedContextMenuOnly` + relaunch | Tier-1 menu hides PR; classic "Show more options" still lists it |
| 4 | Any search/replace option toggle (regex, match-all, case-sensitive, autocomplete, last-use) | `winapp ui invoke checkBox_regex` / `checkBox_matchAll` / `checkBox_case` (etc.); re-read `power-rename-settings.json` | the settings key flips **and** the preview behavior changes accordingly |
| 5 | Case mode (single-select) | toggle **buttons** `toggleButton_lowerCase` / `upperCase` / `titleCase` / `capitalize` (not a dropdown) | preview column shows case-transformed names |
| 6 | Scope: include/exclude Files / Folders / Subfolders | `toggleButton_includeFiles` / `includeFolders` / `includeSubfolders` | excluded row types appear disabled in the preview |
| 7 | Apply-to scope: name-only / extension-only | the "Apply to" selector | replacement affects only the name vs only the extension (preview) |
| 8 | Enumerate items | `toggleButton_enumItems`; Replace accepts `${start=,increment=,padding=}` tokens | preview shows the substituted counter |
| 9 | Datetime tokens | Replace accepts `$DD` `$MMMM` `$YYYY` `$hh` `$mm` `$ss` `$fff` | preview value matches `(Get-Item <file>).CreationTime` formatted the same way |
| 10 | Boost library (Perl regex beyond .NET, e.g. lookbehind) | `UseBoostLib` — **read at process start; relaunch PR after toggling** | the Perl-only pattern matches in the preview without error |
| 11 | Per-row include/exclude in the preview | invoke a row checkbox to uncheck | the unchecked file is unchanged on disk after Rename |
| 12 | Filter preview / select-all (NOT a column-header click — headers `TxtBlock_Original`/`TxtBlock_Renamed` are non-interactive labels) | `btn-filter-XXXX` → `button_showAll` / `button_showRenamed`; `checkBox_selectAll` | visible row set shrinks/grows; all rows toggle on/off |

> **Mapping process**: read the actual checklist item → identify the capability → find its row → drive the named control and design your own inputs + assertions for *that* item. If no row matches, it's a NEW capability — drive it ad-hoc and add a row (capability + control + observation point, no canned inputs).



## Fixture files needed

In a workspace `fixtures/` folder:
- `a.txt`, `b.txt`, `c.txt` — multi-select
- `IMG_001.png`, `IMG_002.png`, `IMG_003.png` — regex capture
- subfolder `subdir/` with 2 inner files — folder/subfolder exclusion
- `Foo_A_A_A.txt` — match-all
- `MIXED.txt` — case-sensitive

Always copy fixtures to a disposable temp folder before running actual rename operations.

## BLOCKED traps

- **TWO settings files — PR reads `power-rename-settings.json`, NOT `settings.json`** (verified 2026-06-10). `%LOCALAPPDATA%\Microsoft\PowerToys\PowerRename\` holds both: (1) `settings.json` = PT-store, keys `bool_mru_enabled`/`bool_persist_input`/`bool_show_icon_on_menu`/`bool_show_extended_menu`/`bool_use_boost_lib`/`int_max_mru_size` (what `Get-PtModuleSettings` + the Settings UI bind to); (2) `power-rename-settings.json` = the module's own store, keys `ShowIcon`/`ExtendedContextMenuOnly`/`PersistState`/`MRUEnabled`/`MaxMRUSize`/`UseBoostLib` — **this is the file the PR UI exe and the context-menu COM handlers actually read at launch** (`lib/Settings.cpp` `CSettings::Load→ParseJson`). The runner (`dll/dllmain.cpp:301-307`) syncs PT-store→module-store only on a Settings-UI *change event*; the PT-store file can sit stale for days. **To drive ShowIcon / ExtendedContextMenuOnly / MRUEnabled / PersistState / UseBoostLib deterministically, edit `power-rename-settings.json` directly + relaunch PR (or restart runner+Explorer for the menu handlers), then restore.** Map (settings.json key → user-facing toggle): ShowIcon→"Show icon on context menu", ExtendedContextMenuOnly→"Appear only in extended menu", MRUEnabled→autocomplete, PersistState→"Show values from last use", UseBoostLib→"Use Boost library". MRU values live in `search-mru.json`/`replace-mru.json`; last-used (persist) in `power-rename-last-run-data.json`.
- **"Show icon on context menu" has no Settings-UI toggle in current builds** — drive it via `power-rename-settings.json` `ShowIcon`. Behavior is observable on the synthetic menu (icon vs text-only); source `PowerRenameContextMenu/dllmain.cpp:73` (`GetIcon→null`).
- **The classic `#32768` ("Show more options") menu IS winapp-enumerable** — open it with `Open-PtClassicContextMenu` then read it with `Get-PtContextMenuItems` (see `references/explorer-context-menu-flow.md` → "Reading the legacy menu"). For "Appear only in extended menu", assert PR is **absent from the Tier-1 menu** but **present in the classic menu** (source: `dllmain.cpp:108` `ECS_HIDDEN` hides it from Tier-1; `PowerRenameExt.cpp:84` returns `E_FAIL` unless `CMF_EXTENDEDVERBS`, i.e. classic-only).
- **PR registers on the directory *background* menu too** — so prefer `Open-PtBackgroundContextMenu` (coordinate-free) for menu-entry / icon-visibility / extended-menu present-absent comparisons (see the module's "Context-menu routing" note + `references/explorer-context-menu-flow.md`).
- **`set-value` on search/replace DOES fire the preview** (TextChanged works, unlike CmdPal) — Apply button enabling/disabling is a reliable match/no-match signal. The search/replace Edit AutomationIds are random per launch (`txt-textbox-XXXX`); discover them each launch by name (`Edit "Search for"` / `Edit "Replace with"`).
- **Preview-row uncheck + column-header invokes need the Preview populated first** — set Search/Replace and wait ~500 ms for the regex engine; otherwise the invokes hit an empty list.
- **Boost library is read at PR process start** — close + relaunch PR after toggling.
- **Icon-on-menu and extended-only checks prefer registry over screenshot** — read HKCR `Extended` / `Icon` REG_SZ; more reliable + locale-independent.
- **Disk mutation is real** — run renames against `$env:TEMP\pr-test-<random>`, not real fixtures.
- **COM cache staleness** when re-checking verbs after enable/disable — call `Reset-PtShellComCache` from `scripts/pt-shell-verbs.ps1`.
- **Don't** try `Invoke-PtShellVerb 'PowerRename'` — returns False on Win11 (no classic registration). Use synthetic menu via `Invoke-PtContextMenuItem` or direct-CLI.
- **Don't** run renames against reusable fixtures — copy to a disposable temp folder. Don't trust screenshot-only for icon/extended checks (use registry). Don't skip the synthetic-menu test for menu-presence — CLI back-door false-PASSes when the entry is correctly hidden (see `references/explorer-context-menu-flow.md`).

## Source citations

- `src\modules\PowerRename\dllmain.cpp` — IExplorerCommand registration (no classic HKCR shadow on Win11).
- `src\modules\PowerRename\PowerRenameUILib\` — XAML for main PR window (toggle/checkbox AutomationIds).
- `src\modules\PowerRename\PowerRenameLib\Settings.cpp` — settings.json schema canonical property names.
