# 11-MouseInput.ps1 — mouse input helpers (PInvoke SendInput).
#
# Closes the gap exposed by the FancyZones C# WinAppDriver tests: winappCli
# has `winapp ui click` (single-shot click on a UIA element) but no mouse-
# move / mouse-drag / mouse-with-modifier capability. The C# tests rely on
# Session.MoveMouseTo + PerformMouseAction(LeftDown) + PressKey(Shift) +
# MoveMouseTo + ReleaseKey + PerformMouseAction(LeftUp) — which we now
# replicate via Win32 SendInput.
#
# Why a separate type from PtSendInput (06-Input.ps1):
# Keeping mouse + keyboard in one [StructLayout(Explicit)] union is fragile
# (size/offset must match for both). Two parallel types is cleaner and
# avoids re-marshalling the existing keyboard helpers.

if (-not ('WinAppCli.PtMouseInput' -as [type])) {
    Add-Type -TypeDefinition @"
        using System;
        using System.Runtime.InteropServices;
        namespace WinAppCli {
            [StructLayout(LayoutKind.Sequential)]
            public struct PtMouseInputData {
                public int dx;
                public int dy;
                public uint mouseData;
                public uint dwFlags;
                public uint time;
                public IntPtr dwExtraInfo;
            }
            [StructLayout(LayoutKind.Explicit, Size = 32)]
            public struct PtMouseInputUnion {
                [FieldOffset(0)] public PtMouseInputData mi;
            }
            [StructLayout(LayoutKind.Sequential)]
            public struct PtMouseInputStruct {
                public uint type;
                public PtMouseInputUnion u;
            }
            public static class PtMouseInput {
                [DllImport("user32.dll", SetLastError = true)]
                private static extern uint SendInput(uint nInputs, [In] PtMouseInputStruct[] pInputs, int cbSize);
                [DllImport("user32.dll")]
                public static extern bool SetCursorPos(int X, int Y);
                [DllImport("user32.dll")]
                public static extern bool GetCursorPos(out POINT lpPoint);
                [DllImport("user32.dll")]
                public static extern int GetSystemMetrics(int nIndex);

                [StructLayout(LayoutKind.Sequential)]
                public struct POINT { public int X; public int Y; }

                public const uint INPUT_MOUSE = 0;
                public const uint MOUSEEVENTF_MOVE       = 0x0001;
                public const uint MOUSEEVENTF_LEFTDOWN   = 0x0002;
                public const uint MOUSEEVENTF_LEFTUP     = 0x0004;
                public const uint MOUSEEVENTF_RIGHTDOWN  = 0x0008;
                public const uint MOUSEEVENTF_RIGHTUP    = 0x0010;
                public const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
                public const uint MOUSEEVENTF_MIDDLEUP   = 0x0040;
                public const uint MOUSEEVENTF_WHEEL      = 0x0800;
                public const uint MOUSEEVENTF_ABSOLUTE   = 0x8000;
                public const int  SM_CXSCREEN = 0;
                public const int  SM_CYSCREEN = 1;

                // Convert a screen-pixel coord to the normalized 0..65535 SendInput
                // coord required by MOUSEEVENTF_ABSOLUTE.
                public static int ToAbs(int pixel, int screenSize) {
                    // +0.5 rounding via int cast of double
                    return (int)((double)pixel * 65535.0 / (double)screenSize);
                }

                public static uint SendMove(int x, int y) {
                    int cx = GetSystemMetrics(SM_CXSCREEN);
                    int cy = GetSystemMetrics(SM_CYSCREEN);
                    PtMouseInputStruct[] arr = new PtMouseInputStruct[1];
                    arr[0].type = INPUT_MOUSE;
                    arr[0].u.mi.dx = ToAbs(x, cx);
                    arr[0].u.mi.dy = ToAbs(y, cy);
                    arr[0].u.mi.mouseData = 0;
                    arr[0].u.mi.dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE;
                    arr[0].u.mi.time = 0;
                    arr[0].u.mi.dwExtraInfo = IntPtr.Zero;
                    return Send(arr);
                }

                public static uint SendButton(uint flag) {
                    PtMouseInputStruct[] arr = new PtMouseInputStruct[1];
                    arr[0].type = INPUT_MOUSE;
                    arr[0].u.mi.dx = 0;
                    arr[0].u.mi.dy = 0;
                    arr[0].u.mi.mouseData = 0;
                    arr[0].u.mi.dwFlags = flag;
                    arr[0].u.mi.time = 0;
                    arr[0].u.mi.dwExtraInfo = IntPtr.Zero;
                    return Send(arr);
                }

                private static uint Send(PtMouseInputStruct[] arr) {
                    int sz = Marshal.SizeOf(typeof(PtMouseInputStruct));
                    uint sent = SendInput((uint)arr.Length, arr, sz);
                    if (sent != arr.Length) {
                        int err = Marshal.GetLastWin32Error();
                        throw new System.ComponentModel.Win32Exception(err,
                            "Mouse SendInput failed (sent=" + sent + ", err=" + err + ", size=" + sz + ")");
                    }
                    return sent;
                }
            }
        }
"@
}

