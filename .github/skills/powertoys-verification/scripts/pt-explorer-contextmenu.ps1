# pt-explorer-contextmenu.ps1 - drive any Explorer (Win11) context-menu PowerToys module
# end-to-end the way a real user does: open Explorer, select file(s), synthetic right-click
# to OPEN the menu, then UIA-invoke the module's menu item by NAME (robust - no coordinate
# click). Used by File Locksmith, Image Resizer, PowerRename, New+, etc.
#
# See explorer-context-menu-flow.md for the full write-up, stability notes, and per-module captions.
#
# Requires an UNLOCKED interactive desktop (synthetic right-click needs foreground). Check first:
#   if ([PtFg]::GetForegroundWindow() -eq [IntPtr]::Zero) -> desktop locked -> BLK-ENV.

Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices;
public static class PtCtx {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string win);
  [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
  [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool f);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte s, uint f, IntPtr e);
  public const uint RIGHTDOWN=0x0008, RIGHTUP=0x0010, LEFTDOWN=0x0002, LEFTUP=0x0004, KEYUP=0x0002;
  // Shift+F10 = "open context menu for the current selection/focus". With NOTHING selected it opens the
  // folder-BACKGROUND menu - coordinate-free, so it can't miss a row or land in the preview pane.
  public static void ShiftF10() {
    keybd_event(0x10,0,0,IntPtr.Zero); System.Threading.Thread.Sleep(40);
    keybd_event(0x79,0,0,IntPtr.Zero); System.Threading.Thread.Sleep(60); keybd_event(0x79,0,KEYUP,IntPtr.Zero);
    System.Threading.Thread.Sleep(40); keybd_event(0x10,0,KEYUP,IntPtr.Zero);
  }
  public static void EscKey() { keybd_event(0x1B,0,0,IntPtr.Zero); System.Threading.Thread.Sleep(40); keybd_event(0x1B,0,KEYUP,IntPtr.Zero); }
  public static void ForceForeground(IntPtr h) {
    IntPtr fg = GetForegroundWindow(); uint fp;
    uint ft = GetWindowThreadProcessId(fg, out fp); uint ct = GetCurrentThreadId();
    ShowWindow(h, 9);
    if (ft != 0 && ft != ct) AttachThreadInput(ct, ft, true);
    BringWindowToTop(h); SetForegroundWindow(h);
    if (ft != 0 && ft != ct) AttachThreadInput(ct, ft, false);
  }
  public static void RightClick(int x, int y) {
    SetCursorPos(x, y); System.Threading.Thread.Sleep(250);
    mouse_event(RIGHTDOWN,0,0,0,IntPtr.Zero); System.Threading.Thread.Sleep(70); mouse_event(RIGHTUP,0,0,0,IntPtr.Zero);
  }
}
'@ -ErrorAction SilentlyContinue

function Test-PtDesktopInteractive {
    # Polls up to $TimeoutSec for a foreground window. A momentary 0 is common for a few seconds
    # right after Restart-PtRunner / Explorer restart - without the poll that blip is misclassified
    # as a locked desktop (false BLK-ENV). A genuinely locked/non-interactive desktop stays 0 for
    # the whole window and still returns $false.
    param([int]$TimeoutSec = 5)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    do {
        if ([PtCtx]::GetForegroundWindow() -ne [IntPtr]::Zero) { return $true }
        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)
    return $false
}

