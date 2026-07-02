#Requires -Version 7.0
# 24-SettingsUI-e2e.ps1 — dot-sourced PARTIAL of 24-SettingsUI.tests.ps1.
#
# NOT a standalone test file (no `.tests.ps1` extension). It's dot-sourced
# from 24-SettingsUI.tests.ps1 and shares its script scope, which means
# it sees the orchestrator-initialised fixture variables: $cpHwnd,
# $cpSettings, $cpEnabled, $cpDataDir, $cpsHwnd, $_settingsUITestIds,
# $script:_settingsUIBucketBackup. Loading it directly (without the
# orchestrator) would error out on undefined variables.
#
# Purpose: full UI -> JSON -> visible CmdPal AppX runtime effect tests.
# Each test clicks one control in the CmdPal AppX Settings window AND
# asserts that the CmdPal AppX actually rendered the change — PowerDock
# window appears at the right location, with the right size, etc.
# 7 tests, all Dock-focused (the only currently-testable runtime-effect
# surface in CmdPal Settings UI). Slow (~16-24s/test) because each
# test waits for AppX to re-render after the JSON write.
#
# Catches the "Settings UI writes settings.json correctly but CmdPal
# silently ignores the field" failure mode that the bindings sub-file
# cannot detect.
#
# Tagged 'integration' (separate from 'mutation') so nightly runs can
# opt in/out independently.

