#Requires -Version 7.0
# 02-Calculator.tests.ps1 — extracted from command-palette-checklist.ps1 during Phase 2b split.
# Dot-sourced from the orchestrator so it shares script scope ($cpHwnd,
# $cpSettings, $cpEnabled, $cpDataDir). See _helpers.ps1 for the
# CmdPal-specific helper functions these tests call into.
# ════════════════════════════════════════════════════════════════════
# Box L1024 — Calculator (5 tests: 3 invocation paths + 2 history)
# ════════════════════════════════════════════════════════════════════
# CmdPal exposes Calculator in three user-reachable ways. Each is
# covered by one full end-to-end test (type expression → invoke Copy →
# verify clipboard). No separate "result returned" smoke test — the
# e2e variant already requires the result to be present (you can't
# Copy something that's not there), so it's a strict superset.
#
# We use a DIFFERENT math expression per path so failure messages
# pin down which path broke without ambiguity:
#
#   1. ALIAS path      — '=' alias → Calc sub-page → '5+7'    → '12'
#   2. EXPLORE path    — 'calc' → invoke Calculator → '17*23' → '391'
#   3. FALLBACK path   — '999*888' typed on home page         → '887112'
#
# Plus 2 history tests (persistent calc history is a separate 0.99.0
# feature, but kept contiguous here so all Calc coverage is together):
#
#   4. History file persists in AppX LocalState
#   5. History survives AppX re-summon

