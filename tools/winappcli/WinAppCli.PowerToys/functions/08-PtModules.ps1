# 08-PtModules.ps1 — process orchestration for individual PowerToys modules.

$script:PtModuleExePaths = @{
    'Hosts'                = @('PowerToys\WinUI3Apps\PowerToys.Hosts.exe', 'WinUI3Apps\PowerToys.Hosts.exe')
    'AlwaysOnTop'          = @('PowerToys\PowerToys.AlwaysOnTop.exe')
    'AwakeUI'              = @('PowerToys\WinUI3Apps\PowerToys.Awake.exe')
    'ColorPicker'          = @('PowerToys\PowerToys.ColorPickerUI.exe')
    'CropAndLock'          = @('PowerToys\PowerToys.CropAndLock.exe')
    'EnvironmentVariables' = @('PowerToys\WinUI3Apps\PowerToys.EnvironmentVariables.exe')
    'FancyZones'           = @('PowerToys\PowerToys.FancyZones.exe')
    'FancyZonesEditor'     = @('PowerToys\PowerToys.FancyZonesEditor.exe')
    'FileLocksmith'        = @('PowerToys\WinUI3Apps\PowerToys.FileLocksmithUI.exe')
    'ImageResizer'         = @('PowerToys\PowerToys.ImageResizer.exe')
    'KeyboardManager'      = @('PowerToys\PowerToys.KeyboardManagerEditor.exe')
    'MouseHighlighter'     = @('PowerToys\PowerToys.MouseHighlighter.exe')
    'PowerLauncher'        = @('PowerToys\PowerToys.PowerLauncher.exe')
    'PowerOcr'             = @('PowerToys\PowerToys.PowerOCR.exe')
    'PowerRename'          = @('PowerToys\WinUI3Apps\PowerToys.PowerRename.exe')
    'PowerToys'            = @('PowerToys\PowerToys.exe')
    'RegistryPreview'      = @('PowerToys\WinUI3Apps\PowerToys.RegistryPreview.exe')
    'ScreenRuler'          = @('PowerToys\PowerToys.MeasureToolUI.exe')
    'ShortcutGuide'        = @('PowerToys\PowerToys.ShortcutGuide.exe')
    'TextExtractor'        = @('PowerToys\WinUI3Apps\PowerToys.PowerOCR.exe')
    'Workspaces'           = @('PowerToys\WinUI3Apps\PowerToys.WorkspacesEditor.exe')
    'ZoomIt'               = @('PowerToys\WinUI3Apps\PowerToys.ZoomIt.exe')
}

function Get-PtModuleExe {
    <#
    .SYNOPSIS
    Locate a PowerToys module's executable. Searches per-user and machine-wide installs.
    Returns full path or $null.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Module)
    $candidates = $script:PtModuleExePaths[$Module]
    if (-not $candidates) { return $null }
    $roots = @($env:LOCALAPPDATA, 'C:\Program Files')
    foreach ($r in $roots) {
        foreach ($c in $candidates) {
            $p = Join-Path $r $c
            if (Test-Path $p) { return $p }
        }
    }
    return $null
}

function Start-PtModule {
    <#
    .SYNOPSIS
    Launch a PowerToys module's executable. Returns @{ procId, hwnd } once the
    main window is detectable, or throws on timeout.
    .PARAMETER Module
    Logical module name (key in $PtModuleExePaths).
    .PARAMETER Args
    Optional arguments to pass.
    .PARAMETER WindowTitlePattern
    Regex the window title must match. Defaults to module name substring.
    .PARAMETER TimeoutMs
    Window-discovery timeout. Default 8 s.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Module,
        [string[]]$Args,
        [string]$WindowTitlePattern = $Module,
        [int]$TimeoutMs = 8000
    )
    $exe = Get-PtModuleExe -Module $Module
    if (-not $exe) { throw "Could not locate $Module executable in any known install path" }
    $procArgs = @{ FilePath = $exe; PassThru = $true }
    if ($Args) { $procArgs['ArgumentList'] = $Args }
    $p = Start-Process @procArgs
    $win = Wait-WindowByTitle -TitlePattern $WindowTitlePattern -ProcId $p.Id -TimeoutMs $TimeoutMs
    if (-not $win) {
        # Some modules (AlwaysOnTop, FancyZones) are tray-only with no window.
        return [pscustomobject]@{ procId = $p.Id; hwnd = $null; exe = $exe }
    }
    return [pscustomobject]@{ procId = $p.Id; hwnd = [int]$win.hwnd; exe = $exe }
}

function Stop-PtModule {
    <#
    .SYNOPSIS
    Stop all running instances of a module by process name (derived from the
    module's exe). Returns the number of processes stopped.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Module, [int]$WaitMs = 500)
    $exe = Get-PtModuleExe -Module $Module
    if (-not $exe) { return 0 }
    $procName = [IO.Path]::GetFileNameWithoutExtension($exe)
    $stopped = 0
    Get-Process -Name $procName -ErrorAction SilentlyContinue | ForEach-Object {
        try { Stop-Process -Id $_.Id -Force -ErrorAction Stop; $stopped++ } catch {}
    }
    if ($stopped -gt 0) { Start-Sleep -Milliseconds $WaitMs }
    return $stopped
}

function Test-PtModuleEnabled {
    <#
    .SYNOPSIS
    Returns $true if a module is enabled in PowerToys settings.json. Reads the
    `enabled.<Module>` flag in %LOCALAPPDATA%\Microsoft\PowerToys\settings.json.
    .PARAMETER Module
    Module key as it appears in settings.json's `enabled` map.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Module)
    $settingsJson = Join-Path $env:LOCALAPPDATA 'Microsoft\PowerToys\settings.json'
    if (-not (Test-Path $settingsJson)) { return $null }
    try {
        $obj = Get-Content $settingsJson -Raw | ConvertFrom-Json
        if ($obj.enabled.PSObject.Properties.Name -contains $Module) {
            return [bool]$obj.enabled.$Module
        }
    } catch {}
    return $null
}

function Read-PtModuleLog {
    <#
    .SYNOPSIS
    Search a module's log files for a regex pattern. Returns matching lines,
    or empty array if none. Looks in
    %LOCALAPPDATA%\Microsoft\PowerToys\<Module>\Logs\**\*.log.
    .PARAMETER Module
    Module folder name (e.g. 'AlwaysOnTop', 'ColorPicker').
    .PARAMETER Pattern
    Regex passed to Select-String.
    .PARAMETER LastN
    Only consider the most recent N log files (by mtime). Default 5.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Module,
        [Parameter(Mandatory)][string]$Pattern,
        [int]$LastN = 5
    )
    $logRoot = Join-Path $env:LOCALAPPDATA "Microsoft\PowerToys\$Module\Logs"
    if (-not (Test-Path $logRoot)) { return @() }
    $files = Get-ChildItem $logRoot -Recurse -Filter '*.log' -ErrorAction SilentlyContinue |
             Sort-Object LastWriteTime -Descending | Select-Object -First $LastN
    if (-not $files) { return @() }
    return @(Select-String -Path ($files | ForEach-Object FullName) -Pattern $Pattern -ErrorAction SilentlyContinue)
}
