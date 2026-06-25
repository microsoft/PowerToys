# Peek — module verification profile

**PT module**: `Peek` (file previewer activated on Ctrl+Space with Explorer file selected)
**Source**: `<PT-repo>\src\modules\Peek\` (PT repo)
**Settings file**: `%LOCALAPPDATA%\Microsoft\PowerToys\Peek\settings.json`
**Logs**: `%LOCALAPPDATA%\Microsoft\PowerToys\Peek\Logs\v<ver>\log_<date>.log`
**Exes**: `%LOCALAPPDATA%\PowerToys\WinUI3Apps\PowerToys.Peek.UI.exe`
**Default hotkey**: `Ctrl+Space` (modifiers=`ctrl`, code=32; see `settings.json` → `ActivationShortcut`)
**Named Event**: `Local\ShowPeekEvent` (friendly name: `Peek.Show` in `pt-shared-events.ps1` catalog)
**DSC resource**: `Microsoft.PowerToys/PeekSettings`

## Three entry-paths (try in order)

### 1. CLI back-door — fastest, no Explorer needed
```powershell
Start-Process "$env:LOCALAPPDATA\PowerToys\WinUI3Apps\PowerToys.Peek.UI.exe" -ArgumentList "<file>"
```
**Source**: `Peek.UI\PeekXAML\App.xaml.cs:106-134` — when last arg is not int (=runner PID) and is an existing file, it sets `_launchedFromCli=true`, builds `SelectedItemByPath`, calls `OnShowPeek()`. Bypasses hotkey + Explorer foreground.

**Use for**: single-file previewer rendering tests (Recipes 1-2) and the CLI-accepts-path assertion (Recipe 8).

**Cannot use for**: navigation tests (Recipes 4-7, 10-11) — source has `if (_isFromCli) return;` guard that disables arrow navigation, and CLI mode spawns a fresh process every call (no pin-state-across-reopen).

### 2. Shell.Application COM + Ctrl+Space — Explorer-driven, supports navigation
This is the canonical "do what a real user would do" path that drives all the navigation/pin tests.

```powershell
# Dot-source helpers first
. "$skill\scripts\pt-explorer-com.ps1"
. "$skill\scripts\pt-sendinput-chord.ps1"

# Set up multi-file selection in Explorer + trigger Peek in one call:
$peekHwnd = Invoke-PtPeekWithExplorerSelection `
    -FolderPath 'D:\fixtures' `
    -FileNames 'test-markdown.md','test-html.html','test-source.cs'

