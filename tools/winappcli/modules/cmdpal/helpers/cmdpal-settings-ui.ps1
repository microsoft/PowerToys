#Requires -Version 7.0
# cmdpal-settings-ui.ps1 — split from _helpers.ps1 (review item #5).
# Dot-sourced from _helpers.ps1; shares script scope with the orchestrator
# so it sees $cpHwnd / $cpSettings / $cpEnabled / $cpDataDir.

# ════════════════════════════════════════════════════════════════════════
#   Settings-UI helpers (added 2026-05-25 for the UI-binding test category)
# ════════════════════════════════════════════════════════════════════════
# These drive the CmdPal AppX's own "Command Palette Settings" window
# (NOT PT Settings, NOT the CmdPal main palette). That AppX Settings
# window is a separate WinUI 3 window owned by Microsoft.CmdPal.UI and
# is reachable via the "Settings" button on the CmdPal home page (or
# from PT Settings → Command Palette → Settings button, or via the
# tray icon menu).
#
# The AppX Settings window exposes 3 main sub-pages:
#   - General             (NavItem 'GeneralPageNavItem')
#   - Personalization     (NavItem 'AppearancePageNavItem')
#   - Dock (Preview)      (NavItem 'DockSettingsPageNavItem')
# Plus 2 extensions sub-pages:
#   - Installed           (NavItem 'ExtensionPageNavItem')
#   - Gallery             (NavItem 'GalleryPageNavItem')
#
# PR #48033 added stable AutomationIds for most controls. Verified
# (2026-05-25 on CmdPal 0.11.11411.0):
#   General: CmdPal_GeneralPage_HighlightSearch, _KeepPreviousQuery,
#            _IgnoreShortcutWhenBusy, _IgnoreShortcutWhenFullscreen,
#            _AllowBreakthroughShortcut, _LowLevelHook, _AutoGoHome,
#            _ShowSystemTrayIcon, _AllowExternalReload, _MonitorPosition,
#            _ActivationKey, _LearnMore, _About, _SendFeedback
#   Personalization: CmdPal_AppearancePage_Theme, _BackdropStyle,
#            _BackdropOpacity, _ColorizationMode, _ShowAppDetails,
#            _BackspaceGoesBack, _EscapeKeyBehavior, _SingleClickActivates,
#            _DisableAnimations, _OpenCommandPalette, _ResetAppearance
#   Dock:    CmdPal_DockSettingsPage_EnableDock, _Theme, _AlwaysOnTop,
#            _ColorizationMode, _LearnMore
#            (plus DockPositionComboBox, DockSizeComboBox, DockSizeSettingsCard,
#            BackdropComboBox — legacy IDs left alone by PR #48033)
#
# Save semantics (verified): toggling a CheckBox / ToggleSwitch fires
# TogglePattern and CmdPal writes settings.json within ~1.5s. ComboBox
# selection also writes immediately on change. No explicit Save button.
#
# Window discovery: the AppX Settings window may already be open (from a
# prior test or user action), hidden (if CmdPal dismissed it), or not
# yet spawned. Open-CmdPalAppXSettings handles all three cases.

# Locates the CmdPal AppX Settings window if present, returns its hwnd
# or $null if not found. Looks for a top-level window with title
# 'Command Palette Settings' owned by Microsoft.CmdPal.UI.
function Get-CmdPalAppXSettingsHwnd {
    $w = winapp ui list-windows --json 2>$null | ConvertFrom-Json |
         Where-Object { $_.title -eq 'Command Palette Settings' } |
         Select-Object -First 1
    if ($w) { return [int64]$w.hwnd }
    return $null
}

