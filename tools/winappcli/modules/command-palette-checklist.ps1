#Requires -Version 7.0
# command-palette-checklist.ps1  (Phase-4: translated from upstream tests-checklist-template.md L1012-1051
#                                  + updated for 0.99.0/0.99.1 architectural changes)
#
# IMPORTANT — CmdPal in 0.99.x is RADICALLY DIFFERENT from when the checklist was written:
#
#   1. It is now a PACKAGED MSIX AppX: Microsoft.CommandPalette_8wekyb3d8bbwe
#      (NOT a plain Win32 PT exe like PowerToys.Run). Lives in
#      %PROGRAMFILES%\WindowsApps\Microsoft.CommandPalette_<ver>_x64__8wekyb3d8bbwe
#      and its user data lives in %LOCALAPPDATA%\Packages\Microsoft.CommandPalette_8wekyb3d8bbwe\
#
#   2. Plugins are now called "providers" / "extensions" with stable IDs like
#      com.microsoft.cmdpal.builtin.calculator. Each has IsEnabled in
#      ProviderSettings + PinnedCommandIds for the new Dock.
#
#   3. NEW in 0.99.0: Dock (compact/always-on-top/pinning), persistent
#      Calculator history, plain-text + image viewer content types, command
#      pinning dialog with title/subtitle, extension-load hardening.
#
#   4. NEW in 0.99.1: DockSettings null-deserialization crash fix +
#      dock-label persistence fix.
#
# This script asserts the SCHEMA + STATE of each provider (much stronger than
# the legacy "manually try each plugin in the UI" approach), plus verifies
# all 0.99.x new features have their settings on disk.

[CmdletBinding()]
param(
    [string]$OutputDir = (Join-Path $env:TEMP 'winappcli-command-palette-checklist'),
    [int]$DemoPauseMs = 0,

    # Run only tests whose Id or Name matches ANY of these wildcard patterns.
    # Empty = run everything. Three calling conventions are accepted:
    #
    #   1. PowerShell native array (works inside a pwsh session via &):
    #      & ./command-palette-checklist.ps1 -Only @('CmdPal_Calculator_*','CmdPal_Dock*')
    #
    #   2. pwsh -Command (CLI, forwards arrays correctly):
    #      pwsh -Command "& ./command-palette-checklist.ps1 -Only 'CmdPal_Calculator_*','CmdPal_Dock*'"
    #
    #   3. pwsh -File with comma-separated string (CLI, simplest):
    #      pwsh -File ./command-palette-checklist.ps1 -Only 'CmdPal_Calculator_*,CmdPal_Dock*'
    #      (We split on commas + semicolons below to accept this form.)
    #
    [string[]]$Only = @(),

    # Skip tests whose Id or Name matches ANY of these wildcard patterns.
    # Same calling conventions as -Only. Useful for "everything except the flaky one":
    #   -Skip '*Stability*'
    #   -Skip '*Stability*,*Walker*'
    [string[]]$Skip = @(),

    # Tag-based filter: each tag expands to a set of -Only wildcard patterns
    # via $_tagMap (defined below). Useful for running test classes in CI/nightly
    # without remembering individual test IDs.
    #
    # Available tags (run `-Tag list` to print the map):
    #   schema        — pure file/JSON reads, no UI driving (~1s total)
    #   functional    — provider e2e tests that drive CmdPal UI (~3min)
    #   mutation      — edit settings.json + restart AppX + verify (~80s)
    #   stability     — regression guards (rapid typing, separator nav)
    #   integration   — PowerToys-CmdPal integration tests
    #   pin           — Dock pin tests
    #   bootstrap     — install/settings-page/runtime verification
    #
    # Composite shortcuts:
    #   ci            — schema + bootstrap (CI gate, ~5s)
    #   nightly       — everything except destructive (full coverage)
    #
    # Examples:
    #   -Tag schema                       # 5 tests, ~2s — CI gate
    #   -Tag functional                   # 19 tests, ~3min — nightly
    #   -Tag 'schema,mutation'            # combined (additive)
    #   -Tag schema -Only 'CmdPal_Dock*'  # AND-combination: schema tests matching Dock
    #
    # -Tag adds to -Only (additive). -Skip still applies.
    [string[]]$Tag = @()
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot '..\WinAppCli.PowerToys\WinAppCli.PowerToys.psd1') -Force
if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir | Out-Null }
Reset-TestSuite

