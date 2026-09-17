# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Prepare', 'Run', 'Recover', 'Cleanup')]
    [string] $Mode,
    [Parameter(Mandatory)]
    [guid] $RunId,
    [string] $ArtifactRoot,
    [string] $ResultsDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-MwbLocalPath {
    param([string] $Path)

    if ($Path -notmatch '^[A-Za-z]:\\' -or $Path -match '["\r\n]') {
        throw 'MWB Sandbox requires a local absolute path without command-line metacharacters.'
    }
    $ancestor = [IO.Path]::GetFullPath($Path)
    while ($ancestor) {
        if ((Test-Path -LiteralPath $ancestor) -and
            ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'MWB Sandbox paths must not contain reparse points.'
        }
        $ancestor = Split-Path $ancestor -Parent
    }
}

function Get-MwbSandboxPayload {
    param([string] $Root)

    Assert-MwbLocalPath $Root
    $rootPath = (Resolve-Path -LiteralPath $Root).Path.TrimEnd('\')
    if ((Split-Path $rootPath -Leaf) -cne 'build-x64-Debug') {
        throw 'MWB Sandbox requires the directly downloaded build-x64-Debug artifact.'
    }
    $products = @(Get-ChildItem -LiteralPath $rootPath -Filter 'PowerToys.exe' -File -Recurse |
        Where-Object {
            $_.DirectoryName -match '\\x64\\Debug$' -and
            (Test-Path -LiteralPath (Join-Path $_.DirectoryName 'PowerToys.MouseWithoutBorders.exe')) -and
            (Test-Path -LiteralPath (Join-Path $_.DirectoryName 'PowerToys.MouseWithoutBorders.dll'))
        })
    if ($products.Count -ne 1) {
        throw "Expected exactly one staged Debug product root; found $($products.Count). Installed Release is never a fallback."
    }
    $productRoot = $products[0].DirectoryName
    Assert-MwbLocalPath $productRoot
    $runners = @(Get-ChildItem -LiteralPath (Join-Path $productRoot 'tests') `
        -Filter 'MouseWithoutBorders.UITests.runtimeconfig.json' -File -Recurse)
    if ($runners.Count -ne 1) {
        throw "Expected exactly one MWB Sandbox test runner; found $($runners.Count)."
    }
    $testExecutable = Join-Path $runners[0].DirectoryName 'MouseWithoutBorders.UITests.exe'
    Assert-MwbLocalPath $testExecutable
    if (-not (Test-Path -LiteralPath $testExecutable -PathType Leaf)) {
        throw 'The MWB Sandbox MTP executable was not staged with its runtime configuration.'
    }
    foreach ($name in @('Recover-Host.ps1', 'EndpointSupport.ps1', 'NativeSupport.cs')) {
        $path = Join-Path $runners[0].DirectoryName "Payload\$name"
        Assert-MwbLocalPath $path
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "The staged MWB recovery payload is incomplete: $name."
        }
    }
    [pscustomobject]@{ ProductRoot = $productRoot; TestExecutable = $testExecutable }
}

function Get-MwbPublicRunRoot {
    param([string] $ResultsRoot, [guid] $Id)

    Assert-MwbLocalPath $ResultsRoot
    Join-Path ([IO.Path]::GetFullPath($ResultsRoot)) "ui-$($Id.ToString('N').Substring(0, 12))\mwb-$Id"
}

function Assert-MwbProtectedDirectory {
    param([string] $Path)

    Assert-MwbLocalPath $Path
    $acl = Get-Acl -LiteralPath $Path
    $trustedSids = @('S-1-5-18', 'S-1-5-32-544')
    if ($acl.GetOwner([Security.Principal.SecurityIdentifier]).Value -notin $trustedSids) {
        throw 'MWB provisioning state must be owned by SYSTEM or Administrators.'
    }
    $writeRights = [Security.AccessControl.FileSystemRights]'Write, Delete, DeleteSubdirectoriesAndFiles, ChangePermissions, TakeOwnership'
    foreach ($rule in $acl.GetAccessRules($true, $true, [Security.Principal.SecurityIdentifier])) {
        if ($rule.AccessControlType -eq 'Allow' -and ($rule.FileSystemRights -band $writeRights) -and
            $rule.IdentityReference.Value -notin $trustedSids) {
            throw 'MWB provisioning state must not be writable by the interactive test user.'
        }
    }
}

function New-MwbProtectedDirectory {
    param([string] $Path)

    Assert-MwbLocalPath $Path
    if (Test-Path -LiteralPath $Path) {
        Assert-MwbProtectedDirectory $Path
        return
    }
    $acl = [Security.AccessControl.DirectorySecurity]::new()
    $acl.SetAccessRuleProtection($true, $false)
    $acl.SetOwner([Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))
    foreach ($sid in @('S-1-5-18', 'S-1-5-32-544')) {
        $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new(
            [Security.Principal.SecurityIdentifier]::new($sid), 'FullControl',
            'ContainerInherit,ObjectInherit', 'None', 'Allow'))
    }
    [IO.FileSystemAclExtensions]::Create([IO.DirectoryInfo]::new($Path), $acl)
    Assert-MwbProtectedDirectory $Path
}

function Read-MwbProvisioningMarker {
    param([string] $ExpectedProductRoot, [string] $ExpectedUserSid)

    Assert-MwbProtectedDirectory $stateRoot
    Assert-MwbProtectedDirectory $markerPath
    $marker = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
    if ($marker.RunId -cne $RunId.ToString() -or
        $marker.ProductRoot -ine $ExpectedProductRoot -or
        $marker.Executable -ine (Join-Path $ExpectedProductRoot 'PowerToys.MouseWithoutBorders.exe') -or
        $marker.RuleName -cne "PowerToys.Mwb.UITest.$RunId" -or
        $marker.TestUserSid -cne $ExpectedUserSid) {
        throw 'The privileged provisioning marker does not belong to this CI run and payload.'
    }
    $marker
}

function Write-MwbProvisioningDiagnostics {
    param([string] $RunDirectory)

    Assert-MwbProtectedDirectory $RunDirectory
    $logRoot = Join-Path $RunDirectory 'provisioning-logs'
    if (-not (Test-Path -LiteralPath $logRoot -PathType Container)) {
        Write-Host 'MWB provisioning diagnostics are unavailable; the logging launcher may not have started.'
        return
    }
    Assert-MwbProtectedDirectory $logRoot
    foreach ($name in @('stderr.log', 'stdout.log')) {
        $path = Join-Path $logRoot $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            Write-Host "MWB provisioning ${name}: not created; the logging launcher may not have started."
            continue
        }
        Assert-MwbProtectedDirectory $path
        $stream = $null
        try {
            $stream = [IO.File]::Open($path, 'Open', 'Read', 'ReadWrite')
            # Bound both I/O and task output even for a growing log or one enormous line.
            $buffer = [byte[]]::new(16384)
            $offset = [Math]::Max(0, $stream.Length - $buffer.Length)
            $null = $stream.Seek($offset, 'Begin')
            $count = $stream.Read($buffer, 0, $buffer.Length)
            $text = [Text.Encoding]::UTF8.GetString($buffer, 0, $count)
            if ($offset -gt 0) {
                $newline = $text.IndexOf("`n")
                if ($newline -lt 0) {
                    Write-Host "MWB provisioning ${name}: oversized line omitted; inspect the protected run-local log."
                    continue
                }
                $text = $text.Substring($newline + 1)
            }
            $text = [regex]::Replace($text, '\x1b\[[0-?]*[ -/]*[@-~]', '')
            # Suppress the entire excerpt, not just the matching line: diagnostics can
            # wrap credentials or command arguments onto subsequent lines.
            if ($text -match '(?i)WindowsSandboxClient|password|token|credential|secret|authorization') {
                Write-Host "MWB provisioning ${name}: sensitive diagnostic content omitted; inspect the protected run-local log."
                continue
            }
            if ([string]::IsNullOrWhiteSpace($text)) {
                Write-Host "MWB provisioning ${name}: empty."
                continue
            }
            foreach ($line in @($text -split '\r\n|\n|\r' | Select-Object -Last 40)) {
                # Untrusted text must not be interpreted as Azure logging commands.
                Write-Host "MWB provisioning ${name}: $($line.Replace('##', '# #'))"
            }
        }
        catch [IO.IOException], [UnauthorizedAccessException] {
            Write-Host "MWB provisioning ${name}: cannot read the protected log ($($_.Exception.GetType().Name))."
        }
        finally {
            if ($null -ne $stream) { $stream.Dispose() }
        }
    }
}

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
try {
    if (-not ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'BLOCKED_INFRASTRUCTURE: MWB CI setup/cleanup requires an already elevated agent. No UAC or feature installation is attempted.'
    }
}
finally {
    $identity.Dispose()
}