# Opens the Win11 context menu for a file in an already-open Explorer window and returns the
# menu popup HWND. $ExplorerHwnd = the CabinetWClass window; $FileName = item to right-click.
function Open-PtExplorerContextMenu {
    param([Parameter(Mandatory)][int]$ExplorerHwnd, [Parameter(Mandatory)][string]$FileName, [int]$MaxTries = 3)
    if (-not (Test-PtDesktopInteractive)) { throw 'BLK-ENV: desktop is locked / no foreground (GetForegroundWindow()=0). Unlock and retry.' }
    # Turn off the preview pane (best-effort) so a fallback coordinate right-click can't land in the
    # preview surface -> folder-BACKGROUND menu (rejected by the honesty guard below).
    try { Disable-PtPreviewPane -ExplorerHwnd $ExplorerHwnd | Out-Null } catch { }
    $fgEverOk = $false
    for ($try = 1; $try -le $MaxTries; $try++) {
        # Bring Explorer to the foreground AND confirm it actually stuck. Shift+F10 and the coordinate
        # fallback both need the target window to OWN the foreground + keyboard focus. If the input
        # desktop is detached (RDP minimized/disconnected, workstation locked -> GetForegroundWindow()=0)
        # or another process holds the foreground-lock, ForceForeground silently no-ops, the keystrokes
        # go nowhere, and the menu never opens. Track whether foreground ever stuck so the throw below
        # can distinguish this environment problem from a genuine wrong-menu open.
        $fgThisTry = $false
        for ($f = 0; $f -lt 3; $f++) {
            [PtCtx]::ForceForeground([IntPtr]$ExplorerHwnd); Start-Sleep -Milliseconds 400
            if ([PtCtx]::GetForegroundWindow() -eq [IntPtr]$ExplorerHwnd) { $fgThisTry = $true; $fgEverOk = $true; break }
        }
        # HARD GATE: Explorer must actually OWN the foreground before we synthesize input. If it never
        # stuck (locked/secure desktop, or another window holds the foreground-lock), the Shift+F10 /
        # coordinate-click below would go nowhere - so skip them and retry rather than waste keystrokes.
        # If it never sticks across all $MaxTries, $fgEverOk stays $false and we throw BLK-ENV (below).
        if (-not $fgThisTry) { Start-Sleep -Milliseconds 500; continue }
        # Select the target via Shell COM first (reliable, coordinate-free, scrolls it into view) so the
        # right-click has a guaranteed-visible selected row - never opens the file / never renames it.
        try {
            $sh = New-Object -ComObject Shell.Application
            $w = $sh.Windows() | Where-Object { $_.HWND -eq $ExplorerHwnd } | Select-Object -First 1
            if ($w -and $w.Document) {
                $it = $w.Document.Folder.Items() | Where-Object { $_.Name -eq $FileName -or $_.Name -like "$FileName*" } | Select-Object -First 1
                if ($it) { $w.Document.SelectItem($it, 0x2D); Start-Sleep -Milliseconds 300 }
            }
        } catch { }
        $item = (winapp ui search $FileName -w $ExplorerHwnd --json 2>$null | ConvertFrom-Json).matches |
            Where-Object { $_.type -eq 'ListItem' } | Select-Object -First 1
        if (-not $item) { Start-Sleep -Milliseconds 500; continue }   # transient (mid-populate) - retry
        # OPEN the menu COORDINATE-FREE (preferred): Shift+F10 acts on the COM-selected item - no pixel
        # involved, so a preview pane / DPI / wide Details row / screen-edge row can't misplace the click
        # (measured 10/10 on fresh windows). First restore KEYBOARD focus to the list item so Shift+F10
        # targets the file and not the nav tree / address bar (verified: focus-in-tree -> wrong menu).
        winapp ui focus $item.selector -w $ExplorerHwnd 2>$null | Out-Null; Start-Sleep -Milliseconds 200
        [PtCtx]::ShiftF10()
        # Poll for the Shift+F10 menu (the PopupHost can take >1.2s to render under RDP / slow foreground)
        # before degrading to the fragile coordinate fallback below. ~3.6s budget (12 x 300ms).
        $menu = $null
        for ($poll = 0; $poll -lt 12 -and -not $menu; $poll++) {
            Start-Sleep -Milliseconds 300
            $menu = winapp ui list-windows --json 2>$null | ConvertFrom-Json |
                Where-Object { $_.className -match 'PopupWindowSiteBridge' } | Sort-Object height -Descending | Select-Object -First 1
        }
        if (-not $menu) {   # Shift+F10 produced nothing - fall back to the coordinate right-click by selector
            winapp ui click --right $item.selector -w $ExplorerHwnd 2>$null | Out-Null
            Start-Sleep -Seconds 2
            $menu = winapp ui list-windows --json 2>$null | ConvertFrom-Json |
                Where-Object { $_.className -match 'PopupWindowSiteBridge' } | Sort-Object height -Descending | Select-Object -First 1
        }
        if (-not $menu) {   # still nothing - last-resort coordinate right-click on the row point
            [PtCtx]::RightClick([int]($item.x + [Math]::Min(80, $item.width/2)), [int]($item.y + $item.height/2))
            Start-Sleep -Seconds 2
            $menu = winapp ui list-windows --json 2>$null | ConvertFrom-Json |
                Where-Object { $_.className -match 'PopupWindowSiteBridge' } | Sort-Object height -Descending | Select-Object -First 1
        }
        if ($menu) {
            # HONESTY GUARD: confirm we actually opened the FILE item's menu, not the folder background
            # (winapp's click point can land in the preview pane / empty canvas -> background menu). A file
            # menu has Open/Cut/Copy/Delete; the background menu has View/Sort by/Group by. Never return
            # the background menu here and pretend it's the file's - retry, then fail loudly.
            $names = Get-PtContextMenuItems -MenuHwnd $menu.hwnd
            if ($names -match '^(Open|Cut|Copy|Delete|Rename)$') { return $menu.hwnd }
            [PtCtx]::EscKey(); Start-Sleep -Milliseconds 400   # wrong (background) menu - close and retry
        }
        Start-Sleep -Milliseconds 500
    }
    if (-not $fgEverOk) {
        throw "BLK-ENV: could not bring Explorer (HWND $ExplorerHwnd) to the foreground after $MaxTries attempts - GetForegroundWindow() never matched it. This is a detached/locked input desktop (RDP minimized or disconnected, workstation locked, screensaver) or another window holds the foreground-lock; Shift+F10 keystrokes are dropped in this state. Restore an attached interactive desktop and retry. See references/environment-setup.md and SKILL.md pitfall #7."
    }
    throw "Could not open the FILE context menu for '$FileName' after $MaxTries attempts (got background/none) even though Explorer had the foreground. If the module's entry appears on the folder menu (PowerRename, New+), use Open-PtBackgroundContextMenu instead."
}

