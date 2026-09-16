# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Runs the autonomous MWB MSTest executable in an existing nested-Sandbox Win10 VM.
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
    [string]$BuildLabel = 'local-mwb-debug',
    [string]$Filter,
    [string]$StandardUser = 'PTUser',
    [string]$GuestRuntimeArchive = 'mwb-guest-runtime.zip',
    [switch]$ReuseStagedPayload,
    [switch]$PlanOnly
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..\..'))
$skillRoot = Join-Path $repoRoot '.github\skills\ui-tests-local-vm\scripts'
$controller = Join-Path $skillRoot 'Invoke-LocalVmUiTest.ps1'
$parameters = @{
    VmRoot = $VmRoot; VmName = $VmName; ConfigurationPath = $ConfigurationPath
    ExchangeRoot = $ExchangeRoot; TestExecutable = 'MouseWithoutBorders.UITests.exe'
    Platform = 'x64Win10'; BuildLabel = $BuildLabel; StandardUser = $StandardUser
    SuiteTimeout = '45m'; TimeoutMinutes = 60; StartupTimeoutMinutes = 15
    ReuseStagedPayload = $ReuseStagedPayload
}
if ($Filter) { $parameters.Filter = $Filter }
if ($PlanOnly) {
    & $controller @parameters -PlanOnly
    return
}
& (Join-Path $skillRoot 'Initialize-LocalVmHost.ps1') -VmRoot $VmRoot -ConfigPath $ConfigurationPath -CheckOnly
if ($LASTEXITCODE -ne 0) { throw 'Local-VM host readiness failed.' }
& (Join-Path $VmRoot 'Start-LocalVm.ps1') -ConfigPath $ConfigurationPath -Wait -TimeoutMinutes 15
$runId = [Guid]::NewGuid().ToString()
$taskName = "PowerToys-Mwb-Provision-$runId"
$guestTools = "C:\ProgramData\PowerToysMwbProvisioner\$runId"
$credentialPath = Join-Path $env:LOCALAPPDATA 'PowerToysUiTestVm\admin.credential.xml'
$credential = Import-Clixml $credentialPath
$session = New-PSSession -VMName $VmName -Credential $credential
$taskRegistered = $false
$provisioned = $false
$result = $null
try {
    # The protected marker fingerprints these exact files before the standard-user
    # controller extracts the same archive into the same product path.
    $archive = Join-Path $ExchangeRoot 'powertoys-runtime.zip'
    $guestArchive = "C:\PowerToysUiTestExchange\MwbProvisioning\$runId\powertoys-runtime.zip"
    $archiveHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
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
        $guestArchive = 'C:\PowerToysUiTestExchange\MouseWithoutBorders\powertoys-runtime.zip'
    }
    $sandboxArchive = "C:\PowerToysUiTestExchange\MwbProvisioning\$runId\sandbox\mwb-guest-runtime.zip"
    Copy-VMFile -Name $VmName -SourcePath (Join-Path $ExchangeRoot $GuestRuntimeArchive) `
        -DestinationPath $sandboxArchive -FileSource Host -CreateFullPath -Force
    Invoke-Command -Session $session -ScriptBlock {
        param($Archive, $Tools, $ReuseProduct, $User)
        $ErrorActionPreference = 'Stop'
        $product = 'C:\PowerToysUiTestRun\PowerToys'
        if (@(Get-CimInstance Win32_Process | Where-Object {
            $_.ExecutablePath -and $_.ExecutablePath.StartsWith("$product\", [StringComparison]::OrdinalIgnoreCase)
        }).Count) { throw 'Staged product is running before provisioning.' }
        $null = New-Item -ItemType Directory -Path $product, $Tools -Force
        if (-not $ReuseProduct) {
            & tar.exe -xf $Archive -C $product
            if ($LASTEXITCODE -ne 0) { throw 'Product archive extraction failed.' }
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
    } -ArgumentList $guestArchive, $guestTools, $reuseProduct, $StandardUser
    foreach ($name in @('Initialize-AutonomousHost.ps1', 'Remove-AutonomousHost.ps1')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -ToSession $session -Destination $guestTools
    }
    Copy-Item -LiteralPath (Join-Path $repoRoot '.pipelines\runUiTestAsUser.ps1') `
        -ToSession $session -Destination $guestTools
    Invoke-Command -Session $session -ScriptBlock {
        param($Tools, $TaskName, $User, $RunId, $Archive)
        $ErrorActionPreference = 'Stop'
        $arguments = '-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File "{0}\Initialize-AutonomousHost.ps1" -ProductRoot C:\PowerToysUiTestRun\PowerToys -TestUser "{1}\{2}" -RunId {3} -GuestArchivePath "{4}"' -f $Tools, $env:COMPUTERNAME, $User, $RunId, $Archive
        $action = New-ScheduledTaskAction -Execute 'C:\Program Files\PowerShell\7\pwsh.exe' -Argument $arguments
        $principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
        $settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Minutes 18)
        $null = Register-ScheduledTask -TaskName $TaskName -Action $action -Principal $principal -Settings $settings
        Start-ScheduledTask -TaskName $TaskName
    } -ArgumentList $guestTools, $taskName, $StandardUser, $runId, $sandboxArchive
    $taskRegistered = $true
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    do {
        $marker = Invoke-Command -Session $session -ScriptBlock {
            $path = 'C:\ProgramData\PowerToysMwbExperiment\host-provisioning.json'
            if (Test-Path -LiteralPath $path) { [IO.File]::ReadAllText($path) | ConvertFrom-Json }
        }
        if ($marker -and $marker.RunId -eq $runId) { break }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    if (-not $marker -or $marker.RunId -ne $runId -or $marker.Status -notin 'Ready', 'WaitingForSandbox') {
        $task = Invoke-Command -Session $session -ScriptBlock {
            param($Name)
            Get-ScheduledTaskInfo -TaskName $Name | Select-Object LastTaskResult
        } -ArgumentList $taskName
        throw "Privileged setup did not initialize its marker. Task result: $($task.LastTaskResult)"
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
            param($Tools, $RunId)
            $path = 'C:\ProgramData\PowerToysMwbExperiment\host-provisioning.json'
            if (Test-Path -LiteralPath $path) {
                $state = [IO.File]::ReadAllText($path) | ConvertFrom-Json
                if ($state.RunId -ne $RunId) { throw 'Provisioning identity changed; refusing cleanup.' }
                & 'C:\Program Files\PowerShell\7\pwsh.exe' -NoLogo -NoProfile -NonInteractive `
                    -ExecutionPolicy Bypass -File "$Tools\Remove-AutonomousHost.ps1" | Out-Null
                if ($LASTEXITCODE -ne 0) { throw 'Privileged firewall cleanup failed.' }
                [IO.File]::ReadAllText($path) | ConvertFrom-Json | Select-Object RunId, Status, RuleName
            }
        } -ArgumentList $guestTools, $runId
        $evidenceRoot = Join-Path $ExchangeRoot 'ProvisioningResults'
        $null = New-Item -ItemType Directory -Path $evidenceRoot -Force
        @{ RunId = $runId; Provisioned = $provisioned; Cleanup = $cleanup } |
            ConvertTo-Json -Depth 6 | Set-Content (Join-Path $evidenceRoot "$runId.json")
    }
    finally {
        if ($taskRegistered) {
            Invoke-Command -Session $session -ScriptBlock {
                param($Name)
                Unregister-ScheduledTask -TaskName $Name -Confirm:$false
            } -ArgumentList $taskName
        }
        Remove-PSSession $session
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
