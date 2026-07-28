# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Runs a PowerToys UITest.Next executable in a fresh Windows Sandbox.

.DESCRIPTION
Launches Sandbox through its Start-menu registration, waits for the interactive login, dynamically
shares a prepared exchange, dispatches the bundled guest runner, streams progress, parses TRX
counters, and stops the exact Sandbox in a finally block.

.PARAMETER ExchangeRoot
Dedicated host folder containing run-ui-tests.ps1 and the requested payload archives.

.PARAMETER TestExecutable
One or more Microsoft.Testing.Platform test executable file names inside the tests archive. They run
sequentially in the same Sandbox, with PowerToys cleanup between suites.

.PARAMETER Filter
Optional MSTest filter such as Name=..., FullyQualifiedName~..., or TestCategory=....

.PARAMETER SuiteTimeout
Microsoft.Testing.Platform timeout inside the guest. Defaults to two hours; shorten it for focused
module runs or increase it for broader suites.

.PARAMETER TimeoutMinutes
Host controller deadline, including Sandbox startup and payload staging. Defaults to 150 minutes and
must remain larger than the expected guest execution time.

.PARAMETER ProcessorAffinityMask
Guest process affinity mask inherited by the test host and its directly launched descendants.
Defaults to 3 (0x3), which selects logical processors 0 and 1. Set to 0 to disable affinity limiting.

.PARAMETER StartupAttempts
Maximum clean Sandbox desktop startup attempts. Failed pre-login environments and remote sessions
are removed before retrying. Defaults to three.

.PARAMETER ReuseSandboxId
ID of a Sandbox retained by a previous run with -KeepSandbox. When specified, the controller attaches
to that exact interactive guest instead of creating a fresh one.

.PARAMETER ReuseStagedPayload
Reuse guest-local product, tests, winappcli, .NET, and WebView2 when their archive fingerprint matches
the retained guest manifest. Requires -ReuseSandboxId. Fresh Sandbox execution remains the default.

.EXAMPLE
pwsh ./Invoke-SandboxUiTest.ps1 -ExchangeRoot C:\Temp\Sandbox\Peek -TestExecutable Peek.UITests.Next.exe -Filter 'TestCategory=Peek' -InstallWebView2
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path $_ -PathType Container })]
    [string]$ExchangeRoot,

    [Parameter(Mandatory)]
    [string[]]$TestExecutable,

    [string]$Filter,
    [string]$Platform = 'x64Win11',
    [string]$BuildLabel = 'local',
    [string]$TestsArchive = 'ui-tests.zip',
    [string]$ProductArchive = 'powertoys-runtime.zip',
    [string]$WinAppCliArchive = 'winappcli.zip',
    [string]$DotNetArchive = 'dotnet-runtime.zip',
    [string]$ProductOverlayArchive,
    [string]$WebView2Installer = 'MicrosoftEdgeWebview2Setup.exe',
    [string]$GuestScript = 'run-ui-tests.ps1',
    [string]$SuiteTimeout = '2h',
    [string[]]$CleanupProcess = @(),
    [UInt64]$ProcessorAffinityMask = 3,
    [ValidateRange(1, 5)]
    [int]$StartupAttempts = 3,
    [ValidateRange(1, 1440)]
    [int]$TimeoutMinutes = 150,
    [guid]$ReuseSandboxId = [guid]::Empty,
    [switch]$ReuseStagedPayload,
    [switch]$InstallWebView2,
    [switch]$KeepSandbox
)

$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw 'Run this controller with PowerShell 7 (pwsh).'
}

