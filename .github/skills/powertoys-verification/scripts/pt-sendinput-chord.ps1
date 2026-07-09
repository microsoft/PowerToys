# scripts/pt-sendinput-chord.ps1
# Inject a global hotkey chord (e.g. Win+Shift+/) into the system input stream.
# Critical: INPUT struct MUST be cb=40 on x64 (with padding for the MOUSEINPUT union member).
# The common bug "Win+ hotkeys can't be injected" is a marshaling error producing 32-byte struct
# and SendInput returns 0 with GetLastError()==87 (ERROR_INVALID_PARAMETER).
#
# This SHOULD be a last resort. Prefer Named Events (Invoke-PtSharedEvent) when the module exposes one.
# Use this only for: (a) explicit hotkey-trigger verification tests, (b) modules without Named Events,
# (c) UI keystrokes inside an already-foreground window (use Send-KeyToHwnd via PostMessage instead
# for elevated -> non-elevated AppX, see references/winapp-ui-testing.md).

if (-not ('PtChord' -as [type])) {
    Add-Type -TypeDefinition @'
        using System;
        using System.Runtime.InteropServices;
        using System.Collections.Generic;
        public static class PtChord {
            [StructLayout(LayoutKind.Sequential)]
            struct INPUT { public uint type; public KEYBDINPUT ki; public int pad1; public int pad2; } // pad to 40 bytes
            [StructLayout(LayoutKind.Sequential)]
            struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
            [DllImport("user32.dll", SetLastError=true)]
            static extern uint SendInput(uint n, INPUT[] p, int cb);
            const uint KEYUP = 0x0002;
            static INPUT K(ushort vk, bool up) { INPUT i=new INPUT(); i.type=1; i.ki.wVk=vk; i.ki.dwFlags=up?KEYUP:0; return i; }
            public static uint Chord(ushort[] mods, ushort key) {
                var l=new List<INPUT>();
                foreach(var m in mods) l.Add(K(m,false));
                l.Add(K(key,false)); l.Add(K(key,true));
                for(int i=mods.Length-1;i>=0;i--) l.Add(K(mods[i],true));
                var a=l.ToArray();
                return SendInput((uint)a.Length, a, Marshal.SizeOf(typeof(INPUT)));
            }
            public static uint Tap(ushort key) { return Chord(new ushort[0], key); }
        }
'@
}

# Common VK codes for chord mods:
#   LWIN=0x5B  RWIN=0x5C  CTRL=0x11  SHIFT=0x10  ALT=0x12
# Main key VKs:
#   0x08 Backspace 0x09 Tab 0x0D Enter 0x1B Escape 0x20 Space
#   0x25 Left 0x26 Up 0x27 Right 0x28 Down
#   0x30..0x39 0..9    0x41..0x5A A..Z

function Send-PtChord {
    <#
    .SYNOPSIS
    Inject a hotkey chord. Returns number of inputs Windows accepted (0 = failed; check GetLastError).
    .EXAMPLE
    Send-PtChord -Mods 0x5B,0x10 -Key 0x43      # Win+Shift+C (Color Picker)
    Send-PtChord -Mods 0x5B,0x11 -Key 0x52      # Win+Ctrl+R (PowerOcr)
    Send-PtChord -Mods 0x5B,0xA4 -Key 0x20      # Win+Alt+Space (CmdPal default)
    Send-PtChord -Key 0x0D                       # plain Enter (no mods)
    #>
    [CmdletBinding()]
    param(
        [uint16[]]$Mods = @(),
        [Parameter(Mandatory)][uint16]$Key
    )
    $sent = [PtChord]::Chord($Mods, $Key)
    if ($sent -eq 0) {
        $err = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
        throw "SendInput failed (returned 0, GetLastError=$err). Likely caller is at lower integrity than PT runner, or chord is OS-reserved (Win+L, Win+Tab)."
    }
    return $sent
}

function Wait-PtHotkeyAccepted {
    <#
    .SYNOPSIS
    After Send-PtChord, verify the PT runner saw it by tailing its log for the centralized-hook line.
    Returns the matching log line (if any) within $TimeoutSec.
    .EXAMPLE
    Send-PtChord -Mods 0x5B,0x10 -Key 0x43
    $line = Wait-PtHotkeyAccepted -ModuleHint 'Color' -TimeoutSec 3
    if (-not $line) { throw "Runner did not log hotkey invocation" }
    #>
    [CmdletBinding()]
    param([string]$ModuleHint = '', [int]$TimeoutSec = 3)
    $log = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\PowerToys\RunnerLogs" -Filter 'runner-log_*.log' -EA SilentlyContinue |
           Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $log) { return $null }
    $start = (Get-Date).AddSeconds(-2)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    do {
        $line = Get-Content $log.FullName -Tail 50 -EA SilentlyContinue |
                Where-Object { $_ -match 'hotkey is invoked from Centralized keyboard hook' -and ($ModuleHint -eq '' -or $_ -match $ModuleHint) } |
                Select-Object -Last 1
        if ($line) { return $line }
        Start-Sleep -Milliseconds 200
    } while ((Get-Date) -lt $deadline)
    return $null
}
