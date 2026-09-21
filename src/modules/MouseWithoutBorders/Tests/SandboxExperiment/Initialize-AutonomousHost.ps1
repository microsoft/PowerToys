# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Creates the narrowly scoped firewall prerequisite for the unattended MWB test.
.DESCRIPTION
Run in an already privileged provisioning context inside the test VM or CI agent,
before starting the standard-user MSTest process. Never requests elevation or
enables Windows features, remoting, service mode, or broad firewall access.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProductRoot,
    [Parameter(Mandatory = $true)][string]$TestUser,
    [Parameter(Mandatory = $true)][string]$GuestArchivePath,
    [ValidateSet('Legacy', 'WinApp')][string]$SandboxBackend = 'Legacy',
    [string]$SandboxWinAppPath,
    [string]$InterfaceAlias = 'vEthernet (Default Switch)',
    [string]$StateRoot = 'C:\ProgramData\PowerToysMwbExperiment',
    [Guid]$RunId = [Guid]::NewGuid(),
    [ValidateRange(30, 1800)][int]$NetworkTimeoutSeconds = 900
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$preparationWatch = [Diagnostics.Stopwatch]::StartNew()
function Write-ProvisioningPhase {
    param([string]$Phase)
    Write-Host ('MWB provisioning: {0} ({1:N1}s)' -f $Phase, $preparationWatch.Elapsed.TotalSeconds)
}
Write-ProvisioningPhase 'Validating staged payload'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
try {
    if (-not (New-Object Security.Principal.WindowsPrincipal($identity)).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'BLOCKED_INFRASTRUCTURE: firewall provisioning requires an already elevated setup context.'
    }
}
finally { $identity.Dispose() }

