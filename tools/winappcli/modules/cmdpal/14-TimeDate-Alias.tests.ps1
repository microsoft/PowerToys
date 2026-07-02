#Requires -Version 7.0
# 14-TimeDate-Alias.tests.ps1 — extracted from command-palette-checklist.ps1 during Phase 2b split.
# Dot-sourced from the orchestrator so it shares script scope ($cpHwnd,
# $cpSettings, $cpEnabled, $cpDataDir). See _helpers.ps1 for the
# CmdPal-specific helper functions these tests call into.
# ── Box L1039 ★ FULL: Time/Date via ')' DIRECT alias ────────────────
# Same recipe as L1028 (CmdPal_TimeDate_CopiesFirstValueToClipboardOnEnter)
# but enters the Time/Date sub-page via the ')' alias instead of typing
# 'time' on home. Covers the alias-routing code path. The actual copy
# behaviour is covered by L1028; this asserts the alias gets us into the
# right sub-page (Primary='Copy', placeholder mentions time stamp).
#
# Wrapped in Use-CmdPalProviderEnabled (datetime provider) — independent
# of the earlier TimeDate fixture wrap because they're far apart in the
# file. Each probe is ~2.5s when calc isn't responsive; if datetime is
# already live, both probes are no-ops.
$_timeDateTest2Ids = @('CmdPal_TimeDate_DirectAliasOpensProvider')
$_registerTimeDateTest2 = {
Test-Case 'CmdPal_TimeDate_DirectAliasOpensProvider' "Box L1039 ★: Time/Date ')' alias navigates to provider sub-page (FUNCTIONAL — safe, doesn't invoke)" {
    try {
    # Act
        $placeholder = Invoke-CmdPalAlias ')'
        # Stash for assertion
        $script:_tdSubPlaceholder = $placeholder
        # Wait for the bottom-bar Primary to actually become 'Copy'
        # instead of a blind 400ms sleep. The sub-page transitions then
        # populates Primary; on slow boxes 400ms wasn't always enough.
        $null = Wait-Until -TimeoutMs 2000 -PollMs 100 -IgnoreException `
            -Message "Time/Date sub-page Primary did not become 'Copy' within 2s after ')' alias navigation" `
            -Condition { (Get-UiaProperty 'PrimaryCommandButton' 'Name' -Hwnd $cpHwnd) -eq 'Copy' }
    # Assert
        $placeholder = $script:_tdSubPlaceholder
        Assert-Match $placeholder '(?i)time|date|stamp' -Because "Time/Date sub-page placeholder '$placeholder' should mention time/date/stamp"
        $primary = (Get-UiaProperty 'PrimaryCommandButton' 'Name' -Hwnd $cpHwnd)
        Assert-Equal $primary 'Copy' -Because 'On Time/Date sub-page Primary should be Copy'
        # Verify ItemsList has at least one ListItem (the formatted-time rows).
        # Use text-mode inspect — JSON returns the wrong subtree on this page.
        $insLines = (winapp ui inspect 'ItemsList' -w $cpHwnd --depth 4 2>$null) -split "`n"
        $itemCount = @($insLines | Where-Object { $_ -match 'ListItem "' }).Count
        Assert-GreaterThan $itemCount 2 -Because "Time/Date sub-page should have multiple time-format rows (got $itemCount ListItems)"
        Write-Host "    info: ')' alias landed on sub-page with $itemCount ListItems, Primary='$primary'" -ForegroundColor DarkGray
    } finally {
    # Cleanup
        Reset-CmdPalToHome
        Remove-Variable -Scope Script -Name '_tdSubPlaceholder' -ErrorAction SilentlyContinue
    }
}
}  # end $_registerTimeDateTest2 scriptblock

if (Test-AnyTestWillRun -Ids $_timeDateTest2Ids) {
    Use-CmdPalProviderEnabled -ProviderId 'com.microsoft.cmdpal.builtin.datetime' -Body $_registerTimeDateTest2
} else {
    & $_registerTimeDateTest2
}
