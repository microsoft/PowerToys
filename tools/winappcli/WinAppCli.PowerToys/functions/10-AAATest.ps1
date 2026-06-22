# 10-AAATest.ps1 — Arrange-Act-Assert-Cleanup test harness + generic UIA helpers.
#
# Implements the xUnit-style 4A pattern adapted for PowerShell + winappCli:
#
#   Arrange  → set up state, snapshot artifacts, spawn fixtures
#              (return a hashtable; becomes the $Context for later phases)
#   Act      → drive the UI under test (set-value, invoke, click, …)
#   Assert   → verify the expected outcome (throw on failure)
#   Cleanup  → restore state ALWAYS (runs in finally; errors here don't fail
#              the test — they're warnings)
#
# Why this pattern:
#   - 80% of UIA test code is state management + cleanup. Making the phases
#     explicit forces consistent shape across modules.
#   - When a test fails, the report identifies WHICH phase failed (the
#     stack trace shows the Act block, not Cleanup).
#   - Cleanup ALWAYS runs even if Act/Assert throws — no leaked notepads,
#     no polluted clipboards, no sub-page traps for the next test.
#   - Context flows through phases as a hashtable (no globals).
#
# All four blocks receive $Context as $args[0]; Arrange may return a
# hashtable to seed the context. Example:
#
#   Invoke-AAATest -Name "Box L1024 ★ FULL: Calculator copies on Enter" `
#     -Tag direct `
#     -Arrange {
#         @{ origClip = Get-ClipboardSafe ; sentinel = "SENTINEL_$(Get-Random)" }
#     } `
#     -Act {
#         param($ctx)
#         Set-ClipboardSafe $ctx.sentinel
#         # … type 7+5, invoke Copy …
#     } `
#     -Assert {
#         param($ctx)
#         $clip = Get-ClipboardSafe
#         if ($clip -ne '12') { throw "expected '12' got '$clip'" }
#     } `
#     -Cleanup {
#         param($ctx)
#         if ($ctx.origClip) { Set-ClipboardSafe $ctx.origClip }
#     }

# ── 4A test runner ────────────────────────────────────────────────────────

# Session-level filter state. Set via Set-AAAFilter; consulted by every
# Invoke-AAATest call. -Only patterns must match; -Skip patterns must NOT
# match. Both accept wildcards (powershell -like) against the test ID
# (preferred, stable) AND the test Name (substring). Empty/null = no filter.
$script:AAAFilter = @{
    Only = @()      # array of patterns; test runs if ANY matches
    Skip = @()      # array of patterns; test is filtered-out if ANY matches
}

function Set-AAAFilter {
    <#
    .SYNOPSIS
    Configure session-level test filters consulted by Invoke-AAATest. Tests
    that don't match -Only (when set) or that match -Skip are recorded as
    SKIP with a reason like "filtered (--only)".

    .PARAMETER Only
    Array of wildcard patterns. A test runs if its Id OR Name matches ANY.
    Pass @() (empty) to clear.

    .PARAMETER Skip
    Array of wildcard patterns. A test is skipped if its Id OR Name matches
    ANY. Pass @() (empty) to clear.

    .EXAMPLE
    Set-AAAFilter -Only 'L1024*'           # only the calculator boxes
    Set-AAAFilter -Skip '*regression*'     # skip flaky stress tests
    Set-AAAFilter -Only 'L1024-FULL', 'L1029-*'   # multiple patterns
    Set-AAAFilter -Only @() -Skip @()      # clear all filters
    #>
    [CmdletBinding()]
    param([string[]]$Only, [string[]]$Skip)
    if ($PSBoundParameters.ContainsKey('Only')) { $script:AAAFilter.Only = @($Only | Where-Object { $_ }) }
    if ($PSBoundParameters.ContainsKey('Skip')) { $script:AAAFilter.Skip = @($Skip | Where-Object { $_ }) }
}

function Get-AAAFilter {
    <#
    .SYNOPSIS
    Returns the current session-level AAA test filter (Only/Skip pattern arrays).
    #>
    [CmdletBinding()]
    param()
    [pscustomobject]@{ Only = @($script:AAAFilter.Only); Skip = @($script:AAAFilter.Skip) }
}

