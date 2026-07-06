---
name: powertoys-module-verification
description: "Verify a single PowerToys module's release checklist items end-to-end. Drive each checkbox via UIA / Named Events / settings.json edits / clipboard inspection / GPO / SendInput. Output a structured PASS / FAIL / BLOCKED verdict per item with evidence (FAIL distinguishes product defects from stale/ambiguous checklist items). Combine standard winapp ui mechanics (see references/winapp-ui-testing.md) with PT-specific recipes and the helper .ps1 files shipped with this skill."
license: Complete terms in LICENSE.txt
---

## When to use this skill

Use this skill when you need to **verify every checklist item for a single PowerToys module** for a release sign-off — e.g. "verify all 18 Color Picker items", "verify all 88 Command Palette items". Each item produces a PASS / FAIL / BLOCKED verdict with evidence (UIA enumeration, log line, settings.json diff, screenshot, etc.).

The **checklist to verify is supplied with the task** (the calling prompt points you at the module's checklist file). This skill is the *how* — the drive techniques, helpers, taxonomy, and reporting format — independent of any specific checklist.

## Required reads (in order)

1. **`references/winapp-ui-testing.md`** — the **prerequisite** UIA mechanics doc (winapp ui verbs, scripted batch testing, file pickers, accessibility audits, screenshots, click-vs-invoke, PostMessage, SendInput cb=40, stunted-UIA recovery, settings-mutation safety contract). **Read this first** — this skill assumes you know its content and only adds PT-specific extensions.
2. **This `SKILL.md`** — the PT-specific playbook: the 3-bucket drive-technique selector (Step 2), classification taxonomy, critical pitfalls, helper-script catalog.
3. **`references/modules/<module>.md` IF IT EXISTS** — per-module entry-paths, item-by-item recipes, common BLOCKED traps, fixture lists, source citations. **Always check `references/modules/` first.** If no profile exists, fall back to this SKILL.md and create one after you finish (template in `references/modules/README.md`).
4. **`references/explorer-context-menu-flow.md` IF your module registers an Explorer right-click entry** (PowerRename, File Locksmith, Image Resizer, New+, Preview Pane, RegistryPreview) — shared synthetic-right-click + UIA-invoke + multi-file-selection flow + module-caption table. Helper: `scripts/pt-explorer-contextmenu.ps1`.
5. **`references/pre-flight.md`** — pre-flight checks, bootstrap snippet, state-hygiene cleanup, final wrap-up, hard rules.
6. **`references/reporting-format.md`** — per-item table template, top-of-report summary, step-table rules, anti-patterns, worked example.
7. **`references/environment-setup.md`** — RDP/sleep/screensaver/session-attachment gotchas. Cite in BLK-ENV verdicts.
8. **`references/release-checklist/<module>.md`** — the checklist for the module under test (one file per module; see `references/release-checklist/index.md` for the full list). Each item carries `[ADMIN: …]` + `[CLARITY: …]` metadata. **This file IS the set of items to verify.**

## Helper scripts shipped with this skill

| File | Purpose |
|---|---|
| `scripts/pt-shared-events.ps1` | `Invoke-PtSharedEvent`, `Test-PtSharedEvent`, `Get-PtSharedEventCatalog` — 56-entry friendly-name map for PT Named Events (CmdPal.Show, AOT.Pin, PowerLauncher.Invoke, LightSwitch.Toggle, ZoomIt.Draw, ...). The deterministic, foreground-free, UIPI-immune way to trigger a module. |
| `scripts/pt-sendinput-chord.ps1` | `Send-PtChord`, `Wait-PtHotkeyAccepted` — last-resort SendInput hotkey injection with the cb=40 fix. Use only when the module has no Named Event and the hotkey itself is the test subject. |
| `scripts/pt-foreground-guard.ps1` | `Test-PtForeground`, `Force-PtForeground`, `Assert-PtForegroundOrAbort` — guard helpers to ensure target window IS foreground before SendInput, so keys don't leak to caller's terminal. |
| `scripts/pt-cmdpal-recycle.ps1` | `Reset-CmdPalAppX`, `Reset-CmdPalToHome`, `Test-CmdPalDegraded`, `Invoke-CmdPalQuery` — CmdPal-specific lifecycle (handles TextChanged-broken state, BackButton navigation, AppX recycle). |
| `scripts/pt-admin-probe.ps1` | `Test-PtAdmin`, `Test-ProcessElevated`, `Test-PtRunnerAdmin` — TokenElevation probes to verify your session and the PT runner have the right elevation for the test. |
| `scripts/pt-clipboard-diff.ps1` | `Get-PtClipboardFormats`, `Compare-PtClipboardFormatDiff`, `Set-PtClipboardRich` — multi-format clipboard inspection for Advanced Paste tests. |
| `scripts/pt-explorer-com.ps1` | `Get-PtExplorerWindows`, `Open-PtExplorerAtPath`, `Select-PtExplorerFiles`, `Invoke-PtPeekWithExplorerSelection`, `Test-PtInteractiveDesktop` — drive Explorer via Shell COM to set up multi-file selections, then trigger Peek/FZ/PowerRename/Image Resizer/Workspaces via their hotkeys. **Use this for Peek L706-L709, L719-L720 and any test that needs an Explorer file selection.** |
| `scripts/pt-explorer-contextmenu.ps1` | `Test-PtDesktopInteractive`, `Open-PtExplorerContextMenu`, `Invoke-PtContextMenuItem`, `Get-PtContextMenuItems` — open Win11's real context menu via synthetic right-click (with retry), then UIA-invoke a menu item by name. **Canonical user-flow path for File Locksmith / Image Resizer / PowerRename / New+ menu-presence + launch tests.** Needs an unlocked interactive desktop. See `references/explorer-context-menu-flow.md` for the full write-up, stability notes, and per-module captions. |
| `scripts/pt-shell-verbs.ps1` | `Get-PtShellVerbs`, `Invoke-PtShellVerb`, `Reset-PtShellComCache` — enumerate + invoke CLASSIC HKCR shell verbs via Shell.Application COM. **NOT for PT context-menu modules on Win11** (PT registers via IExplorerCommand, not classic — use `pt-explorer-contextmenu.ps1` for those). Useful for non-PT verbs (Open/Edit/Send-to/third-party) and as a negative check that PT verbs are NOT classic-shadowed. |
| `scripts/pt-state.ps1` | `Get-PtSettings`, `Get-PtModuleSettings`, `Get-CmdPalSettings`, `Get-PtRunnerLogTail`, `Test-PtModuleEnabled`, `Test-PtModuleProcess`, `Restart-PtRunner`, `Backup-PtModuleSettings`, `Restore-PtModuleSettings` — common state checks. |
| `scripts/pt-nonelevated.ps1` | `Start-PtNonElevated`, `Invoke-PtNonElevatedCapture` — launch an exe at **Medium IL (non-elevated)** from an elevated agent shell via a one-shot `RunLevel Limited` scheduled task. Required for elevation-visibility tests (a non-elevated module must NOT see higher-integrity processes; e.g. File Locksmith L649/L650). Verify the result with `Test-ProcessElevated`. |

Dot-source them **all** at once in your bootstrap (the `Get-ChildItem` loop loads every helper — see **Step 1 — Bootstrap**):
```powershell
$skill = '<this skill folder>'   # the folder containing SKILL.md, e.g. <PT-repo>\.github\skills\powertoys-module-verification
Get-ChildItem "$skill\scripts" -Filter '*.ps1' | ForEach-Object { . $_.FullName }
```

## Step 1 — Bootstrap

```powershell
$module = 'AdvancedPaste'  # or 'CmdPal', 'FZ', 'Peek', ...
# Work out of %TEMP% during the run (keeps screenshots/scratch off OneDrive); move to the
# sign-off archive at the very end (see Step 7).
$workspace = "$env:TEMP\verify-$module-$(Get-Date -Format yyyyMMdd-HHmmss)"
New-Item -ItemType Directory -Path $workspace, "$workspace\artifacts" -Force | Out-Null
$report = "$workspace\verify-$module.md"

# Dot-source helpers
$skill = '<this skill folder>'   # set once at top of your script (the folder containing SKILL.md)
Get-ChildItem "$skill\scripts" -Filter '*.ps1' | ForEach-Object { . $_.FullName }

# Verify environment
"=== Environment ===" | Tee-Object $report -Append
"IsAdmin: $(Test-PtAdmin)" | Tee-Object $report -Append
$rn = Test-PtRunnerAdmin
"PT runner: PID=$($rn.Pid) Elevated=$($rn.Elevated)" | Tee-Object $report -Append

# The checklist items to verify are supplied with the task (see the calling prompt).
# Read that module's checklist file and iterate its items (see Step 6 — Verifier loop).
```

## Step 2 — Drive techniques

Every checklist item boils down to ONE of three intents. **Pick the bucket from the verb in the item, then use the best technique inside it.** Stop at the first technique that works.

| Intent | Verb-cues in the checklist item | Bucket |
|---|---|---|
| Change a setting | "default is X", "setting persists", "is enabled/disabled by default", "value Y is accepted" | §2.A |
| Interact with a UI element | "click X", "toggle X", "type into Y", "X button is visible", "selecting Z does W" | §2.B |
| Trigger a module action | "pressing hotkey X opens Y", "module launches", "Z happens when invoked" | §2.C |

### §2.A — Change a setting (single technique)

Edit the JSON file the module reads, wait for the file-watcher debounce, assert, then restore from backup. Zero external tools.

```powershell
$bk = Backup-PtModuleSettings -ModuleDir AdvancedPaste
try {
    $j = Get-PtModuleSettings -ModuleDir AdvancedPaste
    $j.properties.IsAdvancedAIEnabled.value = $false
    $j | ConvertTo-Json -Depth 12 | Set-Content "$env:LOCALAPPDATA\Microsoft\PowerToys\AdvancedPaste\settings.json"
    Start-Sleep -Seconds 4  # debounce — runner re-reads via file-watcher
    # ... assertion ...
} finally {
    Restore-PtModuleSettings -ModuleDir AdvancedPaste -BackupPath $bk
}
```

> For shell-extension modules (PowerRename, File Locksmith, Image Resizer, New+) edit the **module-owned** file under `%LOCALAPPDATA%\Microsoft\PowerToys\<Module>\`, then `Restart-PtRunner` (and on stubborn handlers, restart Explorer). See pitfall #18 below.
>
> If you need to flip the *enabled* bit for a whole module, debounce isn't enough — call `Restart-PtRunner` after the write.

### §2.B — Interact with a UI element (2 techniques, most-reliable first)

#### B1. UIA invoke / set-value — **always try first**
```powershell
winapp ui invoke 'SubmitButton' -a PowerToys.Settings
winapp ui set-value 'QueryTextBox' '=2+3*4' -a PowerToys.PowerLauncher
Start-Sleep -Milliseconds 600
winapp ui inspect -a PowerToys.PowerLauncher --depth 7 -i 2>$null
```
Invoke goes through UIA InvokePattern COM IPC — no foreground steal, no UIPI. See references/winapp-ui-testing.md §CRITICAL — invoke vs click.

#### B2. PostMessage WM_KEYDOWN/CHAR — when UIA can't reach the target
For elevated targets, AppX windows with stunted UIA trees, or keystrokes that UIA `set-value` can't dispatch (arrow-key ListView nav, Enter to commit). See references/winapp-ui-testing.md §CRITICAL — Keystroke input that bypasses UIPI (PostMessage). Esc is often filtered by WinUI 3 raw-input hook — use BackButton invoke instead.

### §2.C — Trigger a module action (2 techniques, most-reliable first)

| | C1 Named Event | C2 SendInput chord |
|---|---|---|
| **Proves** | The action fires (the path *downstream* of the hotkey). **Not** that the chord is bound. | The full path: real keys → runner hook → action. The **only** method that proves the chord binding itself. |
| **Robustness** | Highest — no foreground, no input desktop, UIPI-immune; works headless / RDP-minimized. | Lowest — needs an attached input desktop (else `BLK-ENV`), steals foreground, can't inject OS-reserved chords (Win+L / Win+Tab). |
| **Precondition** | Owning module process is running (the event only exists while it is). | Attached input desktop + foreground. |

**Pick by what the item asserts:** for "does action Y happen" use C1; for "pressing chord X triggers Y" or "the rebind takes effect", C1 is insufficient (it bypasses the chord) — use C2, or C1 *plus* a runner-log line proving the chord was accepted.

#### C1. Named Event signal — preferred
```powershell
Invoke-PtSharedEvent -Name 'CmdPal.Show'           # opens CmdPal without keyboard
Invoke-PtSharedEvent -Name 'AOT.Pin'               # pins foreground window via AOT
Invoke-PtSharedEvent -Name 'PowerLauncher.Invoke'  # opens PT Run
Invoke-PtSharedEvent -Name 'LightSwitch.Toggle'    # toggles theme
Get-PtSharedEventCatalog | Format-Table            # full list
```
No synthetic input — it's a `SetEvent` on the kernel event the module waits on, the same downstream path the runner's hotkey handler signals. Verify the side effect via UIA (`winapp ui list-windows -a <module>`), a log line (`Get-PtRunnerLogTail`), or settings.json diff (`Get-PtModuleSettings`). The event only exists while the owning process runs, so `Test-PtSharedEvent` doubles as an "is the module alive" check.

#### C2. SendInput chord — last resort / chord-binding verification
Real synthetic keys. Loud (steals foreground) and fragile, but the only way to prove the activation chord is actually bound. The runner's global keyboard hook catches the chord regardless of focus, so the precondition is just an **attached input desktop** (pitfall #13; on a detached desktop `SendInput` returns `ACCESS_DENIED` and the keys vanish → mark `BLK-ENV`).
```powershell
# Precondition: input desktop attached? 0 = detached → don't bother sending, mark BLK-ENV (pitfall #13)
if ([PtFg]::GetForegroundWindow() -eq [IntPtr]::Zero) { throw 'No input desktop — BLK-ENV (pitfall #13)' }

Send-PtChord -Mods 0x5B,0x10 -Key 0x43           # Win+Shift+C → Color Picker  (cb=40 fix is inside the helper)
$line = Wait-PtHotkeyAccepted -ModuleHint 'Color' -TimeoutSec 3
if (-not $line) { throw 'Runner did not log hotkey invocation' }
```

> **Rare fallback — a module that uses its own `RegisterHotKey` and exposes no Named Event.** Post `WM_HOTKEY` (`0x312`) straight to its message window (find the HWND via `EnumWindows`+`GetClassName` through `Add-Type` — same P/Invoke pattern as `pt-foreground-guard.ps1`). **No current PT module needs this:** ZoomIt — the obvious candidate — also waits on Named Events (`ZoomIt.Zoom`, `ZoomIt.Draw`, …; source: `Zoomit.cpp` `CreateEventW(ZOOMIT_ZOOM_EVENT)`), so drive it with C1.

> **Different case — sending keys *into* a specific focused window** (e.g. a CmdPal alias like `=` / `<` / `>` that `winapp ui set-value` can't trigger because it bypasses TextChanged; see pitfalls #4 and #6). Here the keystrokes go to whatever currently has focus, so you must bring the target window foreground first:
> ```powershell
> Assert-PtForegroundOrAbort -AppId Microsoft.CmdPal.UI   # -AppId = the window you're typing INTO
> Send-PtChord -Key 0xBB                                  # '=' (no modifiers) to trigger the calculator alias
> ```
> The `-AppId` is whatever window you're targeting — it's **not** CmdPal-specific. CmdPal is just the worst offender: its AppX foreground-lock drops focus after the first `SetForegroundWindow`, so without the guard the keys silently leak to your terminal.

> Verdict decisions (PASS if behavior matches spec; **FAIL** if the product is wrong *or* the checklist item is stale/ambiguous; BLOCKED if you couldn't run the check after ≥2 entry-paths) live in **Step 3 — Classification taxonomy** below. Don't put verdict logic in Step 2.

## Step 3 — Classification taxonomy

### Verdicts (assign exactly ONE per item)

| Verdict | Meaning |
|---|---|
| **PASS** | You drove/observed the behavior and it matched the spec. **A pass is a pass — there is no PASS sub-type.** Record *how* you verified in the item's **Category** field as free text, e.g. "full UIA flow + asserted popup", "settings.json round-trip", "runner-log line", "Shell COM / IExplorerCommand", "screenshot pixel-diff", "output matches fixture", "process spawn/exit", "module CLI", "admin GPO write". |
| **FAIL** | The item is **red** — something is wrong and action is required. Treat the checklist as test code: a test fails because **the product is wrong** *or* **the test/checklist is wrong**. Record the **cause** in the **Category** field: <br>• **product** — behavior contradicts a valid spec → file a product bug (repro + expected-vs-actual + screenshot/log + build version). <br>• **checklist** — the item itself is broken: *stale* (feature was removed/deprecated — cite the source grep proving it's gone) or *ambiguous* (`[CLARITY: VAGUE-*]`, no definable pass/fail criterion — quote the original wording). Fix the checklist, not the product. |
| **BLOCKED** | Couldn't run the check in this environment / with this toolset *after ≥2 entry-paths* — inconclusive, like a skipped test. **Not red against the product.** Tag exactly one concrete reason below. |

### BLOCKED reasons
Different failure reasons stay distinct because each drives a different remediation.

| Reason | When |
|---|---|
| `BLK-ENV` | This specific shell can't drive it (non-interactive / Session 0, RDP-minimized, missing Explorer windows) but a normal interactive desktop CAN. Triggers a "re-run on an interactive desktop" recommendation. Cite `references/environment-setup.md`. |
| `BLK-HARDWARE` | Needs hardware this session lacks — multi-monitor, 2 physical PCs (MWB), real camera / battery / game-mode, or live screen/device capture. State the specific shortfall in **Category**. |
| `BLK-DRAG-REQUIRED` | Needs a real mouse-drag gesture; synthetic drag is insufficient (e.g. FancyZones snap). |
| `BLK-DESTRUCTIVE` | Reboot, hibernate, install/uninstall, or mid-session AppX uninstall — would damage the run environment. |
| `BLK-VISUAL-RENDER` | The thing to verify is a rendered surface UIA can't see — WinUI3 islands, WebView2, or Explorer-side context-menu rendering/localization. Needs pixel/OCR or a manual eyeball. |
| `BLK-OVERLAY-INPUT-BLOCK` | Overlay both blocks input and excludes itself from capture (`BlockInput` + `WDA_EXCLUDEFROMCAPTURE`, e.g. ZoomIt draw mode) — can neither drive nor screenshot it. |
| `BLK-EXTERNAL-APP` | Needs a 3rd-party tool, a real API key, or a system locale change. |

**Rule of thumb**: in your report, separate the two FAIL causes — *product* FAILs are bugs to file; *checklist* FAILs are items to rewrite or prune. `BLOCKED` is only for a concrete, named obstacle (cite it), never a substitute for effort. If a large share of a module's items are checklist-FAILs, the checklist needs an overhaul before re-verifying.

## Step 4 — Report format

**See `references/reporting-format.md` for the full template** (per-item table, summary, step-table rules, anti-patterns, worked example). Don't paraphrase; copy the templates literally. This includes a mandatory **§G Retrospective** — a self-reflection on the *run itself*: list every friction encountered (classified by source — `SKILL-UNCLEAR` / `WINAPP-TOOL-BUG` / `WINAPP-DOC-UNCLEAR` / `HELPER-FLAW` / `PT-PRODUCT` / `ENVIRONMENT` — with severity + minutes/attempts cost + a suggested fix), or write `Everything was smooth — no friction encountered.` if there was none. This is how the skill improves run over run, so don't skip it.

## Step 5 — State hygiene (CRITICAL)

**See `references/pre-flight.md` §State hygiene** for the backup/restore pattern and cleanup commands. Always wrap mutations in `try { ... } finally { Restore-* }`.

## Module-specific quick reference

**Look for `references/modules/<module>.md` FIRST.** Each per-module profile contains paths, entry-paths, item-by-item recipes, common BLOCKED traps, fixture lists, and source citations specific to that module.

Catalog: see `references/modules/README.md`. Currently authored: `peek.md`, `power-rename.md`, `file-locksmith.md`, `image-resizer.md`.

If your module has NO profile yet:
1. Fall back to the generic drive-stack in §2 above.
2. **For Explorer-context-menu modules** (PowerRename / File Locksmith / Image Resizer / New+ / Preview Pane / RegistryPreview): read **`references/explorer-context-menu-flow.md`** first — it has the synthetic-right-click + UIA-invoke pattern with stability rules and module-caption table. Per-module profiles cite it and only document module-specific quirks. The canonical helper is `scripts/pt-explorer-contextmenu.ps1`.
3. After finishing the verification, **create the profile** using the template in `references/modules/README.md` so the next agent benefits from what you learned.

Quick one-liners for modules without dedicated profiles (will be moved to per-module files as they're authored):

- **Advanced Paste**: `Invoke-PtSharedEvent -Name 'AdvancedPaste.ShowUI'` + `Set-PtClipboardRich` + `Compare-PtClipboardFormatDiff` (see `scripts/pt-clipboard-diff.ps1`).
- **Command Palette**: `Invoke-PtSharedEvent -Name 'CmdPal.Show'` + `Invoke-CmdPalQuery` (auto-handles degraded state via `scripts/pt-cmdpal-recycle.ps1`). Settings file via `Get-CmdPalSettings`.
- **PowerToys Run**: `Invoke-PtSharedEvent -Name 'PowerLauncher.Invoke'` + `winapp ui set-value QueryTextBox`. Window has 2 HWNDs — filter by width ≥ 800.
- **FancyZones**: `Invoke-PtSharedEvent -Name 'FancyZones.ToggleEditor'`. Snap-drag tests are usually `BLK-DRAG-REQUIRED`; settings verify via settings.json round-trip.
- **Light Switch**: `Invoke-PtSharedEvent -Name 'LightSwitch.Toggle' | LightSwitch.Light | LightSwitch.Dark`. Verify via `HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme`.
- **Always on Top**: `Invoke-PtSharedEvent -Name 'AOT.Pin'`. Verify `WS_EX_TOPMOST` on pinned HWND.
- **Hosts File Editor** (admin): `Invoke-PtSharedEvent -Name 'Hosts.Show' | Hosts.ShowAdmin`.
- **GPO** (admin): write `HKLM:\Software\Policies\PowerToys` + `Restart-PtRunner` + `Get-PtRunnerLogTail -Pattern 'GPO sets'`. Cleanup: `Remove-Item HKLM:\Software\Policies\PowerToys -Recurse -Force`.
- **Mouse Without Borders**: most items `BLK-HARDWARE` (need 2 physical PCs).
- **ZoomIt**: most modes inside `BlockInput + WDA_EXCLUDEFROMCAPTURE` overlay → `BLK-OVERLAY-INPUT-BLOCK`. Mode triggers: `ZoomIt.Zoom`, `ZoomIt.Draw`, `ZoomIt.Break`, etc.
- **Peek**: see `references/modules/peek.md` for the full recipe (CLI back-door + Shell.Application + Ctrl+Space).

## Step 6 — Verifier loop per checkbox

```
For each item in module:
   1. Pick a bucket from the verb in the item (§2.A change a setting / §2.B interact with UI / §2.C trigger an action)
   2. Walk that bucket's techniques top-to-bottom; stop at the first one that drives the item
   3. Compare observed behavior to the spec:
        • matches the spec       → PASS (note the method in Category)
        • product behaves wrong  → FAIL, cause=product (repro + expected/actual + screenshot/log + build)
   4. Checklist item itself is broken — feature removed from source, or spec too ambiguous to judge → FAIL, cause=checklist (cite the source proof / quote the wording)
   5. Couldn't drive it after ≥2 entry-paths → BLOCKED with a concrete reason (§3)
   6. Record verdict + evidence + cleanup
   7. Next item
```

When done, run state hygiene cleanup, write the report **including the §G retrospective**, archive the workspace (Step 7), and exit.

## Step 7 — Archive the workspace to the sign-off folder (do this LAST)

The live run works out of `%TEMP%`, but the **final deliverable must live in the module sign-off archive** so reports persist and sync via OneDrive:

```powershell
# After the report is written AND the artifact-existence check passes:
$signoff = "$env:OneDrive\PowerToys\Module-Signoff"   # e.g. C:\Users\<you>\OneDrive - Microsoft\PowerToys\Module-Signoff
New-Item -ItemType Directory -Path $signoff -Force | Out-Null
$final = Join-Path $signoff (Split-Path $workspace -Leaf)
Move-Item -Path $workspace -Destination $final -Force
# Report uses RELATIVE artifacts/… paths, so all links stay valid after the move.
Write-Host "Final report: $(Join-Path $final (Split-Path $report -Leaf))"
```

Print the **moved** report path (under `…\PowerToys\Module-Signoff\`) as the last line — never the `%TEMP%` path.

## Invocation & placeholders

This skill auto-activates when you ask to verify a PowerToys module's checklist (e.g. "verify all Color Picker items"). **One module per run** — never chain multiple modules into one report. Resolve these placeholders for the module under test:

| Placeholder | Substitute with |
|---|---|
| `<Module>` | Exact display name, e.g. `Color Picker`, `Command Palette`, `PowerToys Run`, `FancyZones` (see `references/release-checklist/index.md`). |
| `<module>` | Lowercase-kebab-case for file lookup, e.g. `color-picker`, `command-palette`, `power-rename` — used for BOTH `references/release-checklist/<module>.md` (checklist) and `references/modules/<module>.md` (profile, if any). |
| `<ModuleDir>` | settings.json sub-dir under `%LOCALAPPDATA%\Microsoft\PowerToys\` (e.g. `AdvancedPaste`, `FancyZones`, `PowerToys Run` (with space)). |
| `<N>` | Total item count for this module. |

**Execution order:** `references/pre-flight.md` → per item, the §2 drive-stack (this file) → `references/reporting-format.md` per-item table → Step 6 verifier loop → `references/pre-flight.md` §Final wrap-up → Step 7 archive → print the final report path.

## What NOT to do

- Do NOT chain multiple modules in one report — one module per run.
- Do NOT mark an item BLOCKED without a concrete, named obstacle (see §3 and `references/pre-flight.md` §Hard rules).
- Do NOT invent steps for a VAGUE checklist item — if the spec is too ambiguous to judge, that is FAIL (cause=checklist), not a guess.
- All other rules (foreground guard, always restore mutated state, etc.) live in `references/pre-flight.md` §Hard rules — follow them.

## Critical pitfalls (PT-specific)

*Reference, not a sequential step — skim before you start and consult while driving. Numbered for cross-reference only.*

1. **PT runner does NOT auto-pickup edits to master `settings.json`** (top-level `enabled.<Module>` flags). Call `Restart-PtRunner`.
2. **Each module's own `<Module>\settings.json` IS hot-reloaded** via per-module file watcher (~3s debounce). **EXCEPTION — shell-extension/context-menu modules do NOT read this file; see pitfall #18.**
3. **PT Run setting key has a space**: `"PowerToys Run"` not `PowerToysRun`.
4. **CmdPal AppX foreground from external CLI is unreliable** — Windows foreground-lock blocks `SetForegroundWindow` after the first call. SendInput keys silently leak to your terminal. **Always `Assert-PtForegroundOrAbort` before SendInput.**
5. **CmdPal AppX enters TextChanged-broken state** every ~30 probes — `Test-CmdPalDegraded` + `Reset-CmdPalAppX` to recover.
6. **CmdPal alias detection (`=`, `<`, `>`, `:`, `$`, `??`, `)`) requires real keystrokes** — `winapp ui set-value` bypasses TextChanged and the alias never fires. Use Send-PtChord + `Assert-PtForegroundOrAbort` for aliases; use set-value for plain queries.
7. **CmdPal Esc handler is filtered** by WinUI 3 raw-input hook — use `winapp ui invoke BackButton` instead (see `Reset-CmdPalToHome`).
8. **GPO HKLM vs HKCU**: HKLM wins when both are set with conflicting values.
9. **HKLM `Software\Policies\PowerToys` writes require admin** — verify with `Test-PtAdmin`.
10. **`Stop-Process` is policy-blocked in this session unless you pass `-Id <int>` literally**. Always inline the PID.
11. **WinUI 3 islands are largely invisible to UIA** (QuickAccess flyout, RegistryPreview Monaco editor, Peek WebView2). For these, fall back to screenshot + OCR or settings.json diff.
12. **OS-reserved chords (Win+L, Win+Tab)** are consumed by Windows before any hook and cannot be injected via SendInput at all.
13. **RDP minimized = `SendInput` denied.** Even though `quser` shows the remote session State=Active, minimizing the mstsc client detaches the session's input desktop. `GetForegroundWindow()` returns 0; `SendInput` returns `ACCESS_DENIED (5)`; tests that need synthetic input fail. **Same applies to: closed mstsc with X (Disconnected), local PC sleep (RDP TCP drops), remote screensaver/workstation lock, remote machine sleep.** Run `scripts/pt-session-diagnose.ps1` in pre-flight to detect, and see `references/environment-setup.md` for the full per-scenario table + `powercfg` setup commands the user should run before starting the agent. The agent should call `Test-PtForeground` mid-run before each input-injection-dependent item; if it returns False, mark `BLK-ENV` with mitigation citation (an environment block — not a product FAIL).
14. **`winapp ui` arg-order quirk**: `winapp ui inspect --depth N -w $hwnd` may intermittently fail to parse `--depth` as Int64 if `-w` precedes it. **Put `-w $hwnd` AFTER `--depth N`** or as the first arg before any flag. If you see "Cannot bind argument" or numeric parse errors, swap the order and retry.
15. **`winapp ui list-windows` line wrapping**: when window titles or process names are long, output may wrap a single window's `HWND <id>: "<title>" ... (proc, PID N)` across multiple lines, breaking single-line regexes. Either pipe through `Out-String` and use a multi-line regex, or use `--json` (when supported) and parse structured output.
16. **De-elevation: launching a NON-elevated (Medium IL) child from an elevated agent shell.** The drive-stack only covers gaining *more* privilege; some items need the opposite. From a High-IL shell you cannot `Start-Process` a Medium-IL child directly. Use `scripts/pt-nonelevated.ps1` (`Start-PtNonElevated` / `Invoke-PtNonElevatedCapture`) — a one-shot `RunLevel Limited` + `LogonType Interactive` scheduled task that lands on the user's desktop at their filtered token. Confirm with `Test-ProcessElevated`. Needed for elevation-visibility pairs (File Locksmith L649/L650: non-elevated FL must not see the elevated runner; elevated FL must).
17. **Win11 packaged context menus are not observable without real Explorer.** Modern PT context-menu entries are packaged `IExplorerCommand`s (sparse MSIX, e.g. File Locksmith CLSID `{AAF1E27D-…}`). They are **NOT** enumerable via classic `Shell.Application … FolderItem.Verbs()` and **NOT** `CoCreate`-able from a non-Explorer host (`REGDB_E_CLASSNOTREGISTERED`). So "verify the entry appears / no longer appears" cannot be pixel-verified by API. Verify instead via the gate flag the entry's `GetState` reads (e.g. general `enabled.<Module>`) + a source citation that maps it to `ECS_HIDDEN`; treat the literal render as `BLK-VISUAL-RENDER` and recommend a 5-second manual right-click. (Disabling does NOT unregister the package — it stays `Status Ok`; the entry is hidden dynamically.)
18. **Shell-extension modules read a module-OWNED settings file, NOT the PT-store `<Module>\settings.json`.** PowerRename, File Locksmith, Image Resizer, and New+ context-menu handlers and exes run *outside* the runner (hosted by Explorer / launched on demand) and cannot use the PT-Settings IPC. Each reads its **own** json in `%LOCALAPPDATA%\Microsoft\PowerToys\<Module>\` *at process/handler launch* (registry-migrated `CSettings`/`Settings` classes — `lib/Settings.cpp` `Load→ParseJson`). The PT-Settings UI writes the *PT-store* `settings.json` (the `bool_*`/`int_*` file `Get-PtModuleSettings` reads); the runner's module DLL syncs PT-store→module-store **only on a Settings-UI change event** — so the PT-store file can be **stale for days** and editing it has **no effect** on the running shell handler. **To drive a settings item on these modules, edit the module-owned file directly (drive-stack §2.A) and relaunch the module (or restart runner+Explorer for the menu handlers), then restore.**

**Pitfall #18 — module-owned files + their key style** (verified 2026-06-10 against `<PT-repo>\src`):

| Module | Module-owned file (under `…\PowerToys\<Module>\`) | Key style | PT-store `settings.json` keys (UI/`Get-PtModuleSettings`) |
|---|---|---|---|
| PowerRename | `power-rename-settings.json` (+ `power-rename-last-run-data.json`, `search-mru.json`, `replace-mru.json`) | `ShowIcon`, `ExtendedContextMenuOnly`, `PersistState`, `MRUEnabled`, `MaxMRUSize`, `UseBoostLib` | `bool_show_icon_on_menu`, `bool_show_extended_menu`, `bool_persist_input`, `bool_mru_enabled`, `int_max_mru_size`, `bool_use_boost_lib` |
| File Locksmith | `file-locksmith-settings.json` | `ShowInExtendedContextMenu` | `bool_show_extended_context_menu` |
| Image Resizer | `image-resizer-settings.json` | (resize sizes/encoder/etc.) | mirrored `imageresizer*` keys |
| New+ | `NewPlus\settings.json` (sub-folder **`NewPlus`**, verified on disk + `constants.h` `powertoy_name=L"NewPlus"`) | `HideFileExtension`, `HideStartingDigits`, `TemplateLocation`, `ReplaceVariables`, `BuiltInNewHidePreference` | mirrored `newplus*` keys |

Confirm which file actually drives behavior with a quick A/B: edit the module-owned file → relaunch → observe; if behavior follows, that's the source of truth (PowerRename L394/L395/L396/L397/L409 were all driven this way).

If you find another gap during verification, update this skill (add a recipe) AND consider proposing the addition to references/winapp-ui-testing.md if it's generic enough.
