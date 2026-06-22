# TestHarness.ps1 — Reset-TestSuite, New-TestStep, Get-TestSuiteReport.
#
# Six tags supported (matches §15 of the plan):
#   direct  — UIA + filesystem only
#   helper  — UIA + small PS helper (PInvoke, JSON edit, etc.)
#   visual  — Win32 / Pixel / Hash check
#   audio   — log-grep / WASAPI loopback / process lifecycle
#   skipped — out of scope or env-specific
#   info    — informational marker (e.g. comments separating sections)

function Reset-TestSuite {
    <#
    .SYNOPSIS
    Clears any accumulated test results. Call at the top of a test script.
    #>
    [CmdletBinding()]
    param()
    $script:TestResults.Clear()
}

function New-TestStep {
    <#
    .SYNOPSIS
    Runs a single test step, times it, records the result, and prints a one-line
    status. Use throw for failure, return for success.
    .PARAMETER Tag
    One of: direct, helper, visual, audio, skipped, info.
    .PARAMETER Name
    Short description shown in the report.
    .PARAMETER Id
    Optional short stable identifier (e.g. 'CmdPal_Calculator_ReturnsFour').
    Renders as [Id] prefix in console + report. Used by Set-AAAFilter to
    select / skip tests by wildcard. More stable than Name for filtering —
    Names get reworded; Ids should not.
    .PARAMETER Body
    Scriptblock that does the work. Throw to signal failure. Do NOT use 'exit'
    inside the scriptblock — it terminates the whole script (PowerShell semantics).
    .PARAMETER SkipReason
    Required when -Tag is 'skipped'; recorded in the report.
    #>
    [CmdletBinding(DefaultParameterSetName = 'Body')]
    param(
        [Parameter(Mandatory)][ValidateSet('direct','helper','visual','audio','skipped','info')][string]$Tag,
        [Parameter(Mandatory)][string]$Name,
        [string]$Id,
        [Parameter(ParameterSetName = 'Body')][scriptblock]$Body,
        [Parameter(ParameterSetName = 'Skip')][string]$SkipReason
    )
    # Apply [Id] prefix consistently with Invoke-AAATest
    $displayName = if ($Id) { "[$Id] $Name" } else { $Name }

    if ($Tag -eq 'skipped') {
        if (-not $SkipReason) { $SkipReason = '(no reason given)' }
        $script:TestResults.Add([pscustomobject]@{
            tag = $Tag; name = $displayName; id = $Id; status = 'SKIP'; ms = 0; detail = $SkipReason
        }) | Out-Null
        Write-Host ("  [SKIP    ] {0} — {1}" -f $displayName, $SkipReason) -ForegroundColor DarkGray
        return
    }

    # Consult session-level filter (set by Set-AAAFilter). The filter applies
    # to ALL tests, regardless of which API (Invoke-AAATest vs New-TestStep)
    # registered them. Module is loaded as a unit so the script-scoped
    # $AAAFilter from 10-AAATest.ps1 is visible here.
    if (Get-Variable -Scope Script -Name 'AAAFilter' -ErrorAction SilentlyContinue) {
        # Skip-filter: any pattern match → record SKIP
        foreach ($pat in $script:AAAFilter.Skip) {
            $matches = ($Id -and $Id -like $pat) -or ($Name -and $Name -like "*$pat*")
            if ($matches) {
                $script:TestResults.Add([pscustomobject]@{
                    tag = 'skipped'; name = $displayName; id = $Id; status = 'SKIP'; ms = 0
                    detail = "filtered (--skip='$pat')"
                }) | Out-Null
                Write-Host ("  [SKIP    ] {0} — filtered (--skip='{1}')" -f $displayName, $pat) -ForegroundColor DarkGray
                return
            }
        }
        # Only-filter: when set, must match at least one
        if ($script:AAAFilter.Only -and $script:AAAFilter.Only.Count -gt 0) {
            $matched = $false
            foreach ($pat in $script:AAAFilter.Only) {
                if (($Id -and $Id -like $pat) -or ($Name -and $Name -like "*$pat*")) { $matched = $true; break }
            }
            if (-not $matched) {
                $reason = "filtered (--only='$($script:AAAFilter.Only -join ',')')"
                $script:TestResults.Add([pscustomobject]@{
                    tag = 'skipped'; name = $displayName; id = $Id; status = 'SKIP'; ms = 0; detail = $reason
                }) | Out-Null
                Write-Host ("  [SKIP    ] {0} — {1}" -f $displayName, $reason) -ForegroundColor DarkGray
                return
            }
        }
    }

    if (-not $Body) {
        throw "New-TestStep '$Name' has no -Body scriptblock."
    }
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $detail = ''
    # IMPORTANT: do NOT use $LASTEXITCODE to determine pass/fail — it leaks
    # across test bodies (e.g., a prior `winapp ui ...` call inside the previous
    # step can set it non-zero, which would mark a pure-PS body as FAIL even
    # though it never threw). The contract is: a body fails iff it throws.
    try {
        & $Body 2>&1 | Out-Null
        $sw.Stop()
        $ok = $true
    } catch {
        $sw.Stop()
        $ok = $false
        $detail = "$_"
    }
    $status = if ($ok) { 'PASS' } else { 'FAIL' }
    $color  = if ($ok) { 'Green' } else { 'Red' }
    $script:TestResults.Add([pscustomobject]@{
        tag    = $Tag; name = $displayName; id = $Id; status = $status
        ms     = $sw.ElapsedMilliseconds
        detail = $detail.Substring(0, [Math]::Min(2000, $detail.Length))
    }) | Out-Null
    $tail = if ($ok) { '' } else { "  [FAIL] $detail" }
    Write-Host ("  [{0,-8}] {1,5} ms  {2}{3}" -f $Tag.ToUpper(), $sw.ElapsedMilliseconds, $displayName, $tail) -ForegroundColor $color
}

