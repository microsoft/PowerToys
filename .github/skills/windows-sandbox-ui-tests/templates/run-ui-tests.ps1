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
$hostResultsRoot = Join-Path $exchangeRoot "SandboxResults\$($request.RunId)"
$startedUtc = [DateTime]::UtcNow
$exitCode = 1
$errorMessage = $null
$transcriptStarted = $false

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

try {
    Write-RunProgress -Stage 'Starting' -Detail 'The guest runner is active.'
    Remove-Item $workRoot -Recurse -Force -ErrorAction SilentlyContinue
    New-Item $workRoot -ItemType Directory -Force | Out-Null
    New-Item $localResultsRoot -ItemType Directory -Force | Out-Null
    Start-Transcript -Path $localLog -Force | Out-Null
    $transcriptStarted = $true

    $archives = [ordered]@{
        $request.Archives.Tests = $testRoot
        $request.Archives.Product = $productRoot
        $request.Archives.WinAppCli = $winAppRoot
        $request.Archives.DotNet = $dotNetRoot
    }

    foreach ($archive in $archives.GetEnumerator()) {
        $archivePath = Join-Path $exchangeRoot $archive.Key
        if (-not (Test-Path $archivePath -PathType Leaf)) {
            throw "Required payload is missing: $archivePath"
        }

        Write-RunProgress -Stage 'Extracting' -Detail $archive.Key
        Expand-Archive -Path $archivePath -DestinationPath $archive.Value -Force
    }

    if (-not [string]::IsNullOrWhiteSpace($request.Archives.ProductOverlay)) {
        $overlayPath = Join-Path $exchangeRoot $request.Archives.ProductOverlay
        Write-RunProgress -Stage 'Overlaying' -Detail $request.BuildLabel
        Expand-Archive -Path $overlayPath -DestinationPath $productRoot -Force
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

    Write-RunProgress -Stage 'Preparing' -Detail 'Locating the test runner and dependencies.'
    Get-ChildItem $winAppRoot -Recurse | Unblock-File -ErrorAction SilentlyContinue

    $testExe = Get-ChildItem $testRoot -Recurse -Filter $request.TestExecutable -File | Select-Object -First 1
    $winApp = Get-ChildItem $winAppRoot -Recurse -Filter 'winapp.exe' -File | Select-Object -First 1
    if ($null -eq $testExe) {
        throw "$($request.TestExecutable) was not found under $testRoot."
    }
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

    $testArguments = @(
        '--report-trx',
        '--report-trx-filename', 'sandbox-ui-tests.trx',
        '--results-directory', $localResultsRoot,
        '--timeout', $request.SuiteTimeout
    )
    if (-not [string]::IsNullOrWhiteSpace($request.Filter)) {
        $effectiveFilter = if ($request.Filter -match '[=~!&|()]') { $request.Filter } else { "Name=$($request.Filter)" }
        $testArguments += @('--filter', $effectiveFilter)
    }

    Write-RunProgress -Stage 'Testing' -Detail $effectiveFilter
    Set-Location $testExe.DirectoryName
    & $testExe.FullName @testArguments
    $exitCode = $LASTEXITCODE
}
catch {
    $errorMessage = $_.Exception.Message
    Write-Host "Sandbox guest runner failed: $errorMessage"
}
finally {
    $cleanupProcesses = @('PowerToys', 'PowerToys.Settings', 'winapp') + @($request.CleanupProcesses)
    Get-Process -Name ($cleanupProcesses | Sort-Object -Unique) -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue

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