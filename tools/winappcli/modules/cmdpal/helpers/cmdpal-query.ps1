#Requires -Version 7.0
# cmdpal-query.ps1 — split from _helpers.ps1 (review item #5).
# Dot-sourced from _helpers.ps1; shares script scope with the orchestrator
# so it sees $cpHwnd / $cpSettings / $cpEnabled / $cpDataDir.

# Type a query into MainSearchBox starting from a known-home state.
# Retries on AppX-suspension race (write lost when UI thread is waking).
# Does NOT pre-clear with set-value '' — that destabilises CmdPal's
# TextChanged listener on some queries; just overwrite.
# If retries exhaust OR set-value echoes but no ListItems appear (the
# "TextChanged-broken" degraded state), probe for degradation and
# auto-restart the AppX once.
function Invoke-CmdPalQuery {
    param([string]$Query, [int]$MaxAttempts = 3, [bool]$_AlreadyRecovered = $false)
    # Slow-machine multiplier so all hand-rolled deadlines below scale
    # with $env:WINAPPCLI_SLOW_FACTOR (1.0 default for fast dev box;
    # 3.0-5.0 typical for slow CI/VM). Without this, the 800ms echo
    # and item deadlines silently fail on slow boxes — TextChanged
    # debounce + provider chain can take >2s on a loaded shared runner.
    $slow = Get-WinAppCliSlowFactor
    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        # Use Reset-CmdPalToHome (BackButton-based) instead of Reset-AppToHome
        # (Esc-based, unreachable from elevated tests).
        Reset-CmdPalToHome
        Start-Sleep -Milliseconds 100

        winapp ui set-value 'MainSearchBox' $Query -w $cpHwnd 2>$null | Out-Null

        # Verify echo (proof the box accepted text). Retry full sequence on miss.
        $deadline = (Get-Date).AddMilliseconds(800 * $slow)
        $echoed = $false
        do {
            $cur = winapp ui get-value 'MainSearchBox' -w $cpHwnd 2>$null
            if ($cur -eq $Query) { $echoed = $true; break }
            Start-Sleep -Milliseconds 100
        } while ((Get-Date) -lt $deadline)
        if (-not $echoed) { continue }

        # Echo passed. For non-trivial queries (≥2 chars), also confirm the
        # result list has at least one ListItem — this catches the
        # "TextChanged-broken" degraded state where the box accepts text
        # but the provider chain never sees the change. Use inspect (not
        # `winapp ui search ''` which errors out on missing selector).
        if ($Query.Length -ge 2) {
            $itemDeadline = (Get-Date).AddMilliseconds(800 * $slow)
            do {
                $insLines = (winapp ui inspect 'ItemsList' -w $cpHwnd --depth 1 2>$null) -split "`n"
                $itemCount = @($insLines | Where-Object { $_ -match '^\s*itm-\S+\s+ListItem\s+' }).Count
                if ($itemCount -gt 0) { return }
                Start-Sleep -Milliseconds 150
            } while ((Get-Date) -lt $itemDeadline)
            # Echo OK but no results — degraded. Fall through to recovery
            # below (don't keep retrying same broken state).
            break
        } else {
            return
        }
    }
    # Echo never landed OR results never came back — AppX is in the
    # TextChanged-broken degraded state. Try once to recover.
    if (-not $_AlreadyRecovered) {
        if (Reset-CmdPalAppXIfDegraded) {
            Invoke-CmdPalQuery -Query $Query -MaxAttempts $MaxAttempts -_AlreadyRecovered $true
            return
        }
    }
    throw "CmdPal MainSearchBox did not echo query '$Query' or returned no results after $MaxAttempts attempts (AppX may be suspended/unresponsive)"
}

