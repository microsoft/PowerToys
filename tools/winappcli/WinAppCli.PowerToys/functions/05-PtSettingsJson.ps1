# PtSettingsJson.ps1 — Tier-B (direct JSON + restart) Settings operations.
#
# These functions edit the per-module settings.json files under
#   %LOCALAPPDATA%\Microsoft\PowerToys\<ModuleName>\settings.json
# and (optionally) restart the runner. Much faster than Tier-A for setup /
# teardown. Use Tier-A inside actual tests; Tier-B in before/after blocks.
#
# Safety: every mutation flows through Backup-PtSettings → Restore-PtSettings
# so user preferences are not permanently changed.

$script:PtSettingsRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\PowerToys'

function Get-PtSettingsJsonPath {
    <#
    .SYNOPSIS
    Return the absolute path of <Module>\settings.json under the PowerToys
    user-settings root, or $null if it doesn't exist.
    .PARAMETER Module
    Folder name as PowerToys writes it (e.g. 'Hosts', 'FancyZones',
    'Always On Top'). The casing/spaces must match the on-disk folder.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Module)
    $p = Join-Path $script:PtSettingsRoot (Join-Path $Module 'settings.json')
    if (Test-Path $p) { return $p }
    # Fall back to the global settings.json (no module subfolder)
    if ($Module -in 'General','PowerToys') {
        $g = Join-Path $script:PtSettingsRoot 'settings.json'
        if (Test-Path $g) { return $g }
    }
    return $null
}

function Backup-PtSettings {
    <#
    .SYNOPSIS
    Snapshot a module's settings.json into a temp file. Returns a token object
    you pass to Restore-PtSettings to revert.
    .PARAMETER Module
    Module folder name (e.g. 'Hosts').
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Module)
    $src = Get-PtSettingsJsonPath -Module $Module
    if (-not $src) { throw "settings.json not found for module '$Module'." }
    $dst = Join-Path $env:TEMP ("winappcli-helpers-backup-{0}-{1}.json" -f $Module, (Get-Random -Maximum 0xFFFFFF))
    Copy-Item $src $dst -Force
    return [pscustomobject]@{ Module = $Module; Source = $src; Backup = $dst; CreatedAt = Get-Date }
}

function Restore-PtSettings {
    <#
    .SYNOPSIS
    Restore a backup made by Backup-PtSettings. Optionally restarts PowerToys
    so the runner picks up the reverted state.
    .PARAMETER Token
    The object returned by Backup-PtSettings.
    .PARAMETER Restart
    If set, restarts the runner after restoring.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][pscustomobject]$Token,
        [switch]$Restart
    )
    if (-not (Test-Path $Token.Backup)) { throw "Backup file no longer exists: $($Token.Backup)" }
    Copy-Item $Token.Backup $Token.Source -Force
    Remove-Item $Token.Backup -Force -ErrorAction SilentlyContinue
    if ($Restart) { Restart-PowerToys | Out-Null }
}

function Set-PtSettingJson {
    <#
    .SYNOPSIS
    Set a property in a module's settings.json. Creates the property if missing.
    Optionally restarts PowerToys so the change takes effect.
    .PARAMETER Module
    Module folder name (e.g. 'Hosts').
    .PARAMETER PropertyPath
    Dotted JSON path to the leaf property (e.g. 'properties.ShowStartupWarning.value').
    .PARAMETER Value
    The new value (string, bool, int, etc.). Will be JSON-serialised as-is.
    .PARAMETER Restart
    If set, restarts PowerToys after writing.
    .EXAMPLE
    Set-PtSettingJson -Module Hosts -PropertyPath 'properties.ShowStartupWarning.value' -Value $false -Restart
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Module,
        [Parameter(Mandatory)][string]$PropertyPath,
        [Parameter(Mandatory)]$Value,
        [switch]$Restart
    )
    $jsonPath = Get-PtSettingsJsonPath -Module $Module
    if (-not $jsonPath) { throw "settings.json not found for module '$Module'." }
    $obj = Get-Content $jsonPath -Raw | ConvertFrom-Json
    # Walk the property path, creating intermediate objects as needed
    $segments = $PropertyPath.Split('.')
    $cursor = $obj
    for ($i = 0; $i -lt $segments.Count - 1; $i++) {
        $seg = $segments[$i]
        if ($null -eq $cursor.$seg) {
            $cursor | Add-Member -NotePropertyName $seg -NotePropertyValue ([pscustomobject]@{}) -Force
        }
        $cursor = $cursor.$seg
    }
    $leaf = $segments[-1]
    if ($cursor.PSObject.Properties.Name -contains $leaf) {
        $cursor.$leaf = $Value
    } else {
        $cursor | Add-Member -NotePropertyName $leaf -NotePropertyValue $Value -Force
    }
    $obj | ConvertTo-Json -Depth 20 | Out-File -FilePath $jsonPath -Encoding utf8
    if ($Restart) { Restart-PowerToys | Out-Null }
}

function Disable-PtModuleWarning {
    <#
    .SYNOPSIS
    Convenience for the Hosts editor's "Show warning at startup" toggle (and
    same-shape settings in other modules). Backs up, sets the warning property
    to $false, and returns a restore-token.
    .DESCRIPTION
    For Hosts, the editor reads settings.json on every launch — no runner
    restart needed. Other modules may differ; pass -Restart if your target
    module reads the setting only at runner start.
    .PARAMETER Module
    Module folder name. Defaults to 'Hosts'.
    .PARAMETER PropertyPath
    The property path to flip. Defaults to 'properties.ShowStartupWarning.value'
    (Hosts module). Override for other modules with different schemas.
    .PARAMETER Restart
    If set, restart the PowerToys runner after the JSON write. Default off
    (verified unnecessary for Hosts on 2026-05-12).
    #>
    [CmdletBinding()]
    param(
        [string]$Module = 'Hosts',
        [string]$PropertyPath = 'properties.ShowStartupWarning.value',
        [switch]$Restart
    )
    $token = Backup-PtSettings -Module $Module
    try {
        if ($Restart) {
            Set-PtSettingJson -Module $Module -PropertyPath $PropertyPath -Value $false -Restart
        } else {
            Set-PtSettingJson -Module $Module -PropertyPath $PropertyPath -Value $false
        }
    } catch {
        Restore-PtSettings -Token $token
        throw
    }
    return $token
}
