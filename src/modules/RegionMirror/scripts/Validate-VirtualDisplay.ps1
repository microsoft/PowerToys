# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.

<#
.SYNOPSIS
Runs a bounded local virtual-display acceptance check, without joining or sharing a meeting.
.DESCRIPTION
Requires elevation. -StageDriver explicitly stages the pinned external driver first.
Captures a small source region for 15 seconds, then waits for device removal and
the original physical display layout, even when capture fails.
#>
[CmdletBinding()]
param([switch] $StageDriver)

$ErrorActionPreference = 'Stop'
$moduleDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$executable = Join-Path $moduleDirectory 'bin\x64\Debug\PowerToys.RegionMirror.exe'
$outputDirectory = Join-Path $moduleDirectory 'artifacts\virtual-display-validation'
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
    $source = $before | Where-Object { $_.x -le 0 -and $_.y -le 0 -and ($_.x + $_.width) -gt 0 -and ($_.y + $_.height) -gt 0 } | Select-Object -First 1
    if (-not $source) { $source = $before | Select-Object -First 1 }
    if (-not $source -or $source.width -lt 128 -or $source.height -lt 128) { throw 'No suitable active source display.' }
    $width = [Math]::Min(640, $source.width - 64)
    $height = [Math]::Min(360, $source.height - 64)
    $region = '{0},{1},{2},{3}' -f ($source.x + 32), ($source.y + 32), $width, $height
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
        foreach ($requiredProperty in @('exitCode', 'framesPresented', 'ownedDeviceInstance', 'error')) {
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
        sourceIdentity = $source.identity
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
