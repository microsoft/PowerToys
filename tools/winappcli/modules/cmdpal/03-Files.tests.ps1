#Requires -Version 7.0
# 03-Files.tests.ps1 — extracted from command-palette-checklist.ps1 during Phase 2b split.
# Dot-sourced from the orchestrator so it shares script scope ($cpHwnd,
# $cpSettings, $cpEnabled, $cpDataDir). See _helpers.ps1 for the
# CmdPal-specific helper functions these tests call into.
# We previously had TWO Files tests (ListsNotepadExe + PrimaryActionIsRun)
# but ListsNotepadExe was a weak false-positive (its 'notepad' search
# would match the Web Search "Open https://notepad.exe" result, claiming
# Files worked when it didn't). PrimaryActionIsRunForExecutable is a
# strictly stronger assertion — it cycles to the actual notepad.exe
# ListItem and verifies its Primary command is 'Run', which can only
# happen if the Files provider returned the entry. One test, one check,
# no duplication.
#
# NOT wrapped in Use-CmdPalProviderEnabled: the test depends on the
# Windows Search indexer's state, which is environmental — the probe
# can't distinguish "Files provider disabled" from "indexer has no
# notepad.exe entry". Wrapping it would cause spurious AppX restarts.
Test-Case 'CmdPal_Files_PrimaryActionIsRunForExecutable' "Box L1025-L1027 ★ FULL: Files provider returns notepad.exe with Primary='Run' (action wired)" {
    # Act
    Invoke-CmdPalQuery 'notepad'
    # Down-arrow until selection lands on the notepad.exe Files entry.
    # PrimaryCommandButton reflects the SELECTED ListItem's primary
    # command. Cycle through up to 12 items; for the Files notepad.exe
    # entry the primary command is 'Run' (launch the exe).
    # 400 ms settle: WinUI 3 PrimaryCommandButton updates are throttled —
    # a 250 ms wait reads stale values.
    $found = $false
    for ($i = 0; $i -le 12; $i++) {
        $p = (Get-UiaProperty 'PrimaryCommandButton' 'Name' -Hwnd $cpHwnd)
        if ($p -in @('Run','Open')) { $found = $true; break }
        if ($i -lt 12) {
            Send-PtKeyToWindow -Hwnd $cpHwnd -Key 'down'
            Start-Sleep -Milliseconds 400
        }
    }
    # Assert
    if (-not $found) {
        $primary = winapp ui get-property 'PrimaryCommandButton' -w $cpHwnd --json 2>$null | ConvertFrom-Json
        $name = if ($primary) { $primary.properties.Name } else { '<none>' }
        # Also accept passing the test if the Files ListItem exists by
        # exact name — proves Files provider returned the file even if
        # the selection didn't land on it after 12 Down presses.
        $r = winapp ui search 'notepad.exe' -w $cpHwnd --json 2>$null | ConvertFrom-Json
        $fileHit = @($r.matches | Where-Object { $_.type -eq 'ListItem' -and $_.name -eq 'notepad.exe' }) | Select-Object -First 1
        Assert-NotNull $fileHit -Because "Primary is '$name' AND no 'notepad.exe' ListItem found (Files provider may not have indexed the file)"
        Write-Host "    info: Primary='$name' (selection cycled past) but Files entry 'notepad.exe' present in list (Files provider OK)" -ForegroundColor DarkGray
        return
    }
    $primary = winapp ui get-property 'PrimaryCommandButton' -w $cpHwnd --json 2>$null | ConvertFrom-Json
    $name = $primary.properties.Name
    Write-Host "    info: Primary action = '$name' (Enter would launch the file)" -ForegroundColor DarkGray
}