# ── Public mouse helpers ──────────────────────────────────────────────

function Move-PtMouseTo {
    <#
    .SYNOPSIS
    Move the OS mouse cursor to absolute screen coordinates (x, y) via Win32
    SendInput. This is what generates real mouse-move messages that apps
    listen for (vs SetCursorPos, which jumps the cursor without firing the
    full input pipeline that FancyZones' drag detection needs).

    .EXAMPLE
    Move-PtMouseTo -X 800 -Y 400
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][int]$X, [Parameter(Mandatory)][int]$Y)
    [WinAppCli.PtMouseInput]::SendMove($X, $Y) | Out-Null
}

function Send-PtMouseButton {
    <#
    .SYNOPSIS
    Send a mouse button event (down or up) at the current cursor position.

    .PARAMETER Button
    Left, Right, or Middle.
    .PARAMETER Action
    Down or Up.

    .EXAMPLE
    Send-PtMouseButton -Button Left -Action Down
    Send-PtMouseButton -Button Left -Action Up
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateSet('Left','Right','Middle')][string]$Button,
        [Parameter(Mandatory)][ValidateSet('Down','Up')][string]$Action
    )
    $flag = switch ("$Button-$Action") {
        'Left-Down'   { [WinAppCli.PtMouseInput]::MOUSEEVENTF_LEFTDOWN }
        'Left-Up'     { [WinAppCli.PtMouseInput]::MOUSEEVENTF_LEFTUP }
        'Right-Down'  { [WinAppCli.PtMouseInput]::MOUSEEVENTF_RIGHTDOWN }
        'Right-Up'    { [WinAppCli.PtMouseInput]::MOUSEEVENTF_RIGHTUP }
        'Middle-Down' { [WinAppCli.PtMouseInput]::MOUSEEVENTF_MIDDLEDOWN }
        'Middle-Up'   { [WinAppCli.PtMouseInput]::MOUSEEVENTF_MIDDLEUP }
    }
    [WinAppCli.PtMouseInput]::SendButton($flag) | Out-Null
}

function Send-PtMouseClick {
    <#
    .SYNOPSIS
    Convenience: button-down + button-up at current cursor position.

    .EXAMPLE
    Move-PtMouseTo 800 400
    Send-PtMouseClick -Button Right
    #>
    [CmdletBinding()]
    param([ValidateSet('Left','Right','Middle')][string]$Button = 'Left', [int]$HoldMs = 30)
    Send-PtMouseButton -Button $Button -Action Down
    Start-Sleep -Milliseconds $HoldMs
    Send-PtMouseButton -Button $Button -Action Up
}

function Get-PtCursorPos {
    <#
    .SYNOPSIS
    Returns the current cursor position as @{ X=…; Y=… }.
    #>
    $p = New-Object 'WinAppCli.PtMouseInput+POINT'
    [WinAppCli.PtMouseInput]::GetCursorPos([ref]$p) | Out-Null
    [pscustomobject]@{ X = $p.X; Y = $p.Y }
}

