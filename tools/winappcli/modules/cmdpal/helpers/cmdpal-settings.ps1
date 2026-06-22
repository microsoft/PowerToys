#Requires -Version 7.0
# cmdpal-settings.ps1 — split from _helpers.ps1 (review item #5).
# Dot-sourced from _helpers.ps1; shares script scope with the orchestrator
# so it sees $cpHwnd / $cpSettings / $cpEnabled / $cpDataDir.

# ── Settings-mutation scaffold (used by L1048-L1050 tests) ──────────
# Some tests need to MUTATE CmdPal's settings.json (alias/hotkey/provider
# IsEnabled), restart the AppX, verify the change took effect, then
# ALWAYS restore the original settings — even on test failure / Ctrl+C.
# Risk if Cleanup fails: user's settings get corrupted.
# Mitigation: backup to %TEMP% first, restore in -Cleanup, atomic Copy.
#
# IMPORTANT: CmdPal AppX holds settings.json open while running, so we
# must STOP the AppX before writing settings, then start it again. The
# write-with-AppX-stopped helper does that atomically.

function Backup-CmdPalSettingsJson {
    # Returns the path to a temp backup. Caller passes it to Restore- on cleanup.
    # Uses [File]::ReadAllBytes which respects the file's existing share mode
    # (CmdPal opens settings.json with read-share, so this works while AppX
    # is alive). PowerShell's Get-Content -Raw can fail on the same file.
    if (-not (Test-Path $cpSettings)) {
        throw "Cannot backup — settings.json missing at $cpSettings"
    }
    $backup = Join-Path $env:TEMP ("winappcli-cmdpal-settings-backup-$(Get-Random).json")
    # Try multiple methods; CmdPal sometimes briefly holds an exclusive lock
    # during writes. Retry up to 3s.
    $deadline = (Get-Date).AddSeconds(3)
    do {
        try {
            $bytes = [System.IO.File]::ReadAllBytes($cpSettings)
            [System.IO.File]::WriteAllBytes($backup, $bytes)
            return $backup
        } catch {
            Start-Sleep -Milliseconds 200
        }
    } while ((Get-Date) -lt $deadline)
    throw "Cannot read settings.json for backup after 3s: file is exclusively locked"
}

function Restore-CmdPalSettingsJson {
    # Stop AppX so we can write the file, then write, then leave to caller
    # to restart. Uses [File]::* to bypass PowerShell cmdlet share-mode quirks.
    #
    # USER-DATA SAFETY contract (added Phase 1 of 2026-05-20 review):
    # - On success, the on-disk settings.json is byte-identical to the
    #   backup. We verify by re-reading and comparing length + SHA256.
    # - On failure (file locked, partial write, mismatch), we THROW
    #   loudly — the caller (always in a finally{} block) will surface
    #   it via Write-Warning at minimum, and the backup is preserved.
    # - We NEVER delete the backup unless the restore is byte-verified.
    #
    # The previous version silently logged a yellow Host message and
    # moved on, which meant a failed restore could leave the user's
    # CmdPal config in a half-mutated state with no loud signal.
    param([Parameter(Mandatory)][string]$BackupPath)
    if (-not (Test-Path $BackupPath)) {
        throw "Restore-CmdPalSettingsJson: backup file '$BackupPath' missing — cannot restore (was it already restored, or did Backup fail?)"
    }
    $ui = Get-Process Microsoft.CmdPal.UI -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($ui) {
        try { $ui.Kill(); $ui.WaitForExit(5000) | Out-Null }
        catch { Write-Warning "Restore-CmdPalSettingsJson: failed to kill CmdPal.UI PID $($ui.Id) cleanly: $($_.Exception.Message). Attempting restore anyway." }
    }
    # Compute expected hash from backup ONCE so we can verify at the end.
    $backupBytes = [System.IO.File]::ReadAllBytes($BackupPath)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $expectedHash = [BitConverter]::ToString($sha.ComputeHash($backupBytes)).Replace('-','')
    } finally { $sha.Dispose() }
    # Wait for file unlock + write (same logic as Edit-CmdPalSettingsAndRestart)
    $deadline = (Get-Date).AddSeconds(8)
    $written = $false
    $lastErr = $null
    do {
        try {
            [System.IO.File]::WriteAllBytes($cpSettings, $backupBytes)
            $written = $true
            break
        } catch {
            $lastErr = $_
            Start-Sleep -Milliseconds 300
        }
    } while ((Get-Date) -lt $deadline)
    if (-not $written) {
        throw "Restore-CmdPalSettingsJson: settings.json still locked after 8s — backup preserved at '$BackupPath'. Last error: $($lastErr.Exception.Message)"
    }
    # Verify byte-identical restore (length + SHA256). If this fails the
    # on-disk file is NOT what the user had before — keep the backup so
    # they can manually recover.
    $writtenBytes = [System.IO.File]::ReadAllBytes($cpSettings)
    if ($writtenBytes.Length -ne $backupBytes.Length) {
        throw "Restore-CmdPalSettingsJson: byte-length mismatch after restore (backup=$($backupBytes.Length), on-disk=$($writtenBytes.Length)). Backup preserved at '$BackupPath' for manual recovery."
    }
    $sha2 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $writtenHash = [BitConverter]::ToString($sha2.ComputeHash($writtenBytes)).Replace('-','')
    } finally { $sha2.Dispose() }
    if ($writtenHash -ne $expectedHash) {
        throw "Restore-CmdPalSettingsJson: SHA256 mismatch after restore (backup=$expectedHash, on-disk=$writtenHash). Backup preserved at '$BackupPath' for manual recovery."
    }
    # Verified byte-identical → safe to delete the backup.
    Remove-Item -LiteralPath $BackupPath -Force -ErrorAction SilentlyContinue
}

