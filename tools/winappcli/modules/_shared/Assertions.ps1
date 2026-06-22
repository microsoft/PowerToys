#Requires -Version 7.0
# Assertions.ps1 — uniform assertion vocabulary for module checklists (shared across modules/*)
#
# Replaces hand-rolled `if (...) { throw "..." }` sites in the CmdPal
# test suite with a small set of Assert-* verbs that:
#   - Put the assertion intent FIRST (Assert-Equal vs `if (-ne)`)
#   - Format failure messages uniformly ("Expected X, got Y — because Z")
#   - Truncate large values (multi-KB JSON dumps don't flood CI logs)
#   - Always include the caller's optional -Because reason
#   - Accept -Because as either a [string] (eager) OR a [scriptblock]
#     that is only invoked on failure (R2-2 — for expensive diagnostic
#     context like a UIA probe that should only fire when we're about
#     to throw anyway).
#
# Use inside Test-Case bodies — they throw on failure which New-TestStep
# catches and records as a test failure (same path as direct `throw`).
#
# Dot-sourced from each module checklist (or its _helpers.ps1) so every test file has access
# without per-file imports.

# ──────────────────────────────────────────────────────────────────────
#   Private helpers (prefix _ — not for external callers)
# ──────────────────────────────────────────────────────────────────────

# Format a value for inclusion in an assertion failure message:
#   - long strings get truncated with ... (truncated, N more chars)
#   - arrays get summarized as [a, b, c, ... (N total)]
#   - PSObjects get type-name + key-count summary
#   - other types fall back to [string]$value
# R2-9: keeps CI logs scannable when one fails — a multi-KB JSON dump
# in a single-line message scrolls the real signal off-screen.
function _FormatForMessage {
    param(
        [AllowNull()]$Value,
        [int]$MaxLen = 200
    )
    if ($null -eq $Value) { return '<null>' }
    if ($Value -is [System.Collections.IList] -and $Value -isnot [string]) {
        $count = $Value.Count
        $sample = ($Value | Select-Object -First 6 | ForEach-Object { _FormatForMessage $_ 60 }) -join ', '
        if ($count -le 6) { return "[$sample]" }
        return "[$sample, ... ($count total)]"
    }
    if ($Value -is [System.Management.Automation.PSCustomObject]) {
        $keys = @($Value.PSObject.Properties.Name)
        return "[PSCustomObject k=$($keys.Count): $(($keys | Select-Object -First 6) -join ',')$(if ($keys.Count -gt 6) { ',...' })]"
    }
    $s = [string]$Value
    if ($s.Length -le $MaxLen) { return $s }
    $extra = $s.Length - $MaxLen
    return $s.Substring(0, $MaxLen) + "... (truncated, $extra more chars)"
}

# Resolve -Because into a final string. Accepts:
#   - $null / empty   → returns $null (no Because suffix)
#   - [string]        → returns as-is
#   - [scriptblock]   → invokes ONLY here (caller's "diagnose lazily on
#                       failure" path: e.g. expensive UIA probe)
function _ResolveBecause {
    param([AllowNull()]$Because)
    if ($null -eq $Because) { return $null }
    if ($Because -is [scriptblock]) {
        try { return [string](& $Because) }
        catch { return "<-Because scriptblock threw: $($_.Exception.Message)>" }
    }
    return [string]$Because
}

# Combine a base message + Because-result into the final throw text.
function _MakeAssertMessage {
    param([string]$Base, [AllowNull()]$Because)
    $b = _ResolveBecause $Because
    if ([string]::IsNullOrEmpty($b)) { return $Base }
    return "$Base — $b"
}

# ──────────────────────────────────────────────────────────────────────
#   Scalar assertions
# ──────────────────────────────────────────────────────────────────────

function Assert-True {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position=0)][AllowNull()]$Actual,
        # -Because accepts [string] OR [scriptblock] (lazy-evaluated only on failure)
        [AllowNull()]$Because
    )
    if (-not $Actual) {
        throw (_MakeAssertMessage "Expected truthy value, got '$(_FormatForMessage $Actual)'" $Because)
    }
}

