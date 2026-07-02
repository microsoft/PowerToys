#Requires -Version 7.0
# 17-Schemas-Extended.tests.ps1 — extracted from command-palette-checklist.ps1 during Phase 2b split.
# Dot-sourced from the orchestrator so it shares script scope ($cpHwnd,
# $cpSettings, $cpEnabled, $cpDataDir). See _helpers.ps1 for the
# CmdPal-specific helper functions these tests call into.
# ════════════════════════════════════════════════════════════════════
# ★ CONSOLIDATED Settings/Dock schema invariants (Assert.Multiple style)
# ════════════════════════════════════════════════════════════════════
# Replaces 5 individual read-only schema tests with one combined test
# that shares the Arrange+Act (read settings.json once) and runs all
# 5 sets of assertions, collecting every failure and reporting them
# together. Better diagnostics than first-fail and ~5x less per-test
# overhead since the file is only opened once.
#
# Folded-in tests (still tagged in failure messages so a regression
# points at the right area):
#   [DockBands]       PR #46436 — bands have ShowTitles/ShowSubtitles fields
#   [Backdrop]        PR #46436 — DockSettings.Backdrop is a valid enum
#   [DockSize]        PR #46699 — DockSettings.DockSize is a valid enum
#   [Personalization] 0.97.0    — 12 personalization fields at top level
#   [FallbackRanks]   0.97.0    — FallbackRanks is an array at top level
Test-Case 'CmdPal_SettingsSchema_AllReadOnlyInvariants' "★ 0.97-0.99: settings.json schema — 5 invariants checked together (DockBands fields, Backdrop enum, DockSize enum, Personalization fields, FallbackRanks array)" {
    # Arrange — read+parse settings.json once
    $obj = Get-CmdPalSettings

    # Assert — collect all failures, throw once via Assert-Empty
    $failures = New-Object System.Collections.Generic.List[string]
    
    # ── [DockBands] each band has ProviderId/CommandId/ShowTitles/ShowSubtitles ──
    $allBands = @()
    foreach ($bandKey in 'StartBands','CenterBands','EndBands') {
        $allBands += @($obj.DockSettings.$bandKey)
    }
    if ($allBands.Count -eq 0) {
        $failures.Add('[DockBands] no bands present in DockSettings to validate schema')
    } else {
        foreach ($b in $allBands) {
            foreach ($f in 'ProviderId','CommandId','ShowTitles','ShowSubtitles') {
                if (-not $b.PSObject.Properties.Name.Contains($f)) {
                    $failures.Add("[DockBands] $($b.ProviderId)/$($b.CommandId): missing field $f")
                }
            }
        }
    }
    
    # ── [Backdrop] DockSettings.Backdrop is a valid enum + EnableDock present ──
    if (-not $obj.PSObject.Properties.Name.Contains('EnableDock')) {
        $failures.Add('[Backdrop] top-level EnableDock field missing')
    }
    $bd = $obj.DockSettings.Backdrop
    $validBd = @('Acrylic','Mica','MicaAlt','None','Default')
    if (-not $bd) {
        $failures.Add('[Backdrop] DockSettings.Backdrop is null/empty')
    } elseif ($validBd -notcontains $bd) {
        $failures.Add("[Backdrop] DockSettings.Backdrop='$bd' is not one of [$($validBd -join ', ')]")
    }
    
    # ── [DockSize] DockSettings.DockSize is a valid enum ──
    $sz = $obj.DockSettings.DockSize
    $validSz = @('Default','Compact','Small','Medium','Large')
    if ($null -eq $sz) {
        $failures.Add('[DockSize] DockSettings.DockSize missing')
    } elseif ($validSz -notcontains $sz) {
        $failures.Add("[DockSize] DockSettings.DockSize='$sz' is not one of [$($validSz -join ', ')]")
    }
    
    # ── [Personalization] 12 fields present at top level ──
    foreach ($pf in 'BackgroundImagePath','BackgroundImageTintIntensity','BackgroundImageOpacity','BackgroundImageBlurAmount','BackgroundImageBrightness','BackgroundImageFit','BackdropStyle','BackdropOpacity','Theme','ColorizationMode','CustomThemeColor','CustomThemeColorIntensity') {
        if (-not $obj.PSObject.Properties.Name.Contains($pf)) {
            $failures.Add("[Personalization] settings.json missing field '$pf'")
        }
    }
    
    # ── [FallbackRanks] top-level field present (may be array or null) ──
    if (-not $obj.PSObject.Properties.Name.Contains('FallbackRanks')) {
        $failures.Add('[FallbackRanks] settings.json missing top-level FallbackRanks field')
    }
    
    # Report all failures together — better diagnostics than first-fail.
    Assert-Empty $failures.ToArray() -Because 'schema invariants'
    Write-Host "    info: 5 schema invariants OK ($($allBands.Count) bands; Backdrop=$bd; DockSize=$sz)" -ForegroundColor DarkGray
}