# Stop the AppX, transform settings.json, then start AppX. $Mutator gets
# the parsed JSON object and should mutate it in place. Returns the new HWND.
function Edit-CmdPalSettingsAndRestart {
    param(
        [Parameter(Mandatory)][scriptblock]$Mutator,
        [int]$WaitSec = 12,
        # Optional: capture the JSON text we wrote BEFORE AppX restarts and
        # potentially re-serializes the file. Pass a [ref]$var; the helper
        # populates it with the exact bytes that hit disk. Lets a test
        # verify mutator outputs (e.g. literal "DockSettings": null) that
        # would otherwise get overwritten on AppX startup.
        #
        # NOTE: default is a sentinel [ref] pointing at nothing — PowerShell
        # rejects $null as a value for a [ref]-typed parameter, so callers
        # who don't care can omit the parameter and the default ref is
        # written into but never read.
        [ref]$WrittenJson = ([ref]$null)
    )
    # 1. Stop AppX so the file isn't locked. Wait for the file handle to
    #    actually be released — process exit doesn't immediately unlock files.
    $ui = Get-Process Microsoft.CmdPal.UI -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($ui) {
        try { $ui.Kill(); $ui.WaitForExit(5000) | Out-Null }
        catch { Write-Warning "Edit-CmdPalSettingsAndRestart: failed to kill CmdPal.UI PID $($ui.Id) cleanly: $($_.Exception.Message). Attempting edit anyway." }
    }
    # 2. Poll file-availability via try-open-for-write. Allow other readers
    # (some background process may briefly read the file too); only require
    # that no one else is WRITING. If our own write succeeds in step 3 the
    # file is genuinely free.
    $fileFreeDeadline = (Get-Date).AddSeconds(10)
    $isFree = $false
    do {
        try {
            $fs = [System.IO.File]::Open($cpSettings, 'Open', 'ReadWrite', 'Read')
            $fs.Close(); $fs.Dispose()
            $isFree = $true; break
        } catch {
            Start-Sleep -Milliseconds 300
        }
    } while ((Get-Date) -lt $fileFreeDeadline)
    if (-not $isFree) { throw "settings.json still locked after AppX kill (10s timeout)" }

    # 3. Read, mutate, write — use [File]::* directly to bypass PowerShell
    # cmdlet restrictive share-mode handling.
    $jsonText = [System.IO.File]::ReadAllText($cpSettings)
    $j = $jsonText | ConvertFrom-Json
    & $Mutator $j
    $newJson = $j | ConvertTo-Json -Depth 20
    # Encoding: AppX writes UTF8 with no BOM. Mirror that.
    [System.IO.File]::WriteAllText($cpSettings, $newJson, (New-Object System.Text.UTF8Encoding($false)))
    # Only write to the ref if caller actually supplied one (the default
    # ([ref]$null) sentinel above has Value=$null and would harmlessly
    # accept a write, but distinguishing is clearer for future readers).
    if ($PSBoundParameters.ContainsKey('WrittenJson')) { $WrittenJson.Value = $newJson }

    # 4. Relaunch AppX
    Start-Process 'shell:AppsFolder\Microsoft.CommandPalette_8wekyb3d8bbwe!App' | Out-Null

    # 5. Wait for window. Avoid property access on $null pipeline result
    # under StrictMode — bind to a variable first, then conditionally read.
    $deadline = (Get-Date).AddSeconds($WaitSec)
    do {
        Start-Sleep -Milliseconds 500
        $win = winapp ui list-windows -a 'CmdPal' --json 2>$null | ConvertFrom-Json |
               Where-Object { $_.title -eq 'Command Palette' } | Select-Object -First 1
        $newHwnd = if ($win) { [int64]$win.hwnd } else { 0 }
        if ($newHwnd) {
            $script:cpHwnd = $newHwnd
            Start-Sleep -Milliseconds 800
            return $newHwnd
        }
    } while ((Get-Date) -lt $deadline)
    throw "CmdPal AppX did not come back online within ${WaitSec}s after settings edit"
}

