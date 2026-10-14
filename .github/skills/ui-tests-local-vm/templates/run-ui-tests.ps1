# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Stages and runs a PowerToys UITest.Next payload inside a persistent local Windows VM.

.DESCRIPTION
This guest template is dispatched by Invoke-LocalVmUiTest.ps1. It reads the generated request,
extracts archives to guest-local storage, provisions optional WebView2, runs the test executable,
and exports progress, status, TRX, logs, and attachments through the shared exchange.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$RequestPath,
    [switch]$Detached
)

$ErrorActionPreference = 'Stop'
$currentPowerShell = (Get-Process -Id $PID).Path

if ($Detached) {
    $arguments = '-NoLogo -NoProfile -ExecutionPolicy Bypass -File "{0}" -RequestPath "{1}"' -f $PSCommandPath,$RequestPath
    Start-Process $currentPowerShell -ArgumentList $arguments -WindowStyle Hidden
    return
}

$request = Get-Content $RequestPath -Raw | ConvertFrom-Json
$exchangeRoot = if ($request.PSObject.Properties.Name -contains 'ExchangeRoot') {
    [string]$request.ExchangeRoot
}
else {
    'C:\PowerToysUiTestExchange'
}
if ([string]::IsNullOrWhiteSpace($exchangeRoot) -or -not [IO.Path]::IsPathRooted($exchangeRoot)) {
    throw 'ExchangeRoot must be an absolute path.'
}
$workRoot = 'C:\PowerToysUiTestRun'
$testRoot = Join-Path $workRoot 'Tests'
$productRoot = Join-Path $workRoot 'PowerToys'
$winAppRoot = Join-Path $workRoot 'winappcli'
$dotNetRoot = Join-Path $workRoot 'dotnet'
$localResultsRoot = Join-Path $workRoot 'TestResults'
$localLog = Join-Path $workRoot 'local-vm-ui-tests.log'
$stagingManifestPath = Join-Path $workRoot 'staging-manifest.json'
$hostResultsRoot = Join-Path $exchangeRoot "LocalVmResults\$($request.RunId)"
$startedUtc = [DateTime]::UtcNow
$exitCode = 1
$errorMessage = $null
$transcriptStarted = $false
$outputHeartbeatSeconds = if ($null -eq $request.OutputHeartbeatSeconds) { 0 } else { [int]$request.OutputHeartbeatSeconds }
$reusedStagedPayload = $false
$heartbeatProcess = $null
$webView2Version = $null
$exportErrors = @()

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

function Copy-SharedItem {
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [Parameter(Mandatory)]
        [string]$Destination
    )

    $sourceItem = Get-Item $Path -Force
    if ($sourceItem.PSIsContainer) {
        $directoryDestination = Join-Path $Destination $sourceItem.Name
        New-Item $directoryDestination -ItemType Directory -Force | Out-Null
        & robocopy.exe $sourceItem.FullName $directoryDestination `
            /E /R:5 /W:1 /COPY:DAT /DCOPY:DAT /XJ /NP /NFL /NDL /NJH /NJS | Out-Null
        $robocopyExitCode = $LASTEXITCODE
        if ($robocopyExitCode -ge 8) {
            throw "robocopy failed with exit code $robocopyExitCode while exporting '$Path'."
        }
        return
    }

    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try {
            $fileDestination = Join-Path $Destination $sourceItem.Name
            Copy-Item $Path $fileDestination -Force -ErrorAction Stop
            return
        }
        catch {
            if ($attempt -eq 5) {
                throw
            }
            [Threading.Thread]::Sleep(200)
        }
    }
}

function Expand-PayloadArchive {
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [Parameter(Mandatory)]
        [string]$Destination
    )

    New-Item $Destination -ItemType Directory -Force | Out-Null
    $tar = Get-Command tar.exe -ErrorAction SilentlyContinue
    if ($null -eq $tar) {
        Expand-Archive -Path $Path -DestinationPath $Destination -Force
        return
    }

    & $tar.Source -xf $Path -C $Destination
    if ($LASTEXITCODE -ne 0) {
        throw "tar.exe failed with exit code $LASTEXITCODE while extracting '$Path'."
    }
}

function Stop-RunProcesses {
    $cleanupProcesses = @('PowerToys', 'PowerToys.Settings', 'winapp') + @($request.CleanupProcesses)
    Get-Process -Name ($cleanupProcesses | Sort-Object -Unique) -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
}

function Start-OutputHeartbeat {
    if ($outputHeartbeatSeconds -le 0) {
        return $null
    }

    $intervalMilliseconds = $outputHeartbeatSeconds * 1000
    $heartbeatScript = @"
while (`$true) {
    Write-Output ('[GuestHeartbeat] ' + [DateTime]::UtcNow.ToString('O'))
    [Threading.Thread]::Sleep($intervalMilliseconds)
}
"@
    $encodedHeartbeat = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($heartbeatScript))
    return Start-Process $currentPowerShell `
        -ArgumentList '-NoLogo','-NoProfile','-EncodedCommand',$encodedHeartbeat `
        -NoNewWindow -PassThru
}

