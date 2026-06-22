@{
    RootModule        = 'WinAppCli.PowerToys.psm1'
    ModuleVersion     = '0.1.0'
    GUID              = 'a4d2f0a0-5d63-4e1a-9c7e-2f0a5d637c1e'
    Author            = 'PowerToys winappCli prototype'
    Description       = 'Helpers for driving PowerToys modules via winappCli (UI Automation), plus settings backup/restore and a Test-Step harness for translating manual release-checklist boxes into runnable scripts.'
    PowerShellVersion = '7.2'
    RequiredModules   = @()
    AliasesToExport   = @('Goto-PtSettingsPage')
    FunctionsToExport = @(
        # Common
        'Test-WinAppCliInstalled', 'Test-IsElevated', 'Assert-Elevated',
        'Get-WinAppCliVersion', 'Get-FirstSlug', 'Get-EntrySlugs', 'Wait-WindowByTitle',
        'Wait-Until', 'Wait-StaysTrue', 'Get-WinAppCliSlowFactor',
        'Assert-PtControlExists',
        # Test harness
        'Reset-TestSuite', 'New-TestStep', 'Get-TestSuiteReport', 'Save-TestSuiteReport',
        # Runner
        'Get-PtRunnerExe', 'Test-PtRunnerRunning', 'Stop-PowerToys',
        'Start-PowerToys', 'Restart-PowerToys',
        # Settings (Tier A — UI driven)
        'Open-PtSettings', 'Switch-PtSettingsPage',
        'Get-PtSettingsToggle', 'Set-PtSettingsToggle',
        # Settings (Tier B — JSON + restart)
        'Get-PtSettingsJsonPath', 'Backup-PtSettings', 'Restore-PtSettings',
        'Set-PtSettingJson', 'Disable-PtModuleWarning',
        # Input — keyboard simulation (PInvoke SendInput)
        'Send-PtHotkey', 'Send-PtKey',
        # Input — keyboard via PostMessage (bypasses SendInput's UIPI restrictions)
        'Send-PtKeyToWindow',
        # Input — mouse simulation (PInvoke SendInput) — closes the WinAppDriver gap
        'Move-PtMouseTo', 'Send-PtMouseButton', 'Send-PtMouseClick',
        'Send-PtMouseDrag', 'Get-PtCursorPos',
        # Visual — Win32 + GDI deterministic checks
        'Get-WindowExStyle', 'Test-WindowTopmost', 'Get-WindowParent', 'Get-WindowRect',
        'Set-WindowForeground', 'Get-ForegroundHwnd', 'Hide-Window', 'Test-WindowVisible',
        'Get-PixelAt', 'Get-PixelRowSample', 'Test-PixelColorMatch',
        # Module orchestration
        'Get-PtModuleExe', 'Start-PtModule', 'Stop-PtModule',
        'Test-PtModuleEnabled', 'Read-PtModuleLog',
        # Shared kernel events — proper hotkey-bypass for PT modules
        'Invoke-PtSharedEvent', 'Test-PtSharedEvent', 'Get-PtSharedEventCatalog',
        # AAA test pattern (Arrange-Act-Assert-Cleanup) + generic UIA helpers
        'Invoke-AAATest', 'Test-Case', 'Set-AAAFilter', 'Get-AAAFilter',
        'Wait-UiaListItem', 'Reset-AppToHome',
        'Set-ClipboardSafe', 'Get-ClipboardSafe',
        'Get-ProcessesStartedAfter', 'Stop-ProcessesSafely',
        # High-level UIA wrappers — Playwright/Selenium-style sugar
        'Get-UiaProperty', 'Set-UiaText', 'Invoke-UiaAction',
        'Wait-AnyOf', 'Wait-AllOf', 'Wait-PropertyChange', 'Wait-ListCount',
        # Retrying assertions
        'Assert-Eventually', 'Assert-EventuallyEquals', 'Assert-EventuallyMatches',
        # Lifecycle (block-scoped state snapshots, AppX restart)
        'Use-ClipboardSnapshot', 'New-FreshAppX'
    )
    PrivateData = @{
        PSData = @{
            Tags         = @('PowerToys', 'winappcli', 'UIA', 'testing')
            ProjectUri   = 'https://github.com/microsoft/PowerToys'
            ReleaseNotes = 'v0.1.0 — prototype MVP: Common + TestHarness + Runner + Settings (Tier A & B). See ../Documents/winappcli-integration-plan.md §16.'
        }
    }
}
