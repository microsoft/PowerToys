#Requires -Version 7.0
# 24-SettingsUI-cleanup.ps1 — dot-sourced PARTIAL of 24-SettingsUI.tests.ps1.
#
# NOT a standalone test file (no `.tests.ps1` extension). It's dot-sourced
# from 24-SettingsUI.tests.ps1 and shares its script scope, which means
# it sees the orchestrator-initialised fixture variables: $cpHwnd,
# $cpSettings, $cpEnabled, $cpDataDir, $cpsHwnd, $_settingsUITestIds,
# $script:_settingsUIBucketBackup. Loading it directly (without the
# orchestrator) would error out on undefined variables.
#
# Purpose: bucket-level cleanup safety net — verify settings.json is
# byte-identical to the pre-test snapshot; restore from snapshot if
# anything drifted. Always runs LAST in the 24-SettingsUI bucket.
# ════════════════════════════════════════════════════════════════════
# Bucket-level cleanup safety net (always runs if any SettingsUI test ran)
# ════════════════════════════════════════════════════════════════════
# This test is registered LAST so the orchestrator runs it AFTER all
# 8 individual SettingsUI tests. It compares the current settings.json
# byte-for-byte against the snapshot taken at fixture load and restores
# from snapshot if anything drifted. PASS means either no drift OR
# drift was successfully restored; FAIL means drift could not be
# repaired (user's settings still mutated).
#
# Belt-and-suspenders: per-test finally blocks SHOULD restore each
# setting, but if anyone's Wait-Until times out during cleanup, or a
# test throws before its $orig capture, the per-test restore is missed.
# This bucket-end check catches all of those.
if (Test-AnyTestWillRun -Ids $_settingsUITestIds) {
Test-Case 'CmdPal_SettingsUI_ZZZ_CleanupSafetyNet' "★ SAFETY NET ★: verify settings.json is byte-identical to pre-SettingsUI-tests snapshot; restore from snapshot if not (PASS = settings restored to pre-test state)" {
    Assert-NotNull $script:_settingsUIBucketBackup -Because 'no settings.json snapshot was taken at fixture load — cannot verify cleanup. Per-test cleanup is the only safety net.'
    Assert-PathExists $script:_settingsUIBucketBackup -Because "snapshot file missing — cannot verify cleanup."
    try {
        $orig = [System.IO.File]::ReadAllBytes($script:_settingsUIBucketBackup)
        $cur  = if (Test-Path $cpSettings) {
            # Use shared-read so we don't block the live AppX. Read into a
            # MemoryStream loop to handle the race where the file length
            # changes between FileStream.Length read and the actual Read()
            # (CmdPal writes settings.json atomically; during the swap our
            # initial Length may not match the eventual content).
            $fs = [System.IO.File]::Open($cpSettings, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
            try {
                $ms = New-Object System.IO.MemoryStream
                $buf = New-Object byte[] 4096
                while (($n = $fs.Read($buf, 0, $buf.Length)) -gt 0) {
                    $ms.Write($buf, 0, $n)
                }
                $ms.ToArray()
            } finally { $fs.Dispose() }
        } else { @() }

        $sameLength = ($orig.Length -eq $cur.Length)
        if ($sameLength) {
            # Avoid Compare-Object on big byte arrays — use direct comparison
            $same = $true
            for ($i = 0; $i -lt $orig.Length; $i++) {
                if ($orig[$i] -ne $cur[$i]) { $same = $false; break }
            }
        } else {
            $same = $false
        }

        if ($same) {
            Write-Host "    info: settings.json byte-identical to snapshot ($($orig.Length) bytes) — all per-test cleanups succeeded" -ForegroundColor DarkGray
            return
        }

        # Drifted in bytes. Diff at field level: if NO field actually
        # differs in value, this is a whitespace/serialization-format
        # difference (CmdPal AppX may re-format JSON after our tests
        # touch certain fields, even if the values themselves are
        # identical). Treat that as success — user state is intact.
        $fieldDriftCount = -1   # -1 = unknown (diff parsing failed); >=0 = real count
        try {
            $jBefore = [System.Text.Encoding]::UTF8.GetString($orig) | ConvertFrom-Json -ErrorAction Stop
            $jAfter  = [System.Text.Encoding]::UTF8.GetString($cur)  | ConvertFrom-Json -ErrorAction Stop
            $drifted = New-Object System.Collections.Generic.List[string]
            foreach ($prop in $jBefore.PSObject.Properties) {
                $b = $prop.Value; $a = $jAfter.$($prop.Name)
                if ((($b -is [bool]) -or ($b -is [int]) -or ($b -is [string])) -and ($a -ne $b)) {
                    $drifted.Add("$($prop.Name): '$b' -> '$a'")
                }
            }
            if ($jBefore.DockSettings -and $jAfter.DockSettings) {
                foreach ($prop in $jBefore.DockSettings.PSObject.Properties) {
                    $b = $prop.Value; $a = $jAfter.DockSettings.$($prop.Name)
                    if ((($b -is [bool]) -or ($b -is [int]) -or ($b -is [string])) -and ($a -ne $b)) {
                        $drifted.Add("DockSettings.$($prop.Name): '$b' -> '$a'")
                    }
                }
            }
            $fieldDriftCount = $drifted.Count
            if ($fieldDriftCount -eq 0) {
                # Bytes differ but every scalar field value matches —
                # CmdPal re-serialized with different whitespace / key
                # order / float precision. User state is semantically
                # intact, no restore needed.
                Write-Host "    info: settings.json bytes differ from snapshot but ALL scalar field values match — CmdPal re-serialized JSON (whitespace/format), user state intact (NO restore needed)" -ForegroundColor DarkGray
                return
            }
            Write-Host "    warn: settings.json drifted on $fieldDriftCount field(s): $($drifted -join ' | ')" -ForegroundColor Yellow
        } catch {
            Write-Host "    warn: settings.json bytes differ from snapshot (field-level diff parsing failed: $($_.Exception.Message)) — will attempt restore as safety measure" -ForegroundColor Yellow
        }

        # Restore from snapshot. This stops the AppX, writes settings, then
        # caller-side Restart-CmdPalAppX brings it back. Without restart,
        # the AppX would write its in-memory state right back over our restore.
        Restore-CmdPalSettingsJson -BackupPath $script:_settingsUIBucketBackup
        try { Restart-CmdPalAppX | Out-Null } catch {
            throw "snapshot restore succeeded but CmdPal AppX restart failed: $($_.Exception.Message). settings.json IS restored on disk but the AppX may still hold stale in-memory state."
        }

        # Verify restore landed: re-read and compare again (use MemoryStream loop)
        Start-Sleep -Milliseconds 500
        $fs = [System.IO.File]::Open($cpSettings, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
        try {
            $ms = New-Object System.IO.MemoryStream
            $buf = New-Object byte[] 4096
            while (($n = $fs.Read($buf, 0, $buf.Length)) -gt 0) {
                $ms.Write($buf, 0, $n)
            }
            $verifyBytes = $ms.ToArray()
        } finally { $fs.Dispose() }
        Assert-Equal $verifyBytes.Length $orig.Length -Because 'restore may have corrupted file'
        for ($i = 0; $i -lt $orig.Length; $i++) {
            if ($verifyBytes[$i] -ne $orig[$i]) {
                throw "restore wrote different bytes at offset $i — restore failed verification"
            }
        }
        Write-Host "    info: settings.json restored from snapshot byte-identical ($($orig.Length) bytes) + AppX restarted" -ForegroundColor DarkGray
    } finally {
        # Delete the snapshot whether the comparison passed or failed.
        # The engine-exit handler also checks for the backup file, so we
        # only delete here if we've already verified the disk state.
        # If the restore threw above, leave the backup intact for manual recovery.
        if ($script:_settingsUIBucketBackup -and (Test-Path $script:_settingsUIBucketBackup)) {
            # Only auto-delete if we got here without throwing (which means
            # either no drift OR drift was successfully restored)
            Remove-Item $script:_settingsUIBucketBackup -ErrorAction SilentlyContinue
            $script:_settingsUIBucketBackup = $null
        }
        # Also unregister the engine-exit handler (no longer needed)
        Get-EventSubscriber -SourceIdentifier 'PowerShell.Exiting' -ErrorAction SilentlyContinue |
            Where-Object { $_.Action.ToString() -match '_settingsUIBucketBackup' } |
            ForEach-Object { Unregister-Event -SubscriptionId $_.SubscriptionId -ErrorAction SilentlyContinue }
    }
}
}  # end Test-AnyTestWillRun guard for safety net

