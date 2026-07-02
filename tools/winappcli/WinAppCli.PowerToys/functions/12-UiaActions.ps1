# 12-UiaActions.ps1 — Selenium/Playwright-style high-level UIA wrappers.
#
# These exist to kill boilerplate. Every test was doing:
#     (winapp ui get-property X -w $h --json | ConvertFrom-Json).properties.Y
#     winapp ui set-value X "foo" -w $h ; Start-Sleep 1500
#     winapp ui wait-for X -w $h -t 3000 --json | Out-Null ; winapp ui invoke X -w $h
# Now it's:
#     Get-UiaProperty X Y -Hwnd $h
#     Set-UiaText X 'foo' -Hwnd $h -VerifyEcho
#     Invoke-UiaAction X invoke -Hwnd $h

function Get-UiaProperty {
    <#
    .SYNOPSIS
    Read a single UIA property in one call. Shorthand for the
    `(winapp ui get-property X -w $h --json | ConvertFrom-Json).properties.Y`
    chain that we type dozens of times.

    Returns $null if the element doesn't exist OR the property is missing.
    Does NOT wait — if you need to wait for a value to appear or change,
    use Wait-Until / Wait-UiaProperty / `winapp ui wait-for`.

    .PARAMETER Selector
    UIA selector — semantic slug, AutomationId, or text. Same vocabulary
    as `winapp ui get-property`.
    .PARAMETER Property
    Property name to read (e.g. 'Name', 'IsEnabled', 'IsSelected', 'Value').
    .PARAMETER Hwnd
    Target window handle.

    .EXAMPLE
    $pri = Get-UiaProperty 'PrimaryCommandButton' 'Name' -Hwnd $cpHwnd
    if ($pri -eq 'Copy') { … }
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position=0)][string]$Selector,
        [Parameter(Mandatory, Position=1)][string]$Property,
        [Parameter(Mandatory)][int64]$Hwnd
    )
    $obj = winapp ui get-property $Selector -w $Hwnd --json 2>$null | ConvertFrom-Json
    if (-not $obj -or -not $obj.properties) { return $null }
    return $obj.properties.$Property
}

function Set-UiaText {
    <#
    .SYNOPSIS
    Type text into a UIA Edit/TextBox element. Optionally verifies the box
    echoes the value (catches the "AppX suspended / TextChanged-broken"
    failure modes where set-value succeeds but the text never lands).

    .PARAMETER Selector
    UIA selector for the input element (typically an AutomationId).
    .PARAMETER Text
    String to write. Use '' to clear.
    .PARAMETER Hwnd
    Target window handle.
    .PARAMETER VerifyEcho
    When set, polls get-value up to -TimeoutMs and throws if the box
    doesn't show Text. Recommended for inputs that drive downstream
    behaviour (search boxes, command palettes).
    .PARAMETER TimeoutMs
    How long to wait for the echo (only used with -VerifyEcho). Default 2000.

    .EXAMPLE
    Set-UiaText 'MainSearchBox' '2+2' -Hwnd $cpHwnd -VerifyEcho
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position=0)][string]$Selector,
        [Parameter(Mandatory, Position=1)][AllowEmptyString()][string]$Text,
        [Parameter(Mandatory)][int64]$Hwnd,
        [switch]$VerifyEcho,
        [int]$TimeoutMs = 2000
    )
    winapp ui set-value $Selector $Text -w $Hwnd 2>$null | Out-Null
    if ($VerifyEcho) {
        $hwndLocal = $Hwnd
        $selLocal  = $Selector
        $textLocal = $Text
        Wait-Until -TimeoutMs $TimeoutMs -PollMs 100 `
            -Message "set-value '$Selector' did not echo '$Text' in window $Hwnd" `
            -Condition {
                (winapp ui get-value $selLocal -w $hwndLocal 2>$null) -eq $textLocal
            } | Out-Null
    }
}