function Get-TestSuiteReport {
    <#
    .SYNOPSIS
    Returns a summary object: counts per status, counts per tag, total ms, and
    the full per-step results. Print or persist as JSON.
    #>
    [CmdletBinding()]
    param()
    $r = $script:TestResults
    $passCount = @($r | Where-Object { $_.status -eq 'PASS' }).Count
    $failCount = @($r | Where-Object { $_.status -eq 'FAIL' }).Count
    $skipCount = @($r | Where-Object { $_.status -eq 'SKIP' }).Count
    $byTag = @()
    foreach ($g in ($r | Group-Object -Property tag)) {
        $byTag += [pscustomobject]@{
            tag   = $g.Name
            total = $g.Count
            pass  = @($g.Group | Where-Object { $_.status -eq 'PASS' }).Count
            fail  = @($g.Group | Where-Object { $_.status -eq 'FAIL' }).Count
            skip  = @($g.Group | Where-Object { $_.status -eq 'SKIP' }).Count
        }
    }
    $totalMs = 0
    foreach ($x in $r) { $totalMs += [int]$x.ms }
    [pscustomobject]@{
        passCount = $passCount
        failCount = $failCount
        skipCount = $skipCount
        total     = $r.Count
        totalMs   = $totalMs
        byTag     = $byTag
        results   = $r.ToArray()
    }
}

function Save-TestSuiteReport {
    <#
    .SYNOPSIS
    Persists the report as JSON to the given path (and writes a one-line summary).
    .PARAMETER Path
    Full path to a .json file.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)
    $report = Get-TestSuiteReport
    $report | ConvertTo-Json -Depth 5 | Out-File -FilePath $Path -Encoding utf8
    Write-Host ("Report: PASS {0} · FAIL {1} · SKIP {2} · total {3} steps · {4} ms · {5}" -f `
        $report.passCount, $report.failCount, $report.skipCount, $report.total, $report.totalMs, $Path) -ForegroundColor Cyan
}