# IsDirect aliases ('<', '>', ':', '$', ')', '??') navigate to a sub-page
# rather than echoing — wait for the search box to STOP being the alias char
# (becomes the sub-page placeholder, e.g. 'Search open windows...').
# Auto-recovers from AppX degradation once.
function Invoke-CmdPalAlias {
    param([string]$Alias, [int]$NavTimeoutMs = 5000, [bool]$_AlreadyRecovered = $false)
    # Reset via BackButton (Esc keys don't reach CmdPal from elevated tests)
    Reset-CmdPalToHome
    Start-Sleep -Milliseconds 100

    # CmdPal's AliasManager.CheckAlias fires when the search text matches
    # an alias key. The detector is sensitive to the previous text — if
    # any residual text is in the box (e.g. from the prior test or from
    # CmdPal's KeepPreviousQuery behaviour), set-value to the alias char
    # may not trigger the alias because there's no clean transition.
    # Explicitly clear, settle briefly, THEN type the alias.
    winapp ui set-value 'MainSearchBox' '' -w $cpHwnd 2>$null | Out-Null
    Start-Sleep -Milliseconds 200
    # Re-signal Show immediately before typing the alias — on a cold AppX
    # CmdPal can be in a half-awake state where set-value writes the text
    # but the alias detector hasn't subscribed yet. Show forces a full
    # wake before the alias arrives.
    try { Invoke-PtSharedEvent -Name 'CmdPal.Show' | Out-Null } catch {}
    Start-Sleep -Milliseconds 400
    winapp ui set-value 'MainSearchBox' $Alias -w $cpHwnd 2>$null | Out-Null

    # Wait for REAL state-change signals proving we left home and landed
    # on a sub-page (either condition is sufficient):
    #
    #   A. PrimaryCommandButton.Name became something OTHER than the home
    #      default 'Open in default browser' (Web Search) — every direct
    #      alias produces a provider-specific Primary on its sub-page
    #      ('Copy' for Calc/TimeDate, 'Run' for Shell, 'Switch to' for
    #      Walker, etc.). Exception: '??' (Web Search) keeps the same
    #      Primary, so we don't rely on this alone — see B.
    #
    #   B. MainSearchBox value is no longer the literal alias char AND
    #      isn't the home placeholder. After navigation it shows the
    #      sub-page placeholder ('Type an equation...', etc.).
    try {
        return Wait-Until -TimeoutMs $NavTimeoutMs -PollMs 100 `
            -Message "Direct alias '$Alias' did not navigate to a sub-page" `
            -Condition {
                $cur = winapp ui get-value 'MainSearchBox' -w $cpHwnd 2>$null
                $pri = (Get-UiaProperty 'PrimaryCommandButton' 'Name' -Hwnd $cpHwnd)
                if ($pri -and $pri -ne 'Open in default browser') {
                    return ($cur ? $cur : "<subpage:$pri>")
                }
                if ($cur -and $cur -ne $Alias -and $cur -notmatch '^Search for apps') {
                    return $cur
                }
                return $null
            }
    } catch {
        # Wait-Until threw → navigation didn't happen within timeout.
        # Try a one-shot retry — sometimes the very first set-value
        # after a fresh AppX or Reset races CmdPal's TextChanged
        # subscription registration; a second attempt typically lands.
        if (-not $_AlreadyRecovered) {
            winapp ui set-value 'MainSearchBox' '' -w $cpHwnd 2>$null | Out-Null
            Start-Sleep -Milliseconds 300
            winapp ui set-value 'MainSearchBox' $Alias -w $cpHwnd 2>$null | Out-Null
            try {
                return Wait-Until -TimeoutMs $NavTimeoutMs -PollMs 100 `
                    -Message "Direct alias '$Alias' did not navigate (after inline retry)" `
                    -Condition {
                        $cur = winapp ui get-value 'MainSearchBox' -w $cpHwnd 2>$null
                        $pri = (Get-UiaProperty 'PrimaryCommandButton' 'Name' -Hwnd $cpHwnd)
                        if ($pri -and $pri -ne 'Open in default browser') {
                            return ($cur ? $cur : "<subpage:$pri>")
                        }
                        if ($cur -and $cur -ne $Alias -and $cur -notmatch '^Search for apps') {
                            return $cur
                        }
                        return $null
                    }
            } catch {
                # Escalate to AppX restart.
                if (Reset-CmdPalAppXIfDegraded) {
                    return Invoke-CmdPalAlias -Alias $Alias -NavTimeoutMs $NavTimeoutMs -_AlreadyRecovered $true
                }
                throw
            }
        }
        throw
    }
}

# Local poll wrapper that fixes the Hwnd parameter to $cpHwnd. Tests just
# call Wait-CmdPalListItem 'foo' instead of repeating -Hwnd everywhere.
function Wait-CmdPalListItem {
    param([Parameter(Mandatory)][string]$ExpectedName, [int]$TimeoutMs = 3000, [int]$PollMs = 200)
    Wait-UiaListItem -Hwnd $cpHwnd -ExpectedName $ExpectedName -TimeoutMs $TimeoutMs -PollMs $PollMs
}

