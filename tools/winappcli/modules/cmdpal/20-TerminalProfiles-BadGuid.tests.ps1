#Requires -Version 7.0
# 20-TerminalProfiles-BadGuid.tests.ps1 — extracted from command-palette-checklist.ps1 during Phase 2b split.
# Dot-sourced from the orchestrator so it shares script scope ($cpHwnd,
# $cpSettings, $cpEnabled, $cpDataDir). See _helpers.ps1 for the
# CmdPal-specific helper functions these tests call into.
# ── 0.99.0 PR #46372 — Terminal bad GUID does not break listing ─────
# Backup WT settings.json, corrupt the LAST profile's GUID to a non-GUID
# string, restart CmdPal so it re-reads WT profiles, query for the FIRST
# profile's name, assert it still shows up. Restore WT settings on cleanup.
Test-Case 'CmdPal_TerminalProfiles_BadGuidInWtSettingsDoesNotBreakListing' "★ 0.99.0: PR #46372 — corrupting one WT profile GUID does not break the rest of the CmdPal terminal profile listing" {
    # Arrange
            $wtPkg = Get-ChildItem "$env:LOCALAPPDATA\Packages" -Filter 'Microsoft.WindowsTerminal*' -Directory -EA SilentlyContinue | Select-Object -First 1
    if (-not $wtPkg) {
    Write-Host '    info: skipping — Windows Terminal not installed' -ForegroundColor Yellow
    return @{ skipped = $true }
    }
    $wtSettings = Join-Path $wtPkg.FullName 'LocalState\settings.json'
    if (-not (Test-Path $wtSettings)) {
    Write-Host '    info: skipping — WT settings.json missing' -ForegroundColor Yellow
    return @{ skipped = $true }
    }
    $backup = Join-Path $env:TEMP "winappcli-wt-settings-backup-$(Get-Random).json"
    Copy-Item $wtSettings $backup -Force
    $s = Get-Content $wtSettings -Raw | ConvertFrom-Json
    $origCount = $s.profiles.list.Count
    if ($origCount -lt 2) {
    Remove-Item $backup -Force -EA SilentlyContinue
    Write-Host '    info: skipping — need >=2 WT profiles for this test' -ForegroundColor Yellow
    return @{ skipped = $true }
    }
    $lastIdx = $origCount - 1
    $survivingName = $s.profiles.list[0].name
    $s.profiles.list[$lastIdx].guid = 'not-a-valid-guid-{NOT-A-GUID}'
    $utf8nb = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($wtSettings, ($s | ConvertTo-Json -Depth 30), $utf8nb)
    Restart-CmdPalAppX -WaitSec 12 | Out-Null
    $skipped = $false
    try {
    # Act
        if ($skipped) { return }
                    try {
                        Invoke-CmdPalQuery -Query $survivingName
                    } catch {
                        throw "could not echo query for surviving profile '$($survivingName)': $($_.Exception.Message)"
                    }
                    # Wait for the surviving profile to appear instead of a
                    # 1s blind sleep. Race-aware presence check.
                    $null = Wait-Until -TimeoutMs 3000 -PollMs 200 -IgnoreException `
                        -Message "surviving WT profile '$($survivingName)' not found within 3s after one bad GUID was injected" `
                        -Condition {
                            $r = winapp ui search $survivingName -w $cpHwnd --json 2>$null | ConvertFrom-Json
                            @($r.matches | Where-Object {
                                $_.type -eq 'ListItem' -and $_.name -match [regex]::Escape($survivingName)
                            }).Count -gt 0
                        }
                    # Re-fetch matches for the info log and assertion.
                    $r = winapp ui search $survivingName -w $cpHwnd --json 2>$null | ConvertFrom-Json
                    $matched = @($r.matches | Where-Object {
                        $_.type -eq 'ListItem' -and $_.name -match [regex]::Escape($survivingName)
                    })
                    Assert-GreaterThan $matched.Count 0 -Because {
                        $names = ($r.matches | Where-Object { $_.type -eq 'ListItem' } | Select-Object -First 8).name -join ', '
                        "surviving WT profile '$survivingName' not found in CmdPal after one bad GUID was injected. Got: $names — REGRESSION of #46372"
                    }
                    Write-Host "    info: surviving profile '$($survivingName)' still findable ($($matched.Count) match) after bad-GUID injection" -ForegroundColor DarkGray
    } finally {
    # Cleanup
        try {
                        if ($wtSettings -and $backup -and (Test-Path $backup)) {
                            Copy-Item $backup $wtSettings -Force
                            Remove-Item $backup -Force -EA SilentlyContinue
                            Restart-CmdPalAppX -WaitSec 12 | Out-Null
                        }
                        if ($cpHwnd) { winapp ui set-value 'MainSearchBox' '' -w $cpHwnd 2>$null | Out-Null }
                        Reset-CmdPalToHome
                    } catch { Write-Warning "[cleanup] $($_.Exception.Message)" }
    }
}
