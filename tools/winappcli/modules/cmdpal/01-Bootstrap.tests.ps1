#Requires -Version 7.0
# 01-Bootstrap.tests.ps1 — extracted from command-palette-checklist.ps1 during Phase 2b split.
# Dot-sourced from the orchestrator so it shares script scope ($cpHwnd,
# $cpSettings, $cpEnabled, $cpDataDir). See _helpers.ps1 for the
# CmdPal-specific helper functions these tests call into.
# ── Standalone: Settings page surface ──────────────────────────────────
# This one is its own Invoke-AAATest because it asserts the WPF Settings UI
# (different Arrange from the JSON-reading bucket below).
Test-Case 'CmdPal_Settings_PageReachable' "Command Palette Settings page reachable in PT Settings UI" {
    # Assert
    Assert-PtControlExists -Text 'Command Palette' -Hwnd $settings.hwnd
}

# ════════════════════════════════════════════════════════════════════════
#   BUCKET 1 — INSTALLED. Asserts CmdPal AppX is correctly deployed:
#   AppX present + Status OK + install location has exe + AppX-sandbox
#   data dir exists + settings.json exists + master settings.json has the
#   CmdPal enable flag. Shared Arrange: read AppX + paths once.
# ════════════════════════════════════════════════════════════════════════
Test-Case 'CmdPal_Installed_AppXAndDataPathsAndEnableFlag' "Box L1014: CmdPal AppX installed + sandbox paths + enable flag (BUCKET — Installed)" {
    # Arrange — read AppX + path state once, share across all assertions in the bucket
    $appx = Get-AppxPackage -Name 'Microsoft.CommandPalette' -ErrorAction SilentlyContinue
    $masterJson = "$env:LOCALAPPDATA\Microsoft\PowerToys\settings.json"

    # Act — none (read-only bucket)

    # Assert — collect all failures, throw once via Assert-Empty (xUnit Assert.Multiple style)
    $errs = New-Object System.Collections.Generic.List[string]

            if (-not $appx) {
                $errs.Add("Microsoft.CommandPalette AppX not installed (CmdPal in 0.99+ is a packaged MSIX)")
            } else {
                if ($appx.Status -ne 'Ok') {
                    $errs.Add("AppX Status is '$($appx.Status)' (expected 'Ok')")
                }
                $exe = Join-Path $appx.InstallLocation 'Microsoft.CmdPal.UI.exe'
                if (-not (Test-Path $exe)) {
                    $errs.Add("Microsoft.CmdPal.UI.exe missing in AppX install location ($($appx.InstallLocation))")
                }
                Write-Host "    info: AppX version $($appx.Version)" -ForegroundColor DarkGray
            }
            if (-not (Test-Path $cpDataDir)) {
                $errs.Add("AppX data dir missing at $($cpDataDir)")
            }
            if (-not (Test-Path $cpSettings)) {
                $errs.Add("CmdPal settings.json missing at $($cpSettings)")
            }
            if (Test-Path $masterJson) {
                $obj = Get-Content $masterJson -Raw | ConvertFrom-Json
                if (-not $obj.enabled.PSObject.Properties.Name.Contains('CmdPal')) {
                    $errs.Add("master settings.json missing 'CmdPal' enabled flag")
                }
            } else {
                $errs.Add("PT master settings.json missing at $($masterJson)")
            }
            Write-Host "    info: CmdPal currently $(if ($cpEnabled) { 'ENABLED' } else { 'DISABLED' })" -ForegroundColor DarkGray
    
            Assert-Empty $errs.ToArray() -Because 'BUCKET (Installed) assertions'
}

