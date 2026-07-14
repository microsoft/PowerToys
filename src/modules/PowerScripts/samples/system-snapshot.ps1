# @powerscript.id           system-snapshot
# @powerscript.name         System Snapshot
# @powerscript.description   Show computer name, OS and uptime.
# @powerscript.kind         system
# @powerscript.capability   systemInfo
#
# A "system" PowerScript (no file input). Surfaced via a Keyboard Manager hotkey or the Command
# Palette (both inferred from the system kind).

$os = Get-CimInstance Win32_OperatingSystem -ErrorAction SilentlyContinue

[pscustomobject]@{
    Computer = $env:COMPUTERNAME
    User     = $env:USERNAME
    OS       = if ($os) { $os.Caption } else { [System.Environment]::OSVersion.VersionString }
    Uptime   = if ($os) { (Get-Date) - $os.LastBootUpTime } else { 'n/a' }
    Time     = (Get-Date).ToString('s')
} | Format-List
