# PtSettings.ps1 — Tier-A (UI-driven) Settings operations.
#
# These functions drive the actual Settings window via winapp ui — slower (~1 s
# per setting change) but exercises the real user flow including XAML binding
# writeback. For fast setup/teardown, see PtSettingsJson.ps1 (Tier B).

# Module → category map. Definitive structure derived from
# src/settings-ui/Settings.UI/SettingsXAML/Views/ShellPage.xaml.
# Top-level pages have $null. Extend as new modules are added.
$script:ModuleNavCategory = @{
    Dashboard           = $null   # top-level
    General             = $null   # top-level

    # System Tools
    AdvancedPaste       = 'SystemTools'
    Awake               = 'SystemTools'
    CmdPal              = 'SystemTools'
    ColorPicker         = 'SystemTools'
    LightSwitch         = 'SystemTools'
    PowerLauncher       = 'SystemTools'
    MeasureTool         = 'SystemTools'
    ShortcutGuide       = 'SystemTools'
    TextExtractor       = 'SystemTools'
    ZoomIt              = 'SystemTools'

    # Windowing & Layouts
    AlwaysOnTop         = 'WindowingAndLayouts'
    CropAndLock         = 'WindowingAndLayouts'
    FancyZones          = 'WindowingAndLayouts'
    GrabAndMove         = 'WindowingAndLayouts'
    Workspaces          = 'WindowingAndLayouts'

    # Input & Output
    KeyboardManager     = 'InputOutput'
    MouseUtilities      = 'InputOutput'
    MouseWithoutBorders = 'InputOutput'
    PowerDisplay        = 'InputOutput'
    QuickAccent         = 'InputOutput'

    # File Management
    PowerPreview        = 'FileManagement'
    FileLocksmith       = 'FileManagement'
    ImageResizer        = 'FileManagement'
    NewPlus             = 'FileManagement'
    Peek                = 'FileManagement'
    PowerRename         = 'FileManagement'

    # Advanced  (Hosts lives here, not File Management — important!)
    CmdNotFound         = 'Advanced'
    EnvironmentVariables= 'Advanced'
    Hosts               = 'Advanced'
    RegistryPreview     = 'Advanced'
}

function Open-PtSettings {
    <#
    .SYNOPSIS
    Open PowerToys Settings via the runner's --open-settings switch (or reuse the
    existing Settings window if one is already open). Returns @{ procId, hwnd }.

    The window is **maximized** so the NavigationView stays in expanded (sidebar)
    mode. In compact-overlay mode (the default for narrow windows), clicking a
    NavItem auto-collapses the pane, so child items like CmdNotFoundNavItem are
    not addressable. Maximizing keeps the pane open.
    .PARAMETER TimeoutMs
    How long to wait for the Settings window to appear (default 8 s).
    #>
    [CmdletBinding()]
    param([int]$TimeoutMs = 8000)
    # Reuse if already open. Tolerate the "Administrator: " title prefix that
    # Windows adds for elevated processes — when PT runs elevated, its child
    # windows inherit that prefix and the strict "^PowerToys Settings$" pattern
    # would never match.
    $titlePattern = '^(Administrator: )?PowerToys Settings$'
    $existing = Wait-WindowByTitle -TitlePattern $titlePattern -AppName 'PowerToys.Settings' -TimeoutMs 1000
    if ($existing) {
        $hwnd = $existing.hwnd
    } else {
        $runner = Get-PtRunnerExe
        if (-not $runner) { throw "PowerToys runner not found." }
        & $runner --open-settings | Out-Null
        $w = Wait-WindowByTitle -TitlePattern $titlePattern -AppName 'PowerToys.Settings' -TimeoutMs $TimeoutMs
        if (-not $w) { throw "Settings window did not appear within ${TimeoutMs}ms." }
        $existing = $w
        $hwnd = $w.hwnd
    }
    # Maximize so the NavView is in expanded (left-pane) mode.
    if (-not ('WinAppCli.PtWindowState' -as [type])) {
        Add-Type -TypeDefinition @"
            using System;
            using System.Runtime.InteropServices;
            namespace WinAppCli {
                public static class PtWindowState {
                    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
                    [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr hWnd);
                    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
                    public const int SW_HIDE      = 0;
                    public const int SW_SHOWNORMAL = 1;
                    public const int SW_MAXIMIZE  = 3;
                }
            }
"@
    }
    if (-not [WinAppCli.PtWindowState]::IsZoomed([IntPtr]$hwnd)) {
        [WinAppCli.PtWindowState]::ShowWindow([IntPtr]$hwnd, [WinAppCli.PtWindowState]::SW_MAXIMIZE) | Out-Null
        Start-Sleep -Milliseconds 500
    }
    return [pscustomobject]@{ procId = $existing.processId; hwnd = $hwnd }
}