# Semantic "type X, expect ListItem Y" wrapper. Combines the most common
# pair of operations in CmdPal tests: type a query into the search box,
# then wait for a specific ListItem name to appear in the result tree.
# Throws with a clear message if Y never shows up. Returns the matched
# item record so callers can read its selector / properties.
#
# Use this whenever the assertion is "typing X should produce ListItem Y":
#
#   Assert-CmdPalQueryReturns -Query 'calc'   -ExpectedItem 'Calculator'
#   Assert-CmdPalQueryReturns -Query '2+2'    -ExpectedItem '4'
#   Assert-CmdPalQueryReturns -Query 'lock'   -ExpectedItem 'Lock'
#
# NOT for queries that legitimately produce no Y (e.g. testing that a
# provider is gone after Disable). Those should use Wait-CmdPalListItem
# directly and check the $null return.
function Assert-CmdPalQueryReturns {
    param(
        [Parameter(Mandatory)][string]$Query,
        [Parameter(Mandatory)][string]$ExpectedItem,
        [int]$TimeoutMs = 3000
    )
    Invoke-CmdPalQuery $Query
    $hit = Wait-CmdPalListItem -ExpectedName $ExpectedItem -TimeoutMs $TimeoutMs
    if (-not $hit) {
        throw "After typing '$Query', no ListItem named '$ExpectedItem' appeared within ${TimeoutMs}ms"
    }
    return $hit
}

# Block-scoped sub-page navigation: enter via Invoke-CmdPalAlias, run the
# caller's scriptblock, ALWAYS reset to home on exit (try/finally —
# cleanup happens even if the scriptblock throws). Mirrors C# `using`
# for an IDisposable scope or JS `try/finally` ergonomics:
#
#   Use-CmdPalSubPage '=' {
#       Set-UiaText 'MainSearchBox' '5+7' -Hwnd $cpHwnd -VerifyEcho
#       $hit = Wait-CmdPalListItem '12'
#       Invoke-UiaAction 'PrimaryCommandButton' invoke -Hwnd $cpHwnd
#   }
#   # ← Reset-CmdPalToHome auto-runs here, even if the body threw
#
# Replaces the per-test pattern of "alias → work → reset in -Cleanup".
function Use-CmdPalSubPage {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position=0)][string]$Alias,
        [Parameter(Mandatory, Position=1)][scriptblock]$ScriptBlock
    )
    $placeholder = Invoke-CmdPalAlias $Alias
    if (-not $placeholder) { throw "'$Alias' alias did not navigate to a sub-page" }
    try {
        return & $ScriptBlock
    } finally {
        try { Reset-CmdPalToHome } catch {}
    }
}

