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
File name of the Microsoft.Testing.Platform test executable inside the tests archive.

.PARAMETER Filter
Optional MSTest filter such as Name=..., FullyQualifiedName~..., or TestCategory=....

.PARAMETER SuiteTimeout
Microsoft.Testing.Platform timeout inside the guest. Defaults to two hours; shorten it for focused
module runs or increase it for broader suites.

.PARAMETER TimeoutMinutes
Host controller deadline, including Sandbox startup and payload staging. Defaults to 150 minutes and
must remain larger than the expected guest execution time.

.EXAMPLE
pwsh ./Invoke-SandboxUiTest.ps1 -ExchangeRoot C:\Temp\Sandbox\Peek -TestExecutable Peek.UITests.Next.exe -Filter 'TestCategory=Peek' -InstallWebView2
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path $_ -PathType Container })]
    [string]$ExchangeRoot,

    [Parameter(Mandatory)]
    [string]$TestExecutable,

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
    [ValidateRange(1, 1440)]
    [int]$TimeoutMinutes = 150,
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

    $existingIds = @(Get-SandboxIds)
    if ($existingIds.Count -ne 0) {
        throw "Refusing to overlap existing Windows Sandbox environment(s): $($existingIds -join ', ')"
    }

    New-Item $hostResultRoot -ItemType Directory -Force | Out-Null
    $request = [ordered]@{
        RunId = $runId
        BuildLabel = $BuildLabel
        TestExecutable = $TestExecutable
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
    }
    $request | ConvertTo-Json -Depth 4 | Set-Content $requestPath -Encoding utf8

    Write-Host "Launching Windows Sandbox for run $runId"
    Start-Process explorer.exe -ArgumentList 'shell:AppsFolder\Microsoft.Windows.Containers.Sandbox' | Out-Null

    $startDeadline = [DateTime]::UtcNow.AddMinutes(2)
    do {
        $ids = @(Get-SandboxIds)
        if ($ids.Count -eq 1) {
            $sandboxId = $ids[0]
            break
        }
        [Threading.Thread]::Sleep(500)
    } while ([DateTime]::UtcNow -lt $startDeadline)

    if ($null -eq $sandboxId) {
        $observedIds = @(Get-SandboxIds)
        throw "Windows Sandbox did not publish exactly one environment ID within two minutes. Observed: $($observedIds -join ', ')"
    }

    $loginDeadline = [DateTime]::UtcNow.AddMinutes(2)
    do {
        $probe = Invoke-Wsb -Arguments @(
            'exec', '--id', $sandboxId.ToString(), '--run-as', 'ExistingLogin',
            '--command', 'cmd.exe /d /c exit 0', '--raw'
        ) -AllowFailure
        if ($probe.ExitCode -eq 0) {
            break
        }
        [Threading.Thread]::Sleep(500)
    } while ([DateTime]::UtcNow -lt $loginDeadline)

    if ($probe.ExitCode -ne 0) {
        throw "The Sandbox interactive login did not become ready: $($probe.Output)"
    }

    Invoke-Wsb -Arguments @(
        'share', '--id', $sandboxId.ToString(),
        '--host-path', $ExchangeRoot,
        '--sandbox-path', 'C:\SandboxExchange',
        '--allow-write', '--raw'
    ) | Out-Null

    $guestRequestPath = "C:\SandboxExchange\SandboxResults\$runId\request.json"
    $guestScriptPath = "C:\SandboxExchange\$GuestScript"
    $guestCommand = "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File `"$guestScriptPath`" -RequestPath `"$guestRequestPath`" -Detached"
    Invoke-Wsb -Arguments @(
        'exec', '--id', $sandboxId.ToString(), '--run-as', 'ExistingLogin',
        '--working-directory', 'C:\SandboxExchange',
        '--command', $guestCommand, '--raw'
    ) | Out-Null

    $deadline = [DateTime]::UtcNow.AddMinutes($TimeoutMinutes)
    $lastProgress = $null
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
        [Threading.Thread]::Sleep(500)
    }

    if (-not (Test-Path $statusPath)) {
        throw "Sandbox run exceeded $TimeoutMinutes minute(s). Last progress: $lastProgress"
    }

    $status = Read-SharedJson -Path $statusPath -Attempts 20
    if ($null -eq $status) {
        throw 'status.json remained unreadable after the Sandbox run completed.'
    }

    $trx = Get-ChildItem $hostResultRoot -Recurse -Filter '*.trx' -File | Select-Object -First 1
    $summary = $null
    if ($null -ne $trx) {
        [xml]$trxDocument = Get-Content $trx.FullName -Raw
        $counters = $trxDocument.TestRun.ResultSummary.Counters
        $summary = [ordered]@{
            Total = [int]$counters.total
            Executed = [int]$counters.executed
            Passed = [int]$counters.passed
            Failed = [int]$counters.failed
            Error = [int]$counters.error
        }
    }

    [pscustomobject]@{
        SandboxId = $sandboxId
        RunId = $runId
        BuildLabel = $status.BuildLabel
        Status = $status.Status
        ExitCode = $status.ExitCode
        ResultsPath = $hostResultRoot
        Tests = $summary
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