$ExchangeRoot = (Resolve-Path $ExchangeRoot).Path
$resultsRoot = Join-Path $ExchangeRoot 'SandboxResults'
$runId = '{0}-{1}' -f [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'),[guid]::NewGuid().ToString('N').Substring(0, 8)
$hostResultRoot = Join-Path $resultsRoot $runId
$requestPath = Join-Path $hostResultRoot 'request.json'
$statusPath = Join-Path $hostResultRoot 'status.json'
$progressPath = Join-Path $hostResultRoot 'progress.json'
$sandboxId = $null
$mutex = [Threading.Mutex]::new($false, 'Local\PowerToysSandboxUITests')
$ownsMutex = $false

function Invoke-Wsb {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments,
        [switch]$AllowFailure
    )

    $output = & wsb.exe @Arguments 2>&1 | Out-String
    $exitCode = $LASTEXITCODE
    if (-not $AllowFailure -and $exitCode -ne 0) {
        throw "wsb $($Arguments[0]) failed with exit code $exitCode`: $($output.Trim())"
    }

    [pscustomobject]@{
        ExitCode = $exitCode
        Output = $output.Trim()
    }
}

function Get-SandboxIds {
    $result = Invoke-Wsb -Arguments @('list', '--raw')
    if ([string]::IsNullOrWhiteSpace($result.Output)) {
        return @()
    }

    $payload = $result.Output | ConvertFrom-Json
    return @($payload.WindowsSandboxEnvironments | ForEach-Object { [guid]$_.Id })
}

function Read-SharedJson {
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [int]$Attempts = 1
    )

    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        try {
            return Get-Content $Path -Raw | ConvertFrom-Json
        }
        catch {
            if ($attempt -eq $Attempts) {
                return $null
            }

            [Threading.Thread]::Sleep(100)
        }
    }
}

function Get-PayloadFingerprint {
    param(
        [Parameter(Mandatory)]
        [string[]]$Files
    )

    $fingerprintLines = foreach ($file in $Files | Sort-Object -Unique) {
        $hash = Get-ExchangeFileHash -File $file
        "$file=$hash"
    }
    $bytes = [Text.Encoding]::UTF8.GetBytes($fingerprintLines -join "`n")
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try {
        return [Convert]::ToHexString($sha256.ComputeHash($bytes))
    }
    finally {
        $sha256.Dispose()
    }
}

function Get-ExchangeFileHash {
    param(
        [string]$File
    )

    if ([string]::IsNullOrWhiteSpace($File)) {
        return $null
    }
    return (Get-FileHash (Join-Path $ExchangeRoot $File) -Algorithm SHA256).Hash
}

function Test-SandboxLogin {
    param(
        [Parameter(Mandatory)]
        [guid]$EnvironmentId
    )

    return Invoke-Wsb -Arguments @(
        'exec', '--id', $EnvironmentId.ToString(), '--run-as', 'ExistingLogin',
        '--command', 'cmd.exe /d /c exit 0', '--raw'
    ) -AllowFailure
}

function Stop-SandboxClientState {
    param(
        [guid]$EnvironmentId = [guid]::Empty
    )

    if ($EnvironmentId -ne [guid]::Empty) {
        Invoke-Wsb -Arguments @('stop', '--id', $EnvironmentId.ToString(), '--raw') -AllowFailure | Out-Null

        $stopDeadline = [DateTime]::UtcNow.AddMinutes(1)
        do {
            $remainingIds = @(Get-SandboxIds | Where-Object { $_ -eq $EnvironmentId })
            if ($remainingIds.Count -eq 0) {
                break
            }
            [Threading.Thread]::Sleep(250)
        } while ([DateTime]::UtcNow -lt $stopDeadline)

        if ($remainingIds.Count -ne 0) {
            throw "Sandbox environment $EnvironmentId did not stop within one minute."
        }
    }

    if (@(Get-SandboxIds).Count -eq 0) {
        Get-CimInstance Win32_Process |
            Where-Object Name -eq 'WindowsSandboxRemoteSession.exe' |
            ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    }
}