function Invoke-UiaAction {
    <#
    .SYNOPSIS
    Wait for a UIA element to exist, then perform an action on it. Mirrors
    Playwright's auto-wait-then-act model:

        await page.locator('foo').click()    # JS
        Invoke-UiaAction 'foo' click -Hwnd $h # PowerShell

    .PARAMETER Selector
    UIA selector — semantic slug, AutomationId, or text.
    .PARAMETER Action
    One of 'invoke' (UIA InvokePattern), 'click' (mouse simulation), or
    'focus' (SetFocus). 'invoke' is preferred when available (works without
    foreground); 'click' is the fallback for elements that don't expose
    InvokePattern (e.g. ListItems on some controls).
    .PARAMETER Hwnd
    Target window handle.
    .PARAMETER TimeoutMs
    Max wait for the element to appear. Default 3000.

    .EXAMPLE
    Invoke-UiaAction 'BackButton' invoke -Hwnd $cpHwnd
    Invoke-UiaAction 'itm-12-xxxx' click -Hwnd $cpHwnd
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position=0)][string]$Selector,
        [Parameter(Mandatory, Position=1)][ValidateSet('invoke','click','focus')][string]$Action,
        [Parameter(Mandatory)][int64]$Hwnd,
        [int]$TimeoutMs = 3000
    )
    # Use winappCli's native wait-for: native polling at 100ms is faster
    # than a PS Wait-Until loop. wait-for returns rich JSON; on timeout
    # found:false and we throw with a useful message.
    $waitOut = winapp ui wait-for $Selector -w $Hwnd -t $TimeoutMs --json 2>$null | ConvertFrom-Json
    if (-not $waitOut.found) {
        throw "UIA element '$Selector' did not appear in window $Hwnd within ${TimeoutMs}ms (Invoke-UiaAction -Action $Action)"
    }
    winapp ui $Action $Selector -w $Hwnd 2>$null | Out-Null
}

function Wait-AnyOf {
    <#
    .SYNOPSIS
    Wait until ANY of N condition scriptblocks returns truthy. Returns the
    first truthy value. Throws on timeout listing which conditions were
    checked. Selenium's ExpectedConditions.AnyOf equivalent.

    .PARAMETER Conditions
    Array of scriptblocks. Pass with -Conditions @({...}, {...}) — the
    array syntax is explicit so PowerShell doesn't try to treat each
    block as a positional parameter.
    .PARAMETER TimeoutMs
    Max wait. Default 5000.
    .PARAMETER PollMs
    Polling interval. Default 150 (faster than Wait-Until's 200 because
    we're typically using Wait-AnyOf to detect quick state transitions).
    .PARAMETER Message
    Prefix for the timeout exception. Each condition's index appears in
    the failure message to help diagnose which side of the OR is broken.

    .EXAMPLE
    # Wait for either Primary to change OR placeholder to show sub-page text
    Wait-AnyOf -TimeoutMs 5000 -Message "alias '=' didn't navigate" -Conditions @(
        { (Get-UiaProperty 'PrimaryCommandButton' Name -Hwnd $h) -eq 'Copy' },
        { (winapp ui get-value 'MainSearchBox' -w $h 2>$null) -match 'equation' }
    )
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][scriptblock[]]$Conditions,
        [int]$TimeoutMs = 5000,
        [int]$PollMs = 150,
        [string]$Message = 'None of the conditions became true'
    )
    $conds = $Conditions
    return Wait-Until -TimeoutMs $TimeoutMs -PollMs $PollMs -Message $Message `
        -Condition {
            for ($i = 0; $i -lt $conds.Count; $i++) {
                $r = & $conds[$i]
                if ($r -is [array]) { $r = $r[-1] }
                if ($r) { return $r }
            }
            return $null
        }
}

function Wait-AllOf {
    <#
    .SYNOPSIS
    Wait until ALL N condition scriptblocks return truthy. Returns the array
    of values. Throws on timeout including which condition(s) were still false.

    .PARAMETER Conditions
    Array of scriptblocks. Pass with -Conditions @({...}, {...}).
    .PARAMETER TimeoutMs
    Max wait. Default 5000.
    .PARAMETER PollMs
    Polling interval. Default 150.
    .PARAMETER Message
    Prefix for the timeout exception.

    .EXAMPLE
    Wait-AllOf -TimeoutMs 5000 -Conditions @(
        { (Get-UiaProperty 'BackButton'         'IsEnabled' -Hwnd $h) -eq $true },
        { (Get-UiaProperty 'PrimaryCommandButton' 'Name'    -Hwnd $h) -eq 'Copy' }
    )
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][scriptblock[]]$Conditions,
        [int]$TimeoutMs = 5000,
        [int]$PollMs = 150,
        [string]$Message = 'Not all conditions became true'
    )
    $conds = $Conditions
    $count = $conds.Count
    return Wait-Until -TimeoutMs $TimeoutMs -PollMs $PollMs -Message $Message `
        -Condition {
            $results = New-Object 'object[]' $count
            for ($i = 0; $i -lt $count; $i++) {
                $r = & $conds[$i]
                if ($r -is [array]) { $r = $r[-1] }
                if (-not $r) { return $null }
                $results[$i] = $r
            }
            return ,$results
        }
}