$product = (Resolve-Path -LiteralPath $ProductRoot -ErrorAction Stop).Path.TrimEnd('\')
$executable = Join-Path $product 'PowerToys.MouseWithoutBorders.exe'
$library = Join-Path $product 'PowerToys.MouseWithoutBorders.dll'
$guestArchive = (Resolve-Path -LiteralPath $GuestArchivePath -ErrorAction Stop).Path
if ($guestArchive -notmatch '^[A-Za-z]:\\' -or -not (Test-Path -LiteralPath $guestArchive -PathType Leaf)) {
    throw 'A staged local product archive is required for the Sandbox payload.'
}
if ($product -notmatch '^[A-Za-z]:\\' -or -not (Test-Path -LiteralPath $executable -PathType Leaf) -or
    -not (Test-Path -LiteralPath $library -PathType Leaf)) {
    throw 'A local staged MWB executable and managed assembly are required.'
}
$ancestor = $product
while ($ancestor) {
    if ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw "Refusing a reparse-point product path: $ancestor"
    }
    $ancestor = Split-Path $ancestor
}
Write-ProvisioningPhase 'Checking staged process inventory'
if (@(Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -ieq $executable }).Count) {
    throw 'Provision firewall rules before starting the staged MWB process.'
}
Write-ProvisioningPhase 'Checking Sandbox feature'
$feature = Get-WindowsOptionalFeature -Online -FeatureName Containers-DisposableClientVM
if ($feature.State -ne 'Enabled') {
    throw "BLOCKED_INFRASTRUCTURE: Windows Sandbox is $($feature.State); image setup/reboot is required."
}
$sandboxWinAppHash = $null
if ($SandboxBackend -eq 'WinApp') {
    Write-ProvisioningPhase 'Validating Sandbox preview'
    $windowsBuild = [int](Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion').CurrentBuild
    if ($windowsBuild -lt 26100) { throw 'BLOCKED_INFRASTRUCTURE: WinApp Sandbox requires Windows 11 24H2 or newer; use Legacy on Win10.' }
    if ([string]::IsNullOrWhiteSpace($SandboxWinAppPath) -or
        $SandboxWinAppPath -notmatch '^[A-Za-z]:\\' -or
        [IO.Path]::GetFileName($SandboxWinAppPath) -ine 'winapp.exe' -or
        -not (Test-Path -LiteralPath $SandboxWinAppPath -PathType Leaf)) {
        throw 'BLOCKED_INFRASTRUCTURE: stage the Sandbox-capable preview winapp.exe explicitly for the WinApp backend.'
    }
    $SandboxWinAppPath = (Resolve-Path -LiteralPath $SandboxWinAppPath).Path
    $ancestor = $SandboxWinAppPath
    while ($ancestor) {
        if ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw 'The staged Sandbox winapp must not be reached through a reparse point.'
        }
        $ancestor = Split-Path -Parent $ancestor
    }
    $sandboxWinAppHash = (Get-FileHash -LiteralPath $SandboxWinAppPath -Algorithm SHA256).Hash
}
elseif (-not [string]::IsNullOrWhiteSpace($SandboxWinAppPath)) {
    throw 'A Sandbox preview CLI is only accepted for the explicit WinApp backend.'
}
if ((Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending') -or
    (Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired')) {
    throw 'BLOCKED_INFRASTRUCTURE: complete the pending image reboot before provisioning the test.'
}
$userSid = (New-Object Security.Principal.NTAccount($TestUser)).Translate(
    [Security.Principal.SecurityIdentifier])
$stateDirectory = [IO.Path]::GetFullPath($StateRoot).TrimEnd('\')
$marker = Join-Path $stateDirectory 'host-provisioning.json'
if ($stateDirectory -notmatch '^[A-Za-z]:\\') { throw 'A local provisioning directory is required.' }
if (Test-Path -LiteralPath $stateDirectory) {
    if ((Get-Item -LiteralPath $stateDirectory).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw 'The provisioning directory cannot be a reparse point.'
    }
    if (-not (Test-Path -LiteralPath $marker) -or
        ([IO.File]::ReadAllText($marker) | ConvertFrom-Json).Status -ne 'Removed') {
        throw 'An earlier provisioning record needs cleanup before reuse.'
    }
}
Write-ProvisioningPhase 'Checking firewall conflicts'
$applicationFilters = @(Get-NetFirewallApplicationFilter -PolicyStore ActiveStore | Where-Object {
    $_.Program -ieq $executable
})
$conflicting = @($applicationFilters | Get-NetFirewallRule | Where-Object {
    $_.Enabled -eq 'True' -and $_.Direction -eq 'Inbound'
})
if ($conflicting.Count) {
    throw 'BLOCKED_INFRASTRUCTURE: existing firewall rules for this payload invalidate a fresh baseline; use a fresh payload path or clean image.'
}

$ruleName = "PowerToys.Mwb.UITest.$RunId"
Write-ProvisioningPhase 'Fingerprinting payload'
$state = [ordered]@{
    FormatVersion = 1
    RunId = $RunId.ToString()
    ProductRoot = $product
    Executable = $executable
    ExecutableSha256 = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash
    LibrarySha256 = (Get-FileHash -LiteralPath $library -Algorithm SHA256).Hash
    GuestArchivePath = $guestArchive
    GuestArchiveSha256 = (Get-FileHash -LiteralPath $guestArchive -Algorithm SHA256).Hash
    SandboxBackend = $SandboxBackend
    SandboxWinAppPath = $SandboxWinAppPath
    SandboxWinAppSha256 = $sandboxWinAppHash
    RuleName = $ruleName
    HostAddress = ''
    InnerSubnet = ''
    InterfaceAlias = $InterfaceAlias
    TestUserSid = $userSid.Value
    ProvisionerProcessId = $PID
    ProvisionerStartTimeUtc = (Get-Process -Id $PID).StartTime.ToUniversalTime().ToString('o')
    ProvisionerExecutable = (Get-Process -Id $PID).Path
    PreparedUtc = [DateTime]::UtcNow.ToString('o')
    Status = 'WaitingForSandbox'
}
# Complete the potentially slow hashes before creating a directory that requires
# an ownership marker for recovery.
$acl = New-Object Security.AccessControl.DirectorySecurity
$acl.SetAccessRuleProtection($true, $false)
$inheritance = [Security.AccessControl.InheritanceFlags]'ContainerInherit, ObjectInherit'
foreach ($sid in @('S-1-5-18', 'S-1-5-32-544')) {
    $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new(
        [Security.Principal.SecurityIdentifier]::new($sid), 'FullControl', $inheritance, 'None', 'Allow'))
}
$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new(
    $userSid, 'ReadAndExecute', $inheritance, 'None', 'Allow'))
$null = New-Item -ItemType Directory -Path $stateDirectory -Force
Set-Acl -LiteralPath $stateDirectory -AclObject $acl
[IO.File]::WriteAllText($marker, ($state | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
Write-ProvisioningPhase 'Waiting for Sandbox network'
try {
    $deadline = [DateTime]::UtcNow.AddSeconds($NetworkTimeoutSeconds)
    do {
        $addresses = @(Get-NetIPAddress -AddressFamily IPv4 | Where-Object {
            $_.InterfaceAlias -eq $InterfaceAlias -and $_.InterfaceAlias -like 'vEthernet*' -and
            $_.IPAddress -notmatch '^(127\.|169\.254\.)' -and
            $_.PrefixLength -ge 16 -and $_.PrefixLength -le 30
        })
        if ($addresses.Count -gt 1) { throw 'Sandbox network identity is ambiguous.' }
        if ($addresses.Count -eq 1) { break }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($addresses.Count -ne 1) { throw 'BLOCKED_INFRASTRUCTURE: Sandbox did not create its inner network before the deadline.' }
    $address = $addresses[0]
    $octets = [Net.IPAddress]::Parse($address.IPAddress).GetAddressBytes()
    for ($index = 0; $index -lt 4; $index++) {
        $bits = [Math]::Max(0, [Math]::Min(8, $address.PrefixLength - ($index * 8)))
        $mask = if ($bits -eq 0) { 0 } else { (255 -shl (8 - $bits)) -band 255 }
        $octets[$index] = $octets[$index] -band $mask
    }
    $subnet = ([Net.IPAddress]::new($octets)).ToString() + '/' + $address.PrefixLength
    $null = New-NetFirewallRule -Name $ruleName -DisplayName $ruleName -Direction Inbound -Action Allow `
        -Enabled True -Profile Any -Program $executable -Protocol TCP -LocalPort 15100,15101 `
        -RemoteAddress $subnet -InterfaceAlias $address.InterfaceAlias -EdgeTraversalPolicy Block
    $rule = Get-NetFirewallRule -Name $ruleName -ErrorAction Stop
    if ($rule.Enabled -ne 'True' -or $rule.Action -ne 'Allow') {
        throw 'The test firewall rule was not enabled; inspect effective policy.'
    }
    $state.Status = 'Ready'
    $state.HostAddress = $address.IPAddress
    $state.InnerSubnet = $subnet
    [IO.File]::WriteAllText($marker, ($state | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
}
catch {
    $state.Status = 'Failed'
    $state.Error = $_.ToString()
    [IO.File]::WriteAllText($marker, ($state | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
    throw
}
[pscustomobject]@{ Status = 'Ready'; ProvisioningMarker = $marker; RuleName = $ruleName; InnerSubnet = $subnet }