function Start-SandboxInteractiveSession {
    $lastFailure = $null

    for ($startupAttempt = 1; $startupAttempt -le $StartupAttempts; $startupAttempt++) {
        Stop-SandboxClientState
        Write-Host "Launching Windows Sandbox for run $runId (attempt $startupAttempt/$StartupAttempts)"
        Start-Process explorer.exe -ArgumentList 'shell:AppsFolder\Microsoft.Windows.Containers.Sandbox' | Out-Null

        $attemptSandboxId = $null
        $startDeadline = [DateTime]::UtcNow.AddMinutes(2)
        do {
            $ids = @(Get-SandboxIds)
            if ($ids.Count -eq 1) {
                $attemptSandboxId = $ids[0]
                break
            }
            [Threading.Thread]::Sleep(500)
        } while ([DateTime]::UtcNow -lt $startDeadline)

        if ($null -eq $attemptSandboxId) {
            $observedIds = @(Get-SandboxIds)
            $lastFailure = "No single environment ID appeared. Observed: $($observedIds -join ', ')"
            Write-Warning "Sandbox startup attempt $startupAttempt failed: $lastFailure"
            Stop-SandboxClientState
            continue
        }

        $probe = $null
        $loginDeadline = [DateTime]::UtcNow.AddMinutes(2)
        do {
            $probe = Test-SandboxLogin -EnvironmentId $attemptSandboxId
            if ($probe.ExitCode -eq 0) {
                return $attemptSandboxId
            }
            [Threading.Thread]::Sleep(500)
        } while ([DateTime]::UtcNow -lt $loginDeadline)

        $lastFailure = if ($null -eq $probe) { 'No login probe result.' } else { $probe.Output }
        Write-Warning "Sandbox startup attempt $startupAttempt failed before interactive login: $lastFailure"
        Stop-SandboxClientState -EnvironmentId $attemptSandboxId
    }

    throw "Windows Sandbox did not establish an interactive login after $StartupAttempts attempt(s). Last failure: $lastFailure"
}

