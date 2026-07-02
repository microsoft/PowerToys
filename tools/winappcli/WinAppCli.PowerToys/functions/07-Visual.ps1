# 07-Visual.ps1 — 🔵 deterministic visual checks via Win32 + GDI.
#
# Three sub-paths from plan §15:
#   Win32   — Test-WindowTopmost, Get-WindowExStyle, Get-WindowParent
#   Pixel   — Get-PixelAt, Get-PixelRowSample (sample N pixels along a window edge)
#   Hash    — Test-RegionContent (NOT-empty / minimum-distinctness check)

if (-not ('WinAppCli.PtVisual' -as [type])) {
    Add-Type -TypeDefinition @"
        using System;
        using System.Drawing;
        using System.Runtime.InteropServices;
        namespace WinAppCli {
            [StructLayout(LayoutKind.Sequential)]
            public struct PtRect { public int Left, Top, Right, Bottom; }
            public static class PtVisual {
                [DllImport("user32.dll")]
                public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
                [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
                public static extern long GetWindowLongPtr(IntPtr hWnd, int nIndex);
                [DllImport("user32.dll")]
                public static extern IntPtr GetParent(IntPtr hWnd);
                [DllImport("user32.dll")]
                [return: MarshalAs(UnmanagedType.Bool)]
                public static extern bool GetWindowRect(IntPtr hWnd, out PtRect lpRect);
                [DllImport("user32.dll")]
                [return: MarshalAs(UnmanagedType.Bool)]
                public static extern bool IsWindow(IntPtr hWnd);

                // Foreground / focus management
                [DllImport("user32.dll")]
                public static extern IntPtr GetForegroundWindow();
                [DllImport("user32.dll")]
                [return: MarshalAs(UnmanagedType.Bool)]
                public static extern bool SetForegroundWindow(IntPtr hWnd);
                [DllImport("user32.dll")]
                [return: MarshalAs(UnmanagedType.Bool)]
                public static extern bool AllowSetForegroundWindow(int dwProcessId);
                [DllImport("user32.dll")]
                [return: MarshalAs(UnmanagedType.Bool)]
                public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);
                [DllImport("user32.dll")]
                public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
                [DllImport("kernel32.dll")]
                public static extern uint GetCurrentThreadId();
                [DllImport("user32.dll")]
                [return: MarshalAs(UnmanagedType.Bool)]
                public static extern bool BringWindowToTop(IntPtr hWnd);
                [DllImport("user32.dll")]
                [return: MarshalAs(UnmanagedType.Bool)]
                public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

                public const int GWL_EXSTYLE = -20;
                public const long WS_EX_TOPMOST = 0x00000008L;
                public const int SW_RESTORE = 9;

                /// <summary>
                /// Force a window into the foreground, defeating Win11 foreground-stealing
                /// prevention by temporarily attaching this thread's input queue to the
                /// target window's thread.  Returns true if the window is foreground after.
                /// </summary>
                public static bool ForceForeground(IntPtr hWnd) {
                    if (hWnd == IntPtr.Zero || !IsWindow(hWnd)) return false;
                    if (GetForegroundWindow() == hWnd) return true;
                    uint targetPid;
                    uint targetTid = GetWindowThreadProcessId(hWnd, out targetPid);
                    uint thisTid   = GetCurrentThreadId();
                    bool attached = false;
                    try {
                        if (targetTid != 0 && targetTid != thisTid) {
                            attached = AttachThreadInput(thisTid, targetTid, true);
                        }
                        ShowWindow(hWnd, SW_RESTORE);
                        BringWindowToTop(hWnd);
                        SetForegroundWindow(hWnd);
                    } finally {
                        if (attached) {
                            AttachThreadInput(thisTid, targetTid, false);
                        }
                    }
                    System.Threading.Thread.Sleep(60);
                    return GetForegroundWindow() == hWnd;
                }
            }
        }
"@ -ReferencedAssemblies System.Drawing, System.Threading.Thread
}

function Get-WindowExStyle {
    <#
    .SYNOPSIS
    Returns the WS_EX_* extended-style bits of a window as an Int64.
    .PARAMETER Hwnd
    Window handle (Int).
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][int]$Hwnd)
    $h = [IntPtr]$Hwnd
    if (-not [WinAppCli.PtVisual]::IsWindow($h)) { throw "HWND $Hwnd is not a window" }
    if ([IntPtr]::Size -eq 8) {
        return [WinAppCli.PtVisual]::GetWindowLongPtr($h, [WinAppCli.PtVisual]::GWL_EXSTYLE)
    } else {
        return [int64][WinAppCli.PtVisual]::GetWindowLong($h, [WinAppCli.PtVisual]::GWL_EXSTYLE)
    }
}

