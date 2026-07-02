#Requires -Version 7.0
# cmdpal-providers.ps1 — split from _helpers.ps1 (review item #5).
# Dot-sourced from _helpers.ps1; shares script scope with the orchestrator
# so it sees $cpHwnd / $cpSettings / $cpEnabled / $cpDataDir.

# ── Provider-state fixtures ──────────────────────────────────────────
# CmdPal providers (Calculator, Files, etc.) live in settings.json under
# ProviderSettings.<id>.IsEnabled. If a user has disabled a provider, the
# tests that rely on it would silently fail with confusing messages like
# "alias '=' did not navigate". A bucket-level fixture (xUnit
# [ClassInitialize] equivalent) ensures the provider is RESPONSIVE in
# the live AppX before the test bucket runs and restores any disk
# mutation on teardown.
#
# IMPORTANT: disk state (settings.json on disk) and live state (the
# running AppX's cached IsEnabled) can diverge — CmdPal AppX caches
# IsEnabled at startup, so writing settings.json without restarting the
# AppX leaves them out of sync. The fixture therefore probes the LIVE
# AppX (not disk) as the source of truth for "is this provider working
# right now?", and only mutates disk when the probe says the provider
# is unresponsive.
function Get-CmdPalProviderEnabled {
    param([Parameter(Mandatory)][string]$ProviderId)
    if (-not (Test-Path $cpSettings)) { return $null }
    $j = Get-CmdPalSettings
    if ($null -eq $j) { return $null }
    $p = $j.ProviderSettings.PSObject.Properties[$ProviderId]
    if (-not $p) { return $null }
    return [bool]$p.Value.IsEnabled
}

# Live probe: types a known query into the running CmdPal AppX and checks
# whether the provider responds with its expected result. This is the only
# reliable way to know if a provider is actually loaded — disk-state checks
# are fooled by the disk/AppX cache divergence described above.
#
# Returns $true if the probe sees the expected ListItem, $false otherwise.
# Always returns CmdPal to home + clears the search box on exit (the
# finally block runs BackButton before clearing, in case the probe used a
# direct-alias query like '$' that navigated to a sub-page).
function Test-CmdPalProviderLive {
    param([Parameter(Mandatory)][string]$ProviderId, [int]$TimeoutMs = 2500)
    if (-not $script:cpHwnd) { return $false }
    # Provider-specific probe: each entry is a deterministic
    # query→expected-ListItem-name pair that's cheap, side-effect-free,
    # and uniquely identifies the provider's contribution.
    #
    # Query selection strategy:
    #   - Providers with HOME-page contribution (calc fallback, Files
    #     indexer, AllApps, System fallback, TimeDate fallback): query a
    #     keyword that surfaces a known ListItem on home → cheaper, no
    #     sub-page navigation.
    #   - Providers that only respond on a SUB-PAGE (WindowsSettings in
    #     CmdPal 0.10.11181+): query the direct alias to enter the
    #     sub-page → expect a default sub-page entry. The finally block
    #     handles the BackButton to return to home.
    $probeMap = @{
        'com.microsoft.cmdpal.builtin.calculator'      = @{ Query='5+7';      Expect='12' }
        'com.microsoft.cmdpal.builtin.windowssettings' = @{ Query='$';        Expect='Open Settings app' }
        'com.microsoft.cmdpal.builtin.datetime'        = @{ Query='time';     Expect='Time and date' }
        'com.microsoft.cmdpal.builtin.system'          = @{ Query='shutdown'; Expect='Shutdown computer' }
        # NOTE: 'Files' provider intentionally not probed. Its home-page
        # contribution depends on the Windows Search indexer's state,
        # which is environmental — even when the provider is loaded and
        # IsEnabled=true, common queries like 'notepad' may return zero
        # file ListItems if the indexer hasn't picked up the file. The
        # probe can't distinguish "provider disabled" from "indexer
        # state bad", so wrapping the Files test in this fixture would
        # cause spurious AppX restarts without fixing the underlying
        # issue. The Files test stays unprotected for now.
    }
    $probe = $probeMap[$ProviderId]
    if (-not $probe) {
        Write-Host "    warn: Test-CmdPalProviderLive: no probe defined for '$ProviderId', assuming live" -ForegroundColor Yellow
        return $true
    }
    try {
        # Reset to known home state
        winapp ui invoke 'BackButton' -w $script:cpHwnd 2>$null | Out-Null
        Start-Sleep -Milliseconds 150
        winapp ui set-value 'MainSearchBox' '' -w $script:cpHwnd 2>$null | Out-Null
        Start-Sleep -Milliseconds 150
        # Type probe query and wait for expected result
        winapp ui set-value 'MainSearchBox' $probe.Query -w $script:cpHwnd 2>$null | Out-Null
        $hit = $null
        try { $hit = Wait-CmdPalListItem -ExpectedName $probe.Expect -TimeoutMs $TimeoutMs } catch { $hit = $null }
        return [bool]$hit
    } finally {
        # Always return CmdPal to a clean home state regardless of probe
        # outcome. For probes that triggered direct-alias navigation
        # (e.g. WindowsSettings '$'), BackButton is needed before clear.
        try { winapp ui invoke 'BackButton' -w $script:cpHwnd 2>$null | Out-Null } catch {}
        try { winapp ui set-value 'MainSearchBox' '' -w $script:cpHwnd 2>$null | Out-Null } catch {}
    }
}

