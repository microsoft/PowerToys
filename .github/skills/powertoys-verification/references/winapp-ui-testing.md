# WinUI UI-testing mechanics (winapp ui)

> **Provenance:** Adapted from the `winui-ui-testing` skill in [microsoft/win-dev-skills](https://github.com/microsoft/win-dev-skills) (MIT, © Microsoft Corporation and Contributors), with PowerToys-specific edits. This is a **reference doc** for the `powertoys-verification` skill — it is intentionally not a standalone skill (no frontmatter), so it is not separately discovered.

Automated UI testing for WinUI 3 apps — generate a batch test script, run all tests in one pass, read results. Covers element assertions, interactions, value checking (TextBox, ComboBox, ToggleSwitch), file pickers, flyouts, dialogs, persistence, and accessibility audits.

### Approach

The goal of this skill is to validate UI and app functionality automatically, without manual interaction, by exercising the app's UI elements, verifying their state, and asserting that the app behaves as expected under test conditions.

There are two main approaches:
1. Interactive exploration — manually run the app, use `winapp ui <command>` to explore the UI tree, find AutomationIds, verify element properties, and test functionality interactively. This is useful for discovery, but slow and expensive if repeated for every test iteration.
2. Scripted batch testing — generate a `ui-tests.ps1` script that exercises all UI elements and asserts expected behavior in one pass. This allows you to run the tests automatically, capture results, and iterate quickly without manually interacting with the app each time.

Unless the user asked for interactive exploration, or you are unfamiliar with the code/app or need to explore the UI tree to discover AutomationIds for hidden or dynamically generated elements (flyouts, dialogs, lazy-loaded content), **prefer scripted batch testing** — it is faster, repeatable, and produces a record of pass/fail results that can be reviewed and acted on.

### `winapp ui` Verbs

`status`, `inspect`, `search`, `get-property`, `get-value`, `screenshot`, `invoke`, `click`, `set-value`, `focus`, `scroll`, `scroll-into-view`, `wait-for`, `list-windows`, `get-focused`. Run `winapp ui --cli-schema` for the complete command structure as JSON, or `winapp ui <verb> --help` for any single verb.

### Step 1: Use the Running App

If the app is already running, use its PID. **Do NOT relaunch** — use the PID already captured from the build step. If the app is not running, build and launch it using the guidance in the winui-dev-workflow skill.

### Step 2: Write the Test Script

**If you wrote the code:** Skip inspect — you already know all the AutomationIds and control structure from the XAML and code-behind. Write tests directly from that knowledge. Inspect misses popups, flyouts, dialogs, and lazy-loaded content anyway.

**If you're verifying code you didn't write:** Run inspect first to discover the UI:
```powershell
winapp ui inspect -a <PID> --interactive
```
Then read the XAML files to find AutomationIds that aren't currently visible (flyout items, dialog buttons, secondary pages).

Create a `ui-tests.ps1` file that tests all the app's requirements in one pass:

```powershell
# ui-tests.ps1
param([Parameter(Mandatory)][int]$AppPid)
# NOTE: Do NOT name the parameter $Pid — it's read-only in PowerShell

$ErrorActionPreference = 'Continue'
$pass = 0; $fail = 0; $results = @()

# Get main window HWND (avoids PopupHost interference with JSON parsing)
$windows = winapp ui list-windows -a $AppPid --json 2>$null | ConvertFrom-Json
$hwnd = ($windows | Where-Object { $_.title -ne "PopupHost" } | Select-Object -First 1).hwnd

function Test-UI {
    param([string]$Name, [scriptblock]$Script)
    # IMPORTANT: Inside $Script, use 'throw' to signal failure — NOT 'exit 1'
    # (exit terminates the entire script, not just the test)
    try {
        $output = & $Script 2>&1
        if ($LASTEXITCODE -eq 0) {
            $script:pass++; $script:results += @{ name = $Name; status = "PASS" }
        } else {
            $script:fail++; $script:results += @{ name = $Name; status = "FAIL"; detail = "$output" }
        }
    } catch {
        $script:fail++; $script:results += @{ name = $Name; status = "FAIL"; detail = "$_" }
    }
}

# ─── Element Existence ───
Test-UI "NavHome exists" { winapp ui wait-for "NavHome" -a $AppPid -t 3000 }
Test-UI "NavSettings exists" { winapp ui wait-for "NavSettings" -a $AppPid -t 3000 }

# ─── Navigation ───
Test-UI "Navigate to Settings" { winapp ui invoke "NavSettings" -a $AppPid }
Test-UI "Settings page loaded" { winapp ui wait-for "TxtUserName" -a $AppPid -t 3000 }

# ─── Interactions ───
Test-UI "Set username" { winapp ui set-value "TxtUserName" "TestUser" -a $AppPid }
Test-UI "Click Save" { winapp ui invoke "BtnSave" -a $AppPid }  # commits the TextBox binding
Test-UI "Username value set" {
    winapp ui wait-for "TxtUserName" -a $AppPid --value "TestUser" -t 2000
}

# ─── Value assertions for different control types ───
Test-UI "Theme is System default" {
    winapp ui wait-for "CmbTheme" -a $AppPid --value "System default" -t 2000
}
Test-UI "Logging is off" {
    winapp ui wait-for "TglLogging" -a $AppPid --value "Off" -t 2000
}

# ─── Accessibility Audit ───
# Only audit controls in the app's main window (exclude OS picker/popup controls)
$allElements = (winapp ui inspect -a $AppPid --interactive --json 2>$null | ConvertFrom-Json).elements
$appElements = @($allElements | Where-Object {
    $_.type -match 'Button|TextBox|ComboBox|CheckBox|ToggleSwitch|TabItem|Edit' -and
    $_.name -notmatch 'Minimize|Maximize|Close|System' -and          # window chrome
    $_.className -notmatch 'PickerHost|#32770|CabinetWClass'         # OS dialogs
})
$missingId = @($appElements | Where-Object { -not $_.automationId })
if ($missingId.Count -eq 0) {
    $pass++; $results += @{ name = "All app controls have AutomationId"; status = "PASS" }
} else {
    $fail++
    $names = ($missingId | ForEach-Object { "$($_.type) '$($_.name)'" }) -join ", "
    $results += @{ name = "AutomationId coverage"; status = "FAIL"; detail = "Missing: $names" }
}

# ─── State Screenshots (capture each meaningful state for visual review) ───
New-Item -ItemType Directory -Force -Path "screenshots" | Out-Null
winapp ui screenshot -a $AppPid -o "screenshots/01-initial.png" 2>$null
# ...take more screenshots after key interactions above (mode switches, dialogs opened, etc.)

# ─── Final Screenshot ───
winapp ui screenshot -a $AppPid -o "test-screenshot.png" 2>$null

# ─── Results ───
Write-Host "`nPassed: $pass | Failed: $fail"
$results | Where-Object { $_.status -eq "FAIL" } | ForEach-Object {
    Write-Host "  FAIL: $($_.name) — $($_.detail)" -ForegroundColor Red
}
$results | ConvertTo-Json | Out-File "test-results.json"
if ($fail -gt 0) { exit 1 } else { exit 0 }
```

### What to Test

Write tests for **every requirement** from the user's prompt:

| Requirement type | Test approach |
|---|---|
| "Has a button that does X" | `search` to verify exists, `invoke` to click, `wait-for --value` to check result |
| "Text field shows value" | `wait-for "TxtName" --value "expected"` — works for TextBox, TextBlock, labels |
| "Status bar contains text" | `wait-for "StatusBar" --value "words" --contains` — substring match for dynamic content |
| "Dropdown is set to X" | `wait-for "CmbTheme" --value "Dark"` — reads the selected item automatically |
| "Toggle is on/off" | `wait-for "TglFeature" --value "On"` — reads the toggle state |
| "Navigation between pages" | `invoke` nav item, `wait-for` a page-specific element to appear |
| "Open file dialog" | `invoke` trigger, `list-windows` to find picker HWND, interact with `-w` |
| "Save file dialog" | Same as open — find picker with `list-windows`, `set-value` filename, `invoke` Save |
| "Right-click context menu" | `click --right` on element, `invoke` the flyout MenuItem |
| "Confirmation dialog" | `invoke` trigger, `search` for dialog buttons, `invoke` Primary/Secondary/Close |
| "Data persists" | Set values, `invoke` a button (to commit bindings), verify data file on disk (`Get-Content` + `ConvertFrom-Json`) |
| "All controls accessible" | `inspect --interactive --json` + check all have AutomationId |

### Step 3: Run and Read Results

```powershell
.\ui-tests.ps1 -AppPid <PID>
```

Read `test-results.json` for structured pass/fail. Only fix code if tests fail.

### Step 3.5: Look at the Screenshots

UIA assertions don't see clipping, overlap, wrong theming, or controls bleeding past their container — UIA returns `PASS` while the app is visually broken. **Capture screenshots with `winapp ui screenshot` and view each PNG.**

Capture the initial state and any state after a major interaction (the State Screenshots block in the script template above handles this).

**Visual checklist — fail the run if any item is `no`:**
- [ ] No unintended scrollbars
- [ ] No text ending in `…` that shouldn't be
- [ ] Hero elements fully visible (not sliced)
- [ ] Right-edge controls fully visible
- [ ] No overlapping rows
- [ ] Content uses the available width — no asymmetric dead zones (e.g. content pinned to one edge leaving empty space on the other)
- [ ] Spacing intentional — not cramped, not unintentionally vast
- [ ] Theming matches the user's ask (Light/Dark/HighContrast if relevant)
- [ ] Focus/hover/error states render if tested

If the checklist fails, it's a bug — fix before declaring done. Window too small → grow per `winui-design` Step 4.

### Step 4: Fix and Rerun (if the user asked for it)

If tests fail:
1. Read the failure details from `test-results.json`
2. Batch-fix all issues in one pass
3. Rebuild with `.\BuildAndRun.ps1` (blocking mode — shows crash info if the fix broke something)
4. Rerun `.\ui-tests.ps1 -AppPid <PID>` (parse PID from the `launched (PID: XXXXX)` output)

**Maximum 2 fix-and-rerun cycles.** If the same tests keep failing after 2 cycles, report them as known issues and move on — do not keep iterating.

### Assertion Reference

Use `wait-for --value` as the primary assertion — it uses a smart fallback chain that reads the right value for any control type:

| Control type | `--value` reads from | Example |
|---|---|---|
| TextBlock / Label | Name property | `wait-for "LblTitle" --value "Home"` |
| TextBox / NumberBox | ValuePattern | `wait-for "TxtName" --value "John"` |
| RichEditBox | TextPattern | `wait-for "Editor" --value "Hello"` |
| ComboBox | Selected item (SelectionPattern) | `wait-for "CmbTheme" --value "Dark"` |
| ToggleSwitch | Toggle state (On/Off) | `wait-for "TglDark" --value "On"` |
| CheckBox | Toggle state (On/Off) | `wait-for "ChkAgree" --value "On"` |

**Full assertion commands:**

| Assertion | Command |
|---|---|
| Element exists | `winapp ui wait-for "Id" -a PID -t 3000` |
| Element has exact value | `winapp ui wait-for "Id" -a PID --value "expected" -t 3000` |
| Value contains text | `winapp ui wait-for "Id" -a PID --value "words" --contains -t 3000` |
| Element gone | `winapp ui wait-for "Id" -a PID --gone -t 3000` |
| Specific property | `winapp ui wait-for "Id" -a PID -p IsEnabled --value "True" -t 3000` |
| Button clickable | `winapp ui invoke "Id" -a PID` (exit code 0) |
| Set then verify | `winapp ui set-value "Id" "text" -a PID` then `wait-for --value` |
| Screenshot | `winapp ui screenshot -a PID -o path.png` |
| Dialog appeared | `winapp ui list-windows -a PID --json` (check window count) |
| Right-click menu | `winapp ui click "Id" -a PID --right` then `wait-for` menu item |
| Read raw property | `winapp ui get-property "Id" -a PID -p IsEnabled --json` |
| Read current value (no wait) | `(winapp ui get-value "Id" -a PID --json \| ConvertFrom-Json).text` — always pass `--json` when capturing into a variable (plain stdout can include advisory text like "Auto-selected HWND … from N windows"); otherwise prefer `wait-for --value` |
| Scroll item into view | `winapp ui scroll-into-view "Id" -a PID` — call before `wait-for` on virtualized ListView/repeater items below the fold |
| Set keyboard focus | `winapp ui focus "Id" -a PID` — cleaner than clicking another control to trigger a TextBox `LostFocus` commit |

### Selecting a ComboBox / dropdown item

*Reading* a ComboBox's current value is one step (`wait-for "Cmb" --value "Dark"`); **changing** the selection takes three, because a collapsed ComboBox's options are not in the UIA tree until it is expanded:

1. **Expand** - `winapp ui invoke <cmb-id>` (fires ExpandCollapsePattern; the dropdown opens).
2. **Inspect while open** - the options now appear as `ListItem`s in a popup with their **own ids** (e.g. `itm-<option>-<suffix>`), distinct from the combo's id. Resolve the target: `winapp ui search '<option text>' -w <hwnd> --json` and pick the match with `type -eq 'ListItem'`.
3. **Invoke that ListItem id** - `winapp ui invoke <itm-id>` (fires SelectionItemPattern). Verify with `wait-for <cmb-id> --value "<option>"`.

**Trap:** after expanding, do **not** invoke the option by its caption text - the text match lands on the child `Text` label, not the selectable `ListItem`, and silently no-ops. Always resolve and invoke the `itm-...` id, not the caption and not the collapsed combo id.

### Testing File Pickers

File/folder pickers (FileOpenPicker, FileSavePicker, FolderPicker) run in a separate `PickerHost` process but are fully interactable. The picker appears as an owned dialog window.

```powershell
# 1. Trigger the picker
winapp ui invoke "BtnOpenFile" -a $AppPid

# 2. Find the picker window (it's a dialog owned by the app window)
Start-Sleep 1
$allWindows = winapp ui list-windows -a $AppPid --json 2>$null | ConvertFrom-Json
$picker = $allWindows | Where-Object { $_.title -match "Open|Save" }
$pickerHwnd = $picker.hwnd

# 3. Interact with the picker using -w <HWND>
#    Type a filename:
winapp ui set-value "FileNameControlHost" "test.txt" -w $pickerHwnd
#    Click Open/Save:
winapp ui invoke "Open" -w $pickerHwnd     # or "Save", "Cancel"
#    Or cancel:
winapp ui invoke "Cancel" -w $pickerHwnd

# 4. Verify the app processed the file
winapp ui wait-for "StatusBar" -a $AppPid -p Name --value "opened" -t 3000
```

**Tip:** Use `winapp ui inspect -w <pickerHwnd> --interactive` to discover the picker's controls — they include the folder tree, file list, filename textbox, and Open/Cancel buttons.

### Testing Context Menus and Flyouts

MenuFlyouts and ContextFlyouts are fully testable. They appear in the UI automation tree when open.

```powershell
# 1. Right-click to open a ContextFlyout
winapp ui click "LstItems" -a $AppPid --right
Start-Sleep 0.5

# 2. The flyout MenuItems appear in the tree immediately
#    Find them with inspect or search:
winapp ui inspect -a $AppPid --interactive   # shows MnuCopy, MnuDelete, etc.

# 3. Click a flyout item
winapp ui invoke "MnuCopy" -a $AppPid

# 4. Verify the action
winapp ui wait-for "StatusText" -a $AppPid -p Name --value "Copied" -t 2000
```

**For MenuBar flyouts** (File, Edit, View menus):
```powershell
# Click the menu header to open
winapp ui invoke "FileMenu" -a $AppPid
Start-Sleep 0.5
# Click the sub-item
winapp ui invoke "MenuSaveAs" -a $AppPid
```

### Testing ContentDialogs

ContentDialogs are in-app controls (same window) — they appear directly in the UI tree when shown.

```powershell
# 1. Trigger the dialog
winapp ui invoke "BtnDelete" -a $AppPid
Start-Sleep 0.5

# 2. The dialog buttons appear in the tree
#    For a standard confirmation dialog:
winapp ui search "Primary" -a $AppPid --json   # finds the primary button
winapp ui invoke "Primary" -a $AppPid           # click "Yes"/"Delete"/"Save"
#    Or:
winapp ui invoke "Secondary" -a $AppPid         # click "No"/"Don't Save"
winapp ui invoke "Close" -a $AppPid             # click "Cancel"

# 3. Wait for dialog to dismiss
winapp ui wait-for "Primary" -a $AppPid --gone -t 3000
```

**Tip:** ContentDialog buttons often don't have custom AutomationIds — use `inspect` to find the actual selector (slug or text match).

### Key Gotchas

- **`set-value` does NOT commit default TextBox bindings** — WinUI 3 `x:Bind TwoWay` on TextBox.Text updates the ViewModel on `LostFocus` by default. UIA `set-value` changes the text but doesn't trigger focus events. **Fix:** apps should use `UpdateSourceTrigger=PropertyChanged` on TextBox bindings (see design skill). If the app doesn't, `invoke` a button or `click` another element after `set-value` to trigger `LostFocus`.
- **Verify persistence via the data file, not UI relaunch** — killing and relaunching a packaged app from a test script is fragile (MSIX registration timing, PID issues). Instead, check the data file on disk: `Get-Content $dataFile | ConvertFrom-Json` and verify expected values.
- **Use `$AppPid` not `$Pid`** — `$Pid` is a read-only automatic variable in PowerShell
- **Use `--value` without `-p`** — it auto-detects the right UIA pattern (TextPattern → ValuePattern → TogglePattern → SelectionPattern → Name). Only use `-p PropertyName --value` when you need a specific property like `IsEnabled`
- **File pickers need `-w <HWND>`** — they run in a separate PickerHost process, so `-a PID` won't find them. Use `list-windows` to discover the picker HWND first
- **Flyouts need a short `Start-Sleep`** after triggering — the menu items appear in the tree asynchronously

### CRITICAL — `invoke` vs `click`: choose the right verb

**`winapp ui invoke <sel>`** dispatches through UIA's **`InvokePattern` via COM IPC**:
- ✅ Bypasses Windows UIPI (User Interface Privilege Isolation)
- ✅ Works even when your test runs elevated and the target is non-elevated AppX
- ✅ Does NOT steal foreground / does NOT trigger focus-loss handlers
- ✅ Works on Buttons, ListItems, ToggleSwitches, CheckBoxes — anything that exposes `InvokePattern` or `TogglePattern`
- ❌ Does NOT work on elements without an UIA action pattern (plain Grid, Text, Pane) — error message says "does not support any invoke pattern"

**`winapp ui click <sel>`** uses Win32 **`SendInput`** under the hood:
- ❌ **BLOCKED by UIPI** when source is elevated and target is non-elevated (or any AppX) — error: `SendInput failed — the target window may be elevated`
- ❌ Triggers foreground change → can dismiss popups, dialogs, AppX windows that hide on deactivation
- ✅ Only use when you genuinely need a synthetic mouse click (e.g. testing mouse hover/right-click flyouts where InvokePattern is unavailable)
- ✅ Subject to your process having interactive desktop access

**Rule of thumb**: try `invoke` first; only fall back to `click` if the target lacks InvokePattern AND you have a non-elevated test runner.

### CRITICAL — DataTemplate AutomationId vs ListItem InvokePattern

When XAML binds `AutomationProperties.AutomationId="{x:Bind <DataProperty>}"` inside a `ListView.ItemTemplate`'s `<DataTemplate>`, the AutomationId lives on the **inner Grid (Group)** the template produces — NOT on the outer ListItem the ListView wraps around it. The outer ListItem is what carries `InvokePattern`.

Concrete example (CmdPal PR #48033 binds Command.Id this way):

```powershell
# This FAILS with "does not support any invoke pattern":
winapp ui invoke 'com.microsoft.cmdpal.calculator' -w $hwnd
#   Element grp-commicrosoftcmd-XXXX (Group) does not support any invoke pattern.
#   No invokable ancestor was found.

# This WORKS — find by Name (matches all 3 siblings), pick the ListItem child:
$r = winapp ui search 'Calculator' -w $hwnd --json | ConvertFrom-Json
$li = $r.matches | Where-Object type -eq 'ListItem' | Select-Object -First 1
winapp ui invoke $li.selector -w $hwnd    # selector like 'itm-calculator-7e3f'
```

If you encounter "does not support any invoke pattern" while trying to use a data-bound AutomationId, this is almost always the cause. The fix is to search by Name and invoke the sibling ListItem.

### CRITICAL — Keystroke input that bypasses UIPI (PostMessage)

`winapp ui` has no `send-keys` verb. For keystroke input into elevated/AppX targets where SendInput fails, use **inline Win32 `PostMessage WM_KEYDOWN/WM_KEYUP`** which goes through the target's message queue without UIPI checks:

```powershell
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class K {
    [DllImport("user32.dll", CharSet=CharSet.Auto)]
    public static extern bool PostMessage(IntPtr h, uint msg, IntPtr wp, IntPtr lp);
    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_KEYUP   = 0x0101;
}
"@

function Send-KeyToHwnd {
    param([IntPtr]$Hwnd, [byte]$Vk)
    [void][K]::PostMessage($Hwnd, [K]::WM_KEYDOWN, [IntPtr]$Vk, [IntPtr]0)
    Start-Sleep -Milliseconds 30
    [void][K]::PostMessage($Hwnd, [K]::WM_KEYUP,   [IntPtr]$Vk, [IntPtr]0)
}

# Common VK codes:
#   0x08 Backspace  0x09 Tab     0x0D Enter   0x1B Escape
#   0x25 Left       0x26 Up      0x27 Right   0x28 Down
Send-KeyToHwnd -Hwnd $h -Vk 0x28   # Down arrow
Send-KeyToHwnd -Hwnd $h -Vk 0x0D   # Enter
```

**Caveats**:
- WinUI3 apps' raw-input hooks may NOT process some keys via WM_KEYDOWN — `Esc` in particular often goes ignored (use BackButton invoke instead). Arrow keys + Enter typically work for ListView navigation.
- PostMessage returns immediately; allow 50-200 ms before reading state.
- Repeat `Send-KeyToHwnd` calls work for multi-step navigation (Down × 5 to scroll, then Enter).

### CRITICAL — Global hotkeys / PowerToys activation chords (SendInput, verified working)

`PostMessage` above targets a specific window's queue. To fire a **global hotkey** (e.g. a PowerToys activation chord like `Win+Shift+C`) you must inject into the **system input stream** with `SendInput` so the low-level keyboard hook (`WH_KEYBOARD_LL`) sees it. This **works for Win+ chords** — the common belief that "Win+ chords can't be injected" is false; it's almost always a **marshaling bug** (`SendInput` returns `0`, `GetLastError()==87`) from building the `INPUT[]` array in PowerShell. Build the array in C#:

```powershell
Add-Type @"
using System; using System.Runtime.InteropServices; using System.Collections.Generic;
public static class Inj {
    [StructLayout(LayoutKind.Sequential)]
    struct INPUT { public uint type; public KEYBDINPUT ki; public int p1; public int p2; }  // p1/p2 pad the union -> cb=40 on x64
    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [DllImport("user32.dll", SetLastError=true)] static extern uint SendInput(uint n, INPUT[] p, int cb);
    const uint KEYUP = 0x0002;
    static INPUT K(ushort vk, bool up){ INPUT i=new INPUT(); i.type=1; i.ki.wVk=vk; i.ki.dwFlags=up?KEYUP:0; return i; }
    public static uint Chord(ushort[] mods, ushort key){          // mods down -> key tap -> mods up (reverse)
        var l=new List<INPUT>();
        foreach(var m in mods) l.Add(K(m,false));
        l.Add(K(key,false)); l.Add(K(key,true));
        for(int i=mods.Length-1;i>=0;i--) l.Add(K(mods[i],true));
        var a=l.ToArray(); return SendInput((uint)a.Length,a,Marshal.SizeOf(typeof(INPUT)));
    }
}
"@
# LWIN=0x5B CTRL=0x11 SHIFT=0x10 ALT=0x12 ; main key VK from the module's settings.json "code"
$sent = [Inj]::Chord([uint16[]]@(0x5B,0x10), [uint16]0x43)   # Win+Shift+C (Color Picker)
if ($sent -eq 0) { throw "SendInput failed err=$([Runtime.InteropServices.Marshal]::GetLastWin32Error())" }
```

**Caveats**:
- The injector must run at the **same or higher integrity level** as the hook owner (PowerToys runner). Default per-user installs run the runner at Medium IL, so a normal shell works; if the runner is elevated, run the injector elevated too (otherwise UIPI silently drops the injection).
- Must run in the interactive desktop session.
- OS-reserved chords (Win+L, Win+Tab) are consumed by Windows before any hook and cannot be injected this way.
- Verify the result via the runner trace log line `… hotkey is invoked from Centralized keyboard hook` (`%LOCALAPPDATA%\Microsoft\PowerToys\RunnerLogs\runner-log_<date>.log`) and/or the module's observable side-effect (overlay window, spawned editor process).

### CRITICAL — Verify foreground BEFORE every SendInput targeting a specific window

`SendInput` injects into the **session-wide** input stream — it goes to whatever IS foreground at the moment. If your target window has lost foreground (very common with AppX windows), the keys silently land in another window (often your own terminal) with no error returned.

Always check the foreground state immediately before calling `SendInput`. For winapp ui's output, the literal substring `foreground` appears in the line for the foreground window:

```powershell
function Test-AppForeground {
    param([Parameter(Mandatory)][string]$AppId)
    $r = winapp ui list-windows -a $AppId 2>$null | Out-String
    return ($r -match 'foreground')
}

# Force foreground (works ONCE per session reliably; subsequent attempts may be blocked by
# Windows foreground-lock):
function Force-AppForeground {
    param([Parameter(Mandatory)][IntPtr]$Hwnd, [int]$ProcessId)
    Add-Type -TypeDefinition @'
        using System; using System.Runtime.InteropServices;
        public static class Fg {
            [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
            [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
            [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
            [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
            [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
            [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
            [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool f);
            [DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(int pid);
        }
'@ -EA SilentlyContinue
    [Fg]::AllowSetForegroundWindow($ProcessId) | Out-Null
    [Fg]::ShowWindow($Hwnd, 9) | Out-Null  # SW_RESTORE
    $fg = [Fg]::GetForegroundWindow(); $fgPid = 0
    $fgThread = [Fg]::GetWindowThreadProcessId($fg, [ref]$fgPid)
    $curThread = [Fg]::GetCurrentThreadId()
    if ($fgThread -ne 0 -and $fgThread -ne $curThread) { [Fg]::AttachThreadInput($curThread, $fgThread, $true) | Out-Null }
    [Fg]::BringWindowToTop($Hwnd) | Out-Null
    [Fg]::SetForegroundWindow($Hwnd) | Out-Null
    if ($fgThread -ne 0 -and $fgThread -ne $curThread) { [Fg]::AttachThreadInput($curThread, $fgThread, $false) | Out-Null }
    Start-Sleep -Milliseconds 400
}

# Guard pattern: abort instead of silently sending keys to wrong window
if (-not (Test-AppForeground -AppId 'Microsoft.CmdPal.UI')) {
    Force-AppForeground -Hwnd $h -ProcessId $pid
    if (-not (Test-AppForeground -AppId 'Microsoft.CmdPal.UI')) {
        throw 'Cannot force CmdPal foreground; aborting SendInput batch'
    }
}
# ... now safe to SendInput ...
```

**Tip**: when foreground cannot be reliably maintained, prefer `winapp ui set-value` (UIA-IPC, no foreground required) or `winapp ui invoke` (UIA InvokePattern, no foreground required) instead of SendInput.

### CRITICAL — `set-value` bypasses TextChanged for some apps (CmdPal alias detection)

`winapp ui set-value` writes the value through UIA's ValuePattern, which fires a programmatic value-change event. **It does NOT raise the `TextBox.TextChanged` event** the way real keystrokes do. For apps whose logic listens to `TextChanged` rather than to property changes — most notably CmdPal's alias detection (typing `=`, `<`, `>`, `:`, `$`, `??`, `)` in MainSearchBox triggers navigation to a provider sub-page) — `set-value` will set the text but the alias will NOT activate.

Workarounds:
- For plain queries: `winapp ui set-value` works fine (CmdPal still re-runs all providers on value change).
- For alias-triggered navigation: use **real keystrokes** via Force-AppForeground + SendInput, typing one character at a time with ~60-100ms delay so the alias detector sees the TextChanged sequence.
- Alternative: invoke the provider tile directly by its stable AutomationId (e.g. `winapp ui invoke 'com.microsoft.cmdpal.calculator' -w $hwnd`) when you only need the destination page, not the alias path.

### CRITICAL — Stunted UIA tree recovery

After ~30+ rapid `set-value` calls or after AppX has been interactive too long, an AppX window's UIA tree can degrade to a "stunted" state where `winapp ui inspect -w $h --depth 6` returns only ~5 elements (TitleBar / Close / Min / Max / RootPane) — even though the app looks fine visually.

Probe + recover pattern:

```powershell
# Probe: any healthy ListView-based AppX has >50 UIA nodes at depth 6
$probe = winapp ui inspect -w $h --depth 6 --json | ConvertFrom-Json
$nodes = 0
$stack = [System.Collections.Stack]::new()
if ($probe.windows[0].elements) { foreach ($e in $probe.windows[0].elements) { $stack.Push($e) } }
while ($stack.Count -gt 0) {
    $n = $stack.Pop(); $nodes++
    if ($n.PSObject.Properties['children']) { foreach ($c in $n.children) { $stack.Push($c) } }
}

if ($nodes -lt 6) {
    Write-Warning "UIA tree stunted ($nodes nodes); restarting AppX"
    Get-Process Microsoft.CmdPal.UI -EA SilentlyContinue | ForEach-Object {
        Stop-Process -Id $_.Id -Force
        Wait-Process -Id $_.Id -Timeout 5 -EA SilentlyContinue
    }
    Start-Process 'shell:AppsFolder\Microsoft.CommandPalette_8wekyb3d8bbwe!App'
    Start-Sleep 5
    # Re-resolve HWND with list-windows
}
```

### Settings.json mutation safety contract

When the only realistic way to reach a needed test state is editing the app's persistent settings (e.g. multi-select that the UI's `SelectionItemPattern.Select` clobbers), wrap mutations with **byte-identical backup + restore-on-exit**:

```powershell
$settings = "$env:LOCALAPPDATA\Packages\Microsoft.CommandPalette_8wekyb3d8bbwe\LocalState\settings.json"
$backup = "$env:TEMP\settings-backup-$(Get-Random).json"
$origBytes = [System.IO.File]::ReadAllBytes($settings)
[System.IO.File]::WriteAllBytes($backup, $origBytes)
try {
    # 1. Stop the AppX so we can write the file (apps usually hold it open)
    Get-Process Microsoft.CmdPal.UI -EA SilentlyContinue | Stop-Process -Force
    Start-Sleep 1
    # 2. Mutate
    $j = $origBytes | ForEach-Object { [char]$_ } | Join-String | ConvertFrom-Json
    $j.SomeKey = 'TestValue'
    [System.IO.File]::WriteAllBytes($settings, [System.Text.Encoding]::UTF8.GetBytes(($j | ConvertTo-Json -Depth 10)))
    # 3. Restart AppX so it re-reads the mutated settings
    Start-Process 'shell:AppsFolder\Microsoft.CommandPalette_8wekyb3d8bbwe!App'
    Start-Sleep 5
    # 4. ... run your test ...
} finally {
    # ALWAYS restore — verify byte-identical via length + SHA256
    Get-Process Microsoft.CmdPal.UI -EA SilentlyContinue | Stop-Process -Force -EA SilentlyContinue
    Start-Sleep 1
    [System.IO.File]::WriteAllBytes($settings, $origBytes)
    $check = [System.IO.File]::ReadAllBytes($settings)
    if ($check.Length -ne $origBytes.Length) { Write-Error "Restore length mismatch!" }
    Start-Process 'shell:AppsFolder\Microsoft.CommandPalette_8wekyb3d8bbwe!App'
}
```

**Important**: this should be used ONLY when the UI route is unreachable. Any setting flippable through the AppX Settings UI should be flipped that way instead (it's the documented user flow and tests real binding code).