# Opens the CmdPal AppX Settings window if it's not already open. Returns
# the hwnd.
#
# Two routes are tried in order:
#   1. Direct: invoke SettingsIconButton on the CmdPal main palette
#      (always present on the palette window, doesn't require PT Settings
#      to be on any specific page). Preferred route — most reliable.
#   2. Fallback: from PT Settings → Command Palette page → click the
#      'Settings' button. Used only when the main palette isn't open
#      (rare — orchestrator keeps it summoned).
#
# Idempotent: if the AppX Settings window is already open, just brings
# it to foreground and returns its hwnd.
function Open-CmdPalAppXSettings {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][int64]$PtSettingsHwnd
    )
    $existing = Get-CmdPalAppXSettingsHwnd
    if ($existing) {
        try { Set-WindowForeground -Hwnd $existing | Out-Null } catch {}
        Start-Sleep -Milliseconds 300
        return $existing
    }

    # Route 1: open via CmdPal main palette's SettingsIconButton (preferred)
    try { Invoke-PtSharedEvent -Name 'CmdPal.Show' | Out-Null } catch {}
    Start-Sleep -Milliseconds 600
    $palette = winapp ui list-windows --json 2>$null | ConvertFrom-Json |
               Where-Object { $_.title -eq 'Command Palette' } |
               Select-Object -First 1
    if ($palette) {
        $palHwnd = [int64]$palette.hwnd
        try {
            $out = winapp ui invoke 'SettingsIconButton' -w $palHwnd 2>&1 | Out-String
            if ($out -match '(?i)invoked') {
                $hwnd = Wait-Until -TimeoutMs 8000 -PollMs 250 -IgnoreException `
                    -Message "Command Palette Settings window did not appear within 8s of invoking SettingsIconButton" `
                    -Condition { Get-CmdPalAppXSettingsHwnd }
                if ($hwnd) {
                    try { Set-WindowForeground -Hwnd $hwnd | Out-Null } catch {}
                    Start-Sleep -Milliseconds 500
                    return [int64]$hwnd
                }
            }
        } catch {
            # Fall through to route 2
        }
    }

    # Route 2 (fallback): click 'Settings' button on PT Settings CmdPal page
    try { Set-WindowForeground -Hwnd $PtSettingsHwnd | Out-Null } catch {}
    Start-Sleep -Milliseconds 300
    try { Switch-PtSettingsPage -Module 'CmdPal' -Hwnd $PtSettingsHwnd | Out-Null } catch {
        Write-Warning "Open-CmdPalAppXSettings: Switch-PtSettingsPage -Module CmdPal failed: $($_.Exception.Message)"
    }
    Start-Sleep -Milliseconds 800
    $btn = winapp ui search 'Settings' -w $PtSettingsHwnd --json 2>$null | ConvertFrom-Json
    $target = $btn.matches | Where-Object {
        $_.type -eq 'Button' -and $_.name -eq 'Settings' -and $_.isEnabled -and -not $_.isOffscreen
    } | Select-Object -First 1
    if (-not $target) {
        Start-Sleep -Milliseconds 1500
        $btn = winapp ui search 'Settings' -w $PtSettingsHwnd --json 2>$null | ConvertFrom-Json
        $target = $btn.matches | Where-Object {
            $_.type -eq 'Button' -and $_.name -eq 'Settings' -and $_.isEnabled -and -not $_.isOffscreen
        } | Select-Object -First 1
    }
    if (-not $target) {
        throw "Could not open CmdPal AppX Settings window — route 1 (SettingsIconButton) didn't fire, route 2 (PT Settings button) failed (PT Settings may not be on the CmdPal page; hwnd=$PtSettingsHwnd)."
    }
    winapp ui invoke $target.selector -w $PtSettingsHwnd 2>&1 | Out-Null
    $hwnd = Wait-Until -TimeoutMs 10000 -PollMs 250 -IgnoreException `
        -Message "Command Palette Settings window did not appear within 10s of clicking the PT Settings 'Settings' button" `
        -Condition { Get-CmdPalAppXSettingsHwnd }
    if (-not $hwnd) { throw "Settings window did not open via either route" }
    try { Set-WindowForeground -Hwnd $hwnd | Out-Null } catch {}
    Start-Sleep -Milliseconds 500
    return [int64]$hwnd
}

