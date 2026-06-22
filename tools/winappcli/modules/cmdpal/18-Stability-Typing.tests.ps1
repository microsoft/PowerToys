#Requires -Version 7.0
# 18-Stability-Typing.tests.ps1 — extracted from command-palette-checklist.ps1 during Phase 2b split.
# Dot-sourced from the orchestrator so it shares script scope ($cpHwnd,
# $cpSettings, $cpEnabled, $cpDataDir). See _helpers.ps1 for the
# CmdPal-specific helper functions these tests call into.
# ── 0.99.0 PR #47148 + #47186 — second typing crash regression guard ──
# The 0.99.0 release fixed TWO typing crashes. We already have
# CmdPal_Stability_RapidTypingDoesNotCrashAppX as the primary regression
# guard. Add a sister test that also probes with the indexer fallback
# enabled (which was the trigger condition for #47186 specifically).
Test-Case 'CmdPal_Stability_TypingDoesNotCrashWithProviderSettingsIntact' "★ 0.99.0: PR #47148/#47186 — typing N chars with ProviderSettings intact does not crash" {
    try {
    # Arrange — verify AppX window is present (read $cpHwnd directly each
    # time rather than capturing it; if Reset-CmdPalAppXIfDegraded fires
    # mid-test and rebinds $script:cpHwnd, a captured local would point
    # at a dead window and downstream UIA calls would silently target it.
    # R2-8: every wrapper should resolve $cpHwnd lazily.
        Assert-NotNull $cpHwnd -Because 'CmdPal window not found via UIA — was AppX killed?'
    # Act
        # Use the existing CmdPal AppX (don't restart — we want to test
        # the steady-state, not the launch state). Use UIA set-value
        # which fires TextChanged the same way real typing would.
        # Set a query that has historically been a crash vector (long string
        # with special chars). 100ms wait between each so reentrancy guard
        # has time to publish each batch.
        $payloads = @('aaaa','aaaa+bbbb','aaaabbbbcccc','file:1234567890','{:?@#%^&*()}')
        foreach ($q in $payloads) {
            & winapp ui set-value 'MainSearchBox' $q -w $cpHwnd 2>&1 | Out-Null
            Start-Sleep -Milliseconds 150
        }
    # Assert — AppX is still alive
        $procs = Get-Process Microsoft.CmdPal.UI -ErrorAction SilentlyContinue
        Assert-NotNull $procs -Because "AppX died after typing payloads [$($payloads -join '|')] — REGRESSION of #47148/#47186"
        # Reset query to empty so the next test starts clean
        & winapp ui set-value 'MainSearchBox' '' -w $cpHwnd 2>&1 | Out-Null
    } finally {
    # Cleanup — read $cpHwnd directly (it may have been rebound by
    # Reset-CmdPalAppXIfDegraded if a downstream wrapper detected
    # degradation during the foreach above).
        try {
            if ($cpHwnd) { & winapp ui set-value 'MainSearchBox' '' -w $cpHwnd 2>&1 | Out-Null }
        } catch { Write-Warning "[cleanup] $($_.Exception.Message)" }
    }
}