# Normalise: each element may itself contain comma/semicolon-separated patterns
# (so `pwsh -File ... -Only 'A,B,C'` works the same as `-Only 'A','B','C'`).
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
$Only = @(_SplitFilter $Only)
$Skip = @(_SplitFilter $Skip)
$Tag  = @(_SplitFilter $Tag)

# Tag → -Only-pattern expansion map. Add new tags here as new providers are
# added. Keep patterns conservative (use specific test-ID prefixes, not bare
# wildcards) to avoid accidental matches.
$_tagMap = @{
    'schema' = @(
        'CmdPal_Installed_*',
        'CmdPal_SettingsSchema_*',
        'CmdPal_DockSchema_FeaturePresence*',
        'CmdPal_Runtime_*',
        'CmdPal_State_*',
        'CmdPal_Providers_NewBuiltin*',
        'CmdPal_ProviderIds_*'
    )
    'functional' = @(
        'CmdPal_Calculator_*',
        'CmdPal_Files_*',
        'CmdPal_AllApps_*',
        'CmdPal_TimeDate_*',
        'CmdPal_WebSearch_*',
        'CmdPal_System_*',
        'CmdPal_Shell_*',
        'CmdPal_Registry_*',
        'CmdPal_WindowsSettings_*',
        'CmdPal_WindowWalker_*'
    )
    'mutation' = @(
        'CmdPal_Settings_HotkeyChangePickedUp',
        'CmdPal_Providers_DisableExtensionRemovesCommands',
        'CmdPal_DockSchema_NullDockSettings*',
        'CmdPal_DockSchema_ShowLabels*',
        'CmdPal_TerminalProfiles_BadGuid*',
        'CmdPal_SettingsUI_*'
    )
    'stability' = @(
        'CmdPal_Stability_*',
        'CmdPal_Navigation_*'
    )
    'integration' = @(
        'CmdPal_PowerToysExtension_*',
        'CmdPal_SettingsUI_Dock_EnableDockShowsPowerDockWindow',
        'CmdPal_SettingsUI_Dock_CompactModeShrinksPowerDockHeight',
        'CmdPal_SettingsUI_Dock_PositionTopBottomRelocatesPowerDock',
        'CmdPal_SettingsUI_Dock_PositionLeftMakesPowerDockVertical',
        'CmdPal_SettingsUI_Dock_DefaultBandsPresentOnFirstEnable',
        'CmdPal_SettingsUI_Dock_PerformanceMonitorBandShowsLiveData',
        'CmdPal_SettingsUI_Dock_DateTimeBandShowsCurrentTime'
    )
    'pin' = @(
        'CmdPal_Pin_*'
    )
    'bootstrap' = @(
        'CmdPal_Settings_PageReachable'
    )
}
# Composite shortcuts: tags that reference other tags via '@' prefix.
$_tagComposites = @{
    'ci'      = @('schema', 'bootstrap')
    'nightly' = @('schema', 'bootstrap', 'functional', 'mutation', 'stability', 'integration', 'pin')
}