function _AAATestMatches {
    # Internal: returns $true if ANY pattern in $Patterns matches Id or Name.
    # Used by New-TestStep's filter consultation; kept here so module-private
    # state (script:AAAFilter) and matching logic live together.
    param([string[]]$Patterns, [string]$Id, [string]$Name)
    if (-not $Patterns -or $Patterns.Count -eq 0) { return $false }
    foreach ($p in $Patterns) {
        if ($Id -and $Id -like $p)     { return $true }
        if ($Name -and $Name -like "*$p*") { return $true }
    }
    return $false
}

function Invoke-AAATest {
    <#
    .SYNOPSIS
    Runs a single test using the Arrange-Act-Assert-Cleanup pattern. Wraps
    New-TestStep so results land in the same report.

    .PARAMETER Name
    Short description of the test (printed and stored in report).
    .PARAMETER Id
    Optional short stable identifier (e.g. 'L1024-FULL', 'L1029-Walker'). Used
    by Set-AAAFilter for selecting / skipping tests. ID is more stable than
    Name for filtering — Name often gets reworded; Id should not.
    .PARAMETER Tag
    One of: direct, helper, visual, audio, skipped, info. Defaults to direct.
    .PARAMETER SkipReason
    If set, the test is recorded as SKIP and Arrange/Act/Assert/Cleanup are
    not invoked.
    .PARAMETER Ignore
    Switch — equivalent to xUnit [Fact(Skip="…")] / [Ignore]. When present,
    the test is recorded as SKIP with the IgnoreReason; Arrange/Act/Assert/
    Cleanup are not invoked. Use this for known-flaky tests you want to
    declaratively disable WITHOUT deleting the code or commenting it out.
    .PARAMETER IgnoreReason
    Required when -Ignore is set. Recorded in the report so reviewers know
    why the test is disabled (e.g. "flaky on RDP — see issue #42").
    .PARAMETER Arrange
    Optional. Sets up state. Return a hashtable to seed the $Context shared
    with Act / Assert / Cleanup. If omitted, Context starts as an empty hashtable.
    .PARAMETER Act
    Required. Drives the UI / system under test. Receives $Context as $args[0].
    .PARAMETER Assert
    Required. Verifies the expected outcome. Throw on failure. Receives $Context.
    .PARAMETER Cleanup
    Optional. Restores state. Runs in finally — ALWAYS executes, even if
    Arrange/Act/Assert threw. Errors during Cleanup are emitted as warnings,
    not test failures (the test has already failed if it got here).
    Receives $Context as $args[0].

    .NOTES
    Skip/Ignore precedence:
      1. -SkipReason (parameter set Skip) → SKIP "<reason>"
      2. -Ignore                          → SKIP "ignored: <IgnoreReason>"
      3. -Tag skipped                     → SKIP (legacy compatibility)
      4. Set-AAAFilter -Only doesn't match → SKIP "filtered (--only=<pat>)"
      5. Set-AAAFilter -Skip matches      → SKIP "filtered (--skip=<pat>)"
      6. Otherwise                        → run all four phases

    Failure semantics:
      - Arrange throws  → test FAILS with detail "Arrange: <err>"; Cleanup still runs
      - Act throws      → test FAILS with detail "Act: <err>"; Assert skipped; Cleanup runs
      - Assert throws   → test FAILS with detail "<err>"; Cleanup runs
      - Cleanup throws  → warning printed; test outcome unchanged
    #>
    [CmdletBinding(DefaultParameterSetName = 'Run')]
    param(
        [Parameter(Mandatory)][string]$Name,
        [string]$Id,
        [ValidateSet('direct','helper','visual','audio','skipped','info')][string]$Tag = 'direct',
        [Parameter(ParameterSetName = 'Skip')][string]$SkipReason,
        [Parameter(ParameterSetName = 'Run')][switch]$Ignore,
        [Parameter(ParameterSetName = 'Run')][string]$IgnoreReason,
        [Parameter(ParameterSetName = 'Run')][scriptblock]$Arrange,
        [Parameter(ParameterSetName = 'Run', Mandatory)][scriptblock]$Act,
        [Parameter(ParameterSetName = 'Run', Mandatory)][scriptblock]$Assert,
        [Parameter(ParameterSetName = 'Run')][scriptblock]$Cleanup
    )

    # 1. Explicit Skip parameter set
    if ($PSCmdlet.ParameterSetName -eq 'Skip' -or $Tag -eq 'skipped') {
        $reason = if ($SkipReason) { $SkipReason } else { '(no reason given)' }
        New-TestStep -Tag skipped -Id $Id -Name $Name -SkipReason $reason
        return
    }

    # 2. Declarative -Ignore (like xUnit [Fact(Skip=...)])
    if ($Ignore) {
        $reason = if ($IgnoreReason) { "ignored: $IgnoreReason" } else { 'ignored (no reason given)' }
        New-TestStep -Tag skipped -Id $Id -Name $Name -SkipReason $reason
        return
    }

    # 3. Run the test — New-TestStep handles -Only/-Skip filter consultation
    # (Set-AAAFilter), [Id] prefix rendering, timing, exception capture, and
    # report registration uniformly with non-AAA tests.
    New-TestStep -Tag $Tag -Id $Id -Name $Name -Body {
        $Context = @{}
        $phase = '<unknown>'
        try {
            if ($Arrange) {
                $phase = 'Arrange'
                $arrResult = & $Arrange $Context
                if ($arrResult -is [hashtable]) { $Context = $arrResult }
            }
            $phase = 'Act'
            & $Act $Context | Out-Null

            $phase = 'Assert'
            & $Assert $Context | Out-Null
        }
        catch {
            if ($phase -eq 'Assert') { throw $_ }
            throw "${phase}: $_"
        }
        finally {
            if ($Cleanup) {
                try { & $Cleanup $Context | Out-Null }
                catch {
                    Write-Host "    [cleanup] $_" -ForegroundColor Yellow
                }
            }
        }
    }
}

