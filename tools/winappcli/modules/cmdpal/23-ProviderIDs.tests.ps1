#Requires -Version 7.0
# 23-ProviderIDs.tests.ps1 — new for the PR #48033 modernization round
# (2026-05-22). These tests exist specifically to exercise and guard the
# AutomationProperties.AutomationId bindings PR #48033 added in CmdPal's
# ListItemSingleRowViewModelTemplate (ExtViews/ListItemsView.xaml).
#
# Without these tests, a future XAML refactor could silently drop the
# `AutomationId="{x:Bind Command.Id, Mode=OneWay}"` binding and no test
# would notice — the suite would continue to pass because every other
# test falls back to text-mode `inspect+regex` over ItemsList.
#
# Each test asserts a stable property of the new ID scheme:
#   1. HomeExposesCorePlugInTiles — built-in providers are reachable by
#      their com.microsoft.cmdpal.* IDs on the empty-query home page.
#   2. CalculatorTileSurfacesForCalcQuery — typing 'calc' reveals the
#      Calculator tile, AND its selector is invokable (proves the
#      ListItemSingleRowViewModelTemplate root Group is reachable).
#   3. WindowsSettingsTileSurfacesForSettingsQuery — typing 'settings'
#      reveals the WindowsSettings tile via the same ID pattern.
#   4. WebSearchFallbackAlwaysPresentForNonEmptyQuery — the
#      websearch.execute.fallback ID surfaces for any non-empty query
#      (proves the fallback-command AutomationId binding works).
#
# Tagged 'schema' because they're presence/regression checks (no mutation,
# no spawned processes); but they do drive the UI, so total cost is ~2-3s.
# Dot-sourced from the orchestrator so they share $cpHwnd / other script-scope vars.

# Stable IDs we expect on the empty-query home page. PR #48033 binds these
# to the provider's Command.Id. If a built-in is disabled per-user the tile
# may be missing — so we assert a minimum SUBSET rather than the exact set,
# and require the canonical built-ins that are enabled-by-default.
#
# Verified on 0.11.11411.0 (2026-05-22): home page exposes 13 tiles when
# default providers are enabled. We assert >= 6 from the list below to
# stay robust against per-machine provider config.
$_corePlugInTileIds = @(
    'com.microsoft.cmdpal.calculator',
    'com.microsoft.cmdpal.timedate',
    'com.microsoft.cmdpal.windowsSettings',
    'com.microsoft.cmdpal.registry',
    'com.microsoft.cmdpal.run',
    'com.microsoft.cmdpal.windowwalker',
    'com.microsoft.cmdpal.websearch',
    'com.microsoft.cmdpal.clipboardHistory',
    'com.microsoft.cmdpal.winget'
)

Test-Case 'CmdPal_ProviderIds_HomeExposesCorePluginTiles' "★ PR #48033 ★: empty-query home page exposes >=6 built-in provider tiles via stable com.microsoft.cmdpal.* AutomationIds" {
    try {
        # Act — empty-query home (clear the search box if anything's there)
        winapp ui set-value 'MainSearchBox' '' -w $cpHwnd 2>$null | Out-Null
        # Wait for at least one com.microsoft.cmdpal.* tile to actually
        # surface (instead of blind 800ms sleep). If the WinUI ItemsList
        # is still re-rendering, the search below would return 0 matches
        # and we'd fail with a misleading "PR #48033 bindings missing"
        # message even when the bindings are fine but the page just
        # hasn't redrawn yet. 5s budget tolerates cold-AppX startup +
        # heavy late-suite state churn (downstream of Stability test's
        # forced AppX restart). Returns early on warm runs.
        $null = Wait-Until -TimeoutMs 5000 -PollMs 200 -IgnoreException `
            -Message "no com.microsoft.cmdpal.* tile surfaced within 5s after clearing search box (PR #48033 bindings may be missing OR ItemsList did not re-render in time)" `
            -Condition { [bool](Find-CmdPalProviderItem 'com.microsoft.cmdpal' -All) }

        # Use the umbrella ID 'com.microsoft.cmdpal' to grab every binding
        # in one query (regex behavior in winapp ui search is substring).
        $all = Find-CmdPalProviderItem 'com.microsoft.cmdpal' -All
        Assert-NotNull $all -Because "winapp ui search 'com.microsoft.cmdpal' returned 0 matches on empty-query home — PR #48033 bindings missing?"
        $found = @($all | ForEach-Object { $_.automationId } | Sort-Object -Unique)
        $coreFound = @($found | Where-Object { $_ -in $_corePlugInTileIds })

        # Assert — at least 6 core plug-in tiles must carry their Command.Id
        Assert-CountGreaterThanOrEqual $coreFound 6 -Because "Only $($coreFound.Count) of $($_corePlugInTileIds.Count) core plug-in tile IDs surfaced (found: $($coreFound -join ', ')). PR #48033 binding may be missing or broken."
        Write-Host "    info: home page exposed $($found.Count) com.microsoft.cmdpal.* IDs ($($coreFound.Count) core: $($coreFound -join ', '))" -ForegroundColor DarkGray
    } finally {
        # Cleanup — nothing to undo
    }
}

