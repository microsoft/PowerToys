# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Runs PowerToys UITest.Next executables in a persistent local Hyper-V VM.

.DESCRIPTION
Creates a hash-addressed request, starts or reuses the guest, verifies a non-admin interactive
desktop, dispatches the shared guest runner through Task Scheduler, and returns durable status/TRX
evidence.

The control channel is PowerShell Direct over VMBus and payloads move with Copy-VMFile, so the guest
needs no listener, no published port, and no network. Hyper-V access is required: either an elevated
shell or membership in the local Hyper-V Administrators group.

.EXAMPLE
pwsh ./Invoke-LocalVmUiTest.ps1 `
  -VmName PowerToysUiTest-Win10 `
  -VmRoot X:\PowerToysUiTestVm `
  -ExchangeRoot X:\PowerToysUiTestVm\shared\PowerToysUiTests\Peek `
  -TestExecutable Peek.UITests.Next.exe `
  -Filter 'Name=Peek.Preview.PDF' `
  -Platform x64Win10 `
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

    [Parameter(Mandatory)]
    [string]$VmName,

    [string]$ConfigurationPath,
    [ValidateSet('Default', 'Constrained')]
    [string]$ResourceProfile = 'Default',
    [string]$Filter,
    # Flows to the guest as the 'platform' environment variable. The framework uses it for visual
    # baseline filenames (VisualAssert) and treats any non-empty value as "running in a pipeline",
    # so it must match the names CI uses or baselines silently fail to resolve.
    [ValidateSet('x64Win10', 'x64Win11', 'ARM64')]
    [string]$Platform = 'x64Win10',
    [string]$BuildLabel = 'local',
    [string]$TestsArchive = 'ui-tests.zip',
    [string]$ProductArchive = 'powertoys-runtime.zip',
    [string]$WinAppCliArchive = 'winappcli.zip',
    [string]$DotNetArchive = 'dotnet-runtime.zip',
    [string]$ProductOverlayArchive,
    [string]$WebView2Installer = 'MicrosoftEdgeWebView2RuntimeInstallerX64.exe',
    [string]$SuiteTimeout = '45m',
    [string[]]$CleanupProcess = @(),
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
    [string]$GuestExchangeRoot = 'C:\PowerToysUiTestExchange',
    [string]$CredentialPath,
    [string]$GuestRunnerSource = (Join-Path $PSScriptRoot '..\templates\run-ui-tests.ps1'),
    [switch]$InstallWebView2,
    [switch]$ReuseStagedPayload,
    [switch]$SkipStart,
    [switch]$StopVmAfterRun,
    [switch]$PlanOnly
)

$ErrorActionPreference = 'Stop'
$executionStateContinuous = [uint32]2147483648
$executionStateSystemRequired = [uint32]2147483649
$scheduledTaskRunningResult = 267009

if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw 'Run this controller with PowerShell 7 (pwsh).'
}
if (($DesktopWidth -eq 0) -ne ($DesktopHeight -eq 0)) {
    throw 'Set both DesktopWidth and DesktopHeight to 0 to disable display validation.'
}
$CleanupProcess = @($CleanupProcess | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })

Import-Module (Join-Path $PSScriptRoot 'LocalVmGuest.psm1') -Force

$controllerName = 'Hyper-V local VM'
$vmRootPath = [IO.Path]::GetFullPath($VmRoot)
if ([string]::IsNullOrWhiteSpace($ConfigurationPath)) {
    $ConfigurationPath = Join-Path $vmRootPath 'vm.config.psd1'
}
$configurationPathResolved = [IO.Path]::GetFullPath($ConfigurationPath)
if (-not (Test-Path $configurationPathResolved -PathType Leaf)) {
    throw "VM configuration was not found: $configurationPathResolved"
}
$vmConfiguration = Import-PowerShellDataFile $configurationPathResolved
if ([string]$vmConfiguration.VmName -ne $VmName) {
    throw "VM configuration '$configurationPathResolved' names '$($vmConfiguration.VmName)', but -VmName is '$VmName'."
}

if ([string]::IsNullOrWhiteSpace($CredentialPath)) {
    $CredentialPath = Join-Path $env:LOCALAPPDATA 'PowerToysUiTestVm\admin.credential.xml'
}

# Hyper-V access, the guest credential, and the guest itself all need a human. Fail on the whole set
# at once so the user gets one actionable instruction instead of three sequential surprises.
if (-not $PlanOnly) {
    $guestAdminUser = [string]$vmConfiguration.AdminUserName

    $hostSetup = Test-LocalVmHostSetup -VmName $VmName -CredentialPath $CredentialPath -AdminUserName $guestAdminUser
    if (-not $hostSetup.IsReady) {
        throw (Get-LocalVmSetupMessage `
            -Status $hostSetup `
            -VmRoot $VmRoot `
            -ConfigPath $configurationPathResolved `
            -CredentialPath $CredentialPath)
    }
}

$exchangePath = [IO.Path]::GetFullPath($ExchangeRoot)
$guestRunnerSourcePath = [IO.Path]::GetFullPath($GuestRunnerSource)
if (-not (Test-Path $guestRunnerSourcePath -PathType Leaf)) {
    throw "Guest runner was not found: $guestRunnerSourcePath"
}

$guestContext = New-LocalVmContext `
    -VmName $VmName -HostExchangeRoot $exchangePath -GuestExchangeRoot $GuestExchangeRoot