function Get-WebView2RuntimeVersion {
    $clientId = '{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}'
    $registryPaths = @(
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\$clientId",
        "HKCU:\Software\Microsoft\EdgeUpdate\Clients\$clientId"
    )
    foreach ($registryPath in $registryPaths) {
        try {
            $versionText = [string](Get-ItemPropertyValue -Path $registryPath -Name 'pv' -ErrorAction Stop)
            if (-not [string]::IsNullOrWhiteSpace($versionText) -and [version]$versionText -gt [version]'0.0.0.0') {
                return $versionText
            }
        }
        catch {
        }
    }

    $runtimeRoots = @(
        'C:\Program Files (x86)\Microsoft\EdgeWebView\Application',
        (Join-Path $env:LOCALAPPDATA 'Microsoft\EdgeWebView\Application')
    )
    foreach ($runtimeRoot in $runtimeRoots) {
        if (-not (Test-Path $runtimeRoot -PathType Container)) {
            continue
        }
        foreach ($versionDirectory in Get-ChildItem $runtimeRoot -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending) {
            try {
                if ([version]$versionDirectory.Name -gt [version]'0.0.0.0' -and
                    (Test-Path (Join-Path $versionDirectory.FullName 'msedgewebview2.exe') -PathType Leaf)) {
                    return $versionDirectory.Name
                }
            }
            catch {
            }
        }
    }

    return $null
}

