# Explorer context-menu flow — driving PowerToys shell-menu modules end-to-end

**Audience**: agents verifying any PowerToys module whose entry point is the **Windows Explorer right-click context menu** — i.e. **File Locksmith, Image Resizer, PowerRename, New+ (NewPlus)**, and similar.

This is the *true user flow*: open Explorer → select file(s) → right-click → click the module's menu item. Use it when an item's assertion is specifically about the **context menu** (e.g. "the entry appears / no longer appears", "right-click → X launches the module on the selection"). For the module's *internal* behavior you can still prefer a faster back-door (CLI / `last-run.log` / Named Event) — see each module profile — but the menu presence/launch itself can only be observed this way.

All the machinery lives in **`scripts/pt-explorer-contextmenu.ps1`** — dot-source it and call the functions; this doc only shows the invocations, not re-implementations. Functions:
`Test-PtDesktopInteractive`, `Open-PtExplorerWindow`, `Open-PtBackgroundContextMenu`, `Open-PtExplorerContextMenu`, `Open-PtClassicContextMenu`, `Get-PtContextMenuItems`, `Invoke-PtContextMenuItem`, `Set-PtModuleEnabledViaSettingsUI`.

**One pattern for every menu:** an `Open-Pt…ContextMenu` opener returns a menu HWND; then `Get-PtContextMenuItems -MenuHwnd <h>` reads it and `Invoke-PtContextMenuItem -MenuHwnd <h> -ItemName <n>` acts on it — identically for the background, file, and legacy `#32768` menus.

## Which approach first? (CLI / back-door vs synthetic menu)

**Pick the tool by what the item ASSERTS — not "always synthetic" or "always CLI".**

| The item asserts… | First approach | Why |
|---|---|---|
| **The menu itself** — entry *appears / no longer appears*, "right-click → select X", caption / localization of the entry | **Synthetic Explorer menu (this doc)** — the *only* valid observer | The CLI/back-door is **blind to the menu**: it runs even when the entry is correctly hidden, so it gives a false PASS (the L652 trap). If the desktop is locked → `BLK-ENV`; do **not** substitute the CLI. |
| **Module behavior** — engine finds the lockers, images get resized, files get renamed (the menu is just the trigger) | **CLI / back-door** (`FileLocksmithCLI.exe`, `last-run.log`, Named Event, DSC) | Instant, deterministic, foreground-free, works on a locked desktop. Synthetic adds ~10s + foreground/retry fragility without changing the assertion. |

**Golden-path rule (do once per module):** run **one** full synthetic right-click → invoke-the-item → confirm-launch. That proves the menu→launch wiring is actually registered *and* validates that the fast back-door is behaviorally equivalent to the real menu (e.g. File Locksmith L641 `step-04/05` did exactly this). After that one golden run, trust the back-door for the remaining behavior items.

Net: for a context-menu module, **most items are behavior → CLI-first**; the **menu-presence/absence/launch/localization items → synthetic-first**; plus one golden-path synthetic launch.

## Is it stable?

**Yes — with the robust variant below.** Two rules make it reliable; ignore them and it gets flaky:

1. **Invoke the menu item by UIA InvokePattern, not a coordinate left-click.** The menu item exposes `InvokePattern` (`isInvokable=True`). `winapp ui invoke <selector> -w <menuHwnd>` is robust and needs no foreground/coordinates for the *click*. A synthetic left-click at the item's pixel center also works but is the fragile part (DPI, menu repositioning near screen edges, scrolled menus).
2. **The right-click that OPENS the menu still needs synthetic input on a foregrounded window — and occasionally a retry.** The first right-click right after Explorer opens sometimes misses (foreground not settled). `Open-PtExplorerContextMenu` retries up to 3×; that removed the flakiness in testing.

**Hard prerequisite — unlocked interactive desktop.** Synthetic right-click injects into the session input stream, so it requires foreground. If the workstation is locked / RDP minimized (`GetForegroundWindow()=0`), this flow is `BLK-ENV` — there is no foreground-free way to open a context menu. `Open-PtExplorerContextMenu` throws a clear BLK-ENV error in that case. (A 4-hour idle auto-lock is the common culprit — see `references/environment-setup.md`.)

