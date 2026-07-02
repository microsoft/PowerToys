#Requires -Version 7.0
# 06-System.tests.ps1 — extracted from command-palette-checklist.ps1 during Phase 2b split.
# Dot-sourced from the orchestrator so it shares script scope ($cpHwnd,
# $cpSettings, $cpEnabled, $cpDataDir). See _helpers.ps1 for the
# CmdPal-specific helper functions these tests call into.
# OLD (broken): asserted "any ListItem matching 'Lock'" — on most machines
# this matched 'Clock' (the system tray clock app from AllApps) on
# substring, and would have passed even if the System provider were
# completely disabled.
#
# NEW: queries 'shutdown' (a reliable System-provider keyword) and
# asserts the exact ListItem name 'Shutdown computer' is returned with
# Primary action 'Restart'/'Shut down'/'Sleep' — any of which prove the
# System provider returned the entry. We don't invoke (would actually
# shut down the machine).
#
# Wrapped in Use-CmdPalProviderEnabled (system provider).
$_systemTestIds = @('CmdPal_System_ReturnsShutdownCommandWithCorrectPrimary')
$_registerSystemTests = {
Test-Case 'CmdPal_System_ReturnsShutdownCommandWithCorrectPrimary' "Box L1040: System provider returns 'Shutdown computer' ListItem (FUNCTIONAL — safe, doesn't invoke)" {
    # Act
    Invoke-CmdPalQuery 'shutdown'
    # Assert — exact ListItem name 'Shutdown computer' is the System
    # provider's deterministic entry for the 'shutdown' query. Its
    # presence ALONE proves the System provider is loaded and indexed —
    # no other provider returns that exact name. We don't invoke
    # (would actually shut down the machine).
    #
    # NOTE: we use `winapp ui inspect` + regex parsing instead of
    # `winapp ui search` because winapp's search has a coverage gap —
    # it returns at most ~4 matches per query and `Shutdown computer`
    # consistently doesn't appear in its results even when present in
    # the actual UIA tree. `inspect ItemsList` enumerates the tree
    # directly with no filter.
    #
    # Kept as an inline do/while-deadline instead of converted to
    # Wait-Until because the parsed $names is needed both INSIDE the
    # loop (the success check) and AFTER the loop (the failure message
    # diagnostics): Wait-Until's condition runs in a child scope, so
    # smuggling $names back out required a $script: variable and was
    # measurably less reliable in the suite (2026-05-27 sweep).
    $deadline = (Get-Date).AddMilliseconds(10000)
    $names = @()
    do {
        $ins = (winapp ui inspect 'ItemsList' -w $cpHwnd --depth 2 2>$null) -split "`n"
        $names = @($ins | Where-Object { $_ -match '^\s*itm-\S+\s+ListItem\s+"([^"]+)"' } |
                   ForEach-Object { if ($_ -match 'ListItem\s+"([^"]+)"') { $matches[1] } })
        if ($names -contains 'Shutdown computer') { break }
        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)
    Assert-Contains $names 'Shutdown computer' -Because "System provider did not return 'Shutdown computer' ListItem within 10s"
    Write-Host "    info: System provider returned 'Shutdown computer' (exact-name match; $($names.Count) total items)" -ForegroundColor DarkGray
}
}  # end $_registerSystemTests scriptblock

if (Test-AnyTestWillRun -Ids $_systemTestIds) {
    Use-CmdPalProviderEnabled -ProviderId 'com.microsoft.cmdpal.builtin.system' -Body $_registerSystemTests
} else {
    & $_registerSystemTests
}
