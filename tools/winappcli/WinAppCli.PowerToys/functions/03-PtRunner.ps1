# PtRunner.ps1 — discover, start, stop, restart the PowerToys runner.

function Get-PtRunnerExe {
    <#
    .SYNOPSIS
    Locate the PowerToys runner executable. Searches per-user and machine-wide install paths.
    Returns $null if not installed.
    #>
    [CmdletBinding()]
    param()
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'PowerToys\PowerToys.exe'),
        'C:\Program Files\PowerToys\PowerToys.exe'
    )
    foreach ($p in $candidates) { if (Test-Path $p) { return $p } }
    return $null
}

function Test-PtRunnerRunning {
    <#
    .SYNOPSIS
    Returns $true if a PowerToys.exe runner process is currently running.
    #>
    [CmdletBinding()]
    param()
    return [bool] (Get-Process PowerToys -ErrorAction SilentlyContinue)
}

function Stop-PowerToys {
    <#
    .SYNOPSIS
    Terminates all PowerToys runner + module processes. Returns the number of processes stopped.
    .DESCRIPTION
    Uses Stop-Process -Id explicitly (the only form allowed by some hosting environments).
    #>
    [CmdletBinding()]
    param([int]$WaitMs = 800)
    $stopped = 0
    $names = 'PowerToys', 'PowerToys.Settings', 'PowerToys.Hosts',
             'PowerToys.FancyZonesEditor', 'PowerToys.AdvancedPaste',
             'PowerToys.PowerLauncher', 'PowerToys.AlwaysOnTop',
             'PowerToys.ColorPickerUI', 'PowerToys.Awake'
    foreach ($name in $names) {
        Get-Process -Name $name -ErrorAction SilentlyContinue | ForEach-Object {
            try { Stop-Process -Id $_.Id -Force -ErrorAction Stop; $stopped++ } catch {}
        }
    }
    if ($stopped -gt 0) { Start-Sleep -Milliseconds $WaitMs }
    return $stopped
}

function Start-PowerToys {
    <#
    .SYNOPSIS
    Launches PowerToys runner if not already running. Waits up to -TimeoutMs (default 10 s)
    for the runner process to be alive. Returns the runner Process object, or $null on failure.
    #>
    [CmdletBinding()]
    param([int]$TimeoutMs = 10000)
    if (Test-PtRunnerRunning) {
        return Get-Process PowerToys -ErrorAction SilentlyContinue | Select-Object -First 1
    }
    $exe = Get-PtRunnerExe
    if (-not $exe) { throw "PowerToys runner not installed at expected paths." }
    $p = Start-Process -FilePath $exe -PassThru
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $TimeoutMs) {
        if (Test-PtRunnerRunning) { return $p }
        Start-Sleep -Milliseconds 300
    }
    return $null
}

function Restart-PowerToys {
    <#
    .SYNOPSIS
    Stops PowerToys (and all its module windows), then starts the runner again.
    Useful when settings changes need a runner restart to take effect.
    #>
    [CmdletBinding()]
    param([int]$TimeoutMs = 12000)
    Stop-PowerToys | Out-Null
    Start-Sleep -Milliseconds 500
    return (Start-PowerToys -TimeoutMs $TimeoutMs)
}
