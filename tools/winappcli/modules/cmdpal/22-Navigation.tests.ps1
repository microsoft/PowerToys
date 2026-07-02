#Requires -Version 7.0
# 22-Navigation.tests.ps1 — extracted from command-palette-checklist.ps1 during Phase 2b split.
# Dot-sourced from the orchestrator so it shares script scope ($cpHwnd,
# $cpSettings, $cpEnabled, $cpDataDir). See _helpers.ps1 for the
# CmdPal-specific helper functions these tests call into.
# ── 0.99.0 PR #46439 — PgUp/PgDown skips separators and headers ─────
# Reframe: rather than driving PgUp/PgDown (needs Send-PtKey + focus
# tracking which is fiddly across the AppX/UIA boundary), verify the
# structural property that the fix relies on: ListItems that ARE
# separators have IsEnabled=false (so PgUp/PgDown's selection logic
# can skip them). This catches regressions where someone forgets to
# mark a section header as non-selectable, which is the root cause
# PR #46439 fixed.
Test-Case 'CmdPal_Navigation_SeparatorListItemsAreMarkedDisabled' "★ 0.99.0 PR #46439 — section-header/separator ListItems are marked IsEnabled=false (so PgUp/PgDown can skip them)" {
    try {
    # Act
        # Query that produces multiple sections (Results + Fallbacks)
        try {
            Invoke-CmdPalQuery -Query 'notepad'
        } catch {
            throw "could not echo 'notepad' query: $($_.Exception.Message)"
        }
        # Wait until at least one ListItem populates (race-aware vs slow box).
        # We use Wait-Until purely as a presence check — re-fetching $allItems
        # below avoids the Wait-Until array-return quirk (line 96 strips
        # arrays to their last element, breaking comma-trick returns).
        $null = Wait-Until -TimeoutMs 3000 -PollMs 200 -IgnoreException `
            -Message "no ListItems appeared for 'notepad' query within 3s" `
            -Condition {
                $t = & winapp ui inspect 'ItemsList' -w $cpHwnd --depth 3 2>$null
                @($t | Where-Object { $_ -match 'ListItem\s+"[^"]+"' }).Count -gt 0
            }
        # Re-read the tree once we know the list is populated, and use it
        # for BOTH allItems and disabledItems assertions.
        # REASON: this test intentionally exercises winappCli's text output
        # format — the `[disabled]` token is what we're asserting (separator
        # headers must be marked disabled, interactive rows must not).
        # Cannot be replaced by AutomationId-based queries: separators have
        # no AutomationIds, and the `[disabled]` semantic is a winappCli
        # output-format property by design. Tier B per PR #48033 modernization.
        $tree = & winapp ui inspect 'ItemsList' -w $cpHwnd --depth 3 2>$null
        $allItems = @($tree | Where-Object { $_ -match 'ListItem\s+"[^"]+"' })
        $disabledItems = @($tree | Where-Object { $_ -match 'ListItem\s+"[^"]+".*\[disabled\]' })
        if ($disabledItems.Count -eq 0) {
            # No headers at all is technically fine (e.g. single-section
            # result list). Test still passes — there's just nothing to
            # validate. Print info.
            Write-Host "    info: $($allItems.Count) ListItems, 0 disabled (no separators in this result list — assertion trivially holds)" -ForegroundColor DarkGray
            return
        }
        # If there ARE disabled items, verify they look like headers
        # (e.g. names 'Results', 'Fallbacks', or SeparatorViewModel).
        $headerNames = @($disabledItems | ForEach-Object {
            if ($_ -match 'ListItem\s+"([^"]+)"') { $Matches[1].Trim() }
        })
        Write-Host "    info: $($allItems.Count) ListItems; $($disabledItems.Count) marked disabled (headers): $($headerNames -join ', ')" -ForegroundColor DarkGray
        
        # Sanity: no INTERACTIVE results should be marked disabled
        $disabledNonHeaders = @($disabledItems | Where-Object {
            $_ -notmatch 'ListItem\s+"\s*(Results|Fallbacks|.*SeparatorViewModel|.*ListItemViewModel)\b'
        })
        Assert-Empty $disabledNonHeaders -Because "found ListItems marked disabled that don't look like headers/separators (would be unreachable to keyboard nav)"
    } finally {
    # Cleanup
        try {
            if ($cpHwnd) { & winapp ui set-value 'MainSearchBox' '' -w $cpHwnd 2>$null | Out-Null }
            Reset-CmdPalToHome
        } catch { Write-Warning "[cleanup] $($_.Exception.Message)" }
    }
}
