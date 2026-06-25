# System Snapshot — a "system" PowerScript (no file input).
# Surfaced via a Keyboard Manager hotkey or the Command Palette.

$os = Get-CimInstance Win32_OperatingSystem -ErrorAction SilentlyContinue

[pscustomobject]@{
    Computer = $env:COMPUTERNAME
    User     = $env:USERNAME
    OS       = if ($os) { $os.Caption } else { [System.Environment]::OSVersion.VersionString }
    Uptime   = if ($os) { (Get-Date) - $os.LastBootUpTime } else { 'n/a' }
    Time     = (Get-Date).ToString('s')
} | Format-List