function Test-WindowTopmost {
    <#
    .SYNOPSIS
    Returns $true if the window has WS_EX_TOPMOST set (i.e. PowerToys' Always
    on Top has pinned it, or the user/app marked it always-on-top).
    .PARAMETER Hwnd
    Window handle.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][int]$Hwnd)
    return ((Get-WindowExStyle -Hwnd $Hwnd) -band [WinAppCli.PtVisual]::WS_EX_TOPMOST) -ne 0
}

function Get-WindowParent {
    <#
    .SYNOPSIS
    Returns the HWND of the parent window (or 0 if top-level). Used to verify
    Crop & Lock's Reparent mode actually re-parented a window.
    .PARAMETER Hwnd
    Window handle.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][int]$Hwnd)
    $p = [WinAppCli.PtVisual]::GetParent([IntPtr]$Hwnd)
    return [int64]$p
}

function Get-WindowRect {
    <#
    .SYNOPSIS
    Returns @{ Left, Top, Right, Bottom, Width, Height } for the window's
    screen-coordinate bounds.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][int]$Hwnd)
    $r = New-Object 'WinAppCli.PtRect'
    if (-not [WinAppCli.PtVisual]::GetWindowRect([IntPtr]$Hwnd, [ref]$r)) {
        throw "GetWindowRect failed for HWND $Hwnd"
    }
    return [pscustomobject]@{ Left=$r.Left; Top=$r.Top; Right=$r.Right; Bottom=$r.Bottom; Width=($r.Right-$r.Left); Height=($r.Bottom-$r.Top) }
}

# Add a foreground-state probe to the helper module — useful for debugging
function Get-ForegroundHwnd {
    <#
    .SYNOPSIS
    Returns the HWND of the currently foreground window. Useful for verifying
    whether Set-WindowForeground actually stuck before SendInput fires.
    #>
    [CmdletBinding()]
    param()
    return [int64][WinAppCli.PtVisual]::GetForegroundWindow()
}

function Set-WindowForeground {
    <#
    .SYNOPSIS
    Force a window to the foreground, defeating Win11's foreground-stealing
    prevention via the AttachThreadInput + SetForegroundWindow trick. Returns
    $true if the window is now foreground.
    .DESCRIPTION
    Modern Windows blocks programs from stealing focus unless they recently
    received user input. The reliable bypass:
      1. Get the target window's thread id
      2. AttachThreadInput(this thread, target thread, true)
      3. ShowWindow(SW_RESTORE) + BringWindowToTop + SetForegroundWindow
      4. Detach input queues
    Use BEFORE Send-PtHotkey to ensure the keystrokes land in the right window.
    .PARAMETER Hwnd
    Window to focus.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][int]$Hwnd)
    return [WinAppCli.PtVisual]::ForceForeground([IntPtr]$Hwnd)
}

function Hide-Window {
    <#
    .SYNOPSIS
    Hide a window via Win32 ShowWindow(SW_HIDE). The window object stays
    alive (process not killed) so the app can resummon it later. Useful for
    suite-end cleanup of toggleable HUDs like CmdPal that would otherwise
    linger on screen between test runs.

    .DESCRIPTION
    SW_HIDE differs from minimize: the window becomes IsWindowVisible=false
    and disappears from the alt-tab list, but the process and its windows
    stay alive. Compare to PostMessage(WM_CLOSE) which terminates the app.

    .PARAMETER Hwnd
    Window to hide.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][int64]$Hwnd)
    # Reuse the PtWindowState ShowWindow PInvoke compiled by Open-PtSettings.
    # If the class hasn't been loaded yet (e.g. caller never used Open-PtSettings),
    # compile a minimal copy here.
    if (-not ('WinAppCli.PtWindowState' -as [type])) {
        Add-Type -TypeDefinition @"
            using System;
            using System.Runtime.InteropServices;
            namespace WinAppCli {
                public static class PtWindowState {
                    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
                    [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr hWnd);
                    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
                    public const int SW_HIDE      = 0;
                    public const int SW_SHOWNORMAL = 1;
                    public const int SW_MAXIMIZE  = 3;
                }
            }
"@
    }
    return [WinAppCli.PtWindowState]::ShowWindow([IntPtr]$Hwnd, [WinAppCli.PtWindowState]::SW_HIDE)
}