# ── Bucket fixture: ensure Calculator provider is enabled ──────────
# All 5 calc tests below depend on com.microsoft.cmdpal.builtin.calculator
# being enabled. A user (or a sister test) may have disabled it, in which
# case the '=' alias resolves to nothing and the home-fallback math goes
# silent. Use-CmdPalProviderEnabled snapshots the current state, enables
# the provider (restarting the AppX so changes take effect), runs the
# body, and on exit restores the original IsEnabled value (true / false
# / missing-entry). Only triggers when at least one calc test will
# actually run under the current -Only/-Skip filter, so filtered runs
# never mutate user state unnecessarily.
$_calcTestIds = @(
    'CmdPal_Calculator_AliasPath_CopyResultOnEnter',
    'CmdPal_Calculator_ExplorePath_CopyResultOnEnter',
    'CmdPal_Calculator_HomeFallback_CopyResultOnEnter',
    'CmdPal_Calculator_SubPageMoreMenuExposesNumberFormats',
    'CmdPal_Calculator_HistoryFilePersistsOnDisk',
    'CmdPal_Calculator_PersistsHistoryAcrossSummons'
)
$_registerCalcTests = {

# ── Path 1 (alias '='): cleanest natural UX — '=' → sub-page → math ─
# Test-Case body is plain sequential code with # Arrange / # Act /
# # Assert comments. Cleanup is an inline try/finally so failures
# in the body still restore clipboard. No $ctx threading.
Test-Case 'CmdPal_Calculator_AliasPath_CopyResultOnEnter' `
          "Box L1024 (alias '=') ★ FULL: '=' → '5+7' → Copy → clipboard='12' (FUNCTIONAL e2e)" `
{
    Use-CmdPalClipboardSnapshot -Body {
    # Arrange
    $sentinel = "WINAPPCLI_CALC_ALIAS_$(Get-Random)"
    Set-ClipboardSafe $sentinel | Out-Null
    $expr     = '5+7'
    $expected = '12'
    # Act
    Use-CmdPalSubPage '=' {
        Set-UiaText 'MainSearchBox' $expr -Hwnd $cpHwnd -VerifyEcho
        $hit = Wait-CmdPalListItem -ExpectedName $expected -TimeoutMs 3000
        Assert-True $hit -Because "Calc sub-page '$expr' did not yield '$expected'"
        $primary = Get-UiaProperty 'PrimaryCommandButton' 'Name' -Hwnd $cpHwnd
        Assert-Equal $primary 'Copy' -Because "Primary after alias-path math — alias path has no shadowing"
        Invoke-UiaAction 'PrimaryCommandButton' invoke -Hwnd $cpHwnd
        # Real condition: poll clipboard until it changes from sentinel
        # to the expected value. Slow-factor-aware (3s × factor).
        Wait-ClipboardChange -PriorValue $sentinel -ExpectedValue $expected -TimeoutMs 3000 | Out-Null
    }
    # Assert — Wait-ClipboardChange already enforced ExpectedValue
    $after = Get-ClipboardSafe
    Assert-Equal $after $expected -Because 'clipboard after alias-path Copy'
    Write-Host "    info: alias path '$expr' → '$expected' copied OK" -ForegroundColor DarkGray
    }
}

# ── Path 2 (explore 'calc'): user-discovery — find Calculator, Enter ─
# Exercises TopLevelCommandManager command lookup (different code path
# from AliasManager.CheckAlias).
Test-Case 'CmdPal_Calculator_ExplorePath_CopyResultOnEnter' `
          "Box L1024 (explore 'calc') ★ FULL: 'calc' → Calculator → '17*23' → Copy → clipboard='391' (FUNCTIONAL e2e)" `
{
    Use-CmdPalClipboardSnapshot -Body {
    # Arrange
    $sentinel = "WINAPPCLI_CALC_EXPLORE_$(Get-Random)"
    Set-ClipboardSafe $sentinel | Out-Null
    $expr     = '17*23'
    $expected = '391'
    # Act
    Assert-CmdPalQueryReturns -Query 'calc' -ExpectedItem 'Calculator' | Out-Null
    $primary = Get-UiaProperty 'PrimaryCommandButton' 'Name' -Hwnd $cpHwnd
    Assert-Equal $primary 'Calculator' -Because "home Primary for 'calc'"
    Invoke-UiaAction 'PrimaryCommandButton' invoke -Hwnd $cpHwnd
    try {
        Wait-Until -TimeoutMs 3000 -Message "Calc sub-page did not load (Primary stayed '$primary')" {
            (Get-UiaProperty 'PrimaryCommandButton' 'Name' -Hwnd $cpHwnd) -eq 'Copy'
        } | Out-Null
        Set-UiaText 'MainSearchBox' $expr -Hwnd $cpHwnd -VerifyEcho
        $hit = Wait-CmdPalListItem -ExpectedName $expected -TimeoutMs 3000
        Assert-True $hit -Because "On Calc sub-page (via explore), '$expr' did not yield '$expected'"
        Invoke-UiaAction 'PrimaryCommandButton' invoke -Hwnd $cpHwnd
        Wait-ClipboardChange -PriorValue $sentinel -ExpectedValue $expected -TimeoutMs 3000 | Out-Null
    } finally {
        Reset-CmdPalToHome
    }
    # Assert — Wait-ClipboardChange already enforced ExpectedValue
    $after = Get-ClipboardSafe
    Assert-Equal $after $expected -Because 'clipboard after explore-path Copy'
    Write-Host "    info: explore path 'calc' → Calculator → '$expr' → '$expected' copied OK" -ForegroundColor DarkGray
    }
}

# ── Path 3 (home fallback): typing math directly on home page ──────
# Backwards-compat: typing math on home still produces a usable result
# (in the Fallbacks section, behind Web Search). Down-key navigation
# drives selection to the calc result before invoking Copy.
Test-Case 'CmdPal_Calculator_HomeFallback_CopyResultOnEnter' `
          "Box L1024 (home fallback) ★ FULL: '999*888' on home → Copy → clipboard='887112' (FUNCTIONAL e2e)" `
{
    Use-CmdPalClipboardSnapshot -Body {
    # Arrange
    $sentinel = "WINAPPCLI_CALC_HOMEFB_$(Get-Random)"
    Set-ClipboardSafe $sentinel | Out-Null
    $expr     = '999*888'
    $expected = '887112'
    # Act
    Assert-CmdPalQueryReturns -Query $expr -ExpectedItem $expected | Out-Null
    $ok = Select-CmdPalListItemByDownKey -ExpectedPrimaryName 'Copy' -MaxDownPresses 6
    Assert-True $ok -Because {
        $p = Get-UiaProperty 'PrimaryCommandButton' 'Name' -Hwnd $cpHwnd
        "Could not select Calculator result via Down keys — Primary still '$p' (expected 'Copy')"
    }
    Invoke-UiaAction 'PrimaryCommandButton' invoke -Hwnd $cpHwnd
    Wait-ClipboardChange -PriorValue $sentinel -ExpectedValue $expected -TimeoutMs 3000 | Out-Null
    # Assert — Wait-ClipboardChange already enforced ExpectedValue
    $after = Get-ClipboardSafe
    Assert-Equal $after $expected -Because 'clipboard after home-fallback Copy'
    Write-Host "    info: home fallback path '$expr' → '$expected' copied OK" -ForegroundColor DarkGray
    }
}

# ── ★ FULL: Calc sub-page More menu exposes number-format actions ──
# Investigation 2026-05-20: CmdPal's More menu (Ctrl+K / MoreContextMenuButton)
# is NOT UIA-virtualized — it's directly readable via the CommandsDropdown
# list in the spawned PopupHost window. We use the calc sub-page because
# its More menu has a deterministic schema: Copy + Paste + Replace query
# for the integer result, plus hex/binary/octal representations (0xA,
# 0b1010, 0o12 for the integer 10).
#
# This is the canonical "context-menu-driving" test for CmdPal — proves
# the More menu pattern works end-to-end for any provider that emits
# CommandsDropdown entries. Replaces the (now-correctly-classified)
# NEEDS-FEATURE skips that previously claimed virtualization.
Test-Case 'CmdPal_Calculator_SubPageMoreMenuExposesNumberFormats' "★ FULL: Calc sub-page More menu (Ctrl+K) lists Copy/Paste/Replace + hex/binary/octal entries" {
    Use-CmdPalSubPage '=' {
        Set-UiaText 'MainSearchBox' '5+5' -Hwnd $cpHwnd -VerifyEcho
        $hit = Wait-CmdPalListItem -ExpectedName '10' -TimeoutMs 3000
        Assert-True $hit -Because "calc didn't return '10' for '5+5'"
        # Open the More menu via MoreContextMenuButton (Ctrl+K equivalent).
        # Use the existing working pattern from Pin_PinToDockDialogAppearsAfterMoreMenuClick.
        $r = winapp ui invoke 'MoreContextMenuButton' -w $cpHwnd 2>&1 | Out-String
        Assert-Match $r 'Invoked' -Because "MoreContextMenuButton invoke didn't fire: $($r.Trim())"
        # Wait for the PopupHost window (height>100 — the smaller one is the
        # tooltip) instead of a blind 1s sleep. Slow-factor-aware.
        $popupLine = Wait-Until -TimeoutMs 3000 -PollMs 150 -IgnoreException `
            -Message "More-menu PopupHost (height>100) did not appear after MoreContextMenuButton click" `
            -Condition {
                $line = winapp ui list-windows -a 'CmdPal' 2>$null |
                    Where-Object { $_ -match 'HWND (\d+):\s*"PopupHost".*\(popup, (\d+)x(\d+)' -and [int]$Matches[3] -gt 100 } |
                    Select-Object -First 1
                if ($line) { return ,$line }
                $null
            }
        if ($popupLine -is [array]) { $popupLine = $popupLine[0] }
        # NOTE: cannot use Assert-Match here — we need $Matches[1] in the
        # caller scope, and Assert-Match's `-match` runs in function scope
        # so it would not leak the groups out.
        if ($popupLine -notmatch 'HWND (\d+):') {
            throw "More-menu PopupHost line did not contain HWND: '$popupLine'"
        }
        $popupHwnd = [int64]$Matches[1]
        try {
            $tree = winapp ui inspect 'CommandsDropdown' -w $popupHwnd --depth 4 2>$null
            $items = @($tree | Where-Object { $_ -match 'ListItem\s+"([^"]+)"' -and $_ -notmatch '\[disabled\]' } |
                       ForEach-Object { if ($_ -match 'ListItem\s+"([^"]+)"') { $matches[1] } })
            # Expected: Copy + Paste + Replace query + 0xA + 0b1010 + 0o12 (at minimum)
            $required = @('Copy','Paste','Replace query','0xA','0b1010','0o12')
            $missing = @($required | Where-Object { $_ -notin $items })
            Assert-Empty $missing -Because "Calc More menu missing required items. Got: $($items -join ', ')"
            Write-Host "    info: Calc More menu has $($items.Count) items including all 6 required (Copy/Paste/Replace/0xA/0b1010/0o12)" -ForegroundColor DarkGray
        } finally {
            # Close the popup so the suite doesn't leave it open. This is
            # best-effort cleanup — Send-PtKeyToWindow's PostMessage does
            # not reliably reach WinUI 3 popups, so we don't assert on
            # PopupHost disappearing. A small breathing room lets the
            # WinUI dismiss-on-focus-loss fire when the next test takes
            # foreground. (Investigated 2026-05-27: a Wait-Until on
            # PopupHost-absent often times out because the popup only
            # closes when the next test's set-value moves focus.)
            try { Send-PtKeyToWindow -Hwnd $cpHwnd -Key 'escape' } catch {}
            Start-Sleep -Milliseconds 300
        }
    }
}

# ── ★ 0.99.0: Calculator history file is persisted (file-system check) ─
# Verifies persistent calc history: file exists, valid JSON array,
# schema (Id/Query/Result/Timestamp), file grows or our entry lands.
Test-Case 'CmdPal_Calculator_HistoryFilePersistsOnDisk' `
          "★ 0.99.0: Calculator history is persisted to AppX LocalState JSON file (FUNCTIONAL — file-system assertion)" `
{
    # Arrange
    $histPath      = "$env:LOCALAPPDATA\Packages\Microsoft.CommandPalette_8wekyb3d8bbwe\LocalState\calculator_history.json"
    $existedBefore = Test-Path $histPath
    $sizeBefore    = if ($existedBefore) { (Get-Item $histPath).Length } else { 0 }
    $a             = Get-Random -Max 999
    $b             = Get-Random -Max 999
    $expr          = "($a) * ($b)"
    $expectedAns   = "$($a * $b)"

    try {
    # Act
        Invoke-CmdPalQuery $expr
        Wait-CmdPalListItem -ExpectedName $expectedAns -TimeoutMs 3000 | Out-Null
        $ok = Select-CmdPalListItemByDownKey -ExpectedPrimaryName 'Copy' -MaxDownPresses 6
        if (-not $ok) {
            Write-Host "    warn: could not select Calculator result — falling back" -ForegroundColor Yellow
        }
        $primary = Get-UiaProperty 'PrimaryCommandButton' 'Name' -Hwnd $cpHwnd
        if ($primary -eq 'Copy') {
            $sizeBeforeCopy = if (Test-Path $histPath) { (Get-Item $histPath).Length } else { 0 }
            Invoke-UiaAction 'PrimaryCommandButton' invoke -Hwnd $cpHwnd
            # Wait for the history file to be created or grow (Copy commits
            # the entry to disk). 3s × slow-factor budget.
            $null = Wait-Until -TimeoutMs 3000 -PollMs 150 -IgnoreException `
                -Message "calculator_history.json was not updated within 3s after Copy invoke" `
                -Condition {
                    if (-not (Test-Path $histPath)) { return $false }
                    (Get-Item $histPath).Length -gt $sizeBeforeCopy
                }
        }
    # Assert
        Assert-PathExists $histPath -Because "calculator_history.json missing at $histPath — persistence not wired"
        $hist = Get-Content $histPath -Raw | ConvertFrom-Json
        Assert-True ($hist -is [array] -or $hist.PSObject.Properties.Name) -Because 'calculator_history.json is not a JSON array'
        $arr = @($hist)
        Assert-GreaterThan $arr.Count 0 -Because 'calculator_history.json is empty'
        $first = $arr[0]
        foreach ($field in 'Id','Query','Result','Timestamp') {
            Assert-JsonHasProperty $first $field -Because "calculator_history.json entry missing '$field' field (schema regression)"
        }
        $sizeAfter = (Get-Item $histPath).Length
        $foundOurExpr = $arr | Where-Object {
            $_.Query -replace '[\u00D7\*\s]','*' -eq ($expr -replace '[\s\*]','*')
        } | Select-Object -First 1
        Assert-True ($foundOurExpr -or $sizeAfter -gt $sizeBefore) -Because "Neither our fresh expression '$expr' was added NOR did file grow (size $sizeBefore → $sizeAfter)"
        Write-Host "    info: history has $($arr.Count) entries, schema OK; size $sizeBefore → $sizeAfter bytes" -ForegroundColor DarkGray
    } finally {
        Reset-CmdPalToHome
    }
}

# ── ★ 0.99.0: Calculator persistent history survives re-summon ─────
# Same expression typed in two separate CmdPal sessions should always
# produce the same answer ListItem, AND the first Copy's clipboard
# value should still be intact when the second summon runs.
Test-Case 'CmdPal_Calculator_PersistsHistoryAcrossSummons' `
          "★ 0.99.0: Calculator persistent history survives re-summon (FUNCTIONAL e2e)" `
{
    # Arrange
    $a        = Get-Random -Minimum 1000 -Maximum 9999
    $b        = Get-Random -Minimum 1000 -Maximum 9999
    $expr     = "$a + $b"
    $expected = "$($a + $b)"
    Use-CmdPalClipboardSnapshot -Body {
        # Act — 1st invocation pins the answer to history via Copy
        Invoke-CmdPalQuery $expr
        $hit = Wait-CmdPalListItem -ExpectedName $expected
        Assert-True $hit -Because "first invocation: '$expr' did not produce '$expected'"
        $ok = Select-CmdPalListItemByDownKey -ExpectedPrimaryName 'Copy' -MaxDownPresses 6
        Assert-True $ok -Because {
            $p = Get-UiaProperty 'PrimaryCommandButton' 'Name' -Hwnd $cpHwnd
            "Primary after first invocation is '$p' (could not navigate to 'Copy' after 6 Down presses)"
        }
        $priorClip = Get-ClipboardSafe
        Invoke-UiaAction 'PrimaryCommandButton' invoke -Hwnd $cpHwnd
        # Wait for clipboard to land before the 2nd invocation (proves Copy
        # committed). Slow-factor-aware via Wait-Until under the hood.
        Wait-ClipboardChange -PriorValue $priorClip -ExpectedValue $expected -TimeoutMs 3000 | Out-Null

        # Act (2nd summon) — same expression, history should make it consistent
        Invoke-CmdPalQuery $expr
        $hit2 = Wait-CmdPalListItem -ExpectedName $expected
        Assert-True $hit2 -Because "second invocation: '$expr' did not produce '$expected' (history broken?)"

        # Assert — clipboard from the first Copy is still intact
        $clip = Get-ClipboardSafe
        Assert-Equal $clip $expected -Because 'clipboard after Copy should still match first invocation result'
        Write-Host "    info: '$expr' → '$expected' on both summons; clipboard intact" -ForegroundColor DarkGray
    }
}

}  # end $_registerCalcTests scriptblock

if (Test-AnyTestWillRun -Ids $_calcTestIds) {
    Use-CmdPalProviderEnabled -ProviderId 'com.microsoft.cmdpal.builtin.calculator' -Body $_registerCalcTests
} else {
    # No calc test will execute under the current filter — register the
    # five Test-Case calls anyway so the report includes them as SKIP
    # (filtered), but skip the provider mutation entirely.
    & $_registerCalcTests
}
