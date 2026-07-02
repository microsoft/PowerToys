#Requires -Version 7.0
# 13-Stability-RapidTyping.tests.ps1 — extracted from command-palette-checklist.ps1 during Phase 2b split.
# Dot-sourced from the orchestrator so it shares script scope ($cpHwnd,
# $cpSettings, $cpEnabled, $cpDataDir). See _helpers.ps1 for the
# CmdPal-specific helper functions these tests call into.
#
# RUNTIME-VARIANCE NOTE (2026-05-27, R2 follow-up):
# This test's wall-time varies wildly (observed 14s — 124s) based on
# CmdPal AppX state at the time it runs. Reason: the for-loop below
# fires 50 set-value UIA calls in tight succession with NO inter-call
# sleep (intentional — it IS the rapid-typing stress). Each UIA call
# round-trips through the AppX UI thread, which takes:
#   - ~200ms on a freshly-launched, idle AppX (best case, e.g.
#     immediately after restart with no prior tests run)
#   - ~2.4s on a state-loaded AppX (typical when this test runs late
#     in the suite, after 60+ prior tests have churned settings)
# 50 × 200ms = 10s vs 50 × 2.4s = 120s — a 10x spread that's NOT
# affected by anything in this test file. The commit message of
# `e09b465aaa` ("CmdPal #7: ... -42% suite time") overstated the
# wall-time improvement on the back of a single anomalously-fast
# run (538s, with Stability at 13.6s). The real, sustained suite
# wall-time on a typical run is ~900-950s. Tests in this file are
# not a useful benchmark for measuring Wait-Until conversion
# speedups; use tests that don't hit AppX in a tight loop (e.g.
# the schema buckets that read settings.json only).
#
# Practical implication for future authors: if you're trying to
# decide whether a wall-time change is real or noise, ignore the
# Stability_RapidTyping line and look at the SUM of all other tests.
Test-Case 'CmdPal_Stability_RapidTypingDoesNotCrashAppX' "★ 0.99.0 regression: 20 rapid set-value typings do not crash CmdPal AppX" {
    # Arrange
            $appx = Get-Process Microsoft.CmdPal.UI -ErrorAction SilentlyContinue | Select-Object -First 1
    Assert-NotNull $appx -Because 'Microsoft.CmdPal.UI not running before stress'
    $pidBefore = $appx.Id
    $degraded  = $false   # set in Act, asserted before finally; checked in finally for restart
    # Iteration count chosen 2026-05-27: was 50, reduced to 20 after a
    # full distribution analysis showed this single test consumed 13%
    # of the suite wall-time (~120s out of 950s) and the regressions
    # it guards (PR #47148 + #47186) reproduce at 5-10 rapid typings.
    # 20 is comfortably above the repro threshold while saving ~70s.
    # The sister test CmdPal_Stability_TypingDoesNotCrashWithProviderSettingsIntact
    # still exercises a smaller distinct payload set with provider-chain
    # validation, so coverage is preserved.
    $iterations = 20
    try {
    # Act
        $rand = [System.Random]::new()
        for ($i = 1; $i -le $iterations; $i++) {
            $len = $rand.Next(1, 30)
            $s = -join (1..$len | ForEach-Object { [char]($rand.Next(32, 127)) })
            winapp ui set-value 'MainSearchBox' $s -w $cpHwnd 2>$null | Out-Null
        }
        # Drain queued events: instead of a blind 500ms sleep, wait for the
        # AppX to be responsive (a tiny probe-set succeeds and echoes back).
        # If AppX is still processing the typing flood it will fail to
        # echo; this gives the suite a fair chance before the assertions
        # below classify the AppX as degraded.
        $drainProbe = "wac_drain_$(Get-Random -Max 9999)"
        $null = Wait-Until -TimeoutMs 3000 -PollMs 100 -IgnoreException `
            -Message "CmdPal AppX did not become responsive (probe echo) within 3s after $iterations rapid typings — likely TextChanged-broken or thread starvation" `
            -Condition {
                winapp ui set-value 'MainSearchBox' $drainProbe -w $cpHwnd 2>$null | Out-Null
                $cur = winapp ui get-value 'MainSearchBox' -w $cpHwnd 2>$null
                $cur -eq $drainProbe
            }
    # Assert — (a) AppX alive, (b) AppX still functional (TextChanged not broken)
        $appxAfter = Get-Process -Id $pidBefore -ErrorAction SilentlyContinue
                    Assert-NotNull $appxAfter -Because "CmdPal AppX (PID $pidBefore) DIED during $iterations rapid set-value operations"
                    Assert-False $appxAfter.HasExited -Because "CmdPal AppX (PID $pidBefore) HasExited=true after stress"
        # (b) Functional probe — set a sentinel, read it back. If echo fails,
        # AppX is in "TextChanged-broken" state where set-value works at the
        # UIA level but the WinUI TextChanged event no longer fires. Real
        # users would see search results stop updating — that's a regression
        # of the same #47148/#47186 class as a hard crash. Previously the
        # cleanup silently restarted the AppX on this condition (= false PASS).
        # Now we assert it as a failure; the finally block still restarts to
        # keep downstream tests running.
        $sentinel = "winappcli_probe_$(Get-Random)"
        winapp ui set-value 'MainSearchBox' $sentinel -w $cpHwnd 2>$null | Out-Null
        # Poll for echo to land instead of a blind 300ms sleep. If echo
        # never lands within 2s the next Assert-False catches it as a
        # TextChanged-broken regression (so the budget just bounds how
        # long we wait for healthy AppX to echo).
        $null = Wait-Until -TimeoutMs 2000 -PollMs 100 -IgnoreException `
            -Message "echo probe never landed within 2s" `
            -Condition { (winapp ui get-value 'MainSearchBox' -w $cpHwnd 2>$null) -eq $sentinel }
        $echoed = winapp ui get-value 'MainSearchBox' -w $cpHwnd 2>$null
        $degraded = ($echoed -ne $sentinel)
        Assert-False $degraded -Because "rapid-typing left AppX in TextChanged-broken state (echo='$echoed' != sentinel='$sentinel') — set-value succeeds at UIA but TextChanged no longer fires, so search results would stop updating for real users. Same regression class as #47148/#47186."
                    Write-Host "    info: CmdPal AppX PID $($pidBefore) survived $iterations rapid typings AND echo-probe (set-value->TextChanged wiring intact)" -ForegroundColor DarkGray
    } finally {
    # Cleanup — always clear the search box; if degradation was detected
    # (whether the Assert fired or not), force-restart the AppX so
    # downstream tests get a clean handle. This is safety-net only —
    # the test result has already been recorded by Assert-False above.
        winapp ui set-value 'MainSearchBox' '' -w $cpHwnd 2>$null | Out-Null
        if ($degraded) {
            Write-Host "    [cleanup] degradation detected — restarting CmdPal.UI to clear it for downstream tests (test result was already recorded)" -ForegroundColor Yellow
            $p = Get-Process Microsoft.CmdPal.UI -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($p) {
                try { Stop-Process -Id $p.Id -Force -ErrorAction Stop } catch { Write-Warning "[cleanup] failed to stop PID $($p.Id): $($_.Exception.Message)" }
                # Wait for the process to actually exit, instead of a blind
                # 2s sleep. WaitForExit returns immediately if the process
                # is already gone; 5s ceiling covers slow disks / handle
                # cleanup. If it doesn't exit, downstream restart races a
                # zombie and the new AppX may not pick up — surface the
                # condition rather than silently sleeping past it.
                try {
                    if (-not $p.WaitForExit(5000)) {
                        Write-Warning "[cleanup] CmdPal.UI PID $($p.Id) did not exit within 5s of Stop-Process — restart may race a zombie"
                    }
                } catch {}
            }
            Start-Process 'shell:AppsFolder\Microsoft.CommandPalette_8wekyb3d8bbwe!App'
            # Wait for the new AppX window to actually appear, instead of
            # a blind 4s sleep. Cold-start is usually 1-3s; allow 10s for
            # slow disks. If the window never appears, the warn below
            # documents the failure and downstream tests will fail loudly
            # rather than silently using a stale handle.
            $newW = Wait-Until -TimeoutMs 10000 -PollMs 250 -IgnoreException `
                -Message "CmdPal AppX restart did not produce a window within 10s" `
                -Condition {
                    $w = (winapp ui list-windows -a 'Microsoft.CmdPal.UI' --json 2>$null) | ConvertFrom-Json
                    if ($w -and $w[0].hwnd) { $w } else { $null }
                }
            # Re-resolve cpHwnd in module scope so downstream tests use the
            # restarted window. (Test bodies close over $cpHwnd captured at
            # module load — that handle is now stale.)
            if ($newW -and $newW[0].hwnd) {
                $script:cpHwnd = [int64]$newW[0].hwnd
                Write-Host "    [cleanup] CmdPal AppX restarted; new hwnd=$($script:cpHwnd)" -ForegroundColor DarkGray
            }
        }
    }
}