# Navigate the AppX Settings window's NavView to one of the 3 main pages.
# Acceptable -Page values: 'General', 'Personalization', 'Dock'.
# After return, the corresponding sub-page controls (CmdPal_<Page>Page_*)
# are present in the UIA tree.
function Switch-CmdPalAppXSettingsPage {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][int64]$Hwnd,
        [Parameter(Mandatory)][ValidateSet('General','Personalization','Dock')][string]$Page
    )
    $navItem = switch ($Page) {
        'General'         { 'GeneralPageNavItem' }
        'Personalization' { 'AppearancePageNavItem' }
        'Dock'            { 'DockSettingsPageNavItem' }
    }
    winapp ui invoke $navItem -w $Hwnd 2>$null | Out-Null
    # Wait for the page-specific stable ID to appear (proves navigation completed)
    # Use a known ID per page:
    $probeId = switch ($Page) {
        'General'         { 'CmdPal_GeneralPage_HighlightSearch' }
        'Personalization' { 'CmdPal_AppearancePage_Theme' }
        'Dock'            { 'CmdPal_DockSettingsPage_EnableDock' }
    }
    $found = Wait-Until -TimeoutMs 3000 -PollMs 150 -IgnoreException `
        -Message "Sub-page '$Page' did not load within 3s (probe '$probeId' did not appear)" `
        -Condition {
            $r = winapp ui search $probeId -w $Hwnd --json 2>$null | ConvertFrom-Json -ErrorAction SilentlyContinue
            if ($r -and $r.matchCount -gt 0) { return $r.matches[0] }
            $null
        }
    if (-not $found) { throw "Sub-page '$Page' load probe failed" }
}

# Toggle (or set) a control in the CmdPal AppX Settings window AND wait
# for settings.json to actually update on disk. Supports:
#   - Toggle buttons / CheckBoxes via TogglePattern (winapp ui invoke)
#   - ComboBoxes via Set-UiaValue (caller provides -Value)
#
# Parameters:
#   -Hwnd          AppX Settings window hwnd (from Open-CmdPalAppXSettings)
#   -ControlId     PR #48033 stable AutomationId (e.g. 'CmdPal_GeneralPage_HighlightSearch')
#   -SettingsKey   Top-level key in settings.json to watch for the change
#                  (e.g. 'HighlightSearchOnActivate'). Optional nested path
#                  via dot notation: 'DockSettings.ShowLabels'.
#   -ExpectedValue The post-toggle value (Boolean for toggles, string for
#                  ComboBox). When set, asserts JSON equals this value.
#   -Mode          'Toggle' (default — invoke once) or 'Set' (ComboBox: open
#                  drop-down + select item by name passed in -ExpectedValue).
#
# Throws if the control isn't reachable, the click fails, or the JSON
# never reflects the change within the slow-factor-aware timeout.
#
# Returns: the post-change JSON value (so caller can assert further).
function Set-CmdPalAppXSettingsControl {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][int64]$Hwnd,
        [Parameter(Mandatory)][string]$ControlId,
        [Parameter(Mandatory)][string]$SettingsKey,
        [Parameter()][object]$ExpectedValue,
        [ValidateSet('Toggle','Set')][string]$Mode = 'Toggle',
        # CmdPal debounces settings writes by ~2-2.5s after a toggle
        # (verified 2026-05-25). 6s default gives healthy headroom; on
        # slow CI the SlowFactor multiplier in Wait-Until handles it.
        [int]$TimeoutMs = 6000
    )
    # 1. Verify control is in the tree (page must be navigated first)
    $r = winapp ui search $ControlId -w $Hwnd --json 2>$null | ConvertFrom-Json
    if (-not $r -or $r.matchCount -eq 0) {
        throw "Settings control '$ControlId' not present in Settings window (hwnd=$Hwnd). Did you call Switch-CmdPalAppXSettingsPage first?"
    }

    # 2. Snapshot settings.json value (mtime no longer used — value-based check is sufficient)
    $jBefore = _ReadJsonShared $script:cpSettings
    $beforeValue = _ResolveJsonPath -Obj $jBefore -Path $SettingsKey
    Write-Verbose "[Set-CmdPalAppXSettingsControl] $ControlId / $SettingsKey before=$beforeValue, mode=$Mode"

    # 3. Drive the control. Bring the Settings window to the foreground and
    # give it a brief settle so TogglePattern handlers are armed — without
    # this, the FIRST toggle after navigation is often silently dropped
    # even though winapp reports 'Invoked ... via TogglePattern'.
    try { Set-WindowForeground -Hwnd $Hwnd | Out-Null } catch {}
    Start-Sleep -Milliseconds 200
    switch ($Mode) {
        'Toggle' {
            $invokeOut = winapp ui invoke $ControlId -w $Hwnd 2>&1 | Out-String
            if ($invokeOut -notmatch '(?i)invoked|toggled') {
                throw "Failed to toggle '$ControlId': $($invokeOut.Trim())"
            }
        }
        'Set' {
            if ($null -eq $ExpectedValue) {
                throw "Mode='Set' requires -ExpectedValue (the ComboBox item Name to select)"
            }
            # ComboBox: open then click the item with matching Name. Avoid
            # winapp ui set-value (some ComboBoxes don't accept ValuePattern).
            winapp ui invoke $ControlId -w $Hwnd 2>$null | Out-Null
            Start-Sleep -Milliseconds 400
            # Find the dropdown item by name and invoke it
            $items = winapp ui search $ExpectedValue -w $Hwnd --json 2>$null | ConvertFrom-Json
            $item = $items.matches | Where-Object { $_.type -eq 'ListItem' -and $_.name -eq $ExpectedValue } | Select-Object -First 1
            if (-not $item) {
                # Close the dropdown and report
                try { Send-PtKeyToWindow -Hwnd $Hwnd -Key 'escape' } catch {}
                throw "ComboBox '$ControlId' has no item named '$ExpectedValue' after opening"
            }
            winapp ui invoke $item.selector -w $Hwnd 2>$null | Out-Null
        }
    }

    # 4. Wait for settings.json to update. We rely on VALUE comparison only
    # (not mtime) because (a) the value may transition via an intermediate
    # state on some controls and (b) mtime granularity / file-cache races
    # can cause false negatives. The value-based check is correct as long
    # as $beforeValue captures the pre-toggle state, which we do above.
    # Use shared read (FileShare.ReadWrite) so we never block CmdPal's writes.
    #
    # IMPORTANT: We use Wait-Until as a PRESENCE check only (returns $true
    # when the value differs) and re-fetch the new value AFTER Wait-Until
    # returns. Reason: Wait-Until's line 125 unwraps single-element arrays
    # and treats `$false` as falsy, so the `,$v` comma-trick fails for
    # Boolean settings transitioning from True to False — Wait-Until would
    # see `$false` and keep polling, timing out even though the value did
    # change. Documented in Wait-Until's .NOTES.
    $null = Wait-Until -TimeoutMs $TimeoutMs -PollMs 150 -IgnoreException `
        -Message "settings.json '$SettingsKey' did not change from '$beforeValue' within ${TimeoutMs}ms after $Mode on '$ControlId'" `
        -Condition {
            $j = _ReadJsonShared $script:cpSettings
            if (-not $j) { return $false }
            $v = _ResolveJsonPath -Obj $j -Path $SettingsKey
            $v -ne $beforeValue   # boolean — true when value has flipped
        }
    # Re-fetch the actual new value (not via Wait-Until — see above)
    $jAfter = _ReadJsonShared $script:cpSettings
    $newValue = _ResolveJsonPath -Obj $jAfter -Path $SettingsKey

    # 5. Optional ExpectedValue assertion
    if ($PSBoundParameters.ContainsKey('ExpectedValue') -and $Mode -eq 'Toggle') {
        if ($newValue -ne $ExpectedValue) {
            throw "settings.json '$SettingsKey' is '$newValue' after toggle (expected '$ExpectedValue')"
        }
    }
    return $newValue
}

