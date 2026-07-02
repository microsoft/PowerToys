#Requires -Version 7.0
# 04-TimeDate-Home.tests.ps1 — extracted from command-palette-checklist.ps1 during Phase 2b split.
# Dot-sourced from the orchestrator so it shares script scope ($cpHwnd,
# $cpSettings, $cpEnabled, $cpDataDir). See _helpers.ps1 for the
# CmdPal-specific helper functions these tests call into.
# Recipe: query 'time' → invoke (Open) → land on Time-and-date sub-page →
# capture first ListItem name (= what Copy will write) → invoke Copy →
# verify clipboard equals that name. AAA makes the sub-page navigation
# explicit in Act, the clipboard verify in Assert, and the home-restore
# in Cleanup.
#
# Wrapped in Use-CmdPalProviderEnabled (datetime provider). Note: the
# other TimeDate test (DirectAliasOpensProvider) is far below in the
# file and gets its own independent fixture — both probes are cheap
# (~2.5s each) when the provider is already responsive.
$_timeDateTest1Ids = @('CmdPal_TimeDate_CopiesFirstValueToClipboardOnEnter')
$_registerTimeDateTest1 = {
Test-Case 'CmdPal_TimeDate_CopiesFirstValueToClipboardOnEnter' "Box L1028 ★ FULL: Time/Date 'Time' value is COPIED to clipboard (FUNCTIONAL e2e)" {
    Use-CmdPalClipboardSnapshot -Body {
    # Arrange
    $sentinel = "WINAPPCLI_TIME_SENTINEL_$(Get-Random)"
    Set-ClipboardSafe $sentinel | Out-Null
    try {
    # Act
        # Open the Time and date provider page from home
        Invoke-CmdPalQuery 'time'
        $tdItem = Wait-CmdPalListItem -ExpectedName 'Time and date' -TimeoutMs 3000
        Assert-True $tdItem -Because "'Time and date' provider not found on home page after typing 'time'"
        $homePri = (Get-UiaProperty 'PrimaryCommandButton' 'Name' -Hwnd $cpHwnd)
        Assert-Equal $homePri 'Open' -Because "home Primary on 'time' query"
        winapp ui invoke 'PrimaryCommandButton' -w $cpHwnd 2>$null | Out-Null
        # Wait for sub-page Primary to flip to 'Copy' instead of
        # blind 600ms — sub-page transition can take longer on
        # slow boxes.
        $subPri = Wait-Until -TimeoutMs 3000 -PollMs 150 -IgnoreException `
            -Message "Time-and-date sub-page Primary did not become 'Copy' within 3s" `
            -Condition {
                $p = Get-UiaProperty 'PrimaryCommandButton' 'Name' -Hwnd $cpHwnd
                if ($p -eq 'Copy') { return $p }
                $null
            }
        Assert-Equal $subPri 'Copy' -Because 'On Time-and-date sub-page, Primary should be Copy'

        # Capture the first ListItem name = expected clipboard. Use text-mode
        # inspect because --json returns the window root on this sub-page,
        # not the ItemsList subtree.
        $insLines = (winapp ui inspect 'ItemsList' -w $cpHwnd --depth 4 2>$null) -split "`n"
        foreach ($ln in $insLines) {
            if ($ln -match 'ListItem "([^"]+)"') { $expectedClip = $matches[1]; break }
        }
        Assert-NotNull $expectedClip -Because 'No ListItem found on Time-and-date sub-page'
        Write-Host "    info: first item name = '$($expectedClip)' (this is what Copy should write)" -ForegroundColor DarkGray

        # Invoke Copy
        winapp ui invoke 'PrimaryCommandButton' -w $cpHwnd 2>$null | Out-Null
        # Wait for clipboard to land (slow-factor-aware) instead of blind 800ms.
        Wait-ClipboardChange -PriorValue $sentinel -ExpectedValue $expectedClip -TimeoutMs 3000 | Out-Null
    # Assert — Wait-ClipboardChange already enforced ExpectedValue
        $after = Get-ClipboardSafe
        Assert-Equal $after $expectedClip -Because 'clipboard after Time-and-date Copy'
        Write-Host "    info: clipboard = '$after' ✓ (matches Time-and-date first item)" -ForegroundColor DarkGray
    } finally {
    # Cleanup — return to home (clipboard restored by outer scope helper)
        Reset-CmdPalToHome
    }
    }
}
}  # end $_registerTimeDateTest1 scriptblock

if (Test-AnyTestWillRun -Ids $_timeDateTest1Ids) {
    Use-CmdPalProviderEnabled -ProviderId 'com.microsoft.cmdpal.builtin.datetime' -Body $_registerTimeDateTest1
} else {
    & $_registerTimeDateTest1
}
