#Requires -Version 7.0
# 16-Mutation-Dock.tests.ps1 — extracted from command-palette-checklist.ps1 during Phase 2b split.
# Dot-sourced from the orchestrator so it shares script scope ($cpHwnd,
# $cpSettings, $cpEnabled, $cpDataDir). See _helpers.ps1 for the
# CmdPal-specific helper functions these tests call into.
# ════════════════════════════════════════════════════════════════════
# ★ 0.96 → 0.99 GAP-FILL — tests added to track 3 releases of new features
# ════════════════════════════════════════════════════════════════════
# See the release notes for v0.97.0, v0.99.0, v0.99.1 — these tests
# verify schema/persistence of features introduced after the original
# checklist was authored. All pure JSON (no UI driving) for fast,
# reliable signal. Reference PRs cited per test.

# ── 0.99.1 PR #47296 — DockSettings null-deserialization crash fix ──
# Regression guard: if anything ever writes literal `"DockSettings": null`
# to settings.json, CmdPal must NOT crash on startup. Simulate the bad
# state, restart CmdPal, verify the process is still alive.
Test-Case 'CmdPal_DockSchema_NullDockSettingsDoesNotCrashOnStartup' "★ 0.99.1: PR #47296 — null DockSettings in settings.json does not crash CmdPal on startup" {
    # Arrange
            $backup = Backup-CmdPalSettingsJson
    # Capture the JSON we wrote BEFORE the AppX restart had a chance to
    # re-serialize it. Without -WrittenJson the AppX startup overwrites
    # "DockSettings": null with its in-memory default — making the disk
    # verification below silently pass on the wrong content. Now we
    # verify the EXACT bytes we wrote.
    $writtenJson = $null
    Edit-CmdPalSettingsAndRestart -Mutator {
    param($obj)
    # Force-write null over the existing DockSettings object.
    $obj.DockSettings = $null
    } -WrittenJson ([ref]$writtenJson) | Out-Null
    try {
    # Sanity: confirm we actually wrote `"DockSettings": null` to disk.
    # Without this, a PowerShell quirk (e.g. PSCustomObject property
    # serialization stripping null) could make the test silently
    # exercise a different mutation than intended — the crash-guard
    # below would pass even though we never reproduced the regression.
        Assert-Match $writtenJson '"DockSettings"\s*:\s*null' -Because 'mutator output must contain literal "DockSettings": null — if not, the ConvertTo-Json round-trip stripped the null and we did not actually reproduce the #47296 condition'

    # Act — assert CmdPal.UI stays alive for the full observation window
    # (regression #47296 = crash on startup with null DockSettings).
    # Use Wait-StaysTrue (NOT Start-Sleep + single check): blind sleep is
    # semantically WRONG on slow boxes because a longer sleep just grows
    # the crash window and makes false PASSes more likely. Wait-StaysTrue
    # polls every 200ms over the entire window and fails the INSTANT the
    # process disappears. 2s budget is scaled by WINAPPCLI_SLOW_FACTOR.
        Wait-StaysTrue -DurationMs 2000 -PollMs 200 `
            -Message 'Microsoft.CmdPal.UI process exited after null DockSettings restart — REGRESSION of #47296' `
            -Condition { [bool](Get-Process Microsoft.CmdPal.UI -ErrorAction SilentlyContinue) } | Out-Null
        $procs = Get-Process Microsoft.CmdPal.UI -ErrorAction SilentlyContinue
        Write-Host "    info: CmdPal survived null DockSettings (verified literal null was written to disk); PID(s) $($procs.Id -join ',')" -ForegroundColor DarkGray
    } finally {
    # Cleanup
        if ($backup) { Restore-CmdPalSettingsJson -BackupPath $backup }
                    try { Restart-CmdPalAppX | Out-Null } catch { Write-Warning "[cleanup] Restart-CmdPalAppX failed: $($_.Exception.Message)" }
    }
}

# ── 0.99.1 PR #47317 — dock label persistence ───────────────────────
# Toggle DockSettings.ShowLabels false→true→false across restarts.
# Each state must round-trip exactly. Catches the regression where
# the field was being silently dropped on save.
Test-Case 'CmdPal_DockSchema_ShowLabelsPersistsAcrossSessions' "★ 0.99.1: PR #47317 — DockSettings.ShowLabels round-trips across CmdPal restart" {
    # Arrange
            $backup = Backup-CmdPalSettingsJson
    $obj = Get-CmdPalSettings
    $orig = if ($null -ne $obj.DockSettings.ShowLabels) { [bool]$obj.DockSettings.ShowLabels } else { $true }
    $target = -not $orig
    Edit-CmdPalSettingsAndRestart -Mutator {
    param($o)
    $o.DockSettings.ShowLabels = $target
    } | Out-Null
    try {
    # Act
        # After Edit-CmdPalSettingsAndRestart returns, the new AppX may
        # still be in the middle of writing its in-memory settings back
        # to disk. Wait until settings.json on disk actually reflects our
        # written value (== $target), instead of a blind 1s sleep that
        # under-waited on slow boxes (intermittent false FAIL) and
        # over-waited on fast ones.
        $actual = $null
        $null = Wait-Until -TimeoutMs 5000 -PollMs 200 -IgnoreException `
            -Message "settings.json on disk did not reflect DockSettings.ShowLabels=$target within 5s after AppX restart — write may have been clobbered by AppX startup re-serialization" `
            -Condition {
                # Get-CmdPalSettings uses FileShare.ReadWrite under the hood
                # so it never blocks AppX mid-rewrite; returns $null on any
                # parse error (we just retry).
                $obj = Get-CmdPalSettings
                if ($null -eq $obj) { return $null }
                $script:_dockShowLabelsActual = [bool]$obj.DockSettings.ShowLabels
                if ($script:_dockShowLabelsActual -eq $target) { return $true }
                $null
            }
        $actual = $script:_dockShowLabelsActual
        Remove-Variable -Scope Script -Name '_dockShowLabelsActual' -ErrorAction SilentlyContinue
        Assert-Equal $actual $target -Because 'DockSettings.ShowLabels after restart'
        Write-Host "    info: ShowLabels round-tripped: $($orig) → $($target) → $actual" -ForegroundColor DarkGray
    } finally {
    # Cleanup
        if ($backup) { Restore-CmdPalSettingsJson -BackupPath $backup }
                    try { Restart-CmdPalAppX | Out-Null } catch { Write-Warning "[cleanup] Restart-CmdPalAppX failed: $($_.Exception.Message)" }
    }
}