# Open a CmdPal AppX Settings ComboBox, read the available display
# labels, then close the dropdown. Returns @() of strings in the order
# they appear in the dropdown. Used by ComboBox tests that need to
# iterate options (since display labels don't always 1:1 match the
# underlying JSON enum values — e.g. 'Use system settings' -> Theme
# 'Default'). The caller picks a target by display label or by trial.
function _EnumerateComboBoxOptions {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][int64]$Hwnd,
        [Parameter(Mandatory)][string]$ControlId
    )
    # Verify the control is in the tree first
    $r = winapp ui search $ControlId -w $Hwnd --json 2>$null | ConvertFrom-Json
    if (-not $r -or $r.matchCount -eq 0) { return @() }

    # Open the dropdown
    winapp ui invoke $ControlId -w $Hwnd 2>$null | Out-Null
    Start-Sleep -Milliseconds 500

    # Items render as ComboBoxItem children of the Settings window itself
    # (NOT in the PopupHost — that's only the floating popup chrome).
    # Use winapp ui inspect with a deep walk + parse ComboBoxItem lines.
    $items = @()
    try {
        $insLines = (winapp ui inspect -w $Hwnd --depth 9 2>$null) -split "`n"
        # The dropdown items render as ListItem entries (inspect's type
        # column), with className=ComboBoxItem internally. Match on the
        # display TYPE which is what inspect prints. Slug prefix is
        # 'itm-...' for these items, distinguishing them from button-type
        # items elsewhere in the tree.
        $items = @($insLines |
            Where-Object { $_ -match '^\s*itm-\S+\s+ListItem\s+"([^"]+)"' } |
            ForEach-Object { if ($_ -match 'ListItem\s+"([^"]+)"') { $matches[1] } } |
            Where-Object { $_ -notmatch '^\s*$' -and $_ -notmatch 'ListItemViewModel' } |
            Select-Object -Unique)
    } catch { }

    # Close the dropdown so the test doesn't leave it open
    try { Send-PtKeyToWindow -Hwnd $Hwnd -Key 'escape' } catch {}
    Start-Sleep -Milliseconds 300

    return $items
}

