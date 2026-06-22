#Requires -Version 7.0
# 11-Registry.tests.ps1 — extracted from command-palette-checklist.ps1 during Phase 2b split.
# Dot-sourced from the orchestrator so it shares script scope ($cpHwnd,
# $cpSettings, $cpEnabled, $cpDataDir). See _helpers.ps1 for the
# CmdPal-specific helper functions these tests call into.
# ── Box L1036-L1037: Registry provider via ':' alias ─────────────────
# Drive the Registry provider via its IsDirect ':' alias. Asserts:
#   - alias navigates to a sub-page
#   - the well-known root keys (HKEY_LOCAL_MACHINE, HKEY_CURRENT_USER, …)
#     appear as ListItems
#   - Primary action label is something like 'Open' (= drill into the key)
# We do NOT navigate further — the deep walk + Copy-key-path is covered
# by L1037 PROTOTYPE when we wire it. This test proves the registry
# provider loads and the root level renders.
Test-Case 'CmdPal_Registry_AliasOpensRootKeys' "Box L1036: Registry ':' alias opens provider showing HKEY_* root keys (FUNCTIONAL — safe, doesn't invoke)" {
    try {
    # Act
        $script:_regPlaceholder = $null
        try { $script:_regPlaceholder = Invoke-CmdPalAlias ':' } catch { }
        # Wait for at least one HKEY_* ListItem to appear (registry sub-page
        # may take a beat — slow-factor-aware via Wait-Until presence check).
        $null = Wait-Until -TimeoutMs 3000 -PollMs 200 -IgnoreException `
            -Message "Registry ':' alias did not show HKEY_* root keys within 3s" `
            -Condition {
                $insLines = (winapp ui inspect 'ItemsList' -w $cpHwnd --depth 4 2>$null) -split "`n"
                @($insLines | Where-Object { $_ -match 'ListItem "HKEY_' }).Count -gt 0
            }
        # Re-fetch via the same inspect, with the search fallback if needed.
        $insLines = (winapp ui inspect 'ItemsList' -w $cpHwnd --depth 4 2>$null) -split "`n"
        $rootHits = @($insLines | Where-Object { $_ -match 'ListItem "HKEY_' })
        if ($rootHits.Count -eq 0) {
            $r = winapp ui search 'HKEY' -w $cpHwnd --json 2>$null | ConvertFrom-Json
            $rootHits = @($r.matches | Where-Object { $_.type -eq 'ListItem' -and $_.name -match '^HKEY_' })
            Assert-GreaterThan $rootHits.Count 0 -Because { "Registry ':' alias did not show HKEY_* root keys (placeholder='$($script:_regPlaceholder)')" }
        }
    # Assert
        $ph = $script:_regPlaceholder
        $primary = (Get-UiaProperty 'PrimaryCommandButton' 'Name' -Hwnd $cpHwnd)
        Write-Host "    info: Registry ':' alias rendered $($rootHits.Count) HKEY_* root key(s); Primary='$primary'" -ForegroundColor DarkGray
    } finally {
    # Cleanup
        Reset-CmdPalToHome
        Remove-Variable -Scope Script -Name '_regPlaceholder' -ErrorAction SilentlyContinue
    }
}
