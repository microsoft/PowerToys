#Requires -Version 7.0
# 10-Shell.tests.ps1 — extracted from command-palette-checklist.ps1 during Phase 2b split.
# Dot-sourced from the orchestrator so it shares script scope ($cpHwnd,
# $cpSettings, $cpEnabled, $cpDataDir). See _helpers.ps1 for the
# CmdPal-specific helper functions these tests call into.
# ── Box L1028: Run Commands shell provider via 'run' query ──────────
# CmdPal 0.99.99 (0.11.11411) regression: the '>' alias no longer navigates
# when typed via set-value (verified 2026-05-23 — all 7 other aliases work,
# only '>' is broken: typing '>' leaves it as a literal char in the box).
# The sibling test CmdPal_Shell_RunCommandActuallyExecutes currently passes
# by accident because typing 'notepad' on home produces an identical
# "Run command" fallback row that, when invoked, spawns notepad.exe.
#
# This test was updated to drive the Shell provider via the new PR #48033
# pattern: type 'run' on home to surface the com.microsoft.cmdpal.run
# tile, then invoke PrimaryCommandButton (which is then labelled
# 'Run commands') to navigate to the Shell sub-page. Verify the sub-page
# placeholder is the Shell-specific one. This is strictly stronger than
# the old alias check because it confirms both (a) the Run commands tile
# carries its stable AutomationId and (b) invoking the tile actually
# navigates to the Shell provider sub-page (not a fallback).
Test-Case 'CmdPal_Shell_AliasOpensProviderWithRunPrimary' "Box L1028: Run commands provider tile + invoke opens Shell sub-page with command placeholder (FUNCTIONAL — safe, doesn't invoke a command)" {
    try {
    # Act
        Invoke-CmdPalQuery 'run'
        # Wait for the Run commands tile to surface via the new ID
        $null = Wait-Until -TimeoutMs 3000 -PollMs 200 -IgnoreException `
            -Message "com.microsoft.cmdpal.run tile did not surface within 3s after typing 'run'" `
            -Condition { (Find-CmdPalProviderItem 'com.microsoft.cmdpal.run') -ne $null }
        $tile = Find-CmdPalProviderItem 'com.microsoft.cmdpal.run'
        Assert-NotNull $tile -Because 'Run commands tile not found after Wait-Until passed'
        Assert-Equal $tile.name 'Run commands' -Because 'Run commands tile Name'

        # PrimaryCommandButton.Name reflects the home tile's action label.
        # In 0.99.99 this is 'Run commands' (the navigation target, not 'Open').
        $homePrimary = Get-UiaProperty 'PrimaryCommandButton' 'Name' -Hwnd $cpHwnd
        Assert-Equal $homePrimary 'Run commands' -Because "Home PrimaryCommandButton after typing 'run'"
        # Invoke navigates to the Shell sub-page
        Invoke-UiaAction 'PrimaryCommandButton' invoke -Hwnd $cpHwnd

        # Wait for the sub-page transition — Shell sub-page has a distinctive
        # placeholder. Real state-change signal (not blind sleep).
        # Tightened from loose `command|run|name of a` (rev-8) to anchored
        # phrases that the Shell page's placeholder actually uses across
        # CmdPal 0.99/0.100. The home-page placeholder is intentionally
        # excluded so a no-op transition is caught as failure.
        $homePh = 'Search for apps, files and commands'
        $shellPh = '(?i)(name of a command|type a command|run command|run a command)'
        $shellPlaceholder = Wait-Until -TimeoutMs 3000 -PollMs 200 -IgnoreException `
            -Message "Shell sub-page did not load within 3s (placeholder did not become a command-prompt one)" `
            -Condition {
                $p = winapp ui get-property 'MainSearchBox' -w $cpHwnd --json 2>$null | ConvertFrom-Json -ErrorAction SilentlyContinue
                $n = if ($p) { $p.properties.Name } else { '' }
                if ($n -and $n -ne $homePh -and $n -match $shellPh) { return $n }
                $null
            }
    # Assert — Shell sub-page placeholder mentions command/run/name-of-a
        Assert-True $shellPlaceholder -Because 'Shell sub-page never reached — placeholder did not become a command-prompt one'
        Assert-Match $shellPlaceholder $shellPh -Because "Shell sub-page placeholder '$shellPlaceholder' should mention 'command' / 'name of a command'"
        Assert-NotEqual $shellPlaceholder $homePh -Because 'placeholder must differ from home page (transition really happened)'
        Write-Host "    info: 'run' tile -> invoke -> Shell sub-page (placeholder='$shellPlaceholder')" -ForegroundColor DarkGray
    } finally {
    # Cleanup
        Reset-CmdPalToHome
    }
}