function Assert-False {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position=0)][AllowNull()]$Actual,
        [AllowNull()]$Because
    )
    if ($Actual) {
        throw (_MakeAssertMessage "Expected falsy value, got '$(_FormatForMessage $Actual)'" $Because)
    }
}

function Assert-Equal {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position=0)][AllowNull()]$Actual,
        [Parameter(Mandatory, Position=1)][AllowNull()]$Expected,
        [AllowNull()]$Because
    )
    if ($Actual -ne $Expected) {
        throw (_MakeAssertMessage "Expected '$(_FormatForMessage $Expected)', got '$(_FormatForMessage $Actual)'" $Because)
    }
}

function Assert-NotEqual {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position=0)][AllowNull()]$Actual,
        [Parameter(Mandatory, Position=1)][AllowNull()]$NotExpected,
        [AllowNull()]$Because
    )
    if ($Actual -eq $NotExpected) {
        throw (_MakeAssertMessage "Expected NOT '$(_FormatForMessage $NotExpected)', got '$(_FormatForMessage $Actual)'" $Because)
    }
}

function Assert-NotNull {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position=0)][AllowNull()]$Actual,
        [AllowNull()]$Because
    )
    if ($null -eq $Actual) {
        throw (_MakeAssertMessage 'Expected non-null value, got null' $Because)
    }
}

function Assert-Null {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position=0)][AllowNull()]$Actual,
        [AllowNull()]$Because
    )
    if ($null -ne $Actual) {
        throw (_MakeAssertMessage "Expected null, got '$(_FormatForMessage $Actual)'" $Because)
    }
}

# ──────────────────────────────────────────────────────────────────────
#   Numeric assertions
# ──────────────────────────────────────────────────────────────────────

function Assert-GreaterThan {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position=0)]$Actual,
        [Parameter(Mandatory, Position=1)]$Threshold,
        [AllowNull()]$Because
    )
    if (-not ($Actual -gt $Threshold)) {
        throw (_MakeAssertMessage "Expected value > $Threshold, got $Actual" $Because)
    }
}

function Assert-LessThan {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position=0)]$Actual,
        [Parameter(Mandatory, Position=1)]$Threshold,
        [AllowNull()]$Because
    )
    if (-not ($Actual -lt $Threshold)) {
        throw (_MakeAssertMessage "Expected value < $Threshold, got $Actual" $Because)
    }
}

# ──────────────────────────────────────────────────────────────────────
#   String / regex assertions
# ──────────────────────────────────────────────────────────────────────

function Assert-Match {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position=0)][AllowNull()][string]$Value,
        [Parameter(Mandatory, Position=1)][string]$Pattern,
        [AllowNull()]$Because
    )
    # NOTE: Assert-Match runs `-match` in this function's scope, so the
    # automatic $Matches variable does NOT leak to the caller. If you
    # need capture-groups in the caller, use raw `-match` and check the
    # boolean yourself rather than using Assert-Match.
    if ($Value -notmatch $Pattern) {
        throw (_MakeAssertMessage "Expected value matching /$Pattern/, got '$(_FormatForMessage $Value)'" $Because)
    }
}

function Assert-NotMatch {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position=0)][AllowNull()][string]$Value,
        [Parameter(Mandatory, Position=1)][string]$Pattern,
        [AllowNull()]$Because
    )
    if ($Value -match $Pattern) {
        throw (_MakeAssertMessage "Expected value NOT matching /$Pattern/, got '$(_FormatForMessage $Value)' (matched)" $Because)
    }
}

# ──────────────────────────────────────────────────────────────────────
#   Collection assertions
# ──────────────────────────────────────────────────────────────────────

function Assert-Contains {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position=0)][AllowNull()][AllowEmptyCollection()][object[]]$Collection,
        [Parameter(Mandatory, Position=1)][AllowNull()]$Value,
        [AllowNull()]$Because
    )
    if ($null -eq $Collection -or -not ($Collection -contains $Value)) {
        $count = if ($Collection) { $Collection.Count } else { 0 }
        throw (_MakeAssertMessage "Expected collection to contain '$(_FormatForMessage $Value)' but it did not (count=$count; sample: $(_FormatForMessage $Collection))" $Because)
    }
}

