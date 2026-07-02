#Requires -Version 7.0
# 09-WindowWalker.tests.ps1 — extracted from command-palette-checklist.ps1 during Phase 2b split.
# Dot-sourced from the orchestrator so it shares script scope ($cpHwnd,
# $cpSettings, $cpEnabled, $cpDataDir). See _helpers.ps1 for the
# CmdPal-specific helper functions these tests call into.
# ── Box L1029-L1030: Window Walker switches to an open window ────
# Spawn notepad, drive Walker via '<' alias, verify 'Untitled - Notepad'
# ListItem is present with Primary 'Switch to'. Don't invoke (foreground
# assertion needs an interactive desktop). AAA's Cleanup KILLS the
# spawned notepad — even on Assert failure.
Test-Case 'CmdPal_WindowWalker_FindsOpenWindowByTitle' "Box L1029-L1030: Window Walker '<' alias finds a real open window (FUNCTIONAL — safe, doesn't invoke)" {
    # Arrange — spawn notepad and wait for a NEW notepad process with a
    # visible top-level window. On Win11 22H2+ `Start-Process notepad`
    # may launch a UWP-stub that exits in <1s and the real notepad spawns
    # asynchronously as a different PID; we must discover that PID by
    # diff'ing the process list and wait for ITS MainWindowHandle.
    $beforeIds = @(Get-Process notepad -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
    $null = Start-Process notepad -PassThru   # discard stub PID
    $np = Wait-Until -TimeoutMs 8000 -PollMs 200 -IgnoreException `
        -Message "no new notepad process with a window appeared within 8s" `
        -Condition {
            $candidates = @(Get-Process notepad -ErrorAction SilentlyContinue |
                Where-Object { $_.Id -notin $beforeIds })
            foreach ($c in $candidates) {
                $c.Refresh()
                if ($c.MainWindowHandle -ne 0) { return $c }
            }
            $null
        }
    Assert-NotNull $np -Because 'WindowWalker fixture: failed to spawn notepad with a window'
    # Give Walker's WinEventHook (EVENT_OBJECT_NAMECHANGE) a tick to index
    # the new window after MainWindowHandle becomes non-zero.
    Start-Sleep -Milliseconds 400
    try {
    # Act
        $placeholder = Invoke-CmdPalAlias '<'
        # Tightened from substring `open windows` (rev-8) to a more specific
        # pattern that anchors the expected wording. Window Walker's placeholder
        # across CmdPal 0.99/0.100 is "Search open windows" or "Search for open
        # windows" — both should match this anchored, case-insensitive pattern.
        Assert-Match $placeholder '(?i)^search\s+(for\s+)?open\s+windows' -Because "expected Walker sub-page placeholder 'Search [for] open windows...'"
        
        # The bottom-bar Primary button takes some milliseconds to populate
        # after sub-page navigation. Use Wait-Until (slow-factor-aware) so
        # the 2s budget scales with WINAPPCLI_SLOW_FACTOR for CI runners.
        $null = Wait-Until -TimeoutMs 2000 -PollMs 150 -IgnoreException `
            -Message "On Walker sub-page Primary did not become 'Switch to'" `
            -Condition {
                $p = Get-UiaProperty 'PrimaryCommandButton' 'Name' -Hwnd $cpHwnd
                if ($p -eq 'Switch to') { return $p }
                $null
            }
        $primaryBefore = Get-UiaProperty 'PrimaryCommandButton' 'Name' -Hwnd $cpHwnd
        Assert-Equal $primaryBefore 'Switch to' -Because 'On Walker sub-page Primary should be Switch to after 2s'
        
        # Set query + wait for ListItem in a single slow-factor-aware call.
        $hit = Set-CmdPalQueryAndWait -Query 'Untitled' -ExpectedItem 'Untitled - Notepad' -TimeoutMs 3000
    # Assert
        Assert-True $hit -Because "Window Walker did not list 'Untitled - Notepad' for our spawned notepad PID $($np.Id) within 3s"
        Write-Host "    info: Window Walker found 'Untitled - Notepad'" -ForegroundColor DarkGray
    } finally {
    # Cleanup
        if ($np) { try { $np.Kill() } catch { Write-Warning "[cleanup] failed to kill spawned notepad PID $($np.Id): $($_.Exception.Message)" } }
                    Reset-CmdPalToHome
    }
}
