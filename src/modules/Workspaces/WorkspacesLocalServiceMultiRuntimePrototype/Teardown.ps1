[CmdletBinding()]
param(
    [string]$FirstOwnerSid = 'S-1-5-21-1959867211-618815089-525172305-1122',
    [string]$SecondOwnerSid = 'S-1-5-21-1959867211-618815089-525172305-1123',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$PreserveTrustedCertificates
)

$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Teardown requires an elevated administrator token.'
}

function Get-RuntimeServiceName([string]$ownerSid) {
    $bytes = [Text.Encoding]::Unicode.GetBytes($ownerSid)
    $digest = [Security.Cryptography.SHA256]::HashData($bytes)
    return 'PtPuvrRuntime_' + (
        [Convert]::ToHexString($digest).ToLowerInvariant().Substring(0, 16))
}

$newPackageFullNames = @(
    'Microsoft.PowerToys.WsPuvr.Runtime1_1.0.0.0_x64__fcbv3b023fanj',
    'Microsoft.PowerToys.WsPuvr.Runtime2_2.0.0.0_x64__fcbv3b023fanj'
)
$legacyPackageFullNames = @(
    'Microsoft.PowerToys.WsPuvr.Updater_5.0.0.0_x64__t8ed0av59w5q6',
    'Microsoft.PowerToys.WsPuvr.Runtime1_1.0.0.0_x64__t8ed0av59w5q6',
    'Microsoft.PowerToys.WsPuvr.Runtime2_2.0.0.0_x64__t8ed0av59w5q6'
)
$exactPackageFullNames = @($newPackageFullNames + $legacyPackageFullNames)

function Remove-ExactPackage([string]$packageFullName) {
    & $controller `
        --remove-package `
        --package-full-name $packageFullName
    $exitCode = $LASTEXITCODE
    $packageDirectory = Join-Path `
        (Join-Path $env:ProgramFiles 'WindowsApps') `
        $packageFullName
    for ($attempt = 0; $attempt -lt 40 -and
         (Test-Path -LiteralPath $packageDirectory); $attempt++) {
        Start-Sleep -Milliseconds 250
    }
    if ($exitCode -ne 0 -or (Test-Path -LiteralPath $packageDirectory)) {
        throw "Exact package removal failed for ${packageFullName}: controller exit $exitCode"
    }
}

$controller = Join-Path `
    $PSScriptRoot `
    "artifacts\bin\x64\$Configuration\PtPuvrController.exe"
$updater = Get-Service -Name PtPuvrUpdater -ErrorAction SilentlyContinue
if ($updater -and $updater.Status -ne 'Running') {
    try {
        Start-Service -Name PtPuvrUpdater
        $updater.WaitForStatus('Running', [TimeSpan]::FromSeconds(30))
    }
    catch {
        Write-Warning "Could not start updater for managed cleanup: $($_.Exception.Message)"
    }
}

if ((Get-Service -Name PtPuvrUpdater -ErrorAction SilentlyContinue).Status -eq 'Running' -and
    (Test-Path -LiteralPath $controller -PathType Leaf)) {
    foreach ($owner in $FirstOwnerSid, $SecondOwnerSid) {
        & $controller --cleanup --owner-sid $owner
        if ($LASTEXITCODE -ne 0 -and
            $LASTEXITCODE -ne 1060 -and
            $LASTEXITCODE -ne 1168) {
            Write-Warning "Updater cleanup failed for ${owner}: $LASTEXITCODE"
        }
    }
}

foreach ($owner in $FirstOwnerSid, $SecondOwnerSid) {
    $serviceName = Get-RuntimeServiceName $owner
    $runtime = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if ($runtime) {
        if ($runtime.Status -ne 'Stopped') {
            Stop-Service -Name $serviceName -Force
            $runtime.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
        }
        sc.exe delete $serviceName | Out-Host
    }
}

$updater = Get-Service -Name PtPuvrUpdater -ErrorAction SilentlyContinue
if ($updater) {
    if ($updater.Status -ne 'Stopped') {
        Stop-Service -Name PtPuvrUpdater -Force
        $updater.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
    }
    sc.exe delete PtPuvrUpdater | Out-Host
}

foreach ($packageFullName in $exactPackageFullNames) {
    Remove-ExactPackage $packageFullName
}

$installRoot = Join-Path `
    $env:ProgramFiles `
    'PowerToys\WorkspacesUnpackagedUpdaterVirtualRuntimePrototype'
$storeRoot = Join-Path `
    $env:ProgramData `
    'Microsoft\PowerToys\WorkspacesUnpackagedUpdaterVirtualRuntimePrototype'
$legacyStoreRoot = Join-Path `
    $env:ProgramData `
    'Microsoft\PowerToys\WorkspacesPackagedUpdaterVirtualRuntimePrototype'
Remove-Item -LiteralPath $installRoot -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $storeRoot -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $legacyStoreRoot -Recurse -Force -ErrorAction SilentlyContinue

if (-not $PreserveTrustedCertificates) {
    $certificateSubjects = @(
        'CN=PowerToys Workspaces Unpackaged Updater Virtual Runtime Prototype Test',
        'CN=PowerToys Workspaces Packaged Updater Virtual Runtime Prototype Test'
    )
    foreach ($store in 'Cert:\CurrentUser\My',
                         'Cert:\CurrentUser\TrustedPeople',
                         'Cert:\LocalMachine\My',
                         'Cert:\LocalMachine\TrustedPeople') {
        $certificates = @(
            Get-ChildItem $store -ErrorAction SilentlyContinue |
                Where-Object { $_.Subject -in $certificateSubjects })
        foreach ($certificate in $certificates) {
            Remove-Item -LiteralPath $certificate.PSPath -Force
        }
    }
}

$remainingServices = @(Get-Service -Name 'PtPuvr*' -ErrorAction SilentlyContinue)
$remainingCertificates = @()
if (-not $PreserveTrustedCertificates) {
    $remainingCertificates = @(
        foreach ($store in 'Cert:\CurrentUser\My',
                             'Cert:\CurrentUser\TrustedPeople',
                             'Cert:\LocalMachine\My',
                             'Cert:\LocalMachine\TrustedPeople') {
            Get-ChildItem $store -ErrorAction SilentlyContinue |
                Where-Object { $_.Subject -in $certificateSubjects }
        }
    )
}
$remainingPackageDirectories = @(
    foreach ($packageFullName in $exactPackageFullNames) {
        $packageDirectory = Join-Path `
            (Join-Path $env:ProgramFiles 'WindowsApps') `
            $packageFullName
        if (Test-Path -LiteralPath $packageDirectory) {
            $packageDirectory
        }
    }
)
if ($remainingServices.Count -ne 0 -or
    $remainingCertificates.Count -ne 0 -or
    $remainingPackageDirectories.Count -ne 0 -or
    (Test-Path -LiteralPath $installRoot) -or
    (Test-Path -LiteralPath $storeRoot) -or
    (Test-Path -LiteralPath $legacyStoreRoot)) {
    throw 'Teardown verification failed; prototype state remains.'
}
$scope = if ($PreserveTrustedCertificates) {
    'services, packages, install roots, or stores'
}
else {
    'services, packages, install roots, stores, or trusted certificates'
}
Write-Host "Teardown PASS: no prototype $scope remain."