# ════════════════════════════════════════════════════════════════════════
#   Settings-UI test workers (used by 24-SettingsUI.tests.ps1)
# ════════════════════════════════════════════════════════════════════════
# These do the entire test body — capture orig, flip, assert, restore —
# so each registered Test-Case in 24-SettingsUI.tests.ps1 becomes a
# single Invoke-CmdPal*BindingTest call. Worker is a function (NOT a
# scriptblock closure) so it can resolve dot-sourced helpers like
# Switch-CmdPalAppXSettingsPage at the call site rather than at closure
# capture time.

function Invoke-CmdPalToggleBindingTest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateSet('General','Personalization','Dock')][string]$Page,
        [Parameter(Mandatory)][string]$ControlId,
        # Dotted path supported for nested keys, e.g. 'DockSettings.AlwaysOnTop'
        [Parameter(Mandatory)][string]$SettingsKey
    )
    Switch-CmdPalAppXSettingsPage -Hwnd $cpsHwnd -Page $Page
    $orig = _ResolveJsonPath -Obj (_ReadJsonShared $cpSettings) -Path $SettingsKey
    try {
        $new = Set-CmdPalAppXSettingsControl -Hwnd $cpsHwnd `
            -ControlId $ControlId -SettingsKey $SettingsKey -ExpectedValue (-not $orig)
        if ($new -ne (-not $orig)) {
            throw "Toggle did not flip the value: orig=$orig, new=$new"
        }
        Write-Host "    info: $SettingsKey $orig -> $new via UI toggle" -ForegroundColor DarkGray
    } finally {
        try {
            $cur = _ResolveJsonPath -Obj (_ReadJsonShared $cpSettings) -Path $SettingsKey
            if ($cur -ne $orig) {
                Set-CmdPalAppXSettingsControl -Hwnd $cpsHwnd `
                    -ControlId $ControlId -SettingsKey $SettingsKey -ExpectedValue $orig | Out-Null
            }
        } catch { Write-Warning "[cleanup] failed to restore $SettingsKey to $orig`: $($_.Exception.Message)" }
    }
}

