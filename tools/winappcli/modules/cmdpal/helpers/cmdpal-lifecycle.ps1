#Requires -Version 7.0
# cmdpal-lifecycle.ps1 — split from _helpers.ps1 (review item #5).
# Dot-sourced from _helpers.ps1; shares script scope with the orchestrator
# so it sees $cpHwnd / $cpSettings / $cpEnabled / $cpDataDir.
#
# Future: when these helpers stabilise, move to
#   WinAppCli.PowerToys/functions/15-CmdPal.ps1
# as a proper module with parameterised signatures (-Hwnd $cpHwnd etc.).
# That's a separate refactor.
function Reset-CmdPalToHome {
    # Return CmdPal to its home page from any sub-page.
    # CmdPal's Escape key handler is NOT reachable via SendInput (UIPI
    # blocks elevated→non-elevated AppX) OR via PostMessage (the WinUI 3
    # navigation handler hooks raw input, not WM_KEYDOWN). The reliable
    # way is to invoke the BackButton via UIA InvokePattern, which works
    # regardless of elevation. Loop until the search box matches the
    # home placeholder OR we've clicked Back 6 times (defensive cap).
    #
    # IMPORTANT: BackButton on the home page DISMISSES the CmdPal window.
    # That can leave it half-restored when we then signal CmdPal.Show
    # and immediately try set-value (providers like Files/WindowsSettings
    # need more time to repopulate their results after a hide/show cycle).
    # So: detect home first; only click Back from sub-pages; always
    # re-signal Show with a generous settle.
    $homePlaceholder = 'Search for apps, files and commands'
    for ($i = 0; $i -lt 6; $i++) {
        $cur = winapp ui get-value 'MainSearchBox' -w $cpHwnd 2>$null
        if ($cur -and $cur -match [regex]::Escape($homePlaceholder)) { break }
        winapp ui invoke 'BackButton' -w $cpHwnd 2>$null | Out-Null
        Start-Sleep -Milliseconds 200
    }
    # Re-signal Show in case CmdPal hid itself (it does this when BackButton
    # is invoked on the home page). Give a generous settle so all providers
    # finish their re-init — short waits cause Files/WindowsSettings to
    # silently skip the next query.
    try { Invoke-PtSharedEvent -Name 'CmdPal.Show' | Out-Null } catch {}
    Start-Sleep -Milliseconds 800
    try { Set-WindowForeground -Hwnd $cpHwnd | Out-Null } catch {}
    Start-Sleep -Milliseconds 200
}

