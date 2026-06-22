# Common.ps1 — environment checks, winappCli helpers, slug disambiguator.

function Wait-Until {
    <#
    .SYNOPSIS
    Generic event-driven wait primitive — repeatedly evaluates Condition
    until it returns truthy, returns that value, or throws on timeout.

    .DESCRIPTION
    Modelled on Selenium's WebDriverWait / Java's FluentWait:

        // Selenium C#
        WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
        IWebElement el = wait.Until(d => d.FindElement(By.Id("foo")));

        // Java FluentWait
        Wait<WebDriver> wait = new FluentWait<>(driver)
            .withTimeout(Duration.ofSeconds(30))
            .pollingEvery(Duration.ofMillis(500))
            .ignoring(NoSuchElementException.class);
        WebElement foo = wait.until(d -> d.findElement(By.id("foo")));

    Use this everywhere a test would otherwise hand-roll a deadline +
    polling + null/throw loop. The condition's return value is what
    Wait-Until returns — so you both "wait for X" AND "fetch X" in one call.

    .PARAMETER Condition
    Scriptblock evaluated on each poll. Return any truthy value to succeed
    (the value is returned by Wait-Until). Return $false / $null / empty
    string to "not yet, keep polling".

    .PARAMETER TimeoutMs
    How long to keep retrying before throwing, in milliseconds. Default
    10000 (10 s). Always milliseconds — there is no -TimeoutSec because
    one unit is enough to remember.

    .PARAMETER PollMs
    Pause between condition evaluations. Default 200ms. Lower = faster
    response when condition flips, higher CPU; higher = vice versa.

    .PARAMETER Message
    Prefix for the timeout exception message. Default 'Condition did not
    become true'. Make it descriptive — it's the only signal you'll have
    when a wait times out in CI logs.

    .PARAMETER IgnoreException
    When set, exceptions thrown by Condition are treated as "not yet, keep
    polling" and the last exception is included in the timeout message if
    the wait ultimately fails. Without this switch, the first exception
    propagates immediately. Equivalent to Selenium's
    FluentWait.ignoring(ExceptionType.class).

    .EXAMPLE
    # Wait up to 5s for a UIA element to exist; throw with descriptive message on timeout
    $item = Wait-Until -TimeoutMs 5000 -Message "Calc result '4' not found" {
        $r = winapp ui search '4' -w $cpHwnd --json 2>$null | ConvertFrom-Json
        $r.matches | Where-Object { $_.type -eq 'ListItem' -and $_.name -eq '4' } | Select-Object -First 1
    }

    .EXAMPLE
    # Wait for a property to stop being a stale default value
    Wait-Until -TimeoutMs 3000 -PollMs 100 -Message "Primary stuck on home default" {
        $pri = (winapp ui get-property 'PrimaryCommandButton' -w $h --json | ConvertFrom-Json).properties.Name
        if ($pri -and $pri -ne 'Open in default browser') { return $pri }
    }

    .EXAMPLE
    # Wait for a window to appear, ignoring transient "no such window" errors
    $hwnd = Wait-Until -TimeoutMs 30000 -IgnoreException -Message "Settings did not open" {
        (Get-Process MyApp -ErrorAction Stop).MainWindowHandle
    }

    .NOTES
    GOTCHA: Wait-Until is unreliable for returning arrays from Condition.
    Two reasons:
      1. The "[array]/strip-to-last-element" line below was originally
         intended to coerce implicit multi-value returns to a single value
         for truthiness, but it ALSO strips intentional ,$arr comma-trick
         returns down to the last element.
      2. PowerShell function returns unroll arrays through the pipeline,
         so even if Wait-Until returns an array intact, $x = Wait-Until {}
         will turn a single-element array into a scalar.
    If your Condition logically produces an array, use Wait-Until purely as
    a presence check (Condition returns @(...).Count -gt 0) and re-fetch
    the array in the caller AFTER Wait-Until returns. See e.g.
    cmdpal/22-Navigation.tests.ps1 for the pattern.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0)]
        [scriptblock]$Condition,

        [Alias('Timeout')]
        [int]$TimeoutMs = 10000,

        [Alias('Poll','Interval')]
        [int]$PollMs = 200,

        [string]$Message = 'Condition did not become true',

        [switch]$IgnoreException,

        # When set, ignore the WINAPPCLI_SLOW_FACTOR env knob (use $TimeoutMs
        # exactly as given). Use for waits where a longer deadline would
        # change semantics (e.g. negative-assertion polls in Wait-StaysTrue).
        [switch]$NoSlowFactor
    )
    # Apply slow-machine multiplier to deadline so the same test code runs
    # on a fast dev box (factor=1) and a slow CI runner (factor=3-5).
    # PollMs is NOT scaled — finer polling on slow boxes is harmless.
    if (-not $NoSlowFactor) {
        $factor = Get-WinAppCliSlowFactor
        if ($factor -gt 1) {
            $TimeoutMs = [int]($TimeoutMs * $factor)
        }
    }
    $start    = Get-Date
    $deadline = $start.AddMilliseconds($TimeoutMs)
    $lastError = $null
    do {
        try {
            $result = & $Condition
            # PowerShell scriptblocks can implicitly return multiple values;
            # convert to a single value for truthiness check.
            if ($result -is [array]) { $result = $result[-1] }
            if ($result) { return $result }
        } catch {
            $lastError = $_
            if (-not $IgnoreException) { throw }
        }
        if ((Get-Date) -ge $deadline) { break }
        Start-Sleep -Milliseconds $PollMs
    } while ((Get-Date) -lt $deadline)
    $elapsedMs = [int]((Get-Date) - $start).TotalMilliseconds
    $msg = "$Message (timed out after ${TimeoutMs}ms [${elapsedMs}ms elapsed], $((($elapsedMs / $PollMs) -as [int])) polls)"
    if ($lastError) { $msg += "`n  last exception: $($lastError.Exception.Message)" }
    throw $msg
}

