#Requires -Version 7.0
# 21-Pin.tests.ps1 — extracted from command-palette-checklist.ps1 during Phase 2b split.
# Dot-sourced from the orchestrator so it shares script scope ($cpHwnd,
# $cpSettings, $cpEnabled, $cpDataDir). See _helpers.ps1 for the
# CmdPal-specific helper functions these tests call into.
# ── 0.99.0 PR #46436 — Pin-to-Dock dialog appears via context menu ──
# After enabling Dock, search for a command, open the More context menu,
# find the "Pin to dock" entry, click it, verify the popup containing
# the new Pin-to-Dock dialog appears. This exercises the new dialog
# codepath from PR #46436 (the title/subtitle toggles + dock position
# picker). We back up + restore EnableDock so user state is preserved.
Test-Case 'CmdPal_Pin_PinToDockDialogAppearsAfterMoreMenuClick' "★ 0.99.0 PR #46436 — Pin to dock entry in More context menu opens the pin-to-dock dialog popup" {
    # Arrange
            $backup = Backup-CmdPalSettingsJson
    Edit-CmdPalSettingsAndRestart -Mutator {
    param($obj)
    $obj.EnableDock = $true
    } | Out-Null
    try {
    # Act
        try {
                        Invoke-CmdPalQuery -Query 'notepad'
                    } catch {
                        throw "could not echo 'notepad' query: $($_.Exception.Message)"
                    }
                    # Wait for a notepad.exe ListItem instead of blind 800ms.
                    $null = Wait-CmdPalListItem -ExpectedName 'notepad.exe' -TimeoutMs 3000

                    # Click More context menu button
                    $r = & winapp ui invoke 'MoreContextMenuButton' -w $cpHwnd 2>&1 | Out-String
                    Assert-Match $r 'Invoked' -Because "MoreContextMenuButton invoke didn't fire: $($r.Trim())"

                    # Wait for popup window instead of blind 1s sleep.
                    $popupLine = Wait-Until -TimeoutMs 3000 -PollMs 150 -IgnoreException `
                        -Message 'PopupHost window did not appear after MoreContextMenuButton click' `
                        -Condition {
                            $line = & winapp ui list-windows -a 'CmdPal' 2>$null |
                                Where-Object { $_ -match 'HWND (\d+):\s*"PopupHost"' } |
                                Select-Object -First 1
                            if ($line) { return ,$line }
                            $null
                        }
                    if ($popupLine -is [array]) { $popupLine = $popupLine[0] }
                    # NOTE: raw `-match` here (not Assert-Match) — we need $Matches[1] to leak to caller.
                    if ($popupLine -notmatch 'HWND (\d+):') {
                        throw "PopupHost line did not contain HWND: '$popupLine'"
                    }
                    $popupHwnd = [int64]$Matches[1]
                    Write-Host "    info: popup HWND=$popupHwnd" -ForegroundColor DarkGray
        
                    # Inspect popup for any Pin-related item (resource strings:
                    # dock_pin_command_name + top_level_pin_command_name).
                    # REASON: CommandsDropdown popup items don't have stable
                    # AutomationIds in CmdPal 0.99.99 (PR #48033 didn't cover
                    # popups). Keep inspect+regex for popup menu enumeration.
                    $tree = & winapp ui inspect 'CommandsDropdown' -w $popupHwnd --depth 4 2>$null
                    $pinItems = @($tree | Where-Object { $_ -match 'ListItem\s+"(Pin to (?:dock|home)|Unpin from (?:dock|home))"' })
                    Assert-GreaterThan $pinItems.Count 0 -Because "no Pin-related ListItem in context menu popup (popup tree did not match 'Pin to (dock|home)')"
                    Write-Host "    info: found $($pinItems.Count) Pin-related menu items: $($pinItems -join ' | ')" -ForegroundColor DarkGray
        
                    # Specifically look for the dock-pin entry. With EnableDock=true it should be present.
                    $dockPin = @($tree | Where-Object { $_ -match 'ListItem\s+"Pin to dock"' })
                    Assert-GreaterThan $dockPin.Count 0 -Because "EnableDock=true was set in Arrange but 'Pin to dock' menu entry is missing — PR #46436 regression OR menu rendering issue. Popup contents: $($pinItems -join ' | ')"
    } finally {
    # Cleanup
        try {
                        if ($cpHwnd) { & winapp ui set-value 'MainSearchBox' '' -w $cpHwnd 2>$null | Out-Null }
                        Reset-CmdPalToHome
                        if ($backup) { Restore-CmdPalSettingsJson -BackupPath $backup }
                        Restart-CmdPalAppX | Out-Null
                    } catch { Write-Warning "[cleanup] $($_.Exception.Message)" }
    }
}