# ════════════════════════════════════════════════════════════════════════
#   BUCKET 2 — SETTINGS SCHEMA. Asserts CmdPal settings.json schema for
#   Hotkey, Providers (presence + per-provider IsEnabled/Pinned schema),
#   Aliases, Theme/Backdrop, CommandHotkeys. Shared Arrange: read+parse
#   settings.json once (vs. 6 reads in the original tests).
# ════════════════════════════════════════════════════════════════════════
Test-Case 'CmdPal_SettingsSchema_HotkeyAndProvidersAndAliasesAndTheme' "Box L1016+L1021-1048+L1050: CmdPal settings.json schema (BUCKET — SettingsSchema)" {
    # Arrange — read+parse settings.json once (shared across 6 sub-checks)
    Assert-PathExists $cpSettings -Because 'settings.json missing — run Installed bucket first'
    $obj = Get-CmdPalSettings

    # Act — none (read-only bucket)

    # Assert — collect all failures, throw once via Assert-Empty (xUnit Assert.Multiple style)
    $errs = New-Object System.Collections.Generic.List[string]
            # 2a. Hotkey schema (L1016)
            if (-not $obj.Hotkey) {
                $errs.Add("Hotkey property missing from settings.json")
            } else {
                foreach ($field in 'win','ctrl','alt','shift','code') {
                    if (-not $obj.Hotkey.PSObject.Properties.Name.Contains($field)) {
                        $errs.Add("Hotkey schema regression — missing '$field' field")
                    }
                }
                $hk = $obj.Hotkey
                Write-Host "    info: Hotkey win=$($hk.win) ctrl=$($hk.ctrl) alt=$($hk.alt) shift=$($hk.shift) code=$($hk.code)" -ForegroundColor DarkGray
            }
    
            # 2b. All 14 expected providers present (L1021-1045)
            $providers = @($obj.ProviderSettings.PSObject.Properties.Name)
            $expected = @{
                'AllApps (L1021-1023)'                = @('AllApps')
                'Calculator (L1024)'                  = @('com.microsoft.cmdpal.builtin.calculator')
                'File Search (L1025-1027)'            = @('Files')
                'Run Commands (L1028)'                = @('com.microsoft.cmdpal.builtin.run')
                'Window Walker (L1029-1030)'          = @('WindowWalker')
                'WinGet (L1031)'                      = @('WinGet')
                'Web Search (L1032)'                  = @('com.microsoft.cmdpal.builtin.websearch')
                'Windows Terminal Profiles (L1033-1034)' = @('WindowsTerminalProfiles')
                'Windows Settings (L1035)'            = @('com.microsoft.cmdpal.builtin.windowssettings')
                'Registry (L1036-1037)'               = @('Windows.Registry')
                'Windows Service (L1038)'             = @('Windows.Services')
                'Time And Date (L1039)'               = @('com.microsoft.cmdpal.builtin.datetime')
                'Windows System Command (L1040-1043)' = @('com.microsoft.cmdpal.builtin.system')
                'Bookmark (L1044-1045)'               = @('Bookmarks')
            }
            foreach ($k in $expected.Keys) {
                $found = $false
                foreach ($candidate in $expected[$k]) {
                    if ($providers -contains $candidate) { $found = $true; break }
                }
                if (-not $found) { $errs.Add("Provider missing: $k") }
            }
            Write-Host "    info: $($providers.Count) total providers configured" -ForegroundColor DarkGray
    
            # 2c. Each provider entry has IsEnabled / FallbackCommands / PinnedCommandIds (L1048)
            foreach ($p in $providers) {
                $entry = $obj.ProviderSettings.$p
                foreach ($field in 'IsEnabled','FallbackCommands','PinnedCommandIds') {
                    if (-not $entry.PSObject.Properties.Name.Contains($field)) {
                        $errs.Add("Provider '$p' schema missing '$field' field")
                    }
                }
            }
    
            # 2d. Built-in aliases present (L1036/L1039/L1050)
            foreach ($a in ':','=',')','??','>','<','$') {
                if (-not $obj.Aliases.PSObject.Properties.Name.Contains($a)) {
                    $errs.Add("Built-in alias '$a' missing from Aliases map")
                }
            }
            Write-Host "    info: $(@($obj.Aliases.PSObject.Properties).Count) aliases configured" -ForegroundColor DarkGray
    
            # 2e. Theme / Backdrop / BackgroundImage settings exposed (★ 0.99.0)
            foreach ($f in 'Theme','BackdropStyle','BackdropOpacity','BackgroundImagePath','BackgroundImageOpacity','BackgroundImageBlurAmount') {
                if (-not $obj.PSObject.Properties.Name.Contains($f)) {
                    $errs.Add("Theme/Backdrop setting '$f' missing")
                }
            }
    
            # 2f. Behaviour-toggle settings (★ 0.99.0)
            foreach ($f in 'CommandHotkeys','ShowSystemTrayIcon','IgnoreShortcutWhenFullscreen','IgnoreShortcutWhenBusy','AllowBreakthroughShortcut','HighlightSearchOnActivate','KeepPreviousQuery','EscapeKeyBehaviorSetting') {
                if (-not $obj.PSObject.Properties.Name.Contains($f)) {
                    $errs.Add("0.99.0 setting '$f' missing")
                }
            }
    
            Assert-Empty $errs.ToArray() -Because 'BUCKET (SettingsSchema) assertions'
}