function Test-WindowVisible {
    <#
    .SYNOPSIS
    Returns $true if the window (by HWND) is currently shown (Win32
    IsWindowVisible). Cheaper than parsing winapp ui list-windows output.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][int64]$Hwnd)
    if (-not ('WinAppCli.PtWindowState' -as [type])) {
        Add-Type -TypeDefinition @"
            using System;
            using System.Runtime.InteropServices;
            namespace WinAppCli {
                public static class PtWindowState {
                    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
                    [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr hWnd);
                    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
                    public const int SW_HIDE      = 0;
                    public const int SW_SHOWNORMAL = 1;
                    public const int SW_MAXIMIZE  = 3;
                }
            }
"@
    }
    return [WinAppCli.PtWindowState]::IsWindowVisible([IntPtr]$Hwnd)
}

function Get-PixelAt {
    <#
    .SYNOPSIS
    Returns the System.Drawing.Color at screen coordinates (X, Y). Captures
    a 1×1 region via Graphics.CopyFromScreen so it works even when the target
    window is not foreground.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][int]$X, [Parameter(Mandatory)][int]$Y)
    Add-Type -AssemblyName System.Drawing -ErrorAction SilentlyContinue
    $bmp = New-Object System.Drawing.Bitmap 1, 1
    try {
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        try {
            $g.CopyFromScreen($X, $Y, 0, 0, (New-Object System.Drawing.Size 1, 1))
        } finally { $g.Dispose() }
        return $bmp.GetPixel(0, 0)
    } finally { $bmp.Dispose() }
}

function Get-PixelRowSample {
    <#
    .SYNOPSIS
    Sample N evenly-spaced pixels along a single row (or column) and return them.
    Use to check whether the AOT border / FZ accent / etc. is currently drawn
    on a given window edge.
    .PARAMETER Hwnd
    Window to sample relative to (uses GetWindowRect for the bounds).
    .PARAMETER Edge
    Which edge to sample: Top, Bottom, Left, Right.
    .PARAMETER OffsetPx
    Distance from the edge into the window (in pixels) for the row. Defaults to 2 — i.e. 2 px inside the bounding rect.
    .PARAMETER Samples
    Number of evenly-spaced samples along the edge.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][int]$Hwnd,
        [ValidateSet('Top','Bottom','Left','Right')][string]$Edge = 'Top',
        [int]$OffsetPx = 2,
        [int]$Samples = 11
    )
    $rect = Get-WindowRect -Hwnd $Hwnd
    if ($rect.Width -le 0 -or $rect.Height -le 0) { throw "Window $Hwnd has zero size" }
    $colors = New-Object System.Collections.Generic.List[object]
    switch ($Edge) {
        'Top'    { $y = $rect.Top + $OffsetPx;        $xs = 0..($Samples-1) | ForEach-Object { $rect.Left + [int](($rect.Width-1) * $_ / [Math]::Max(1,$Samples-1)) } }
        'Bottom' { $y = $rect.Bottom - 1 - $OffsetPx; $xs = 0..($Samples-1) | ForEach-Object { $rect.Left + [int](($rect.Width-1) * $_ / [Math]::Max(1,$Samples-1)) } }
        'Left'   { $x = $rect.Left + $OffsetPx;       $ys = 0..($Samples-1) | ForEach-Object { $rect.Top  + [int](($rect.Height-1) * $_ / [Math]::Max(1,$Samples-1)) } }
        'Right'  { $x = $rect.Right - 1 - $OffsetPx;  $ys = 0..($Samples-1) | ForEach-Object { $rect.Top  + [int](($rect.Height-1) * $_ / [Math]::Max(1,$Samples-1)) } }
    }
    if ($Edge -in 'Top','Bottom') {
        foreach ($x in $xs) { $colors.Add((Get-PixelAt -X $x -Y $y)) | Out-Null }
    } else {
        foreach ($yy in $ys) { $colors.Add((Get-PixelAt -X $x -Y $yy)) | Out-Null }
    }
    return ,$colors.ToArray()
}

function Test-PixelColorMatch {
    <#
    .SYNOPSIS
    Returns $true if at least $MinMatchPercent of $Pixels are within $Tolerance
    of $Expected. Use to assert "the border row is the AOT accent color".
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][System.Drawing.Color[]]$Pixels,
        [Parameter(Mandatory)][System.Drawing.Color]$Expected,
        [int]$Tolerance = 25,
        [int]$MinMatchPercent = 60
    )
    $hits = 0
    foreach ($p in $Pixels) {
        if ([Math]::Abs($p.R - $Expected.R) -le $Tolerance -and
            [Math]::Abs($p.G - $Expected.G) -le $Tolerance -and
            [Math]::Abs($p.B - $Expected.B) -le $Tolerance) { $hits++ }
    }
    $pct = ($hits * 100) / [Math]::Max(1, $Pixels.Count)
    return $pct -ge $MinMatchPercent
}
