# scripts/pt-cmdpal-recycle.ps1
# Recover CmdPal AppX from "stuck" states (TextChanged-broken, sub-page hang, foreground-lock).
# The helper Microsoft.CmdPal.Ext.PowerToys is kept alive so the CmdPal.Show event listener wiring
# is not lost on recycle.

function Reset-CmdPalAppX {
    <#
    .SYNOPSIS
    Kill the Microsoft.CmdPal.UI process and relaunch the AppX. Returns the new HWND or 0 on failure.
    .NOTES
    Symptoms requiring this:
      - set-value MainSearchBox echoes the text but ZERO ListItems appear within 1.5s
      - winapp ui invoke <button> hangs subsequent inspect calls
      - Force-PtForeground returns false repeatedly
    #>
    $cp = Get-Process Microsoft.CmdPal.UI -ErrorAction SilentlyContinue
    if ($cp) {
        Stop-Process -Id $cp.Id -Force
        $deadline = (Get-Date).AddSeconds(5)
        while ((Get-Process -Id $cp.Id -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
            Start-Sleep -Milliseconds 200
        }
    }
    Start-Process 'shell:AppsFolder\Microsoft.CommandPalette_8wekyb3d8bbwe!App'
    $deadline = (Get-Date).AddSeconds(10)
    do {
        Start-Sleep -Milliseconds 300
        $r = winapp ui list-windows -a Microsoft.CmdPal.UI 2>$null | Out-String
        if ($r -match 'HWND (\d+):') { return [IntPtr][int64]$matches[1] }
    } while ((Get-Date) -lt $deadline)
    return [IntPtr]::Zero
}

function Reset-CmdPalToHome {
    <#
    .SYNOPSIS
    Navigate CmdPal back to the home page from any sub-page by invoking BackButton via UIA.
    CmdPal's Esc handler is unreachable via SendInput from elevated sessions (UIPI), and Esc-via-
    PostMessage is filtered by the WinUI 3 raw-input hook. BackButton invoke via UIA InvokePattern
    works regardless.
    #>
    $homePlaceholder = 'Search for apps, files and commands'
    for ($i = 0; $i -lt 6; $i++) {
        $cur = winapp ui get-value 'MainSearchBox' -a Microsoft.CmdPal.UI 2>$null
        if ($cur -and ($cur -match [regex]::Escape($homePlaceholder))) { break }
        winapp ui invoke 'BackButton' -a Microsoft.CmdPal.UI 2>$null | Out-Null
        Start-Sleep -Milliseconds 200
    }
    # Re-signal Show in case BackButton dismissed the window
    if (Get-Command Invoke-PtSharedEvent -ErrorAction SilentlyContinue) {
        try { Invoke-PtSharedEvent -Name 'CmdPal.Show' | Out-Null } catch {}
    }
    Start-Sleep -Milliseconds 500
}

function Test-CmdPalDegraded {
    <#
    .SYNOPSIS
    Probe the AppX with a known-good query ('notepad') and verify >=1 ListItem appears within
    1500ms. Returns $true if degraded (TextChanged-broken).
    #>
    Reset-CmdPalToHome
    winapp ui set-value 'MainSearchBox' 'notepad' -a Microsoft.CmdPal.UI 2>$null | Out-Null
    $deadline = (Get-Date).AddMilliseconds(1500)
    do {
        $insLines = (winapp ui inspect -a Microsoft.CmdPal.UI --depth 7 -i 2>$null) -split "`n"
        $items = $insLines | Where-Object { $_ -match 'itm-' -and $_ -match 'ListItem' }
        if ($items.Count -gt 0) {
            winapp ui set-value 'MainSearchBox' '' -a Microsoft.CmdPal.UI 2>$null | Out-Null
            return $false
        }
        Start-Sleep -Milliseconds 150
    } while ((Get-Date) -lt $deadline)
    return $true
}

function Invoke-CmdPalQuery {
    <#
    .SYNOPSIS
    Type a query into MainSearchBox after returning to home. Auto-recovers if AppX is degraded.
    Returns the result items as an array of strings (text lines starting with itm-).
    .EXAMPLE
    $items = Invoke-CmdPalQuery -Query 'notepad'
    if ($items | Where-Object { $_ -match 'Notepad' }) { 'PASS' } else { 'FAIL' }
    #>
    param([Parameter(Mandatory)][string]$Query, [int]$WaitMs = 800)
    Reset-CmdPalToHome
    winapp ui set-value 'MainSearchBox' $Query -a Microsoft.CmdPal.UI 2>$null | Out-Null
    Start-Sleep -Milliseconds $WaitMs
    $out = winapp ui inspect -a Microsoft.CmdPal.UI --depth 7 -i 2>$null | Out-String
    $items = ($out -split "`r?`n" | Where-Object { $_ -match 'itm-' -and $_ -match 'ListItem' })
    if ($items.Count -eq 0) {
        if (Test-CmdPalDegraded) {
            Reset-CmdPalAppX | Out-Null
            return (Invoke-CmdPalQuery -Query $Query -WaitMs $WaitMs)
        }
    }
    return $items
}