function Invoke-CmdPalComboBoxBindingTest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateSet('General','Personalization','Dock')][string]$Page,
        [Parameter(Mandatory)][string]$ControlId,
        [Parameter(Mandatory)][string]$SettingsKey
    )
    Switch-CmdPalAppXSettingsPage -Hwnd $cpsHwnd -Page $Page
    $orig = _ResolveJsonPath -Obj (_ReadJsonShared $cpSettings) -Path $SettingsKey
    $items = @(_EnumerateComboBoxOptions -Hwnd $cpsHwnd -ControlId $ControlId)
    if ($items.Count -lt 2) {
        throw "$ControlId ComboBox has fewer than 2 items (found $($items.Count): $($items -join ', ')) — cannot test a CHANGE"
    }

    # Performance optimisation: read the ComboBox's CURRENT display
    # label so we can skip it during iteration (it almost always maps
    # to the current JSON value, so trying it wastes a full 2.5s
    # debounce timeout before we move on). The ComboBox's Name
    # property reflects the selected item's display string.
    $origDisplayLabel = $null
    try {
        $p = winapp ui get-property $ControlId -w $cpsHwnd --json 2>$null | ConvertFrom-Json -ErrorAction Stop
        if ($p -and $p.properties -and $p.properties.Name) {
            $origDisplayLabel = $p.properties.Name
        }
    } catch { }

    # Iterate items until JSON changes. Skip the currently-selected
    # display label (saves one full timeout). Use 2500ms per-item
    # timeout — CmdPal's debounce is ~2.5s, anything longer just
    # wastes time when the item maps to the SAME JSON enum value
    # (display 'Use system settings' -> JSON 'Default' is silent).
    $changedTo = $null
    $changedDisplay = $null
    try {
        foreach ($it in $items) {
            if ($it -eq $origDisplayLabel) { continue }   # skip same-as-current
            try {
                Set-CmdPalAppXSettingsControl -Hwnd $cpsHwnd `
                    -ControlId $ControlId -SettingsKey $SettingsKey `
                    -Mode Set -ExpectedValue $it -TimeoutMs 2500 | Out-Null
                $after = _ResolveJsonPath -Obj (_ReadJsonShared $cpSettings) -Path $SettingsKey
                if ($after -ne $orig) {
                    $changedTo = "display='$it' / JSON='$after'"
                    $changedDisplay = $it
                    break
                }
            } catch { }
        }
        if (-not $changedTo) {
            throw "Tried all $($items.Count) ComboBox options ($($items -join ', ')) but $SettingsKey never changed from '$orig'"
        }
        Write-Host "    info: $SettingsKey changed via UI ComboBox: $changedTo (was JSON '$orig')" -ForegroundColor DarkGray
    } finally {
        # Cleanup optimisation: restore by selecting $origDisplayLabel
        # directly if we know it (no foreach trial-and-error). The
        # ComboBox helper's Set-...Control may still time-out on the
        # 'no-op' restore (selecting the already-selected display),
        # but that only happens if JSON didn't actually change in act
        # (which would mean the test threw above and finally is just
        # cleanup-best-effort).
        try {
            $cur = _ResolveJsonPath -Obj (_ReadJsonShared $cpSettings) -Path $SettingsKey
            if ($cur -ne $orig) {
                if ($origDisplayLabel) {
                    # Fast path: select the original display label directly
                    try {
                        Set-CmdPalAppXSettingsControl -Hwnd $cpsHwnd `
                            -ControlId $ControlId -SettingsKey $SettingsKey `
                            -Mode Set -ExpectedValue $origDisplayLabel -TimeoutMs 2500 | Out-Null
                    } catch {
                        # Original label didn't restore — fall through to slow path
                    }
                }
                # Verify; if still not restored, slow-path foreach
                if ((_ResolveJsonPath -Obj (_ReadJsonShared $cpSettings) -Path $SettingsKey) -ne $orig) {
                    foreach ($it in $items) {
                        if ($it -eq $changedDisplay) { continue }   # skip the one we just confirmed changes JSON
                        try {
                            Set-CmdPalAppXSettingsControl -Hwnd $cpsHwnd `
                                -ControlId $ControlId -SettingsKey $SettingsKey `
                                -Mode Set -ExpectedValue $it -TimeoutMs 2500 | Out-Null
                            if ((_ResolveJsonPath -Obj (_ReadJsonShared $cpSettings) -Path $SettingsKey) -eq $orig) { break }
                        } catch { continue }
                    }
                }
            }
        } catch { Write-Warning "[cleanup] failed to restore $SettingsKey to '$orig': $($_.Exception.Message)" }
    }
}

# ════════════════════════════════════════════════════════════════════════
#   Dock-bucket helpers (used by 24-SettingsUI.tests.ps1 Dock E2E tests)
# ════════════════════════════════════════════════════════════════════════
# Several Dock tests (PowerDock window assertions, band content checks)
# need EnableDock=true to be in effect. These helpers ensure that state
# without each test repeating the boilerplate.

# Get the PowerDock window handle, or $null if not present.
function Get-PowerDockHwnd {
    $d = winapp ui list-windows -a 'CmdPal' --json 2>$null | ConvertFrom-Json |
         Where-Object { $_.title -eq 'PowerDock' } | Select-Object -First 1
    if ($d) { return [int64]$d.hwnd }
    return $null
}

