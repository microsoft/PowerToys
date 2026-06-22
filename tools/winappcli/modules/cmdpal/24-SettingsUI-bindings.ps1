#Requires -Version 7.0
# 24-SettingsUI-bindings.ps1 — dot-sourced PARTIAL of 24-SettingsUI.tests.ps1.
#
# NOT a standalone test file (no `.tests.ps1` extension). It's dot-sourced
# from 24-SettingsUI.tests.ps1 and shares its script scope, which means
# it sees the orchestrator-initialised fixture variables: $cpHwnd,
# $cpSettings, $cpEnabled, $cpDataDir, $cpsHwnd, $_settingsUITestIds,
# $script:_settingsUIBucketBackup. Loading it directly (without the
# orchestrator) would error out on undefined variables.
#
# Purpose: UI -> JSON binding tests. Each test clicks one control in
# the CmdPal AppX Settings window and asserts the corresponding key
# in settings.json updates. 13 tests covering all toggle + ComboBox
# write paths across General / Personalization / Dock sub-pages.
# Fast (~8-21s/test); does NOT require PowerDock to be visible.
if (Test-AnyTestWillRun -Ids $_settingsUITestIds) {

# ── Bucket-level safety net: snapshot + restore ────────────────────
# Belt-and-suspenders: even though every individual test has a finally{}
# that restores its own setting, we ALSO snapshot the whole settings.json
# at fixture-load time and compare/restore at suite-end. This catches:
#   - A per-test cleanup that silently fails (e.g. Wait-Until timeout)
#   - A test that throws BEFORE its $orig variable is captured (rare,
#     but possible if Switch-CmdPalAppXSettingsPage throws)
#   - Test ordering bugs where one test's mutation leaks into another's
#     "original" snapshot
#   - Ctrl+C / suite crash (covered by the PS engine event handler)
#
# We use Backup-CmdPalSettingsJson + Restore-CmdPalSettingsJson — the
# same helpers the existing 15-Mutation-Settings tests use, so we get
# byte-level verification + atomic write semantics.
$script:_settingsUIBucketBackup = $null
try {
    $script:_settingsUIBucketBackup = Backup-CmdPalSettingsJson
    Write-Host "    [24-SettingsUI fixture] settings.json snapshot saved to $($script:_settingsUIBucketBackup)" -ForegroundColor DarkGray
} catch {
    Write-Warning "[24-SettingsUI fixture] failed to snapshot settings.json before tests run: $($_.Exception.Message). Per-test cleanup is the only safety net."
}

# Engine exit handler — fires on PowerShell session shutdown including
# Ctrl+C and pipeline-stop scenarios. Uses the snapshot to restore
# settings.json if it has drifted from the captured baseline. The
# regular safety-net Test-Case at the end of this file handles the
# normal-completion path (and is faster — no AppX restart cost on
# exit). The engine handler is a fallback for abnormal exits.
$null = Register-EngineEvent -SourceIdentifier 'PowerShell.Exiting' -Action {
    if (-not $script:_settingsUIBucketBackup) { return }
    if (-not (Test-Path $script:_settingsUIBucketBackup)) { return }
    try {
        $orig = [System.IO.File]::ReadAllBytes($script:_settingsUIBucketBackup)
        $cur  = if (Test-Path $cpSettings) { [System.IO.File]::ReadAllBytes($cpSettings) } else { @() }
        $drift = ($orig.Length -ne $cur.Length) -or (Compare-Object $orig $cur -SyncWindow 0).Count -gt 0
        if ($drift) {
            Write-Warning "[24-SettingsUI engine-exit] settings.json drifted from snapshot — restoring from $($script:_settingsUIBucketBackup)"
            try { Restore-CmdPalSettingsJson -BackupPath $script:_settingsUIBucketBackup } catch {
                Write-Warning "[24-SettingsUI engine-exit] Restore failed: $($_.Exception.Message). Backup preserved at $($script:_settingsUIBucketBackup)"
            }
        }
    } catch {
        # Swallow — engine-exit handlers can't really report errors anywhere useful
    }
} -SupportEvent

# Re-acquire PT Settings — the orchestrator opens it at startup, but
# intermediate tests (AppX restarts, settings mutations) can close/move
# it. Re-call Open-PtSettings (idempotent) + Switch-PtSettingsPage to
# guarantee we're on the CmdPal page before clicking the AppX Settings
# button.
#
# Wrap the AppX Settings open in try/catch so a fixture-setup failure
# becomes a per-test FAIL rather than a fatal crash that takes down the
# whole suite. Late-suite runs are vulnerable to PT Settings being
# orphaned by upstream tests' AppX restarts, focus changes, etc.
$settings = Open-PtSettings
Switch-PtSettingsPage -Module 'CmdPal' -Hwnd $settings.hwnd
Start-Sleep -Milliseconds 800

$cpsHwnd = $null
$cpsHwndError = $null
try {
    $cpsHwnd = Open-CmdPalAppXSettings -PtSettingsHwnd $settings.hwnd
} catch {
    $cpsHwndError = $_.Exception.Message
    Write-Warning "[24-SettingsUI fixture] Open-CmdPalAppXSettings failed: $cpsHwndError"
}

# If fixture setup failed, all tests below should fail with a useful
# message (rather than the obscure 'Settings control not present').
if (-not $cpsHwnd) {
    foreach ($id in $_settingsUITestIds) {
        Test-Case $id "SettingsUI fixture FAILED to open CmdPal AppX Settings window — all UI-binding tests SKIPPED" {
            throw "fixture failed: $($cpsHwndError ?? 'unknown error')"
        }.GetNewClosure()
    }
    return
}

# ════════════════════════════════════════════════════════════════════
# UI → JSON binding (single-line per test)
# ════════════════════════════════════════════════════════════════════
# Each Test-Case below delegates to a worker function in _helpers.ps1
# (Invoke-CmdPalToggleBindingTest or Invoke-CmdPalComboBoxBindingTest)
# which does: navigate -> capture orig -> flip/iterate -> assert ->
# restore in finally. To add a new control test, copy one line and
# change the 3 parameters (Page, ControlId, SettingsKey).

# ── General sub-page ─────────────────────────────────────────────────
Test-Case 'CmdPal_SettingsUI_General_HighlightSearchOnActivateTogglesJson' "★ UI-binding ★: General → 'Highlight search on activate' toggle updates HighlightSearchOnActivate in settings.json" {
    Invoke-CmdPalToggleBindingTest -Page General -ControlId 'CmdPal_GeneralPage_HighlightSearch' -SettingsKey 'HighlightSearchOnActivate'
}

Test-Case 'CmdPal_SettingsUI_General_KeepPreviousQueryTogglesJson' "★ UI-binding ★: General → 'Keep previous query' toggle updates KeepPreviousQuery in settings.json" {
    Invoke-CmdPalToggleBindingTest -Page General -ControlId 'CmdPal_GeneralPage_KeepPreviousQuery' -SettingsKey 'KeepPreviousQuery'
}

Test-Case 'CmdPal_SettingsUI_General_IgnoreShortcutWhenBusyTogglesJson' "★ UI-binding ★ (PR #45891): General → 'Ignore shortcut when system heuristically detects fullscreen' toggle updates IgnoreShortcutWhenBusy in settings.json" {
    Invoke-CmdPalToggleBindingTest -Page General -ControlId 'CmdPal_GeneralPage_IgnoreShortcutWhenBusy' -SettingsKey 'IgnoreShortcutWhenBusy'
}

Test-Case 'CmdPal_SettingsUI_General_AllowBreakthroughShortcutTogglesJson' "★ UI-binding ★ (PR #45891): General → 'Allow breakthrough with rapid shortcut presses' toggle updates AllowBreakthroughShortcut in settings.json" {
    Invoke-CmdPalToggleBindingTest -Page General -ControlId 'CmdPal_GeneralPage_AllowBreakthroughShortcut' -SettingsKey 'AllowBreakthroughShortcut'
}

Test-Case 'CmdPal_SettingsUI_General_AutoGoHomeWritesJson' "★ UI-binding ★: General → 'Automatically return home' ComboBox updates AutoGoHomeInterval in settings.json (ComboBox path on General)" {
    Invoke-CmdPalComboBoxBindingTest -Page General -ControlId 'CmdPal_GeneralPage_AutoGoHome' -SettingsKey 'AutoGoHomeInterval'
}

# ── Personalization sub-page ─────────────────────────────────────────
Test-Case 'CmdPal_SettingsUI_Personalization_ShowAppDetailsTogglesJson' "★ UI-binding ★: Personalization → 'Show app details' toggle updates ShowAppDetails in settings.json" {
    Invoke-CmdPalToggleBindingTest -Page Personalization -ControlId 'CmdPal_AppearancePage_ShowAppDetails' -SettingsKey 'ShowAppDetails'
}

Test-Case 'CmdPal_SettingsUI_Personalization_BackspaceGoesBackTogglesJson' "★ UI-binding ★ (PR #47126): Personalization → 'Backspace goes back' toggle updates BackspaceGoesBack in settings.json" {
    Invoke-CmdPalToggleBindingTest -Page Personalization -ControlId 'CmdPal_AppearancePage_BackspaceGoesBack' -SettingsKey 'BackspaceGoesBack'
}

Test-Case 'CmdPal_SettingsUI_Personalization_ThemeWritesJson' "★ UI-binding ★: Personalization → 'App theme mode' ComboBox updates Theme in settings.json (ComboBox path on Personalization)" {
    Invoke-CmdPalComboBoxBindingTest -Page Personalization -ControlId 'CmdPal_AppearancePage_Theme' -SettingsKey 'Theme'
}

# ── Dock sub-page ────────────────────────────────────────────────────
Test-Case 'CmdPal_SettingsUI_Dock_EnableDockTogglesJson' "★ UI-binding ★: Dock → 'Enable Dock' toggle updates EnableDock in settings.json" {
    Invoke-CmdPalToggleBindingTest -Page Dock -ControlId 'CmdPal_DockSettingsPage_EnableDock' -SettingsKey 'EnableDock'
}

# Nested key — dotted-path support exercises Set-CmdPalAppXSettingsControl's path resolution
Test-Case 'CmdPal_SettingsUI_Dock_AlwaysOnTopTogglesJson' "★ UI-binding ★ (PR #46163): Dock → 'Always stay on top' toggle updates DockSettings.AlwaysOnTop (nested key)" {
    Invoke-CmdPalToggleBindingTest -Page Dock -ControlId 'CmdPal_DockSettingsPage_AlwaysOnTop' -SettingsKey 'DockSettings.AlwaysOnTop'
}

Test-Case 'CmdPal_SettingsUI_Dock_ThemeWritesJson' "★ UI-binding ★: Dock → 'Theme mode' ComboBox updates DockSettings.Theme in settings.json (Dock-page ComboBox)" {
    Invoke-CmdPalComboBoxBindingTest -Page Dock -ControlId 'CmdPal_DockSettingsPage_Theme' -SettingsKey 'DockSettings.Theme'
}

# Material dropdown (Acrylic / Transparent) — uses legacy AutomationId 'BackdropComboBox'
# (PR #48033 didn't rename this one), writes to DockSettings.Backdrop.
Test-Case 'CmdPal_SettingsUI_Dock_MaterialWritesJson' "★ UI-binding ★: Dock → 'Material' ComboBox updates DockSettings.Backdrop in settings.json" {
    Invoke-CmdPalComboBoxBindingTest -Page Dock -ControlId 'BackdropComboBox' -SettingsKey 'DockSettings.Backdrop'
}

# Background colorization (None / Accent / Custom / Image) — writes to
# DockSettings.ColorizationMode. Tests the Background section's primary
# ComboBox; per-mode sub-controls (color picker, image picker) are out
# of scope (harder to drive, lower bug frequency).
Test-Case 'CmdPal_SettingsUI_Dock_BackgroundColorizationWritesJson' "★ UI-binding ★: Dock → 'Background colorization' ComboBox updates DockSettings.ColorizationMode in settings.json" {
    Invoke-CmdPalComboBoxBindingTest -Page Dock -ControlId 'CmdPal_DockSettingsPage_ColorizationMode' -SettingsKey 'DockSettings.ColorizationMode'
}

}  # end Test-AnyTestWillRun guard (bindings sub-file)