# Opens the folder-BACKGROUND context menu (no file needed) via Shift+F10 with nothing selected -
# coordinate-free, so it can't miss a row or land in the preview pane. Use this for modules whose entry
# appears on the folder menu (PowerRename, New+). NOT valid for File Locksmith / Image Resizer, whose
# entries only appear on a selected file (Image Resizer: an image) - use Open-PtExplorerContextMenu +
# Select-PtExplorerFiles for those.
function Open-PtBackgroundContextMenu {
    param([Parameter(Mandatory)][int]$ExplorerHwnd, [int]$MaxTries = 3)
    if (-not (Test-PtDesktopInteractive)) { throw 'BLK-ENV: desktop is locked / no foreground (GetForegroundWindow()=0). Unlock and retry.' }
    for ($try = 1; $try -le $MaxTries; $try++) {
        [PtCtx]::ForceForeground([IntPtr]$ExplorerHwnd); Start-Sleep -Milliseconds 400
        [PtCtx]::EscKey(); Start-Sleep -Milliseconds 200   # clear any selection so Shift+F10 targets the folder background
        # Put KEYBOARD focus in the FILE-LIST (not the nav tree / address bar) so Shift+F10 opens the
        # folder-background menu, not a tree node's menu (verified: focus-in-tree -> wrong menu, module entry
        # absent). Do this LAST before Shift+F10. Coordinate-free via UIA SetFocus.
        $listSel = (winapp ui search 'Items View' -w $ExplorerHwnd --json 2>$null | ConvertFrom-Json).matches |
            Where-Object { $_.type -eq 'List' } | Select-Object -First 1 -ExpandProperty selector
        if ($listSel) { winapp ui focus $listSel -w $ExplorerHwnd 2>$null | Out-Null; Start-Sleep -Milliseconds 200 }
        [PtCtx]::ShiftF10()
        # Poll for the background menu (PopupHost can take >2s to render under RDP / slow foreground). ~3.6s budget.
        $menu = $null
        for ($poll = 0; $poll -lt 12 -and -not $menu; $poll++) {
            Start-Sleep -Milliseconds 300
            $menu = winapp ui list-windows --json 2>$null | ConvertFrom-Json |
                Where-Object { $_.className -match 'PopupWindowSiteBridge' } | Sort-Object height -Descending | Select-Object -First 1
        }
        if ($menu) {
            # Confirm it's the BACKGROUND menu (View/Sort by/Group by), not an item menu.
            $names = Get-PtContextMenuItems -MenuHwnd $menu.hwnd
            if ($names -match '^(View|Sort by|Group by)$') { return $menu.hwnd }
            [PtCtx]::EscKey(); Start-Sleep -Milliseconds 400
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Could not open the folder BACKGROUND context menu after $MaxTries attempts."
}

# Invokes a context-menu item by its visible NAME (robust - UIA InvokePattern, no coord click).
# Returns $true if invoked. Match the module caption, e.g.:
#   File Locksmith -> 'Unlock with File Locksmith'   PowerRename -> 'Rename with PowerRename'
#   Image Resizer  -> 'Resize images' (verify by enumerating)   New+ -> 'New+'
function Invoke-PtContextMenuItem {
    param([Parameter(Mandatory)][int]$MenuHwnd, [Parameter(Mandatory)][string]$ItemName)
    $m = (winapp ui search $ItemName -w $MenuHwnd --json 2>$null | ConvertFrom-Json).matches |
        Where-Object { $_.type -eq 'MenuItem' } | Select-Object -First 1
    if (-not $m) { return $false }   # caller can treat $false as "entry absent" (e.g. module disabled)
    winapp ui invoke $m.selector -w $MenuHwnd 2>$null | Out-Null
    return $true
}

# Lists all context-menu item names - works on ANY menu HWND: the modern Win11 menu (from
# Open-PtBackgroundContextMenu / Open-PtExplorerContextMenu) OR the legacy #32768 menu (from
# Open-PtShowMoreOptionsMenu). Use for present/absent assertions and to discover a module's caption.
function Get-PtContextMenuItems {
    param([Parameter(Mandatory)][int]$MenuHwnd)
    winapp ui inspect -w $MenuHwnd --depth 8 2>$null | Out-String |
        Select-String 'MenuItem "([^"]+)"' -AllMatches | ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value }
}

# Open an Explorer window on $Path and return its CabinetWClass HWND (int) - the handle the context-menu
# openers need. Polls until the window appears (or throws). Use at the start of a synthetic-menu flow.
# Detect whether the Explorer preview (reading) pane is currently shown. Heuristic: when the preview
# pane is open it occupies the right portion of the window, so the "Shell Folder View" list pane's
# right edge falls well short of the window's right edge (>200px gap). Returns $true/$false; best-effort
# $false on any error. Uses UIA only (no foreground / keystrokes needed).
function Get-PtPreviewPaneOn {
    param([Parameter(Mandatory)][int]$ExplorerHwnd)
    try {
        $win = winapp ui list-windows --json 2>$null | ConvertFrom-Json |
            Where-Object { $_.hwnd -eq $ExplorerHwnd } | Select-Object -First 1
        if (-not $win) { return $false }
        $line = (winapp ui inspect -w $ExplorerHwnd --depth 10 2>$null | Out-String) -split "`r?`n" |
            Where-Object { $_ -match 'Shell Folder View' } | Select-Object -First 1
        if ($line -match '\((\d+),(\d+)\s+(\d+)x(\d+)\)') {
            $right = [int]$matches[1] + [int]$matches[3]
            return (($win.width - $right) -gt 200)
        }
    } catch { }
    return $false
}

# Turn the Explorer preview (reading) pane OFF if it's on. An open preview pane is the top cause of the
# coordinate right-click FALLBACK (in Open-PtExplorerContextMenu, used when Shift+F10 doesn't produce a
# menu) landing in the preview surface -> the folder-BACKGROUND menu, which the honesty guard rejects
# (looks like "got background/none"). Toggles via the command-bar "PreviewPaneToggleButton" using UIA
# InvokePattern (no foreground needed, unlike the Alt+P keystroke). Best-effort: never throws, so it
# can't break the menu flow. Returns $true if the pane ends up OFF. Note: on a very narrow window the
# button can collapse into the "..." More menu and the invoke may miss - the width heuristic then simply
# reports the unchanged state and the caller proceeds (Shift+F10 is unaffected either way).
function Disable-PtPreviewPane {
    param([Parameter(Mandatory)][int]$ExplorerHwnd)
    for ($i = 0; $i -lt 2; $i++) {
        if (-not (Get-PtPreviewPaneOn -ExplorerHwnd $ExplorerHwnd)) { return $true }
        winapp ui invoke PreviewPaneToggleButton -w $ExplorerHwnd 2>$null | Out-Null
        Start-Sleep -Milliseconds 900
    }
    return (-not (Get-PtPreviewPaneOn -ExplorerHwnd $ExplorerHwnd))
}

function Open-PtExplorerWindow {
    param([Parameter(Mandatory)][string]$Path, [int]$TimeoutSec = 10)
    if (-not (Test-Path $Path)) { throw "Path not found: $Path" }
    Start-Process explorer.exe $Path
    $leaf = Split-Path $Path -Leaf
    $h = $null; $deadline = (Get-Date).AddSeconds($TimeoutSec)
    do {
        Start-Sleep -Milliseconds 800
        $h = (winapp ui list-windows --json 2>$null | ConvertFrom-Json |
            Where-Object { $_.className -eq 'CabinetWClass' -and $_.title -match [regex]::Escape($leaf) } |
            Select-Object -First 1).hwnd
    } while (-not $h -and (Get-Date) -lt $deadline)
    if (-not $h) { throw "Explorer window for '$Path' did not appear within $TimeoutSec s." }
    # Turn off the preview pane so the coordinate right-click fallback in Open-PtExplorerContextMenu
    # can't land in the preview surface (-> folder-background menu). Best-effort; ignore failures.
    try { Disable-PtPreviewPane -ExplorerHwnd ([int]$h) | Out-Null } catch { }
    [int]$h
}

# From an already-open MODERN menu (HWND from Open-PtBackgroundContextMenu / Open-PtExplorerContextMenu),
# expand "Show more options" and return the legacy classic #32768 menu's HWND (int). The #32768 window is
# NOT enumerable via `winapp ui list-windows`, so its handle is resolved with Win32 FindWindow; the returned
# HWND is a normal menu HWND - read it with Get-PtContextMenuItems / act with Invoke-PtContextMenuItem, exactly
# like the modern menu. See explorer-context-menu-flow.md for the writeup.
function Open-PtShowMoreOptionsMenu {
    param([Parameter(Mandatory)][int]$MenuHwnd, [int]$TimeoutSec = 6)
    if (-not (Invoke-PtContextMenuItem -MenuHwnd $MenuHwnd -ItemName 'Show more options')) {
        throw "No 'Show more options' entry on the modern menu (HWND $MenuHwnd) - is this a Win11 packaged menu?"
    }
    $classic = [IntPtr]::Zero; $deadline = (Get-Date).AddSeconds($TimeoutSec)
    do { Start-Sleep -Milliseconds 500; $classic = [PtCtx]::FindWindow('#32768', $null) }
    while ($classic -eq [IntPtr]::Zero -and (Get-Date) -lt $deadline)
    if ($classic -eq [IntPtr]::Zero) { throw 'Classic #32768 menu did not appear after invoking "Show more options".' }
    [int64]$classic
}

# From an already-open MODERN menu (HWND from Open-PtBackgroundContextMenu / Open-PtExplorerContextMenu),
# expand a submenu-parent item (e.g. New+'s "New+") and return the child submenu's HWND (int). This is the
# MODERN-menu sibling of Open-PtShowMoreOptionsMenu: same "invoke a parent item -> wait -> return the child
# menu's HWND" shape, but the child is a WinUI PopupWindowSiteBridge popup (NOT the legacy #32768 window),
# so it's resolved by enumerating windows rather than FindWindow('#32768'). It reuses Invoke-PtContextMenuItem
# for the click and Get-PtContextMenuItems to disambiguate the popup; the returned HWND is a normal menu HWND
# you read/act on with Get-PtContextMenuItems / Invoke-PtContextMenuItem, exactly like the parent menu.
#   -MenuHwnd    the parent modern menu's HWND
#   -ItemName    the submenu-parent MenuItem to invoke (e.g. 'New+')
#   -ContainsItem an item name known to live in the CHILD submenu (e.g. New+'s always-present 'Open templates'),
#                used to pick the right popup when several PopupWindowSiteBridge windows are open (parent + child).
# Throws if the parent item is absent, or if no child popup containing -ContainsItem appears within TimeoutSec.
function Expand-PtModernSubmenu {
    param(
        [Parameter(Mandatory)][int]$MenuHwnd,
        [Parameter(Mandatory)][string]$ItemName,
        [string]$ContainsItem,
        [int]$TimeoutSec = 6
    )
    # Record the popups already open so we can distinguish the NEW child popup from the parent menu.
    $before = @(winapp ui list-windows --json 2>$null | ConvertFrom-Json |
        Where-Object { $_.className -match 'PopupWindowSiteBridge' } | ForEach-Object { [int]$_.hwnd })
    # Poll for the parent item before invoking: Win11 menus populate asynchronously and 3rd-party
    # packaged entries (New+, PowerRename, ...) land a beat AFTER the base verbs, so a too-early invoke
    # on a freshly-opened menu can miss the item (a transient race, esp. right after enabling the module).
    $invoked = $false; $itemDeadline = (Get-Date).AddSeconds([Math]::Max(2, [Math]::Floor($TimeoutSec / 2)))
    do {
        if (Invoke-PtContextMenuItem -MenuHwnd $MenuHwnd -ItemName $ItemName) { $invoked = $true; break }
        Start-Sleep -Milliseconds 400
    } while ((Get-Date) -lt $itemDeadline)
    if (-not $invoked) {
        throw "No '$ItemName' entry on the modern menu (HWND $MenuHwnd) to expand (still absent after waiting for late-populating packaged entries)."
    }
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    do {
        Start-Sleep -Milliseconds 300
        $popups = winapp ui list-windows --json 2>$null | ConvertFrom-Json |
            Where-Object { $_.className -match 'PopupWindowSiteBridge' }
        # Prefer a popup that actually contains the known child item (unambiguous). Otherwise, take a
        # newly-appeared popup (one not open before the invoke) as the child submenu.
        if ($ContainsItem) {
            foreach ($p in $popups) { if ((Get-PtContextMenuItems -MenuHwnd $p.hwnd) -contains $ContainsItem) { return [int]$p.hwnd } }
        } else {
            $new = $popups | Where-Object { [int]$_.hwnd -notin $before } | Sort-Object height -Descending | Select-Object -First 1
            if ($new) { return [int]$new.hwnd }
        }
    } while ((Get-Date) -lt $deadline)
    throw "Submenu for '$ItemName' did not appear within $TimeoutSec s (looked for a PopupWindowSiteBridge$(if($ContainsItem){" containing '$ContainsItem'"}))."
}

# Open the EXTENDED (CMF_EXTENDEDVERBS) legacy #32768 menu for a file via a GENUINE Shift+right-click, and
# return its HWND (int). Use this - NOT Open-PtShowMoreOptionsMenu - whenever the entry under test is registered
# "extended-verbs only" (e.g. PowerRename "Extended context menu only"): the non-Shift "Show more options" path
# does NOT pass CMF_EXTENDEDVERBS, so such entries are (correctly) absent there and only appear on Shift+right-click
# (src PowerRenameExt.cpp:84 returns E_FAIL unless CMF_EXTENDEDVERBS). Mechanics: COM-select the file (verifiable),
# hold VK_SHIFT around winapp's element-resolved right-click, release Shift, then FindWindow('#32768'). Retries
# because the first attempt right after Explorer opens can miss (foreground not settled). Read/act on the returned
# HWND with Get-PtContextMenuItems / Invoke-PtContextMenuItem like any other menu.
function Open-PtShiftRightClickMenu {
    param([Parameter(Mandatory)][int]$ExplorerHwnd, [Parameter(Mandatory)][string]$FileName, [int]$MaxTries = 3)
    if (-not (Test-PtDesktopInteractive)) { throw 'BLK-ENV: desktop is locked / no foreground (GetForegroundWindow()=0). Unlock and retry.' }
    for ($try = 1; $try -le $MaxTries; $try++) {
        [PtCtx]::ForceForeground([IntPtr]$ExplorerHwnd); Start-Sleep -Milliseconds 500
        # COM-select the target first (deterministic, verifiable, scrolls it into view; never opens/renames it).
        try {
            $sh = New-Object -ComObject Shell.Application
            $w = $sh.Windows() | Where-Object { $_.HWND -eq $ExplorerHwnd } | Select-Object -First 1
            if ($w -and $w.Document) {
                $it = $w.Document.Folder.Items() | Where-Object { $_.Name -eq $FileName -or $_.Name -like "$FileName*" } | Select-Object -First 1
                if ($it) { $w.Document.SelectItem($it, 0x1D); Start-Sleep -Milliseconds 300 }
            }
        } catch { }
        $item = (winapp ui search $FileName -w $ExplorerHwnd --json 2>$null | ConvertFrom-Json).matches |
            Where-Object { $_.type -eq 'ListItem' } | Select-Object -First 1
        if (-not $item) { Start-Sleep -Milliseconds 500; continue }   # transient (mid-populate) - retry
        # Hold Shift around winapp's element-resolved right-click (Shift => CMF_EXTENDEDVERBS). Always release Shift.
        try {
            [PtCtx]::keybd_event(0x10, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 60          # SHIFT down
            winapp ui click --right $item.selector -w $ExplorerHwnd 2>$null | Out-Null
            Start-Sleep -Milliseconds 80
        } finally { [PtCtx]::keybd_event(0x10, 0, 0x2, [IntPtr]::Zero) }                            # SHIFT up
        Start-Sleep -Milliseconds 800
        # Shift+right-click on Win11 opens the classic #32768 DIRECTLY (bypasses the modern PopupWindowSiteBridge).
        $classic = [int64][PtCtx]::FindWindow('#32768', $null)
        if ($classic -ne 0) { return [int]$classic }
        Start-Sleep -Milliseconds 500
    }
    throw "Could not open the extended (Shift+right-click) #32768 menu for '$FileName' after $MaxTries attempts."
}

# Enable/disable a whole PowerToys MODULE through the real Settings-UI toggle switch - the faithful
# user flow for "check enable/disable of the module works" items, and the sanctioned way to do it.
# The UI toggle exercises the Settings->runner IPC enable/disable path and takes effect on the live
# context menu WITHOUT a runner restart; for New+ it also runs the enable-time CopyTemplateExamples
# that seeds templates. If the desktop is locked so this can't be driven, mark the item BLK-ENV.
# Verified for PowerRename / File Locksmith / Image Resizer / New+ (2026-07-01). Always restore the
# original state in a finally{} (call again with the old value).
#
#   $r = Set-PtModuleEnabledViaSettingsUI -PageTag PowerRename -Enabled $false -EnabledKey PowerRename
#   # ...assert the context-menu entry is gone...
#   Set-PtModuleEnabledViaSettingsUI -PageTag PowerRename -Enabled $true  -EnabledKey PowerRename   # restore
#
# -PageTag    : the `--open-settings=<tag>` moniker / page id. Known: PowerRename, FileLocksmith,
#               ImageResizer, NewPlus (no spaces).
# -Enabled    : desired state ($true = on, $false = off). No-ops if already in that state.
# -EnabledKey : (optional) master settings.json `enabled.<key>` to cross-check. Note the key uses the
#               DISPLAY name with spaces: 'PowerRename','File Locksmith','Image Resizer','NewPlus'.
# Returns: [pscustomobject] @{ SettingsHwnd; Selector; State('on'|'off'); EnabledFlag; Matched }.
# NOTE: leaves the Settings window OPEN (caller closes it, e.g. `winapp ui invoke btn-close-<id> -w <hwnd>`).
function Set-PtModuleEnabledViaSettingsUI {
    param(
        [Parameter(Mandatory)][string]$PageTag,
        [Parameter(Mandatory)][bool]$Enabled,
        [string]$EnabledKey,
        [int]$TimeoutSec = 20
    )
    if (-not (Test-PtDesktopInteractive)) { throw 'BLK-ENV: desktop locked / no foreground; cannot drive the Settings UI toggle.' }

    # 1) Open Settings straight on the module page (the runner honours --open-settings=<tag>; single-instance,
    #    so if Settings is already open it just navigates+focuses that window).
    Start-Process "$env:LOCALAPPDATA\PowerToys\PowerToys.exe" -ArgumentList "--open-settings=$PageTag" -EA SilentlyContinue
    $h = $null; $deadline = (Get-Date).AddSeconds($TimeoutSec)
    do {
        Start-Sleep 1
        $w = winapp ui list-windows --json 2>$null | ConvertFrom-Json |
            Where-Object { $_.title -match 'PowerToys Settings' } | Select-Object -First 1
        if ($w) { $h = [int]$w.hwnd }
    } while (-not $h -and (Get-Date) -lt $deadline)
    if (-not $h) { throw "Could not find the PowerToys Settings window after --open-settings=$PageTag." }
    Start-Sleep 2   # let the page render

    # 2) The module's master enable toggle is ALWAYS the top-most ToggleSwitch on its page, i.e. the FIRST
    #    element whose UIA state renders as [on]/[off]. AutomationIds carry a per-session suffix
    #    (btn-powerrename-XXXX), so discover by state, never by a hard-coded id.
    $sel = $null; $state = $null; $deadline = (Get-Date).AddSeconds($TimeoutSec)
    do {
        $dump = (winapp ui inspect -w $h --depth 12 2>$null | Out-String) -split "`r?`n"
        $line = $dump | Where-Object { $_ -match '\[(on|off)\]' } | Select-Object -First 1
        if ($line -and $line -match '^\s*(\S+)\b.*\[(on|off)\]') { $sel = $matches[1]; $state = $matches[2]; break }
        Start-Sleep 1
    } while ((Get-Date) -lt $deadline)
    if (-not $sel) { throw "Could not locate the enable ToggleSwitch on the '$PageTag' Settings page." }

    # 3) Flip only if needed
    $want = if ($Enabled) { 'on' } else { 'off' }
    if ($state -ne $want) {
        winapp ui invoke $sel -w $h 2>$null | Out-Null
        Start-Sleep 3
        $dump = (winapp ui inspect -w $h --depth 12 2>$null | Out-String) -split "`r?`n"
        $line = $dump | Where-Object { $_ -match [regex]::Escape($sel) -and $_ -match '\[(on|off)\]' } | Select-Object -First 1
        if ($line -and $line -match '\[(on|off)\]') { $state = $matches[1] }
    }

    # 4) Optional cross-check against the master settings.json enabled flag (the UI writes it for you)
    $flag = $null
    if ($EnabledKey) {
        Start-Sleep 1
        try { $flag = (Get-Content "$env:LOCALAPPDATA\Microsoft\PowerToys\settings.json" -Raw | ConvertFrom-Json).enabled.$EnabledKey } catch {}
    }
    [pscustomobject]@{ SettingsHwnd = $h; Selector = $sel; State = $state; EnabledFlag = $flag; Matched = ($state -eq $want) }
}