# Now Peek is open over a 3-file IShellItemArray. Test:
winapp ui invoke 'PinButton' -w $peekHwnd        # pin
# (move window via SetWindowPos)
Send-PtChord -Key 0x27                           # Right arrow → switch file
# verify the pinned position stuck
```

**Use for**: pin behavior, multi-file navigation, file switching (Recipes 4-7, 10-11).

**Requires**: interactive desktop session (`Test-PtInteractiveDesktop` must show both `ForegroundOk=True` and `ShellComOk=True`).

### 3. Named Event signal — quick smoke
```powershell
Invoke-PtSharedEvent -Name 'Peek.Show'
```
Wakes the resident Peek process (different from CLI back-door — respects current Explorer foreground selection). Used by some framework tests for the "Peek is enabled and listening" assertion.

## Recipes — a control/observation map, NOT a per-test-case answer key

> Maps each Peek *capability* to **how to drive it** and **where the result shows**. It does NOT prescribe concrete fixtures/coords/inputs or expected values — design those at runtime from the actual checklist item. Only a real UI/behavior change should force an edit here.

| # | Capability | Drive (control / command) | Observe (where the result shows) |
|---|---|---|---|
| 1 | File-type previewer renders (image / text+code / markdown / PDF / HTML / archive / unsupported) | `Peek.UI.exe <fixture>` (entry-path 1) → `winapp ui inspect -w <hwnd> --depth 7` | the type's previewer node present (`ImagePreview Image`; `PreviewBrowser Pane` for dev/text/md/HTML; archive tree for zip; File-Type/Size/Date view for unsupported). Prefer `winapp ui search` for an in-fixture marker over OCR |
| 2 | "Open with default app" via button | `winapp ui invoke LaunchAppButton` | a new editor process/window for `<file>` appears (PID diff) |
| 3 | "Open with default app" via Enter | `Assert-PtForegroundOrAbort` → `Send-PtChord -Key <Enter>` | same as #2 |
| 4 | Pin keeps window position when switching files | Shell COM + Ctrl+Space (entry-path 2) → `winapp ui invoke PinButton` → move window → navigate to next file | window stays at the pinned coordinates |
| 5 | Pin position persists across close + reopen | pinned → Esc to close (graceful — **don't `Stop-Process`**, it bypasses the pin-save handler) → reopen via Shell COM + Ctrl+Space | new window opens at the same pinned coordinates |
| 6 | Unpin releases the lock; switching file reverts to default | `winapp ui invoke PinButton` again (unpin) → navigate | window moves to the default position |
| 7 | Unpinned reopen uses default position | unpinned → Esc-close → reopen | new window at default, not the stale pinned coords |
| 8 | `Peek.UI.exe <file>` CLI opens Peek | entry-path 1 | covered by #1 across file types |
| 9 | Concurrent Peek sessions don't crash/interfere | launch `Peek.UI.exe` several times on different files, leaving windows open | each spawns its own process/window; no error in `Peek\Logs` |
| 10 | Arrow keys cycle between selected files | Shell COM multi-file selection → Ctrl+Space → `Send-PtChord` Right/Left | window title updates to each file in sequence, wraps at the ends |
| 11 | Multi-file selection scopes navigation | select a subset of a folder → navigate | only the selected files cycle, not the rest |
| 12 | Activation-hotkey reassignment takes effect | edit `Peek\settings.json` `properties.ActivationShortcut` → `Restart-PtRunner` (**not hot-reloaded** — see Gotchas) → press the new chord, then the old chord | new chord opens Peek; old chord does nothing |

> **Mapping process**: read the actual checklist item → identify the capability → find its row → drive the named control and design your own inputs + assertions for *that* item. If no row matches, it's a NEW capability — drive ad-hoc and add a row (capability + control + observation point; no canned inputs).



## BLOCKED triage (single source of truth)

If the agent only tried the CLI back-door and marked the pin / navigation tests BLOCKED → **misdiagnosis**, try entry-path #2 (Shell.Application COM + Ctrl+Space).

If the agent tried Shell COM + Ctrl+Space and got `GetForegroundWindow()=0` + `SendInput → ACCESS_DENIED (5)` → **environment**, not framework. The session has no attached input desktop (RDP minimized, screen locked, screensaver, etc.). See `SKILL.md` pitfall #13 and `references/environment-setup.md` for the per-scenario table + powercfg setup commands. Mark BLK-ENV with mitigation citation.

Both traps were observed in 2026-06-08 sign-off runs; preventing both is now the agent's pre-flight job (`pt-session-diagnose.ps1`).

## Fixture files needed

Put these in a workspace `fixtures/` folder before starting:
- `small-image.png` (any 200x150 PNG)
- `Program.cs` (any C# file)
- `readme.md` (markdown with H1 + bold + bullet list)
- `test-pdf.pdf` (PDF with embedded text "PDF_FIXTURE_OK" + "PDF_MARKER_42")
- `page.html` (HTML with `<h1>` containing "HTMLPEEKMARKER")
- `archive.zip` (zip containing 1 small text file)
- `unsupported.xyz` (any small binary)
- 3 differently-sized images for the pin-position tests (e.g. 320x240, 800x600, 1920x1080)

## Source citations

- `<PT-repo>\src\modules\Peek\Peek.UI\PeekXAML\App.xaml.cs:106-134` — CLI arg parsing, `_isFromCli` flag, OnShowPeek call.
- `<PT-repo>\src\modules\Peek\Peek.UI\PeekXAML\Models\NavigationManager.cs` — `// TODO: implement navigation` + `if (_isFromCli) return;` guards.
- `<PT-repo>\src\common\interop\shared_constants.h` — `ShowPeekEvent` name.

## Ceiling

**18/18 = 100%** achievable from a normal interactive admin console session (verified 2026-06-08). The change-shortcut item is PASS-able via the settings.json + runner-restart path — see Recipe 12.

## Peek-specific gotchas

- **Activation-shortcut is NOT hot-reloaded.** Editing `Peek\settings.json` `ActivationShortcut` and waiting for the file-watcher debounce does nothing — the centralized keyboard hook only re-registers the chord after `Restart-PtRunner`. Restart after the change AND again after restoring.
- **PinButton spawns a `PopupHost` teaching-tip.** Invoking `PinButton` pops a small confirmation flyout (≈192x63) titled `PopupHost` that surfaces *first* in `winapp ui list-windows`. A naive "first HWND" regex grabs the popup, not Peek. Match by title suffix `- Peek` (regex like `HWND (\d+): "([^"]*- Peek)"`) and/or cache the original Peek HWND before invoking PinButton.
- **Win11 Notepad tabs/session-restore** muddy the "open-with-default-app" tests (Recipes 2-3): the spawned Notepad restores prior tabs, so the foreground Notepad's title may not show your file. Enumerate all Notepad windows and match `"<file> - Notepad"` explicitly.

## Don'ts

- **Don't `Stop-Process PowerToys.Peek.UI -Force`** to close Peek between iterations — bypasses the save handler, breaks the pin-state-persistence tests (Recipes 5, 7). Use Esc / `winapp ui invoke CloseButton`.
- **Don't assume CLI back-door supports navigation** — it doesn't (`_isFromCli` guard). For nav tests use Shell COM + Ctrl+Space.
- **Don't OCR the previewer surface** when UIA already exposes the correct nodes (`ImagePreview`, `PreviewBrowser`, `LaunchAppButton`, `PinButton`). UIA is more reliable than OCR.
