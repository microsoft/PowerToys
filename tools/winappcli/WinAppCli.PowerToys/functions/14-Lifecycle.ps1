# 14-Lifecycle.ps1 — block-scoped state snapshots + process restart helpers.
#
# Pattern: "snapshot X → run body → ALWAYS restore X" lives in many tests.
# Wrap it in `Use-` helpers (named after C#'s `using` block, JS `try/finally`)
# so the cleanup is automatic and test bodies stay linear.

function Use-ClipboardSnapshot {
    <#
    .SYNOPSIS
    Snapshot the current clipboard, optionally seed a sentinel value, run a
    scriptblock, then ALWAYS restore the original clipboard (try/finally).
    Replaces the pattern in every Calc/Copy test:

        $orig = Get-ClipboardSafe
        Set-ClipboardSafe 'sentinel' | Out-Null
        try { … } finally { if ($orig) { Set-ClipboardSafe $orig | Out-Null } }

    With this helper:

        Use-ClipboardSnapshot -Sentinel "WINAPPCLI_$(Get-Random)" {
            # body runs with clipboard = sentinel
            # verify body left a different value in clipboard
        }
        # original clipboard restored here

    .PARAMETER ScriptBlock
    Body to run. Clipboard sentinel (if provided) is set before invocation.
    .PARAMETER Sentinel
    Optional value to write to the clipboard before running the body. If
    omitted, the clipboard is left as the user had it.

    .EXAMPLE
    Use-ClipboardSnapshot -Sentinel 'WINAPPCLI_CALC_SENTINEL' {
        Use-CmdPalSubPage '=' {
            Set-UiaText 'MainSearchBox' '7+5' -Hwnd $cpHwnd -VerifyEcho
            Invoke-UiaAction 'PrimaryCommandButton' invoke -Hwnd $cpHwnd
        }
        Assert-EventuallyEquals -Actual { Get-ClipboardSafe } -Expected '12' `
            -Message 'Calc Copy should put 12 in clipboard' -TimeoutMs 2000
    }
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position=0)][scriptblock]$ScriptBlock,
        [string]$Sentinel
    )
    $orig = Get-ClipboardSafe
    if ($PSBoundParameters.ContainsKey('Sentinel')) {
        Set-ClipboardSafe $Sentinel | Out-Null
    }
    try {
        return & $ScriptBlock
    } finally {
        if ($null -ne $orig) {
            try { Set-ClipboardSafe $orig | Out-Null } catch {}
        }
    }
}

function New-FreshAppX {
    <#
    .SYNOPSIS
    Kill an AppX UI process (by ProcessName) and relaunch via shell:AppsFolder
    URI. Waits for the new process to appear. Useful for between-test isolation
    when test state pollution becomes a problem.

    .PARAMETER ProcessName
    Process name to kill (e.g. 'Microsoft.CmdPal.UI').
    .PARAMETER RelaunchUri
    shell:AppsFolder URI to relaunch the AppX (e.g.
    'shell:AppsFolder\Microsoft.CommandPalette_8wekyb3d8bbwe!App').
    .PARAMETER WaitAfterKillMs
    How long to wait after killing before relaunching. Default 2000ms — gives
    Windows time to release the AppX file lock.
    .PARAMETER WaitAfterLaunchMs
    How long to wait after launching before returning. Default 5000ms — gives
    the AppX process time to spawn its main window.

    .EXAMPLE
    New-FreshAppX -ProcessName 'Microsoft.CmdPal.UI' `
                  -RelaunchUri 'shell:AppsFolder\Microsoft.CommandPalette_8wekyb3d8bbwe!App'
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ProcessName,
        [Parameter(Mandatory)][string]$RelaunchUri,
        [int]$WaitAfterKillMs   = 2000,
        [int]$WaitAfterLaunchMs = 5000
    )
    $proc = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($proc) {
        try { Stop-Process -Id $proc.Id -Force -ErrorAction Stop } catch {}
        Start-Sleep -Milliseconds $WaitAfterKillMs
    }
    Start-Process $RelaunchUri
    Start-Sleep -Milliseconds $WaitAfterLaunchMs
    return Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Select-Object -First 1
}
