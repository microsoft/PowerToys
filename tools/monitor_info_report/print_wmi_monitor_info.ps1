$output = gwmi WmiMonitorID -Namespace root\wmi | ForEach-Object { ($_.UserFriendlyName -notmatch 0 | foreach {[char]$_}) -join ""; ($_.SerialNumberID -notmatch 0 | foreach {[char]$_}) -join ""; $_.InstanceName + "`n" } | Out-String
Write-Host $output
$LogTime = Get-Date -Format "MM-dd-yyyy_hh-mm-ss"
$LogFile = 'wmi_monitor_info-' + $LogTime + ".log"
Write-Output $output | Out-File $LogFile