function Test-Case {
    <#
    .SYNOPSIS
    Light alternative to Invoke-AAATest: runs a SINGLE scriptblock as one test
    case. No $Context threading, no separate Arrange/Act/Assert parameters —
    the body is plain sequential code, conventionally marked with
    `# Arrange` / `# Act` / `# Assert` comments. Cleanup is the caller's
    responsibility via inline `try { … } finally { … }`.

    Use this when the AAA structure adds more noise than value (small tests,
    tests with simple linear flow). For complex tests where the four phases
    each have non-trivial setup, prefer Invoke-AAATest.

    .DESCRIPTION
    Honors the same Set-AAAFilter -Only/-Skip filters as Invoke-AAATest, logs
    via the same New-TestStep harness, captures exceptions and times the
    body. Throw from anywhere in the body to fail the test; return
    normally to pass.

    .PARAMETER Id
    Stable identifier (used for filtering and the report).
    .PARAMETER Name
    Human-readable description (printed and stored in the report).
    .PARAMETER Body
    The scriptblock to run. No parameters. Throw to fail.
    .PARAMETER Tag
    Optional tag — defaults to 'direct'.

    .EXAMPLE
    Test-Case -Id 'Foo_Bar' -Name 'Foo does bar' {
        # Arrange
        $orig = Get-ClipboardSafe
        Set-ClipboardSafe 'sentinel' | Out-Null
        try {
            # Act
            Use-CmdPalSubPage '=' {
                Set-UiaText 'MainSearchBox' '5+7' -Hwnd $cpHwnd -VerifyEcho
                Invoke-UiaAction 'PrimaryCommandButton' invoke -Hwnd $cpHwnd
                Start-Sleep -Milliseconds 800
            }
            # Assert
            $after = Get-ClipboardSafe
            if ($after -ne '12') { throw "Clipboard='$after' (expected '12')" }
        } finally {
            if ($orig) { Set-ClipboardSafe $orig | Out-Null }
        }
    }
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0)][string]$Id,
        [Parameter(Mandatory, Position = 1)][string]$Name,
        [Parameter(Mandatory, Position = 2)][scriptblock]$Body,
        [ValidateSet('direct','helper','visual','audio','skipped','info')][string]$Tag = 'direct'
    )
    # Delegates entirely to New-TestStep — same filtering, timing, logging
    # paths as Invoke-AAATest. The body runs as-is; exceptions are caught
    # by New-TestStep and recorded as failures with the message preserved.
    New-TestStep -Tag $Tag -Id $Id -Name $Name -Body $Body
}

