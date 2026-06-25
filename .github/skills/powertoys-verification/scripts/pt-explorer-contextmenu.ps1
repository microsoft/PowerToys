# pt-explorer-contextmenu.ps1 — drive any Explorer (Win11) context-menu PowerToys module
# end-to-end the way a real user does: open Explorer, select file(s), synthetic right-click
# to OPEN the menu, then UIA-invoke the module's menu item by NAME (robust — no coordinate
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
  [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
  [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool f);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
  public const uint RIGHTDOWN=0x0008, RIGHTUP=0x0010, LEFTDOWN=0x0002, LEFTUP=0x0004;
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
    # right after Restart-PtRunner / Explorer restart — without the poll that blip is misclassified
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
    for ($try = 1; $try -le $MaxTries; $try++) {
        [PtCtx]::ForceForeground([IntPtr]$ExplorerHwnd); Start-Sleep -Milliseconds 500
        $item = (winapp ui search $FileName -w $ExplorerHwnd --json 2>$null | ConvertFrom-Json).matches |
            Where-Object { $_.type -eq 'ListItem' } | Select-Object -First 1
        if (-not $item) { throw "File item '$FileName' not found in Explorer window $ExplorerHwnd" }
        # Right-click near the row's LEFT edge (on the filename), not the geometric center:
        # in Details view the ListItem rect spans ~full row width (measured 71% of window), so
        # x+width/2 lands far right over other columns / empty canvas → background menu or missed
        # click. x + min(80, width/2) is on the filename in Details AND on the tile in Icons view.
        [PtCtx]::RightClick([int]($item.x + [Math]::Min(80, $item.width/2)), [int]($item.y + $item.height/2))
        Start-Sleep -Seconds 2
        # The Win11 menu is its own top-level popup window:
        $menu = winapp ui list-windows --json 2>$null | ConvertFrom-Json |
            Where-Object { $_.className -match 'PopupWindowSiteBridge' } | Sort-Object height -Descending | Select-Object -First 1
        if ($menu) { return $menu.hwnd }
        Start-Sleep -Milliseconds 500   # retry: foreground/menu wasn't ready (common on the first attempt right after Explorer opens)
    }
    throw "Context-menu popup window not found after $MaxTries right-click attempts"
}

# Invokes a context-menu item by its visible NAME (robust — UIA InvokePattern, no coord click).
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

# Lists all context-menu item names (for discovering a module's caption or asserting absence).
function Get-PtContextMenuItems {
    param([Parameter(Mandatory)][int]$MenuHwnd)
    winapp ui inspect -w $MenuHwnd --depth 8 2>$null | Out-String |
        Select-String 'MenuItem "([^"]+)"' -AllMatches | ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value }
}