function Get-WinAppCliSlowFactor {
    <#
    .SYNOPSIS
    Returns a positive double multiplier for all UI-automation timeouts.

    .DESCRIPTION
    Reads $env:WINAPPCLI_SLOW_FACTOR. Defaults to 1.0 (fast dev box).
    Typical CI/VM setting is 3.0-5.0. Invalid/missing values clamp to 1.0.
    Centralized so any helper can opt into slow-mode without re-parsing.

    Used by Wait-Until (default-on) and by hand-rolled poll loops that
    want to widen their deadlines on slow boxes. Call once at the start
    of a poll loop and multiply your deadline by it.

    .EXAMPLE
    $deadline = (Get-Date).AddMilliseconds(2000 * (Get-WinAppCliSlowFactor))
    #>
    [CmdletBinding()]
    param()
    $raw = $env:WINAPPCLI_SLOW_FACTOR
    if ([string]::IsNullOrWhiteSpace($raw)) { return 1.0 }
    $parsed = 0.0
    if ([double]::TryParse($raw, [ref]$parsed) -and $parsed -gt 0) {
        return [double]$parsed
    }
    return 1.0
}

function Wait-StaysTrue {
    <#
    .SYNOPSIS
    Negative-assertion wait primitive — polls Condition over a duration
    and fails the FIRST time the condition becomes falsy.

    .DESCRIPTION
    The semantic opposite of Wait-Until. Use this when you need to assert
    "X stays true for the next N seconds" — typically post-action liveness
    checks ("after restarting AppX, the process must not crash within 4s").

    A naive Start-Sleep + single check is WRONG for this on slow machines:
    longer sleep = the crash window grows = test more likely to PASS
    falsely. This helper polls every PollMs and fails as soon as a crash
    is detected. The duration IS the assertion budget, not just padding.

    Like Wait-Until, honors WINAPPCLI_SLOW_FACTOR by default — slow CI
    runners get a wider observation window proportional to their slowdown.
    Pass -NoSlowFactor to opt out.

    .EXAMPLE
    # Confirm CmdPal.UI survives 2 seconds after a settings restart.
    Wait-StaysTrue -DurationMs 2000 -Message 'CmdPal.UI crashed' {
        [bool](Get-Process Microsoft.CmdPal.UI -ErrorAction SilentlyContinue)
    }
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0)]
        [scriptblock]$Condition,

        [Alias('Duration')]
        [int]$DurationMs = 2000,

        [Alias('Poll','Interval')]
        [int]$PollMs = 200,

        [string]$Message = 'Condition flipped from true to false during observation window',

        [switch]$NoSlowFactor
    )
    if (-not $NoSlowFactor) {
        $factor = Get-WinAppCliSlowFactor
        if ($factor -gt 1) { $DurationMs = [int]($DurationMs * $factor) }
    }
    $start    = Get-Date
    $deadline = $start.AddMilliseconds($DurationMs)
    # Require at least one truthy reading at t=0 — if it starts false,
    # something is already broken and we should fail immediately rather
    # than emit a misleading "stayed true" pass.
    $first = & $Condition
    if (-not $first) {
        throw "${Message}: condition was false at t=0 (cannot stay true if it never started true)"
    }
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds $PollMs
        $result = & $Condition
        if (-not $result) {
            $elapsedMs = [int]((Get-Date) - $start).TotalMilliseconds
            throw "${Message} after ${elapsedMs}ms (observation window was ${DurationMs}ms)"
        }
    }
    return $true
}