try {
    Write-RunProgress -Stage 'Starting' -Detail 'The guest runner is active.'
    $heartbeatProcess = Start-OutputHeartbeat

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
        Expand-PayloadArchive -Path $archivePath -Destination $component.Destination
        $refreshedComponents += $component.Name
    }

    if ($refreshProduct) {
        if (-not [string]::IsNullOrWhiteSpace($request.Archives.ProductOverlay)) {
            $overlayPath = Join-Path $exchangeRoot $request.Archives.ProductOverlay
            Write-RunProgress -Stage 'Overlaying' -Detail $request.BuildLabel
            Expand-PayloadArchive -Path $overlayPath -Destination $productRoot
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($request.WebView2Installer)) {
        $webView2Version = Get-WebView2RuntimeVersion
        if ([string]::IsNullOrWhiteSpace($webView2Version)) {
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
            $webView2Version = Get-WebView2RuntimeVersion
            if ([string]::IsNullOrWhiteSpace($webView2Version)) {
                $installerExitCode = $installerProcess.ExitCode
                $installerExitCodeHex = '0x{0:X8}' -f ([uint32]([int64]$installerExitCode -band 0xffffffffL))
                throw "WebView2 installation failed with exit code $installerExitCode ($installerExitCodeHex), and no runtime was detected."
            }
        }
        Write-RunProgress -Stage 'Preparing' -Detail "Microsoft Edge WebView2 Runtime $webView2Version"
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
    $env:WINAPP_CLI_INVOKE_TIMEOUT_SECONDS = '180'
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
        $trxPath = Join-Path $localResultsRoot "$($testExe.BaseName).trx"
        if ($testExitCode -eq 0) {
            if (-not (Test-Path $trxPath -PathType Leaf)) {
                Write-Host "Test runner returned success without producing '$trxPath'."
                $testExitCode = 1
            }
            else {
                [xml]$trx = Get-Content $trxPath -Raw
                $counters = $trx.TestRun.ResultSummary.Counters
                $total = [int]$counters.total
                $executed = [int]$counters.executed
                if ($total -eq 0 -or $executed -ne $total) {
                    Write-Host "Incomplete test run: total=$total, executed=$executed, notExecuted=$([int]$counters.notExecuted)."
                    $testExitCode = 1
                }
            }
        }
        if ($testExitCode -ne 0 -and $overallExitCode -eq 0) {
            $overallExitCode = $testExitCode
        }

        Stop-RunProcesses
    }
    $exitCode = $overallExitCode
}
catch {
    $errorMessage = $_.Exception.Message
    Write-Host "Local VM guest runner failed: $errorMessage"
}
finally {
    if ($null -ne $heartbeatProcess) {
        Stop-Process -Id $heartbeatProcess.Id -Force -ErrorAction SilentlyContinue
        $heartbeatProcess.Dispose()
    }
    Stop-RunProcesses

    if ($transcriptStarted) {
        Stop-Transcript -ErrorAction SilentlyContinue | Out-Null
    }

    New-Item $hostResultsRoot -ItemType Directory -Force | Out-Null
    if (Test-Path $localResultsRoot) {
        $hostTestResultsRoot = Join-Path $hostResultsRoot 'TestResults'
        New-Item $hostTestResultsRoot -ItemType Directory -Force | Out-Null
        foreach ($resultItem in Get-ChildItem $localResultsRoot -Force) {
            try {
                Copy-SharedItem -Path $resultItem.FullName -Destination $hostTestResultsRoot
            }
            catch {
                $exportErrors += "Failed to export '$($resultItem.FullName)': $($_.Exception.Message)"
            }
        }
    }
    if (Test-Path $localLog) {
        try {
            Copy-SharedItem -Path $localLog -Destination $hostResultsRoot
        }
        catch {
            $exportErrors += "Failed to export '$localLog': $($_.Exception.Message)"
        }
    }
    if ($exportErrors.Count -gt 0) {
        if ($exitCode -eq 0) {
            $exitCode = 1
        }
        $exportErrorMessage = $exportErrors -join ' '
        $errorMessage = if ([string]::IsNullOrWhiteSpace($errorMessage)) {
            $exportErrorMessage
        }
        else {
            "$errorMessage $exportErrorMessage"
        }
    }

    $status = [ordered]@{
        Status = if ($exitCode -eq 0) { 'PASS' } else { 'FAIL' }
        ExitCode = $exitCode
        Error = $errorMessage
        ExportErrors = @($exportErrors)
        BuildLabel = $request.BuildLabel
        Filter = $request.Filter
        Platform = $request.Platform
        OutputHeartbeatSeconds = $outputHeartbeatSeconds
        WebView2Version = $webView2Version
        DesktopWidth = 0
        DesktopHeight = 0
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
    try {
        Add-Type -AssemblyName System.Windows.Forms
        $desktopBounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
        $status.DesktopWidth = $desktopBounds.Width
        $status.DesktopHeight = $desktopBounds.Height
    }
    catch {
    }
    Write-SharedText -Path (Join-Path $hostResultsRoot 'status.json') -Value ($status | ConvertTo-Json)
    Write-RunProgress -Stage 'Completed' -Detail $status.Status
}

exit $exitCode