# Restart the CmdPal AppX UI process (helper survives) and refresh
# $script:cpHwnd to the new window's handle. Returns the new HWND.
# No-op edit case — used when Cleanup just needs to reload original settings.
function Restart-CmdPalAppX {
    param([int]$WaitSec = 12)
    Edit-CmdPalSettingsAndRestart -Mutator { } -WaitSec $WaitSec
}

# ── Scope helpers (rev-4): wrap mutation/restore boilerplate ──────────
# Use-CmdPalMutableSettings: idiomatic "with-settings-mutation" block.
# Replaces the 6-line Backup/Edit/try { Body } finally { Restore + Restart }
# boilerplate that recurs across mutation tests.
#
#   Use-CmdPalMutableSettings -Mutate {
#       param($obj) $obj.Hotkey.code = 35
#   } -Body {
#       # Test assertions here. settings.json has been mutated and AppX
#       # restarted. On exit (success OR throw) settings.json is restored
#       # from a temp backup and AppX is restarted again.
#       Assert-Equal (Get-CmdPalSettings).Hotkey.code 35
#   }
function Use-CmdPalMutableSettings {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][scriptblock]$Mutate,
        [Parameter(Mandatory)][scriptblock]$Body
    )
    $backup = Backup-CmdPalSettingsJson
    Edit-CmdPalSettingsAndRestart -Mutator $Mutate | Out-Null
    try {
        & $Body
    } finally {
        if ($backup) { Restore-CmdPalSettingsJson -BackupPath $backup }
        try { Restart-CmdPalAppX | Out-Null }
        catch { Write-Warning "[Use-CmdPalMutableSettings cleanup] Restart-CmdPalAppX failed: $($_.Exception.Message)" }
    }
}

# Use-CmdPalClipboardSnapshot: snapshot clipboard, run body, restore.
# Replaces the 4-line $orig = Get-ClipboardSafe / try / Set-ClipboardSafe $orig
# pattern recurring in Calculator/Files/TimeDate tests.
#
#   Use-CmdPalClipboardSnapshot -Body {
#       # Test code that mutates clipboard; original restored after.
#   }
function Use-CmdPalClipboardSnapshot {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][scriptblock]$Body
    )
    $orig = Get-ClipboardSafe
    try {
        & $Body
    } finally {
        if ($null -ne $orig) { Set-ClipboardSafe $orig | Out-Null }
    }
}

# Resolves a dot-separated JSON path on a PSObject.
# Returns $null if any segment is missing.
function _ResolveJsonPath {
    param([Parameter(Mandatory)][object]$Obj, [Parameter(Mandatory)][string]$Path)
    $cur = $Obj
    foreach ($seg in $Path -split '\.') {
        if ($null -eq $cur) { return $null }
        if (-not $cur.PSObject.Properties.Name.Contains($seg)) { return $null }
        $cur = $cur.$seg
    }
    return $cur
}

# Reads JSON from a file using FileShare.ReadWrite so concurrent writes
# from CmdPal AppX aren't blocked by our read lock. Returns parsed
# PSObject, or $null on any error (caller polls).
function _ReadJsonShared {
    param([Parameter(Mandatory)][string]$Path)
    try {
        $fs = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
        try {
            $sr = New-Object System.IO.StreamReader($fs)
            try {
                $text = $sr.ReadToEnd()
            } finally { $sr.Dispose() }
        } finally { $fs.Dispose() }
        if ([string]::IsNullOrWhiteSpace($text)) { return $null }
        return $text | ConvertFrom-Json -ErrorAction Stop
    } catch { return $null }
}

# Public-API wrapper for CmdPal settings.json reads. Use this everywhere
# tests need the parsed settings.json instead of raw `Get-Content ... |
# ConvertFrom-Json` — the raw form opens the file with FileShare.Read,
# which hits a sharing-violation IOException if CmdPal AppX is mid-write
# (mutation tests + future parallelisation). _ReadJsonShared (called
# under the hood) opens with FileShare.ReadWrite to never block AppX.
#
# .PARAMETER Path
#   Defaults to $cpSettings (the orchestrator-scope CmdPal settings.json).
#   Pass an explicit path if you need to read a different settings.json
#   (e.g. a backup snapshot).
#
# .EXAMPLE
#   $obj = Get-CmdPalSettings
#   Assert-Equal $obj.DockSettings.ShowLabels $true
#
# .EXAMPLE
#   $snapshot = Get-CmdPalSettings -Path $backupPath
function Get-CmdPalSettings {
    [CmdletBinding()]
    param(
        [string]$Path
    )
    if ([string]::IsNullOrEmpty($Path)) { $Path = $cpSettings }
    return _ReadJsonShared -Path $Path
}

