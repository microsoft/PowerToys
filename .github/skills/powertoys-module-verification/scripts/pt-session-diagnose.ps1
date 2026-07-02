# pt-session-diagnose.ps1
# Diagnose whether the current shell can drive interactive PowerToys tests.
# Tells you in one go: am I on the active console session, can I see foreground windows,
# and can I use Shell COM. If not, prints the exact psexec mitigation command.

Add-Type 'using System; using System.Runtime.InteropServices;
public class WTS { [DllImport("kernel32.dll")] public static extern uint WTSGetActiveConsoleSessionId(); }
public class FG  { [DllImport("user32.dll")]   public static extern IntPtr GetForegroundWindow(); }'

Write-Host "--- Logged-on users + sessions ---" -ForegroundColor Cyan
quser 2>&1

Write-Host "`n--- This shell's session ---" -ForegroundColor Cyan
$me = [Diagnostics.Process]::GetCurrentProcess()
"  PID:     $($me.Id)"
"  Session: $($me.SessionId)"

Write-Host "`n--- Console Explorer session(s) ---" -ForegroundColor Cyan
$exps = Get-Process explorer -ErrorAction SilentlyContinue
if ($exps) {
    $exps | Select-Object Id, SessionId, @{N='StartTime';E={$_.StartTime}} | Format-Table -AutoSize
} else {
    Write-Host "  (no explorer.exe running)" -ForegroundColor Yellow
}

Write-Host "`n--- Windows active console + foreground + Shell COM ---" -ForegroundColor Cyan
$activeConsole = [WTS]::WTSGetActiveConsoleSessionId()
$fg = [FG]::GetForegroundWindow()
$shellOk = $false
try { @((New-Object -ComObject Shell.Application).Windows()).Count | Out-Null; $shellOk = $true } catch {}
"  WTSGetActiveConsoleSessionId() = $activeConsole"
"  GetForegroundWindow()          = $fg"
"  Shell.Application available    = $shellOk"

Write-Host "`n--- Verdict ---" -ForegroundColor Cyan
$consoleSession = ($exps | Select-Object -First 1).SessionId
if ($me.SessionId -eq $consoleSession -and $fg -ne 0 -and $shellOk) {
    Write-Host "  PASS - this shell can drive interactive PowerToys tests." -ForegroundColor Green
} elseif ($me.SessionId -eq $consoleSession -and $fg -eq 0) {
    Write-Host "  WARN - same session as explorer but no foreground (workstation locked?). Unlock and re-run." -ForegroundColor Yellow
} elseif (-not $shellOk) {
    Write-Host "  FAIL - Shell COM unavailable (likely Session 0 / service context). Very few tests possible." -ForegroundColor Red
} else {
    Write-Host "  FAIL - shell in Session $($me.SessionId), console explorer in Session $consoleSession. Input injection denied." -ForegroundColor Red
    Write-Host "         Mitigation: relaunch in the console session with:" -ForegroundColor Yellow
    Write-Host "         psexec -accepteula -h -i $consoleSession -s pwsh.exe" -ForegroundColor Yellow
}
