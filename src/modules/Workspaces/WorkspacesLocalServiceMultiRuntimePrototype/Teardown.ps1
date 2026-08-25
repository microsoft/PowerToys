[CmdletBinding()]
param(
    [switch]$PreserveTrustedCertificates
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$releaseRoot = Join-Path $root 'artifacts\release'
$ownershipPath = Join-Path $releaseRoot 'certificate-ownership.json'
$packageName = 'Microsoft.PowerToys.WsPuvr.ControlPlane'
$installRoot = Join-Path $env:ProgramFiles 'PowerToys\WorkspacesProtectedRuntimeControlPlanePrototype'
$storeRoot = Join-Path $env:ProgramData 'Microsoft\PowerToys\WorkspacesProtectedRuntimeControlPlanePrototype'
$endpointRegistryPath = 'HKLM:\SOFTWARE\Microsoft\PowerToys\WorkspacesProtectedRuntimeControlPlanePrototype'
$cleanupOutcomeRegistryPath = 'HKLM:\SOFTWARE\Microsoft\PowerToys\WorkspacesProtectedRuntimeControlPlanePrototypeValidation'

function Assert-True($Value, [string]$Label) {
    if (-not $Value) {
        throw "Assertion failed: $Label"
    }
}

function Test-Elevated {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Convert-ExitCodeToUInt32([int]$ExitCode) {
    return [BitConverter]::ToUInt32([BitConverter]::GetBytes($ExitCode), 0)
}

function Get-HostExecutable {
    $service = Get-CimInstance Win32_Service `
        -Filter "Name='PtPuvrHost'" `
        -ErrorAction SilentlyContinue
    if (-not $service) {
        return $null
    }
    return $service.PathName.Trim('"')
}

function Remove-Package {
    $provisioned = Get-AppxProvisionedPackage -Online |
        Where-Object DisplayName -eq $packageName |
        Select-Object -First 1
    if ($provisioned) {
        Remove-AppxProvisionedPackage `
            -Online `
            -PackageName $provisioned.PackageName `
            -AllUsers | Out-Null
    }
    foreach ($package in @(Get-AppxPackage -AllUsers -Name $packageName -ErrorAction SilentlyContinue)) {
        Remove-AppxPackage `
            -Package $package.PackageFullName `
            -AllUsers `
            -ErrorAction SilentlyContinue
    }
}

function Restore-OwnedCertificates {
    if ($PreserveTrustedCertificates -or
        -not (Test-Path -LiteralPath $ownershipPath -PathType Leaf)) {
        return
    }
    $ownership = Get-Content -LiteralPath $ownershipPath -Raw | ConvertFrom-Json
    Assert-True ($ownership.format -eq 2) 'certificate ownership format'
    foreach ($record in @($ownership.certificates)) {
        foreach ($store in @($record.stores)) {
            if (-not $store.preRunPresent) {
                $parts = $store.path -split '\\'
                Assert-True (
                    $parts.Count -eq 3 -and $parts[0] -eq 'Cert:'
                ) "certificate store path '$($store.path)'"
                $location = [System.Security.Cryptography.X509Certificates.StoreLocation]::$($parts[1])
                $certificateStore = [System.Security.Cryptography.X509Certificates.X509Store]::new(
                    $parts[2],
                    $location)
                try {
                    $certificateStore.Open(
                        [System.Security.Cryptography.X509Certificates.OpenFlags]::ReadOnly)
                    $present = $certificateStore.Certificates.Find(
                        [System.Security.Cryptography.X509Certificates.X509FindType]::FindByThumbprint,
                        $record.thumbprint,
                        $false).Count -gt 0
                }
                finally {
                    $certificateStore.Close()
                }
                if ($present) {
                    $certutilArguments = @()
                    if ($location -eq
                        [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser) {
                        $certutilArguments += '-user'
                    }
                    $certutilArguments += @(
                        '-delstore',
                        $parts[2],
                        $record.thumbprint)
                    & "$env:SystemRoot\System32\certutil.exe" @certutilArguments |
                        Out-Null
                    Assert-True ($LASTEXITCODE -eq 0) `
                        "remove certificate '$($record.thumbprint)' from '$($store.path)'"
                }
            }
        }
    }
}

function Remove-TestUser([string]$Name) {
    $user = Get-LocalUser -Name $Name -ErrorAction SilentlyContinue
    if (-not $user) {
        return
    }
    $sid = $user.SID.Value
    Remove-LocalUser -Name $Name
    $profile = Get-CimInstance Win32_UserProfile `
        -Filter "SID='$sid'" `
        -ErrorAction SilentlyContinue
    if ($profile) {
        $profile | Remove-CimInstance -ErrorAction SilentlyContinue
    }
}

if (-not (Test-Elevated)) {
    throw 'Teardown requires an elevated administrator token.'
}

$leasePath = Join-Path $storeRoot 'leases.txt'
if (Test-Path -LiteralPath $leasePath -PathType Leaf) {
    $leases = @(
        Get-Content -LiteralPath $leasePath |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    )
    Assert-True ($leases.Count -eq 0) `
        'teardown is refused while protected owner leases remain'
}
Assert-True (
    @(Get-CimInstance Win32_Service |
        Where-Object Name -like 'PtPuvrRuntime_*').Count -eq 0
) 'teardown is refused while Runtime services remain'

$hostPath = Get-HostExecutable
if ($hostPath) {
    $hostService = Get-Service -Name PtPuvrHost
    if ($hostService.Status -ne 'Stopped') {
        Stop-Service -Name PtPuvrHost -Force
        $hostService.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
    }
    if (Test-Path -LiteralPath $hostPath -PathType Leaf) {
        $process = Start-Process `
            -FilePath $hostPath `
            -ArgumentList '--package-uninstall-cleanup' `
            -Wait `
            -PassThru
        Assert-True (
            (Convert-ExitCodeToUInt32 $process.ExitCode) -eq 0
        ) 'packaged Host cleanup'
    }
}

Remove-Package
$deadline = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $deadline -and
    (Get-Service -Name PtPuvrHost -ErrorAction SilentlyContinue)) {
    Start-Sleep -Milliseconds 250
}

foreach ($path in $installRoot, $storeRoot) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}
foreach ($path in $endpointRegistryPath, $cleanupOutcomeRegistryPath) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}
foreach ($name in 'PtPuvrOwnerA', 'PtPuvrOwnerB') {
    Remove-TestUser $name
}
Restore-OwnedCertificates

Assert-True (-not (Get-Service -Name 'PtPuvr*' -ErrorAction SilentlyContinue)) `
    'all PtPuvr services removed'
Assert-True (-not (Get-AppxProvisionedPackage -Online |
    Where-Object DisplayName -eq $packageName)) 'machine provisioning removed'
Assert-True (-not (Get-AppxPackage -AllUsers -Name $packageName -ErrorAction SilentlyContinue)) `
    'all package registrations removed'
Assert-True (-not (Test-Path -LiteralPath $installRoot)) 'ordinary install root removed'
Assert-True (-not (Test-Path -LiteralPath $storeRoot)) 'protected store root removed'
Assert-True (-not (Get-LocalUser -Name PtPuvrOwnerA -ErrorAction SilentlyContinue)) `
    'owner A test user removed'
Assert-True (-not (Get-LocalUser -Name PtPuvrOwnerB -ErrorAction SilentlyContinue)) `
    'owner B test user removed'

Write-Output 'PACKAGED CONTROL-PLANE TEARDOWN PASS'