function Test-WinAppCliInstalled {
    <#
    .SYNOPSIS
    Returns $true when winapp.exe is on PATH.
    #>
    [CmdletBinding()]
    param()
    return [bool] (Get-Command winapp -ErrorAction SilentlyContinue)
}

function Get-WinAppCliVersion {
    <#
    .SYNOPSIS
    Returns the installed winappCli version string (e.g. '0.3.1') or $null if not installed.
    #>
    [CmdletBinding()]
    param()
    if (-not (Test-WinAppCliInstalled)) { return $null }
    $raw = & winapp --version 2>$null
    # winapp prints a banner above the version line; the version itself matches semver
    $line = ($raw | Select-String '^\d+\.\d+\.\d+' | Select-Object -First 1)
    if ($line) { return $line.Line.Trim() }
    return ($raw | Where-Object { $_ -match '\d+\.\d+\.\d+' } | Select-Object -Last 1)
}

function Test-IsElevated {
    <#
    .SYNOPSIS
    Returns $true if the current PowerShell is running as Administrator.
    #>
    [CmdletBinding()]
    param()
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    return (New-Object Security.Principal.WindowsPrincipal($id)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Assert-Elevated {
    <#
    .SYNOPSIS
    Throws a clear error if the current shell is not elevated. Use at the top of
    test scripts that touch elevated-only utilities (Hosts editor, Mouse Without
    Borders, anything that writes to %WinDir%).
    .PARAMETER Reason
    Human-readable reason shown in the error.
    #>
    [CmdletBinding()]
    param([string]$Reason = 'this operation requires administrator privileges')
    if (-not (Test-IsElevated)) {
        throw "Elevated PowerShell required: $Reason. Re-run from 'Run as Administrator'."
    }
}

function Get-EntrySlugs {
    <#
    .SYNOPSIS
    Returns the slugs of UI elements that represent **actual entries** (rows
    in the editor's list) whose name matches $Text. Filters out labels,
    groups and other non-row elements that share the same name text.
    .DESCRIPTION
    `winapp ui search 'foo'` matches ANY element whose Name contains 'foo'
    — for a single Hosts entry, that includes the ListItem (the row), the
    Group (the row's content container) and the TextBlock (the visible IP
    label). All three carry the same name. Counting raw matches gives 3
    per entry, which is structurally a false positive for "are there N
    entries". This helper filters to the ListItem rows only.
    .PARAMETER Text
    Substring to match against the entry's Name (typically the IP address).
    .PARAMETER Hwnd
    Hosts editor window HWND.
    .OUTPUTS
    Array of selector strings — one per actual entry row.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Text,
        [Parameter(Mandatory)][int]$Hwnd
    )
    $resp = winapp ui search $Text -w $Hwnd --json 2>$null | ConvertFrom-Json
    if (-not $resp -or -not $resp.matches) { return @() }
    return @($resp.matches | Where-Object { $_.type -eq 'ListItem' } | ForEach-Object { $_.selector })
}

function Get-FirstSlug {
    <#
    .SYNOPSIS
    Returns the slug of the FIRST element matching $Text whose selector starts
    with $TypePrefix, scoped to window $Hwnd.
    .DESCRIPTION
    Disambiguates the common case where multiple elements share a Name (e.g. an
    "Active" toggle on every entry, or a dialog "Address" label + edit + hint).
    Use a TypePrefix like 'btn', 'txt', 'lbl', 'itm', 'tab' to filter to the
    element kind you want.
    .PARAMETER Text
    Text to search for (case-insensitive substring against Name/AutomationId).
    .PARAMETER TypePrefix
    Slug prefix the desired element should have (btn, txt, lbl, itm, tab, mnu, …).
    .PARAMETER Hwnd
    Target window HWND.
    .EXAMPLE
    Get-FirstSlug -Text 'Active' -TypePrefix 'btn' -Hwnd $h
    # → 'btn-active-bcd6'  (the first toggle, even when 5 entries each have one)
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Text,
        [Parameter(Mandatory)][string]$TypePrefix,
        [Parameter(Mandatory)][int]$Hwnd
    )
    $resp = winapp ui search $Text -w $Hwnd --json 2>$null | ConvertFrom-Json
    if (-not $resp -or -not $resp.matches) { return $null }
    $first = @($resp.matches) | Where-Object { $_.selector -like "$TypePrefix-*" } | Select-Object -First 1
    return $first.selector
}

function Wait-WindowByTitle {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$TitlePattern,
        [int]$ProcId,
        [string]$AppName,
        [int]$TimeoutMs = 8000
    )
    # Returns the matching window record, or $null on timeout (does NOT
    # throw — many callers tolerate "not found" with a fallback path).
    try {
        return Wait-Until -TimeoutMs $TimeoutMs -PollMs 300 `
            -Message "no window matched '$TitlePattern' (proc=$ProcId app=$AppName)" `
            -Condition {
                $sources = @()
                if ($ProcId)  { $sources += $ProcId.ToString() }
                if ($AppName) { $sources += $AppName }
                foreach ($source in $sources) {
                    $w = winapp ui list-windows -a $source --json 2>$null | ConvertFrom-Json
                    if ($w) {
                        $match = @($w) | Where-Object { $_.title -match $TitlePattern } | Select-Object -First 1
                        if ($match) { return $match }
                    }
                }
                return $null
            }
    } catch {
        return $null
    }
}

function Assert-PtControlExists {
    <#
    .SYNOPSIS
    Throws if no element matching $Text is found within $TimeoutMs in window $Hwnd.
    Polls every 200ms. Use this everywhere a checklist box reduces to "this
    control exists on the page".
    .PARAMETER Text
    Substring or AutomationId to search for. Stable AutomationIds are preferred.
    .PARAMETER Hwnd
    Target window HWND.
    .PARAMETER TimeoutMs
    How long to wait before giving up. Default 2000.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Text,
        [Parameter(Mandatory)][int]$Hwnd,
        [int]$TimeoutMs = 2000
    )
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $TimeoutMs) {
        $r = winapp ui search $Text -w $Hwnd --json 2>$null | ConvertFrom-Json
        if ($r -and $r.matches -and @($r.matches).Count -gt 0) { return }
        Start-Sleep -Milliseconds 200
    }
    throw "Settings page does not expose '$Text' (timed out after ${TimeoutMs}ms)"
}
