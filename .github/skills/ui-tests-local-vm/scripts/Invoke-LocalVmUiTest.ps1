# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Runs PowerToys UITest.Next executables in a persistent dockur/windows VM.

.DESCRIPTION
Creates a hash-addressed request, starts or reuses the VM, verifies a non-admin interactive desktop,
dispatches the shared guest runner through Task Scheduler, and returns durable status/TRX evidence.

.EXAMPLE
pwsh ./Invoke-LocalVmUiTest.ps1 `
  -VmRoot X:\PowerToysUiTestVm `
  -ExchangeRoot X:\PowerToysUiTestVm\shared\PowerToysUiTests\Peek `
  -TestExecutable Peek.UITests.Next.exe `
  -Filter 'Name=Peek.Preview.PDF' `
  -BuildLabel (git rev-parse HEAD) `
  -ReuseStagedPayload
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path $_ -PathType Container })]
    [string]$VmRoot,

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
    [string]$WebView2Installer = 'MicrosoftEdgeWebView2RuntimeInstallerX64.exe',
    [string]$SuiteTimeout = '45m',
    [string[]]$CleanupProcess = @(),
    [UInt64]$ProcessorAffinityMask = 3,
    [ValidateRange(0, 300)]
    [int]$OutputHeartbeatSeconds = 15,
    [ValidateRange(0, 7680)]
    [int]$DesktopWidth = 1920,
    [ValidateRange(0, 4320)]
    [int]$DesktopHeight = 1080,
    [ValidateRange(1, 1440)]
    [int]$TimeoutMinutes = 60,
    [ValidateRange(1, 120)]
    [int]$StartupTimeoutMinutes = 45,
    [string]$StandardUser = 'PTUser',
    [string]$GuestShareRoot = '\\host.lan\Data',
    [ValidateRange(1, 65535)]
    [int]$WinRmPort = 15986,
    [string]$CredentialPath = (Join-Path $env:LOCALAPPDATA 'PowerToysUiTestVm\admin.credential.xml'),
    [string]$GuestRunnerSource = (Join-Path $PSScriptRoot '..\templates\run-ui-tests.ps1'),
    [switch]$InstallWebView2,
    [switch]$ReuseStagedPayload,
    [switch]$SkipStart,
    [switch]$StopVmAfterRun,
    [Alias('AllowUnencryptedWinRM')]
    [switch]$UseHttpWinRM,
    [switch]$PlanOnly
)

$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw 'Run this controller with PowerShell 7 (pwsh).'
}
if (($DesktopWidth -eq 0) -ne ($DesktopHeight -eq 0)) {
    throw 'Set both DesktopWidth and DesktopHeight to 0 to disable display validation.'
}
if ($UseHttpWinRM -and $WinRmPort -eq 15986) {
    Write-Warning 'HTTP WinRM was selected with the default HTTPS port. Verify the compose mapping.'
}

$vmRootPath = [IO.Path]::GetFullPath($VmRoot)
$sharedRoot = [IO.Path]::GetFullPath((Join-Path $vmRootPath 'shared'))
$exchangePath = [IO.Path]::GetFullPath($ExchangeRoot)
$guestRunnerSourcePath = [IO.Path]::GetFullPath($GuestRunnerSource)
if (-not (Test-Path $sharedRoot -PathType Container)) {
    throw "VM shared root was not found: $sharedRoot"
}
if (-not (Test-Path $guestRunnerSourcePath -PathType Leaf)) {
    throw "Guest runner was not found: $guestRunnerSourcePath"
}

$relativeExchange = [IO.Path]::GetRelativePath($sharedRoot, $exchangePath)
if ($relativeExchange -eq '..' -or $relativeExchange.StartsWith("..$([IO.Path]::DirectorySeparatorChar)")) {
    throw "ExchangeRoot must be inside the VM shared root '$sharedRoot'."
}
$guestExchangeRoot = if ($relativeExchange -eq '.') {
    $GuestShareRoot.TrimEnd('\')
}
else {
    Join-Path $GuestShareRoot.TrimEnd('\') $relativeExchange
}

function Get-ExchangeFileHash {
    param([string]$File)

    if ([string]::IsNullOrWhiteSpace($File)) {
        return $null
    }
    return (Get-FileHash (Join-Path $exchangePath $File) -Algorithm SHA256).Hash
}

function Read-SharedJson {
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [int]$Attempts = 1
    )

    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        try {
            $text = Get-Content $Path -Raw
            if (-not [string]::IsNullOrWhiteSpace($text)) {
                return $text | ConvertFrom-Json
            }
        }
        catch {
        }
        if ($attempt -lt $Attempts) {
            Start-Sleep -Milliseconds 100
        }
    }
    return $null
}

