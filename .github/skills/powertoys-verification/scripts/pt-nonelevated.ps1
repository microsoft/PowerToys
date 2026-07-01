# pt-nonelevated.ps1 — launch a process at MEDIUM integrity (non-elevated) from an
# already-elevated agent shell. Needed for tests that assert elevation-dependent
# visibility (e.g. File Locksmith L649/L650: a non-elevated module must NOT see
# higher-integrity processes; an elevated one must).
#
# The drive-stack in SKILL.md only covers gaining MORE privilege. De-elevation is the
# opposite problem: from a High-IL shell you cannot simply CreateProcess a Medium-IL
# child. The robust, dependency-free way is a one-shot Scheduled Task registered with
# RunLevel=Limited + LogonType=Interactive, which lands on the logged-on user's desktop
# at their filtered (medium) token. (The classic explorer-shell-injection trick is more
# fragile across sessions.)
#
# Functions:
#   Start-PtNonElevated -Exe <path> [-Arguments <str>]            -> launches a GUI/console exe non-elevated, returns the spawned PID(s)
#   Invoke-PtNonElevatedCapture -Exe <path> -Arguments <str> -OutFile <path>  -> runs a console exe non-elevated, redirects stdout/err to a file, waits, returns the file path
#
# Verify elevation of the result with Test-ProcessElevated (scripts/pt-admin-probe.ps1).

function Start-PtNonElevated {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Exe,
        [string]$Arguments = '',
        [int]$WaitSeconds = 5,
        [string]$MatchProcessName  # optional: base name to enumerate spawned PIDs (e.g. 'PowerToys.FileLocksmithUI')
    )
    if (-not (Test-Path $Exe)) { throw "Exe not found: $Exe" }
    $taskName = "PtNonElev_$([guid]::NewGuid().ToString('N').Substring(0,8))"
    $before = @()
    if ($MatchProcessName) { $before = @(Get-Process -Name $MatchProcessName -EA SilentlyContinue | Select-Object -Expand Id) }
    try {
        $action    = New-ScheduledTaskAction -Execute $Exe -Argument $Arguments
        $principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -RunLevel Limited -LogonType Interactive
        Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal -Force | Out-Null
        Start-ScheduledTask -TaskName $taskName
        Start-Sleep -Seconds $WaitSeconds
        if ($MatchProcessName) {
            $after = @(Get-Process -Name $MatchProcessName -EA SilentlyContinue | Select-Object -Expand Id)
            return ($after | Where-Object { $_ -notin $before })
        }
        return $null
    }
    finally {
        Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -EA SilentlyContinue
    }
}

function Invoke-PtNonElevatedCapture {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Exe,
        [string]$Arguments = '',
        [Parameter(Mandatory)][string]$OutFile,
        [int]$TimeoutSeconds = 30
    )
    if (-not (Test-Path $Exe)) { throw "Exe not found: $Exe" }
    Remove-Item $OutFile -EA SilentlyContinue
    $wrap = [IO.Path]::ChangeExtension($OutFile, '.cmd')
    "`"$Exe`" $Arguments > `"$OutFile`" 2>&1" | Set-Content -Encoding ascii $wrap
    $taskName = "PtNonElev_$([guid]::NewGuid().ToString('N').Substring(0,8))"
    try {
        $action    = New-ScheduledTaskAction -Execute 'cmd.exe' -Argument "/c `"$wrap`""
        $principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -RunLevel Limited -LogonType Interactive
        Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal -Force | Out-Null
        Start-ScheduledTask -TaskName $taskName
        $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
        do { Start-Sleep 1; $info = Get-ScheduledTaskInfo -TaskName $taskName }
        while ($info.LastTaskResult -eq 267009 -and (Get-Date) -lt $deadline)   # 267009 = task still running
        Start-Sleep 1
        return $OutFile
    }
    finally {
        Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -EA SilentlyContinue
        Remove-Item $wrap -EA SilentlyContinue
    }
}