function Switch-PtSettingsPage {
    <#
    .SYNOPSIS
    Navigate Settings to the page for $Module. Expands the parent NavView
    category if needed, then clicks the child item. Returns nothing on success;
    throws on failure.

    Note: assumes the Settings window is wide enough that the NavView is in
    expanded (left-pane) mode. Open-PtSettings maximizes the window for this
    reason. Without this, NavView runs in compact-overlay mode where the pane
    auto-collapses after each click and child items are unreachable.
    .PARAMETER Module
    Module name as used in $script:ModuleNavCategory (e.g. 'FancyZones', 'Hosts').
    .PARAMETER Hwnd
    Settings window HWND from Open-PtSettings.
    .PARAMETER Category
    Optional override for the parent NavView category. Defaults from the map.
    #>
    [CmdletBinding()]
    [Alias('Goto-PtSettingsPage')]
    param(
        [Parameter(Mandatory)][string]$Module,
        [Parameter(Mandatory)][int]$Hwnd,
        [string]$Category
    )
    if (-not $Category) {
        if ($script:ModuleNavCategory.ContainsKey($Module)) {
            $Category = $script:ModuleNavCategory[$Module]
        }
    }
    if ($Category) {
        # Expand the parent category if not already expanded. Idempotent — only
        # invokes when ExpandCollapseState is currently Collapsed.
        $catItem = "${Category}NavItem"
        $catProps = winapp ui get-property $catItem -w $Hwnd --json 2>$null | ConvertFrom-Json
        if ($catProps -and $catProps.properties.ExpandCollapseState -eq 'Collapsed') {
            winapp ui invoke $catItem -w $Hwnd 2>$null | Out-Null
            Start-Sleep -Milliseconds 600
        }
    }
    $navItem = "${Module}NavItem"
    winapp ui invoke $navItem -w $Hwnd 2>$null | Out-Null
    Start-Sleep -Milliseconds 800
}

function Get-PtSettingsToggle {
    <#
    .SYNOPSIS
    Return the current state ('On' or 'Off') of a ToggleSwitch by AutomationId.
    .PARAMETER AutomationId
    The toggle's AutomationProperties.AutomationId (e.g. 'EnableFancyZonesToggleSwitch').
    .PARAMETER Hwnd
    Window HWND.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$AutomationId,
        [Parameter(Mandatory)][int]$Hwnd
    )
    $resp = winapp ui get-property $AutomationId -w $Hwnd --json 2>$null | ConvertFrom-Json
    if (-not $resp) { throw "Element '$AutomationId' not found." }
    $state = $resp.properties.ToggleState
    if ($state -eq 'On') { return 'On' }
    if ($state -eq 'Off') { return 'Off' }
    if ($state) { return $state }
    throw "Element '$AutomationId' is not a Toggle (no ToggleState)."
}

function Set-PtSettingsToggle {
    <#
    .SYNOPSIS
    Idempotently set a ToggleSwitch to On or Off. Reads current state and
    invokes only when a change is needed. Returns $true if a change was made.
    .PARAMETER AutomationId
    The toggle's AutomationId.
    .PARAMETER Value
    Desired state: 'On' or 'Off'.
    .PARAMETER Hwnd
    Window HWND.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$AutomationId,
        [Parameter(Mandatory)][ValidateSet('On','Off')][string]$Value,
        [Parameter(Mandatory)][int]$Hwnd
    )
    $current = Get-PtSettingsToggle -AutomationId $AutomationId -Hwnd $Hwnd
    if ($current -eq $Value) { return $false }
    winapp ui invoke $AutomationId -w $Hwnd | Out-Null
    Start-Sleep -Milliseconds 300
    $after = Get-PtSettingsToggle -AutomationId $AutomationId -Hwnd $Hwnd
    if ($after -ne $Value) {
        throw "Toggle '$AutomationId' did not reach '$Value' (still '$after')."
    }
    return $true
}