function Assert-NotContains {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position=0)][AllowNull()][AllowEmptyCollection()][object[]]$Collection,
        [Parameter(Mandatory, Position=1)][AllowNull()]$Value,
        [AllowNull()]$Because
    )
    if ($Collection -and ($Collection -contains $Value)) {
        throw (_MakeAssertMessage "Expected collection to NOT contain '$(_FormatForMessage $Value)' but it did" $Because)
    }
}

function Assert-Empty {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position=0)][AllowNull()][AllowEmptyCollection()][object[]]$Collection,
        [AllowNull()]$Because
    )
    if ($Collection -and $Collection.Count -gt 0) {
        throw (_MakeAssertMessage "Expected empty collection, got $($Collection.Count) item(s): $(_FormatForMessage $Collection)" $Because)
    }
}

function Assert-CountGreaterThanOrEqual {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position=0)][AllowNull()][AllowEmptyCollection()][object[]]$Collection,
        [Parameter(Mandatory, Position=1)][int]$MinCount,
        [AllowNull()]$Because
    )
    $count = if ($Collection) { $Collection.Count } else { 0 }
    if ($count -lt $MinCount) {
        throw (_MakeAssertMessage "Expected collection of size >= $MinCount, got $count" $Because)
    }
}

# ──────────────────────────────────────────────────────────────────────
#   Path / process / JSON assertions
# ──────────────────────────────────────────────────────────────────────

function Assert-PathExists {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position=0)][string]$Path,
        [AllowNull()]$Because
    )
    if (-not (Test-Path $Path)) {
        throw (_MakeAssertMessage "Expected path to exist: $Path" $Because)
    }
}

function Assert-PathNotExists {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position=0)][string]$Path,
        [AllowNull()]$Because
    )
    if (Test-Path $Path) {
        throw (_MakeAssertMessage "Expected path to NOT exist: $Path" $Because)
    }
}

function Assert-ProcessRunning {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position=0)][string]$Name,
        [AllowNull()]$Because
    )
    $p = Get-Process -Name $Name -ErrorAction SilentlyContinue
    if (-not $p) {
        throw (_MakeAssertMessage "Expected process '$Name' to be running, but it is not" $Because)
    }
}

function Assert-JsonHasProperty {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position=0)][object]$Obj,
        [Parameter(Mandatory, Position=1)][string]$Path,
        [AllowNull()]$Because
    )
    # Dotted-path traversal (e.g. 'DockSettings.ShowLabels')
    $cur = $Obj
    foreach ($seg in $Path -split '\.') {
        if ($null -eq $cur -or -not $cur.PSObject.Properties.Name.Contains($seg)) {
            throw (_MakeAssertMessage "Expected JSON object to have property '$Path' (missing at segment '$seg')" $Because)
        }
        $cur = $cur.$seg
    }
}

# ──────────────────────────────────────────────────────────────────────
#   BUCKET-assertion convention (no helper; just a documented pattern)
# ──────────────────────────────────────────────────────────────────────
# The original draft of this file shipped an `Assert-AllOf` helper for
# "collect all failures, throw once at the end" buckets. Reality after
# the round-1 migration: it had 0 callers — the closure-array form
# (`Assert-AllOf -Assertions @({...},{...},...)`) reads worse than the
# straightforward `$errs.Add(...)` collector pattern that the bucket
# tests already use. Removed to avoid unused public API rot.
#
# DOCUMENTED CONVENTION for new bucket tests (asserting multiple
# independent conditions, collecting all failures before throwing):
#
#   $errs = [System.Collections.Generic.List[string]]::new()
#   if (-not $cond1) { $errs.Add('condition 1 failed: <context>') }
#   if (-not $cond2) { $errs.Add('condition 2 failed: <context>') }
#   ...
#   Assert-Empty $errs.ToArray() -Because 'BUCKET (Name) assertions'
#
# When a single condition is checked, prefer the verb-first form:
#   Assert-NotNull $x -Because 'x must be present'
#   Assert-Equal $a $b -Because '...'
#
# When the failure message needs expensive context (e.g. a UIA probe),
# pass -Because as a scriptblock — it only runs on failure:
#   Assert-True $ok -Because {
#       $p = Get-UiaProperty 'PrimaryCommandButton' 'Name' -Hwnd $cpHwnd
#       "could not select result — Primary still '$p'"
#   }

