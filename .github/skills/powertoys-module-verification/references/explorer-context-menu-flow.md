# Explorer context-menu flow — driving PowerToys shell-menu modules end-to-end

**Audience**: agents verifying any PowerToys module whose entry point is the **Windows Explorer right-click context menu** — i.e. **File Locksmith, Image Resizer, PowerRename, New+ (NewPlus)**, and similar.

This is the *true user flow*: open Explorer → select file(s) → right-click → click the module's menu item. Use it when an item's assertion is specifically about the **context menu** (e.g. "the entry appears / no longer appears", "right-click → X launches the module on the selection"). For the module's *internal* behavior you can still prefer a faster back-door (CLI / `last-run.log` / Named Event) — see each module profile — but the menu presence/launch itself can only be observed this way.

Helper: `scripts/pt-explorer-contextmenu.ps1` (`Test-PtDesktopInteractive`, `Open-PtExplorerContextMenu`, `Invoke-PtContextMenuItem`, `Get-PtContextMenuItems`).

## Which approach first? (CLI / back-door vs synthetic menu)

**Pick the tool by what the item ASSERTS — not "always synthetic" or "always CLI".**

| The item asserts… | First approach | Why |
|---|---|---|
| **The menu itself** — entry *appears / no longer appears*, "right-click → select X", caption / localization of the entry | **Synthetic Explorer menu (this doc)** — the *only* valid observer | The CLI/back-door is **blind to the menu**: it runs even when the entry is correctly hidden, so it gives a false PASS (the L652 trap). If the desktop is locked → `BLK-ENV`; do **not** substitute the CLI. |
| **Module behavior** — engine finds the lockers, images get resized, files get renamed (the menu is just the trigger) | **CLI / back-door** (`FileLocksmithCLI.exe`, `last-run.log`, Named Event, DSC) | Instant, deterministic, foreground-free, works on a locked desktop. Synthetic adds ~10s + foreground/retry fragility without changing the assertion. |

**Golden-path rule (do once per module):** run **one** full synthetic right-click → invoke-the-item → confirm-launch. That proves the menu→launch wiring is actually registered *and* validates that the fast back-door is behaviorally equivalent to the real menu (e.g. File Locksmith L641 `step-04/05` did exactly this). After that one golden run, trust the back-door for the remaining behavior items.

Net: for a context-menu module, **most items are behavior → CLI-first**; the **menu-presence/absence/launch/localization items → synthetic-first**; plus one golden-path synthetic launch.

## Is it stable?

**Yes — with the robust variant below.** Verified repeatedly on Win11 (2026-06-08) launching File Locksmith via a genuine right-click + menu click. Two rules make it reliable; ignore them and it gets flaky:

1. **Invoke the menu item by UIA InvokePattern, not a coordinate left-click.** The menu item exposes `InvokePattern` (`isInvokable=True`). `winapp ui invoke <selector> -w <menuHwnd>` is robust and needs no foreground/coordinates for the *click*. A synthetic left-click at the item's pixel center also works but is the fragile part (DPI, menu repositioning near screen edges, scrolled menus).
2. **The right-click that OPENS the menu still needs synthetic input on a foregrounded window — and occasionally a retry.** The first right-click right after Explorer opens sometimes misses (foreground not settled). `Open-PtExplorerContextMenu` retries up to 3×; that removed the flakiness in testing.

**Hard prerequisite — unlocked interactive desktop.** Synthetic right-click injects into the session input stream, so it requires foreground. If the workstation is locked / RDP minimized (`GetForegroundWindow()=0`), this flow is `BLK-ENV` — there is no foreground-free way to open a context menu. `Open-PtExplorerContextMenu` throws a clear BLK-ENV error in that case. (A 4-hour idle auto-lock is the common culprit — see `references/environment-setup.md`.)

**Other constraints:**
- **Settings for these modules live in a module-OWNED file, not the PT-store `settings.json`** — see `SKILL.md` pitfall #18. The context-menu handler reads e.g. `power-rename-settings.json` / `file-locksmith-settings.json` / `image-resizer-settings.json` / `New\settings.json` at launch; editing the PT-store `<Module>\settings.json` (what `Get-PtModuleSettings` reads) often has **no effect** on the live handler. Drive icon/extended-menu/feature toggles via the module-owned file + relaunch (restart runner+Explorer for the menu handlers), then restore.
- This is the **Win11 packaged** context menu (`Microsoft.UI.Content.PopupWindowSiteBridge` / "PopupHost"). The packaged module commands appear **only** here — not in classic `Shell.Application.Verbs()` and not via `CoCreate` of the command CLSID (`REGDB_E_CLASSNOTREGISTERED`). On Win10, or under "Show more options", you'd get the classic menu instead (different structure).
- The menu exists in the UIA tree **only while open** — you must open it with real input first; you can't enumerate it cold.
- A menu-launched module UI runs **non-elevated** (Explorer's integrity), even if your agent shell is elevated. Mind elevation-visibility (e.g. a non-elevated File Locksmith can't see higher-IL processes — match locker integrity with `scripts/pt-nonelevated.ps1`).

