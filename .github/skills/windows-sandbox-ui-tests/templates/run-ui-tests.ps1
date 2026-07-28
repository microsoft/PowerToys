# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Stages and runs a PowerToys UITest.Next payload inside Windows Sandbox.

.DESCRIPTION
This guest template is dispatched by Invoke-SandboxUiTest.ps1. It reads the generated request,
extracts archives to guest-local storage, provisions optional WebView2, runs the test executable,
and exports progress, status, TRX, logs, and attachments through the mapped exchange.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$RequestPath,
    [switch]$Detached
)

$ErrorActionPreference = 'Stop'

if ($Detached) {
    $arguments = '-NoLogo -NoProfile -ExecutionPolicy Bypass -File "{0}" -RequestPath "{1}"' -f $PSCommandPath,$RequestPath
    Start-Process powershell.exe -ArgumentList $arguments -WindowStyle Hidden
    return
}

$request = Get-Content $RequestPath -Raw | ConvertFrom-Json
$exchangeRoot = 'C:\SandboxExchange'
$workRoot = 'C:\PowerToysSandboxRun'
$testRoot = Join-Path $workRoot 'Tests'
$productRoot = Join-Path $workRoot 'PowerToys'
$winAppRoot = Join-Path $workRoot 'winappcli'
$dotNetRoot = Join-Path $workRoot 'dotnet'
$localResultsRoot = Join-Path $workRoot 'TestResults'
$localLog = Join-Path $workRoot 'sandbox-ui-tests.log'
$stagingManifestPath = Join-Path $workRoot 'staging-manifest.json'
$hostResultsRoot = Join-Path $exchangeRoot "SandboxResults\$($request.RunId)"
$startedUtc = [DateTime]::UtcNow
$exitCode = 1
$errorMessage = $null
$transcriptStarted = $false
$logicalProcessorCount = [Environment]::ProcessorCount
$processorAffinityMask = if ($null -eq $request.ProcessorAffinityMask) { [UInt64]3 } else { [UInt64]$request.ProcessorAffinityMask }
$reusedStagedPayload = $false

function Write-SharedText {
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [Parameter(Mandatory)]
        [string]$Value
    )

    for ($attempt = 1; $attempt -le 20; $attempt++) {
        try {
            $Value | Set-Content $Path -Encoding utf8
            return
        }
        catch [IO.IOException] {
            if ($attempt -eq 20) {
                throw
            }
            [Threading.Thread]::Sleep(100)
        }
    }
}

function Write-RunProgress {
    param(
        [Parameter(Mandatory)]
        [string]$Stage,
        [string]$Detail
    )

    New-Item $hostResultsRoot -ItemType Directory -Force | Out-Null
    $payload = [ordered]@{
        Stage = $Stage
        Detail = $Detail
        UpdatedUtc = [DateTime]::UtcNow.ToString('O')
        RunId = $request.RunId
    } | ConvertTo-Json
    Write-SharedText -Path (Join-Path $hostResultsRoot 'progress.json') -Value $payload
}

function Stop-RunProcesses {
    $cleanupProcesses = @('PowerToys', 'PowerToys.Settings', 'winapp') + @($request.CleanupProcesses)
    Get-Process -Name ($cleanupProcesses | Sort-Object -Unique) -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
}