function Use-CmdPalProviderEnabled {
    # Bucket-level scope guard. Ensures $ProviderId is RESPONSIVE in the
    # live AppX while $Body runs, then restores any disk changes.
    #
    # Three cases based on the live probe + disk state:
    #
    #   probe=true                — provider works; no action, no cleanup.
    #   probe=false + disk=true   — disk already says enabled but AppX has
    #                               stale cache; just restart AppX (no
    #                               disk mutation, no cleanup).
    #   probe=false + disk=false  — disk says disabled; enable on disk +
    #                               restart AppX. Cleanup: restore disk
    #                               to its original value + restart again.
    #
    # This means a normally-configured user (calc enabled) pays only the
    # probe cost (~1-2s) and zero AppX restarts.
    param(
        [Parameter(Mandatory)][string]$ProviderId,
        [Parameter(Mandatory)][scriptblock]$Body
    )
    # Print before the probe so the user sees the fixture is alive — the
    # probe + potential AppX restart can take 14+ seconds with NO other
    # output, which looks like a hang. Prefer "tell the user what's about
    # to happen" over silence.
    Write-Host "    [fixture] provider '$ProviderId' — probing live AppX (max 2500ms)..." -ForegroundColor DarkGray
    $live = Test-CmdPalProviderLive -ProviderId $ProviderId
    $orig = Get-CmdPalProviderEnabled -ProviderId $ProviderId
    $needCleanup = $false
    if ($live) {
        Write-Host "    [fixture] provider '$ProviderId' — live probe PASSED (no action needed)" -ForegroundColor DarkGray
    } else {
        if ($orig -eq $true) {
            Write-Host "    [fixture] provider '$ProviderId' — disk=enabled but live AppX not responsive; restarting AppX (no disk change, ~10-12s)..." -ForegroundColor DarkGray
            Restart-CmdPalAppX | Out-Null
            Write-Host "    [fixture] provider '$ProviderId' — AppX restart complete" -ForegroundColor DarkGray
        } else {
            Write-Host "    [fixture] provider '$ProviderId' — disk was IsEnabled=$orig and live not responsive; enabling on disk + restart (~10-12s)..." -ForegroundColor DarkGray
            Edit-CmdPalSettingsAndRestart -Mutator {
                param($j)
                $p = $j.ProviderSettings.PSObject.Properties[$ProviderId]
                if (-not $p) {
                    $j.ProviderSettings | Add-Member -NotePropertyName $ProviderId -NotePropertyValue ([pscustomobject]@{
                        IsEnabled = $true
                        PinnedCommandIds = @()
                    })
                } else {
                    $p.Value.IsEnabled = $true
                }
            } | Out-Null
            Write-Host "    [fixture] provider '$ProviderId' — disk write + AppX restart complete (will restore on cleanup)" -ForegroundColor DarkGray
            $needCleanup = $true
        }
    }
    try {
        & $Body
    } finally {
        if ($needCleanup) {
            Write-Host "    [fixture cleanup] restoring provider '$ProviderId' to disk IsEnabled=$orig (will restart AppX, ~10-12s)..." -ForegroundColor DarkGray
            try {
                Edit-CmdPalSettingsAndRestart -Mutator {
                    param($j)
                    $p = $j.ProviderSettings.PSObject.Properties[$ProviderId]
                    if ($p) {
                        if ($null -eq $orig) {
                            $j.ProviderSettings.PSObject.Properties.Remove($ProviderId)
                        } else {
                            $p.Value.IsEnabled = [bool]$orig
                        }
                    }
                } | Out-Null
                Write-Host "    [fixture cleanup] restore complete" -ForegroundColor DarkGray
            } catch {
                Write-Host "    [fixture cleanup] FAILED to restore provider '$ProviderId' to IsEnabled=$($orig): $_" -ForegroundColor Yellow
            }
        }
    }
}

# Returns $true if at least one of $Ids would actually execute under the
# current Set-AAAFilter -Only/-Skip configuration. Used by bucket fixtures
# to avoid mutating user state (e.g. enabling a disabled provider) on
# filtered runs where the bucket's tests won't actually run.
function Test-AnyTestWillRun {
    param([Parameter(Mandatory)][string[]]$Ids)
    $f = Get-AAAFilter
    $only = @($f.Only); $skip = @($f.Skip)
    foreach ($id in $Ids) {
        $okOnly = ($only.Count -eq 0)
        foreach ($p in $only) { if ($id -like $p) { $okOnly = $true; break } }
        if (-not $okOnly) { continue }
        $isSkipped = $false
        foreach ($p in $skip) { if ($id -like $p) { $isSkipped = $true; break } }
        if (-not $isSkipped) { return $true }
    }
    return $false
}