# ════════════════════════════════════════════════════════════════════════
#   BUCKET 3 — DOCK SCHEMA. ★ 0.99.0/0.99.1 — covers Dock feature presence,
#   DockSettings field schema, the PR #47296 null-deserialization regression
#   guard, and PR #47317 ShowLabels-persistence guard. Shared Arrange:
#   read settings.json once (vs. 4 reads originally).
# ════════════════════════════════════════════════════════════════════════
Test-Case 'CmdPal_DockSchema_FeaturePresenceAndFieldsAndRegressionGuards' "★ 0.99.0/0.99.1: Dock feature schema + regression guards (BUCKET — DockSchema)" {
    # Arrange — read+parse settings.json once
    Assert-PathExists $cpSettings -Because 'settings.json missing'
    $obj = Get-CmdPalSettings

    # Act — none (read-only bucket)

    # Assert — collect all failures, throw once via Assert-Empty (xUnit Assert.Multiple style)
    $errs = New-Object System.Collections.Generic.List[string]
            # 3a. Dock feature present (★ 0.99.0)
            foreach ($key in 'EnableDock','DockSettings') {
                if (-not $obj.PSObject.Properties.Name.Contains($key)) {
                    $errs.Add("settings.json missing '$key' (added in 0.99.0)")
                }
            }
    
            # 3b. DockSettings is NOT null (★ 0.99.1 PR #47296 regression guard)
            if ($null -eq $obj.DockSettings) {
                $errs.Add("DockSettings is NULL — would crash CmdPal on startup. PR #47296 fix regression")
            } else {
                # 3c. DockSettings has all expected fields (★ 0.99.0)
                foreach ($f in 'Side','DockSize','AlwaysOnTop','Backdrop','Theme','StartBands','CenterBands','EndBands') {
                    if (-not $obj.DockSettings.PSObject.Properties.Name.Contains($f)) {
                        $errs.Add("DockSettings missing '$f' field")
                    }
                }
                # 3d. DockSettings.StartBands is NOT null
                if ($null -eq $obj.DockSettings.StartBands) {
                    $errs.Add("DockSettings.StartBands is NULL — would break dock rendering")
                }
                # 3e. ShowLabels persisted (★ 0.99.1 PR #47317 regression guard)
                if (-not $obj.DockSettings.PSObject.Properties.Name.Contains('ShowLabels')) {
                    $errs.Add("DockSettings.ShowLabels missing — PR #47317 fix regression (dock labels won't persist)")
                }
    
                $ds = $obj.DockSettings
                Write-Host "    info: EnableDock=$($obj.EnableDock), Side=$($ds.Side), AlwaysOnTop=$($ds.AlwaysOnTop), Backdrop=$($ds.Backdrop)" -ForegroundColor DarkGray
            }
    
            Assert-Empty $errs.ToArray() -Because 'BUCKET (DockSchema) assertions'
}

# ════════════════════════════════════════════════════════════════════════
#   BUCKET 4 — RUNTIME. Asserts process state when CmdPal is enabled:
#   AppX UI process running, helper process running, IPC named events
#   registered (CmdPal.Show + CmdPal.Exit). Shared Arrange: capture
#   "is CmdPal enabled?" once.
# ════════════════════════════════════════════════════════════════════════
Test-Case 'CmdPal_Runtime_ProcessesAndIPCEventsAlive' "L1016-1019 + process state: CmdPal runtime & IPC (BUCKET — Runtime)" {
    # Arrange — capture "is CmdPal enabled?" once
    $enabled = $cpEnabled

    # Act — none (read-only bucket)

    # Assert — collect all failures, throw once via Assert-Empty (xUnit Assert.Multiple style)
    $errs = New-Object System.Collections.Generic.List[string]
            # IPC events should ALWAYS be registered (the helper publishes them
            # whether CmdPal is enabled or not — they're how PT triggers it).
            if (-not (Test-PtSharedEvent -Name 'CmdPal.Show')) {
                $errs.Add("Local\PowerToysCmdPal-ShowEvent-... not present")
            }
            if (-not (Test-PtSharedEvent -Name 'CmdPal.Exit')) {
                $errs.Add("Local\PowerToysCmdPal-ExitEvent-... not present")
            }
    
            # Process checks only when CmdPal is enabled.
            if ($enabled) {
                $ui = Get-Process -Name 'Microsoft.CmdPal.UI' -ErrorAction SilentlyContinue
                if (-not $ui) {
                    $errs.Add("CmdPal enabled in PT but Microsoft.CmdPal.UI.exe is not running")
                } else {
                    Write-Host "    info: CmdPal.UI running (PID $($ui.Id))" -ForegroundColor DarkGray
                }
                $helper = Get-Process -Name 'Microsoft.CmdPal.Ext.PowerToys' -ErrorAction SilentlyContinue
                if (-not $helper) {
                    $errs.Add("Microsoft.CmdPal.Ext.PowerToys (the CmdPal ↔ PowerToys integration extension) is not running")
                } else {
                    Write-Host "    info: helper running (PID $($helper.Id))" -ForegroundColor DarkGray
                }
            } else {
                Write-Host "    info: CmdPal is DISABLED — skipping process-presence checks" -ForegroundColor DarkGray
            }
    
            Assert-Empty $errs.ToArray() -Because 'BUCKET (Runtime) assertions'
}
