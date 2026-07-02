# 06-Input.ps1 — keyboard input helpers (PInvoke SendInput).
#
# winappCli has no native send-keys verb (only mouse via `winapp ui click`).
# These helpers let test scripts simulate hotkeys (e.g. Win+Shift+/) and
# typed strings against whatever window currently has keyboard focus.

# Compile the SendInput PInvoke surface once per session
if (-not ('WinAppCli.PtSendInput' -as [type])) {
    Add-Type -TypeDefinition @"
        using System;
        using System.Runtime.InteropServices;
        namespace WinAppCli {
            [StructLayout(LayoutKind.Sequential)]
            public struct PtKeybdInput {
                public ushort wVk;
                public ushort wScan;
                public uint dwFlags;
                public uint time;
                public IntPtr dwExtraInfo;
            }
            [StructLayout(LayoutKind.Explicit, Size = 32)]
            public struct PtInputUnion {
                [FieldOffset(0)] public PtKeybdInput ki;
            }
            [StructLayout(LayoutKind.Sequential)]
            public struct PtInput {
                public uint type;
                public PtInputUnion u;
            }
            public static class PtSendInput {
                [DllImport("user32.dll", SetLastError = true)]
                private static extern uint SendInput(uint nInputs, [In] PtInput[] pInputs, int cbSize);

                public const uint INPUT_KEYBOARD = 1;
                public const uint KEYEVENTF_KEYUP = 0x0002;

                public static uint SendKey(ushort vk, bool keyUp) {
                    PtInput[] arr = new PtInput[1];
                    arr[0].type = INPUT_KEYBOARD;
                    arr[0].u.ki.wVk = vk;
                    arr[0].u.ki.wScan = 0;
                    arr[0].u.ki.dwFlags = keyUp ? KEYEVENTF_KEYUP : 0;
                    arr[0].u.ki.time = 0;
                    arr[0].u.ki.dwExtraInfo = IntPtr.Zero;
                    int sz = Marshal.SizeOf(typeof(PtInput));
                    uint sent = SendInput(1, arr, sz);
                    if (sent != 1) {
                        int err = Marshal.GetLastWin32Error();
                        throw new System.ComponentModel.Win32Exception(err, "SendInput failed (sent=" + sent + ", err=" + err + ", size=" + sz + ")");
                    }
                    return sent;
                }
            }
        }
"@
}

# Token → Win32 virtual-key code map
$script:VkCode = @{
    'win'='0x5B'; 'lwin'='0x5B'; 'rwin'='0x5C'
    'ctrl'='0x11'; 'control'='0x11'; 'lctrl'='0xA2'; 'rctrl'='0xA3'
    'shift'='0x10'; 'lshift'='0xA0'; 'rshift'='0xA1'
    'alt'='0x12'; 'lalt'='0xA4'; 'ralt'='0xA5'
    'esc'='0x1B'; 'escape'='0x1B'; 'enter'='0x0D'; 'return'='0x0D'
    'space'='0x20'; 'tab'='0x09'; 'backspace'='0x08'; 'delete'='0x2E'; 'del'='0x2E'
    'home'='0x24'; 'end'='0x23'; 'pageup'='0x21'; 'pagedown'='0x22'
    'left'='0x25'; 'up'='0x26'; 'right'='0x27'; 'down'='0x28'
    'f1'='0x70'; 'f2'='0x71'; 'f3'='0x72'; 'f4'='0x73'; 'f5'='0x74'; 'f6'='0x75'
    'f7'='0x76'; 'f8'='0x77'; 'f9'='0x78'; 'f10'='0x79'; 'f11'='0x7A'; 'f12'='0x7B'
    '/'='0xBF'; '\'='0xDC'; ';'='0xBA'; "'"='0xDE'; ','='0xBC'; '.'='0xBE'
    '-'='0xBD'; '='='0xBB'; '['='0xDB'; ']'='0xDD'; '`'='0xC0'
}

function _ResolveVk {
    param([string]$Token)
    $t = $Token.ToLowerInvariant()
    if ($script:VkCode.ContainsKey($t)) { return [byte]([Convert]::ToInt32($script:VkCode[$t], 16)) }
    if ($t.Length -eq 1) {
        $code = [int][char]$t
        if ($code -ge [int][char]'0' -and $code -le [int][char]'9') { return [byte]$code }   # VK_0..VK_9 == ASCII '0'..'9'
        if ($code -ge [int][char]'a' -and $code -le [int][char]'z') { return [byte]($code - 32) }  # VK_A..VK_Z == ASCII 'A'..'Z'
    }
    throw "Unknown key token: $Token"
}

function _SendKey {
    param([byte]$Vk, [bool]$KeyUp)
    [WinAppCli.PtSendInput]::SendKey([ushort]$Vk, $KeyUp) | Out-Null
}

