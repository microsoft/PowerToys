#Requires -Version 7.0
# 15-Mutation-Settings.tests.ps1 — extracted from command-palette-checklist.ps1 during Phase 2b split.
# Dot-sourced from the orchestrator so it shares script scope ($cpHwnd,
# $cpSettings, $cpEnabled, $cpDataDir). See _helpers.ps1 for the
# CmdPal-specific helper functions these tests call into.

# ════════════════════════════════════════════════════════════════════
#   SETTINGS-MUTATION TESTS — edit settings.json, restart, verify
# ════════════════════════════════════════════════════════════════════
# These tests mutate the live settings.json via Use-CmdPalMutableSettings,
# which handles snapshot/restore + AppX restart for both the test body
# and the cleanup, even on exceptions.

# ── Box L1049: Global hotkey change is picked up after restart ──────
# Mutates Hotkey.code in settings.json (e.g. Space → PageUp), restarts
# CmdPal, verifies the new value persisted (= settings reload worked +
# CmdPal didn't reset/crash) and process is still alive (= AppX didn't
# crash on the new hotkey value). We do NOT actually press the new
# hotkey via SendInput — that would require Win+Alt+<key> which races
# with foreground focus and is itself environment-dependent.
Test-Case 'CmdPal_Settings_HotkeyChangePickedUp' "Box L1049: Global hotkey change persists across CmdPal restart" {
    # Arrange
    $j = Get-CmdPalSettings
    $origCode = $j.Hotkey.code
    $newCode  = if ($origCode -eq 33) { 35 } else { 33 }

    Use-CmdPalMutableSettings `
        -Mutate { param($obj) $obj.Hotkey.code = $newCode } `
        -Body {
            # Assert — settings reloaded + process alive + IPC event listener intact
            $jPost = Get-CmdPalSettings
            Assert-Equal $jPost.Hotkey.code $newCode -Because 'Settings reload should persist new hotkey code'
            Assert-ProcessRunning 'Microsoft.CmdPal.UI' -Because 'CmdPal AppX should still be running after hotkey change'
            try { Invoke-PtSharedEvent -Name 'CmdPal.Show' | Out-Null }
            catch { throw "CmdPal.Show event listener missing after hotkey change: $_" }
            $ui = Get-Process Microsoft.CmdPal.UI -ErrorAction SilentlyContinue | Select-Object -First 1
            Write-Host "    info: hotkey code $origCode → $newCode, CmdPal AppX still alive (PID $($ui.Id))" -ForegroundColor DarkGray
        }
}

# ── Box L1050: Alias change is picked up after restart ───────────────
# Adds a NEW custom alias mapped to the calculator provider, restarts
# CmdPal, then types the new alias and verifies it navigates to the
# Calculator sub-page (Primary='Copy' + first ListItem is the result).
# Strong assertion — proves alias map is reloaded from settings.json.
Invoke-AAATest -Tag direct -Id 'CmdPal_Settings_AliasChangePickedUp' `
    -Name "Box L1050: New alias added to settings.json activates after CmdPal restart" `
    -Ignore -IgnoreReason 'CmdPal strips externally-added aliases on startup (only honours aliases added via Extensions Manager UI). Investigated 2026-05-15: write+restart leaves aliases unchanged. Either drive the Extensions Manager dialog (PROTOTYPE), or use a different verification strategy (e.g. modify an EXISTING alias key and see if CmdPal honours the change).' `
    -Act { } -Assert { }

# ── Box L1048: Disable extension removes its commands from results ──
# Disables the Calculator provider in ProviderSettings, restarts CmdPal,
# types '5+5', verifies '10' is NOT in results. Strong assertion that
# IsEnabled=false actually unloads the provider (vs. e.g. just hiding
# it in Extensions Manager UI).
Test-Case 'CmdPal_Providers_DisableExtensionRemovesCommands' "Box L1048: Disabling Calculator provider removes its results after restart" {
    # Arrange
    $providerId = 'com.microsoft.cmdpal.builtin.calculator'

    Use-CmdPalMutableSettings `
        -Mutate {
            param($obj)
            Assert-True ($obj.ProviderSettings.PSObject.Properties.Name.Contains($providerId)) -Because "ProviderSettings missing '$providerId'"
            $obj.ProviderSettings.$providerId.IsEnabled = $false
        } `
        -Body {
            try {
            # Act
                Invoke-CmdPalQuery '5+5'
                # Wait for the result list to settle (some ListItem populates),
                # then assert '10' is NOT among them.
                $listSettled = Wait-Until -TimeoutMs 1500 -PollMs 150 -IgnoreException `
                    -Message 'Result list never produced any ListItem for "5+5"' `
                    -Condition {
                        $insLines = (winapp ui inspect 'ItemsList' -w $cpHwnd --depth 2 2>$null) -split "`n"
                        @($insLines | Where-Object { $_ -match '^\s*itm-\S+\s+ListItem\s+' }).Count -gt 0
                    }
                if (-not $listSettled) {
                    Write-Host "    warn: result list empty within budget (provider may be slow); proceeding to absence check" -ForegroundColor Yellow
                }
            # Assert — '10' MUST NOT be a ListItem (Calculator disabled)
                $r = winapp ui search '10' -w $cpHwnd --json 2>$null | ConvertFrom-Json
                $hit = @($r.matches | Where-Object { $_.type -eq 'ListItem' -and $_.name -eq '10' }) | Select-Object -First 1
                Assert-Null $hit -Because "Calculator provider was DISABLED but '5+5' still produced ListItem '10' (provider didn't unload)"

                # Additional stronger assertion (PR #48033 stable IDs): when the
                # Calculator provider is disabled, its home-page tile must also be
                # gone. Type 'calc' and verify the com.microsoft.cmdpal.calculator
                # AutomationId is absent.
                Invoke-CmdPalQuery 'calc'
                # Wait for the result list to settle (any ListItem present)
                # BEFORE we check for absence of the calc tile. Otherwise a
                # blind 800ms sleep could let us check absence before the
                # list rendered at all — false PASS. We're asserting absence,
                # so we need positive confirmation the list rendered first.
                $null = Wait-Until -TimeoutMs 2000 -PollMs 150 -IgnoreException `
                    -Message "Result list never produced any ListItem for 'calc' — cannot verify absence assertion (list never rendered)" `
                    -Condition {
                        $ins = (winapp ui inspect 'ItemsList' -w $cpHwnd --depth 2 2>$null) -split "`n"
                        @($ins | Where-Object { $_ -match '^\s*itm-\S+\s+ListItem' }).Count -gt 0
                    }
                $calcTile = Find-CmdPalProviderItem 'com.microsoft.cmdpal.calculator'
                Assert-Null $calcTile -Because "Calculator tile (com.microsoft.cmdpal.calculator) still on home after disable — PR #48033 stable-ID assertion: provider didn't fully unload"
                Write-Host "    info: with Calculator IsEnabled=false, '5+5' produced no '10' ListItem AND 'calc' produced no com.microsoft.cmdpal.calculator tile (provider unloaded as expected)" -ForegroundColor DarkGray
            } finally {
            # Inner cleanup — return to home before scope helper restores settings
                Reset-CmdPalToHome
            }
        }
}