# Recover from "TextChanged-broken" state: when CmdPal AppX has been
# interacting for too long it can enter a degraded state where set-value
# writes succeed at the UIA level (the box visually updates) but the
# TextChanged event does NOT fire — so result list stays empty and aliases
# don't navigate. Kicked by rapid typing, long runs, or just bad luck.
# The only known cure is to restart the UI process (the helper process
# Microsoft.CmdPal.Ext.PowerToys keeps the Show event listener alive, so
# we never lose the hotkey wiring).
#
# WARNING (R2-8): on the recovery path this function REBINDS
# $script:cpHwnd to the freshly-launched window. Any caller that
# captured $cpHwnd into a local (e.g. `$h = $cpHwnd`) before invoking
# UI calls that may trigger recovery will silently target the dead
# pre-restart window. The fix is to ALWAYS read $cpHwnd directly at
# each call site instead of stashing it in a local — `winapp ui ...
# -w $cpHwnd` re-resolves on every call. If you must cache (rare),
# refresh after any operation that could trigger recovery.
function Reset-CmdPalAppXIfDegraded {
    # Probe the AppX with a known-good query that ALWAYS returns at least
    # one ListItem on any Windows install — the AllApps provider returns
    # a 'Notepad' ListItem for the query 'notepad'. If the box accepts
    # text but ZERO ListItems appear within ~1.5 s, TextChanged is broken
    # (set-value writes echo back at the UIA level but the provider chain
    # never sees the change). The only known cure is to restart the
    # Microsoft.CmdPal.UI process — the helper Microsoft.CmdPal.Ext.PowerToys
    # is left alive so we don't lose the CmdPal.Show event listener.
    #
    # The 'notepad' string here is NOT related to any specific test; it's
    # purely a degradation-detection probe. When you see the warn message
    # below it means recovery fired, not that a Notepad/Files test ran.

    # Fast-path early-return: if the MainSearchBox itself is reachable via
    # a no-side-effect probe (just a tree query, no set-value), the AppX is
    # almost certainly healthy and we can skip the slow set-value/clear/
    # restart cycle entirely. Saves ~2-3s × (number of callers) per suite
    # on the common (healthy) path. We still fall through to the full
    # probe when the cheap check fails — TextChanged-broken state CAN
    # leave MainSearchBox reachable but unresponsive to input.
    try {
        $box = winapp ui search 'MainSearchBox' -w $cpHwnd --json 2>$null | ConvertFrom-Json -ErrorAction Stop
        if ($box -and $box.matchCount -gt 0) {
            # MainSearchBox is in the tree — verify TextChanged actually
            # fires by doing a quick set-value/echo/clear round trip with
            # a tight 500ms budget. If echo lands fast, AppX is healthy.
            $probe = "wac_fp_$(Get-Random -Max 9999)"
            winapp ui set-value 'MainSearchBox' $probe -w $cpHwnd 2>$null | Out-Null
            $echoDeadline = (Get-Date).AddMilliseconds(500 * (Get-WinAppCliSlowFactor))
            $echoOk = $false
            do {
                $cur = winapp ui get-value 'MainSearchBox' -w $cpHwnd 2>$null
                if ($cur -eq $probe) { $echoOk = $true; break }
                Start-Sleep -Milliseconds 80
            } while ((Get-Date) -lt $echoDeadline)
            winapp ui set-value 'MainSearchBox' '' -w $cpHwnd 2>$null | Out-Null
            if ($echoOk) { return $false }
            # echo failed — fall through to full recovery
        }
    } catch {
        # search/ConvertFrom-Json failed — AppX likely not responsive at all;
        # fall through to full recovery (Reset-AppToHome + restart).
    }

    Reset-AppToHome -Hwnd $cpHwnd -EscapeCount 3 -PauseMs 100
    Invoke-PtSharedEvent -Name 'CmdPal.Show' | Out-Null
    Start-Sleep -Milliseconds 200
    winapp ui set-value 'MainSearchBox' 'notepad' -w $cpHwnd 2>$null | Out-Null
    # 1.5s probe budget scaled by SlowFactor so a slow CI runner doesn't
    # misclassify itself as "degraded" and trigger spurious AppX restarts.
    $deadline = (Get-Date).AddMilliseconds(1500 * (Get-WinAppCliSlowFactor))
    $healthy = $false
    do {
        $r = winapp ui search 'Notepad' -w $cpHwnd --json 2>$null | ConvertFrom-Json
        $items = @($r.matches | Where-Object { $_.type -eq 'ListItem' })
        if ($items.Count -gt 0) { $healthy = $true; break }
        Start-Sleep -Milliseconds 200
    } while ((Get-Date) -lt $deadline)

    # Clear the search box no matter what
    winapp ui set-value 'MainSearchBox' '' -w $cpHwnd 2>$null | Out-Null
    if ($healthy) { return $false }
    $_probeMs = [int](1500 * (Get-WinAppCliSlowFactor))
    Write-Host "    warn: CmdPal AppX degraded (recovery probe 'notepad' produced 0 ListItems within ${_probeMs}ms) — restarting UI process" -ForegroundColor Yellow
    $p = Get-Process Microsoft.CmdPal.UI -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($p) {
        try { Stop-Process -Id $p.Id -Force -ErrorAction Stop } catch { Write-Warning "[cleanup] failed to stop PID $($p.Id): $($_.Exception.Message)" }
        # Wait for the process to actually exit before relaunching, instead
        # of a blind 2s sleep. The kill itself returns synchronously but the
        # AppX container takes a tick to free the PID; if we relaunch too
        # fast Windows may reuse the suspended instance.
        $null = Wait-Until -TimeoutMs 5000 -PollMs 100 -IgnoreException `
            -Message "CmdPal.UI PID $($p.Id) did not exit within 5s of Stop-Process" `
            -Condition { -not (Get-Process -Id $p.Id -ErrorAction SilentlyContinue) }
    }
    Start-Process 'shell:AppsFolder\Microsoft.CommandPalette_8wekyb3d8bbwe!App'
    # Wait for the new AppX window to appear instead of a blind 4s sleep.
    # Cold AppX activation is usually 1-3s; allow 10s to cover slow disks.
    $newW = Wait-Until -TimeoutMs 10000 -PollMs 250 -IgnoreException `
        -Message "CmdPal AppX restart did not produce a window within 10s" `
        -Condition {
            $w = (winapp ui list-windows -a 'Microsoft.CmdPal.UI' --json 2>$null) | ConvertFrom-Json
            if ($w -and $w[0].hwnd) { $w } else { $null }
        }
    # Re-resolve the window handle — the AppX restart gives a NEW hwnd.
    if ($newW -and $newW[0].hwnd) {
        $script:cpHwnd = [int64]$newW[0].hwnd
        Write-Host "    info: CmdPal AppX restarted; new hwnd=$($script:cpHwnd)" -ForegroundColor DarkGray
    } else {
        Write-Host "    warn: CmdPal AppX restart did not produce a new window handle" -ForegroundColor Yellow
        return $false   # tell caller recovery didn't fully succeed
    }
    # Settle: wait until the fresh AppX accepts a set-value probe AND echoes
    # back. The WinUI 3 search box can take another second or two before
    # TextChanged is wired even after the window exists. Without this
    # settle, the caller's immediate retry races the warmup and the very
    # next set-value silently no-ops.
    $settleDeadline = (Get-Date).AddSeconds(8)
    $settled = $false
    do {
        Invoke-PtSharedEvent -Name 'CmdPal.Show' | Out-Null
        Start-Sleep -Milliseconds 400
        $probe = "wac_settle_$(Get-Random -Max 9999)"
        winapp ui set-value 'MainSearchBox' $probe -w $script:cpHwnd 2>$null | Out-Null
        Start-Sleep -Milliseconds 300
        $echo = winapp ui get-value 'MainSearchBox' -w $script:cpHwnd 2>$null
        if ($echo -eq $probe) {
            # Echo OK. Clear and confirm provider results land too.
            winapp ui set-value 'MainSearchBox' '' -w $script:cpHwnd 2>$null | Out-Null
            Start-Sleep -Milliseconds 200
            $settled = $true
            break
        }
        Start-Sleep -Milliseconds 400
    } while ((Get-Date) -lt $settleDeadline)
    if ($settled) {
        Write-Host "    info: CmdPal AppX settled and accepting input" -ForegroundColor DarkGray
    } else {
        Write-Host "    warn: CmdPal AppX restarted but did not settle within 8s — caller may still fail" -ForegroundColor Yellow
    }
    return $true
}