# ── Generic UIA helpers usable by any module's checklist ──────────────────

function Wait-UiaListItem {
    <#
    .SYNOPSIS
    Polls a winapp-driven app's element tree until a ListItem with the given
    Name appears, or the timeout elapses. Returns the match object or $null.

    .DESCRIPTION
    Thin wrapper around Wait-Until specialised for the common pattern of
    "wait for a ListItem with name X to appear". Returns $null on timeout
    (does NOT throw) — callers decide whether absence is an error.

    .PARAMETER ExpectedName
    Exact Name property of the ListItem to wait for.
    .PARAMETER Hwnd
    Target window handle (passed as -w to winapp).
    .PARAMETER TimeoutMs
    Max wait in milliseconds. Default 3000.
    .PARAMETER PollMs
    Polling interval. Default 200.
    .PARAMETER SearchToken
    Optional override for the `winapp ui search` token. Defaults to ExpectedName.

    .EXAMPLE
    $hit = Wait-UiaListItem -ExpectedName '4' -Hwnd $cpHwnd
    if (-not $hit) { throw "Calculator did not produce '4'" }
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ExpectedName,
        [Parameter(Mandatory)][int64]$Hwnd,
        [int]$TimeoutMs = 3000,
        [int]$PollMs    = 200,
        [string]$SearchToken
    )
    if (-not $SearchToken) { $SearchToken = $ExpectedName }
    # Convert "throw on timeout" semantics of Wait-Until into "return null"
    # by catching the timeout. The condition itself returns the match record
    # which becomes Wait-Until's return value, or $null to keep polling.
    try {
        return Wait-Until -TimeoutMs $TimeoutMs -PollMs $PollMs `
            -Message "ListItem '$ExpectedName' did not appear in window $Hwnd" `
            -Condition {
                $r = winapp ui search $SearchToken -w $Hwnd --json 2>$null | ConvertFrom-Json
                @($r.matches | Where-Object {
                    $_.type -eq 'ListItem' -and $_.name -eq $ExpectedName
                }) | Select-Object -First 1
            }
    } catch {
        return $null   # timeout — caller decides whether to escalate
    }
}

function Reset-AppToHome {
    <#
    .SYNOPSIS
    Forces a target window back to its "home" state by bringing it to the
    foreground and pressing Escape several times. Useful when a previous
    test navigated into a sub-page / dialog / mode and you need a clean
    starting point. Does NOT signal app-specific events (e.g. CmdPal.Show);
    callers can chain that themselves.

    .PARAMETER Hwnd
    Window handle to reset.
    .PARAMETER EscapeCount
    Number of times to press Escape. Default 5 — handles deeply-nested pages.
    .PARAMETER ActivateFirst
    Bring the window to foreground before sending keys. Default $true.
    Required for SendInput-style key events to land on this window.
    .PARAMETER PauseMs
    Milliseconds to sleep between Escape presses. Default 200.

    .NOTES
    SAFETY GUARD: keystrokes are ONLY sent if the target window is actually
    the foreground window when each key fires. Win11 sometimes blocks
    foreground steal — without this guard, our Esc keys would land on
    whatever IS in the foreground (e.g. the calling terminal), which can
    cancel running scripts. Verify foreground every iteration; skip the
    Esc if mismatched.

    .EXAMPLE
    Reset-AppToHome -Hwnd $cpHwnd -EscapeCount 5
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][int64]$Hwnd,
        [int]$EscapeCount  = 5,
        [bool]$ActivateFirst = $true,
        [int]$PauseMs      = 200
    )
    if ($ActivateFirst) {
        try { Set-WindowForeground -Hwnd $Hwnd | Out-Null } catch {}
        Start-Sleep -Milliseconds 200
    }
    for ($i = 0; $i -lt $EscapeCount; $i++) {
        # Re-verify foreground before each keypress. If Win11 blocked our
        # steal (or another window grabbed focus), DO NOT press Esc — it
        # would land on the wrong window. Try to re-acquire foreground first;
        # if that also fails, skip this iteration silently.
        $fg = 0
        try { $fg = Get-ForegroundHwnd } catch {}
        if ($fg -ne $Hwnd) {
            try { Set-WindowForeground -Hwnd $Hwnd | Out-Null } catch {}
            Start-Sleep -Milliseconds 100
            try { $fg = Get-ForegroundHwnd } catch {}
        }
        if ($fg -eq $Hwnd) {
            try { Send-PtKey -Key 'Esc' } catch {}
        }
        Start-Sleep -Milliseconds $PauseMs
    }
}