function _ExpandTags([string[]]$tags) {
    if ($tags.Count -eq 0) { return @() }
    $patterns = New-Object System.Collections.Generic.List[string]
    $seen = New-Object System.Collections.Generic.HashSet[string]
    $queue = New-Object System.Collections.Generic.Queue[string]
    foreach ($t in $tags) { $queue.Enqueue($t.ToLowerInvariant()) }
    while ($queue.Count -gt 0) {
        $t = $queue.Dequeue()
        if (-not $seen.Add($t)) { continue }
        if ($_tagComposites.ContainsKey($t)) {
            foreach ($sub in $_tagComposites[$t]) { $queue.Enqueue($sub) }
            continue
        }
        if ($_tagMap.ContainsKey($t)) {
            $_tagMap[$t] | ForEach-Object { [void]$patterns.Add($_) }
            continue
        }
        # Unknown tag — informative but not fatal (caller may have a typo).
        Write-Warning "Unknown -Tag '$t'. Known tags: $(($_tagMap.Keys + $_tagComposites.Keys | Sort-Object) -join ', ')."
    }
    @($patterns | Select-Object -Unique)
}

# Special: -Tag list prints the map and exits.
if ($Tag -contains 'list') {
    "Available tags (use -Tag <name>[,<name>...]):"
    ""
    foreach ($k in ($_tagMap.Keys | Sort-Object)) {
        "  {0,-12} ({1} patterns)" -f $k, $_tagMap[$k].Count
        $_tagMap[$k] | ForEach-Object { "    $_" }
        ""
    }
    "Composite shortcuts:"
    foreach ($k in ($_tagComposites.Keys | Sort-Object)) {
        "  {0,-12} = {1}" -f $k, ($_tagComposites[$k] -join ' + ')
    }
    exit 0
}

# Merge -Tag-expanded patterns into -Only (additive). If both -Tag and -Only
# are provided, the resulting -Only is the UNION (matches any test that
# matches any -Only or any -Tag-expanded pattern).
if ($Tag.Count -gt 0) {
    $tagPatterns = _ExpandTags $Tag
    $Only = @($Only + $tagPatterns | Select-Object -Unique)
    Write-Host "-Tag $($Tag -join ',') expanded to $($tagPatterns.Count) -Only patterns" -ForegroundColor Cyan
}

Set-AAAFilter -Only $Only -Skip $Skip
if ($Only.Count -gt 0 -or $Skip.Count -gt 0) {
    Write-Host "filter: only=[$($Only -join ', ')] skip=[$($Skip -join ', ')]" -ForegroundColor Cyan
}

# ── Settings page surface ─────────────────────────────────────────────
$settings = Open-PtSettings
Switch-PtSettingsPage -Module 'CmdPal' -Hwnd $settings.hwnd
Start-Sleep -Milliseconds 500

# CmdPal user-data lives in the AppX-sandbox path, not PT settings
$cpDataDir   = "$env:LOCALAPPDATA\Packages\Microsoft.CommandPalette_8wekyb3d8bbwe"
$cpSettings  = "$cpDataDir\LocalState\settings.json"
$cpEnabled   = Test-PtModuleEnabled -Module 'CmdPal'