# Drive selection (the [highlighted] item) to the ListItem whose Name matches
# $ExpectedItemName, by pressing Down/Up keys via PostMessage until the
# bottom-bar PrimaryCommandButton has Name == $ExpectedPrimaryName.
#
# Background: CmdPal's bottom-bar PrimaryCommandButton always reflects the
# currently-SELECTED ListItem (not the one with keyboard focus). In CmdPal
# 0.99+ the home-page Web Search provider returns 'Search the web in
# Microsoft Edge' as the first enabled item, so Primary defaults to 'Open in
# default browser' even when Calculator/Files/etc. also returned a result.
# UIA SetFocus does NOT change WinUI ListBox SelectedIndex; only a real
# click or arrow key does. SendInput (used by Send-PtKey) is blocked from
# elevated test scripts to non-elevated AppX targets via UIPI. PostMessage
# (Send-PtKeyToWindow) bypasses that restriction by going through the
# window's message queue.
#
# After PR #48033 (CmdPal 0.99.99+) prefer Find-CmdPalProviderItem + direct
# invoke when the target row has a stable AutomationId:
#   $tile = Find-CmdPalProviderItem 'com.microsoft.cmdpal.calculator'
#   winapp ui invoke $tile.selector -w $cpHwnd | Out-Null
# That skips the Down-key loop entirely (saves up to MaxDownPresses*SettleMs).
#
# This function is still required for rows WITHOUT stable AutomationIds:
#   - Files provider per-row results (3 + 03-Files.tests.ps1)
#   - Calculator RESULT rows (the '4' for '2+2', the '887112' for '999*888')
#   - System provider result rows ('Shutdown computer', 'Lock workstation')
#   - Registry HKEY_* root keys on the ':' sub-page
#   - Most provider result rows on sub-pages
# In 0.99.99, only provider TILES on home page (typed by name) carry IDs;
# the per-result rows do not. See Find-CmdPalProviderItem .NOTES for the
# full list of when the new pattern works.
#
# Returns $true if the desired Primary was reached, $false if we ran out
# of attempts (caller decides whether that's fatal).
function Select-CmdPalListItemByDownKey {
    param(
        [Parameter(Mandatory)][string]$ExpectedPrimaryName,   # e.g. 'Copy'
        [int]$MaxDownPresses = 8,
        [int]$SettleMs       = 250
    )
    # Check current Primary before pressing anything
    $p = (Get-UiaProperty 'PrimaryCommandButton' 'Name' -Hwnd $cpHwnd)
    if ($p -eq $ExpectedPrimaryName) { return $true }
    for ($i = 1; $i -le $MaxDownPresses; $i++) {
        Send-PtKeyToWindow -Hwnd $cpHwnd -Key 'down'
        Start-Sleep -Milliseconds $SettleMs
        $p = (Get-UiaProperty 'PrimaryCommandButton' 'Name' -Hwnd $cpHwnd)
        if ($p -eq $ExpectedPrimaryName) { return $true }
    }
    return $false
}

# Type a query into MainSearchBox (current page — does NOT navigate) and
# wait until the expected ListItem appears. Replaces the manual pattern of:
#
#     winapp ui set-value 'MainSearchBox' 'foo' -w $cpHwnd 2>$null | Out-Null
#     Start-Sleep -Milliseconds 800   # blind hope-it's-ready wait
#     $r = winapp ui search 'bar' -w $cpHwnd --json | ConvertFrom-Json
#     $hit = $r.matches | Where-Object { $_.name -eq 'bar' } | Select -First 1
#     if (-not $hit) { throw '...' }
#
# That pattern is HIGH RISK on slow boxes: the blind 800ms sleep is the
# only gate, and Wait-Until's TimeoutMs is on the SEARCH side only. On a
# slow CI runner the WinUI 3 TextChanged debounce can stretch to 1-2s, so
# the search executes before any results land.
#
# This helper combines both into one slow-mode-aware call:
#   1. Set the search box text.
#   2. Wait-CmdPalListItem (Wait-Until under the hood, SlowFactor auto-applies).
#   3. Throw a descriptive error on timeout, or return the matched item.
#
# Use this whenever the test pattern is "type X in current sub-page, then
# expect ListItem Y". For home-page queries that need a Reset-CmdPalToHome
# first, use Assert-CmdPalQueryReturns (calls Invoke-CmdPalQuery which
# does the reset).
function Set-CmdPalQueryAndWait {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position=0)][string]$Query,
        [Parameter(Mandatory, Position=1)][string]$ExpectedItem,
        [int]$TimeoutMs = 3000
    )
    winapp ui set-value 'MainSearchBox' $Query -w $cpHwnd 2>$null | Out-Null
    $hit = Wait-CmdPalListItem -ExpectedName $ExpectedItem -TimeoutMs $TimeoutMs
    if (-not $hit) {
        throw "After typing '$Query' on current page, no ListItem named '$ExpectedItem' appeared within $([int]($TimeoutMs * (Get-WinAppCliSlowFactor)))ms (factor=$(Get-WinAppCliSlowFactor))"
    }
    return $hit
}

