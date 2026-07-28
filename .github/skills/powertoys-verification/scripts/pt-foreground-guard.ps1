# scripts/pt-foreground-guard.ps1
# Verify and force a window to foreground BEFORE sending SendInput.
# Without this guard, SendInput keys silently leak to the caller's terminal when
# the target window has lost foreground (common with CmdPal AppX where Windows
# foreground-lock blocks SetForegroundWindow after the first attempt).
#
# Use winapp ui set-value for UIA-friendly inputs (no foreground required).
# Use this guard ONLY when you need real keystrokes (e.g. CmdPal alias detection).

if (-not ('PtFg' -as [type])) {
    Add-Type -TypeDefinition @'
        using System;
        using System.Runtime.InteropServices;
        public static class PtFg {
            [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
            [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
            [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
            [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
            [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
            [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
            [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool f);
            [DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(int pid);
        }
'@
}

function Test-PtForeground {
    <#
    .SYNOPSIS
    Check whether the target AppX is currently foreground by parsing winapp ui list-windows output
    for the literal substring 'foreground'.
    #>
    param([Parameter(Mandatory)][string]$AppId)
    $r = winapp ui list-windows -a $AppId 2>$null | Out-String
    return ($r -match 'foreground')
}

function Get-PtHwnd {
    <#
    .SYNOPSIS
    Return the first HWND for the given AppX/exe. Returns [IntPtr]::Zero if none.
    #>
    param([Parameter(Mandatory)][string]$AppId)
    $r = winapp ui list-windows -a $AppId 2>$null | Out-String
    if ($r -match 'HWND (\d+):') { return [IntPtr][int64]$matches[1] }
    return [IntPtr]::Zero
}

function Force-PtForeground {
    <#
    .SYNOPSIS
    Force the target AppX window to foreground using the AttachThreadInput + AllowSetForegroundWindow
    trick. Returns $true if window is foreground after this attempt; $false otherwise.
    .NOTES
    Windows foreground-lock will block subsequent SetForegroundWindow calls in the same session if
    a real interactive event hasn't fired recently. If this returns $false repeatedly, the only
    reliable recovery is to recycle the AppX (kill + relaunch via shell:AppsFolder URI).
    #>
    param([Parameter(Mandatory)][string]$AppId)
    $h = Get-PtHwnd -AppId $AppId
    if ($h -eq [IntPtr]::Zero) { return $false }

    # Permission grant
    $proc = Get-Process | Where-Object { $_.MainWindowHandle -eq $h } | Select-Object -First 1
    if ($proc) { [PtFg]::AllowSetForegroundWindow($proc.Id) | Out-Null }

    [PtFg]::ShowWindow($h, 9) | Out-Null  # SW_RESTORE
    Start-Sleep -Milliseconds 150

    # AttachThreadInput trick
    $fg = [PtFg]::GetForegroundWindow()
    $fgPid = 0
    $fgThread = [PtFg]::GetWindowThreadProcessId($fg, [ref]$fgPid)
    $curThread = [PtFg]::GetCurrentThreadId()
    if ($fgThread -ne 0 -and $fgThread -ne $curThread) {
        [PtFg]::AttachThreadInput($curThread, $fgThread, $true) | Out-Null
    }
    [PtFg]::BringWindowToTop($h) | Out-Null
    [PtFg]::SetForegroundWindow($h) | Out-Null
    [PtFg]::ShowWindow($h, 5) | Out-Null  # SW_SHOW
    if ($fgThread -ne 0 -and $fgThread -ne $curThread) {
        [PtFg]::AttachThreadInput($curThread, $fgThread, $false) | Out-Null
    }
    Start-Sleep -Milliseconds 400
    return (Test-PtForeground -AppId $AppId)
}

function Assert-PtForegroundOrAbort {
    <#
    .SYNOPSIS
    Guard helper. Throws if the target AppX is NOT foreground. Use this immediately before any
    SendInput call to ensure keys don't leak to the wrong window.
    #>
    param([Parameter(Mandatory)][string]$AppId)
    if (-not (Test-PtForeground -AppId $AppId)) {
        if (-not (Force-PtForeground -AppId $AppId)) {
            throw "ABORT: $AppId is not foreground and cannot be forced foreground. SendInput would leak to wrong window."
        }
    }
}
