# @powerscript.id           volume_up
# @powerscript.name         Volume Up
# @powerscript.description   Raise the system volume a few steps.
# @powerscript.kind         system
# @powerscript.capability   systemControl
#
# A "system" PowerScript (no file input). Assign it to a hotkey in Keyboard Manager. Sends the
# system "Volume Up" media key a few times.

$wsh = New-Object -ComObject WScript.Shell
for ($i = 0; $i -lt 4; $i++) {
    # 0xAF (175) is the Volume Up virtual key.
    $wsh.SendKeys([char]175)
    Start-Sleep -Milliseconds 40
}

'Volume raised.'