## Recipe (robust)

```powershell
. "$skill\scripts\pt-explorer-contextmenu.ps1"

# 0) Guard: must be an unlocked desktop
if (-not (Test-PtDesktopInteractive)) { <# mark BLK-ENV, cite references/environment-setup.md #> }

# 1) Open Explorer on the target folder and grab its CabinetWClass HWND
Start-Process explorer.exe $dir; Start-Sleep 4
$hwnd = (winapp ui list-windows --json | ConvertFrom-Json |
    Where-Object { $_.className -eq 'CabinetWClass' -and $_.title -match [regex]::Escape((Split-Path $dir -Leaf)) } |
    Select-Object -First 1).hwnd

# 2) Open the real context menu (synthetic right-click, auto-retry)
$menu = Open-PtExplorerContextMenu -ExplorerHwnd $hwnd -FileName 'target.txt'

# 3a) ASSERT PRESENCE / ABSENCE (e.g. "entry no longer appears" when the module is disabled)
$items = Get-PtContextMenuItems -MenuHwnd $menu        # all visible MenuItem names
$present = $items -contains 'Unlock with File Locksmith'

# 3b) LAUNCH the module via the real menu (UIA invoke by NAME — robust)
$ok = Invoke-PtContextMenuItem -MenuHwnd $menu -ItemName 'Unlock with File Locksmith'

# 4) Verify the module launched (its process/window appears) — e.g.:
Start-Sleep 4
$ui = Get-Process PowerToys.FileLocksmithUI -EA SilentlyContinue   # or PowerToys.ImageResizer, PowerToys.PowerRename
```

To **assert absence** after disabling a module: re-open the menu and check `Get-PtContextMenuItems` no longer contains the caption (the packaged `GetState` re-reads the enabled flag live, so no Explorer restart is needed between toggles).

## Multi-file selection (Image Resizer, PowerRename)

These operate on a **selection** of files. Select first (Shell COM is reliable and foreground-free), then right-click one of the selected items:
- Use `scripts/pt-explorer-com.ps1` → `Open-PtExplorerAtPath` + `Select-PtExplorerFiles` to establish the multi-select.
- Then `Open-PtExplorerContextMenu` on one selected file and `Invoke-PtContextMenuItem` — the module receives the whole selection (the shell handler enumerates all selected `IShellItem`s).

## Module captions (match by NAME)

Match the **visible caption**, not the AutomationId (Explorer assigns per-session numeric IDs like `32012` whose value/order varies). Discover the exact caption at runtime with `Get-PtContextMenuItems`. Verified captions:

| Module | Launched process | Menu caption (verified ✓ / expected) |
|---|---|---|
| File Locksmith | `PowerToys.FileLocksmithUI.exe` | ✓ `Unlock with File Locksmith` (NB: **not** the checklist's "What's using this file?") |
| PowerRename | `PowerToys.PowerRename.exe` | ✓ `Rename with PowerRename` |
| Image Resizer | `PowerToys.ImageResizer.exe` | `Resize images` (verify via `Get-PtContextMenuItems` — caption shifted across versions) |
| New+ | (creates from template) | `New+` (submenu) |

> Tip: if a module's caption is unknown, enable the module, open the menu on an applicable file, and run `Get-PtContextMenuItems` to read the exact string — then hard-match it for present/absent assertions.

## Common failure modes → fixes

| Symptom | Cause | Fix |
|---|---|---|
| `BLK-ENV: ... GetForegroundWindow()=0` | desktop locked / RDP minimized | unlock & keep mstsc un-minimized (`references/environment-setup.md`); mark `BLK-ENV`, not a test failure |
| "popup not found after N attempts" | foreground not settled (esp. first right-click after Explorer opens) | the helper already retries 3×; raise `-MaxTries`, or pre-foreground the window once before calling |
| menu item `invoke` returns but nothing launches | matched the wrong node / item disabled | match `type -eq 'MenuItem'` by exact Name; confirm the module is enabled |
| caption not found though module enabled | wrong/old caption string, or it's under "Show more options" (classic menu) | enumerate with `Get-PtContextMenuItems`; for classic menu invoke `expandtoclassic` first |
| launched UI shows nothing | menu-launched UI is non-elevated and can't see higher-IL targets | match target integrity (`scripts/pt-nonelevated.ps1`) |

## Referenced by
- `references/modules/file-locksmith.md` (L641/L652 — real right-click launch + menu present/absent)
- *(future)* `references/modules/image-resizer.md`, `references/modules/power-rename.md`, `references/modules/new-plus.md` — reference this doc for their context-menu items.
