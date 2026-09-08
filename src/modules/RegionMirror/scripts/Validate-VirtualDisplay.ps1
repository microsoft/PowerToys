# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.

<#
.SYNOPSIS
Runs a bounded local virtual-display acceptance check, without joining or sharing a meeting.
.DESCRIPTION
Requires elevation. -StageDriver explicitly stages the pinned external driver first.
Captures a small source region for 15 seconds, then waits for device removal and
the original physical display layout, even when capture fails.
-CrossMonitor selects a region intersecting at least two existing physical monitors
and requires capture frames from every reported source. Its evidence is stored separately.
#>
[CmdletBinding()]
param(
    [switch] $StageDriver,
    [switch] $CrossMonitor
)

$ErrorActionPreference = 'Stop'
$moduleDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$executable = Join-Path $moduleDirectory 'bin\x64\Debug\PowerToys.RegionMirror.exe'
$outputDirectory = Join-Path $moduleDirectory $(if ($CrossMonitor) { 'artifacts\virtual-display-validation-cross-monitor' } else { 'artifacts\virtual-display-validation' })
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this acceptance script in an administrator PowerShell session.'
}
if (-not (Test-Path -LiteralPath $executable)) {
    throw 'The Debug x64 application has not been built.'
}

function Read-DisplaySnapshot {
    param([string] $Path, [int] $TimeoutMilliseconds = 15000)

    # Each snapshot must come from this helper invocation, not an earlier acceptance run.
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        Remove-Item -LiteralPath $Path -Force
    }
    $process = Start-Process -FilePath $executable -ArgumentList @('--list-displays', '--report', ('"' + $Path + '"')) -WindowStyle Hidden -PassThru
    try {
        if (-not $process.WaitForExit($TimeoutMilliseconds)) {
            try { $process.Kill() } catch { Write-Verbose $_.Exception.Message }
            throw 'Timed out while enumerating active displays.'
        }
        if ($process.ExitCode -ne 0) { throw "Display enumeration failed with exit code $($process.ExitCode)." }
        if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw 'Display enumeration did not write a snapshot.' }
        Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    } finally {
        $process.Dispose()
    }
}

function Get-DisplayLayoutKey {
    param([object[]] $Displays)

    if ($Displays.Count -eq 0) { throw 'No active displays were returned.' }
    $layout = @(foreach ($display in $Displays) {
        if ([string]::IsNullOrWhiteSpace([string] $display.identity)) {
            throw 'A display is missing its stable identity; rebuild the application with monitorDevicePath reporting.'
        }
        [pscustomobject]@{
            identity = ([string] $display.identity).ToUpperInvariant()
            x = [long] $display.x
            y = [long] $display.y
            width = [long] $display.width
            height = [long] $display.height
        }
    })
    if (@($layout | Group-Object identity | Where-Object Count -GT 1).Count -ne 0) {
        throw 'Display identities are not unique; the original layout cannot be verified.'
    }
    # GDI device names can be renumbered after an adapter is added or removed.
    ConvertTo-Json -InputObject @($layout | Sort-Object identity) -Compress -Depth 3
}

function Test-RegionIntersectsDisplay {
    param([object] $Region, [object] $Display)

    $left = [Math]::Max([long] $Region.x, [long] $Display.x)
    $top = [Math]::Max([long] $Region.y, [long] $Display.y)
    $right = [Math]::Min(([long] $Region.x + [long] $Region.width), ([long] $Display.x + [long] $Display.width))
    $bottom = [Math]::Min(([long] $Region.y + [long] $Region.height), ([long] $Display.y + [long] $Display.height))
    $right -gt $left -and $bottom -gt $top
}