function Get-TrxSummary {
    param([Parameter(Mandatory)][string]$ResultRoot)

    $suites = @()
    $totals = [ordered]@{ Total = 0; Executed = 0; Passed = 0; Failed = 0; Error = 0 }
    foreach ($trx in Get-ChildItem $ResultRoot -Filter '*.trx' -File -Recurse -ErrorAction SilentlyContinue) {
        [xml]$document = Get-Content $trx.FullName -Raw
        $counters = $document.TestRun.ResultSummary.Counters
        $tests = @($document.TestRun.Results.UnitTestResult | ForEach-Object {
            [pscustomobject]@{
                Name = [string]$_.testName
                Outcome = [string]$_.outcome
                Duration = [string]$_.duration
                ErrorMessage = [string]$_.Output.ErrorInfo.Message
            }
        })
        $suite = [ordered]@{
            File = $trx.FullName
            Total = [int]$counters.total
            Executed = [int]$counters.executed
            Passed = [int]$counters.passed
            Failed = [int]$counters.failed
            Error = [int]$counters.error
            Tests = $tests
        }
        $suites += [pscustomobject]$suite
        foreach ($name in @('Total', 'Executed', 'Passed', 'Failed', 'Error')) {
            $totals[$name] += $suite[$name]
        }
    }
    return [pscustomobject]@{ Totals = [pscustomobject]$totals; Suites = $suites }
}

function Start-InteractiveTask {
    param(
        [Parameter(Mandatory)][System.Management.Automation.Runspaces.PSSession]$Session,
        [Parameter(Mandatory)][string]$TaskName,
        [Parameter(Mandatory)][string]$Executable,
        [Parameter(Mandatory)][string]$Arguments,
        [Parameter(Mandatory)][string]$UserName,
        [Parameter(Mandatory)][int]$ExecutionTimeLimitMinutes
    )

    return Invoke-Command -Session $Session -ScriptBlock {
        param($Name, $FilePath, $ArgumentList, $InteractiveUser, $LimitMinutes)

        $ErrorActionPreference = 'Stop'
        Unregister-ScheduledTask -TaskName $Name -Confirm:$false -ErrorAction SilentlyContinue
        $action = New-ScheduledTaskAction -Execute $FilePath -Argument $ArgumentList
        $principal = New-ScheduledTaskPrincipal `
            -UserId "$env:COMPUTERNAME\$InteractiveUser" `
            -LogonType Interactive -RunLevel Limited
        $settings = New-ScheduledTaskSettingsSet `
            -ExecutionTimeLimit (New-TimeSpan -Minutes $LimitMinutes) `
            -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
        $task = New-ScheduledTask -Action $action -Principal $principal -Settings $settings
        Register-ScheduledTask -TaskName $Name -InputObject $task -Force | Out-Null
        Start-ScheduledTask -TaskName $Name
        $registered = Get-ScheduledTask -TaskName $Name
        [pscustomobject]@{
            TaskName = $Name
            State = [string]$registered.State
            UserId = $registered.Principal.UserId
            RunLevel = [string]$registered.Principal.RunLevel
        }
    } -ArgumentList $TaskName, $Executable, $Arguments, $UserName, $ExecutionTimeLimitMinutes
}

$requiredFiles = @($TestsArchive, $ProductArchive, $WinAppCliArchive, $DotNetArchive)
if (-not [string]::IsNullOrWhiteSpace($ProductOverlayArchive)) {
    $requiredFiles += $ProductOverlayArchive
}
if ($InstallWebView2) {
    $requiredFiles += $WebView2Installer
}
foreach ($file in $requiredFiles) {
    if (-not (Test-Path (Join-Path $exchangePath $file) -PathType Leaf)) {
        throw "Required exchange file is missing: $file"
    }
}

$payloadFiles = @($requiredFiles | Sort-Object -Unique)
$payloadHashes = [ordered]@{
    Tests = Get-ExchangeFileHash -File $TestsArchive
    Product = Get-ExchangeFileHash -File $ProductArchive
    ProductOverlay = Get-ExchangeFileHash -File $ProductOverlayArchive
    WinAppCli = Get-ExchangeFileHash -File $WinAppCliArchive
    DotNet = Get-ExchangeFileHash -File $DotNetArchive
    WebView2Installer = if ($InstallWebView2) { Get-ExchangeFileHash -File $WebView2Installer } else { $null }
}
$fingerprintLines = foreach ($file in $payloadFiles) {
    '{0}={1}' -f $file, (Get-ExchangeFileHash -File $file)
}
$sha256 = [Security.Cryptography.SHA256]::Create()
try {
    $payloadFingerprint = [Convert]::ToHexString(
        $sha256.ComputeHash([Text.Encoding]::UTF8.GetBytes($fingerprintLines -join "`n")))
}
finally {
    $sha256.Dispose()
}

