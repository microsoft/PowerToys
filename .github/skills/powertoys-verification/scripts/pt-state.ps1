# scripts/pt-state.ps1
# Common state-verification helpers: settings.json diff, runner log grep, GPO log check,
# process spawn detection, AppX probe.

function Get-PtSettings {
    <#
    .SYNOPSIS
    Read the master PT settings.json (enabled.<Module> flags + run_elevated + theme + language).
    #>
    $f = "$env:LOCALAPPDATA\Microsoft\PowerToys\settings.json"
    if (-not (Test-Path $f)) { return $null }
    Get-Content $f -Raw | ConvertFrom-Json
}

function Get-PtModuleSettings {
    <#
    .SYNOPSIS
    Read a single module's settings.json (e.g. AdvancedPaste, FancyZones, etc.).
    These ARE auto-reloaded by the per-module file watcher (~3s debounce).
    #>
    param([Parameter(Mandatory)][string]$ModuleDir)
    $f = "$env:LOCALAPPDATA\Microsoft\PowerToys\$ModuleDir\settings.json"
    if (-not (Test-Path $f)) { return $null }
    Get-Content $f -Raw | ConvertFrom-Json
}

function Get-CmdPalSettings {
    <#
    .SYNOPSIS
    Read CmdPal AppX settings.json (sandboxed path). Contains 19 ProviderSettings, DockSettings,
    GalleryFeedUrl, EscapeKeyBehaviorSetting, AutoGoHomeInterval, Hotkey, Aliases, etc.
    #>
    $f = "$env:LOCALAPPDATA\Packages\Microsoft.CommandPalette_8wekyb3d8bbwe\LocalState\settings.json"
    if (-not (Test-Path $f)) { return $null }
    Get-Content $f -Raw | ConvertFrom-Json
}

function Get-PtRunnerLogTail {
    <#
    .SYNOPSIS
    Tail the latest runner-log_<date>.log file for matching lines.
    .EXAMPLE
    Get-PtRunnerLogTail -Pattern 'hotkey is invoked' -TailLines 100
    Get-PtRunnerLogTail -Pattern 'GPO sets' -TailLines 50
    #>
    param([string]$Pattern = '.*', [int]$TailLines = 50)
    $log = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\PowerToys\RunnerLogs" -Filter 'runner-log_*.log' -EA SilentlyContinue |
           Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $log) { return @() }
    Get-Content $log.FullName -Tail $TailLines -EA SilentlyContinue | Where-Object { $_ -match $Pattern }
}

function Test-PtModuleEnabled {
    <#
    .SYNOPSIS
    Check whether a specific module is enabled in master settings.json.
    Note: PT Run uses the key "PowerToys Run" (with space).
    #>
    param([Parameter(Mandatory)][string]$ModuleKey)
    $s = Get-PtSettings
    if (-not $s) { return $false }
    return [bool]$s.enabled.$ModuleKey
}

function Test-PtModuleProcess {
    <#
    .SYNOPSIS
    Return the process(es) for a module exe name (e.g. 'PowerToys.AdvancedPaste').
    Returns empty array if not running.
    #>
    param([Parameter(Mandatory)][string]$ExeName)
    @(Get-Process $ExeName -EA SilentlyContinue)
}

function Restart-PtRunner {
    <#
    .SYNOPSIS
    Kill the runner and relaunch to force fresh load of master settings.json.
    The runner does NOT auto-pickup edits to the top-level enabled.<Module> flags.
    #>
    $pt = Get-Process PowerToys -EA SilentlyContinue | Select-Object -First 1
    if ($pt) { Stop-Process -Id $pt.Id -Force; Start-Sleep -Milliseconds 800 }
    Start-Process "$env:LOCALAPPDATA\PowerToys\PowerToys.exe"
    Start-Sleep -Seconds 3
}

function Backup-PtModuleSettings {
    <#
    .SYNOPSIS
    Snapshot a module's settings.json to TEMP for restore-on-exit. Returns the backup path.
    .EXAMPLE
    $bk = Backup-PtModuleSettings -ModuleDir AdvancedPaste
    try { ... mutate ... } finally { Restore-PtModuleSettings -ModuleDir AdvancedPaste -BackupPath $bk }
    #>
    param([Parameter(Mandatory)][string]$ModuleDir)
    $src = "$env:LOCALAPPDATA\Microsoft\PowerToys\$ModuleDir\settings.json"
    if (-not (Test-Path $src)) { return $null }
    $bk = Join-Path $env:TEMP ("ptbk-$ModuleDir-$(Get-Random -Maximum 9999).json")
    Copy-Item -Path $src -Destination $bk -Force
    return $bk
}

function Restore-PtModuleSettings {
    param(
        [Parameter(Mandatory)][string]$ModuleDir,
        [Parameter(Mandatory)][string]$BackupPath
    )
    $dst = "$env:LOCALAPPDATA\Microsoft\PowerToys\$ModuleDir\settings.json"
    Copy-Item -Path $BackupPath -Destination $dst -Force
    Remove-Item $BackupPath -Force -EA SilentlyContinue
}