$guestExchangeRoot = $guestContext.GuestExchangeRoot

function Get-ExchangeFileHash {
    param([string]$File)

    if ([string]::IsNullOrWhiteSpace($File)) {
        return $null
    }
    return (Get-FileHash (Join-Path $exchangePath $File) -Algorithm SHA256).Hash
}

function Get-TrxSummary {
    param([Parameter(Mandatory)][string]$ResultRoot)

    $suites = @()
    $totals = [ordered]@{ Total = 0; Executed = 0; Passed = 0; Failed = 0; Error = 0; NotExecuted = 0 }
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
            NotExecuted = [int]$counters.notExecuted
            Tests = $tests
        }
        $suites += [pscustomobject]$suite
        foreach ($name in @('Total', 'Executed', 'Passed', 'Failed', 'Error', 'NotExecuted')) {
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

function Get-InteractiveTaskInfo {
    param(
        [Parameter(Mandatory)][System.Management.Automation.Runspaces.PSSession]$Session,
        [Parameter(Mandatory)][string]$TaskName,
        [ValidateRange(1, 10)][int]$Attempts = 3
    )

    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        try {
            return Invoke-Command -Session $Session -ScriptBlock {
                param($Name)

                $task = Get-ScheduledTask -TaskName $Name -ErrorAction SilentlyContinue
                $info = Get-ScheduledTaskInfo -TaskName $Name -ErrorAction SilentlyContinue
                $state = if ($null -ne $task) { [string]$task.State } else { 'Missing' }
                $lastTaskResult = if ($null -ne $info) { [int]$info.LastTaskResult } else { $null }
                $lastRunTimeUtc = if ($null -ne $info -and $info.LastRunTime.Year -gt 2000) {
                    $info.LastRunTime.ToUniversalTime().ToString('o')
                }
                else {
                    $null
                }
                [pscustomobject]@{
                    State = $state
                    LastTaskResult = $lastTaskResult
                    LastRunTimeUtc = $lastRunTimeUtc
                }
            } -ArgumentList $TaskName -ErrorAction Stop
        }
        catch {
            if ($attempt -eq $Attempts) {
                throw
            }
            Start-Sleep -Milliseconds 250
        }
    }
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
$resultRelative = "LocalVmResults\$runId"
$hostResultRoot = Join-Path $exchangePath $resultRelative
$guestResultRoot = Join-Path $guestExchangeRoot $resultRelative
$requestRelative = Join-Path $resultRelative 'request.json'
$statusRelative = Join-Path $resultRelative 'status.json'
$progressRelative = Join-Path $resultRelative 'progress.json'
$probeScriptRelative = Join-Path $resultRelative 'desktop-probe.ps1'
$probeRelative = Join-Path $resultRelative 'desktop-probe.json'
$requestPath = Join-Path $hostResultRoot 'request.json'
$guestRequestPath = Join-Path $guestResultRoot 'request.json'
$guestProbeScriptPath = Join-Path $guestResultRoot 'desktop-probe.ps1'
$guestProbePath = Join-Path $guestResultRoot 'desktop-probe.json'
$guestRunnerName = 'run-ui-tests.ps1'
$hostRunnerPath = Join-Path $exchangePath $guestRunnerName
$guestRunnerPath = Join-Path $guestExchangeRoot $guestRunnerName

New-Item $hostResultRoot -ItemType Directory -Force | Out-Null
Copy-Item $guestRunnerSourcePath $hostRunnerPath -Force

$request = [ordered]@{
    RunId = $runId
    Controller = $controllerName
    ExchangeRoot = $guestExchangeRoot
    BuildLabel = $BuildLabel
    TestExecutables = @($TestExecutable)
    Filter = $Filter
    Platform = $Platform
    ResourceProfile = $ResourceProfile
    SuiteTimeout = $SuiteTimeout
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
$requestJson = $request | ConvertTo-Json -Depth 8
$requestJson | Set-Content $requestPath -Encoding utf8

$plan = [ordered]@{
    Controller = $controllerName
    RunId = $runId
    VmRoot = $vmRootPath
    VmName = $guestContext.VmName
    ConfigurationPath = $configurationPathResolved
    ResourceProfile = $ResourceProfile
    ExchangeRoot = $exchangePath
    GuestExchangeRoot = $guestExchangeRoot
    GuestRunnerSource = $guestRunnerSourcePath
    GuestRequestPath = $guestRequestPath
    StandardUser = $StandardUser
    ControlChannel = $guestContext.ControlChannel
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
$evidenceExported = $false
$probeTaskName = "PowerToysUiTest-Probe-$runId"
$testTaskName = "PowerToysUiTest-Run-$runId"
$controllerResult = $null
$executionStateSet = $false
try {
    try {
        if (-not ('PowerToys.UiTests.LocalVm.NativeMethods' -as [type])) {
            Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;

namespace PowerToys.UiTests.LocalVm
{
    public static class NativeMethods
    {
        [DllImport("kernel32.dll")]
        public static extern uint SetThreadExecutionState(uint executionState);
    }
}
'@
        }

        $executionStateSet = [PowerToys.UiTests.LocalVm.NativeMethods]::SetThreadExecutionState($executionStateSystemRequired) -ne 0
        if ($executionStateSet) {
            Write-Host 'Host automatic sleep prevention: active'
        }
        else {
            Write-Warning 'Host automatic sleep prevention was rejected; the local VM run will continue.'
        }
    }
    catch {
        Write-Warning "Host automatic sleep prevention is unavailable; the local VM run will continue: $($_.Exception.Message)"
    }

    if (-not $SkipStart) {
        $startScript = Join-Path $vmRootPath 'Start-LocalVm.ps1'
        if (-not (Test-Path $startScript -PathType Leaf)) {
            throw "VM start script was not found: $startScript"
        }
        $startupOutput = & $startScript `
            -ConfigPath $configurationPathResolved `
            -CredentialPath $CredentialPath `
            -ResourceProfile $ResourceProfile `
            -Wait `
            -TimeoutMinutes $StartupTimeoutMinutes | Out-String
        Write-Verbose $startupOutput
    }

    $session = New-LocalVmSession `
        -Context $guestContext -Credential $credential -TimeoutMinutes $StartupTimeoutMinutes

    $guestWindowsVersion = Invoke-Command -Session $session -ScriptBlock {
        $currentVersion = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
        [pscustomobject]@{
            Build = [int]$currentVersion.CurrentBuild
            Ubr = [int]$currentVersion.UBR
            Display = "$($currentVersion.CurrentBuild).$($currentVersion.UBR)"
        }
    }
    if ($guestWindowsVersion.Build -lt 22000 -and
        ($guestWindowsVersion.Build -lt 19041 -or $guestWindowsVersion.Build -gt 19045 -or $guestWindowsVersion.Ubr -lt 5007)) {
        throw "BLOCKED: '$VmName' is Windows $($guestWindowsVersion.Display). .NET 10 and PowerShell 7.6 require Windows 10 1904x.5007 or newer for CET. Recreate through Initialize-LocalVmHost.ps1 (Setup Dynamic Update) or run Update-LocalVmGuest.ps1."
    }

    $vcArchitecture = if ([string]$vmConfiguration.ProcessorArchitecture -eq 'arm64') { 'arm64' } else { 'x64' }
    $vcRuntimePresent = Invoke-Command -Session $session -ScriptBlock {
        Test-Path "$env:WINDIR\System32\VCRUNTIME140.dll" -PathType Leaf
    }
    if (-not $vcRuntimePresent) {
        $vcRedistPath = Join-Path $vmRootPath "oem\vc_redist.$vcArchitecture.exe"
        if (-not (Test-Path $vcRedistPath -PathType Leaf)) {
            throw "BLOCKED: '$VmName' cannot record failure video because VCRUNTIME140.dll is missing, and no repair payload exists at '$vcRedistPath'. Run Initialize-LocalVmHost.ps1 for this VM config."
        }

        $vcSignature = Get-AuthenticodeSignature $vcRedistPath
        if ($vcSignature.Status -ne 'Valid' -or $vcSignature.SignerCertificate.Subject -notlike 'CN=Microsoft Corporation*') {
            throw "BLOCKED: refusing untrusted VC++ redistributable '$vcRedistPath' (status=$($vcSignature.Status))."
        }

        Invoke-Command -Session $session -ScriptBlock {
            New-Item C:\PowerToysUiTestTools -ItemType Directory -Force | Out-Null
        }
        $guestVcRedist = 'C:\PowerToysUiTestTools\vc_redist.exe'
        Copy-Item $vcRedistPath -Destination $guestVcRedist -ToSession $session -Force
        $vcExitCode = Invoke-Command -Session $session -ScriptBlock {
            param($Installer)
            (Start-Process $Installer -ArgumentList '/install', '/quiet', '/norestart' -Wait -PassThru).ExitCode
        } -ArgumentList $guestVcRedist
        if ($vcExitCode -notin 0, 1638, 3010 -or -not (Invoke-Command -Session $session -ScriptBlock { Test-Path "$env:WINDIR\System32\VCRUNTIME140.dll" })) {
            throw "BLOCKED: Visual C++ redistributable installation failed in '$VmName' (exit=$vcExitCode)."
        }
        Write-Host "Installed the Visual C++ runtime in '$VmName' for failure-video capture."
    }

    $guestPowerShell = 'C:\Program Files\PowerShell\7\pwsh.exe'
    $powerShellPresent = Invoke-Command -Session $session -ScriptBlock {
        param($Path)
        Test-Path $Path -PathType Leaf
    } -ArgumentList $guestPowerShell
    if (-not $powerShellPresent) {
        $powerShellMsi = Get-ChildItem (Join-Path $vmRootPath 'oem') `
            -Filter "PowerShell-*-win-$vcArchitecture.msi" -File |
            Where-Object { Test-Path "$($_.FullName).sha256" -PathType Leaf } |
            Sort-Object Name -Descending | Select-Object -First 1
        if ($null -eq $powerShellMsi) {
            throw "BLOCKED: PowerShell 7 is missing in '$VmName', and no verified repair MSI exists under '$(Join-Path $vmRootPath 'oem')'. Run Initialize-LocalVmHost.ps1 for this VM config."
        }
        $expectedPowerShellHash = (Get-Content "$($powerShellMsi.FullName).sha256" -Raw).Trim()
        $actualPowerShellHash = (Get-FileHash $powerShellMsi.FullName -Algorithm SHA256).Hash
        $powerShellSignature = Get-AuthenticodeSignature $powerShellMsi.FullName
        if ($actualPowerShellHash -ne $expectedPowerShellHash -or
            $powerShellSignature.Status -ne 'Valid' -or
            $powerShellSignature.SignerCertificate.Subject -notlike 'CN=Microsoft Corporation*') {
            throw "BLOCKED: refusing unverified PowerShell MSI '$($powerShellMsi.FullName)' (sha256=$actualPowerShellHash, expected=$expectedPowerShellHash, status=$($powerShellSignature.Status))."
        }

        Invoke-Command -Session $session -ScriptBlock {
            New-Item C:\PowerToysUiTestTools -ItemType Directory -Force | Out-Null
        }
        $guestPowerShellMsi = 'C:\PowerToysUiTestTools\PowerShell.msi'
        Copy-Item $powerShellMsi.FullName -Destination $guestPowerShellMsi -ToSession $session -Force
        $powerShellExitCode = Invoke-Command -Session $session -ScriptBlock {
            param($Installer)
            (Start-Process msiexec.exe -ArgumentList @(
                '/i', $Installer, '/qn', '/norestart',
                'ADD_PATH=1', 'REGISTER_MANIFEST=1',
                'ENABLE_PSREMOTING=0', 'USE_MU=0', 'ENABLE_MU=0') -Wait -PassThru).ExitCode
        } -ArgumentList $guestPowerShellMsi
        if ($powerShellExitCode -notin 0, 1638, 3010 -or -not (Invoke-Command -Session $session -ScriptBlock {
                param($Path)
                Test-Path $Path -PathType Leaf
            } -ArgumentList $guestPowerShell)) {
            throw "BLOCKED: PowerShell 7 installation failed in '$VmName' (exit=$powerShellExitCode)."
        }
        Write-Host "Installed PowerShell 7 in '$VmName' for guest-side test orchestration."
    }

    Initialize-GuestExchange -Context $guestContext -Session $session -StandardUser $StandardUser
    $stagedFiles = @(Copy-ToGuest `
        -Context $guestContext -Session $session -FileName (@($guestRunnerName) + $payloadFiles))
    if ($stagedFiles.Count -gt 0) {
        Write-Host "Staged into the guest: $($stagedFiles -join ', ')"
    }
    Write-GuestText `
        -Context $guestContext -Session $session -RelativePath $requestRelative -Value $requestJson

    $controlIdentity = Invoke-Command -Session $session -ScriptBlock {
        $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
        $principal = [Security.Principal.WindowsPrincipal]$identity
        $windowsApplicationId = '55c92734-d682-4d71-983e-d6ec3f16059f'
        $windowsLicense = Get-CimInstance SoftwareLicensingProduct -ErrorAction SilentlyContinue |
            Where-Object {
                $_.ApplicationID -eq $windowsApplicationId -and
                -not [string]::IsNullOrWhiteSpace($_.PartialProductKey) -and
                $_.Name -like 'Windows*'
            } |
            Select-Object -First 1
        [pscustomobject]@{
            User = $identity.Name
            IsAdministrator = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
            WindowsLicenseDescription = [string]$windowsLicense.Description
            WindowsLicenseStatus = [int]$windowsLicense.LicenseStatus
            WindowsGracePeriodMinutes = [int]$windowsLicense.GracePeriodRemaining
        }
    }
    if (-not $controlIdentity.IsAdministrator) {
        throw "The control identity '$($controlIdentity.User)' is not an administrator."
    }
    if ($controlIdentity.WindowsLicenseDescription.Contains('TIMEBASED_EVAL', [StringComparison]::OrdinalIgnoreCase) -and
        ([int]$controlIdentity.WindowsLicenseStatus -eq 5 -or [int]$controlIdentity.WindowsGracePeriodMinutes -le 0)) {
        throw 'The Windows evaluation period has expired. The guest will shut down hourly; replace it with current evaluation media or a properly licensed baseline before running UI tests.'
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
    ExchangeAccessible = Test-Path '$escapedExchangeRoot'
} | ConvertTo-Json | Set-Content '$escapedProbePath' -Encoding utf8
"@
    Write-GuestText `
        -Context $guestContext -Session $session -RelativePath $probeScriptRelative -Value $probeScript
    $probeArguments = '-NoLogo -NoProfile -ExecutionPolicy Bypass -File "{0}"' -f $guestProbeScriptPath
    $probeTask = Start-InteractiveTask `
        -Session $session -TaskName $probeTaskName `
        -Executable $guestPowerShell -Arguments $probeArguments `
        -UserName $StandardUser -ExecutionTimeLimitMinutes 2
    Write-Host "Desktop probe task: $($probeTask.TaskName), user $($probeTask.UserId)"

    $probeDeadline = [DateTime]::UtcNow.AddMinutes(2)
    do {
        $desktopProbe = Read-GuestJson `
            -Context $guestContext -Session $session -RelativePath $probeRelative -Attempts 3
        if ($null -ne $desktopProbe) {
            break
        }
        if ([DateTime]::UtcNow -ge $probeDeadline) {
            $probeTaskInfo = Get-InteractiveTaskInfo -Session $session -TaskName $probeTaskName
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
    if (-not $desktopProbe.ExchangeAccessible) {
        throw "The interactive user cannot access '$guestExchangeRoot'."
    }
    if ($DesktopWidth -ne 0 -and
        ([int]$desktopProbe.DesktopWidth -ne $DesktopWidth -or [int]$desktopProbe.DesktopHeight -ne $DesktopHeight)) {
        throw "Guest desktop is $($desktopProbe.DesktopWidth)x$($desktopProbe.DesktopHeight); expected ${DesktopWidth}x${DesktopHeight}."
    }

    $runnerArguments = '-NoLogo -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File "{0}" -RequestPath "{1}"' -f $guestRunnerPath, $guestRequestPath
    $taskLimitMinutes = [Math]::Min(1440, $TimeoutMinutes + 5)
    $taskDispatchedUtc = [DateTime]::UtcNow
    $testTask = Start-InteractiveTask `
        -Session $session -TaskName $testTaskName `
        -Executable $guestPowerShell -Arguments $runnerArguments `
        -UserName $StandardUser -ExecutionTimeLimitMinutes $taskLimitMinutes
    Write-Host "UI-test task: $($testTask.TaskName), user $($testTask.UserId)"

    $deadline = [DateTime]::UtcNow.AddMinutes($TimeoutMinutes)
    $taskStartDeadline = $taskDispatchedUtc.AddSeconds(30)
    $nextTaskCheckUtc = $taskDispatchedUtc.AddSeconds(5)
    $lastProgress = $null
    $status = $null
    do {
        $progress = Read-GuestJson `
            -Context $guestContext -Session $session -RelativePath $progressRelative
        if ($null -ne $progress) {
            $progressKey = "$($progress.Stage):$($progress.Detail)"
            if ($progressKey -ne $lastProgress) {
                Write-Host "[$($progress.Stage)] $($progress.Detail)"
                $lastProgress = $progressKey
            }
        }
        $candidate = Read-GuestJson `
            -Context $guestContext -Session $session -RelativePath $statusRelative
        if ($null -ne $candidate -and $candidate.RunId -eq $runId) {
            $status = $candidate
            break
        }
        if ([DateTime]::UtcNow -ge $nextTaskCheckUtc) {
            $taskInfo = Get-InteractiveTaskInfo -Session $session -TaskName $testTaskName
            $nextTaskCheckUtc = [DateTime]::UtcNow.AddSeconds(5)
            $taskIsActive = $taskInfo.State -in @('Running', 'Queued') -or $taskInfo.LastTaskResult -eq $scheduledTaskRunningResult
            if (-not $taskIsActive -and
                [string]::IsNullOrWhiteSpace($taskInfo.LastRunTimeUtc) -and
                [DateTime]::UtcNow -ge $taskStartDeadline) {
                throw "Local VM UI-test task did not start within 30 seconds. Task state: $($taskInfo.State); result: $($taskInfo.LastTaskResult)."
            }
            if (-not $taskIsActive -and -not [string]::IsNullOrWhiteSpace($taskInfo.LastRunTimeUtc)) {
                $candidate = Read-GuestJson `
                    -Context $guestContext -Session $session -RelativePath $statusRelative -Attempts 20
                if ($null -ne $candidate -and $candidate.RunId -eq $runId) {
                    $status = $candidate
                    break
                }
                throw "Local VM UI-test task ended without matching status.json. Task state: $($taskInfo.State); result: $($taskInfo.LastTaskResult); last run UTC: $($taskInfo.LastRunTimeUtc)."
            }
        }
        if ([DateTime]::UtcNow -ge $deadline) {
            # Absorb a status file that was still being created when the deadline elapsed.
            $candidate = Read-GuestJson `
                -Context $guestContext -Session $session -RelativePath $statusRelative -Attempts 20
            if ($null -ne $candidate -and $candidate.RunId -eq $runId) {
                $status = $candidate
                break
            }
            $taskInfo = Get-InteractiveTaskInfo -Session $session -TaskName $testTaskName
            throw "Local VM UI-test run timed out. Task state: $($taskInfo.State); result: $($taskInfo.LastTaskResult)."
        }
        Start-Sleep -Seconds 1
    } while ($true)

    Copy-FromGuest -Context $guestContext -Session $session -RelativePath $resultRelative
    $evidenceExported = $true

    $trx = Get-TrxSummary -ResultRoot $hostResultRoot
    $nonPassingTests = @($trx.Suites | ForEach-Object { $_.Tests } | Where-Object { $_.Outcome -ne 'Passed' })
    $effectiveExitCode = [int]$status.ExitCode
    $effectiveStatus = [string]$status.Status
    if ($trx.Suites.Count -eq 0 -or
        $trx.Totals.Total -eq 0 -or
        $trx.Totals.Executed -ne $trx.Totals.Total -or
        $nonPassingTests.Count -gt 0) {
        $effectiveStatus = 'FAIL'
        if ($effectiveExitCode -eq 0) {
            $effectiveExitCode = 1
        }
    }
    $controllerResult = [pscustomobject]@{
        Controller = $controllerName
        HostSleepPrevented = $executionStateSet
        RunId = $runId
        BuildLabel = $status.BuildLabel
        Status = $effectiveStatus
        ExitCode = $effectiveExitCode
        ResultsPath = $hostResultRoot
        ControlUser = $controlIdentity.User
        GuestUser = $status.User
        GuestSessionId = $status.SessionId
        DesktopWidth = $status.DesktopWidth
        DesktopHeight = $status.DesktopHeight
        ResourceProfile = $ResourceProfile
        ReusedStagedPayload = $status.ReusedStagedPayload
        RefreshedComponents = $status.RefreshedComponents
        ExportErrors = $status.ExportErrors
        Tests = $trx.Totals
        Failed = @($nonPassingTests | ForEach-Object {
            [pscustomobject]@{
                Name = $_.Name
                Outcome = $_.Outcome
                Duration = $_.Duration
                Error = (($_.ErrorMessage -split "`n") | Select-Object -First 1)
            }
        })
        Suites = $trx.Suites
    }
}
finally {
    if ($null -ne $session) {
        if ($session.State -eq 'Opened') {
            if (-not $evidenceExported) {
                try {
                    Copy-FromGuest -Context $guestContext -Session $session -RelativePath $resultRelative
                    $evidenceExported = $true
                }
                catch {
                    Write-Warning "Guest evidence could not be exported: $($_.Exception.Message)"
                }
            }
            if ($evidenceExported) {
                try {
                    Remove-GuestItem -Context $guestContext -Session $session -RelativePath $resultRelative
                }
                catch {
                    Write-Warning "The guest run folder could not be removed: $($_.Exception.Message)"
                }
            }
        }
        try {
            if ($session.State -eq 'Opened') {
                Invoke-Command -Session $session -ScriptBlock {
                    param($ProbeTaskName, $TestTaskName)
                    foreach ($name in @($ProbeTaskName, $TestTaskName)) {
                        Unregister-ScheduledTask -TaskName $name -Confirm:$false -ErrorAction SilentlyContinue
                    }
                } -ArgumentList $probeTaskName, $testTaskName -ErrorAction Stop
            }
        }
        catch {
            Write-Warning "Scheduled-task cleanup was skipped because the guest session was unavailable: $($_.Exception.Message)"
        }
        finally {
            Remove-PSSession $session -ErrorAction SilentlyContinue
        }
    }
    if ($StopVmAfterRun) {
        $stopScript = Join-Path $vmRootPath 'Stop-LocalVm.ps1'
        if (Test-Path $stopScript -PathType Leaf) {
            try {
                & $stopScript `
                    -ConfigPath $configurationPathResolved `
                    -CredentialPath $CredentialPath | Out-Host
            }
            catch {
                Write-Warning "The requested post-run VM stop failed: $($_.Exception.Message)"
            }
        }
    }
    if ($executionStateSet) {
        try {
            $null = [PowerToys.UiTests.LocalVm.NativeMethods]::SetThreadExecutionState($executionStateContinuous)
        }
        catch {
            Write-Warning "Host sleep-prevention cleanup failed: $($_.Exception.Message)"
        }
    }
}

if ($null -ne $controllerResult) {
    $controllerResult | ConvertTo-Json -Depth 8
    if ($controllerResult.ExitCode -ne 0) {
        exit $controllerResult.ExitCode
    }
}
