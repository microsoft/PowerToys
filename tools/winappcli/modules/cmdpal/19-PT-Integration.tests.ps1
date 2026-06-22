#Requires -Version 7.0
# 19-PT-Integration.tests.ps1 — extracted from command-palette-checklist.ps1 during Phase 2b split.
# Dot-sourced from the orchestrator so it shares script scope ($cpHwnd,
# $cpSettings, $cpEnabled, $cpDataDir). See _helpers.ps1 for the
# CmdPal-specific helper functions these tests call into.
# ── ★ 0.97-0.99 GAP-FILL Round 2 — small-effort additions ───────────

# ── 0.97-0.99 PR #46198 — PowerToys provider exposes FancyZones layouts ──
# The built-in PowerToys provider (Microsoft.PowerToys.SparseApp_*)
# exposes FancyZones layout commands so users can pin individual
# layouts to the dock. Verify by searching for "FancyZones" and
# asserting at least one result is FancyZones-related.
Test-Case 'CmdPal_PowerToysExtension_FancyZonesLayoutsListedViaSearch' "★ 0.97-0.99: PR #46198 — PowerToys provider exposes FancyZones via search" {
    try {
    # Act
        try {
            Invoke-CmdPalQuery -Query 'FancyZones'
        } catch {
            throw "could not echo 'FancyZones' query: $($_.Exception.Message)"
        }
        # Wait for one of the expected items to appear (race-aware presence check).
        $required = @('FancyZones','Open FancyZones Editor')
        $null = Wait-Until -TimeoutMs 3000 -PollMs 200 -IgnoreException `
            -Message "PowerToys provider did not return any of [$($required -join ', ')] for query 'FancyZones' within 3s" `
            -Condition {
                $ins = (winapp ui inspect 'ItemsList' -w $cpHwnd --depth 2 2>$null) -split "`n"
                $n = @($ins | Where-Object { $_ -match '^\s*itm-\S+\s+ListItem\s+"([^"]+)"' } |
                       ForEach-Object { if ($_ -match 'ListItem\s+"([^"]+)"') { $matches[1] } })
                @($n | Where-Object { $_ -in $required }).Count -gt 0
            }
        # Re-fetch names for the info log (Wait-Until can't reliably return arrays).
        $ins = (winapp ui inspect 'ItemsList' -w $cpHwnd --depth 2 2>$null) -split "`n"
        $names = @($ins | Where-Object { $_ -match '^\s*itm-\S+\s+ListItem\s+"([^"]+)"' } |
                   ForEach-Object { if ($_ -match 'ListItem\s+"([^"]+)"') { $matches[1] } })
    # Assert
        $hits = @($names | Where-Object { $_ -in $required })
        Write-Host "    info: PowerToys provider returned $($hits.Count) FancyZones entries: $($hits -join ', ')" -ForegroundColor DarkGray
    } finally {
    # Cleanup
        try {
            if ($cpHwnd) { winapp ui set-value 'MainSearchBox' '' -w $cpHwnd 2>$null | Out-Null }
            Reset-CmdPalToHome
        } catch { Write-Warning "[cleanup] $($_.Exception.Message)" }
    }
}

# ── 0.97.0 — PowerToys provider exposes Color Picker via search ─────
# The 0.97 PowerToys provider exposed multiple PT utilities as searchable
# commands. Verify the Color Picker family is reachable.
Test-Case 'CmdPal_PowerToysExtension_ColorPickerListedViaSearch' "★ 0.97.0: PowerToys provider exposes Color Picker via search" {
    try {
    # Act
        try {
            Invoke-CmdPalQuery -Query 'color picker'
        } catch {
            throw "could not echo 'color picker' query: $($_.Exception.Message)"
        }
        # Wait for one of the expected items to appear (race-aware presence check).
        $required = @('Color Picker','Open Color Picker')
        $null = Wait-Until -TimeoutMs 3000 -PollMs 200 -IgnoreException `
            -Message "PowerToys provider did not return any of [$($required -join ', ')] for query 'color picker' within 3s" `
            -Condition {
                $ins = (winapp ui inspect 'ItemsList' -w $cpHwnd --depth 2 2>$null) -split "`n"
                $n = @($ins | Where-Object { $_ -match '^\s*itm-\S+\s+ListItem\s+"([^"]+)"' } |
                       ForEach-Object { if ($_ -match 'ListItem\s+"([^"]+)"') { $matches[1] } })
                @($n | Where-Object { $_ -in $required }).Count -gt 0
            }
        # Re-fetch names for the info log.
        $ins = (winapp ui inspect 'ItemsList' -w $cpHwnd --depth 2 2>$null) -split "`n"
        $names = @($ins | Where-Object { $_ -match '^\s*itm-\S+\s+ListItem\s+"([^"]+)"' } |
                   ForEach-Object { if ($_ -match 'ListItem\s+"([^"]+)"') { $matches[1] } })
    # Assert
        $hits = @($names | Where-Object { $_ -in $required })
        Write-Host "    info: PowerToys provider returned $($hits.Count) Color Picker entries: $($hits -join ', ')" -ForegroundColor DarkGray
    } finally {
    # Cleanup
        try {
            if ($cpHwnd) { winapp ui set-value 'MainSearchBox' '' -w $cpHwnd 2>$null | Out-Null }
            Reset-CmdPalToHome
        } catch { Write-Warning "[cleanup] $($_.Exception.Message)" }
    }
}
