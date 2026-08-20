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
    return 'PtPuvrRuntime_' + ([Convert]::ToHexString($digest).ToLowerInvariant().Substring(0, 16))
}

$controller = Join-Path $PSScriptRoot "artifacts\bin\x64\$Configuration\PtPuvrController.exe"
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
    (Test-Path $controller)) {
    foreach ($owner in $FirstOwnerSid, $SecondOwnerSid) {
        & $controller --cleanup --owner-sid $owner
        if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne 1060 -and $LASTEXITCODE -ne 1168) {
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
if ($updater -and $updater.Status -ne 'Stopped') {
    Stop-Service -Name PtPuvrUpdater -Force
    $updater.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
}

$packageNames = @(
    'Microsoft.PowerToys.WsPuvr.Updater',
    'Microsoft.PowerToys.WsPuvr.Runtime1',
    'Microsoft.PowerToys.WsPuvr.Runtime2'
)
foreach ($packageName in $packageNames) {
    Get-AppxPackage -AllUsers -Name $packageName -ErrorAction SilentlyContinue |
        ForEach-Object {
            try {
                Remove-AppxPackage -Package $_.PackageFullName -AllUsers -ErrorAction Stop
            }
            catch {
                Write-Warning "Package removal failed for $($_.PackageFullName): $($_.Exception.Message)"
            }
        }
}

$storeRoot = Join-Path $env:ProgramData 'Microsoft\PowerToys\WorkspacesPackagedUpdaterVirtualRuntimePrototype'
Remove-Item -LiteralPath $storeRoot -Recurse -Force -ErrorAction SilentlyContinue
if (-not $PreserveTrustedCertificates) {
    $certificateSubject = 'CN=PowerToys Workspaces Packaged Updater Virtual Runtime Prototype Test'
    foreach ($store in 'Cert:\CurrentUser\My', 'Cert:\CurrentUser\TrustedPeople',
             'Cert:\LocalMachine\My', 'Cert:\LocalMachine\TrustedPeople') {
        $certificates = @(Get-ChildItem $store -ErrorAction SilentlyContinue |
            Where-Object { $_.Subject -eq $certificateSubject })
        foreach ($certificate in $certificates) {
            Remove-Item -LiteralPath $certificate.PSPath -Force
        }
    }
}

$remainingServices = @(Get-Service -Name 'PtPuvr*' -ErrorAction SilentlyContinue)
$remainingPackages = @(
    foreach ($packageName in $packageNames) {
        Get-AppxPackage -AllUsers -Name $packageName -ErrorAction SilentlyContinue
    }
)
if ($remainingServices.Count -ne 0 -or $remainingPackages.Count -ne 0 -or (Test-Path $storeRoot)) {
    throw 'Teardown verification failed; prototype state remains.'
}
$scope = if ($PreserveTrustedCertificates) {
    'services, packages, or stores'
}
else {
    'services, packages, stores, or trusted certificates'
}
Write-Host "Teardown PASS: no prototype $scope remain."
