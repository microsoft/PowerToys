<#
.SYNOPSIS
Checks that Awake's settings writes reach .NET FileSystemWatchers set up like the Settings app's.

.DESCRIPTION
AwakePage and SettingsRepository watch settings.json for Changed events with NotifyFilters
LastWrite (and CreationTime). Awake replaces the file atomically and then refreshes its write
time; this script proves each of its writes raises at least one Changed event.
#>
$ErrorActionPreference = 'Stop'
$dir = Join-Path ([IO.Path]::GetTempPath()) "awake-watch-$PID"
New-Item -ItemType Directory -Force $dir | Out-Null
Set-Content -Path (Join-Path $dir 'settings.json') -Value '{}' -NoNewline

$watcher = New-Object IO.FileSystemWatcher $dir, 'settings.json'
$watcher.NotifyFilter = [IO.NotifyFilters]::LastWrite -bor [IO.NotifyFilters]::CreationTime
$changed = [ref]0
$registration = Register-ObjectEvent $watcher Changed -Action { $Event.MessageData.Value++ } -MessageData $changed
$watcher.EnableRaisingEvents = $true

$env:AWAKE_WATCH_TEST_DIR = $dir
Push-Location (Join-Path $PSScriptRoot '..')
try {
    cargo test -q -p awake watcher_probe -- --ignored | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'the write probe failed' }
} finally {
    Pop-Location
}
Start-Sleep -Milliseconds 500
$watcher.EnableRaisingEvents = $false
Unregister-Event -SourceIdentifier $registration.Name
Remove-Item -Recurse -Force $dir

Write-Host "Changed events seen: $($changed.Value) (3 writes)"
if ($changed.Value -lt 3) { throw 'A .NET watcher missed some of Awake''s writes.' }
