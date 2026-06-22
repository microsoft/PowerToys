# 13-Asserts.ps1 — retrying assertion helpers (Playwright/Cypress style).
#
# Replaces the pattern of "do the action; sleep; check value; throw if wrong"
# with assertions that POLL for the expected condition. Catches async side-
# effects (clipboard updates after Copy, file writes after a UI invoke, etc.)
# without hard-coded sleeps.

function Assert-Eventually {
    <#
    .SYNOPSIS
    Generic retrying assertion: poll a predicate scriptblock until it returns
    truthy, or throw on timeout. Sugar over Wait-Until with assert-style
    failure formatting.

    .PARAMETER Condition
    Scriptblock returning truthy for "OK" / falsy for "still wrong".
    .PARAMETER TimeoutMs
    Default 5000.
    .PARAMETER PollMs
    Default 200.
    .PARAMETER Message
    Required — clearly states what was being asserted. Appears in failure msg.

    .EXAMPLE
    # Wait for a process to exit
    Assert-Eventually -Message "notepad.exe should exit" -TimeoutMs 5000 {
        -not (Get-Process notepad -ErrorAction SilentlyContinue)
    }
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position=0)][scriptblock]$Condition,
        [Parameter(Mandatory)][string]$Message,
        [int]$TimeoutMs = 5000,
        [int]$PollMs = 200
    )
    Wait-Until -TimeoutMs $TimeoutMs -PollMs $PollMs `
        -Message "Assert-Eventually FAILED: $Message" `
        -Condition $Condition | Out-Null
}

function Assert-EventuallyEquals {
    <#
    .SYNOPSIS
    Polls -Actual scriptblock until its return value equals -Expected, or
    throws on timeout including last-seen value vs expected.

    Critical for verifying async side-effects: clipboard updates lag behind
    a Copy invoke; file persistence lags behind a save action. Hard-coded
    sleeps to compensate are brittle. This polls until it lands.

    .PARAMETER Actual
    Scriptblock that returns the current value.
    .PARAMETER Expected
    Value to wait for. Compared with -eq (case-insensitive for strings in PS).
    .PARAMETER TimeoutMs
    Default 5000.
    .PARAMETER PollMs
    Default 200.
    .PARAMETER Message
    Required — what's being asserted.

    .EXAMPLE
    # Wait for clipboard to land on '12' after Copy invoke
    Assert-EventuallyEquals -Actual { Get-Clipboard } -Expected '12' `
        -TimeoutMs 2000 -Message "Calculator Copy should put '12' in clipboard"
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][scriptblock]$Actual,
        [Parameter(Mandatory)]$Expected,
        [Parameter(Mandatory)][string]$Message,
        [int]$TimeoutMs = 5000,
        [int]$PollMs = 200
    )
    $exp     = $Expected
    $actual  = $Actual
    $lastSeen = $null
    try {
        Wait-Until -TimeoutMs $TimeoutMs -PollMs $PollMs `
            -Message "Assert-EventuallyEquals FAILED: $Message" `
            -Condition {
                $v = & $actual
                $script:_assertEqLastSeen = $v
                if ($v -eq $exp) { return ,$v }
                return $null
            } | Out-Null
    } catch {
        # Re-throw with the actual-vs-expected diff for clearer failure messages
        $seen = $script:_assertEqLastSeen
        throw "$($_.Exception.Message)`n  expected: $exp`n  last seen: $seen"
    }
}

function Assert-EventuallyMatches {
    <#
    .SYNOPSIS
    Polls -Actual scriptblock until its return value MATCHES -Pattern (regex),
    or throws on timeout with last-seen value.

    .PARAMETER Actual
    Scriptblock that returns the current value (string-coerced).
    .PARAMETER Pattern
    Regex pattern. Use [regex] anchors / character classes as needed.
    .PARAMETER TimeoutMs
    Default 5000.
    .PARAMETER PollMs
    Default 200.
    .PARAMETER Message
    Required — what's being asserted.

    .EXAMPLE
    # Wait until the sub-page placeholder mentions 'equation'
    Assert-EventuallyMatches -Actual { winapp ui get-value 'MainSearchBox' -w $h } `
        -Pattern '(?i)equation|calculat' -TimeoutMs 3000 `
        -Message "Calc sub-page placeholder should mention 'equation' or 'calculator'"
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][scriptblock]$Actual,
        [Parameter(Mandatory)][string]$Pattern,
        [Parameter(Mandatory)][string]$Message,
        [int]$TimeoutMs = 5000,
        [int]$PollMs = 200
    )
    $pat    = $Pattern
    $actual = $Actual
    try {
        Wait-Until -TimeoutMs $TimeoutMs -PollMs $PollMs `
            -Message "Assert-EventuallyMatches FAILED: $Message" `
            -Condition {
                $v = & $actual
                $script:_assertMatchLastSeen = $v
                if ($v -and ($v -match $pat)) { return ,$v }
                return $null
            } | Out-Null
    } catch {
        $seen = $script:_assertMatchLastSeen
        throw "$($_.Exception.Message)`n  pattern: $Pattern`n  last seen: $seen"
    }
}