# ── Box L1028 ★ FULL: Shell '>' alias actually EXECUTES a command ────
# Stronger sibling of CmdPal_Shell_AliasOpensProviderWithRunPrimary — that
# test proves the sub-page opens and Primary='Run'; this one proves Run
# actually launches the named program. Uses 'notepad' (single token, GUI
# process, easy to identify + kill via process name); ipconfig is
# tempting but exits in <1s and may be missed by our process snapshot.
# We snapshot the process list before invoking, capture any spawned
# processes via Get-ProcessesStartedAfter, then kill them in cleanup
# so a failed test doesn't leak a notepad window.
Test-Case 'CmdPal_Shell_RunCommandActuallyExecutes' "Box L1028 ★ FULL: '>' alias actually executes 'notepad' (spawns process, cleanup kills it)" {
    # Arrange
    $sinceTime = Get-Date
    $spawned   = @()
    try {
        # Act — enter '>' sub-page, type 'notepad', invoke Primary (=Run)
        Use-CmdPalSubPage '>' {
            Set-UiaText 'MainSearchBox' 'notepad' -Hwnd $cpHwnd -VerifyEcho
            # Wait for the result list to populate AND Primary to become 'Run'
            # (the Shell sub-page promotes Run as the Primary action when
            # there's at least one runnable command). Replaces a blind 500ms
            # sleep that under-waited on slow boxes (intermittent fail) and
            # over-waited on fast ones.
            $null = Wait-Until -TimeoutMs 2000 -PollMs 100 -IgnoreException `
                -Message "Shell sub-page Primary did not become 'Run' within 2s after typing 'notepad'" `
                -Condition { (Get-UiaProperty 'PrimaryCommandButton' 'Name' -Hwnd $cpHwnd) -eq 'Run' }
            $primary = Get-UiaProperty 'PrimaryCommandButton' 'Name' -Hwnd $cpHwnd
            Assert-Equal $primary 'Run' -Because 'Shell sub-page Primary should be Run before invoke'
            # This is the REAL execution. Don't just check the label.
            Invoke-UiaAction 'PrimaryCommandButton' invoke -Hwnd $cpHwnd
            # Wait for notepad to spawn — race-hider replaced with real
            # condition: poll for the process. Cold-start is ~500-1500ms,
            # warm ~200ms; 5s deadline covers both with margin.
            $null = Wait-Until -TimeoutMs 5000 -PollMs 200 -IgnoreException `
                -Message "notepad.exe did not spawn within 5s after Shell '>' Run invoke" `
                -Condition {
                    @(Get-ProcessesStartedAfter -Since $sinceTime -Name 'notepad').Count -gt 0
                }
        }
        # Assert — at least one notepad.exe started AFTER our timestamp
        $spawned = @(Get-ProcessesStartedAfter -Since $sinceTime -Name 'notepad')
        Assert-GreaterThan $spawned.Count 0 -Because "Shell '>' Run action did not spawn notepad.exe within 5s (no new processes named 'notepad' since $sinceTime)"
        Write-Host "    info: Shell '>' executed 'notepad' — spawned $($spawned.Count) notepad process(es): $($spawned.Id -join ', ')" -ForegroundColor DarkGray
    } finally {
        # Cleanup — KILL spawned processes (so even a failed assertion
        # doesn't leak a notepad window or a notepad waiting for input).
        if ($spawned.Count -gt 0) {
            $spawned | Stop-ProcessesSafely -Reason 'Shell_RunCommandActuallyExecutes test'
        }
        Reset-CmdPalToHome
    }
}