$controlRoot = 'C:\ProgramData\PowerToysMwbSandboxCi'
$runRoot = Join-Path $controlRoot $RunId.ToString('N')
$manifestPath = Join-Path $runRoot 'ci-provisioning.json'
$stateRoot = 'C:\ProgramData\PowerToysMwbExperiment'
$markerPath = Join-Path $stateRoot 'host-provisioning.json'
$taskName = "PowerToys-MwbSandbox-Provision-$($RunId.ToString('N'))"
$setupPath = Join-Path $runRoot 'Invoke-MwbProvisioning.ps1'
$cleanupPath = Join-Path $runRoot 'Remove-AutonomousHost.ps1'
$powerShell = (Get-Process -Id $PID).Path

if ($Mode -eq 'Prepare') {
    $payload = Get-MwbSandboxPayload $ArtifactRoot
    $interactiveUser = (Get-CimInstance Win32_ComputerSystem).UserName
    if ([string]::IsNullOrWhiteSpace($interactiveUser)) {
        throw 'BLOCKED_INFRASTRUCTURE: no logged-on interactive desktop user is available.'
    }
    $userSid = [Security.Principal.NTAccount]::new($interactiveUser).Translate([Security.Principal.SecurityIdentifier])
    if ($userSid.Value -in @('S-1-5-18', 'S-1-5-19', 'S-1-5-20')) {
        throw 'BLOCKED_INFRASTRUCTURE: a service account is not an interactive test user.'
    }
    $interactiveUser = $userSid.Translate([Security.Principal.NTAccount]).Value
    if ($interactiveUser -match '["\r\n]') {
        throw 'Invalid interactive account name.'
    }
    if (Test-Path -LiteralPath $stateRoot) {
        Assert-MwbProtectedDirectory $stateRoot
        Assert-MwbLocalPath $markerPath
        if (-not (Test-Path -LiteralPath $markerPath -PathType Leaf) -or
            (Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json).Status -ne 'Removed') {
            throw 'BLOCKED_INFRASTRUCTURE: an earlier provisioning run needs cleanup before this pilot.'
        }
    }

    New-MwbProtectedDirectory $controlRoot
    if (Test-Path -LiteralPath $runRoot) {
        throw 'This MWB CI run was already prepared. Automatic pilot retries are not allowed.'
    }
    New-MwbProtectedDirectory $runRoot
    $readAcl = Get-Acl -LiteralPath $runRoot
    $readAcl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new(
        $userSid, 'ReadAndExecute', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
    Set-Acl -LiteralPath $runRoot -AclObject $readAcl
    # Raw provisioner output is not readable by the test user and is never attached
    # to public test results. This child directory does not inherit the read grant.
    New-MwbProtectedDirectory (Join-Path $runRoot 'provisioning-logs')
    $sourceRoot = Join-Path $PSScriptRoot '..\src\modules\MouseWithoutBorders\Tests\SandboxExperiment'
    $guestArchive = Join-Path $runRoot 'guest-runtime\product.zip'
    & (Join-Path $sourceRoot 'New-MwbRuntimeArchive.ps1') -ProductRoot $payload.ProductRoot -ArchivePath $guestArchive | Out-Null
    $hashes = @{}
    foreach ($name in @('Initialize-AutonomousHost.ps1', 'Remove-AutonomousHost.ps1', 'Invoke-MwbProvisioning.ps1')) {
        $source = if ($name -ceq 'Invoke-MwbProvisioning.ps1') {
            Join-Path $PSScriptRoot $name
        } else {
            Join-Path $sourceRoot $name
        }
        $destination = Join-Path $runRoot $name
        $hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
        Copy-Item -LiteralPath $source -Destination $destination
        if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -cne $hash) {
            throw 'A privileged provisioning script changed while being staged.'
        }
        $hashes[$name] = $hash
    }
    $arguments = '-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "{0}" -ProductRoot "{1}" -TestUser "{2}" -RunId "{3}" -GuestArchivePath "{4}"' -f
        $setupPath, $payload.ProductRoot, $interactiveUser, $RunId, $guestArchive
    @{
        RunId = $RunId.ToString()
        ProductRoot = $payload.ProductRoot
        TestExecutable = $payload.TestExecutable
        InteractiveUser = $interactiveUser
        TestUserSid = $userSid.Value
        SetupArguments = $arguments
        ScriptHashes = $hashes
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath

    $action = New-ScheduledTaskAction -Execute $powerShell -Argument $arguments
    $principal = New-ScheduledTaskPrincipal -UserId 'S-1-5-18' -LogonType ServiceAccount -RunLevel Highest
    $settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Minutes 20) `
        -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
    Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal -Settings $settings | Out-Null
    Start-ScheduledTask -TaskName $taskName
    $deadline = [DateTime]::UtcNow.AddSeconds(90)
    do {
        if (Test-Path -LiteralPath $markerPath -PathType Leaf) {
            # A previous successfully removed record may remain until the new provisioner writes.
            $current = $null
            try {
                $current = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
            }
            catch [ArgumentException] {
                # The provisioner writes its small marker in place; retry an incomplete write.
            }
            if ($null -eq $current -or ($current.RunId -cne $RunId.ToString() -and $current.Status -eq 'Removed')) {
                Start-Sleep -Seconds 1
                continue
            }
            # The initial marker is enough. On Win10, only the test can start Sandbox's Default Switch.
            $marker = Read-MwbProvisioningMarker $payload.ProductRoot $userSid.Value
            if ($marker.Status -in @('WaitingForSandbox', 'Ready')) {
                Write-Host "Privileged setup $($marker.Status); dispatch may start Sandbox as $interactiveUser."
                return
            }
            Write-MwbProvisioningDiagnostics $runRoot
            throw "BLOCKED_INFRASTRUCTURE: privileged setup status is $($marker.Status)."
        }
        $task = Get-ScheduledTask -TaskName $taskName
        $info = Get-ScheduledTaskInfo -TaskName $taskName
        if ($task.State -ne 'Running' -and $info.LastRunTime.Year -ge 2000) {
            Write-MwbProvisioningDiagnostics $runRoot
            throw "BLOCKED_INFRASTRUCTURE: provisioning exited before its initial marker (scheduler result $($info.LastTaskResult))."
        }
        Start-Sleep -Seconds 1
    } while ([DateTime]::UtcNow -lt $deadline)
    Write-MwbProvisioningDiagnostics $runRoot
    throw 'BLOCKED_INFRASTRUCTURE: provisioning did not publish its initial marker within 90 seconds.'
}

if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    if ($Mode -in @('Recover', 'Cleanup')) {
        Write-Host 'No privileged setup manifest was created for this run; no task or firewall rule is owned.'
        return
    }
    throw 'The MWB CI preparation manifest is missing.'
}
Assert-MwbProtectedDirectory $controlRoot
Assert-MwbProtectedDirectory $runRoot
Assert-MwbProtectedDirectory $manifestPath
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.RunId -cne $RunId.ToString()) {
    throw 'The protected CI manifest belongs to a different run.'
}

if ($Mode -eq 'Run') {
    $payload = Get-MwbSandboxPayload $ArtifactRoot
    if ($payload.ProductRoot -ine $manifest.ProductRoot -or $payload.TestExecutable -ine $manifest.TestExecutable) {
        throw 'The staged MWB test or product root changed after preparation.'
    }
    $marker = Read-MwbProvisioningMarker $payload.ProductRoot $manifest.TestUserSid
    if ($marker.Status -notin @('WaitingForSandbox', 'Ready')) {
        Write-MwbProvisioningDiagnostics $runRoot
        throw "BLOCKED_INFRASTRUCTURE: privileged setup status is $($marker.Status)."
    }
    $env:POWERTOYS_INSTALL_DIR = $payload.ProductRoot
    $env:POWERTOYS_MWB_RUN_ROOT = Get-MwbPublicRunRoot $ResultsDirectory $RunId
    $env:useInstallerForTest = 'false'
    & "$PSScriptRoot\runUiTestAsUser.ps1" -TestExecutable $payload.TestExecutable `
        -ResultsDirectory $ResultsDirectory -InteractiveUser $manifest.InteractiveUser -TimeoutMinutes 45 `
        -TaskRunId $RunId -Filter 'FullyQualifiedName~AutonomousSandboxSmoke'
    exit $LASTEXITCODE
}

if ($Mode -eq 'Recover') {
    $publicRunRoot = Get-MwbPublicRunRoot $ResultsDirectory $RunId
    $journalPath = Join-Path $publicRunRoot 'cleanup-journal.json'
    $launcherPath = Join-Path $PSScriptRoot 'runUiTestAsUser.ps1'
    $testTaskName = "PowerToys-InteractiveUiTest-$($RunId.ToString('N'))"
    $requestPath = Join-Path (Split-Path $publicRunRoot -Parent) 'request.json'
    $expectedArguments = '-ExecutionPolicy Bypass -NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -File "{0}" -RequestPath "{1}"' -f
        $launcherPath, $requestPath
    $task = Get-ScheduledTask -TaskName $testTaskName -ErrorAction SilentlyContinue
    if ($task) {
        # An aborted Azure wrapper may not have reached the launcher's finally. End only its
        # exact Limited task before recovery; Recover-Host independently rejects a still-live MTP.
        if ($task.Actions.Count -ne 1 -or $task.Actions[0].Execute -ine $powerShell -or
            $task.Actions[0].Arguments -cne $expectedArguments -or
            $task.Principal.RunLevel -ne 'Limited' -or $task.Principal.LogonType -ne 'Interactive') {
            throw 'The original MWB desktop task no longer matches this run.'
        }
        $taskUserSid = if ($task.Principal.UserId -match '^S-1-') {
            [Security.Principal.SecurityIdentifier]::new($task.Principal.UserId)
        } else {
            [Security.Principal.NTAccount]::new($task.Principal.UserId).Translate([Security.Principal.SecurityIdentifier])
        }
        if ($taskUserSid.Value -ne $manifest.TestUserSid) {
            throw 'The original MWB desktop task belongs to a different user.'
        }
        Stop-ScheduledTask -TaskName $testTaskName
        $deadline = [DateTime]::UtcNow.AddSeconds(15)
        while ((Get-ScheduledTask -TaskName $testTaskName).State -in @('Running', 'Queued')) {
            if ([DateTime]::UtcNow -ge $deadline) {
                throw 'The original MWB desktop task did not stop before recovery.'
            }
            Start-Sleep -Milliseconds 250
        }
        Unregister-ScheduledTask -TaskName $testTaskName -Confirm:$false
    }
    Assert-MwbLocalPath $journalPath
    if (-not (Test-Path -LiteralPath $journalPath -PathType Leaf)) {
        if (Test-Path -LiteralPath $publicRunRoot) {
            throw 'MWB public run state exists without its recovery journal; a clean baseline is required.'
        }
        Write-Host 'MSTest did not create public MWB run state; no user recovery journal is available.'
        return
    }
    # The administrator passes a fixed journal path without interpreting its contents.
    # Only the original Limited user executes the packaged, ownership-validating recovery script.
    & $launcherPath -TestExecutable $manifest.TestExecutable -ResultsDirectory $ResultsDirectory `
        -InteractiveUser $manifest.InteractiveUser -MwbRecoveryJournal $journalPath -TimeoutMinutes 4
    exit $LASTEXITCODE
}