**Other constraints:**
- **Settings for these modules live in a module-OWNED file, not the PT-store `settings.json`** — see `SKILL.md` pitfall #17. The context-menu handler reads e.g. `power-rename-settings.json` / `file-locksmith-settings.json` / `image-resizer-settings.json` / `NewPlus\settings.json` at launch; editing the PT-store `<Module>\settings.json` (what `Get-PtModuleSettings` reads) often has **no effect** on the live handler. Drive icon/extended-menu/feature toggles via the module-owned file + relaunch (restart runner+Explorer for the menu handlers), then restore.
- This is the **Win11 packaged** context menu (`Microsoft.UI.Content.PopupWindowSiteBridge` / "PopupHost"). The packaged module commands appear **only** here — not in classic `Shell.Application.Verbs()` and not via `CoCreate` of the command CLSID (`REGDB_E_CLASSNOTREGISTERED`). On Win10, or under "Show more options", you'd get the classic menu instead (different structure) — see **[Reading the legacy "Show more options" (`#32768`) menu](#reading-the-legacy-show-more-options-32768-menu)** below.
- The menu exists in the UIA tree **only while open** — you must open it with real input first; you can't enumerate it cold.
- A menu-launched module UI runs **non-elevated** (Explorer's integrity), even if your agent shell is elevated. Mind elevation-visibility (e.g. a non-elevated File Locksmith can't see higher-IL processes — match locker integrity with `scripts/pt-nonelevated.ps1`).

## Recipe (robust)

```powershell
. "$skill\scripts\pt-explorer-contextmenu.ps1"

# 0) Guard: must be an unlocked desktop
if (-not (Test-PtDesktopInteractive)) { <# mark BLK-ENV, cite references/environment-setup.md #> }

# 1) Open Explorer on the target folder and get its CabinetWClass HWND
$hwnd = Open-PtExplorerWindow -Path $dir

# 2) Open the real context menu (synthetic right-click, auto-retry)
$menu = Open-PtExplorerContextMenu -ExplorerHwnd $hwnd -FileName 'target.txt'

# 3a) ASSERT PRESENCE / ABSENCE (e.g. "entry no longer appears" when the module is disabled)
$present = (Get-PtContextMenuItems -MenuHwnd $menu) -contains 'Unlock with File Locksmith'

# 3b) LAUNCH the module via the real menu (UIA invoke by NAME — robust)
$ok = Invoke-PtContextMenuItem -MenuHwnd $menu -ItemName 'Unlock with File Locksmith'

# 4) Verify the module launched (its process/window appears) — e.g.:
Start-Sleep 4
$ui = Get-Process PowerToys.FileLocksmithUI -EA SilentlyContinue   # or PowerToys.ImageResizer, PowerToys.PowerRename
```

To **assert absence** after disabling a module: re-open the menu and check `Get-PtContextMenuItems` no longer contains the caption (the packaged `GetState` re-reads the enabled flag live, so no Explorer restart is needed between toggles).

## Enabling / disabling the module — via the Settings-UI toggle

For any *"check enable/disable of the module works"* item, flip the module through the **real Settings
UI enable switch**. Two reasons the UI toggle is the correct method:

1. **It's the faithful user flow** the checklist is describing (a user flipping the module's switch),
   and it exercises the **Settings → runner IPC enable/disable path**.
2. **It takes effect live** — the context-menu entry appears/disappears the instant you flip the
   toggle, no runner restart. For New+ the toggle additionally
   runs the enable-time `CopyTemplateExamples` that seeds the default templates (see
   `references/modules/new-plus.md`).

Use the shipped helper **`Set-PtModuleEnabledViaSettingsUI`** (`scripts/pt-explorer-contextmenu.ps1`).
It opens Settings straight on the module page via `--open-settings=<tag>`, discovers the enable
ToggleSwitch by its `[on]/[off]` state (the AutomationId carries a per-session suffix — never
hard-code it), flips it only if needed, and cross-checks the `enabled.<key>` flag for evidence:

