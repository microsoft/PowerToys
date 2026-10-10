# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Runs the autonomous MWB MSTest executable in an existing nested-Sandbox VM.
.DESCRIPTION
Uses the local-VM skill for standard-user test dispatch, payloads and evidence.
A protected, noninteractive provisioning task creates only the test's scoped
firewall rule when Sandbox creates its inner switch. Cleanup always removes it.
The VM must already have nested virtualization and Sandbox enabled.
#>
[CmdletBinding()]
param(
    [string]$VmRoot = 'C:\PowerToysUiTestVm',
    [string]$VmName = 'PowerToysUiTest-Win10',
    [string]$ConfigurationPath = 'C:\PowerToysUiTestVm\vm.config.win10.psd1',
    [Parameter(Mandatory = $true)][string]$ExchangeRoot,
    [string]$BuildLabel = 'local-mwb-release',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$Filter,
    [string]$StandardUser = 'PTUser',
    [string]$GuestRuntimeArchive = 'mwb-guest-runtime.zip',
    [string]$ProductArchive = 'powertoys-runtime.zip',
    [string]$TestsArchive = 'ui-tests.zip',
    [ValidateSet('x64Win10', 'x64Win11')][string]$Platform = 'x64Win10',
    [ValidateSet('Auto', 'Legacy', 'WinApp')][string]$SandboxBackend = 'Auto',
    [string]$SandboxWinAppArchive,
    [string]$CredentialPath = (Join-Path $env:LOCALAPPDATA 'PowerToysUiTestVm\admin.credential.xml'),
    [switch]$ReuseStagedPayload,
    [switch]$PlanOnly
)

$ErrorActionPreference = 'Stop'

function ConvertTo-MwbHostPowerEvent {
    param($Event, [DateTime]$StartedUtc, [DateTime]$CompletedUtc)
    $StartedUtc = $StartedUtc.ToUniversalTime()
    $CompletedUtc = $CompletedUtc.ToUniversalTime()
    [xml]$document = $Event.ToXml()
    $sleepValue = @($document.Event.EventData.Data | Where-Object Name -CEQ 'SleepTime')
    $wakeValue = @($document.Event.EventData.Data | Where-Object Name -CEQ 'WakeTime')
    if ($sleepValue.Count -ne 1 -or $wakeValue.Count -ne 1) {
        throw 'The host power event has no unambiguous sleep/wake timing.'
    }
    $sleep = [DateTime]::MinValue
    $wake = [DateTime]::MinValue
    if (-not [DateTime]::TryParse($sleepValue[0].InnerText, [Globalization.CultureInfo]::InvariantCulture,
            [Globalization.DateTimeStyles]::RoundtripKind, [ref]$sleep) -or
        -not [DateTime]::TryParse($wakeValue[0].InnerText, [Globalization.CultureInfo]::InvariantCulture,
            [Globalization.DateTimeStyles]::RoundtripKind, [ref]$wake)) {
        throw 'The host power event has invalid sleep/wake timing.'
    }
    $sleep = $sleep.ToUniversalTime()
    $wake = $wake.ToUniversalTime()
    if ($wake -lt $sleep) { throw 'The host power event has reversed sleep/wake timing.' }
    $overlapStart = if ($sleep -gt $StartedUtc) { $sleep } else { $StartedUtc }
    $overlapEnd = if ($wake -lt $CompletedUtc) { $wake } else { $CompletedUtc }
    [ordered]@{
        RecordId = [long]$Event.RecordId
        SleepUtc = $sleep.ToString('o')
        WakeUtc = $wake.ToString('o')
        SuspensionMilliseconds = ($wake - $sleep).TotalMilliseconds
        RunOverlapMilliseconds = [Math]::Max(0, ($overlapEnd - $overlapStart).TotalMilliseconds)
    }
}

