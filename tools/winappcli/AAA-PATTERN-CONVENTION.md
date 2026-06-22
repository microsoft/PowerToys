# AAA Test Pattern Convention

> **TL;DR**: Use comments as AAA markers inside ordinary `New-TestStep -Body { … }` blocks. Reserve the heavyweight `Invoke-AAATest` cmdlet (with separate `-Arrange` / `-Act` / `-Assert` / `-Cleanup` scriptblocks) ONLY for tests that genuinely need automatic cleanup-runs-always semantics or phase-tagged failure attribution.

---

## Motivation

We considered two ways to enforce the Arrange-Act-Assert-Cleanup pattern across module checklists:

### Option A — Lightweight: comments-as-AAA markers (CHOSEN)

```powershell
New-TestStep -Tag direct -Id 'CmdPal_Calculator_CopyOnEnter' `
    -Name "Calculator copies '7+5' result on Enter" -Body {
    # ── Arrange ──
    $orig = Get-ClipboardSafe
    $sentinel = "WHATEVER"
    Set-ClipboardSafe $sentinel

    # ── Act ──
    winapp ui set-value 'MainSearchBox' '7+5'
    winapp ui invoke 'PrimaryCommandButton'

    try {
        # ── Assert ──
        $clip = Get-ClipboardSafe
        if ($clip -ne '12') { throw "expected '12', got '$clip'" }
    }
    finally {
        # ── Cleanup ──
        Set-ClipboardSafe $orig
    }
}
```

Pros:
- ✅ **Variables flow naturally top-to-bottom** — no `param($ctx)` ceremony, no `$ctx.foo` rewrites
- ✅ **`try/catch/finally` works as PowerShell intends** — no wrapper to fight
- ✅ **Reads like the test you'd write** — comments document intent without enforcing structure
- ✅ **Existing tests already follow this de-facto** — most just lack the comment markers
- ✅ **Trivial migration** — add comment lines, no code changes
- ✅ **Works with `-Id` + `Set-AAAFilter`** — full filter UX without extra wrapper

Cons:
- ⚠ Comment markers are **convention not enforcement** — a sloppy contributor could mis-place them
- ⚠ Cleanup-always semantics depend on `try/finally` discipline (easy to forget in a new test)

### Option B — Heavyweight: `Invoke-AAATest` cmdlet with separate scriptblocks

```powershell
Invoke-AAATest -Tag direct -Id 'CmdPal_Calculator_CopyOnEnter' `
    -Name "Calculator copies '7+5' result on Enter" `
    -Arrange {
        $orig = Get-ClipboardSafe
        $sentinel = "WHATEVER"
        Set-ClipboardSafe $sentinel
        @{ orig = $orig; sentinel = $sentinel }   # return → becomes $ctx
    } `
    -Act { param($ctx)
        winapp ui set-value 'MainSearchBox' '7+5'
        winapp ui invoke 'PrimaryCommandButton'
    } `
    -Assert { param($ctx)
        $clip = Get-ClipboardSafe
        if ($clip -ne '12') { throw "expected '12', got '$clip'" }
    } `
    -Cleanup { param($ctx)
        Set-ClipboardSafe $ctx.orig
    }
```

Pros:
- ✅ **Cleanup ALWAYS runs** (wrapped in `finally`) — can't forget it
- ✅ **Phase-tagged failure messages** — "Arrange: …" vs "Act: …" vs "<bare>" for Assert
- ✅ Mechanically reviewable (each phase isolated)

Cons:
- ❌ **Each scriptblock is its own scope** — must thread state via `$Context` hashtable
- ❌ Every fixture variable becomes `$ctx.foo` (5 vars × 3 phases = 15 rewrites per test)
- ❌ Easy to forget `param($ctx)` line in subsequent blocks → silent `$null` references
- ❌ Easy to forget the implicit `@{...}` return in `-Arrange` → empty `$Context`
- ❌ Adds significant verbosity for tests with no real cleanup needs

---

## Decision