```powershell
. "$skill\scripts\pt-explorer-contextmenu.ps1"
try {
    $r = Set-PtModuleEnabledViaSettingsUI -PageTag PowerRename -Enabled $false -EnabledKey PowerRename
    # $r.State -eq 'off', $r.EnabledFlag -eq $false  → now assert the menu entry is GONE
    $menu = Open-PtBackgroundContextMenu -ExplorerHwnd $hwnd
    $gone = -not ((Get-PtContextMenuItems -MenuHwnd $menu) -match 'Rename with PowerRename')
}
finally {
    Set-PtModuleEnabledViaSettingsUI -PageTag PowerRename -Enabled $true -EnabledKey PowerRename  # restore
    # close the Settings window the helper opened, e.g. winapp ui invoke btn-close-<id> -w $r.SettingsHwnd
}
```

Per-module arguments (`-PageTag` for `--open-settings`, `-EnabledKey` = the `enabled.<key>` to read
back as evidence — note the key uses the **display name with spaces**):

| Module | `-PageTag` | `-EnabledKey` | Menu caption to assert |
|---|---|---|---|
| PowerRename | `PowerRename` | `PowerRename` | `Rename with PowerRename` |
| File Locksmith | `FileLocksmith` | `File Locksmith` | `Unlock with File Locksmith` |
| Image Resizer | `ImageResizer` | `Image Resizer` | `Resize with Image Resizer` |
| New+ | `NewPlus` | `NewPlus` | `New+` |

> The toggle needs an unlocked interactive desktop. If the desktop is locked / RDP-minimized so the
> Settings UI can't be driven, mark the item **`BLK-ENV`** (cite `references/environment-setup.md`).


## Which menu — decided per module (see the module profile)

**Whether a module's entry lives on the file menu, the folder-background menu, or both is
module-specific — it is recorded in each `references/modules/<module>.md` (the module's
"Context-menu routing" note), not here.** This doc only provides the openers and the machinery;
the module profile says which opener to call and the exact caption to match.

## The openers & operations — the machinery

**Openers** (each returns a menu HWND):

- **`Open-PtExplorerWindow -Path <dir>`** — opens Explorer on a folder and returns its `CabinetWClass`
  HWND (int), the handle the menu openers below take. (Discovery via `list-windows`; polls until the
  window appears.)