# ── File-level test-filter optimisation ─────────────────────────────
# With the Phase 2b split, each test file is dot-sourced unconditionally
# and Test-Case checks the -Only/-Skip filter per call. When the user
# runs ``-Only 'CmdPal_Calculator*'`` that means 56 of 57 tests register
# as "filtered" SKIP entries — correct behaviour, but noisy output.
#
# This helper pre-scans a file for Test-Case / Invoke-AAATest IDs and
# only dot-sources it if at least one ID matches the active filter.
# Tests filtered IN still appear; tests in skipped files don't appear
# at all (cleaner output). Reuses Test-AnyTestWillRun from _helpers.ps1
# once that's dot-sourced; before then (e.g. for 01-Bootstrap which
# precedes _helpers), uses an inline check.
#
# Cache the test-ID scan so repeated -Only invocations don't re-grep.
$script:_fileIdCache = @{}
function Import-CmdPalTests {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)
    if (-not (Test-Path $Path)) { throw "Import-CmdPalTests: $Path not found" }
    # Scan once per file (cached).
    if (-not $script:_fileIdCache.ContainsKey($Path)) {
        $ids = New-Object System.Collections.Generic.List[string]
        foreach ($line in [System.IO.File]::ReadAllLines($Path)) {
            if ($line -match "^\s*Test-Case\s+'([A-Za-z0-9_]+)'") { $ids.Add($matches[1]) }
            elseif ($line -match "^\s*Invoke-AAATest.*Id\s+'([A-Za-z0-9_]+)'") { $ids.Add($matches[1]) }
        }
        $script:_fileIdCache[$Path] = $ids.ToArray()
    }
    $fileIds = $script:_fileIdCache[$Path]
    # If no filter active or file has no Test-Case calls (e.g. _helpers), always load.
    if ($fileIds.Count -eq 0 -or ($Only.Count -eq 0 -and $Skip.Count -eq 0)) {
        . $Path
        return
    }
    # Determine if ANY test in this file would actually run under current filter.
    $anyMatches = $false
    foreach ($id in $fileIds) {
        $okOnly = ($Only.Count -eq 0)
        foreach ($p in $Only) { if ($id -like $p) { $okOnly = $true; break } }
        if (-not $okOnly) { continue }
        $isSkipped = $false
        foreach ($p in $Skip) { if ($id -like $p) { $isSkipped = $true; break } }
        if (-not $isSkipped) { $anyMatches = $true; break }
    }
    if ($anyMatches) {
        . $Path
    }
    # else: silently skip — no SKIP rows emitted for tests in this file.
    # This trades reporting completeness (no "filtered" rows for whole-file
    # skips) for cleaner output when running a narrow filter. The full-run
    # case (no filter) always loads everything.
}

# Dot-source the assertion library BEFORE 01-Bootstrap registration so the
# Test-Case bodies in that file can use Assert-*. (The full _helpers.ps1
# dot-source happens later, after $cpHwnd is acquired, because it depends
# on script-scope $cpHwnd / $cpSettings being already set.)
. (Join-Path $PSScriptRoot 'cmdpal\helpers\Assertions.ps1')

# Also dot-source cmdpal-settings.ps1 early so 01-Bootstrap (and the
# read-only schema buckets) can use Get-CmdPalSettings + Backup-/Restore-
# CmdPalSettingsJson. These functions only USE $cpSettings / $cpHwnd
# when invoked; defining them up-front is safe. (Stale "Assert-AllOf"
# wording in the previous note removed — the helper was deleted.)
. (Join-Path $PSScriptRoot 'cmdpal\helpers\cmdpal-settings.ps1')

Import-CmdPalTests (Join-Path $PSScriptRoot 'cmdpal\01-Bootstrap.tests.ps1')

# ── Functional verification — drive CmdPal UI directly ────────────────
# This section was missing from earlier batches. CmdPal's UIA tree is
# available even when the window is hidden (the AppX hosts a foreground-aware
# UIA provider; the tree is queryable in either state).
#
# Pattern:
#   1. Signal CmdPal.Show event (re-summon if auto-dismissed)
#   2. winapp ui set-value 'MainSearchBox' <query>
#   3. winapp ui search <expected-result-text> -- assert matchCount > 0
#
# These are TRUE FUNCTIONAL tests — the calculator must compute correctly,
# the file search must return files, etc. — not just schema.
#
# AutoGoHomeInterval can be aggressive (-00:00:00.0010000 = 1ms by default
# in some builds) so we re-signal Show before each query.

