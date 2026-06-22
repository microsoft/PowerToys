#Requires -Version 7.0
# 05-WebSearch.tests.ps1 — extracted from command-palette-checklist.ps1 during Phase 2b split.
# Dot-sourced from the orchestrator so it shares script scope ($cpHwnd,
# $cpSettings, $cpEnabled, $cpDataDir). See _helpers.ps1 for the
# CmdPal-specific helper functions these tests call into.
# ── Box L1032: Web search alias produces a search result ─────────
# CmdPal 0.99.99 behavior change: typing 'alias + text' in one set-value
# call (e.g. '?? hello world') does NOT navigate. Instead the literal
# string sits on home and produces an IndexedSearch 'Run command' first
# in the results. To navigate cleanly we type the alias ALONE, wait for
# the sub-page to load, then type the query on the sub-page. The same
# helper Use-CmdPalSubPage encapsulates this two-step flow.
Test-Case 'CmdPal_WebSearch_ReturnsResultForQuery' "Box L1032: Web search alias '?? hello world' returns a Web result (FUNCTIONAL)" {
    # Act — alias-then-query, two-step (single set-value doesn't navigate in 0.99.99)
    Use-CmdPalSubPage '??' {
        Set-UiaText 'MainSearchBox' 'hello world' -Hwnd $cpHwnd -VerifyEcho
        # Wait for the websearch fallback ListItem to land
        $null = Wait-Until -TimeoutMs 3000 -PollMs 200 -IgnoreException `
            -Message "WebSearch sub-page did not produce a result within 3s for 'hello world'" `
            -Condition {
                $r = winapp ui search 'web' -w $cpHwnd --json 2>$null | ConvertFrom-Json
                $r.matchCount -gt 0
            }
        # Assert
        $r = winapp ui search 'web' -w $cpHwnd --json 2>$null | ConvertFrom-Json
        Assert-GreaterThan $r.matchCount 0 -Because "Web search sub-page did not produce any 'web'-named result for 'hello world'"
    }
}

# ── Box L1032 ★ EXTENDED: Web search Primary action label ────────
# Pressing Enter on a web-search ListItem opens default browser. We verify
# the wiring by checking the bottom-bar Primary action label says 'Open in
# default browser' — that's what gets executed on Enter. We do NOT invoke
# (would actually open a browser tab).
#
# CmdPal 0.99.99 (verified 2026-05-23): on the '??' WebSearch sub-page
# with a non-empty query, PrimaryCommandButton.Name reliably becomes
# 'Open in default browser'. The change vs 0.10 is that we must navigate
# via two-step alias-then-query (Use-CmdPalSubPage), not the previous
# single 'Invoke-CmdPalQuery ??text' call (which now leaves us on home).
Test-Case 'CmdPal_WebSearch_PrimaryActionOpensDefaultBrowser' "Box L1032 ★ EXTENDED: Web search PrimaryCommandButton.Name == 'Open in default browser'" {
    Use-CmdPalSubPage '??' {
        # Act
        Set-UiaText 'MainSearchBox' 'hello world' -Hwnd $cpHwnd -VerifyEcho
        # Wait for Primary to settle to the WebSearch sub-page label
        $primaryName = Wait-Until -TimeoutMs 3000 -PollMs 200 -IgnoreException `
            -Message "PrimaryCommandButton did not become 'Open in default browser' within 3s on WebSearch sub-page" `
            -Condition {
                $p = winapp ui get-property 'PrimaryCommandButton' -w $cpHwnd --json 2>$null | ConvertFrom-Json -ErrorAction SilentlyContinue
                $n = if ($p -and $p.properties) { $p.properties.Name } else { '' }
                if ($n -match '(?i)default browser') { return $n }
                $null
            }
        # Assert — already enforced by Wait-Until's regex; double-check final value
        Assert-Match $primaryName '(?i)default browser' -Because "PrimaryCommandButton should mention 'default browser' on WebSearch sub-page"
        $prog = (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice' -ErrorAction SilentlyContinue).ProgId
        Write-Host "    info: Primary action = '$primaryName' (would launch '$prog')" -ForegroundColor DarkGray
    }
}