- **`Open-PtBackgroundContextMenu -ExplorerHwnd <h>`** — folder-background menu via **Shift+F10 with
  nothing selected**. Coordinate-free (can't miss a row or land in the preview pane); it *validates* it
  opened a background menu (View/Sort by/Group by) or throws.
- **`Open-PtExplorerContextMenu -ExplorerHwnd <h> -FileName <name>`** — a specific file's menu. It
  COM-selects the item first (reliable, never opens/renames it), right-clicks it via
  `winapp ui click --right` (winapp resolves the element point), and **validates it got a FILE menu
  (Open/Cut/Copy/Delete)** — if it only got the background menu it retries, then **throws** rather than
  passing a wrong menu off as the file menu.
- **`Open-PtClassicContextMenu -MenuHwnd <modernMenu>`** — from an open modern menu, expands "Show more
  options" and returns the legacy `#32768` menu's HWND (see [Reading the legacy menu](#reading-the-legacy-show-more-options-32768-menu)).

**Operations** (take any menu HWND from the openers above — modern *or* classic):

- **`Get-PtContextMenuItems -MenuHwnd <h>`** — returns the menu's item captions (present/absent).
- **`Invoke-PtContextMenuItem -MenuHwnd <h> -ItemName <name>`** — invokes an entry by caption (`$false` = absent).

> **Coordinates are not fully avoidable for the file menu.** `winapp ui click --right` still resolves
> the element's bounding-rect point, so a wide Details row / an open **preview pane** / DPI scaling can
> push the click into the preview pane → the file menu won't open. The helper now **fails loudly**
> instead of silently returning the folder-background menu. Mitigations: close the preview pane
> (Alt+P) for a deterministic layout, or if it still can't land, mark the item `BLK-ENV` — never accept
> the background menu as a stand-in for the file menu.

## Reading the legacy "Show more options" (`#32768`) menu

The two openers above (and `Get-PtContextMenuItems`) only reach the **modern** Win11 menu
(`PopupWindowSiteBridge`). The legacy menu — what you get under **"Show more options"** (or on Win10,
or when a module registers `ExtendedContextMenuOnly` / "Appear only in extended menu") — is a classic
Win32 `#32768` popup with a different structure, and it needs a different read path.

**The one gotcha:** the `#32768` window is **not returned by `winapp ui list-windows`**. So
`Get-PtContextMenuItems` (which discovers the menu HWND *from* `list-windows`) can't reach it, and
neither can the openers above. That does **not** mean it's invisible to UIA — its item subtree is
fully readable once you obtain the HWND another way. The helpers do this for you: they invoke
"Show more options" on the modern menu, resolve the classic menu's HWND via Win32
`FindWindow('#32768', $null)`, then read it with `winapp ui inspect -w <hwnd>` — an exact,
locale-accurate present/absent signal, no OCR or screenshot guessing.

```powershell
. "$skill\scripts\pt-explorer-contextmenu.ps1"

# Same 2-step pattern as the modern menu: an opener returns a menu HWND, then the GENERIC reader/actor.
# Open the modern menu (folder-background here; use Open-PtExplorerContextMenu -FileName <f> for a file's menu),
# expand "Show more options" to get the classic #32768 HWND, then read it with Get-PtContextMenuItems.
$menu    = Open-PtBackgroundContextMenu -ExplorerHwnd $hwnd
$classic = Open-PtClassicContextMenu    -MenuHwnd $menu       # returns the #32768 HWND (int)
$items   = Get-PtContextMenuItems       -MenuHwnd $classic    # SAME reader as the modern menu
$present = $items -match 'Rename with PowerRename'            # exact caption match

# Need to screenshot the classic menu? You already have its HWND:
#   winapp ui screenshot -w $classic -o <png>
```

Notes:
- **A screenshot is corroborating evidence only**, not the verifier — `winapp ui screenshot -w $classic -o <png>` is fine to attach, but the pass/fail signal is the UIA MenuItem name text above.
- **Duplicate entries are normal on Win11**: a module can surface **twice** in the classic menu (the packaged `IExplorerCommand` *and* the legacy classic `IContextMenu` handler both appear). For a present/absent assertion either satisfies it; don't treat the duplicate as a bug.

## Matching the caption

Match the module's entry by its **visible caption** (a string), NOT the AutomationId — Explorer
assigns per-session numeric IDs like `32012` whose value/order varies. **The exact caption per module
lives in that module's profile**; discover/confirm it at runtime with `Get-PtContextMenuItems`
(enable the module, open the applicable menu, read the exact string), then hard-match it for
present/absent assertions.

## Common failure modes → fixes

| Symptom | Cause | Fix |
|---|---|---|
| `BLK-ENV: ... GetForegroundWindow()=0` | desktop locked / RDP minimized | unlock & keep mstsc un-minimized (`references/environment-setup.md`); mark `BLK-ENV`, not a test failure |
| "popup not found after N attempts" | foreground not settled (esp. first right-click after Explorer opens) | the helper already retries 3×; raise `-MaxTries`, or pre-foreground the window once before calling |
| menu item `invoke` returns but nothing launches | matched the wrong node / item disabled | match `type -eq 'MenuItem'` by exact Name; confirm the module is enabled |
| caption not found though module enabled | wrong/old caption string, or it's only under "Show more options" (classic `#32768` menu — which `list-windows` can't see) | for the modern menu, `Get-PtContextMenuItems`; for the classic menu, `Open-PtClassicContextMenu` then the same `Get-PtContextMenuItems` — see [Reading the legacy menu](#reading-the-legacy-show-more-options-32768-menu) |
| launched UI shows nothing | menu-launched UI is non-elevated and can't see higher-IL targets | match target integrity (`scripts/pt-nonelevated.ps1`) |

## Referenced by
- `references/modules/file-locksmith.md` (L641/L652 — real right-click launch + menu present/absent)
- *(future)* `references/modules/image-resizer.md`, `references/modules/power-rename.md`, `references/modules/new-plus.md` — reference this doc for their context-menu items.
