#Requires -Version 7.0
# 07-AllApps.tests.ps1 — extracted from command-palette-checklist.ps1 during Phase 2b split.
# Dot-sourced from the orchestrator so it shares script scope ($cpHwnd,
# $cpSettings, $cpEnabled, $cpDataDir). See _helpers.ps1 for the
# CmdPal-specific helper functions these tests call into.
# ── Box L1021-L1023: Installed Apps provider returns the AllApps entry ─
# Distinguishes the AllApps provider (which lists 'Notepad' without .exe)
# from the Files indexer (which finds 'notepad.exe'). Both should be
# present for a typical install.
Test-Case 'CmdPal_AllApps_ReturnsNotepadAppEntry' "Box L1021-L1023: Installed Apps provider returns 'Notepad' app entry (FUNCTIONAL — safe, doesn't invoke)" {
    # Act
    Invoke-CmdPalQuery 'notepad'
    # Assert
    
    $r = winapp ui search 'Notepad' -w $cpHwnd --json 2>$null | ConvertFrom-Json
    $appHit = @($r.matches | Where-Object { $_.type -eq 'ListItem' -and $_.name -eq 'Notepad' }) | Select-Object -First 1
    Assert-NotNull $appHit -Because "AllApps provider did not return 'Notepad' app entry (got: $($r.matches.name -join ', '))"
    $primary = (Get-UiaProperty 'PrimaryCommandButton' 'Name' -Hwnd $cpHwnd)
    Assert-Contains @('Run','Open','Launch') $primary -Because "Primary action for an app should be 'Run'/'Open'/'Launch'"
    Write-Host "    info: AllApps 'Notepad' present with Primary='$primary'" -ForegroundColor DarkGray
}
