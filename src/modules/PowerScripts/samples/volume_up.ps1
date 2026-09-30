# Volume Up — raise the system volume. Metadata lives in the sibling volume_up.ps1.tool.json
# descriptor (an MCP Tool). A "system" PowerScript (no file input): assign it to a hotkey in
# Keyboard Manager. Sends the system "Volume Up" media key a few times.

$wsh = New-Object -ComObject WScript.Shell
for ($i = 0; $i -lt 4; $i++) {
    # 0xAF (175) is the Volume Up virtual key.
    $wsh.SendKeys([char]175)
    Start-Sleep -Milliseconds 40
}

'Volume raised.'
