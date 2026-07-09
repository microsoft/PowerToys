# scripts/pt-shell-verbs.ps1
# Enumerate Windows classic shell verbs (HKCR-registered) via Shell.Application COM.
#
# SCOPE WARNING: this does NOT find PowerToys context-menu items on Win11. PT registers
# PowerRename, Image Resizer, File Locksmith, New+ etc. via IExplorerCommand (Tier-1 modern
# menu), which is invisible to Shell.Application.Verbs(). For PT-context-menu drives, use
# `pt-explorer-contextmenu.ps1` (synthetic right-click + UIA invoke). See
# `explorer-context-menu-flow.md` for the canonical pattern.
#
# Useful for: enumerating non-PT classic verbs (Open, Edit, Send-to, third-party shell extensions),
# and as a negative check that PT verbs are NOT classic-shadowed.

function Get-PtShellVerbs {
    <#
    .SYNOPSIS
    Enumerate classic HKCR shell verbs on a file or folder. Returns Name + the underlying Verb COM object.
    .EXAMPLE
    Get-PtShellVerbs -Path 'D:\fixtures\image.png' | Format-Table Name
    #>
    [CmdletBinding()] param([Parameter(Mandatory)][string]$Path)
    if (-not (Test-Path $Path)) { throw "Path not found: $Path" }
    $abs = (Resolve-Path $Path).Path
    $folder = Split-Path -Parent $abs
    $leaf = Split-Path -Leaf $abs
    $shell = New-Object -ComObject Shell.Application
    $ns = $shell.NameSpace($folder)
    if (-not $ns) { throw "Cannot open folder namespace: $folder" }
    $item = $ns.ParseName($leaf)
    if (-not $item) { throw "File not in folder: $leaf" }
    return @($item.Verbs()) | ForEach-Object {
        [pscustomobject]@{ Name = $_.Name; Verb = $_ }
    }
}

function Invoke-PtShellVerb {
    <#
    .SYNOPSIS
    Invoke a classic shell verb on a file by name-regex match. Returns $true on success.
    Does NOT work for PT Win11 modern-menu items - see SCOPE WARNING at top.
    .EXAMPLE
    Invoke-PtShellVerb -Path 'D:\fixtures\img.png' -NamePattern '^Edit$'
    #>
    [CmdletBinding()] param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$NamePattern
    )
    $verb = Get-PtShellVerbs -Path $Path | Where-Object { $_.Name -match $NamePattern } | Select-Object -First 1
    if (-not $verb) {
        Write-Warning "No classic shell verb matching '$NamePattern' on '$Path'. (Win11 PT modern-menu items are NOT visible here - use pt-explorer-contextmenu.ps1 instead.)"
        return $false
    }
    $verb.Verb.DoIt()
    return $true
}

function Reset-PtShellComCache {
    <#
    .SYNOPSIS
    Release current Shell.Application COM instance + force a fresh one on next call.
    Use when you've installed/registered a shell handler mid-test and the cached verb list
    still reflects the old state.
    #>
    [System.Runtime.InteropServices.Marshal]::CleanupUnusedObjectsInCurrentContext()
    [System.GC]::Collect()
    [System.GC]::WaitForPendingFinalizers()
}