function Send-PtHotkey {
    <#
    .SYNOPSIS
    Sends a hotkey combination via Win32 SendInput. Press order: modifiers down,
    key down, key up, modifiers up — matching how Windows interprets shortcuts.
    .PARAMETER Keys
    A '+'-separated combo, case-insensitive. Tokens: win, ctrl, alt, shift,
    f1-f12, esc, enter, space, tab, arrow keys (left/up/right/down), home, end,
    pageup, pagedown, del, /, \, ;, etc., or any single letter / digit.
    .EXAMPLE
    Send-PtHotkey -Keys 'Win+Shift+/'
    Send-PtHotkey -Keys 'Ctrl+Alt+L'
    Send-PtHotkey -Keys 'Esc'
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Keys, [int]$HoldMs = 30)
    $tokens = $Keys.Split('+', [StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object { $_.Trim() }
    if (-not $tokens) { throw "Empty key combo" }
    $modifierTokens = @('win','lwin','rwin','ctrl','control','lctrl','rctrl','shift','lshift','rshift','alt','lalt','ralt')
    $modifiers = @($tokens | Where-Object { $modifierTokens -contains $_.ToLowerInvariant() })
    $regular   = @($tokens | Where-Object { $modifierTokens -notcontains $_.ToLowerInvariant() })
    if ($regular.Count -eq 0) { throw "No non-modifier key in combo '$Keys'" }
    $modVks = $modifiers | ForEach-Object { _ResolveVk $_ }
    $keyVks = $regular   | ForEach-Object { _ResolveVk $_ }

    foreach ($v in $modVks) { _SendKey -Vk $v -KeyUp $false }
    Start-Sleep -Milliseconds 10
    foreach ($v in $keyVks) { _SendKey -Vk $v -KeyUp $false }
    Start-Sleep -Milliseconds $HoldMs
    foreach ($v in $keyVks) { _SendKey -Vk $v -KeyUp $true }
    foreach ($v in ($modVks | Sort-Object -Descending)) { _SendKey -Vk $v -KeyUp $true }
}

function Send-PtKey {
    <#
    .SYNOPSIS
    Send a single key (no modifiers). Convenience wrapper around Send-PtHotkey.
    .EXAMPLE
    Send-PtKey -Key 'Esc'
    Send-PtKey -Key 'Enter'
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Key, [int]$HoldMs = 30)
    Send-PtHotkey -Keys $Key -HoldMs $HoldMs
}

# Compile PostMessage PInvoke once per session — used by Post-PtKey below
if (-not ('WinAppCli.PtPostMessage' -as [type])) {
    Add-Type -TypeDefinition @"
        using System;
        using System.Runtime.InteropServices;
        namespace WinAppCli {
            public static class PtPostMessage {
                [DllImport("user32.dll", SetLastError = true)]
                public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
                public const uint WM_KEYDOWN = 0x0100;
                public const uint WM_KEYUP   = 0x0101;
            }
        }
"@
}

function Send-PtKeyToWindow {
    <#
    .SYNOPSIS
    Post a single key press (WM_KEYDOWN + WM_KEYUP) directly to a target HWND
    via PostMessage. Unlike Send-PtKey (which uses SendInput → kernel input queue),
    PostMessage goes straight into the target window's message queue. This means:

      - Does NOT require the target window to be foreground.
      - Does NOT require the caller and target to share elevation level
        (SendInput fails with ERROR_ACCESS_DENIED when an elevated test script
        tries to send input to a non-elevated AppX like CmdPal — PostMessage
        bypasses that UIPI restriction).
      - Useful for selecting items in a ListView, dismissing dialogs, etc.,
        without stealing focus from whatever the user is doing.

    Caveat: some apps with custom raw-input loops (games, RDP clients) do not
    process WM_KEYDOWN through their window proc and won't react. WinUI 3 and
    classic Win32 apps generally do.

    .PARAMETER Hwnd
    Target window handle. Get this from `winapp ui list-windows --json`.
    .PARAMETER Key
    Key token: 'down', 'up', 'left', 'right', 'enter', 'esc', 'tab', etc.
    Same vocabulary as Send-PtKey.
    .PARAMETER HoldMs
    Milliseconds between KEYDOWN and KEYUP. Default 30.

    .EXAMPLE
    Send-PtKeyToWindow -Hwnd $cpHwnd -Key 'down'    # navigate down 1 item without stealing focus
    Send-PtKeyToWindow -Hwnd $cpHwnd -Key 'enter'   # press Enter inside CmdPal
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][int64]$Hwnd,
        [Parameter(Mandatory)][string]$Key,
        [int]$HoldMs = 30
    )
    $vk = _ResolveVk $Key
    [void][WinAppCli.PtPostMessage]::PostMessage(
        [IntPtr]$Hwnd, [WinAppCli.PtPostMessage]::WM_KEYDOWN, [IntPtr]$vk, [IntPtr]0)
    Start-Sleep -Milliseconds $HoldMs
    [void][WinAppCli.PtPostMessage]::PostMessage(
        [IntPtr]$Hwnd, [WinAppCli.PtPostMessage]::WM_KEYUP,   [IntPtr]$vk, [IntPtr]0)
}

