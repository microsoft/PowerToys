# Command Palette (CmdPal) — module verification profile

**PT module**: `CmdPal` / display name **"Command Palette"** (WinUI 3 packaged launcher — search box + extensible result providers/aliases)
**Source**: `src\modules\cmdpal\`
**Settings**: via `Get-CmdPalSettings` (`scripts/pt-state.ps1`) — CmdPal keeps its **own** packaged-app store, **not** the PT-store `<Module>\settings.json` that `Get-PtModuleSettings` reads.
**App identity**: packaged AppX, UIA AppId **`Microsoft.CmdPal.UI`**
**Named Event**: friendly name **`CmdPal.Show`** in the `pt-shared-events.ps1` catalog (foreground-free open)
**Helper**: **`scripts/pt-cmdpal-recycle.ps1`** — `Reset-CmdPalAppX`, `Reset-CmdPalToHome`, `Test-CmdPalDegraded`, `Invoke-CmdPalQuery` (CmdPal-specific lifecycle: TextChanged-broken recovery, BackButton navigation, AppX recycle)

> CmdPal is an **AppX WinUI 3** app, so it inherits the WinUI-island UIA limitations (SKILL.md pitfall #5)
> and an AppX foreground-lock that makes external `SetForegroundWindow` unreliable (see BLOCKED traps).
> Prefer the Named Event + the `pt-cmdpal-recycle.ps1` helpers over raw SendInput wherever possible.

## Entry-paths (try in order)

### 1. Named Event — fastest, foreground-free
```powershell
. "$skill\scripts\pt-shared-events.ps1"
Invoke-PtSharedEvent -Name 'CmdPal.Show'     # opens CmdPal without keyboard/foreground
```
The deterministic, UIPI-immune way to open CmdPal (same downstream path the runner's hotkey handler signals). Verify via `winapp ui list-windows` for the `Microsoft.CmdPal.UI` window.

### 2. Query helper — drive a search + read results
```powershell
. "$skill\scripts\pt-cmdpal-recycle.ps1"
Invoke-CmdPalQuery -Text 'notepad'           # types a PLAIN query; auto-handles degraded state
```
Use for plain-text queries (result list assertions). It routes through the helper so a TextChanged-broken
session is detected + recycled first. **Aliases are different — see trap below.**

### 3. Activation hotkey — only when the chord binding itself is under test
Press the configured activation shortcut (Settings → Command Palette). SendInput requires an attached
input desktop (SKILL.md pitfall #7) **and** you must foreground CmdPal first (trap below); otherwise
prefer entry-path 1.

## Recipes — control/observation map, NOT an answer key

| # | Capability | Drive (control / command) | Observe (where the result shows) |
|---|---|---|---|
| 1 | CmdPal opens & is listening | `Invoke-PtSharedEvent -Name 'CmdPal.Show'` (or the hotkey) | `Microsoft.CmdPal.UI` window appears (`winapp ui list-windows`) |
| 2 | Plain-text query returns results | `Invoke-CmdPalQuery -Text '<q>'` / `winapp ui set-value` the search box | results list updates to matching items |
| 3 | Alias engages (`=` `<` `>` `:` `$` `??` `)`) | **real keystrokes** — `Assert-PtForegroundOrAbort -AppId Microsoft.CmdPal.UI` → `Send-PtChord` (NOT `set-value`) | the alias provider activates (e.g. `=` → calculator result) |
| 4 | Dismiss / navigate back | `winapp ui invoke BackButton` (NOT Esc — filtered) / `Reset-CmdPalToHome` | returns to home / closes |
| 5 | Recover a wedged session | `Test-CmdPalDegraded` → `Reset-CmdPalAppX` | CmdPal responds to input again |
| 6 | A setting round-trips | edit via Settings UI → `Get-CmdPalSettings` | the CmdPal store reflects the change |

> Mapping: read the checklist item → find the capability row → drive that control and design your own
> inputs/assertions. New capability ⇒ add a row.

## BLOCKED traps
- **AppX foreground-lock — SendInput keys leak to your terminal.** External `SetForegroundWindow` on the CmdPal AppX window is unreliable: Windows' foreground-lock blocks it after the first call, so synthetic keys go to whatever is actually foreground (often your caller). **Always `Assert-PtForegroundOrAbort -AppId Microsoft.CmdPal.UI` immediately before any SendInput**; prefer the Named Event (entry-path 1) which needs no foreground.
- **TextChanged-broken state every ~30 probes.** After heavy scripted querying the search box stops raising TextChanged and results freeze. Detect with `Test-CmdPalDegraded` and recover with `Reset-CmdPalAppX` before continuing.
- **Alias detection requires REAL keystrokes.** `winapp ui set-value` on the search box bypasses TextChanged, so alias prefixes (`=`, `<`, `>`, `:`, `$`, `??`, `)`) never fire. Use `Send-PtChord` (after `Assert-PtForegroundOrAbort`) for aliases; reserve `set-value` for plain queries.
- **Esc is filtered** by the WinUI 3 raw-input hook, so "press Esc to go back/close" doesn't work via injection. Use `winapp ui invoke BackButton` (or `Reset-CmdPalToHome`) instead.
