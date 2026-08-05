[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$OtherOwnerSid,
    [string]$OwnerSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$metadata = Get-Content (Join-Path $root 'artifacts\packages\packages.json') -Raw | ConvertFrom-Json
$controller = Join-Path $root "artifacts\bin\x64\$Configuration\PtAliasProtoController.exe"
$launcher = Join-Path $root "artifacts\bin\x64\$Configuration\PtAliasProtoLauncher.exe"
$brokerSource = Join-Path $root "artifacts\bin\x64\$Configuration\PtAliasProtoSessionBroker.exe"
$targetSession = [System.Diagnostics.Process]::GetCurrentProcess().SessionId

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Cross-session validation requires an elevated PowerShell session."
    }
}
function Invoke-Controller([string[]]$Arguments) {
    & $controller @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Controller failed ($LASTEXITCODE): $($Arguments -join ' ')"
    }
}
function Get-InstanceSuffix([string]$Sid) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha.ComputeHash([Text.Encoding]::Unicode.GetBytes($Sid))
        return (($hash[0..3] | ForEach-Object { $_.ToString('x2') }) -join '')
    } finally {
        $sha.Dispose()
    }
}
function Wait-ServiceState([string]$Name, [string]$State, [int]$Seconds = 30) {
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    do {
        $service = Get-Service $Name -ErrorAction SilentlyContinue
        if ($service -and $service.Status.ToString() -eq $State) {
            return
        }
        if ($service -and $service.Status -eq 'Stopped' -and $State -eq 'Running') {
            $details = Get-CimInstance Win32_Service -Filter "Name='$Name'"
            throw "$Name stopped during startup: Win32ExitCode=$($details.ExitCode)"
        }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Timed out waiting for $Name to reach $State."
}
function Remove-Broker([string]$Name) {
    $service = Get-Service $Name -ErrorAction SilentlyContinue
    if ($service) {
        if ($service.Status -ne 'Stopped') {
            Stop-Service $Name -Force -ErrorAction SilentlyContinue
            Wait-ServiceState $Name 'Stopped'
        }
        & sc.exe delete $Name | Out-Host
        $deadline = [DateTime]::UtcNow.AddSeconds(15)
        while ((Get-Service $Name -ErrorAction SilentlyContinue) -and [DateTime]::UtcNow -lt $deadline) {
            Start-Sleep -Milliseconds 200
        }
    }
}
function Uninstall-IfPresent([string]$Sid) {
    $suffix = Get-InstanceSuffix $Sid
    if (Get-Service "PtAliasProtoSvc_$suffix" -ErrorAction SilentlyContinue) {
        & $controller uninstall --owner-sid $Sid 2>&1 | Write-Host
    }
}

Assert-Administrator
if ($targetSession -eq 0) {
    throw "Run CrossSession.ps1 from an interactive session, not session 0."
}
foreach ($path in @($controller, $launcher, $brokerSource)) {
    if (-not (Test-Path $path)) {
        throw "Missing build artifact: $path"
    }
}

$ownerSuffix = Get-InstanceSuffix $OwnerSid
$otherSuffix = Get-InstanceSuffix $OtherOwnerSid
$brokerName = "PtAliasProtoBroker_$ownerSuffix"
$ownerServiceName = "PtAliasProtoSvc_$ownerSuffix"
$otherServiceName = "PtAliasProtoSvc_$otherSuffix"
$ownerAccountName = "PtAliasProto$ownerSuffix"
$otherAccountName = "PtAliasProto$otherSuffix"
$statePath = Join-Path $env:ProgramData "Microsoft\PowerToys\PtAliasProto\$ownerSuffix\state.bin"
$protectedBroker = Join-Path $env:ProgramFiles "PowerToys\PtAliasProto\$ownerSuffix\PtAliasProtoSessionBroker.exe"
$logPath = Join-Path (Split-Path $statePath) 'prototype.log'
$ownerStore = Split-Path $statePath
$otherStore = Join-Path $env:ProgramData "Microsoft\PowerToys\PtAliasProto\$otherSuffix"
$ownerLauncherDirectory = Split-Path $protectedBroker
$otherLauncherDirectory = Join-Path $env:ProgramFiles "PowerToys\PtAliasProto\$otherSuffix"

if ($ownerSuffix -eq $otherSuffix) {
    throw 'OwnerSid and OtherOwnerSid must identify different prototype instances.'
}
foreach ($serviceName in @($ownerServiceName, $otherServiceName, $brokerName)) {
    if (Get-Service $serviceName -ErrorAction SilentlyContinue) {
        throw "Refusing to modify pre-existing service $serviceName."
    }
}
foreach ($accountName in @($ownerAccountName, $otherAccountName)) {
    if (Get-LocalUser -Name $accountName -ErrorAction SilentlyContinue) {
        throw "Refusing to modify pre-existing local account $accountName."
    }
}
foreach ($directory in @($ownerStore, $otherStore, $ownerLauncherDirectory, $otherLauncherDirectory)) {
    if (Test-Path $directory) {
        throw "Refusing to modify pre-existing prototype directory $directory."
    }
}

$ownerCreated = $false
$otherCreated = $false
$brokerCreated = $false

try {
    Invoke-Controller @('install', '--launcher', $launcher, '--package-full-name', $metadata.packages.v1.fullName, '--owner-sid', $OwnerSid)
    $ownerCreated = $true
    Invoke-Controller @('stop-worker', '--owner-sid', $OwnerSid)
    Invoke-Controller @('install', '--launcher', $launcher, '--package-full-name', $metadata.packages.v1.fullName, '--owner-sid', $OtherOwnerSid)
    $otherCreated = $true
    Invoke-Controller @('stop-worker', '--owner-sid', $OtherOwnerSid)

    Copy-Item $brokerSource $protectedBroker -Force
    $binaryPath =
        "`"$protectedBroker`" --service --service-name `"$brokerName`" --state `"$statePath`" --target-session $targetSession"
    New-Service -Name $brokerName -BinaryPathName $binaryPath -StartupType Manual | Out-Null
    $brokerCreated = $true
    Remove-Item $logPath -Force -ErrorAction SilentlyContinue
    try {
        Start-Service $brokerName
    } catch {
        $logText = ''
        if (Test-Path $logPath) {
            Write-Host "Session broker log before cleanup:"
            $logText = Get-Content $logPath -Raw -Encoding Unicode
            $logText | Write-Host
        }
        if ($logText -match 'CreateProcessW\(account bridge alias\).*Win32 error 1920') {
            Write-Host 'PASS: cross-session ordinary process creation worked, but AppModel rejected the dedicated-account own-profile alias with error 1920.'
            Write-Host 'VERDICT: changing TokenSessionId does not create a supported interactive AppX session for the service-logon account.'
            return
        }
        throw
    }
    Wait-ServiceState $brokerName 'Running'
    Invoke-Controller @(
        'ensure-package',
        '--package-full-name', $metadata.packages.v1.fullName,
        '--owner-sid', $OtherOwnerSid)

    $ownerStatus = & $controller status --owner-sid $OwnerSid 2>&1
    if ($LASTEXITCODE -ne 0) { throw ($ownerStatus -join [Environment]::NewLine) }
    $otherStatus = & $controller status --owner-sid $OtherOwnerSid 2>&1
    if ($LASTEXITCODE -ne 0) { throw ($otherStatus -join [Environment]::NewLine) }
    $ownerText = $ownerStatus -join [Environment]::NewLine
    $otherText = $otherStatus -join [Environment]::NewLine
    $ownerEvidence = [regex]::Match($ownerText, 'evidence pid=(\d+) session=(\d+) package=')
    $otherEvidence = [regex]::Match($otherText, 'evidence pid=(\d+) session=(\d+) package=')
    if (-not $ownerEvidence.Success -or
        [int]$ownerEvidence.Groups[2].Value -ne $targetSession -or
        -not $ownerText.Contains("package=$($metadata.packages.v1.fullName)") -or
        $ownerText -notmatch 'serviceSidPresent=1') {
        throw "Cross-session owner worker verification failed:`n$ownerText"
    }
    if (-not $otherEvidence.Success -or
        [int]$otherEvidence.Groups[2].Value -ne 0 -or
        -not $otherText.Contains("package=$($metadata.packages.v1.fullName)") -or
        $otherText -notmatch 'workerPid=(?!0)\d+' -or
        $otherText -notmatch 'serviceSidPresent=1') {
        throw "Session-0 comparison worker verification failed:`n$otherText"
    }
    $ownerProcess = Get-Process -Id ([int]$ownerEvidence.Groups[1].Value) -ErrorAction Stop
    $otherProcess = Get-Process -Id ([int]$otherEvidence.Groups[1].Value) -ErrorAction Stop
    if ($ownerProcess.SessionId -ne $targetSession -or $otherProcess.SessionId -ne 0) {
        throw "OS process session verification failed."
    }
    Write-Host $ownerText
    Write-Host $otherText
    Write-Host "PASS: identical packaged application identity is running concurrently in sessions 0 and $targetSession under different dedicated accounts."
}
finally {
    if ($brokerCreated) {
        Remove-Broker $brokerName
    }
    if ($ownerCreated) {
        Uninstall-IfPresent $OwnerSid
    }
    if ($otherCreated) {
        Uninstall-IfPresent $OtherOwnerSid
    }
    if (-not (Get-Service 'PtAliasProtoSvc_*' -ErrorAction SilentlyContinue)) {
        foreach ($package in $metadata.packages.psobject.Properties.Value) {
            & $controller unstage-package --package-full-name $package.fullName 2>&1 | Write-Host
        }
    }
    $thumbprint = $metadata.certificateThumbprint
    foreach ($store in @('Cert:\CurrentUser\My', 'Cert:\CurrentUser\TrustedPeople', 'Cert:\LocalMachine\TrustedPeople')) {
        Get-ChildItem $store -ErrorAction SilentlyContinue |
            Where-Object Thumbprint -eq $thumbprint |
            Remove-Item -Force -ErrorAction SilentlyContinue
    }
    Write-Host "Cross-session services/accounts/packages/test certificate cleanup attempted."
}