$runId = 'localvm-{0}-{1}' -f [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'), [guid]::NewGuid().ToString('N').Substring(0, 8)
$hostResultRoot = Join-Path $exchangePath "LocalVmResults\$runId"
$guestResultRoot = Join-Path $guestExchangeRoot "LocalVmResults\$runId"
$requestPath = Join-Path $hostResultRoot 'request.json'
$guestRequestPath = Join-Path $guestResultRoot 'request.json'
$statusPath = Join-Path $hostResultRoot 'status.json'
$progressPath = Join-Path $hostResultRoot 'progress.json'
$probeScriptPath = Join-Path $hostResultRoot 'desktop-probe.ps1'
$probePath = Join-Path $hostResultRoot 'desktop-probe.json'
$guestProbeScriptPath = Join-Path $guestResultRoot 'desktop-probe.ps1'
$guestProbePath = Join-Path $guestResultRoot 'desktop-probe.json'
$guestRunnerName = 'run-ui-tests.ps1'
$hostRunnerPath = Join-Path $exchangePath $guestRunnerName
$guestRunnerPath = Join-Path $guestExchangeRoot $guestRunnerName

New-Item $hostResultRoot -ItemType Directory -Force | Out-Null
Copy-Item $guestRunnerSourcePath $hostRunnerPath -Force

$request = [ordered]@{
    RunId = $runId
    Controller = 'dockur/windows local VM'
    ExchangeRoot = $guestExchangeRoot
    BuildLabel = $BuildLabel
    TestExecutables = @($TestExecutable)
    Filter = $Filter
    Platform = $Platform
    SuiteTimeout = $SuiteTimeout
    ProcessorAffinityMask = $ProcessorAffinityMask
    OutputHeartbeatSeconds = $OutputHeartbeatSeconds
    DesktopWidth = $DesktopWidth
    DesktopHeight = $DesktopHeight
    ReuseStagedPayload = [bool]$ReuseStagedPayload
    PayloadFingerprint = $payloadFingerprint
    PayloadFiles = $payloadFiles
    PayloadHashes = $payloadHashes
    Archives = [ordered]@{
        Tests = $TestsArchive
        Product = $ProductArchive
        WinAppCli = $WinAppCliArchive
        DotNet = $DotNetArchive
        ProductOverlay = $ProductOverlayArchive
    }
    WebView2Installer = if ($InstallWebView2) { $WebView2Installer } else { $null }
    CleanupProcesses = @($CleanupProcess)
}
$request | ConvertTo-Json -Depth 8 | Set-Content $requestPath -Encoding utf8

$plan = [ordered]@{
    Controller = 'dockur/windows local VM'
    RunId = $runId
    VmRoot = $vmRootPath
    ExchangeRoot = $exchangePath
    GuestExchangeRoot = $guestExchangeRoot
    GuestRunnerSource = $guestRunnerSourcePath
    GuestRequestPath = $guestRequestPath
    StandardUser = $StandardUser
    WinRM = if ($UseHttpWinRM) { "http://127.0.0.1:$WinRmPort/wsman" } else { "https://127.0.0.1:$WinRmPort/wsman" }
    ReuseStagedPayload = [bool]$ReuseStagedPayload
    StopVmAfterRun = [bool]$StopVmAfterRun
    PayloadFingerprint = $payloadFingerprint
    PayloadHashes = $payloadHashes
}
$plan | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $hostResultRoot 'controller-plan.json') -Encoding utf8
if ($PlanOnly) {
    [pscustomobject]@{
        Status = 'PLAN'
        RunId = $runId
        ResultsPath = $hostResultRoot
        RequestPath = $requestPath
        PayloadFingerprint = $payloadFingerprint
    } | ConvertTo-Json
    return
}

if (-not (Test-Path $CredentialPath -PathType Leaf)) {
    throw "DPAPI credential file was not found: $CredentialPath"
}
$credential = Import-Clixml $CredentialPath
if ($credential -isnot [System.Management.Automation.PSCredential]) {
    throw "Credential file does not contain a PSCredential: $CredentialPath"
}

