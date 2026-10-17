# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$ConfigurationPath)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\EndpointSupport.ps1"
Add-Type -Path "$PSScriptRoot\NativeSupport.cs"
$config = Read-RunJson $ConfigurationPath
$provision = Read-RunJson $config.ProvisioningMarker
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
try {
    if ($identity.IsSystem -or [Security.Principal.WindowsPrincipal]::new($identity).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator) -or
        $identity.User.Value -ne $provision.TestUserSid -or
        $config.RunId -ne $provision.RunId -or
        $config.ProductRoot.TrimEnd('\') -ine $provision.ProductRoot.TrimEnd('\')) {
        throw 'Recovery fixture does not match the protected standard-user provisioning identity.'
    }
}
finally { $identity.Dispose() }
$owner = [Microsoft.MouseWithoutBorders.UITests.ProcessIdentity]::Capture($PID)
$parent = [Diagnostics.Process]::GetProcessById([int]$config.Parent.Id)
$null = $parent.Handle
if ($parent.StartTime.ToUniversalTime() -ne ([DateTime]$config.Parent.StartTimeUtc).ToUniversalTime() -or
    $parent.SessionId -ne $owner.SessionId -or $owner.ParentId -ne $parent.Id) {
    throw 'Recovery controller parent identity changed.'
}
$journal = [ordered]@{
    FormatVersion = 1; RunId = $config.RunId; RunRoot = $config.RunRoot; ControlRoot = $config.ControlRoot
    ProductRoot = $config.ProductRoot; HostName = Get-MachineName; TestUserSid = $provision.TestUserSid
    ProvisioningMarker = $config.ProvisioningMarker; RuleName = $provision.RuleName; TestProcess = $owner
    HostWorker = $null; LeasePublishers = @(); SandboxProcesses = @(); ViewerHwnd = 0
    Phase = 'Recovery fixture bootstrap'; Status = 'Running'; TimestampUtc = ''
}
function Get-RecoveryProcessRecord {
    param([int]$Id)
    $record = [Microsoft.MouseWithoutBorders.UITests.ProcessIdentity]::Capture($Id)
    [ordered]@{
        Id = $record.Id; ParentId = $record.ParentId; SessionId = $record.SessionId
        Path = $record.Path; StartTimeUtc = $record.StartTimeUtc.ToString('o')
    }
}
$journal.TestProcess = Get-RecoveryProcessRecord $PID
function Save-RecoveryControllerJournal {
    $journal.TimestampUtc = [DateTime]::UtcNow.ToString('o')
    Write-RunJson "$($config.RunRoot)\cleanup-journal.json" $journal
}
function Start-RecoveryChild {
    param([string]$Script, [string]$Arguments)
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe"
    $start.Arguments = '-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -STA -WindowStyle Hidden -File "{0}\{1}" {2}' -f $PSScriptRoot, $Script, $Arguments
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WorkingDirectory = $config.RunRoot
    [Diagnostics.Process]::Start($start)
}
Save-RecoveryControllerJournal
try {
    $publisherConfig = Join-Path $config.ControlRoot 'publisher.json'
    Write-RunJson $publisherConfig @{
        RunId = $config.RunId; Role = 'Host'; ParentId = $owner.Id
        ParentStartTimeUtc = $owner.StartTimeUtc.ToString('o'); ParentSessionId = $owner.SessionId
        HardDeadlineUtc = $config.HardDeadlineUtc; InputRoot = $config.InputRoot; InitialSequence = 1
        StopPath = "$($config.ControlRoot)\publisher-stop.json"
        OutputPath = "$($config.RunRoot)\Host-lease-publisher.json"
    }
    $publisher = Start-RecoveryChild 'Publish-Leases.ps1' ('-ConfigurationPath "{0}"' -f $publisherConfig)
    $journal.LeasePublishers = @(Get-RecoveryProcessRecord $publisher.Id)
    Save-RecoveryControllerJournal
    $worker = Start-RecoveryChild 'EndpointWorker.ps1' (
        '-InputRoot "{0}" -OutputRoot "{1}\host" -ProductRoot "{2}" -WinApp "{3}"' -f
        $config.InputRoot, $config.RunRoot, $config.ProductRoot, $config.WinApp)
    $journal.HostWorker = Get-RecoveryProcessRecord $worker.Id
    Save-RecoveryControllerJournal
    Write-RunJson "$($config.RunRoot)\controller-ready.json" $journal
    while (-not $parent.WaitForExit(500)) {
        if ([DateTime]::UtcNow -ge ([DateTime]$config.HardDeadlineUtc).ToUniversalTime()) {
            throw 'Recovery controller reached its bounded hard deadline.'
        }
    }
}
catch {
    Write-RunJson "$($config.RunRoot)\controller-failed.json" @{
        RunId = $config.RunId; Error = $_.Exception.Message
    }
    throw
}
finally { $parent.Dispose() }
