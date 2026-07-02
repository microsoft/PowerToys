#Requires -Version 7.0
# 24-SettingsUI.tests.ps1 — new for the Settings-UI-binding category
# (2026-05-25). These tests drive the CmdPal AppX's own "Command Palette
# Settings" WinUI 3 window — toggling a control and asserting the
# corresponding key in settings.json updates.
#
# Catches a CLASS OF BUGS not currently covered by any other test:
#   - Broken click handler in Settings UI (click does nothing, no save)
#   - Wrong AutomationId / XAML binding (UI looks right but writes wrong key)
#   - Type mismatch between UI control and SettingsModel field
#   - Save semantics regression (toggle no longer persists)
#
# Pattern (all tests in this file):
#   1. Capture original JSON value
#   2. Open CmdPal AppX Settings window + navigate to target sub-page
#   3. Toggle the control via Set-CmdPalAppXSettingsControl helper
#   4. Helper waits for settings.json to actually update on disk
#   5. Assert new JSON value matches expected
#   6. Cleanup: restore original value (always, in finally)
#
# Cost: ~6s per test (4-5 controls = ~30s total at SlowFactor=1).
# Tag: 'mutation' (these mutate user settings; safe-restore in finally).
#
# Dependencies:
#   - PR #48033 stable AutomationIds (verified present on 0.11.11411.0)
#   - $settings (PT Settings hwnd) set by orchestrator
#   - $cpSettings, $cpHwnd, $cpEnabled set by orchestrator

# Bucket fixture: open the AppX Settings window ONCE, share across tests.
# Each test navigates to its own sub-page (cheap, ~1s) and restores original
# value on cleanup. If no Settings-UI test will run under the active filter,
# skip the (potentially slow ~10s) AppX Settings window open.
$_settingsUITestIds = @(
    'CmdPal_SettingsUI_General_HighlightSearchOnActivateTogglesJson',
    'CmdPal_SettingsUI_General_KeepPreviousQueryTogglesJson',
    'CmdPal_SettingsUI_General_IgnoreShortcutWhenBusyTogglesJson',
    'CmdPal_SettingsUI_General_AllowBreakthroughShortcutTogglesJson',
    'CmdPal_SettingsUI_General_AutoGoHomeWritesJson',
    'CmdPal_SettingsUI_Personalization_ShowAppDetailsTogglesJson',
    'CmdPal_SettingsUI_Personalization_BackspaceGoesBackTogglesJson',
    'CmdPal_SettingsUI_Personalization_ThemeWritesJson',
    'CmdPal_SettingsUI_Dock_EnableDockTogglesJson',
    'CmdPal_SettingsUI_Dock_AlwaysOnTopTogglesJson',
    'CmdPal_SettingsUI_Dock_ThemeWritesJson',
    'CmdPal_SettingsUI_Dock_MaterialWritesJson',
    'CmdPal_SettingsUI_Dock_BackgroundColorizationWritesJson',
    'CmdPal_SettingsUI_Dock_EnableDockShowsPowerDockWindow',
    'CmdPal_SettingsUI_Dock_CompactModeShrinksPowerDockHeight',
    'CmdPal_SettingsUI_Dock_PositionTopBottomRelocatesPowerDock',
    'CmdPal_SettingsUI_Dock_PositionLeftMakesPowerDockVertical',
    'CmdPal_SettingsUI_Dock_DefaultBandsPresentOnFirstEnable',
    'CmdPal_SettingsUI_Dock_PerformanceMonitorBandShowsLiveData',
    'CmdPal_SettingsUI_Dock_DateTimeBandShowsCurrentTime'
)

# ════════════════════════════════════════════════════════════════════
#   SPLIT (review item #6, 2026-05-27): the 718-line monolith was
#   carved into 3 topical sub-files at this same directory level,
#   dot-sourced here in the order the original file executed them.
#   Each sub-file shares script scope so $cpHwnd / $cpsHwnd /
#   $_settingsUITestIds / $script:_settingsUIBucketBackup are all
#   visible across the split.
#
#   The sub-files use the `.ps1` extension (NOT `.tests.ps1`) to
#   signal that they're DOT-SOURCED PARTIALS of this orchestrator,
#   not first-class test files (they would not work standalone — they
#   reference $cpsHwndB and other fixture variables initialized
#   above). Naming convention: any `.ps1` file under cmdpal/ that
#   contains `Test-Case` calls is a partial dot-sourced from a
#   sibling `.tests.ps1` orchestrator.
#
#   What each sub-file holds:
#     * 24-SettingsUI-bindings.ps1
#         13 single-line tests: click a control in Settings UI -> verify
#         settings.json updates. Fast (~8-21s/test); does NOT require
#         PowerDock to be visible. Covers all toggle/ComboBox write paths.
#     * 24-SettingsUI-e2e.ps1
#         7 full E2E tests: click control -> JSON updates -> CmdPal AppX
#         actually renders the change. Slow (~16-24s/test). Drives
#         PowerDock window appearance/position/contents (Dock-only).
#     * 24-SettingsUI-cleanup.ps1
#         1 bucket safety-net test: verify settings.json is byte-
#         identical to pre-test snapshot; restore from snapshot if not.
#         Always runs last.
# ════════════════════════════════════════════════════════════════════
. (Join-Path $PSScriptRoot '24-SettingsUI-bindings.ps1')
. (Join-Path $PSScriptRoot '24-SettingsUI-e2e.ps1')
. (Join-Path $PSScriptRoot '24-SettingsUI-cleanup.ps1')