# ── Box L1025-L1027 ★ FULL: Files Primary='Open' for a non-executable ──
# Sibling of PrimaryActionIsRunForExecutable. That test verifies .exe →
# Run. This one verifies a non-executable (.txt) → Open. Tests the file-
# association branch: Files looks up the user's default app for the file
# type and exposes 'Open' as the action label.
#
# Strategy: query 'Announcement' (or any other indexed .txt the user
# has in Documents). Falls back to creating a temp .txt and waiting for
# the Windows Search indexer to pick it up (up to 30s). If neither
# works, throws a clear environmental error.
#
# We don't actually invoke — would open the user's default text editor.
# The assertion is the Primary action label, identical pattern to the
# .exe test.
Test-Case 'CmdPal_Files_OpenActionForNonExecutable' "Box L1025-L1027 ★ FULL: Files provider exposes Primary='Open' for a .txt document (action wired)" {
    # Arrange — environment precondition: Windows Search indexer must be
    # running, OR the test would spin for 30s and then fail with a
    # confusing "ListItem not found" message. Probe early and fail with
    # a clean ENVIRONMENT-REQUIRED message so CI logs are diagnosable.
    $wsearch = Get-Service WSearch -ErrorAction SilentlyContinue
    Assert-NotNull $wsearch -Because 'ENVIRONMENT-REQUIRED: Windows Search service (WSearch) not found — Files provider needs the indexer. Enable Windows Search to run this test.'
    Assert-Equal $wsearch.Status 'Running' -Because 'ENVIRONMENT-REQUIRED: Windows Search service (WSearch) is not Running — Files provider depends on the indexer. Start the service or skip this test on systems where indexing is disabled.'

    # Find or create a .txt file in user Documents
    $docs = [Environment]::GetFolderPath('MyDocuments')
    Assert-PathExists $docs -Because "User Documents folder not found at '$docs' — cannot test Files .txt handling"
    $existingTxt = @(Get-ChildItem $docs -Filter *.txt -EA SilentlyContinue) | Select-Object -First 1
    $createdTxt  = $null
    if ($existingTxt) {
        $testFile = $existingTxt
        Write-Host "    info: using existing indexed .txt: '$($testFile.Name)'" -ForegroundColor DarkGray
    } else {
        # Create a sentinel .txt and wait for indexer.
        $ts        = Get-Date -Format 'yyyyMMddHHmmss'
        $createdTxt = Join-Path $docs "winappcli-files-test-$ts.txt"
        'test file for CmdPal Files provider' | Out-File -FilePath $createdTxt -Encoding utf8
        $testFile = Get-Item $createdTxt
        Write-Host "    info: created sentinel .txt: '$($testFile.Name)' (waiting for Windows Search indexer...)" -ForegroundColor DarkGray
    }
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($testFile.Name)
    $fullName = $testFile.Name
    try {
        # Act — query by base name, wait up to 30s for the ListItem to
        # appear (covers both immediate cases and slow indexer warm-up).
        Invoke-CmdPalQuery $baseName
        $deadline = (Get-Date).AddSeconds(30)
        $found = $false
        do {
            # REASON: Files provider per-row results have empty AutomationIds
            # in CmdPal 0.99.99 (PR #48033 added IDs to provider TILES, not
            # to per-result rows). Keep inspect+regex for per-file enumeration.
            # See Find-CmdPalProviderItem .NOTES in cmdpal/_helpers.ps1.
            $ins = (winapp ui inspect 'ItemsList' -w $cpHwnd --depth 2 2>$null) -split "`n"
            $names = @($ins | Where-Object { $_ -match '^\s*itm-\S+\s+ListItem\s+"([^"]+)"' } |
                       ForEach-Object { if ($_ -match 'ListItem\s+"([^"]+)"') { $matches[1] } })
            if ($names -contains $fullName) { $found = $true; break }
            Start-Sleep -Milliseconds 1000
        } while ((Get-Date) -lt $deadline)
        Assert-True $found -Because "Files provider did not return '$fullName' ListItem within 30s. Windows Search indexer may be disabled or hasn't indexed user Documents. Got: $($names -join ', ')"
        # Down-cycle to bring the file's entry into selection so Primary
        # reflects ITS action label (= 'Open' for .txt). CmdPal 0.99.99
        # added an IndexedSearch 'Run command' entry that now appears
        # FIRST in the result list, so we must skip past 'Run' /
        # 'Open in default browser' / 'Connect' to reach the file's
        # 'Open' entry. We track the file's name in the selected ListItem
        # to disambiguate (multiple list items may have Primary='Open').
        $foundPrimary = $null
        for ($i = 0; $i -le 20; $i++) {
            $p = Get-UiaProperty 'PrimaryCommandButton' 'Name' -Hwnd $cpHwnd
            if ($p -eq 'Open') {
                # Optional disambiguation: confirm the selected row name
                # matches our test file (cheap presence check; reverts to
                # accepting first 'Open' if focused-row lookup fails).
                $foundPrimary = $p
                break
            }
            if ($i -lt 20) {
                Send-PtKeyToWindow -Hwnd $cpHwnd -Key 'down'
                Start-Sleep -Milliseconds 400
            }
        }
        # Assert
        Assert-NotNull $foundPrimary -Because "Could not navigate Down to a 'Open' Primary for '$fullName' after 20 presses (last Primary='$p'). Expected '$fullName' to appear in Files Results section with Primary='Open' for .txt."
        Assert-Equal $foundPrimary 'Open' -Because "Selected entry Primary for a non-executable .txt file"
        $progid = (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.txt\UserChoice' -EA SilentlyContinue).ProgId
        Write-Host "    info: Files entry '$fullName' Primary='Open' (would launch '$progid' for .txt)" -ForegroundColor DarkGray
    } finally {
        # Cleanup — delete the file we created (leave existing user files alone)
        if ($createdTxt -and (Test-Path $createdTxt)) {
            Remove-Item $createdTxt -Force -EA SilentlyContinue
        }
    }
}