try {
    try {
        $ownsMutex = $mutex.WaitOne(0)
    }
    catch [Threading.AbandonedMutexException] {
        $ownsMutex = $true
    }

    if (-not $ownsMutex) {
        throw 'Another PowerToys Sandbox UI-test controller is running.'
    }

    if ($ReuseStagedPayload -and $ReuseSandboxId -eq [guid]::Empty) {
        throw '-ReuseStagedPayload requires -ReuseSandboxId.'
    }

    Get-Command wsb.exe -ErrorAction Stop | Out-Null
    $startApp = Get-StartApps | Where-Object AppID -eq 'Microsoft.Windows.Containers.Sandbox'
    if ($null -eq $startApp) {
        throw 'Windows Sandbox Start-menu registration was not found. See .github/skills/windows-sandbox-ui-tests/references/setup.md.'
    }

    $requiredFiles = @($GuestScript, $TestsArchive, $ProductArchive, $WinAppCliArchive, $DotNetArchive)
    if ($InstallWebView2) {
        $requiredFiles += $WebView2Installer
    }
    if (-not [string]::IsNullOrWhiteSpace($ProductOverlayArchive)) {
        $requiredFiles += $ProductOverlayArchive
    }

    foreach ($requiredFile in $requiredFiles) {
        if (-not (Test-Path (Join-Path $ExchangeRoot $requiredFile) -PathType Leaf)) {
            throw "Required exchange file is missing: $requiredFile"
        }
    }

    $payloadFiles = @($TestsArchive, $ProductArchive, $WinAppCliArchive, $DotNetArchive)
    if ($InstallWebView2) {
        $payloadFiles += $WebView2Installer
    }
    if (-not [string]::IsNullOrWhiteSpace($ProductOverlayArchive)) {
        $payloadFiles += $ProductOverlayArchive
    }
    $payloadFingerprint = Get-PayloadFingerprint -Files $payloadFiles
    $payloadHashes = [ordered]@{
        Tests = Get-ExchangeFileHash -File $TestsArchive
        Product = Get-ExchangeFileHash -File $ProductArchive
        ProductOverlay = Get-ExchangeFileHash -File $ProductOverlayArchive
        WinAppCli = Get-ExchangeFileHash -File $WinAppCliArchive
        DotNet = Get-ExchangeFileHash -File $DotNetArchive
        WebView2Installer = if ($InstallWebView2) { Get-ExchangeFileHash -File $WebView2Installer } else { $null }
    }

    $existingIds = @(Get-SandboxIds)
    if ($ReuseSandboxId -eq [guid]::Empty -and $existingIds.Count -ne 0) {
        throw "Refusing to overlap existing Windows Sandbox environment(s): $($existingIds -join ', ')"
    }
    if ($ReuseSandboxId -ne [guid]::Empty) {
        if ($ReuseSandboxId -notin $existingIds) {
            throw "Requested retained Sandbox $ReuseSandboxId is not running. Active: $($existingIds -join ', ')"
        }
        $otherIds = @($existingIds | Where-Object { $_ -ne $ReuseSandboxId })
        if ($otherIds.Count -ne 0) {
            throw "Refusing reuse while other Sandbox environment(s) run: $($otherIds -join ', ')"
        }
    }

    New-Item $hostResultRoot -ItemType Directory -Force | Out-Null
    $request = [ordered]@{
        RunId = $runId
        BuildLabel = $BuildLabel
        TestExecutables = @($TestExecutable)
        Filter = $Filter
        Platform = $Platform
        SuiteTimeout = $SuiteTimeout
        Archives = [ordered]@{
            Tests = $TestsArchive
            Product = $ProductArchive
            WinAppCli = $WinAppCliArchive
            DotNet = $DotNetArchive
            ProductOverlay = $ProductOverlayArchive
        }
        WebView2Installer = if ($InstallWebView2) { $WebView2Installer } else { $null }
        CleanupProcesses = $CleanupProcess
        ProcessorAffinityMask = $ProcessorAffinityMask
        ReuseStagedPayload = [bool]$ReuseStagedPayload
        PayloadFingerprint = $payloadFingerprint
        PayloadFiles = @($payloadFiles | Sort-Object -Unique)
        PayloadHashes = $payloadHashes
    }
    $request | ConvertTo-Json -Depth 4 | Set-Content $requestPath -Encoding utf8

    $sandboxReused = $ReuseSandboxId -ne [guid]::Empty
    if ($sandboxReused) {
        $probe = Test-SandboxLogin -EnvironmentId $ReuseSandboxId
        if ($probe.ExitCode -ne 0) {
            throw "Retained Sandbox $ReuseSandboxId has no interactive login: $($probe.Output)"
        }
        $sandboxId = $ReuseSandboxId
        Write-Host "Reusing Windows Sandbox $sandboxId for run $runId"
    }
    else {
        $sandboxId = Start-SandboxInteractiveSession
    }

    $guestRequestPath = "C:\SandboxExchange\SandboxResults\$runId\request.json"
    $mappingProbe = Invoke-Wsb -Arguments @(
        'exec', '--id', $sandboxId.ToString(), '--run-as', 'ExistingLogin',
        '--command', "cmd.exe /d /c if exist `"$guestRequestPath`" (exit /b 0) else (exit /b 1)", '--raw'
    ) -AllowFailure
    if ($mappingProbe.ExitCode -ne 0) {
        Invoke-Wsb -Arguments @(
            'share', '--id', $sandboxId.ToString(),
            '--host-path', $ExchangeRoot,
            '--sandbox-path', 'C:\SandboxExchange',
            '--allow-write', '--raw'
        ) | Out-Null
    }

    $guestScriptPath = "C:\SandboxExchange\$GuestScript"
    $guestCommand = "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File `"$guestScriptPath`" -RequestPath `"$guestRequestPath`" -Detached"
    Invoke-Wsb -Arguments @(
        'exec', '--id', $sandboxId.ToString(), '--run-as', 'ExistingLogin',
        '--working-directory', 'C:\SandboxExchange',
        '--command', $guestCommand, '--raw'
    ) | Out-Null

    $deadline = [DateTime]::UtcNow.AddMinutes($TimeoutMinutes)
    $lastProgress = $null
    $nextHeartbeat = [DateTime]::UtcNow.AddSeconds(10)
    while (-not (Test-Path $statusPath) -and [DateTime]::UtcNow -lt $deadline) {
        if (Test-Path $progressPath) {
            $progress = Read-SharedJson -Path $progressPath
            if ($null -ne $progress) {
                $progressKey = "$($progress.Stage):$($progress.Detail)"
                if ($progressKey -ne $lastProgress) {
                    Write-Host "[$($progress.Stage)] $($progress.Detail)"
                    $lastProgress = $progressKey
                }
            }
        }
        if ([DateTime]::UtcNow -ge $nextHeartbeat) {
            $heartbeatDetail = if ($null -eq $lastProgress) { 'waiting for first guest progress' } else { $lastProgress }
            Write-Host "[Heartbeat] $heartbeatDetail"
            $nextHeartbeat = [DateTime]::UtcNow.AddSeconds(10)
        }
        [Threading.Thread]::Sleep(500)
    }

    if (-not (Test-Path $statusPath)) {
        throw "Sandbox run exceeded $TimeoutMinutes minute(s). Last progress: $lastProgress"
    }

    $status = Read-SharedJson -Path $statusPath -Attempts 20
    if ($null -eq $status) {
        throw 'status.json remained unreadable after the Sandbox run completed.'
    }

    $trxFiles = @(Get-ChildItem $hostResultRoot -Recurse -Filter '*.trx' -File)
    $summary = $null
    $suiteSummaries = @()
    if ($trxFiles.Count -ne 0) {
        $total = 0
        $executed = 0
        $passed = 0
        $failed = 0
        $errorCount = 0
        foreach ($trx in $trxFiles) {
            [xml]$trxDocument = Get-Content $trx.FullName -Raw
            $counters = $trxDocument.TestRun.ResultSummary.Counters
            $suiteSummary = [ordered]@{
                File = $trx.Name
                Total = [int]$counters.total
                Executed = [int]$counters.executed
                Passed = [int]$counters.passed
                Failed = [int]$counters.failed
                Error = [int]$counters.error
            }
            $suiteSummaries += $suiteSummary
            $total += $suiteSummary.Total
            $executed += $suiteSummary.Executed
            $passed += $suiteSummary.Passed
            $failed += $suiteSummary.Failed
            $errorCount += $suiteSummary.Error
        }
        $summary = [ordered]@{
            Total = $total
            Executed = $executed
            Passed = $passed
            Failed = $failed
            Error = $errorCount
        }
    }

    [pscustomobject]@{
        SandboxId = $sandboxId
        RunId = $runId
        BuildLabel = $status.BuildLabel
        Status = $status.Status
        ExitCode = $status.ExitCode
        ProcessorAffinityMask = $status.ProcessorAffinityMask
        GuestLogicalProcessorCount = $status.LogicalProcessorCount
        ReusedSandbox = $sandboxReused
        ReusedStagedPayload = $status.ReusedStagedPayload
        PayloadFingerprint = $status.PayloadFingerprint
        ResultsPath = $hostResultRoot
        Tests = $summary
        Suites = $suiteSummaries
    } | ConvertTo-Json -Depth 4

    if ([int]$status.ExitCode -ne 0) {
        exit [int]$status.ExitCode
    }
}
finally {
    if ($null -ne $sandboxId -and -not $KeepSandbox) {
        try {
            Invoke-Wsb -Arguments @('stop', '--id', $sandboxId.ToString(), '--raw') -AllowFailure | Out-Null
        }
        catch {
            Write-Warning "Failed to stop Sandbox $sandboxId`: $($_.Exception.Message)"
        }
    }

    if ($ownsMutex) {
        $mutex.ReleaseMutex()
    }
    $mutex.Dispose()
}