$session = $null
$probeTaskName = "PowerToysUiTest-Probe-$runId"
$testTaskName = "PowerToysUiTest-Run-$runId"
$controllerResult = $null
try {
    if (-not $SkipStart) {
        $startScript = Join-Path $vmRootPath 'Start-LocalVm.ps1'
        if (-not (Test-Path $startScript -PathType Leaf)) {
            throw "VM start script was not found: $startScript"
        }
        $startupOutput = & $startScript -WaitForWinRM -TimeoutMinutes $StartupTimeoutMinutes | Out-String
        Write-Verbose $startupOutput
    }

    $scheme = if ($UseHttpWinRM) { 'http' } else { 'https' }
    $connectionUri = "${scheme}://127.0.0.1:$WinRmPort/wsman"
    $authentication = if ($UseHttpWinRM) { 'Negotiate' } else { 'Basic' }
    $sessionOption = if ($UseHttpWinRM) {
        New-PSSessionOption
    }
    else {
        New-PSSessionOption -SkipCACheck -SkipCNCheck -SkipRevocationCheck
    }
    $sessionDeadline = [DateTime]::UtcNow.AddMinutes($StartupTimeoutMinutes)
    do {
        try {
            $session = New-PSSession `
                -ConnectionUri $connectionUri -Authentication $authentication `
                -Credential $credential -SessionOption $sessionOption -ErrorAction Stop
        }
        catch {
            if ([DateTime]::UtcNow -ge $sessionDeadline) {
                throw "Could not establish WinRM at $connectionUri. $($_.Exception.Message)"
            }
            Start-Sleep -Seconds 5
        }
    } while ($null -eq $session)

    $controlIdentity = Invoke-Command -Session $session -ScriptBlock {
        $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
        $principal = [Security.Principal.WindowsPrincipal]$identity
        [pscustomobject]@{
            User = $identity.Name
            IsAdministrator = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
        }
    }
    if (-not $controlIdentity.IsAdministrator) {
        throw "The WinRM control identity '$($controlIdentity.User)' is not an administrator."
    }

    $escapedProbePath = $guestProbePath.Replace("'", "''")
    $escapedExchangeRoot = $guestExchangeRoot.Replace("'", "''")
    $probeScript = @"
`$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
`$principal = [Security.Principal.WindowsPrincipal]`$identity
`$sessionId = (Get-Process -Id `$PID).SessionId
Add-Type -AssemblyName System.Windows.Forms
[ordered]@{
    User = `$identity.Name
    SessionId = `$sessionId
    IsAdministrator = `$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    ExplorerCount = @(Get-Process explorer -ErrorAction SilentlyContinue | Where-Object SessionId -eq `$sessionId).Count
    DesktopWidth = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds.Width
    DesktopHeight = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds.Height
    UncAccessible = Test-Path '$escapedExchangeRoot'
} | ConvertTo-Json | Set-Content '$escapedProbePath' -Encoding utf8
"@
    $probeScript | Set-Content $probeScriptPath -Encoding utf8
    $probeArguments = '-NoLogo -NoProfile -ExecutionPolicy Bypass -File "{0}"' -f $guestProbeScriptPath
    $probeTask = Start-InteractiveTask `
        -Session $session -TaskName $probeTaskName `
        -Executable 'powershell.exe' -Arguments $probeArguments `
        -UserName $StandardUser -ExecutionTimeLimitMinutes 2
    Write-Host "Desktop probe task: $($probeTask.TaskName), user $($probeTask.UserId)"

    $probeDeadline = [DateTime]::UtcNow.AddMinutes(2)
    do {
        $desktopProbe = Read-SharedJson -Path $probePath -Attempts 3
        if ($null -ne $desktopProbe) {
            break
        }
        if ([DateTime]::UtcNow -ge $probeDeadline) {
            $probeTaskInfo = Invoke-Command -Session $session -ScriptBlock {
                param($Name)
                $task = Get-ScheduledTask -TaskName $Name -ErrorAction SilentlyContinue
                $info = Get-ScheduledTaskInfo -TaskName $Name -ErrorAction SilentlyContinue
                [pscustomobject]@{ State = [string]$task.State; LastTaskResult = $info.LastTaskResult }
            } -ArgumentList $probeTaskName
            throw "Interactive desktop probe did not complete. Task state: $($probeTaskInfo.State); result: $($probeTaskInfo.LastTaskResult)."
        }
        Start-Sleep -Seconds 1
    } while ($true)

    if ($desktopProbe.IsAdministrator) {
        throw "Interactive test user '$($desktopProbe.User)' is an administrator."
    }
    if (-not $desktopProbe.User.EndsWith("\$StandardUser", [StringComparison]::OrdinalIgnoreCase)) {
        throw "Interactive task ran as '$($desktopProbe.User)', expected '$StandardUser'."
    }
    if ([int]$desktopProbe.SessionId -le 0 -or [int]$desktopProbe.ExplorerCount -le 0) {
        throw "No interactive Explorer desktop is available for '$StandardUser'."
    }
    if (-not $desktopProbe.UncAccessible) {
        throw "The interactive user cannot access '$guestExchangeRoot'."
    }
    if ($DesktopWidth -ne 0 -and
        ([int]$desktopProbe.DesktopWidth -ne $DesktopWidth -or [int]$desktopProbe.DesktopHeight -ne $DesktopHeight)) {
        throw "Guest desktop is $($desktopProbe.DesktopWidth)x$($desktopProbe.DesktopHeight); expected ${DesktopWidth}x${DesktopHeight}."
    }

    $runnerArguments = '-NoLogo -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File "{0}" -RequestPath "{1}"' -f $guestRunnerPath, $guestRequestPath
    $taskLimitMinutes = [Math]::Min(1440, $TimeoutMinutes + 5)
    $testTask = Start-InteractiveTask `
        -Session $session -TaskName $testTaskName `
        -Executable 'powershell.exe' -Arguments $runnerArguments `
        -UserName $StandardUser -ExecutionTimeLimitMinutes $taskLimitMinutes
    Write-Host "UI-test task: $($testTask.TaskName), user $($testTask.UserId)"

    $deadline = [DateTime]::UtcNow.AddMinutes($TimeoutMinutes)
    $lastProgress = $null
    $status = $null
    do {
        if (Test-Path $progressPath -PathType Leaf) {
            $progress = Read-SharedJson -Path $progressPath -Attempts 3
            if ($null -ne $progress) {
                $progressKey = "$($progress.Stage):$($progress.Detail)"
                if ($progressKey -ne $lastProgress) {
                    Write-Host "[$($progress.Stage)] $($progress.Detail)"
                    $lastProgress = $progressKey
                }
            }
        }
        if (Test-Path $statusPath -PathType Leaf) {
            $candidate = Read-SharedJson -Path $statusPath -Attempts 20
            if ($null -ne $candidate -and $candidate.RunId -eq $runId) {
                $status = $candidate
                break
            }
        }
        if ([DateTime]::UtcNow -ge $deadline) {
            $taskInfo = Invoke-Command -Session $session -ScriptBlock {
                param($Name)
                $task = Get-ScheduledTask -TaskName $Name -ErrorAction SilentlyContinue
                $info = Get-ScheduledTaskInfo -TaskName $Name -ErrorAction SilentlyContinue
                [pscustomobject]@{ State = [string]$task.State; LastTaskResult = $info.LastTaskResult }
            } -ArgumentList $testTaskName
            throw "Local VM UI-test run timed out. Task state: $($taskInfo.State); result: $($taskInfo.LastTaskResult)."
        }
        Start-Sleep -Seconds 1
    } while ($true)

    $trx = Get-TrxSummary -ResultRoot $hostResultRoot
    $controllerResult = [pscustomobject]@{
        Controller = 'dockur/windows local VM'
        RunId = $runId
        BuildLabel = $status.BuildLabel
        Status = $status.Status
        ExitCode = [int]$status.ExitCode
        ResultsPath = $hostResultRoot
        ControlUser = $controlIdentity.User
        GuestUser = $status.User
        GuestSessionId = $status.SessionId
        DesktopWidth = $status.DesktopWidth
        DesktopHeight = $status.DesktopHeight
        ProcessorAffinityMask = $status.ProcessorAffinityMask
        ReusedStagedPayload = $status.ReusedStagedPayload
        RefreshedComponents = $status.RefreshedComponents
        ExportErrors = $status.ExportErrors
        Tests = $trx.Totals
        Suites = $trx.Suites
    }
}
finally {
    if ($null -ne $session) {
        Invoke-Command -Session $session -ScriptBlock {
            param($ProbeTaskName, $TestTaskName)
            foreach ($name in @($ProbeTaskName, $TestTaskName)) {
                Unregister-ScheduledTask -TaskName $name -Confirm:$false -ErrorAction SilentlyContinue
            }
        } -ArgumentList $probeTaskName, $testTaskName -ErrorAction SilentlyContinue
        Remove-PSSession $session
    }
    if ($StopVmAfterRun) {
        $stopScript = Join-Path $vmRootPath 'Stop-LocalVm.ps1'
        if (Test-Path $stopScript -PathType Leaf) {
            & $stopScript | Out-Host
        }
    }
}

if ($null -ne $controllerResult) {
    $controllerResult | ConvertTo-Json -Depth 8
    if ($controllerResult.ExitCode -ne 0) {
        exit $controllerResult.ExitCode
    }
}