# Acquire the CmdPal window. When CmdPal is enabled + AppX is installed we
# proactively summon it (signal CmdPal.Show + poll for the visible window) —
# the prior one-shot `list-windows` read returned null whenever CmdPal had
# auto-dismissed on focus loss, which silently auto-skipped 50+ functional
# tests and masked real failures. If the AppX is genuinely uninstalled or
# CmdPal is disabled in PT, $cpHwnd stays null and the legacy SKIP stubs at
# the bottom of the script take over (preserves the "CmdPal not present"
# behaviour for environments where the suite is run without CmdPal).
$cpHwnd = $null
$_cpAppxInstalled = [bool](Get-AppxPackage -Name 'Microsoft.CommandPalette' -ErrorAction SilentlyContinue)
$_cpShouldBeUp    = ($_cpAppxInstalled -and $cpEnabled -and (Test-PtSharedEvent -Name 'CmdPal.Show'))
$cpHwnd = 0
if ($_cpShouldBeUp) {
    $_showDeadline = (Get-Date).AddSeconds(15)
    do {
        try { Invoke-PtSharedEvent -Name 'CmdPal.Show' | Out-Null } catch {}
        Start-Sleep -Milliseconds 400
        # Bind to var first so StrictMode doesn't error on .hwnd-of-$null
        $_win = winapp ui list-windows -a 'CmdPal' --json 2>$null | ConvertFrom-Json |
                Where-Object { $_.title -eq 'Command Palette' } | Select-Object -First 1
        $cpHwnd = if ($_win) { [int64]$_win.hwnd } else { 0 }
        if ($cpHwnd) { break }
    } while ((Get-Date) -lt $_showDeadline)
    if (-not $cpHwnd) {
        # Diagnostics-rich failure beats silent auto-skip. The user explicitly
        # has CmdPal enabled, the helper is publishing CmdPal.Show, but no
        # window appeared — that's a real bug worth surfacing.
        $_ui = Get-Process Microsoft.CmdPal.UI -EA SilentlyContinue
        $_hp = Get-Process Microsoft.CmdPal.Ext.PowerToys -EA SilentlyContinue
        $_wins = (winapp ui list-windows -a 'CmdPal' --json 2>$null) | ConvertFrom-Json
        $_winSummary = if ($_wins) { ($_wins | ForEach-Object { "hwnd=$($_.hwnd) title='$($_.title)'" }) -join ' | ' } else { '(none)' }
        throw "CmdPal.Show signaled but no 'Command Palette' window found after 15s. UI=$(if($_ui){"PID $($_ui.Id)"}else{'NOT RUNNING'}) Helper=$(if($_hp){"PID $($_hp.Id)"}else{'NOT RUNNING'}) windows=[$_winSummary]"
    }
} else {
    # Best-effort one-shot read for environments where CmdPal exists but the
    # checklist is being run without the helper / module flag (rare).
    $_win = winapp ui list-windows -a 'CmdPal' --json 2>$null | ConvertFrom-Json |
            Where-Object { $_.title -eq 'Command Palette' } | Select-Object -First 1
    $cpHwnd = if ($_win) { [int64]$_win.hwnd } else { 0 }
}

