# What's my IP — look up this PC's public IP. Metadata lives in the sibling
# whats-my-ip.ps1.tool.json descriptor (an MCP Tool), so there is no separate manifest and no
# @powerscript header. Its no-input contract makes it available to action consumers such as
# Keyboard Manager, LightSwitch, and Command Palette.

$ErrorActionPreference = 'Stop'

try {
    $ip = (Invoke-RestMethod -Uri 'https://api.ipify.org?format=json' -TimeoutSec 10).ip
    $message = "Your public IP address is:`n`n$ip"
} catch {
    $message = "Could not determine your public IP address.`n`n$($_.Exception.Message)"
}

Add-Type -Namespace PS -Name Native -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
public static extern int MessageBox(System.IntPtr hWnd, string text, string caption, uint type);
'@

# MB_SYSTEMMODAL | MB_SETFOREGROUND | MB_TOPMOST keeps the result reliably on top.
[PS.Native]::MessageBox([System.IntPtr]::Zero, $message, "What's my IP", 0x00051000) | Out-Null