# Wait until the system clipboard changes away from $PriorValue (typically
# a known sentinel set by the test) — proves the previous Copy action ran
# to completion. Replaces the brittle pattern of:
#
#     Invoke-UiaAction 'PrimaryCommandButton' invoke -Hwnd $cpHwnd   # Copy
#     Start-Sleep -Milliseconds 800   # blind hope-clipboard-latched
#     $val = Get-ClipboardSafe
#
# OLE clipboard writes are async — the "Copy" UI action returns immediately
# but the clipboard contents may take 200ms-2s to land, longer on slow CI.
# This helper polls every PollMs and returns the new value as soon as it's
# different from $PriorValue. Throws on timeout with a clear diagnostic.
#
# SlowFactor auto-applies via Wait-Until. Default 3s budget × factor.
# If ExpectedValue is provided, also asserts the new clipboard == expected
# (catches the "clipboard changed to wrong value" case in one helper).
function Wait-ClipboardChange {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position=0)][AllowEmptyString()][string]$PriorValue,
        [string]$ExpectedValue = $null,
        [int]$TimeoutMs = 3000
    )
    $newValue = Wait-Until -TimeoutMs $TimeoutMs -PollMs 100 -IgnoreException `
        -Message "Clipboard did not change away from prior value '$PriorValue'" `
        -Condition {
            $cur = Get-ClipboardSafe
            if ($cur -ne $PriorValue) { return ,$cur }   # comma operator: preserve '' as truthy
            $null
        }
    # Wait-Until throws on timeout, so $newValue is the post-change value.
    # Comma-operator returns a single-element array; unwrap if needed.
    if ($newValue -is [array]) { $newValue = $newValue[0] }
    if ($PSBoundParameters.ContainsKey('ExpectedValue') -and $newValue -ne $ExpectedValue) {
        throw "Clipboard changed to '$newValue' but expected '$ExpectedValue'"
    }
    return $newValue
}

# Find a CmdPal element by its AutomationId, introduced by PR #48033 (CmdPal 0.99.99+).
# Returns the first match record (with .selector, .automationId, .name etc.) or
# $null if absent. Pass -All to get every match.
#
# When this works (verified 2026-05-22 on CmdPal 0.11.11411.0 fresh restart):
#   - Provider TILES on home page after typing the provider's NAME:
#       'calc'      -> com.microsoft.cmdpal.calculator
#       'time'      -> com.microsoft.cmdpal.timedate
#       'settings'  -> com.microsoft.cmdpal.windowsSettings
#       'registry'  -> com.microsoft.cmdpal.registry
#       'run'       -> com.microsoft.cmdpal.run
#       'window'    -> com.microsoft.cmdpal.windowwalker
#       'web'       -> com.microsoft.cmdpal.websearch
#       'clipboard' -> com.microsoft.cmdpal.clipboardHistory
#       'winget'    -> com.microsoft.cmdpal.winget
#   - Always-present fallbacks (any non-empty query on home):
#       com.microsoft.cmdpal.builtin.websearch.execute.fallback
#       com.microsoft.cmdpal.builtin.remotedesktop.openrdp
#   - Static fallback commands (some queries):
#       com.microsoft.cmdpal.opensettings, com.microsoft.cmdpal.opengallerysettings,
#       com.microsoft.cmdpal.reload, etc.
#   - AllApps items: '<AppName>_<HashCode>' e.g. 'Notepad_9731657500416794521'
#
# When this DOES NOT work:
#   - Provider RESULT ROWS — e.g. calc result '4' for query '2+2', System
#     'Shutdown computer' for query 'shutdown', Files results for query
#     'foo.txt', Registry HKEY_* root keys on the ':' sub-page, etc. These
#     rows carry empty AutomationIds in 0.99.99; keep the legacy
#     `inspect 'ItemsList' + regex` pattern for those.
#   - Sub-page items in general (Time/Date formatted rows, Calc results,
#     WindowsSettings entries after typing 'about' / 'bluetooth' / etc.).
#
# Example usage:
#   Invoke-CmdPalQuery 'calc'
#   $tile = Find-CmdPalProviderItem 'com.microsoft.cmdpal.calculator'
#   if (-not $tile) { throw 'Calculator tile not present after typing "calc"' }
#   winapp ui invoke $tile.selector -w $cpHwnd | Out-Null  # opens Calc sub-page
function Find-CmdPalProviderItem {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position=0)][string]$Id,
        [int64]$Hwnd = $cpHwnd,
        [switch]$All
    )
    $raw = winapp ui search $Id -w $Hwnd --json 2>$null
    if (-not $raw) { return $null }
    try {
        $r = $raw | ConvertFrom-Json -ErrorAction Stop
    } catch {
        return $null
    }
    if (-not $r -or -not $r.PSObject.Properties.Name.Contains('matchCount') -or $r.matchCount -eq 0) {
        return $null
    }
    if ($All) { return @($r.matches) }
    return $r.matches[0]
}