**Use Option A (comments-as-AAA-markers) for ALL module checklists** going forward.

**Keep Option B (`Invoke-AAATest`) available** for the rare tests that actually need:
- True cleanup-always semantics that can't be expressed in a one-line `finally`
- Phase-tagged failure attribution for triage (e.g. tests that set up flaky fixtures and want "Arrange: timeout" vs "Assert: wrong output" prefixes in the report)

CmdPal and Peek hand-written examples already use Option B for their fixture-heavy tests. They serve as references for when Option B is genuinely warranted.

---

## Conventions

### Test ID format (REQUIRED)

Every `New-TestStep` and `Invoke-AAATest` call gets `-Id '<Module>_<Feature>_<ExpectedBehavior>'`:

```powershell
New-TestStep -Tag direct -Id 'Hosts_AddEntry_ResolvesNewName' ...
New-TestStep -Tag direct -Id 'CmdPal_Calculator_ReturnsFourForTwoPlusTwo' ...
```

Why: stable handle for `-Only` / `-Skip` filtering, survives Name reword.

### Comment markers (CONVENTION)

Inside each `-Body { ... }`:

```powershell
New-TestStep ... -Body {
    # ── Arrange ──
    <fixture setup, snapshots, file backups>

    # ── Act ──
    <drive the UI / system under test>

    try {
        # ── Assert ──
        <verify outcome; throw on failure>
    }
    finally {
        # ── Cleanup ──
        <restore state, kill spawned processes>
    }
}
```

Place markers as PowerShell comments (`#`) at column 4. The `try/finally` is OPTIONAL — only add it when the test mutates external state that must be restored. For pure-read tests (settings.json schema check, process-presence assertion), skip the try/finally entirely:

```powershell
New-TestStep -Tag direct -Id 'Peek_Process_RunningWhenEnabled' ... -Body {
    # ── Act ──
    $proc = Get-Process PowerToys.Peek.UI -ErrorAction SilentlyContinue

    # ── Assert ──
    if (-not $proc) { throw "Peek not running" }
}
```

### Filter support (REQUIRED)

Every checklist's `param()` block must declare `-Only` and `-Skip`:

```powershell
[CmdletBinding()]
param(
    [string]$OutputDir = (Join-Path $env:TEMP 'winappcli-<module>-checklist'),
    [string[]]$Only = @(),
    [string[]]$Skip = @()
)
$ErrorActionPreference = 'Stop'
Import-Module ...\WinAppCli.PowerToys.psd1 -Force
Reset-TestSuite

# Filter normalisation (accept comma/semicolon-separated single string)
function _SplitFilter([string[]]$xs) {
    $out = New-Object System.Collections.Generic.List[string]
    foreach ($x in @($xs)) {
        if ([string]::IsNullOrWhiteSpace($x)) { continue }
        foreach ($piece in $x -split '[,;]') {
            $t = $piece.Trim().Trim("'`"")
            if ($t) { $out.Add($t) }
        }
    }
    @($out)
}
$Only = _SplitFilter $Only
$Skip = _SplitFilter $Skip
Set-AAAFilter -Only $Only -Skip $Skip
```

Usage:

```powershell
pwsh -File <module>-checklist.ps1                                  # all tests
pwsh -File <module>-checklist.ps1 -Only 'Hosts_*Add*'              # subset
pwsh -File <module>-checklist.ps1 -Skip 'Hosts_*Quit*'             # skip flake
pwsh -File <module>-checklist.ps1 -Only 'A,B,C' -Skip '*Slow*'     # CSV
```

### When to use `Invoke-AAATest` instead

Use the heavyweight `Invoke-AAATest` cmdlet when ALL of these are true:

1. The test has **fixtures that absolutely must be cleaned up** even if the assert throws (e.g. clipboard restore, settings.json restore, child-process kill)
2. The cleanup logic is **complex enough** that an inline `try/finally` is error-prone
3. You want **phase-tagged failure messages** in the report (rare — usually the Assert message alone is clear enough)

For the 99% case where you have a 1-line cleanup or no cleanup at all, **prefer `New-TestStep` with comment markers**.

---

## Examples by complexity

### Pure-read test — no Cleanup needed, no comments needed for trivial 2-liners

```powershell
New-TestStep -Tag direct -Id 'CmdPal_AppX_Installed' -Name "..." -Body {
    $appx = Get-AppxPackage -Name 'Microsoft.CommandPalette' -EA SilentlyContinue
    if (-not $appx) { throw "Microsoft.CommandPalette AppX not installed" }
}
```

### Multi-step test — comments help readability

```powershell
New-TestStep -Tag direct -Id 'Module_Feature_Behavior' -Name "..." -Body {
    # ── Arrange ──
    $fixture = Get-Fixture
    $snapshot = Snapshot-State

    # ── Act ──
    Drive-Ui $fixture

    # ── Assert ──
    $result = Read-Result
    if ($result -ne 'expected') { throw "got '$result'" }

    # No -Cleanup needed; nothing to undo
}
```

### State-mutating test — try/finally for cleanup discipline

```powershell
New-TestStep -Tag direct -Id 'Hosts_AddEntry_ResolvesNewName' -Name "..." -Body {
    # ── Arrange ──
    $orig = Backup-HostsFile
    Add-HostsEntry "1.2.3.4" "test.local"

    try {
        # ── Act + Assert ──
        $resolved = Resolve-DnsName "test.local"
        if ($resolved.IPAddress -ne "1.2.3.4") {
            throw "expected 1.2.3.4, got $($resolved.IPAddress)"
        }
    }
    finally {
        # ── Cleanup ──
        Restore-HostsFile $orig
    }
}
```

### Heavy-fixture test — `Invoke-AAATest` justified

```powershell
Invoke-AAATest -Tag direct -Id 'CmdPal_Calculator_CopiesResultOnEnter' -Name "..." `
    -Arrange {
        $orig     = Get-ClipboardSafe
        $sentinel = "WINAPPCLI_$(Get-Random)"
        Set-ClipboardSafe $sentinel
        @{ orig = $orig; sentinel = $sentinel; expected = '12' }
    } `
    -Act { param($ctx)
        Invoke-CmdPalQuery '7+5'
        # … invoke Copy …
    } `
    -Assert { param($ctx)
        $after = Get-ClipboardSafe
        if ($after -ne $ctx.expected) { throw "got '$after'" }
    } `
    -Cleanup { param($ctx)
        if ($ctx.orig) { Set-ClipboardSafe $ctx.orig }
    }
```

Justified because: clipboard MUST be restored even if Assert throws (we polluted it with a sentinel); explicit phase tags help triage (was it the seeding that failed, or the actual assert?); the test runs in CI so visible cleanup hygiene matters.

---

## Migration plan

**No mass-conversion script.** Apply this convention test-by-test as you touch a module:

1. When **adding** a new test → use the convention from the start
2. When **fixing** an existing test → add the comment markers + Id while you're in there
3. When **a module ships a new release** → optionally pass through and add markers + Ids to existing tests (good time, you're already reading them)

This keeps churn minimal and respects the working principle that **the tests already work** — we just want the new ones to be more readable.

---

## See also

- `tools/winappcli/modules/command-palette-checklist.ps1` — reference for `Invoke-AAATest` heavyweight pattern (settings-mutation tests with backup/restore)
- `tools/winappcli/modules/peek-checklist.ps1` — reference for `Invoke-AAATest` with process-spawn fixtures (each test has its own Cleanup that calls `Stop-Peek`)
- `tools/winappcli/WinAppCli.PowerToys/functions/02-TestHarness.ps1` — `New-TestStep` source (supports `-Id` parameter and consults `Set-AAAFilter`)
- `tools/winappcli/WinAppCli.PowerToys/functions/10-AAATest.ps1` — `Invoke-AAATest` + `Set-AAAFilter` source