function Get-CrossMonitorRegion {
    param([object[]] $Displays)

    $candidates = @($Displays | Where-Object { $_.width -ge 64 -and $_.height -ge 64 })
    if ($candidates.Count -lt 2) { throw '-CrossMonitor requires at least two active physical source monitors.' }

    # Try every horizontal pair before considering a vertical pair. Both pieces
    # have positive area and fit inside their monitors' shared-edge interval.
    foreach ($orientation in @('horizontal', 'vertical')) {
        for ($first = 0; $first -lt $candidates.Count - 1; $first++) {
            for ($second = $first + 1; $second -lt $candidates.Count; $second++) {
                $a = $candidates[$first]
                $b = $candidates[$second]
                if ($orientation -eq 'horizontal') {
                    if (($a.x + $a.width) -eq $b.x) { $left = $a; $right = $b }
                    elseif (($b.x + $b.width) -eq $a.x) { $left = $b; $right = $a }
                    else { continue }
                    $overlapStart = [Math]::Max([long] $left.y, [long] $right.y)
                    $overlapEnd = [Math]::Min(([long] $left.y + [long] $left.height), ([long] $right.y + [long] $right.height))
                    if ($overlapEnd -le $overlapStart) { continue }
                    $height = [Math]::Min(360, $overlapEnd - $overlapStart)
                    $leftWidth = [Math]::Min(640, [long] $left.width)
                    $rightWidth = [Math]::Min(640, [long] $right.width)
                    return [pscustomobject]@{
                        x = [long] $left.x + [long] $left.width - $leftWidth
                        y = $overlapStart + [long] [Math]::Floor(($overlapEnd - $overlapStart - $height) / 2)
                        width = $leftWidth + $rightWidth
                        height = $height
                        selectionKind = 'horizontal-adjacency'
                    }
                } else {
                    if (($a.y + $a.height) -eq $b.y) { $top = $a; $bottom = $b }
                    elseif (($b.y + $b.height) -eq $a.y) { $top = $b; $bottom = $a }
                    else { continue }
                    $overlapStart = [Math]::Max([long] $top.x, [long] $bottom.x)
                    $overlapEnd = [Math]::Min(([long] $top.x + [long] $top.width), ([long] $bottom.x + [long] $bottom.width))
                    if ($overlapEnd -le $overlapStart) { continue }
                    $width = [Math]::Min(1280, $overlapEnd - $overlapStart)
                    $topHeight = [Math]::Min(180, [long] $top.height)
                    $bottomHeight = [Math]::Min(180, [long] $bottom.height)
                    return [pscustomobject]@{
                        x = $overlapStart + [long] [Math]::Floor(($overlapEnd - $overlapStart - $width) / 2)
                        y = [long] $top.y + [long] $top.height - $topHeight
                        width = $width
                        height = $topHeight + $bottomHeight
                        selectionKind = 'vertical-adjacency'
                    }
                }
            }
        }
    }

    # For disconnected or diagonal layouts, retain the gap in a single desktop
    # rectangle instead of pretending that the displays are adjacent. Pick the
    # smallest valid bounding rectangle of two interior sample squares.
    $samples = @(foreach ($display in $candidates) {
        $width = [Math]::Min(128, [long] $display.width - 32)
        $height = [Math]::Min(128, [long] $display.height - 32)
        [pscustomobject]@{
            x = [long] $display.x + [long] [Math]::Floor(($display.width - $width) / 2)
            y = [long] $display.y + [long] [Math]::Floor(($display.height - $height) / 2)
            width = $width
            height = $height
        }
    })
    $best = $null
    $bestArea = [long]::MaxValue
    for ($first = 0; $first -lt $samples.Count - 1; $first++) {
        for ($second = $first + 1; $second -lt $samples.Count; $second++) {
            $a = $samples[$first]
            $b = $samples[$second]
            $left = [Math]::Min([long] $a.x, [long] $b.x)
            $top = [Math]::Min([long] $a.y, [long] $b.y)
            $width = [Math]::Max(($a.x + $a.width), ($b.x + $b.width)) - $left
            $height = [Math]::Max(($a.y + $a.height), ($b.y + $b.height)) - $top
            if ($width -gt 16384 -or $height -gt 16384) { continue }
            $area = [long] $width * [long] $height
            if ($area -lt $bestArea) {
                $bestArea = $area
                $best = [pscustomobject]@{ x = $left; y = $top; width = $width; height = $height; selectionKind = 'interior-samples-with-gap' }
            }
        }
    }
    if (-not $best) { throw 'No two source monitors fit within the PoC region-dimension limit.' }
    $best
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
Start-Transcript -Path (Join-Path $outputDirectory 'validation.log') -Force | Out-Null
try {
    if ($StageDriver) {
        & (Join-Path $PSScriptRoot 'Prepare-VirtualDisplayDriver.ps1') -Stage
    }
    $beforePath = Join-Path $outputDirectory 'displays-before.json'
    $before = @(Read-DisplaySnapshot -Path $beforePath)
    $beforeLayout = Get-DisplayLayoutKey -Displays $before
    $baselineDeviceInstances = @(Get-PnpDevice -PresentOnly -ErrorAction Stop |
        Where-Object InstanceId -Like 'SWD\PowerToysRegionMirror\*' | Select-Object -ExpandProperty InstanceId)
    # MTT1337 is the pinned driver's EDID identity; never use an existing virtual
    # target as source evidence. New targets are created only after this selection.
    $physicalSources = @($before | Where-Object { $_.identity -notmatch '(?i)#MTT1337#' })
    if ($CrossMonitor) {
        $selectedRegion = Get-CrossMonitorRegion -Displays $physicalSources
    } else {
        $source = $physicalSources | Where-Object { $_.x -le 0 -and $_.y -le 0 -and ($_.x + $_.width) -gt 0 -and ($_.y + $_.height) -gt 0 } | Select-Object -First 1
        if (-not $source) { $source = $physicalSources | Select-Object -First 1 }
        if (-not $source -or $source.width -lt 128 -or $source.height -lt 128) { throw 'No suitable active source display.' }
        $selectedRegion = [pscustomobject]@{
            x = [long] $source.x + 32
            y = [long] $source.y + 32
            width = [Math]::Min(640, $source.width - 64)
            height = [Math]::Min(360, $source.height - 64)
            selectionKind = 'single-monitor'
        }
    }
    $expectedSources = @($physicalSources | Where-Object { Test-RegionIntersectsDisplay -Region $selectedRegion -Display $_ })
    if ($expectedSources.Count -lt $(if ($CrossMonitor) { 2 } else { 1 })) {
        throw 'The chosen region does not have a positive-area intersection with every required physical source monitor.'
    }
    $region = '{0},{1},{2},{3}' -f $selectedRegion.x, $selectedRegion.y, $selectedRegion.width, $selectedRegion.height
    $reportPath = Join-Path $outputDirectory 'capture-result.json'
    $afterPath = Join-Path $outputDirectory 'displays-after.json'
    $acceptancePath = Join-Path $outputDirectory 'acceptance.json'
    foreach ($stalePath in @($reportPath, $afterPath, $acceptancePath)) {
        if (Test-Path -LiteralPath $stalePath -PathType Leaf) { Remove-Item -LiteralPath $stalePath -Force }
    }
    $applicationIssues = [Collections.Generic.List[string]]::new()
    $captureProcess = $null
    $report = $null
    Write-Host "Capturing $region for 15 seconds. The PoC controller will be visible."
    try {
        # This is the interactive PoC being tested, not a background helper.
        $captureProcess = Start-Process -FilePath $executable -ArgumentList @('--region', $region, '--duration', '15', '--report', ('"' + $reportPath + '"')) -Wait -PassThru
    } catch {
        $applicationIssues.Add("Could not complete the capture process: $($_.Exception.Message)")
    }
    try {
        if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) { throw 'The capture process did not write its acceptance report.' }
        $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
        if (-not $report -or $report -is [array]) { throw 'The capture acceptance report is empty or invalid.' }
        foreach ($requiredProperty in @('exitCode', 'framesPresented', 'ownedDeviceInstance', 'error', 'sourceCount', 'sources', 'sourceFrames')) {
            if ($report.PSObject.Properties.Name -notcontains $requiredProperty) { throw "The capture report is missing $requiredProperty." }
        }
    } catch {
        $applicationIssues.Add($_.Exception.Message)
    }
    if ($report.error) { $applicationIssues.Add("Application error: $($report.error)") }
    if ($captureProcess -and $captureProcess.ExitCode -ne 0) { $applicationIssues.Add("The capture process exited with code $($captureProcess.ExitCode).") }
    if ($report) {
        if ($report.exitCode -ne 0) { $applicationIssues.Add("The capture report has exit code $($report.exitCode).") }
        if ($report.framesPresented -lt 1) { $applicationIssues.Add('The application presented no capture frames.') }
        if (-not $report.ownedDeviceInstance) { $applicationIssues.Add('The application did not report an owned device instance.') }
        try {
            $sourceCount = [long] $report.sourceCount
            if ($sourceCount -lt $(if ($CrossMonitor) { 2 } else { 1 })) {
                $applicationIssues.Add("The application reported only $sourceCount source monitors.")
            }
            $reportedSources = @($report.sources)
            $sourceFrames = @($report.sourceFrames)
            if ($report.sources -isnot [array] -or $reportedSources.Count -ne $sourceCount) {
                $applicationIssues.Add('The sources array length does not match sourceCount.')
            }
            if ($report.sourceFrames -isnot [array] -or $sourceFrames.Count -ne $sourceCount) {
                $applicationIssues.Add('The sourceFrames array length does not match sourceCount.')
            }
            if (@($sourceFrames | Where-Object { [long] $_ -lt 1 }).Count -ne 0) {
                $applicationIssues.Add('At least one source monitor produced no capture frames.')
            }
            $reportedIdentities = @(foreach ($reportedSource in $reportedSources) {
                if ([string]::IsNullOrWhiteSpace([string] $reportedSource.identity)) { throw 'A reported source is missing its stable identity.' }
                ([string] $reportedSource.identity).ToUpperInvariant()
            })
            $expectedIdentities = @($expectedSources | ForEach-Object { ([string] $_.identity).ToUpperInvariant() })
            if ($reportedIdentities.Count -eq 0 -or @($reportedIdentities | Select-Object -Unique).Count -ne $reportedIdentities.Count) {
                $applicationIssues.Add('Reported source identities are empty or duplicated.')
            } elseif (@(Compare-Object -ReferenceObject $expectedIdentities -DifferenceObject $reportedIdentities).Count -ne 0) {
                $applicationIssues.Add('Reported source identities do not match the physical monitors intersected by the selected region.')
            }
        } catch {
            $applicationIssues.Add("Could not validate source-monitor evidence: $($_.Exception.Message)")
        }
    }
    $applicationError = if ($applicationIssues.Count) { $applicationIssues -join ' ' } else { $null }

    # SwDeviceClose initiates removal asynchronously. Always observe cleanup, including failed runs.
    $cleanupStarted = [DateTime]::UtcNow
    $deadline = $cleanupStarted.AddSeconds(15)
    $ownedInstance = [string] $report.ownedDeviceInstance
    $cleanupObservations = [Collections.Generic.List[object]]::new()
    $stableSamples = 0
    $cleanupSucceeded = $false
    $ownedDeviceRemoved = $null
    $layoutMatches = $false
    $presentNewDevices = @()
    do {
        $deviceQuerySucceeded = $false
        $deviceQueryError = $null
        $layoutError = $null
        $presentOwnedDevices = @()
        $presentNewDevices = @()
        $ownedDeviceRemoved = $null
        $layoutMatches = $false
        try {
            $presentDevices = @(Get-PnpDevice -PresentOnly -ErrorAction Stop)
            $deviceQuerySucceeded = $true
            if ($ownedInstance) {
                $presentOwnedDevices = @($presentDevices | Where-Object InstanceId -EQ $ownedInstance | Select-Object -ExpandProperty InstanceId)
                $ownedDeviceRemoved = $presentOwnedDevices.Count -eq 0
            }
            # If the app failed before reporting its ID, still observe new devices in its namespace.
            # Existing instances are evidence only and are never removed by this script.
            $presentNewDevices = @($presentDevices | Where-Object {
                $_.InstanceId -like 'SWD\PowerToysRegionMirror\*' -and $baselineDeviceInstances -notcontains $_.InstanceId
            } | Select-Object -ExpandProperty InstanceId)
        } catch {
            $deviceQueryError = $_.Exception.Message
        }
        try {
            $remainingMilliseconds = [Math]::Max(1, [int] [Math]::Ceiling(($deadline - [DateTime]::UtcNow).TotalMilliseconds))
            $after = @(Read-DisplaySnapshot -Path $afterPath -TimeoutMilliseconds $remainingMilliseconds)
            $layoutMatches = (Get-DisplayLayoutKey -Displays $after) -eq $beforeLayout
        } catch {
            $layoutError = $_.Exception.Message
        }
        $devicesSettled = $deviceQuerySucceeded -and $presentOwnedDevices.Count -eq 0 -and $presentNewDevices.Count -eq 0
        if ($devicesSettled -and $layoutMatches) { $stableSamples++ } else { $stableSamples = 0 }
        $cleanupObservations.Add([pscustomobject]@{
            elapsedSeconds = [Math]::Round(([DateTime]::UtcNow - $cleanupStarted).TotalSeconds, 3)
            deviceQuerySucceeded = $deviceQuerySucceeded
            ownedDeviceRemoved = $ownedDeviceRemoved
            presentOwnedDeviceInstances = $presentOwnedDevices
            presentNewRegionMirrorDeviceInstances = $presentNewDevices
            originalDisplayLayoutMatches = $layoutMatches
            deviceQueryError = $deviceQueryError
            layoutError = $layoutError
        })
        # Two matching snapshots avoid accepting the first transient topology during removal.
        if ($stableSamples -ge 2) { $cleanupSucceeded = $true; break }
        $remainingMilliseconds = [int] [Math]::Ceiling(($deadline - [DateTime]::UtcNow).TotalMilliseconds)
        if ($remainingMilliseconds -gt 0) { Start-Sleep -Milliseconds ([Math]::Min(250, $remainingMilliseconds)) }
    } while ([DateTime]::UtcNow -lt $deadline)

    $failureReasons = [Collections.Generic.List[string]]::new()
    if ($applicationError) { $failureReasons.Add($applicationError) }
    if (-not $cleanupSucceeded) {
        $failureReasons.Add('Device removal and the original display layout did not stabilize within 15 seconds; see cleanupObservations in acceptance.json.')
    }
    $success = $applicationIssues.Count -eq 0 -and $cleanupSucceeded -and $ownedDeviceRemoved -eq $true
    $acceptance = [pscustomobject]@{
        success = $success
        originalApplicationError = $applicationError
        processExitCode = if ($captureProcess) { $captureProcess.ExitCode } else { $null }
        reportExitCode = $report.exitCode
        framesPresented = $report.framesPresented
        targetDisplay = $report.targetDisplay
        crossMonitor = [bool] $CrossMonitor
        selectedRegion = $selectedRegion
        expectedSources = $expectedSources
        sourceCount = $report.sourceCount
        sources = @($report.sources)
        sourceFrames = @($report.sourceFrames)
        sourceIdentity = $expectedSources[0].identity
        ownedDeviceInstance = $ownedInstance
        virtualDeviceRemoved = $ownedDeviceRemoved
        originalDisplayLayoutPreserved = $layoutMatches -and $cleanupSucceeded
        cleanupSucceeded = $cleanupSucceeded
        cleanupWaitSeconds = [Math]::Round(([DateTime]::UtcNow - $cleanupStarted).TotalSeconds, 3)
        cleanupObservations = @($cleanupObservations.ToArray())
        errors = @($failureReasons.ToArray())
        meetingShareTested = $false
    }
    try {
        $acceptance | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $acceptancePath -Encoding utf8
    } catch {
        $failureReasons.Add("Could not write cleanup evidence: $($_.Exception.Message)")
        throw ($failureReasons -join [Environment]::NewLine)
    }
    if (-not $success) { throw ($failureReasons -join [Environment]::NewLine) }
    Write-Host 'Virtual display creation, capture, removal, and display-layout preservation passed.'
} finally {
    Stop-Transcript -ErrorAction SilentlyContinue | Out-Null
}