# ── Robust clipboard with retry (transient OpenClipboard contention) ──────

function Set-ClipboardSafe {
    <#
    .SYNOPSIS
    Set the system clipboard text with retry. Win32 SetClipboardData
    transiently fails with CLIPBRD_E_CANT_OPEN when another process holds
    the clipboard for ~milliseconds (browsers, Office, anti-virus, …).
    Retries up to MaxAttempts before throwing.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$Text,
        [int]$MaxAttempts = 10
    )
    Add-Type -AssemblyName System.Windows.Forms -ErrorAction SilentlyContinue
    for ($i = 1; $i -le $MaxAttempts; $i++) {
        try {
            if ([string]::IsNullOrEmpty($Text)) {
                [System.Windows.Forms.Clipboard]::Clear()
            } else {
                [System.Windows.Forms.Clipboard]::SetText($Text)
            }
            Start-Sleep -Milliseconds 50
            $cur = [System.Windows.Forms.Clipboard]::GetText()
            if ($cur -eq $Text) { return $true }
        } catch {
            # transient OLE/CLIPBRD_E_CANT_OPEN — fall through and retry
        }
        Start-Sleep -Milliseconds 100
    }
    throw "Could not seed clipboard with '$Text' after $MaxAttempts attempts (another process may be holding the clipboard)"
}

function Get-ClipboardSafe {
    <#
    .SYNOPSIS
    Read system clipboard text with retry. Returns '' on persistent failure
    rather than throwing — clipboard reads are usually advisory.
    #>
    [CmdletBinding()]
    param([int]$MaxAttempts = 10)
    Add-Type -AssemblyName System.Windows.Forms -ErrorAction SilentlyContinue
    for ($i = 1; $i -le $MaxAttempts; $i++) {
        try { return [System.Windows.Forms.Clipboard]::GetText() }
        catch { Start-Sleep -Milliseconds 100 }
    }
    return ''
}

# ── Process leak detection / cleanup ───────────────────────────────────────

function Get-ProcessesStartedAfter {
    <#
    .SYNOPSIS
    Returns processes whose StartTime is on or after $Since, optionally
    filtered by Name. Useful for end-of-suite cleanup of fixtures (e.g.
    notepads spawned by a Window-Walker test).
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][datetime]$Since,
        [string[]]$Name
    )
    $procs = if ($Name) { Get-Process -Name $Name -ErrorAction SilentlyContinue }
             else        { Get-Process -ErrorAction SilentlyContinue }
    $procs | Where-Object {
        try { $_.StartTime -ge $Since } catch { $false }
    }
}

function Stop-ProcessesSafely {
    <#
    .SYNOPSIS
    Kills the given processes; emits a one-line "killed PID X" per process.
    Errors during Kill are swallowed (process may have died on its own).
    #>
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline = $true)][System.Diagnostics.Process[]]$Process,
        [string]$Reason = 'cleanup'
    )
    process {
        foreach ($p in $Process) {
            try {
                $name = $p.ProcessName
                $id   = $p.Id
                $p.Kill()
                Write-Host "    [$Reason] killed $name (PID $id)" -ForegroundColor DarkGray
            } catch {}
        }
    }
}