function Get-MwbHostPowerEvidence {
    param([DateTime]$StartedUtc, [DateTime]$CompletedUtc)
    $StartedUtc = $StartedUtc.ToUniversalTime()
    $CompletedUtc = $CompletedUtc.ToUniversalTime()
    $report = [ordered]@{
        QueryStatus = 'NotChecked'; QueryErrorHResult = $null
        OverlappedSuspension = $null; EventLimitReached = $false; Events = @()
    }
    try {
        $events = @(Get-WinEvent -FilterHashtable @{
            LogName = 'System'; ProviderName = 'Microsoft-Windows-Power-Troubleshooter'; Id = 1
            StartTime = $StartedUtc.ToLocalTime(); EndTime = $CompletedUtc.ToLocalTime()
        } -MaxEvents 8 -ErrorAction Stop)
        $report.Events = @($events | ForEach-Object { ConvertTo-MwbHostPowerEvent $_ $StartedUtc $CompletedUtc })
        $report.EventLimitReached = $events.Count -eq 8
        $report.OverlappedSuspension = @($report.Events | Where-Object RunOverlapMilliseconds -GT 0).Count -gt 0
        $report.QueryStatus = 'Succeeded'
    }
    catch {
        if ($_.FullyQualifiedErrorId -like 'NoMatchingEventsFound,*') {
            $report.QueryStatus = 'Succeeded'
            $report.OverlappedSuspension = $false
        }
        else {
            $report.QueryStatus = 'Failed'
            $report.QueryErrorHResult = '0x{0:X8}' -f ($_.Exception.HResult -band 0xffffffffL)
            Write-Warning "MWB host power diagnostics unavailable ($($report.QueryErrorHResult)); no absence conclusion was made."
        }
    }
    $report
}

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..\..'))
$skillRoot = Join-Path $repoRoot '.github\skills\ui-tests-local-vm\scripts'
$controller = Join-Path $skillRoot 'Invoke-LocalVmUiTest.ps1'
$backend = if ($SandboxBackend -eq 'Auto') {
    if ($Platform -eq 'x64Win11') { 'WinApp' } else { 'Legacy' }
} else { $SandboxBackend }
if ($backend -eq 'WinApp' -and
    ([string]::IsNullOrWhiteSpace($SandboxWinAppArchive) -or
        -not (Test-Path -LiteralPath (Join-Path $ExchangeRoot $SandboxWinAppArchive) -PathType Leaf))) {
    throw 'Stage the official winappcli runtime archive and pass -SandboxWinAppArchive for the WinApp backend.'
}
$parameters = @{
    VmRoot = $VmRoot; VmName = $VmName; ConfigurationPath = $ConfigurationPath
    ExchangeRoot = $ExchangeRoot; TestExecutable = 'MouseWithoutBorders.UITests.exe'
    Platform = $Platform; BuildLabel = $BuildLabel; StandardUser = $StandardUser
    CredentialPath = $CredentialPath
    ProductArchive = $ProductArchive; TestsArchive = $TestsArchive
    SuiteTimeout = $(if ($backend -eq 'WinApp') { '75m' } else { '45m' })
    TimeoutMinutes = $(if ($backend -eq 'WinApp') { 90 } else { 60 })
    StartupTimeoutMinutes = 15
    ReuseStagedPayload = $ReuseStagedPayload
}
if ($Filter) { $parameters.Filter = $Filter }
if ($PlanOnly) {
    $plan = (& $controller @parameters -PlanOnly | Out-String) | ConvertFrom-Json
    $plan | Add-Member -NotePropertyName SandboxBackend -NotePropertyValue $backend
    $plan | Add-Member -NotePropertyName Configuration -NotePropertyValue $Configuration
    if ($backend -eq 'WinApp') {
        $sandboxCliArchive = Join-Path $ExchangeRoot $SandboxWinAppArchive
        $plan | Add-Member -NotePropertyName SandboxWinAppArchive -NotePropertyValue $SandboxWinAppArchive
        $plan | Add-Member -NotePropertyName SandboxWinAppArchiveSha256 -NotePropertyValue (Get-FileHash -LiteralPath $sandboxCliArchive -Algorithm SHA256).Hash
    }
    $plan | ConvertTo-Json -Depth 10
    return
}
$controllerStartedUtc = [DateTime]::UtcNow
$controllerTimer = [Diagnostics.Stopwatch]::StartNew()
& (Join-Path $skillRoot 'Initialize-LocalVmHost.ps1') -VmRoot $VmRoot -ConfigPath $ConfigurationPath -CredentialPath $CredentialPath -CheckOnly
if ($LASTEXITCODE -ne 0) { throw 'Local-VM host readiness failed.' }
& (Join-Path $VmRoot 'Start-LocalVm.ps1') -ConfigPath $ConfigurationPath -CredentialPath $CredentialPath -Wait -TimeoutMinutes 15
$runId = [Guid]::NewGuid().ToString()
$taskName = "PowerToys-Mwb-Provision-$runId"
$guestTools = "C:\ProgramData\PowerToysMwbProvisioner\$runId"
$credential = Import-Clixml $CredentialPath
$session = New-PSSession -VMName $VmName -Credential $credential
$taskRegistered = $false
$provisioned = $false
$result = $null
try {
    # The protected marker fingerprints these exact files before the standard-user
    # controller extracts the same archive into the same product path.
    $archive = Join-Path $ExchangeRoot $ProductArchive
    $guestArchive = "C:\PowerToysUiTestExchange\MwbProvisioning\$runId\powertoys-runtime.zip"
    $archiveHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
    $certificateHash = $null
    $zip = [IO.Compression.ZipFile]::OpenRead($archive)
    try {
        $entry = $zip.GetEntry('mwb-test-signer.cer')
        if ($null -eq $entry) { $entry = $zip.GetEntry('./mwb-test-signer.cer') }
        if ($null -ne $entry) {
            $stream = $entry.Open()
            try { $certificateHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) }
            finally { $stream.Dispose() }
        }
    }
    finally { $zip.Dispose() }
    $reuseProduct = $false
    if ($ReuseStagedPayload) {
        $reuseProduct = Invoke-Command -Session $session -ScriptBlock {
            param($ExpectedHash)
            $manifest = 'C:\PowerToysUiTestRun\staging-manifest.json'
            if (-not (Test-Path -LiteralPath $manifest)) { return $false }
            $previous = [IO.File]::ReadAllText($manifest) | ConvertFrom-Json
            return $previous.PayloadHashes.Product -eq $ExpectedHash -and
                (Test-Path C:\PowerToysUiTestRun\PowerToys\PowerToys.MouseWithoutBorders.dll)
        } -ArgumentList $archiveHash
    }
    if (-not $reuseProduct) {
        Copy-VMFile -Name $VmName -SourcePath $archive -DestinationPath $guestArchive `
            -FileSource Host -CreateFullPath -Force
    }
    else {
        $guestArchive = "C:\PowerToysUiTestExchange\$(Split-Path $ExchangeRoot -Leaf)\$ProductArchive"
    }
    $sandboxArchive = "C:\PowerToysUiTestExchange\MwbProvisioning\$runId\sandbox\mwb-guest-runtime.zip"
    Copy-VMFile -Name $VmName -SourcePath (Join-Path $ExchangeRoot $GuestRuntimeArchive) `
        -DestinationPath $sandboxArchive -FileSource Host -CreateFullPath -Force
    Invoke-Command -Session $session -ScriptBlock {
        param($Archive, $Tools, $ReuseProduct, $User, $RunId)
        $ErrorActionPreference = 'Stop'
        $product = 'C:\PowerToysUiTestRun\PowerToys'
        if (@(Get-CimInstance Win32_Process | Where-Object {
            $_.ExecutablePath -and $_.ExecutablePath.StartsWith("$product\", [StringComparison]::OrdinalIgnoreCase)
        }).Count) { throw 'Staged product is running before provisioning.' }
        $null = New-Item -ItemType Directory -Path $Tools -Force
        if (-not $ReuseProduct) {
            $stage = "$product-stage-$RunId"
            $previous = "$product-previous-$RunId"
            if ((Test-Path -LiteralPath $stage) -or (Test-Path -LiteralPath $previous)) {
                throw 'Protected product replacement requires new run-scoped directories.'
            }
            $null = New-Item -ItemType Directory -Path $stage
            & tar.exe -xf $Archive -C $stage
            if ($LASTEXITCODE -ne 0) { throw 'Product archive extraction failed.' }
            if (Test-Path -LiteralPath $product) { Move-Item -LiteralPath $product -Destination $previous }
            Move-Item -LiteralPath $stage -Destination $product
            if (Test-Path -LiteralPath $previous) { Remove-Item -LiteralPath $previous -Recurse -Force }
        }
        $acl = New-Object Security.AccessControl.DirectorySecurity
        $acl.SetAccessRuleProtection($true, $false)
        foreach ($sid in @('S-1-5-18', 'S-1-5-32-544')) {
            $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new(
                [Security.Principal.SecurityIdentifier]::new($sid), 'FullControl',
                'ContainerInherit,ObjectInherit', 'None', 'Allow'))
        }
        $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new(
            "$env:COMPUTERNAME\$User", 'ReadAndExecute',
            'ContainerInherit,ObjectInherit', 'None', 'Allow'))
        Set-Acl -LiteralPath $Tools -AclObject $acl
        $logRoot = Join-Path $Tools 'provisioning-logs'
        $null = New-Item -ItemType Directory -Path $logRoot
        $logAcl = New-Object Security.AccessControl.DirectorySecurity
        $logAcl.SetAccessRuleProtection($true, $false)
        foreach ($sid in @('S-1-5-18', 'S-1-5-32-544')) {
            $logAcl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new(
                [Security.Principal.SecurityIdentifier]::new($sid), 'FullControl',
                'ContainerInherit,ObjectInherit', 'None', 'Allow'))
        }
        Set-Acl -LiteralPath $logRoot -AclObject $logAcl
    } -ArgumentList $guestArchive, $guestTools, $reuseProduct, $StandardUser, $runId
    $sandboxWinApp = ''
    if ($backend -eq 'WinApp') {
        $sandboxCliArchive = Join-Path $ExchangeRoot $SandboxWinAppArchive
        $sandboxCliHash = (Get-FileHash -LiteralPath $sandboxCliArchive -Algorithm SHA256).Hash
        Copy-VMFile -Name $VmName -SourcePath $sandboxCliArchive -DestinationPath "$guestTools\sandbox-winapp.zip" `
            -FileSource Host -CreateFullPath -Force
        $sandboxWinApp = Invoke-Command -Session $session -ScriptBlock {
            param($Tools, $ExpectedHash)
            $ErrorActionPreference = 'Stop'
            $archive = "$Tools\sandbox-winapp.zip"
            if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -cne $ExpectedHash) {
                throw 'The protected Sandbox CLI archive differs from its host source.'
            }
            $destination = "$Tools\sandbox-winapp"
            Expand-Archive -LiteralPath $archive -DestinationPath $destination
            $exe = Join-Path $destination 'winapp.exe'
            foreach ($name in @('winapp.exe', 'libSkiaSharp.dll', 'libHarfBuzzSharp.dll')) {
                if (-not (Test-Path -LiteralPath (Join-Path $destination $name) -PathType Leaf)) {
                    throw "Sandbox CLI archive must contain $name directly at its root."
                }
            }
            $exe
        } -ArgumentList $guestTools, $sandboxCliHash
    }
    foreach ($name in @('Initialize-AutonomousHost.ps1', 'Remove-AutonomousHost.ps1', 'MwbTestSigning.ps1')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -ToSession $session -Destination $guestTools
    }
    Copy-Item -LiteralPath (Join-Path $repoRoot '.pipelines\runUiTestAsUser.ps1') `
        -ToSession $session -Destination $guestTools
    Copy-Item -LiteralPath (Join-Path $repoRoot '.pipelines\Invoke-MwbProvisioning.ps1') `
        -ToSession $session -Destination $guestTools
    Invoke-Command -Session $session -ScriptBlock {
        param($Tools, $TaskName, $User, $RunId, $Archive, $Backend, $SandboxWinApp, $Configuration, $CertificateHash)
        $ErrorActionPreference = 'Stop'
        $arguments = '-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File "{0}\Invoke-MwbProvisioning.ps1" -ProductRoot C:\PowerToysUiTestRun\PowerToys -TestUser "{1}\{2}" -RunId {3} -GuestArchivePath "{4}"' -f $Tools, $env:COMPUTERNAME, $User, $RunId, $Archive
        $arguments += ' -SandboxBackend {0}' -f $Backend
        $arguments += ' -Configuration {0}' -f $Configuration
        if ($SandboxWinApp) { $arguments += ' -SandboxWinAppPath "{0}"' -f $SandboxWinApp }
        if ($CertificateHash) { $arguments += ' -ExpectedTestSigningCertificateSha256 {0}' -f $CertificateHash }
        $action = New-ScheduledTaskAction -Execute 'C:\Program Files\PowerShell\7\pwsh.exe' -Argument $arguments
        $principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
        $settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Minutes 18)
        $null = Register-ScheduledTask -TaskName $TaskName -Action $action -Principal $principal -Settings $settings
        Start-ScheduledTask -TaskName $TaskName
    } -ArgumentList $guestTools, $taskName, $StandardUser, $runId, $sandboxArchive, $backend, $sandboxWinApp, $Configuration, $certificateHash
    $taskRegistered = $true
    $deadline = [DateTime]::UtcNow.AddSeconds(90)
    do {
        $marker = Invoke-Command -Session $session -ScriptBlock {
            $path = 'C:\ProgramData\PowerToysMwbExperiment\host-provisioning.json'
            if (Test-Path -LiteralPath $path) { [IO.File]::ReadAllText($path) | ConvertFrom-Json }
        }
        if ($marker -and $marker.RunId -eq $runId -and $marker.Status -ne 'PreparingTrust') { break }
        $task = Invoke-Command -Session $session -ScriptBlock {
            param($Name)
            $info = Get-ScheduledTaskInfo -TaskName $Name
            [pscustomobject]@{
                State = (Get-ScheduledTask -TaskName $Name).State.ToString()
                LastRunTime = $info.LastRunTime
                LastTaskResult = $info.LastTaskResult
            }
        } -ArgumentList $taskName
        if ($task.State -ne 'Running' -and $task.LastRunTime.Year -ge 2000) {
            throw "Privileged setup exited before its marker (result $($task.LastTaskResult)). Protected diagnostics: $guestTools\provisioning-logs"
        }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    if (-not $marker -or $marker.RunId -ne $runId -or $marker.Status -notin 'Ready', 'WaitingForSandbox') {
        throw "Privileged setup did not initialize a ready marker within 90 seconds (state $($task.State), result $($task.LastTaskResult)). Protected diagnostics: $guestTools\provisioning-logs"
    }
    $provisioned = $true
    $result = & $controller @parameters
}
finally {
    $recoveryError = $null
    try {
        if ($provisioned) {
            Invoke-Command -Session $session -ScriptBlock {
                param($Tools, $RunId, $User)
                $ErrorActionPreference = 'Stop'
                # Discover only the scoped filename; recovery interprets the writable
                # journal strictly inside the original user's Limited task.
                $journals = @(Get-ChildItem C:\PowerToysUiTestRun\TestResults -File -Recurse `
                    -Filter cleanup-journal.json -ErrorAction SilentlyContinue | Where-Object {
                    (Split-Path $_.DirectoryName -Leaf) -eq "mwb-$RunId"
                })
                if ($journals.Count -gt 1) { throw 'Ambiguous recovery journal for this provisioning run.' }
                if ($journals.Count -eq 1) {
                    $executable = @(Get-ChildItem C:\PowerToysUiTestRun\Tests -File -Recurse `
                        -Filter MouseWithoutBorders.UITests.exe)
                    if ($executable.Count -ne 1) { throw 'The scoped recovery executable is ambiguous or missing.' }
                    & 'C:\Program Files\PowerShell\7\pwsh.exe' -NoLogo -NoProfile -ExecutionPolicy Bypass `
                        -File "$Tools\runUiTestAsUser.ps1" -TestExecutable $executable[0].FullName `
                        -ResultsDirectory C:\PowerToysUiTestExchange\MouseWithoutBorders\RecoveryResults `
                        -InteractiveUser "$env:COMPUTERNAME\$User" -MwbRecoveryJournal $journals[0].FullName `
                        -TimeoutMinutes 5
                    if ($LASTEXITCODE -ne 0) { throw 'Standard-user recovery requires attention or a baseline reset.' }
                }
            } -ArgumentList $guestTools, $runId, $StandardUser
        }
    }
    catch { $recoveryError = $_ }
    try {
        if ($taskRegistered) {
            Invoke-Command -Session $session -ScriptBlock {
                param($Name)
                $task = Get-ScheduledTask -TaskName $Name
                if ($task.State -eq 'Running') { Stop-ScheduledTask -TaskName $Name }
            } -ArgumentList $taskName
        }
        $cleanup = Invoke-Command -Session $session -ScriptBlock {
            param($Tools, $RunId, $TaskRegistered)
            $path = 'C:\ProgramData\PowerToysMwbExperiment\host-provisioning.json'
            if (Test-Path -LiteralPath $path) {
                $state = [IO.File]::ReadAllText($path) | ConvertFrom-Json
                if ($state.RunId -ne $RunId) {
                    if ($TaskRegistered) { throw 'Provisioning identity changed; refusing cleanup.' }
                    return
                }
                & 'C:\Program Files\PowerShell\7\pwsh.exe' -NoLogo -NoProfile -NonInteractive `
                    -ExecutionPolicy Bypass -File "$Tools\Remove-AutonomousHost.ps1" | Out-Null
                if ($LASTEXITCODE -ne 0) { throw 'Privileged firewall cleanup failed.' }
                [IO.File]::ReadAllText($path) | ConvertFrom-Json | Select-Object RunId, Status, RuleName
            }
        } -ArgumentList $guestTools, $runId, $taskRegistered
        $evidenceRoot = Join-Path $ExchangeRoot 'ProvisioningResults'
        $null = New-Item -ItemType Directory -Path $evidenceRoot -Force
        @{ RunId = $runId; Provisioned = $provisioned; Cleanup = $cleanup } |
            ConvertTo-Json -Depth 6 | Set-Content (Join-Path $evidenceRoot "$runId.json")
    }
    finally {
        try {
            if ($taskRegistered) {
                Invoke-Command -Session $session -ScriptBlock {
                    param($Name)
                    Unregister-ScheduledTask -TaskName $Name -Confirm:$false
                } -ArgumentList $taskName
            }
        }
        finally {
            Remove-PSSession $session
            $completedUtc = [DateTime]::UtcNow
            $clockPath = Join-Path $ExchangeRoot "ProvisioningResults\$runId-clock.json"
            $null = New-Item -ItemType Directory -Path (Split-Path $clockPath) -Force
            [ordered]@{
                RunId = $runId
                StartedUtc = $controllerStartedUtc.ToString('o')
                CompletedUtc = $completedUtc.ToString('o')
                WallElapsedMilliseconds = ($completedUtc - $controllerStartedUtc).TotalMilliseconds
                StopwatchElapsedMilliseconds = $controllerTimer.Elapsed.TotalMilliseconds
                HostPower = Get-MwbHostPowerEvidence $controllerStartedUtc $completedUtc
            } | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $clockPath
        }
    }
    if ($recoveryError) { throw $recoveryError }
}
$result
$summary = $result | Out-String | ConvertFrom-Json
if ($summary.Status -ne 'PASS' -or $summary.ExitCode -ne 0 -or
    $summary.Tests.Total -le 0 -or $summary.Tests.Executed -ne $summary.Tests.Total -or
    $summary.Tests.Passed -ne $summary.Tests.Total -or @($summary.ExportErrors).Count) {
    throw "Autonomous MWB test did not pass cleanly. Inspect $($summary.ResultsPath)."
}