# ── 0.99.0 PR #46685 — per-extension settings migration anchor ──────
# The full migration creates per-extension settings files lazily (only
# when the user opens an extension's settings page). What we CAN check
# is that the shared LocalState files are present + valid JSON: that's
# the migration's source. If this file gets corrupted or moved, the
# migration would silently lose all user prefs.
Test-Case 'CmdPal_State_LocalStateFilesPresentAndValid' "★ 0.99.0: PR #46685 — LocalState anchor files (settings/state/cache/calculator_history) present + valid JSON" {
    # Act
    $ls = "$env:LOCALAPPDATA\Packages\Microsoft.CommandPalette_8wekyb3d8bbwe\LocalState"
    Assert-PathExists $ls -Because 'LocalState dir missing'
    $required = 'settings.json','state.json','commandProviderCache.json'
    $missing = @()
    foreach ($f in $required) {
        $p = Join-Path $ls $f
        if (-not (Test-Path $p)) { $missing += $f; continue }
        try {
            $null = Get-Content $p -Raw | ConvertFrom-Json -ErrorAction Stop
        } catch {
            throw "$f exists but is not valid JSON: $($_.Exception.Message)"
        }
    }
    Assert-Empty $missing -Because 'LocalState anchor files'
    # calculator_history.json is created lazily after first calc; check separately
    $calcHist = Join-Path $ls 'calculator_history.json'
    if (Test-Path $calcHist) {
        try { $null = Get-Content $calcHist -Raw | ConvertFrom-Json -ErrorAction Stop }
        catch { throw "calculator_history.json exists but invalid JSON: $($_.Exception.Message)" }
        Write-Host "    info: all 4 LocalState files present + valid" -ForegroundColor DarkGray
    } else {
        Write-Host "    info: 3 core LocalState files valid; calculator_history.json not yet created (lazy)" -ForegroundColor DarkGray
    }
}

# ── 0.97.0 — new built-in providers (PowerToys, RemoteDesktop) ──────
# PR #46198 (FancyZones via PowerToys provider) + 0.97 RemoteDesktop +
# the new SparseApp PowerToys provider. Verify all 3 appear in ProviderSettings.
Test-Case 'CmdPal_Providers_NewBuiltinProvidersFor097And099Present' "★ 0.97-0.99: new built-in providers (RemoteDesktop, PowerToys, PerformanceMonitor) present in ProviderSettings" {
    # Arrange
    $obj = Get-CmdPalSettings
    $providers = $obj.ProviderSettings.PSObject.Properties.Name
    $expected = @{
        'com.microsoft.cmdpal.builtin.remotedesktop' = '0.97'
        'PerformanceMonitor'                         = '0.99'
    }
    # Assert
    # PowerToys provider has a longer dynamic-named key starting with 'Microsoft.PowerToys.SparseApp'
    $ptProvider = $providers | Where-Object { $_ -like 'Microsoft.PowerToys.SparseApp*PowerToys*' } | Select-Object -First 1
    Assert-NotNull $ptProvider -Because 'PowerToys built-in provider (Microsoft.PowerToys.SparseApp*) missing — was the FancyZones-from-CmdPal extension (#46198) removed?'
    $missing = @()
    foreach ($k in $expected.Keys) {
        if (-not ($providers -contains $k)) { $missing += "$k (added in $($expected[$k]))" }
    }
    Assert-Empty $missing -Because 'built-in providers'
    Write-Host "    info: PT provider key=$ptProvider; total providers=$($providers.Count)" -ForegroundColor DarkGray
}
