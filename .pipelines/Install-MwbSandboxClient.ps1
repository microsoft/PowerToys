# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#Requires -Version 7.0
[CmdletBinding(DefaultParameterSetName = 'Controller')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Controller')][string] $ResultsDirectory,
    [Parameter(Mandatory, ParameterSetName = 'Controller')][guid] $RunId,
    [Parameter(Mandatory, ParameterSetName = 'Controller')]
    [ValidateSet('x64Win10', 'x64Win11', 'arm64')][string] $Platform,
    [Parameter(ParameterSetName = 'Controller')][ValidateRange(30, 300)][int] $InstallTimeoutSeconds = 300,
    [Parameter(Mandatory, ParameterSetName = 'Worker')][string] $RequestPath
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\MwbSandboxCi.Common.ps1"
. "$PSScriptRoot\MwbSandboxClientSetup.Common.ps1"

function New-MwbClientProtectedDirectory {
    param([string] $Path, [string] $ReadSid, [string] $WriteSid)

    Assert-MwbCiPlainPath $Path
    if (Test-Path -LiteralPath $Path) { throw 'ClientSetupDirectoryAlreadyExists' }
    $acl = [Security.AccessControl.DirectorySecurity]::new()
    $acl.SetAccessRuleProtection($true, $false)
    $acl.SetOwner([Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))
    foreach ($sid in @('S-1-5-18', 'S-1-5-32-544', $ReadSid, $WriteSid) | Where-Object { $_ }) {
        $rights = if ($sid -eq $WriteSid) { 'Modify' } elseif ($sid -eq $ReadSid) { 'ReadAndExecute' } else { 'FullControl' }
        $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new(
            [Security.Principal.SecurityIdentifier]::new($sid), $rights, 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
    }
    $null = [IO.FileSystemAclExtensions]::Create([IO.DirectoryInfo]::new($Path), $acl)
}

function Assert-MwbClientProtectedDirectory {
    param([string] $Path)

    Assert-MwbCiPlainPath $Path
    $acl = Get-Acl -LiteralPath $Path
    $trusted = @('S-1-5-18', 'S-1-5-32-544')
    if ($acl.GetOwner([Security.Principal.SecurityIdentifier]).Value -notin $trusted) { throw 'UntrustedSetupOwner' }
    $write = [Security.AccessControl.FileSystemRights]'Write,Delete,DeleteSubdirectoriesAndFiles,ChangePermissions,TakeOwnership'
    foreach ($rule in $acl.GetAccessRules($true, $true, [Security.Principal.SecurityIdentifier])) {
        if ($rule.AccessControlType -eq 'Allow' -and ($rule.FileSystemRights -band $write) -and
            $rule.IdentityReference.Value -notin $trusted) { throw 'WritableSetupPayload' }
    }
}

function Test-MwbClientTaskCompletion {
    param($Task, $Info, [bool] $LaunchObserved, [Collections.IDictionary] $Status, [string] $RunId)

    if (-not $LaunchObserved -or $Task.State -notin @('Ready', 'Disabled')) { return $false }
    if ($null -eq $Status -or $Status.RunId -cne $RunId -or
        $Status.ExitCode -cnotin @(0, 1) -or
        ([long]$Info.LastTaskResult -band 0xffffffffL) -ne [long]$Status.ExitCode) {
        throw 'TaskResultMismatch'
    }
    $true
}

if ($PSCmdlet.ParameterSetName -eq 'Worker') {
    $exitCode = 1
    $report = $null
    $directory = $null
    try {
        Assert-MwbCiPlainPath $RequestPath
        if ([IO.Path]::GetFileName($RequestPath) -cne 'request.json' -or
            (Split-Path $RequestPath -Parent) -ine $PSScriptRoot) { throw 'InvalidSetupRequest' }
        Assert-MwbClientProtectedDirectory $PSScriptRoot
        Assert-MwbClientProtectedDirectory $RequestPath
        $request = Get-Content -LiteralPath $RequestPath -Raw | ConvertFrom-Json -AsHashtable
        if ($request.RunId -cnotmatch '^[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}$' -or
            (Split-Path $PSScriptRoot -Leaf) -cne ([guid]$request.RunId).ToString('N') -or
            $request.InstallTimeoutSeconds -lt 30 -or $request.InstallTimeoutSeconds -gt 300) { throw 'InvalidSetupRequest' }
        $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
        $elevated = ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole(
            [Security.Principal.WindowsBuiltInRole]::Administrator)
        $sessionId = (Get-Process -Id $PID).SessionId
        if ($elevated -or $identity.IsSystem -or $sessionId -eq 0 -or
            $identity.User.Value -cne $request.UserSid -or
            $identity.Name -ine (Get-CimInstance Win32_ComputerSystem).UserName -or
            -not @(Get-Process explorer -ErrorAction SilentlyContinue | Where-Object SessionId -EQ $sessionId)) {
            throw 'WrongInteractiveIdentity'
        }
        $directory = Join-Path $PSScriptRoot 'output'
        $report = [ordered]@{
            SchemaVersion = 1; RunId = $request.RunId; UserSid = $identity.User.Value
            IsElevated = $elevated; SessionId = $sessionId; Status = 'Failed'; Action = 'None'
            Before = @{ Status = 'NotChecked' }; After = @{ Status = 'NotChecked' }
            Cleanup = @{ Status = 'NotStarted' }; ErrorCode = $null; ErrorHResult = $null
        }
        Save-MwbClientWorkerReport $report $directory
        Invoke-MwbClientSetupWorker $request $report $directory
        $exitCode = if ($report.Status -eq 'Ready') { 0 } else { 1 }
    }
    catch {
        # No raw Appx, Store, provider or process diagnostics enter job stdout/stderr.
        if ($report) {
            $report.Status = 'Failed'
            $report.ErrorCode = 'SetupWorkerFailed'
            $report.ErrorHResult = '0x{0:X8}' -f ($_.Exception.HResult -band 0xffffffffL)
            Save-MwbClientWorkerReport $report $directory
        }
    }
    exit $exitCode
}

$published = Get-MwbPrerequisiteDirectory $ResultsDirectory $RunId
Assert-MwbCiPlainPath $published
$null = New-Item -ItemType Directory -Path $published -Force
$reportPath = Join-Path $published 'client-setup.json'
Assert-MwbCiPlainPath $reportPath
$report = [ordered]@{
    SchemaVersion = 1; RunId = $RunId.ToString(); Status = 'NotChecked'; Action = 'None'
    Before = @{ Status = 'NotChecked' }; After = @{ Status = 'NotChecked' }
    Cleanup = @{ Status = 'NotStarted' }; DispatchCleanup = 'NotStarted'
}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding utf8
if ($Platform -eq 'x64Win10') {
    $report.Status = 'SkippedLegacy'
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding utf8
    exit 0
}

$taskName = "PowerToys-MwbClientSetup-$($RunId.ToString('N'))"
$root = 'C:\ProgramData\PowerToysMwbClientSetup'
$runRoot = Join-Path $root $RunId.ToString('N')
$output = Join-Path $runRoot 'output'
$registered = $false
$created = $false
$userSid = $null
$exitCode = 1
try {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    if (-not ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'ElevatedControllerRequired' }
    Write-MwbSandboxPrerequisiteReport $ResultsDirectory $RunId $Platform
    $inventory = Get-Content -LiteralPath (Join-Path $published 'prerequisite-admin.json') -Raw | ConvertFrom-Json
    Copy-Item -LiteralPath (Join-Path $published 'prerequisite-admin.json') `
        -Destination (Join-Path $published 'client-setup-admin-before.json')
    $null = Get-MwbCiBackend $Platform ([int]$inventory.OSBuildNumber)
    if ($inventory.SandboxFeatureState -cne 'Enabled') { throw 'SandboxFeatureNotEnabled' }
    $interactiveUser = (Get-CimInstance Win32_ComputerSystem -ErrorAction Stop).UserName
    if ([string]::IsNullOrWhiteSpace($interactiveUser)) { throw 'InteractiveUserMissing' }
    $sid = [Security.Principal.NTAccount]::new($interactiveUser).Translate([Security.Principal.SecurityIdentifier])
    $userSid = $sid.Value
    if ($userSid -in @('S-1-5-18', 'S-1-5-19', 'S-1-5-20')) { throw 'InteractiveUserMissing' }
    $interactiveUser = $sid.Translate([Security.Principal.NTAccount]).Value
    $report.UserSid = $userSid
    if (-not (Test-Path -LiteralPath $root)) { New-MwbClientProtectedDirectory $root }
    Assert-MwbClientProtectedDirectory $root
    New-MwbClientProtectedDirectory $runRoot -ReadSid $userSid
    $created = $true
    New-MwbClientProtectedDirectory $output -WriteSid $userSid
    foreach ($name in @('Install-MwbSandboxClient.ps1', 'MwbSandboxClientSetup.Common.ps1',
        'MwbSandboxCi.Common.ps1', 'WinAppCli.Common.ps1')) {
        $source = Join-Path $PSScriptRoot $name
        $destination = Join-Path $runRoot $name
        $hash = (Get-FileHash -LiteralPath $source).Hash
        Copy-Item -LiteralPath $source -Destination $destination
        $acl = Get-Acl -LiteralPath $destination
        $acl.SetOwner([Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))
        Set-Acl -LiteralPath $destination -AclObject $acl
        Assert-MwbClientProtectedDirectory $destination
        if ((Get-FileHash -LiteralPath $destination).Hash -cne $hash) { throw 'SetupPayloadChanged' }
    }
    $package = @()
    $packageQueryFailed = $false
    try {
        $package = @(Get-MwbClientPackages -AllUsers | Sort-Object { [version]$_.Version } -Descending |
            Select-Object -First 1)
    }
    catch { $packageQueryFailed = $true }
    $request = @{
        RunId = $RunId.ToString(); UserSid = $userSid; InstallTimeoutSeconds = $InstallTimeoutSeconds
        PackageQueryFailed = $packageQueryFailed
        Package = if ($package.Count) { $package[0] } else { $null }
    }
    $requestPath = Join-Path $runRoot 'request.json'
    $request | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $requestPath -Encoding utf8
    $acl = Get-Acl -LiteralPath $requestPath
    $acl.SetOwner([Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))
    Set-Acl -LiteralPath $requestPath -AclObject $acl
    $powerShell = (Get-Process -Id $PID).Path
    $arguments = '-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File "{0}" -RequestPath "{1}"' -f
        (Join-Path $runRoot 'Install-MwbSandboxClient.ps1'), $requestPath
    $action = New-ScheduledTaskAction -Execute $powerShell -Argument $arguments
    $principal = New-ScheduledTaskPrincipal -UserId $interactiveUser -LogonType Interactive -RunLevel Limited
    $settings = New-ScheduledTaskSettingsSet -Priority 4 -ExecutionTimeLimit (New-TimeSpan -Seconds 630) `
        -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
    Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal -Settings $settings | Out-Null
    $registered = $true
    Start-ScheduledTask -TaskName $taskName
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $launched = $false
    $workerPath = Join-Path $output 'worker-report.json'
    while ($true) {
        Start-Sleep -Seconds 2
        $task = Get-ScheduledTask -TaskName $taskName
        $info = Get-ScheduledTaskInfo -TaskName $taskName
        $launched = $launched -or $task.State -eq 'Running' -or $info.LastRunTime.Year -ge 2000
        if ($launched -and $task.State -in @('Ready', 'Disabled')) {
            $info = Get-ScheduledTaskInfo -TaskName $taskName
            Assert-MwbCiPlainPath $workerPath
            if ((Get-Item -LiteralPath $workerPath).Length -gt 64KB) { throw 'InvalidClientSetupReport' }
            $worker = Get-Content -LiteralPath $workerPath -Raw | ConvertFrom-Json -AsHashtable
            $status = @{
                RunId = $worker.RunId
                ExitCode = if ($worker.Status -eq 'Ready') { 0 } else { 1 }
            }
            $null = Test-MwbClientTaskCompletion $task $info $launched $status $RunId.ToString()
            $report = ConvertTo-MwbPublicClientSetup $worker $RunId.ToString() $userSid
            $exitCode = $status.ExitCode
            break
        }
        if (-not $launched -and $timer.Elapsed.TotalSeconds -gt 30) { throw 'TaskDidNotLaunch' }
        if ($timer.Elapsed.TotalSeconds -gt 600) { throw 'SetupTaskTimeout' }
    }
}
catch {
    $report.Status = 'Failed'
    $allowed = @('ElevatedControllerRequired', 'SandboxFeatureNotEnabled', 'InteractiveUserMissing',
        'SetupPayloadChanged', 'TaskResultMismatch', 'InvalidClientSetupReport', 'TaskDidNotLaunch',
        'SetupTaskTimeout', 'UntrustedSetupOwner', 'WritableSetupPayload', 'ClientSetupDirectoryAlreadyExists')
    $report.ErrorCode = if ($_.Exception.Message -cin $allowed) { $_.Exception.Message } else { 'ClientSetupControllerFailed' }
    $report.ErrorHResult = '0x{0:X8}' -f ($_.Exception.HResult -band 0xffffffffL)
}
finally {
    $report.DispatchCleanup = 'Passed'
    try {
        if ($registered) {
            if ((Get-ScheduledTask -TaskName $taskName).State -in @('Running', 'Queued')) {
                Stop-ScheduledTask -TaskName $taskName
            }
            Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
        }
    }
    catch {
        $exitCode = 1
        $report.Status = 'Failed'
        $report.DispatchCleanup = 'Failed'
        $report.DispatchError = Get-MwbDiagnosticError $_ 'SetupTaskCleanupFailed'
    }
    try {
        if ($created) {
            $workerPath = Join-Path $output 'worker-report.json'
            try {
                Assert-MwbCiPlainPath $workerPath
                if ($report.Status -ne 'Ready' -and (Test-Path -LiteralPath $workerPath) -and
                    (Get-Item -LiteralPath $workerPath).Length -le 64KB) {
                    $worker = Get-Content -LiteralPath $workerPath -Raw | ConvertFrom-Json -AsHashtable
                    $partial = ConvertTo-MwbPublicClientSetup $worker $RunId.ToString() $userSid
                    $report.Before = $partial.Before
                    $report.After = $partial.After
                    $report.Cleanup = $partial.Cleanup
                    $report.Action = $partial.Action
                }
            }
            finally {
                # Revoke both grants even when the worker's report cannot be collected.
                foreach ($path in @($runRoot, $output)) {
                    Assert-MwbCiPlainPath $path
                    $acl = Get-Acl -LiteralPath $path
                    $acl.PurgeAccessRules([Security.Principal.SecurityIdentifier]::new($userSid))
                    Set-Acl -LiteralPath $path -AclObject $acl
                }
            }
            $screenshot = Join-Path $output 'client-setup-failure.png'
            Assert-MwbCiPlainPath $screenshot
            if (Test-Path -LiteralPath $screenshot -PathType Leaf) {
                if ((Get-Item -LiteralPath $screenshot).Length -gt 32MB) { throw 'SetupScreenshotTooLarge' }
                Copy-Item -LiteralPath $screenshot -Destination (Join-Path $published 'client-setup-failure.png')
            }
            foreach ($item in Get-ChildItem -LiteralPath $runRoot -Force -Recurse) {
                Assert-MwbCiPlainPath $item.FullName
            }
            Remove-Item -LiteralPath $runRoot -Recurse -Force
        }
    }
    catch {
        $exitCode = 1
        $report.Status = 'Failed'
        $report.DispatchCleanup = 'Failed'
        $report.DispatchError = Get-MwbDiagnosticError $_ 'SetupDispatchCleanupFailed'
    }
    try {
        $after = Get-MwbSandboxPrerequisiteReport $Platform
        $after | ConvertTo-Json -Depth 10 |
            Set-Content -LiteralPath (Join-Path $published 'client-setup-admin-after.json') -Encoding utf8
    }
    catch {
        [ordered]@{
            SchemaVersion = 3; CollectionContext = 'ElevatedPrepareInventory'; Status = 'QueryFailed'
            QueryError = Get-MwbDiagnosticError $_ 'ClientSetupAfterInventoryFailed'
        } | ConvertTo-Json -Depth 4 |
            Set-Content -LiteralPath (Join-Path $published 'client-setup-admin-after.json') -Encoding utf8
    }
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding utf8
}
Write-Host "Modern Sandbox client setup: $($report.Status). See client-setup.json; no raw installation logs are published."
exit $exitCode