if ($cpHwnd) {
    # Snapshot suite-start time so end-of-suite cleanup can identify processes
    # spawned by our tests (vs ones the user already had open).
    $suiteStartTime = Get-Date

    # ── Module-specific wrappers around generic helpers ─────────────────
    # Reset-AppToHome (generic) + CmdPal.Show event = CmdPal-flavoured reset.
    # ── Helpers ─────────────────────────────────────────────────────────
    # The 17 CmdPal-specific helper functions live in a sibling file for
    # readability (this checklist was nearly 3000 lines). Dot-source so
    # they share script scope — they reference $cpHwnd / $cpSettings /
    # $cpEnabled / $cpDataDir set above, and tests below keep using those
    # names directly.
    . (Join-Path $PSScriptRoot 'cmdpal\_helpers.ps1')

    # Bring CmdPal back to its home page before the first test runs.
    # (This used to be a top-level call inside _helpers.ps1 but was
    # extracted so dot-sourcing has no side effects.)
    Reset-CmdPalToHome


    # ════════════════════════════════════════════════════════════════════
    #   FUNCTIONAL TESTS — Arrange / Act / Assert / Cleanup pattern
    # ════════════════════════════════════════════════════════════════════
    # All tests below follow the 4A pattern via Invoke-AAATest:
    #   - Arrange: optional hashtable seeding $Context (clipboard snapshot,
    #              spawn fixtures, etc.)
    #   - Act:     drive CmdPal (typing, invoking, navigating)
    #   - Assert:  verify result; throw on failure
    #   - Cleanup: ALWAYS runs (in finally); restores clipboard, kills
    #              spawned processes, returns CmdPal to home for next test

    Import-CmdPalTests (Join-Path $PSScriptRoot 'cmdpal\02-Calculator.tests.ps1')

    # ── Box L1025-L1027: Files provider — single test, FULL coverage ──
    Import-CmdPalTests (Join-Path $PSScriptRoot 'cmdpal\03-Files.tests.ps1')

    # ── Box L1028 ★ FULL: Time/Date provider copies a value to clipboard ──
    Import-CmdPalTests (Join-Path $PSScriptRoot 'cmdpal\04-TimeDate-Home.tests.ps1')
    Import-CmdPalTests (Join-Path $PSScriptRoot 'cmdpal\05-WebSearch.tests.ps1')

    # ── Box L1040: System command returns a real system action ─────
    Import-CmdPalTests (Join-Path $PSScriptRoot 'cmdpal\06-System.tests.ps1')

    Import-CmdPalTests (Join-Path $PSScriptRoot 'cmdpal\07-AllApps.tests.ps1')

    Import-CmdPalTests (Join-Path $PSScriptRoot 'cmdpal\08-WindowsSettings.tests.ps1')

    Import-CmdPalTests (Join-Path $PSScriptRoot 'cmdpal\09-WindowWalker.tests.ps1')

    Import-CmdPalTests (Join-Path $PSScriptRoot 'cmdpal\10-Shell.tests.ps1')

    Import-CmdPalTests (Join-Path $PSScriptRoot 'cmdpal\11-Registry.tests.ps1')

    Import-CmdPalTests (Join-Path $PSScriptRoot 'cmdpal\12-TerminalProfiles-Stub.tests.ps1')
    # the AppX process is still alive at the end.
    Import-CmdPalTests (Join-Path $PSScriptRoot 'cmdpal\13-Stability-RapidTyping.tests.ps1')

    Import-CmdPalTests (Join-Path $PSScriptRoot 'cmdpal\14-TimeDate-Alias.tests.ps1')

    Import-CmdPalTests (Join-Path $PSScriptRoot 'cmdpal\15-Mutation-Settings.tests.ps1')

    Import-CmdPalTests (Join-Path $PSScriptRoot 'cmdpal\16-Mutation-Dock.tests.ps1')

    Import-CmdPalTests (Join-Path $PSScriptRoot 'cmdpal\17-Schemas-Extended.tests.ps1')

    Import-CmdPalTests (Join-Path $PSScriptRoot 'cmdpal\18-Stability-Typing.tests.ps1')

    Import-CmdPalTests (Join-Path $PSScriptRoot 'cmdpal\19-PT-Integration.tests.ps1')

    Import-CmdPalTests (Join-Path $PSScriptRoot 'cmdpal\20-TerminalProfiles-BadGuid.tests.ps1')

    Import-CmdPalTests (Join-Path $PSScriptRoot 'cmdpal\21-Pin.tests.ps1')

    Import-CmdPalTests (Join-Path $PSScriptRoot 'cmdpal\22-Navigation.tests.ps1')

    # ── PR #48033 ★ FULL: stable provider AutomationIds (regression guard) ──
    Import-CmdPalTests (Join-Path $PSScriptRoot 'cmdpal\23-ProviderIDs.tests.ps1')

    # ── Settings-UI binding tests — toggle controls in the AppX Settings
    # window and verify settings.json updates. Catches broken click handlers,
    # wrong key bindings, type mismatches.
    Import-CmdPalTests (Join-Path $PSScriptRoot 'cmdpal\24-SettingsUI.tests.ps1')

    # ── Suite-end cleanup: clear search box, reset to home, reap stray notepads ─
    # Belt-and-suspenders. Per-test Cleanup blocks already kill spawned
    # processes, but if a test crashed before its Arrange returned the PID
    # we'd leak. Sweep anything spawned after suite start.
    winapp ui set-value 'MainSearchBox' '' -w $cpHwnd 2>$null | Out-Null
    Reset-CmdPalToHome
    Get-ProcessesStartedAfter -Since $suiteStartTime -Name 'notepad' |
        Stop-ProcessesSafely -Reason 'suite-end'

    # ── Suite-end UI dismiss: hide CmdPal so it doesn't linger on the user's screen ─
    # CmdPal is a toggleable HUD. To dismiss it cleanly we use the Win32
    # ShowWindow(SW_HIDE) primitive via Hide-Window. This is the test-suite
    # equivalent of [ClassCleanup]/[AssemblyCleanup] in MSTest or @AfterAll
    # in JUnit — runs once after ALL tests, regardless of pass/fail.
    #
    # SW_HIDE is the RIGHT choice because:
    #   - It hides the window without killing the AppX process (so the user
    #     can resummon CmdPal with the hotkey afterward — state is preserved)
    #   - It matches CmdPal's own auto-hide behaviour on focus loss
    #   - It doesn't require BackButton click chains (which sometimes leave
    #     CmdPal stuck on a sub-page)
    #   - Compare to PostMessage(WM_CLOSE) which would terminate CmdPal.UI
    #     entirely — too heavy for a between-test-runs cleanup.
    try {
        if (Test-WindowVisible -Hwnd $cpHwnd) {
            $hidden = Hide-Window -Hwnd $cpHwnd
            Start-Sleep -Milliseconds 200
            if (Test-WindowVisible -Hwnd $cpHwnd) {
                Write-Host "    info: CmdPal Hide-Window returned $hidden but window still visible (hwnd=$cpHwnd)" -ForegroundColor DarkGray
            } else {
                Write-Host "    info: CmdPal window dismissed at suite-end (hwnd=$cpHwnd, AppX kept alive)" -ForegroundColor DarkGray
            }
        }
    } catch {
        Write-Host "    info: suite-end CmdPal dismiss failed: $($_.Exception.Message)" -ForegroundColor DarkGray
    }

} else {
    foreach ($pair in @(
        @('Box L1024: Calculator FUNCTIONAL test (CmdPal window not visible to UIA)',
          'CmdPal window not found via list-windows. Was CmdPal launched? Try Invoke-PtSharedEvent CmdPal.Show in advance.'),
        @('Box L1024 extended: Calculator non-trivial math (no UI handle)',           'Same as above'),
        @('Box L1025-L1027: File search FUNCTIONAL (no UI handle)',                   'Same as above'),
        @('Box L1032: Web search alias FUNCTIONAL (no UI handle)',                     'Same as above'),
        @('Box L1040: System command lock FUNCTIONAL (no UI handle)',                  'Same as above')
    )) {
        New-TestStep -Tag skipped -Name $pair[0] -SkipReason $pair[1]
    }
}

# ── Genuinely-skipped tests, organised by category ───────────────────────
#
# Each entry has a stable Id matching the same naming convention as the
# active tests: <Module>_<Feature>_<ExpectedBehavior>. The SkipReason is
# tagged with one of four categories explaining WHY it isn't running:
#
#   YELLOW (PROTOTYPE)   — implementable but needs ~30min of probing first
#   ORANGE (NEEDS-ENV)   — needs special environment (admin, network,
#                          elevation, runner restart, fixture extension)
#   RED-DESTRUCTIVE      — would actually destroy data / shut down system
#                          / install a real package. Permanent skip; opt-in
#                          flag only. Marked with CATEGORY:DESTRUCTIVE.
#   RED-COVERED          — already covered by an active test under a
#                          different Id (avoid duplicate work)

. (Join-Path $PSScriptRoot 'cmdpal\90-SkippedRegistry.ps1')
foreach ($entry in $skipped) {
    New-TestStep -Tag skipped -Id $entry[0] -Name $entry[1] -SkipReason $entry[2]
}

Save-TestSuiteReport -Path (Join-Path $OutputDir 'command-palette-checklist-results.json')
$report = Get-TestSuiteReport
exit ($report.failCount -gt 0 ? 1 : 0)