function Send-PtMouseDrag {
    <#
    .SYNOPSIS
    Smoothly drag the mouse from one screen coord to another, optionally
    with modifier keys (Shift, Ctrl, Alt, Win) held during the move.

    Replicates the WinAppDriver pattern used in FancyZones C# tests:
        Session.MoveMouseTo(x1, y1)
        Session.PerformMouseAction(LeftDown)
        Session.PressKey(Shift)
        Session.MoveMouseTo(x2, y2)
        Session.PerformMouseAction(LeftUp)
        Session.ReleaseKey(Shift)

    The drag goes through Steps intermediate positions so apps that
    require sustained motion (like FancyZones' drag-detect threshold)
    actually see movement.

    .PARAMETER From
    Two-element array @(x, y) — start screen pixel coords.
    .PARAMETER To
    Two-element array @(x, y) — end screen pixel coords.
    .PARAMETER Button
    Mouse button held during drag. Default Left.
    .PARAMETER Modifier
    Optional modifier key(s) to hold during the drag. Tokens recognised by
    Send-PtHotkey: 'Shift', 'Ctrl', 'Alt', 'Win'. Pass as array for
    multi-key combos.
    .PARAMETER Steps
    Number of intermediate mouse-move events generated. Default 20 — enough
    for FancyZones' detection. Larger = smoother but slower.
    .PARAMETER StepDelayMs
    Pause between each intermediate move. Default 15 ms — matches typical
    human drag speed and gives FancyZones time to render the zone overlay.

    .EXAMPLE
    # Drag with no modifier (just a normal drag)
    Send-PtMouseDrag -From @(100,100) -To @(800,400)

    .EXAMPLE
    # Drag with Shift held — what FancyZones requires for snap mode
    Send-PtMouseDrag -From @(100,100) -To @(800,400) -Modifier 'Shift'
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][int[]]$From,
        [Parameter(Mandatory)][int[]]$To,
        [ValidateSet('Left','Right','Middle')][string]$Button = 'Left',
        [string[]]$Modifier = @(),
        [int]$Steps       = 20,
        [int]$StepDelayMs = 15
    )
    if ($From.Count -ne 2) { throw "-From must be @(x, y)" }
    if ($To.Count   -ne 2) { throw "-To must be @(x, y)" }
    if ($Steps -lt 1)      { throw "-Steps must be >= 1" }

    # 1. Move to start position
    Move-PtMouseTo -X $From[0] -Y $From[1]
    Start-Sleep -Milliseconds 50

    # 2. Press the mouse button
    Send-PtMouseButton -Button $Button -Action Down
    Start-Sleep -Milliseconds 50

    # 3. Press modifier keys (if any) — borrow Send-PtHotkey's VK resolver
    foreach ($m in $Modifier) {
        $vk = _ResolveVk $m
        _SendKey -Vk $vk -KeyUp $false
    }
    Start-Sleep -Milliseconds 50

    # 4. Smoothly move to end position via intermediate steps
    for ($i = 1; $i -le $Steps; $i++) {
        $t  = [double]$i / $Steps
        $cx = [int]($From[0] + ($To[0] - $From[0]) * $t)
        $cy = [int]($From[1] + ($To[1] - $From[1]) * $t)
        Move-PtMouseTo -X $cx -Y $cy
        Start-Sleep -Milliseconds $StepDelayMs
    }

    # 5. Release modifier keys (in reverse order)
    foreach ($m in ($Modifier | ForEach-Object { $_ } | Sort-Object -Descending)) {
        $vk = _ResolveVk $m
        _SendKey -Vk $vk -KeyUp $true
    }
    Start-Sleep -Milliseconds 50

    # 6. Release the mouse button
    Send-PtMouseButton -Button $Button -Action Up
    Start-Sleep -Milliseconds 50
}
