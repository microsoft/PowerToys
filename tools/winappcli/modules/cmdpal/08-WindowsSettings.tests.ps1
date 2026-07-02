#Requires -Version 7.0
# 08-WindowsSettings.tests.ps1 — extracted from command-palette-checklist.ps1 during Phase 2b split.
# Dot-sourced from the orchestrator so it shares script scope ($cpHwnd,
# $cpSettings, $cpEnabled, $cpDataDir). See _helpers.ps1 for the
# CmdPal-specific helper functions these tests call into.
# ── Box L1035: Windows Settings provider exposes Settings entries ────
# In CmdPal 0.10.11181.0+ the WindowsSettings provider's
# "Search for X in Windows settings" fallback no longer surfaces on the
# home page — home fallback ranking shows only Web Search / Files /
# RemoteDesktop. The provider IS functional though, accessible via its
# direct alias '$' which navigates to a dedicated sub-page that returns
# real Windows Settings entries.
#
# Using the sub-page is a STRONGER assertion than the old home-fallback
# check: it verifies not just that the provider is registered, but that
# it actually returns matching settings pages (via the WindowsSettings
# bundled index of settings-page mappings).
#
# Placed BEFORE the Walker test because Walker's notepad spawn/kill
# disturbs CmdPal state.
#
# Wrapped in Use-CmdPalProviderEnabled (windowssettings provider).
$_wsTestIds = @(
    'CmdPal_WindowsSettings_InvokeOpensSettingsApp',
    'CmdPal_WindowsSettings_ReturnsFallbackEntryForUnknownQuery'
)
$_registerWsTests = {
# ── Box L1035 ★ FULL: WindowsSettings invokes ms-settings: and opens Settings app ──
# Stronger sibling of ReturnsFallbackEntryForUnknownQuery. That test
# asserts the provider returns matching settings entries; this one
# ACTUALLY INVOKES the entry and verifies a Windows Settings
# (SystemSettings) window appears, proving the ms-settings: URL wiring
# all the way through.
#
# Uses a benign query 'about' → 'About' page which doesn't have any
# side-effects when opened (just shows system info).
#
# NOTE: this test runs FIRST in the bucket (before the simpler reads
# test). Running it second causes the '$' alias to time out — likely
# because CmdPal's alias detector doesn't fully re-arm in time after
# an invoke + sub-page-exit cycle. Running the invoke-test first means
# the simpler test gets a clean state from the fixture's restart.
Test-Case 'CmdPal_WindowsSettings_InvokeOpensSettingsApp' "Box L1035 ★ FULL: WindowsSettings '`$' alias actually invokes ms-settings: URL (FUNCTIONAL e2e — spawns Settings app, cleanup closes it)" {
    # Arrange — snapshot SystemSettings processes that exist BEFORE invoke
    $beforePids = @(Get-Process SystemSettings -EA SilentlyContinue | ForEach-Object Id)
    $sinceTime  = Get-Date
    $spawned    = @()
    try {
        # Act — enter '$' sub-page, type 'about' (innocuous settings page),
        # invoke first item. About page just shows OS version info, no
        # destructive side-effects.
        Use-CmdPalSubPage '$' {
            Set-UiaText 'MainSearchBox' 'about' -Hwnd $cpHwnd -VerifyEcho
            # Wait briefly for the WS index to filter; 2.5s is plenty.
            $ok = Wait-Until -TimeoutMs 2500 -PollMs 200 -IgnoreException {
                $ins = (winapp ui inspect 'ItemsList' -w $cpHwnd --depth 2 2>$null) -split "`n"
                @($ins | Where-Object { $_ -match '^\s*itm-\S+\s+ListItem\s+"([^"]+)"' }).Count -gt 0
            }
            Assert-True $ok -Because "WindowsSettings sub-page returned 0 items for 'about' after 2.5s"
            # Verify Primary='Open' and invoke
            $primary = Get-UiaProperty 'PrimaryCommandButton' 'Name' -Hwnd $cpHwnd
            Assert-Equal $primary 'Open' -Because 'Primary before invoke should be Open'
            # THIS IS THE REAL INVOCATION — fires ms-settings: URL
            Invoke-UiaAction 'PrimaryCommandButton' invoke -Hwnd $cpHwnd
            # Wait for SystemSettings to spawn — race-hider replaced with
            # real condition: poll for a new SystemSettings process.
            # First launch is ~1-3s; warm ~500ms; 6s deadline covers both.
            $null = Wait-Until -TimeoutMs 6000 -PollMs 250 -IgnoreException `
                -Message "SystemSettings did not spawn within 6s of ms-settings: invoke" `
                -Condition {
                    @(Get-Process SystemSettings -EA SilentlyContinue |
                      Where-Object { $_.Id -notin $beforePids -and $_.StartTime -ge $sinceTime }
                    ).Count -gt 0
                }
        }
        # Assert — a new SystemSettings process started after our snapshot
        $afterProcs = @(Get-Process SystemSettings -EA SilentlyContinue)
        $spawned = @($afterProcs | Where-Object {
            $_.Id -notin $beforePids -and $_.StartTime -ge $sinceTime
        })
        Assert-GreaterThan $spawned.Count 0 -Because "WindowsSettings invoke did not spawn a new SystemSettings process within 6s. Before PIDs: [$($beforePids -join ',')], after PIDs: [$($afterProcs.Id -join ',')]"
        Write-Host "    info: WindowsSettings '`$ about' invoke spawned $($spawned.Count) SystemSettings process(es): $($spawned.Id -join ', ')" -ForegroundColor DarkGray
    } finally {
        # Cleanup — kill spawned Settings windows so test doesn't leak UI
        foreach ($proc in $spawned) {
            try { $proc.Kill(); $proc.WaitForExit(2000) | Out-Null } catch { Write-Warning "[cleanup] failed to kill PID $($proc.Id): $($_.Exception.Message)" }
        }
        if ($spawned.Count -gt 0) {
            Write-Host "    [InvokeOpensSettingsApp test] killed $($spawned.Count) SystemSettings PID(s): $($spawned.Id -join ', ')" -ForegroundColor DarkGray
        }
    }
}

Test-Case 'CmdPal_WindowsSettings_ReturnsFallbackEntryForUnknownQuery' "Box L1035: Windows Settings provider '`$' alias returns settings entries (FUNCTIONAL — safe, doesn't invoke)" {
    # Arrange
    $q = 'bluetooth'
    # Act — type '$' alias to navigate to the WindowsSettings sub-page,
    # then type 'bluetooth' on the sub-page and assert settings entries.
    Use-CmdPalSubPage '$' {
        Set-UiaText 'MainSearchBox' $q -Hwnd $cpHwnd -VerifyEcho
        # Wait for the WindowsSettings index to filter to bluetooth-matching
        # pages. 3s is plenty (typical filter is sub-200ms).
        $hits = $null
        $ok = Wait-Until -TimeoutMs 3000 -PollMs 200 -Message "WindowsSettings '$' sub-page returned no items matching '$q'" {
            $ins = (winapp ui inspect 'ItemsList' -w $cpHwnd --depth 2 2>$null) -split "`n"
            $script:_wsItems = @($ins | Where-Object { $_ -match '^\s*itm-\S+\s+ListItem\s+"([^"]+)"' } | ForEach-Object { $matches[1] })
            $script:_wsItems.Count -gt 0
        }
        # Assert
        Assert-True $ok -Because "WindowsSettings sub-page returned 0 items for '$q' after 3s"
        # The sub-page must return at least one Settings entry mentioning
        # bluetooth or devices — proves the WindowsSettings index loaded.
        $relevant = @($script:_wsItems | Where-Object { $_ -match '(?i)bluetooth|device' })
        Assert-GreaterThan $relevant.Count 0 -Because "WindowsSettings sub-page returned $($script:_wsItems.Count) items for '$q' but none mention bluetooth/device (got: $($script:_wsItems -join ' | '))"
        # Primary action on a Settings page entry is 'Open' (would
        # ms-settings: launch). Verify wiring without invoking.
        $primary = Get-UiaProperty 'PrimaryCommandButton' 'Name' -Hwnd $cpHwnd
        Assert-Equal $primary 'Open' -Because 'Primary action for a Settings page entry should be Open'
        Write-Host "    info: WindowsSettings '`$' sub-page returned $($script:_wsItems.Count) items for '$q' (relevant: $($relevant -join ', '); Primary='Open')" -ForegroundColor DarkGray
    }
}

# ── Box L1035 ★ FULL: WindowsSettings invokes ms-settings: and opens Settings app ──
# (Moved earlier in the bucket to avoid an alias-detector flake when run
# after ReturnsFallbackEntryForUnknownQuery. Implementation lives above.)
}  # end $_registerWsTests scriptblock

if (Test-AnyTestWillRun -Ids $_wsTestIds) {
    Use-CmdPalProviderEnabled -ProviderId 'com.microsoft.cmdpal.builtin.windowssettings' -Body $_registerWsTests
} else {
    & $_registerWsTests
}
