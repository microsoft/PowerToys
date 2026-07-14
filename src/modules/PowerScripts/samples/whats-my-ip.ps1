# @powerscript.id          whats-my-ip
# @powerscript.name        What's my IP
# @powerscript.description  Look up this PC's public IP address and show it in a message box.
# @powerscript.kind        system
# @powerscript.capability  network
# @powerscript.icon        \uE774
#
# A self-contained, single-file PowerScript: all of its metadata lives in the @powerscript.*
# header comment above, so there is no separate manifest.json. Because no surfaces are declared,
# PowerScripts infers them from the "action" kind (Keyboard Manager + Command Palette).

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