function Wait-PropertyChange {
    <#
    .SYNOPSIS
    Wait for a UIA element's property to transition. Two modes:
      -To <value>          wait until property EQUALS that value
      -From <value>        wait until property is anything OTHER than that value

    Useful when winappCli's native `wait-for -p X --value Y` is close but not
    quite right (no native --not-value, no From semantics). Internally uses
    Wait-Until + Get-UiaProperty.

    .PARAMETER Selector
    UIA selector for the element.
    .PARAMETER Property
    Property name to read on each poll.
    .PARAMETER Hwnd
    Target window handle.
    .PARAMETER To
    Wait until property value EQUALS this. Mutually exclusive with -From.
    .PARAMETER From
    Wait until property value DIFFERS from this. Mutually exclusive with -To.
    .PARAMETER TimeoutMs
    Default 5000.
    .PARAMETER PollMs
    Default 150.

    .EXAMPLE
    Wait-PropertyChange -Selector 'PrimaryCommandButton' -Property 'Name' `
        -Hwnd $h -To 'Copy' -TimeoutMs 3000
    #>
    [CmdletBinding(DefaultParameterSetName='To')]
    param(
        [Parameter(Mandatory, Position=0)][string]$Selector,
        [Parameter(Mandatory, Position=1)][string]$Property,
        [Parameter(Mandatory)][int64]$Hwnd,
        [Parameter(Mandatory, ParameterSetName='To')]$To,
        [Parameter(Mandatory, ParameterSetName='From')]$From,
        [int]$TimeoutMs = 5000,
        [int]$PollMs = 150
    )
    $selLocal  = $Selector
    $propLocal = $Property
    $hwndLocal = $Hwnd
    if ($PSCmdlet.ParameterSetName -eq 'To') {
        $target = $To
        return Wait-Until -TimeoutMs $TimeoutMs -PollMs $PollMs `
            -Message "UIA $Selector.$Property did not become '$To'" `
            -Condition {
                $v = Get-UiaProperty $selLocal $propLocal -Hwnd $hwndLocal
                if ($v -eq $target) { return ,$v }
                return $null
            }
    } else {
        $original = $From
        return Wait-Until -TimeoutMs $TimeoutMs -PollMs $PollMs `
            -Message "UIA $Selector.$Property did not change from '$From'" `
            -Condition {
                $v = Get-UiaProperty $selLocal $propLocal -Hwnd $hwndLocal
                if ($null -ne $v -and $v -ne $original) { return ,$v }
                return $null
            }
    }
}

function Wait-ListCount {
    <#
    .SYNOPSIS
    Wait until a ListView/ItemsList has a certain number of ListItem children.
    Counts via `winapp ui inspect <selector> --depth 1` and the text-mode
    `ListItem` markers (more reliable than JSON inspect on some sub-pages).

    .PARAMETER Selector
    UIA selector for the container (typically 'ItemsList').
    .PARAMETER Hwnd
    Target window handle.
    .PARAMETER AtLeast
    Wait until child count >= this. Mutually exclusive with -Equals/-AtMost.
    .PARAMETER Equals
    Wait until child count == this. Mutually exclusive with -AtLeast/-AtMost.
    .PARAMETER AtMost
    Wait until child count <= this. Mutually exclusive with -AtLeast/-Equals.
    .PARAMETER TimeoutMs
    Default 5000.
    .PARAMETER PollMs
    Default 200.

    .EXAMPLE
    # Wait for ItemsList to have at least 3 ListItems
    Wait-ListCount -Selector 'ItemsList' -Hwnd $h -AtLeast 3 -TimeoutMs 3000
    #>
    [CmdletBinding(DefaultParameterSetName='AtLeast')]
    param(
        [Parameter(Mandatory, Position=0)][string]$Selector,
        [Parameter(Mandatory)][int64]$Hwnd,
        [Parameter(Mandatory, ParameterSetName='AtLeast')][int]$AtLeast,
        [Parameter(Mandatory, ParameterSetName='Equals')][int]$Equals,
        [Parameter(Mandatory, ParameterSetName='AtMost')][int]$AtMost,
        [int]$TimeoutMs = 5000,
        [int]$PollMs = 200
    )
    $selLocal  = $Selector
    $hwndLocal = $Hwnd
    $mode      = $PSCmdlet.ParameterSetName
    $target    = switch ($mode) { 'AtLeast' { $AtLeast } 'Equals' { $Equals } 'AtMost' { $AtMost } }
    return Wait-Until -TimeoutMs $TimeoutMs -PollMs $PollMs `
        -Message "ListItem count under '$Selector' did not reach $mode $target" `
        -Condition {
            $ins = (winapp ui inspect $selLocal -w $hwndLocal --depth 1 2>$null) -split "`n"
            $count = @($ins | Where-Object { $_ -match '^\s*itm-\S+\s+ListItem\s+' }).Count
            $ok = switch ($mode) {
                'AtLeast' { $count -ge $target }
                'Equals'  { $count -eq $target }
                'AtMost'  { $count -le $target }
            }
            if ($ok) { return ,$count }
            return $null
        }
}