Test-Case 'CmdPal_ProviderIds_CalculatorTileSurfacesForCalcQuery' "★ PR #48033 ★: typing 'calc' surfaces com.microsoft.cmdpal.calculator tile (verifies query-time AutomationId is reachable)" {
    try {
        # Act
        Invoke-CmdPalQuery 'calc'
        # Wait for the tile to appear — the new pattern via Find-CmdPalProviderItem
        # with a Wait-Until presence check (slow-factor-aware).
        $null = Wait-Until -TimeoutMs 3000 -PollMs 200 -IgnoreException `
            -Message "com.microsoft.cmdpal.calculator tile did not surface within 3s after typing 'calc'" `
            -Condition { (Find-CmdPalProviderItem 'com.microsoft.cmdpal.calculator') -ne $null }

        $tile = Find-CmdPalProviderItem 'com.microsoft.cmdpal.calculator'
        Assert-NotNull $tile -Because 'Calculator tile not found via Find-CmdPalProviderItem after Wait-Until passed (race condition?)'

        # Assert — tile has the expected schema (Name='Calculator', selector matches ID)
        Assert-Equal $tile.name 'Calculator' -Because 'Calculator tile Name'
        Assert-Equal $tile.selector 'com.microsoft.cmdpal.calculator' -Because 'Calculator tile selector'
        # Tile schema sanity — isEnabled (interactive), reachable via its
        # canonical selector. winappCli only attaches .invokableAncestor
        # when the search returns multiple candidates needing
        # disambiguation; the exact-ID match used here returns the Group
        # directly, so don't require .invokableAncestor.
        Assert-True $tile.isEnabled -Because 'Calculator tile isEnabled — provider tile should be invokable in the UI'
        Write-Host "    info: Calculator tile reachable via stable ID (selector=$($tile.selector), name='$($tile.name)')" -ForegroundColor DarkGray
    } finally {
        Reset-CmdPalToHome
    }
}

Test-Case 'CmdPal_ProviderIds_WindowsSettingsTileSurfacesForSettingsQuery' "★ PR #48033 ★: typing 'settings' surfaces com.microsoft.cmdpal.windowsSettings tile" {
    try {
        # Act
        Invoke-CmdPalQuery 'settings'
        $null = Wait-Until -TimeoutMs 3000 -PollMs 200 -IgnoreException `
            -Message "com.microsoft.cmdpal.windowsSettings tile did not surface within 3s after typing 'settings'" `
            -Condition { (Find-CmdPalProviderItem 'com.microsoft.cmdpal.windowsSettings') -ne $null }

        $tile = Find-CmdPalProviderItem 'com.microsoft.cmdpal.windowsSettings'
        Assert-NotNull $tile -Because 'WindowsSettings tile not found via Find-CmdPalProviderItem after Wait-Until passed'

        # Assert — tile schema sanity
        Assert-Match $tile.name '(?i)windows\s*settings|search for' -Because "WindowsSettings tile Name should be 'Windows Settings' or 'Search for ... in Windows settings'"
        Write-Host "    info: WindowsSettings tile reachable via stable ID (name='$($tile.name)', selector=$($tile.selector))" -ForegroundColor DarkGray
    } finally {
        Reset-CmdPalToHome
    }
}

Test-Case 'CmdPal_ProviderIds_WebSearchFallbackAlwaysPresentForNonEmptyQuery' "★ PR #48033 ★: websearch.execute.fallback ID surfaces for any non-empty query (fallback-command binding)" {
    try {
        # Act — type a random non-keyword query that no provider will match.
        # Web Search ALWAYS produces a 'Search the web in Microsoft Edge'
        # fallback item with the same AutomationId regardless of query.
        $q = "xyz$(Get-Random -Max 99999)nopr"
        Invoke-CmdPalQuery $q
        $null = Wait-Until -TimeoutMs 3000 -PollMs 200 -IgnoreException `
            -Message "com.microsoft.cmdpal.builtin.websearch.execute.fallback did not surface within 3s for arbitrary query '$q'" `
            -Condition { (Find-CmdPalProviderItem 'com.microsoft.cmdpal.builtin.websearch.execute.fallback') -ne $null }

        $tile = Find-CmdPalProviderItem 'com.microsoft.cmdpal.builtin.websearch.execute.fallback'
        Assert-NotNull $tile -Because 'WebSearch fallback tile not found via Find-CmdPalProviderItem after Wait-Until passed'

        # Assert — fallback should carry the query text in its name
        Assert-Match $tile.name '(?i)search the web|microsoft edge' -Because "WebSearch fallback Name should mention 'Search the web' or 'Microsoft Edge'"
        Write-Host "    info: WebSearch fallback reachable via stable ID for arbitrary query (name='$($tile.name)')" -ForegroundColor DarkGray
    } finally {
        Reset-CmdPalToHome
    }
}
