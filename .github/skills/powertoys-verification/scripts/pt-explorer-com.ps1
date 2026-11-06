# scripts/pt-explorer-com.ps1
# Drive Explorer windows via Shell.Application COM to set up file selections, then trigger
# PT modules that read IShellItemArray from the foreground Explorer window (Peek, Image Resizer,
# PowerRename, File Locksmith, Workspaces).
#
# This bypasses needing a real mouse / interactive selection - Shell COM does the selection
# programmatically, then the PT hotkey (e.g. Ctrl+Space for Peek) fires the centralized hook
# which reads Explorer's selection at the moment of activation.
#
# Requires an interactive desktop session. If GetForegroundWindow() returns 0 or no Explorer
# windows are open, the functions return $null/$false instead of throwing - callers should
# treat that as a BLK-ENV signal (an environment block, not a product FAIL).

function Get-PtExplorerWindows {
    <#
    .SYNOPSIS
    Return all open Explorer windows as Shell COM objects (with .LocationName, .Document.Folder, etc.).
    Returns @() if no Explorer windows are open.
    #>
    try {
        $shell = New-Object -ComObject Shell.Application
        return @($shell.Windows() | Where-Object { $_.Name -eq 'File Explorer' -or $_.FullName -match 'explorer\.exe$' })
    } catch { return @() }
}

function Open-PtExplorerAtPath {
    <#
    .SYNOPSIS
    Open a fresh Explorer window at the given path. Returns the Shell COM window object.
    Useful when no Explorer is open yet.
    #>
    [CmdletBinding()] param([Parameter(Mandatory)][string]$Path)
    if (-not (Test-Path $Path)) { throw "Path not found: $Path" }
    Start-Process explorer.exe -ArgumentList $Path
    Start-Sleep -Milliseconds 1500
    $wins = Get-PtExplorerWindows
    # Note: the -replace must be wrapped in its own parens, otherwise the ',' in -replace '\\','/'
    # is parsed as a second argument to [regex]::Escape() (overload error: "argument count: 2").
    $needle = [regex]::Escape(((Resolve-Path $Path).Path -replace '\\','/'))
    return ($wins | Where-Object { $_.LocationURL -match $needle } | Select-Object -First 1)
}

function Select-PtExplorerFiles {
    <#
    .SYNOPSIS
    Select 1+ files in an open Explorer window via Shell COM. The window comes to foreground.
    .DESCRIPTION
    Uses Shell.Application's SelectItem(item, flags) API. Flags:
      0x01 = SVSI_SELECT
      0x04 = SVSI_DESELECTOTHERS  (apply to the first item only when selecting multiple)
      0x08 = SVSI_ENSUREVISIBLE
      0x20 = SVSI_FOCUSED
    Returns $true on success, $false if any file wasn't found in the folder.
    .EXAMPLE
    $win = Get-PtExplorerWindows | Select-Object -First 1
    Select-PtExplorerFiles -ExplorerWindow $win -FileNames 'test-markdown.md','test-html.html','test-source.cs'
    Send-PtChord -Mods 0x11 -Key 0x20   # Ctrl+Space -> Peek opens on 3 selected files
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$ExplorerWindow,
        [Parameter(Mandatory)][string[]]$FileNames
    )
    if (-not $ExplorerWindow.Document) { return $false }
    $folder = $ExplorerWindow.Document.Folder
    $first = $true
    foreach ($name in $FileNames) {
        $item = $folder.ParseName($name)
        if (-not $item) { Write-Warning "File not found in folder: $name"; return $false }
        # First item: SELECT + DESELECTOTHERS + ENSUREVISIBLE + FOCUSED  = 0x2D
        # Subsequent items: SELECT + ENSUREVISIBLE                      = 0x09
        $flags = if ($first) { 0x2D } else { 0x09 }
        $ExplorerWindow.Document.SelectItem($item, $flags)
        $first = $false
    }
    Start-Sleep -Milliseconds 300
    return $true
}

function Invoke-PtPeekWithExplorerSelection {
    <#
    .SYNOPSIS
    Set up an Explorer multi-file selection and trigger Peek via Ctrl+Space.
    Returns the new Peek window HWND, or $null on failure.
    .EXAMPLE
    $h = Invoke-PtPeekWithExplorerSelection -FolderPath D:\fixtures -FileNames 'a.png','b.md','c.cs'
    winapp ui invoke PinButton -w $h
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$FolderPath,
        [Parameter(Mandatory)][string[]]$FileNames
    )
    $win = Get-PtExplorerWindows | Where-Object { $_.LocationURL -match [regex]::Escape(($FolderPath -replace '\\','/')) } | Select-Object -First 1
    if (-not $win) { $win = Open-PtExplorerAtPath -Path $FolderPath }
    if (-not $win) { return $null }
    if (-not (Select-PtExplorerFiles -ExplorerWindow $win -FileNames $FileNames)) { return $null }

    # Capture pre-state Peek HWND list to detect the new window
    $beforeHwnds = @(Get-Process PowerToys.Peek.UI -EA SilentlyContinue | ForEach-Object MainWindowHandle)

    # Fire Ctrl+Space (Peek default). Requires pt-sendinput-chord.ps1 to be dot-sourced first.
    if (-not (Get-Command Send-PtChord -EA SilentlyContinue)) {
        throw "Send-PtChord not loaded. Dot-source scripts/pt-sendinput-chord.ps1 first."
    }
    Send-PtChord -Mods 0x11 -Key 0x20 | Out-Null   # Ctrl+Space
    Start-Sleep -Milliseconds 1200

    # Find the new Peek window HWND
    $afterHwnds = @(Get-Process PowerToys.Peek.UI -EA SilentlyContinue | ForEach-Object MainWindowHandle)
    $new = $afterHwnds | Where-Object { $_ -ne 0 -and $_ -notin $beforeHwnds } | Select-Object -First 1
    if (-not $new) { $new = $afterHwnds | Where-Object { $_ -ne 0 } | Select-Object -First 1 }
    return $new
}

function Test-PtInteractiveDesktop {
    <#
    .SYNOPSIS
    Probe whether the current session is interactive (foreground + Shell COM both working).
    Returns a PSCustomObject with .ForegroundOk and .ShellComOk.
    .EXAMPLE
    $env = Test-PtInteractiveDesktop
    if (-not $env.ForegroundOk -or -not $env.ShellComOk) {
        Write-Warning "Non-interactive session - Explorer-driven techniques will fail."
    }
    #>
    Add-Type 'using System; using System.Runtime.InteropServices; public class FG3 { [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow(); }' -EA SilentlyContinue
    $hasFg = $false
    for ($i = 0; $i -lt 5; $i++) {
        if ([FG3]::GetForegroundWindow() -ne [IntPtr]::Zero) { $hasFg = $true; break }
        Start-Sleep -Milliseconds 200
    }
    $shellOk = $false
    try { @((New-Object -ComObject Shell.Application).Windows()).Count | Out-Null; $shellOk = $true } catch {}
    [pscustomobject]@{ ForegroundOk = $hasFg; ShellComOk = $shellOk }
}