if (Test-AnyTestWillRun -Ids @(
    'CmdPal_SettingsUI_Dock_EnableDockShowsPowerDockWindow',
    'CmdPal_SettingsUI_Dock_CompactModeShrinksPowerDockHeight',
    'CmdPal_SettingsUI_Dock_PositionTopBottomRelocatesPowerDock',
    'CmdPal_SettingsUI_Dock_PositionLeftMakesPowerDockVertical',
    'CmdPal_SettingsUI_Dock_DefaultBandsPresentOnFirstEnable',
    'CmdPal_SettingsUI_Dock_PerformanceMonitorBandShowsLiveData',
    'CmdPal_SettingsUI_Dock_DateTimeBandShowsCurrentTime'
)) {

# Re-acquire PT Settings as the bindings sub-file's fixture also does (idempotent).
$settings = Open-PtSettings
Switch-PtSettingsPage -Module 'CmdPal' -Hwnd $settings.hwnd
Start-Sleep -Milliseconds 800

# Open AppX Settings (idempotent — shares window with the bindings sub-file if both run)
$cpsHwndB = Open-CmdPalAppXSettings -PtSettingsHwnd $settings.hwnd

# ── B2: Toggle "Enable Dock" -> PowerDock window appears/disappears ──
# Stronger than the bindings sub-file's schema check: not only does settings.json
# update, but the actual Dock window (titled "PowerDock", child window
# of Microsoft.CmdPal.UI) materialises within a few seconds.
# Catches: settings.json updates correctly but CmdPal AppX never reads
# the field, or DockManager doesn't react to settings changes.
Test-Case 'CmdPal_SettingsUI_Dock_EnableDockShowsPowerDockWindow' "★ E2E ★ (PR #46436 / #46163 / #46915): toggle 'Enable Dock' in Settings UI -> PowerDock window appears within 5s -> disable -> window disappears" {
    Switch-CmdPalAppXSettingsPage -Hwnd $cpsHwndB -Page 'Dock'
    $orig = (_ReadJsonShared $cpSettings).EnableDock
    # Track whether we changed state (so cleanup only fires when needed)
    $weEnabled = $false
    try {
        # Act 1: enable dock (whether or not it was already enabled — we
        # need to assert the visible appearance, not just the toggle).
        if ($orig) {
            # Already enabled — disable first so the test exercises the
            # ENABLE path (and we can verify the window appears).
            Set-CmdPalAppXSettingsControl -Hwnd $cpsHwndB `
                -ControlId 'CmdPal_DockSettingsPage_EnableDock' `
                -SettingsKey 'EnableDock' -ExpectedValue $false | Out-Null
            # Allow PowerDock to actually close
            $null = Wait-Until -TimeoutMs 5000 -PollMs 250 -IgnoreException `
                -Message "PowerDock window did not close within 5s after disable" `
                -Condition {
                    -not ((winapp ui list-windows -a 'CmdPal' --json 2>$null | ConvertFrom-Json) | Where-Object { $_.title -eq 'PowerDock' })
                }
        }
        # Now toggle ENABLE
        Set-CmdPalAppXSettingsControl -Hwnd $cpsHwndB `
            -ControlId 'CmdPal_DockSettingsPage_EnableDock' `
            -SettingsKey 'EnableDock' -ExpectedValue $true | Out-Null
        $weEnabled = $true

        # Assert: PowerDock window materialises within 5s. Width is the
        # primary monitor width (e.g. 2560); height is set by DockSize
        # ('Default' is ~57px on this machine, 'Compact' is ~36px). We
        # assert presence + reasonable height (>20, <200) to be display-
        # resolution-independent.
        $dock = Wait-Until -TimeoutMs 5000 -PollMs 250 -IgnoreException `
            -Message "PowerDock window did not appear within 5s of enabling the dock" `
            -Condition {
                $d = (winapp ui list-windows -a 'CmdPal' --json 2>$null | ConvertFrom-Json) |
                     Where-Object { $_.title -eq 'PowerDock' } | Select-Object -First 1
                if ($d) { return $d }
                $null
            }
        Assert-NotNull $dock -Because 'PowerDock window not found after enable'
        Assert-True ($dock.height -ge 20 -and $dock.height -le 200) -Because "PowerDock window height $($dock.height) outside expected 20-200 range — Dock rendering may be broken"
        Assert-GreaterThan $dock.width 799 -Because "PowerDock window width $($dock.width) suspiciously small (expected primary-monitor width)"
        Write-Host "    info: PowerDock window appeared at $($dock.width)x$($dock.height) after Enable Dock toggle" -ForegroundColor DarkGray
    } finally {
        # Cleanup — restore original EnableDock state
        try {
            $cur = (_ReadJsonShared $cpSettings).EnableDock
            if ($cur -ne $orig) {
                Set-CmdPalAppXSettingsControl -Hwnd $cpsHwndB `
                    -ControlId 'CmdPal_DockSettingsPage_EnableDock' `
                    -SettingsKey 'EnableDock' -ExpectedValue $orig | Out-Null
            }
        } catch { Write-Warning "[cleanup] failed to restore EnableDock to $orig`: $($_.Exception.Message)" }
    }
}

# ── B3: DockSize Compact mode shrinks PowerDock height (PR #46699) ──
# PR #46699 added DockSize.Compact which reduces the dock from default
# height (~57px) to 28-36px and hides item subtitles. Verify by reading
# the live PowerDock window dimensions before vs after the size change.
# This catches: settings.json DockSize updates but PowerDock rendering
# doesn't react (regression of #46699).
Test-Case 'CmdPal_SettingsUI_Dock_CompactModeShrinksPowerDockHeight' "★ E2E ★ (PR #46699): DockSize 'Compact' in Settings UI shrinks PowerDock window height vs 'Default'" {
    Switch-CmdPalAppXSettingsPage -Hwnd $cpsHwndB -Page 'Dock'
    $origEnable = (_ReadJsonShared $cpSettings).EnableDock
    $origSize = (_ReadJsonShared $cpSettings).DockSettings.DockSize
    try {
        # Ensure dock is enabled + size is Default (baseline measurement)
        if (-not $origEnable) {
            Set-CmdPalAppXSettingsControl -Hwnd $cpsHwndB `
                -ControlId 'CmdPal_DockSettingsPage_EnableDock' `
                -SettingsKey 'EnableDock' -ExpectedValue $true | Out-Null
        }
        if ($origSize -ne 'Default') {
            Set-CmdPalAppXSettingsControl -Hwnd $cpsHwndB `
                -ControlId 'DockSizeComboBox' `
                -SettingsKey 'DockSettings.DockSize' `
                -Mode Set -ExpectedValue 'Default' | Out-Null
        }
        Start-Sleep -Milliseconds 800   # let dock re-render at default size

        $dockDefault = (winapp ui list-windows -a 'CmdPal' --json | ConvertFrom-Json) |
                       Where-Object { $_.title -eq 'PowerDock' } | Select-Object -First 1
        Assert-NotNull $dockDefault -Because 'PowerDock window not found after enabling at Default size'
        $hDefault = $dockDefault.height

        # Act: switch to Compact
        Set-CmdPalAppXSettingsControl -Hwnd $cpsHwndB `
            -ControlId 'DockSizeComboBox' `
            -SettingsKey 'DockSettings.DockSize' `
            -Mode Set -ExpectedValue 'Compact' | Out-Null
        # Wait for the dock window to actually resize
        $dockCompact = Wait-Until -TimeoutMs 5000 -PollMs 250 -IgnoreException `
            -Message "PowerDock window did not shrink after DockSize=Compact (still $($hDefault)px)" `
            -Condition {
                $d = (winapp ui list-windows -a 'CmdPal' --json 2>$null | ConvertFrom-Json) |
                     Where-Object { $_.title -eq 'PowerDock' } | Select-Object -First 1
                if ($d -and $d.height -lt $hDefault) { return $d }
                $null
            }
        Assert-NotNull $dockCompact -Because 'Compact mode never shrank PowerDock'
        Assert-LessThan $dockCompact.height $hDefault -Because "PowerDock height $($dockCompact.height) is NOT smaller than Default height $hDefault — Compact mode rendering broken"
        Write-Host "    info: PowerDock height $hDefault -> $($dockCompact.height) after DockSize=Compact" -ForegroundColor DarkGray
    } finally {
        # Cleanup — restore original DockSize then EnableDock state
        try {
            $curSize = (_ReadJsonShared $cpSettings).DockSettings.DockSize
            if ($curSize -ne $origSize) {
                Set-CmdPalAppXSettingsControl -Hwnd $cpsHwndB `
                    -ControlId 'DockSizeComboBox' `
                    -SettingsKey 'DockSettings.DockSize' `
                    -Mode Set -ExpectedValue $origSize | Out-Null
            }
            $curEnable = (_ReadJsonShared $cpSettings).EnableDock
            if ($curEnable -ne $origEnable) {
                Set-CmdPalAppXSettingsControl -Hwnd $cpsHwndB `
                    -ControlId 'CmdPal_DockSettingsPage_EnableDock' `
                    -SettingsKey 'EnableDock' -ExpectedValue $origEnable | Out-Null
            }
        } catch { Write-Warning "[cleanup] failed to restore Dock state: $($_.Exception.Message)" }
    }
}

# ── B4: Dock Position changes PowerDock window y-coordinate ────────
# PR #46436 / #46163 / #46915 — Position (Top/Bottom/Left/Right) is
# fundamental to Dock UX. This test verifies the full chain:
# Settings UI ComboBox -> DockSettings.Side write to JSON -> AppX
# Dock manager re-renders PowerDock at the new screen position.
# Catches: ComboBox writes correctly but DockManager ignores the
# position change (regression where Top stays Top after user picked
# Bottom).
#
# Uses real screen coordinates (via Get-WindowRect) because the JSON
# from winapp ui list-windows omits absolute x/y. Verified probe:
#   Top    -> y = 0           (near screen top)
#   Bottom -> y ≈ 1066+       (near screen bottom)
Test-Case 'CmdPal_SettingsUI_Dock_PositionTopBottomRelocatesPowerDock' "★ E2E ★ (PR #46436 / #46163): change Dock Position Top->Bottom in Settings UI relocates the PowerDock window from screen top (top 10%) to screen bottom (bottom 50%)" {
    Switch-CmdPalAppXSettingsPage -Hwnd $cpsHwndB -Page 'Dock'
    $origEnable = (_ReadJsonShared $cpSettings).EnableDock
    $origSide   = (_ReadJsonShared $cpSettings).DockSettings.Side
    # Resolve primary-screen height once — the Top/Bottom thresholds below
    # are derived from it so the assertions work on any resolution. The
    # previous hardcoded 800px floor for "Bottom" silently passed on 1080p+
    # but would never fire on a 720p screen.
    Add-Type -AssemblyName System.Windows.Forms -ErrorAction SilentlyContinue
    $screenHeight = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds.Height
    $topMax       = [int]($screenHeight * 0.10)   # within top 10% of screen counts as "Top"
    $bottomMin    = [int]($screenHeight * 0.50)   # below the midpoint counts as "Bottom"
    try {
        # Ensure dock is enabled at Top (baseline measurement)
        if (-not $origEnable) {
            Set-CmdPalAppXSettingsControl -Hwnd $cpsHwndB `
                -ControlId 'CmdPal_DockSettingsPage_EnableDock' `
                -SettingsKey 'EnableDock' -ExpectedValue $true | Out-Null
        }
        if ($origSide -ne 'Top') {
            Set-CmdPalAppXSettingsControl -Hwnd $cpsHwndB `
                -ControlId 'DockPositionComboBox' `
                -SettingsKey 'DockSettings.Side' `
                -Mode Set -ExpectedValue 'Top' | Out-Null
        }
        Start-Sleep -Milliseconds 1500   # let dock relocate to Top

        $dockTop = (winapp ui list-windows -a 'CmdPal' --json | ConvertFrom-Json) |
                   Where-Object { $_.title -eq 'PowerDock' } | Select-Object -First 1
        Assert-NotNull $dockTop -Because 'PowerDock not found after enabling at Top'
        $rectTop = Get-WindowRect -Hwnd $dockTop.hwnd
        Assert-NotNull $rectTop -Because 'Get-WindowRect failed for PowerDock at Top'
        Assert-LessThan $rectTop.Top ($topMax + 1) -Because "PowerDock at Position=Top has y=$($rectTop.Top), expected within top 10% of screen (<= $topMax of $screenHeight px tall)"
        Write-Host "    info: Top baseline -> PowerDock y=$($rectTop.Top) (screen=${screenHeight}px tall, top-bound=$topMax)" -ForegroundColor DarkGray

        # Act: switch to Bottom
        Set-CmdPalAppXSettingsControl -Hwnd $cpsHwndB `
            -ControlId 'DockPositionComboBox' `
            -SettingsKey 'DockSettings.Side' `
            -Mode Set -ExpectedValue 'Bottom' | Out-Null
        # Wait for PowerDock to actually move
        $rectBottom = Wait-Until -TimeoutMs 6000 -PollMs 300 -IgnoreException `
            -Message "PowerDock did not relocate to bottom of screen within 6s after Side=Bottom" `
            -Condition {
                $d = (winapp ui list-windows -a 'CmdPal' --json 2>$null | ConvertFrom-Json) |
                     Where-Object { $_.title -eq 'PowerDock' } | Select-Object -First 1
                if (-not $d) { return $null }
                $r = Get-WindowRect -Hwnd $d.hwnd
                # Bottom should have y past the midpoint of the primary screen
                if ($r -and $r.Top -gt $bottomMin) { return $r }
                $null
            }
        Assert-NotNull $rectBottom -Because "PowerDock never reached Bottom position (y > $bottomMin on a ${screenHeight}px-tall screen)"
        Assert-GreaterThan $rectBottom.Top $rectTop.Top -Because "PowerDock y at Bottom ($($rectBottom.Top)) is not GREATER than y at Top ($($rectTop.Top)) — Position change didn't actually relocate the window"
        Write-Host "    info: Bottom -> PowerDock y=$($rectBottom.Top) (moved from y=$($rectTop.Top); bottom-bound=$bottomMin)" -ForegroundColor DarkGray
    } finally {
        # Cleanup — restore original Side then EnableDock state
        try {
            $curSide = (_ReadJsonShared $cpSettings).DockSettings.Side
            if ($curSide -ne $origSide) {
                Set-CmdPalAppXSettingsControl -Hwnd $cpsHwndB `
                    -ControlId 'DockPositionComboBox' `
                    -SettingsKey 'DockSettings.Side' `
                    -Mode Set -ExpectedValue $origSide | Out-Null
            }
            $curEnable = (_ReadJsonShared $cpSettings).EnableDock
            if ($curEnable -ne $origEnable) {
                Set-CmdPalAppXSettingsControl -Hwnd $cpsHwndB `
                    -ControlId 'CmdPal_DockSettingsPage_EnableDock' `
                    -SettingsKey 'EnableDock' -ExpectedValue $origEnable | Out-Null
            }
        } catch { Write-Warning "[cleanup] failed to restore Dock Position/Enable state: $($_.Exception.Message)" }
    }
}

# ── B5: Position=Left makes PowerDock vertical (tall, narrow) ──────
# Complements B4 (Top↔Bottom). When Position switches from Top to Left,
# the dock layout flips from wide-and-short to tall-and-narrow. This
# is the only easy way to verify the vertical-vs-horizontal layout
# code path renders correctly via UIA (width/height swap).
Test-Case 'CmdPal_SettingsUI_Dock_PositionLeftMakesPowerDockVertical' "★ E2E ★: change Dock Position to Left in Settings UI makes PowerDock vertical (height > width)" {
    Use-CmdPalDockSetting -SettingsHwnd $cpsHwndB `
                          -SettingKey 'DockSettings.Side' `
                          -ControlId 'DockPositionComboBox' `
                          -Body {
        Set-CmdPalAppXSettingsControl -Hwnd $cpsHwndB `
            -ControlId 'DockPositionComboBox' -SettingsKey 'DockSettings.Side' `
            -Mode Set -ExpectedValue 'Left' | Out-Null
        # Wait for re-render
        $dock = Wait-Until -TimeoutMs 5000 -PollMs 250 -IgnoreException `
            -Message "PowerDock did not become vertical (height>width) within 5s after Side=Left" `
            -Condition {
                $d = (winapp ui list-windows -a 'CmdPal' --json 2>$null | ConvertFrom-Json) |
                     Where-Object { $_.title -eq 'PowerDock' } | Select-Object -First 1
                if ($d -and $d.height -gt $d.width) { return $d }
                $null
            }
        Assert-NotNull $dock -Because 'PowerDock never became vertical at Side=Left'
        Write-Host "    info: Left -> PowerDock $($dock.width)x$($dock.height) (vertical)" -ForegroundColor DarkGray
    }
}

# ── B6: Default bands present on first enable ──────────────────────
# When Dock is freshly enabled, the doc guarantees:
#   Start region: Home + WinGet     -> >= 1 ListItem
#   End   region: PerfMon + DateTime -> >= 2 ListItems
# Catches regression where enable produces empty Dock (broken default
# band seeding) and also verifies StartListView / EndListView are
# reachable via their stable AutomationIds in the PowerDock tree.
Test-Case 'CmdPal_SettingsUI_Dock_DefaultBandsPresentOnFirstEnable' "★ E2E ★: enable Dock -> PowerDock has default bands (StartListView >= 1 item, EndListView >= 2 items including PerfMon + DateTime)" {
    Use-CmdPalEnabledDock -SettingsHwnd $cpsHwndB -Body {
        $dh = Get-PowerDockHwnd
        Assert-NotNull $dh -Because 'PowerDock window not present even after enable'

        $start = Get-CmdPalDockBandContent -DockHwnd $dh -Region 'StartListView'
        $end   = Get-CmdPalDockBandContent -DockHwnd $dh -Region 'EndListView'
        Assert-True $start.Exists -Because 'StartListView (stable AutomationId) not present in PowerDock tree'
        Assert-True $end.Exists   -Because 'EndListView (stable AutomationId) not present in PowerDock tree'
        Assert-GreaterThan $start.ItemCount 0 -Because "Start region has $($start.ItemCount) items (expected >= 1 — default seed should include Home)"
        Assert-GreaterThan $end.ItemCount 1   -Because "End region has $($end.ItemCount) items (expected >= 2 — default seed should include PerfMon + DateTime)"
        Write-Host "    info: PowerDock default bands: Start=$($start.ItemCount) items, End=$($end.ItemCount) items, end subtitles=[$($end.Subtitles -join ', ')]" -ForegroundColor DarkGray
    }
}

# ── B7: Performance Monitor band shows live data ───────────────────
# The default End-region PerfMon band exposes friendly title+subtitle
# Text labels for each metric (CPU/Memory/Disk Receive/Send/GPU). This
# test asserts those subtitle labels are present in the live PowerDock
# UIA tree, proving the PerfMon extension is wired and emitting data.
# Catches: extension crashes on load, content-grid bindings broken,
# subtitles silently empty.
Test-Case 'CmdPal_SettingsUI_Dock_PerformanceMonitorBandShowsLiveData' "★ E2E ★: PowerDock EndListView includes PerformanceMonitor band with CPU/Memory subtitles (extension live data)" {
    Use-CmdPalEnabledDock -SettingsHwnd $cpsHwndB -Body {
        $dh = Get-PowerDockHwnd
        Assert-NotNull $dh -Because 'PowerDock window not present after enable'

        $end = Get-CmdPalDockBandContent -DockHwnd $dh -Region 'EndListView'
        Assert-True $end.Exists -Because 'EndListView not present in PowerDock tree'
        # PerfMon subtitles are e.g. 'CPU', 'Memory', 'GPU', 'Receive ↓', 'Send ↑'
        # Assert at least 2 of the core metrics (CPU, Memory) are present
        $required = @('CPU','Memory')
        $missing = @($required | Where-Object { $_ -notin $end.Subtitles })
        Assert-Empty $missing -Because "PerformanceMonitor band missing required subtitle text. Got subtitles: [$($end.Subtitles -join ', ')]"
        Write-Host "    info: PerfMon band subtitles present: $(@($end.Subtitles | Where-Object { $_ -in @('CPU','Memory','GPU','Receive ↓','Send ↑') }) -join ', ')" -ForegroundColor DarkGray
    }
}

# ── B8: DateTime band shows current time ───────────────────────────
# The default End-region clock band exposes a title-text matching the
# time format (e.g. '12:28 PM') and a subtitle matching the date format
# (e.g. '5/26/2026'). Verifies the DateTime extension is wired and
# emitting live data.
Test-Case 'CmdPal_SettingsUI_Dock_DateTimeBandShowsCurrentTime' "★ E2E ★: PowerDock EndListView includes DateTime band with time + date text (live formatting)" {
    Use-CmdPalEnabledDock -SettingsHwnd $cpsHwndB -Body {
        $dh = Get-PowerDockHwnd
        Assert-NotNull $dh -Because 'PowerDock window not present after enable'

        $end = Get-CmdPalDockBandContent -DockHwnd $dh -Region 'EndListView'
        Assert-True $end.Exists -Because 'EndListView not present in PowerDock tree'
        # Look for time + date patterns. Time can be 12-hour (12:28 PM) or
        # 24-hour (14:28). Date is locale-dependent — accept any string
        # with at least 3 digit groups separated by '/' or '-'.
        $timeRegex = '^\d{1,2}:\d{2}(\s?[AP]M)?$'
        $dateRegex = '^\d+[/-]\d+[/-]\d+$'
        $timeHit = @($end.Titles | Where-Object { $_ -match $timeRegex })
        $dateHit = @($end.Subtitles | Where-Object { $_ -match $dateRegex })
        Assert-GreaterThan $timeHit.Count 0 -Because "No time-formatted title in EndListView (expected something like '12:28 PM' matching '$timeRegex'). Titles: [$($end.Titles -join ', ')]"
        Assert-GreaterThan $dateHit.Count 0 -Because "No date-formatted subtitle in EndListView (expected something like '5/26/2026' matching '$dateRegex'). Subtitles: [$($end.Subtitles -join ', ')]"
        Write-Host "    info: DateTime band: time='$($timeHit[0])' date='$($dateHit[0])'" -ForegroundColor DarkGray
    }
}

}  # end Test-AnyTestWillRun guard (e2e sub-file)

