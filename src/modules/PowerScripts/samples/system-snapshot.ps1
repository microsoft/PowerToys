# System Snapshot — describe this PC. Metadata lives in the sibling system-snapshot.ps1.tool.json
# descriptor (an MCP Tool). Its no-input contract makes it available to action consumers such as
# Keyboard Manager, LightSwitch, and Command Palette.

$os = Get-CimInstance Win32_OperatingSystem -ErrorAction SilentlyContinue

[pscustomobject]@{
    Computer = $env:COMPUTERNAME
    User     = $env:USERNAME
    OS       = if ($os) { $os.Caption } else { [System.Environment]::OSVersion.VersionString }
    Uptime   = if ($os) { (Get-Date) - $os.LastBootUpTime } else { 'n/a' }
    Time     = (Get-Date).ToString('s')
} | Format-List