# Never elevate a test journal or test-supplied request. This path consumes only the protected
# provisioner's marker and copies of the fixed source scripts.
$failures = [Collections.Generic.List[string]]::new()
$task = $null
try {
    $task = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
    if ($task) {
        if ($task.Actions.Count -ne 1 -or $task.Actions[0].Execute -ine $powerShell -or
            $task.Actions[0].Arguments -cne $manifest.SetupArguments) {
            throw 'The provisioning task action no longer matches this run.'
        }
        Stop-ScheduledTask -TaskName $taskName
    }
}
catch {
    $failures.Add($_.Exception.Message)
}
try {
    if (Test-Path -LiteralPath $markerPath -PathType Leaf) {
        $null = Read-MwbProvisioningMarker $manifest.ProductRoot $manifest.TestUserSid
        Assert-MwbLocalPath $cleanupPath
        if ((Get-FileHash -LiteralPath $cleanupPath -Algorithm SHA256).Hash -cne
            $manifest.ScriptHashes.'Remove-AutonomousHost.ps1') {
            throw 'The protected cleanup script hash changed.'
        }
        # Both trusted scripts use process-local policy, never Set-ExecutionPolicy or a UAC prompt.
        & $powerShell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
            -File $cleanupPath -StateRoot $stateRoot | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "The protected cleanup script failed with exit code $LASTEXITCODE."
        }
        $marker = Read-MwbProvisioningMarker $manifest.ProductRoot $manifest.TestUserSid
        if ($marker.Status -ne 'Removed') {
            throw 'Privileged provisioning cleanup did not finish.'
        }
    }
}
catch {
    $failures.Add($_.Exception.Message)
}
try {
    if ($task -and $task.Actions.Count -eq 1 -and $task.Actions[0].Execute -ieq $powerShell -and
        $task.Actions[0].Arguments -ceq $manifest.SetupArguments) {
        Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
    }
}
catch {
    $failures.Add($_.Exception.Message)
}
if ($failures.Count) {
    throw "MWB privileged cleanup failed: $($failures -join '; ')"
}
Write-Host 'Owned provisioning task and firewall prerequisite cleaned up.'