# Enable the Dock via Settings UI; remembers the original state and
# returns it so the caller can restore in finally. Idempotent: returns
# original state without changing anything if already enabled.
#
# Usage:
#   $origEnable = Enable-CmdPalDockForTest -SettingsHwnd $cpsHwnd
#   try { ... PowerDock assertions ... }
#   finally { Disable-CmdPalDockIfWasOff -SettingsHwnd $cpsHwnd -OriginalState $origEnable }
function Enable-CmdPalDockForTest {
    [CmdletBinding()]
    param([Parameter(Mandatory)][int64]$SettingsHwnd)
    $orig = [bool](_ReadJsonShared $script:cpSettings).EnableDock
    if (-not $orig) {
        Switch-CmdPalAppXSettingsPage -Hwnd $SettingsHwnd -Page 'Dock'
        Set-CmdPalAppXSettingsControl -Hwnd $SettingsHwnd `
            -ControlId 'CmdPal_DockSettingsPage_EnableDock' `
            -SettingsKey 'EnableDock' -ExpectedValue $true | Out-Null
        # Wait for PowerDock window to actually appear
        $null = Wait-Until -TimeoutMs 5000 -PollMs 250 -IgnoreException `
            -Message "PowerDock window did not appear within 5s after EnableDock=true" `
            -Condition { Get-PowerDockHwnd }
    }
    return $orig
}

function Disable-CmdPalDockIfWasOff {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][int64]$SettingsHwnd,
        [Parameter(Mandatory)][bool]$OriginalState
    )
    if ($OriginalState) { return }   # was on originally, leave it on
    try {
        Switch-CmdPalAppXSettingsPage -Hwnd $SettingsHwnd -Page 'Dock'
        Set-CmdPalAppXSettingsControl -Hwnd $SettingsHwnd `
            -ControlId 'CmdPal_DockSettingsPage_EnableDock' `
            -SettingsKey 'EnableDock' -ExpectedValue $false | Out-Null
    } catch { Write-Warning "[cleanup] Disable-CmdPalDockIfWasOff failed: $($_.Exception.Message)" }
}

# Enumerate the visible text content of all band items inside one of
# the dock's ListViews (StartListView / CenterListView / EndListView).
# Bands render their content as Group children with friendly Name and
# nested 'lbl-titletext-*' / 'lbl-subtitletext-*' Text labels — we
# inspect with depth 9 and collect both the Group Names and the
# Text labels so callers can pattern-match for known band content
# (e.g. 'CPU' / 'Memory' for PerformanceMonitor, time format for clock).
#
# Returns a hashtable with .Groups (array of Group.Name strings),
# .Titles (array of title-text strings), .Subtitles (array of subtitle
# strings). Empty arrays if the ListView has no items.
function Get-CmdPalDockBandContent {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][int64]$DockHwnd,
        [Parameter(Mandatory)][ValidateSet('StartListView','CenterListView','EndListView')][string]$Region
    )
    # Check the region's ListView exists
    $r = winapp ui search $Region -w $DockHwnd --json 2>$null | ConvertFrom-Json
    $result = [pscustomobject]@{
        Exists    = ($r -and $r.matchCount -gt 0)
        ItemCount = 0
        Groups    = @()
        Titles    = @()
        Subtitles = @()
    }
    if (-not $result.Exists) { return $result }

    $insLines = (winapp ui inspect $Region -w $DockHwnd --depth 9 2>$null) -split "`n"
    $result.ItemCount = @($insLines | Where-Object { $_ -match '^\s*itm-microsoftcmdpal-\S+\s+ListItem\s+' }).Count
    $result.Groups = @($insLines | Where-Object { $_ -match 'grp-contentgrid-\S+\s+Group\s+"([^"]+)"' } | ForEach-Object { if ($_ -match 'Group\s+"([^"]+)"') { $matches[1] } })
    $result.Titles = @($insLines | Where-Object { $_ -match 'lbl-titletext-\S+\s+Text\s+"([^"]+)"' } | ForEach-Object { if ($_ -match 'Text\s+"([^"]+)"') { $matches[1] } })
    $result.Subtitles = @($insLines | Where-Object { $_ -match 'lbl-subtitletext-\S+\s+Text\s+"([^"]+)"' } | ForEach-Object { if ($_ -match 'Text\s+"([^"]+)"') { $matches[1] } })
    return $result
}