try {
    Write-RunProgress -Stage 'Starting' -Detail 'The guest runner is active.'
    if ($processorAffinityMask -ne 0) {
        if ($processorAffinityMask -gt [Int64]::MaxValue) {
            throw 'ProcessorAffinityMask must not exceed 0x7FFFFFFFFFFFFFFF.'
        }

        $highestProcessorIndex = 0
        $remainingMask = $processorAffinityMask
        while ($remainingMask -gt 1) {
            $remainingMask = $remainingMask -shr 1
            $highestProcessorIndex++
        }
        if ($highestProcessorIndex -ge $logicalProcessorCount) {
            throw "ProcessorAffinityMask 0x$($processorAffinityMask.ToString('X')) selects a processor outside the guest's $logicalProcessorCount logical processors."
        }

        $currentProcess = [Diagnostics.Process]::GetCurrentProcess()
        try {
            $currentProcess.ProcessorAffinity = [IntPtr]([Int64]$processorAffinityMask)
        }
        finally {
            $currentProcess.Dispose()
        }
        Write-RunProgress -Stage 'Configuring' -Detail "Process-tree affinity 0x$($processorAffinityMask.ToString('X')) on $logicalProcessorCount logical processors"
    }

    Stop-RunProcesses
    $reuseRequested = $request.PSObject.Properties.Name -contains 'ReuseStagedPayload' -and [bool]$request.ReuseStagedPayload
    $refreshTests = $true
    $refreshProduct = $true
    $refreshWinAppCli = $true
    $refreshDotNet = $true
    $refreshedComponents = @()
    if ($reuseRequested) {
        if (-not (Test-Path $stagingManifestPath -PathType Leaf)) {
            throw 'Staged payload reuse was requested, but the guest manifest is missing.'
        }
        $stagingManifest = Get-Content $stagingManifestPath -Raw | ConvertFrom-Json
        if ($null -eq $request.PayloadHashes -or $null -eq $stagingManifest.PayloadHashes) {
            throw 'Staged payload reuse requires per-component hashes in both the request and guest manifest.'
        }

        $refreshTests = $stagingManifest.PayloadHashes.Tests -ne $request.PayloadHashes.Tests -or
            -not (Test-Path $testRoot -PathType Container)
        $refreshProduct = $stagingManifest.PayloadHashes.Product -ne $request.PayloadHashes.Product -or
            $stagingManifest.PayloadHashes.ProductOverlay -ne $request.PayloadHashes.ProductOverlay -or
            -not (Test-Path $productRoot -PathType Container)
        $refreshWinAppCli = $stagingManifest.PayloadHashes.WinAppCli -ne $request.PayloadHashes.WinAppCli -or
            -not (Test-Path $winAppRoot -PathType Container)
        $refreshDotNet = $stagingManifest.PayloadHashes.DotNet -ne $request.PayloadHashes.DotNet -or
            -not (Test-Path $dotNetRoot -PathType Container)
        $reusedStagedPayload = $true
        $changedComponents = @()
        if ($refreshTests) { $changedComponents += 'Tests' }
        if ($refreshProduct) { $changedComponents += 'Product' }
        if ($refreshWinAppCli) { $changedComponents += 'winappcli' }
        if ($refreshDotNet) { $changedComponents += '.NET' }
        $reuseDetail = if ($changedComponents.Count -eq 0) {
            "Unchanged payload $($request.PayloadFingerprint)"
        }
        else {
            "Refreshing: $($changedComponents -join ', ')"
        }
        Write-RunProgress -Stage 'Reusing' -Detail $reuseDetail
    }
    else {
        Remove-Item $workRoot -Recurse -Force -ErrorAction SilentlyContinue
        New-Item $workRoot -ItemType Directory -Force | Out-Null
    }

    Remove-Item $localResultsRoot -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $localLog -Force -ErrorAction SilentlyContinue
    New-Item $localResultsRoot -ItemType Directory -Force | Out-Null
    Start-Transcript -Path $localLog -Force | Out-Null
    $transcriptStarted = $true

    $components = @(
        [pscustomobject]@{ Name = 'Tests'; Refresh = $refreshTests; Archive = $request.Archives.Tests; Destination = $testRoot },
        [pscustomobject]@{ Name = 'Product'; Refresh = $refreshProduct; Archive = $request.Archives.Product; Destination = $productRoot },
        [pscustomobject]@{ Name = 'winappcli'; Refresh = $refreshWinAppCli; Archive = $request.Archives.WinAppCli; Destination = $winAppRoot },
        [pscustomobject]@{ Name = '.NET'; Refresh = $refreshDotNet; Archive = $request.Archives.DotNet; Destination = $dotNetRoot }
    )
    foreach ($component in $components) {
        if (-not $component.Refresh) {
            continue
        }
        $archivePath = Join-Path $exchangeRoot $component.Archive
        if (-not (Test-Path $archivePath -PathType Leaf)) {
            throw "Required payload is missing: $archivePath"
        }
        Remove-Item $component.Destination -Recurse -Force -ErrorAction SilentlyContinue
        $stage = if ($reuseRequested) { 'Refreshing' } else { 'Extracting' }
        Write-RunProgress -Stage $stage -Detail "$($component.Name): $($component.Archive)"
        Expand-Archive -Path $archivePath -DestinationPath $component.Destination -Force
        $refreshedComponents += $component.Name
    }

    if ($refreshProduct) {
        if (-not [string]::IsNullOrWhiteSpace($request.Archives.ProductOverlay)) {
            $overlayPath = Join-Path $exchangeRoot $request.Archives.ProductOverlay
            Write-RunProgress -Stage 'Overlaying' -Detail $request.BuildLabel
            Expand-Archive -Path $overlayPath -DestinationPath $productRoot -Force
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($request.WebView2Installer)) {
        $webView2Root = 'C:\Program Files (x86)\Microsoft\EdgeWebView\Application'
        if (-not (Test-Path $webView2Root)) {
            $installer = Join-Path $exchangeRoot $request.WebView2Installer
            if (-not (Test-Path $installer -PathType Leaf)) {
                throw "WebView2 installer is missing: $installer"
            }

            Write-RunProgress -Stage 'Installing' -Detail 'Microsoft Edge WebView2 Runtime (5 minute limit)'
            $installerProcess = Start-Process $installer -ArgumentList '/silent','/install' -PassThru
            if (-not $installerProcess.WaitForExit(300000)) {
                Stop-Process -Id $installerProcess.Id -Force -ErrorAction SilentlyContinue
                throw 'WebView2 installation exceeded five minutes.'
            }
            if ($installerProcess.ExitCode -ne 0 -or -not (Test-Path $webView2Root)) {
                throw "WebView2 installation failed with exit code $($installerProcess.ExitCode)."
            }
        }
    }

    [ordered]@{
        PayloadFingerprint = $request.PayloadFingerprint
        PayloadFiles = @($request.PayloadFiles)
        PayloadHashes = $request.PayloadHashes
        StagedUtc = [DateTime]::UtcNow.ToString('O')
    } | ConvertTo-Json -Depth 4 | Set-Content $stagingManifestPath -Encoding utf8

    Write-RunProgress -Stage 'Preparing' -Detail 'Locating the test runner and dependencies.'
    Get-ChildItem $winAppRoot -Recurse | Unblock-File -ErrorAction SilentlyContinue

    $requestedExecutables = if ($request.PSObject.Properties.Name -contains 'TestExecutables') {
        @($request.TestExecutables)
    }
    elseif ($request.PSObject.Properties.Name -contains 'TestExecutable') {
        @($request.TestExecutable)
    }
    else {
        @()
    }
    if ($requestedExecutables.Count -eq 0) {
        throw 'No test executables were requested.'
    }

    $testExecutables = @()
    foreach ($requestedExecutable in $requestedExecutables) {
        $testExe = Get-ChildItem $testRoot -Recurse -Filter $requestedExecutable -File | Select-Object -First 1
        if ($null -eq $testExe) {
            throw "$requestedExecutable was not found under $testRoot."
        }
        $testExecutables += $testExe
    }

    $winApp = Get-ChildItem $winAppRoot -Recurse -Filter 'winapp.exe' -File | Select-Object -First 1
    if ($null -eq $winApp) {
        throw "winapp.exe was not found under $winAppRoot."
    }
    if (-not (Test-Path (Join-Path $dotNetRoot 'dotnet.exe'))) {
        throw "dotnet.exe was not found under $dotNetRoot."
    }
    if (-not (Test-Path (Join-Path $productRoot 'PowerToys.exe'))) {
        throw "PowerToys.exe was not found under $productRoot."
    }

    $env:POWERTOYS_INSTALL_DIR = $productRoot
    $env:WINAPP_CLI_PATH = $winApp.FullName
    $env:DOTNET_ROOT = $dotNetRoot
    $env:PATH = "$dotNetRoot;$env:PATH"
    $env:TF_BUILD = 'true'
    $env:platform = $request.Platform
    $env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'

    $overallExitCode = 0
    for ($testIndex = 0; $testIndex -lt $testExecutables.Count; $testIndex++) {
        $testExe = $testExecutables[$testIndex]
        $testArguments = @(
            '--report-trx',
            '--report-trx-filename', "$($testExe.BaseName).trx",
            '--results-directory', $localResultsRoot,
            '--timeout', $request.SuiteTimeout
        )
        $effectiveFilter = $null
        if (-not [string]::IsNullOrWhiteSpace($request.Filter)) {
            $effectiveFilter = if ($request.Filter -match '[=~!&|()]') { $request.Filter } else { "Name=$($request.Filter)" }
            $testArguments += @('--filter', $effectiveFilter)
        }

        $testDetail = "$($testExe.Name) ($($testIndex + 1)/$($testExecutables.Count))"
        if ($effectiveFilter) {
            $testDetail += ": $effectiveFilter"
        }
        Write-RunProgress -Stage 'Testing' -Detail $testDetail
        Set-Location $testExe.DirectoryName
        & $testExe.FullName @testArguments
        $testExitCode = $LASTEXITCODE
        if ($testExitCode -ne 0 -and $overallExitCode -eq 0) {
            $overallExitCode = $testExitCode
        }

        Stop-RunProcesses
    }
    $exitCode = $overallExitCode
}
catch {
    $errorMessage = $_.Exception.Message
    Write-Host "Sandbox guest runner failed: $errorMessage"
}
finally {
    Stop-RunProcesses

    if ($transcriptStarted) {
        Stop-Transcript -ErrorAction SilentlyContinue | Out-Null
    }

    New-Item $hostResultsRoot -ItemType Directory -Force | Out-Null
    if (Test-Path $localResultsRoot) {
        Copy-Item $localResultsRoot (Join-Path $hostResultsRoot 'TestResults') -Recurse -Force
    }
    if (Test-Path $localLog) {
        Copy-Item $localLog (Join-Path $hostResultsRoot 'sandbox-ui-tests.log') -Force
    }

    $status = [ordered]@{
        Status = if ($exitCode -eq 0) { 'PASS' } else { 'FAIL' }
        ExitCode = $exitCode
        Error = $errorMessage
        BuildLabel = $request.BuildLabel
        Filter = $request.Filter
        Platform = $request.Platform
        ProcessorAffinityMask = "0x$($processorAffinityMask.ToString('X'))"
        LogicalProcessorCount = $logicalProcessorCount
        ReusedStagedPayload = $reusedStagedPayload
        RefreshedComponents = $refreshedComponents
        PayloadFingerprint = $request.PayloadFingerprint
        RunId = $request.RunId
        StartedUtc = $startedUtc.ToString('O')
        CompletedUtc = [DateTime]::UtcNow.ToString('O')
        User = [Security.Principal.WindowsIdentity]::GetCurrent().Name
        SessionId = (Get-Process -Id $PID).SessionId
        OsVersion = [Environment]::OSVersion.Version.ToString()
    }
    Write-SharedText -Path (Join-Path $hostResultsRoot 'status.json') -Value ($status | ConvertTo-Json)
    Write-RunProgress -Stage 'Completed' -Detail $status.Status
}

exit $exitCode