# ──────────────────────────────────────────────────────────────────────
#   Dock E2E test scope helpers — used by 24-SettingsUI-e2e.ps1
# ──────────────────────────────────────────────────────────────────────
# Use-CmdPalEnabledDock — wraps a Dock E2E test body in:
#   - Switch to the Dock settings page
#   - Track EnableDock's original state
#   - Enable Dock if not already enabled (the test body needs PowerDock present)
#   - try { body } finally { restore EnableDock to original state }
#
# Use this for Dock tests that ONLY need PowerDock visible (e.g.
# DefaultBandsPresentOnFirstEnable, PerformanceMonitorBandShowsLiveData,
# DateTimeBandShowsCurrentTime). Tests that VARY EnableDock itself (e.g.
# CmdPal_SettingsUI_Dock_EnableDockShowsPowerDockWindow) should NOT use
# this helper — the toggle IS the system under test.
#
#   Use-CmdPalEnabledDock -SettingsHwnd $cpsHwndB -Body {
#       $dh = Get-PowerDockHwnd
#       Assert-NotNull $dh -Because 'PowerDock window not present after enable'
#       ...
#   }
function Use-CmdPalEnabledDock {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][int64]$SettingsHwnd,
        [Parameter(Mandatory)][scriptblock]$Body
    )
    Switch-CmdPalAppXSettingsPage -Hwnd $SettingsHwnd -Page 'Dock'
    $origEnable = (_ReadJsonShared $cpSettings).EnableDock
    try {
        $null = Enable-CmdPalDockForTest -SettingsHwnd $SettingsHwnd
        & $Body
    } finally {
        try { Disable-CmdPalDockIfWasOff -SettingsHwnd $SettingsHwnd -OriginalState $origEnable }
        catch { Write-Warning "[Use-CmdPalEnabledDock cleanup] $($_.Exception.Message)" }
    }
}

# Use-CmdPalDockSetting — Use-CmdPalEnabledDock + ONE additional Dock
# ComboBox setting that gets captured + restored:
#
#   Use-CmdPalDockSetting -SettingsHwnd $cpsHwndB `
#                         -SettingKey 'DockSettings.Side' `
#                         -ControlId 'DockPositionComboBox' `
#                         -Body { ... act + assert ... }
#
# Use for Dock tests that vary ONE setting (Position, DockSize, etc.)
# AND need PowerDock visible. EnableDock is auto-handled via the
# Use-CmdPalEnabledDock pattern above; the named setting is captured
# before Body and restored after.
function Use-CmdPalDockSetting {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][int64]$SettingsHwnd,
        [Parameter(Mandatory)][string]$SettingKey,      # dotted path e.g. 'DockSettings.Side'
        [Parameter(Mandatory)][string]$ControlId,       # AutomationId of the ComboBox or toggle
        [Parameter(Mandatory)][scriptblock]$Body
    )
    Switch-CmdPalAppXSettingsPage -Hwnd $SettingsHwnd -Page 'Dock'
    $origEnable = (_ReadJsonShared $cpSettings).EnableDock
    $origValue = _ResolveJsonPath -Obj (_ReadJsonShared $cpSettings) -Path $SettingKey
    try {
        $null = Enable-CmdPalDockForTest -SettingsHwnd $SettingsHwnd
        & $Body
    } finally {
        # Restore setting first, EnableDock second — order matters if
        # the setting is something the disabled-dock path can't write.
        try {
            $curValue = _ResolveJsonPath -Obj (_ReadJsonShared $cpSettings) -Path $SettingKey
            if ($curValue -ne $origValue) {
                Set-CmdPalAppXSettingsControl -Hwnd $SettingsHwnd `
                    -ControlId $ControlId -SettingsKey $SettingKey `
                    -Mode Set -ExpectedValue $origValue | Out-Null
            }
        } catch { Write-Warning "[Use-CmdPalDockSetting cleanup] failed to restore $SettingKey to $origValue`: $($_.Exception.Message)" }
        try { Disable-CmdPalDockIfWasOff -SettingsHwnd $SettingsHwnd -OriginalState $origEnable }
        catch { Write-Warning "[Use-CmdPalDockSetting cleanup] $($_.Exception.Message)" }
    }
}

