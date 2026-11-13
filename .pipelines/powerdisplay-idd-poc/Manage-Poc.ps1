# Copyright (c) Microsoft Corporation
# Licensed under the MIT license. See LICENSE in the project root.

#requires -Version 7.0
param(
    [Parameter(Mandatory)][ValidateSet('Install', 'Cleanup')][string]$Action,
    [Parameter(Mandatory)][string]$StatePath,
    [Parameter(Mandatory)][string]$ResultsDirectory,
    [string]$ArtifactDirectory,
    [Parameter(Mandatory)][switch]$IsolatedGuest
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$common = Join-Path $PSScriptRoot '..\umdf\UmdfDriver.Common.ps1'
if (-not (Test-Path -LiteralPath $common)) { $common = Join-Path $PSScriptRoot 'UmdfDriver.Common.ps1' }
. $common
function Test-Administrator {
    Test-UmdfAdministrator
}

if (-not $IsolatedGuest) { throw 'Explicit isolated-guest approval is required.' }
$computer = Get-CimInstance Win32_ComputerSystem
if ($computer.Manufacturer -ne 'Microsoft Corporation' -or $computer.Model -ne 'Virtual Machine') {
    throw 'This prototype may be provisioned only in an approved Hyper-V guest.'
}
if (-not (Test-Administrator)) { throw 'Use the separate, already elevated guest provisioning channel.' }
$hardwareId = 'ROOT\PtDisplayIddPoc'
$subject = 'CN=PowerToys Isolated IDD POC'
New-Item $ResultsDirectory, (Split-Path $StatePath -Parent) -ItemType Directory -Force | Out-Null

function Get-FixtureDevices {
    @(Get-UmdfOwnedDevices -HardwareId $hardwareId -InstancePattern 'ROOT\DISPLAY\*')
}
function Save-Journal {
    Save-UmdfJournal -State $state -StatePath $StatePath
}
function Find-OwnedInfs {
    @(Get-ChildItem "$env:WINDIR\INF" -Filter 'oem*.inf' -File | Where-Object {
        $_.Name -notin $state.BeforeInfs -and (Select-String -LiteralPath $_.FullName -SimpleMatch $hardwareId -Quiet)
    } | Select-Object -ExpandProperty Name)
}

if ($Action -eq 'Cleanup') {
    if (-not (Test-Path -LiteralPath $StatePath)) { throw 'No ownership journal; nothing may be removed.' }
    $state = Read-UmdfJournal -StatePath $StatePath -HardwareId $hardwareId -Subject $subject -InfProperty BeforeInfs
    $errors = [Collections.Generic.List[string]]::new()
    Remove-UmdfDevices -Devices @(Get-FixtureDevices) -Errors $errors
    Remove-UmdfPackages -InfNames @(Find-OwnedInfs) -HardwareId $hardwareId -Errors $errors
    if (@(Get-FixtureDevices).Count) { $errors.Add('Fixture device remains.') }
    # Keep test trust while a package still needs cleanup.
    if ($errors.Count -eq 0) {
        Remove-UmdfTestTrust -State $state -Subject $subject
    }
    [ordered]@{ Removed = $errors.Count -eq 0; Errors = @($errors) } |
        ConvertTo-Json | Set-Content (Join-Path $ResultsDirectory 'cleanup.json')
    if ($errors.Count) { throw ($errors -join '; ') }
    Remove-Item -LiteralPath $StatePath
    return
}

if (-not $ArtifactDirectory) { throw 'Install requires an artifact directory.' }
if ((Test-Path -LiteralPath $StatePath) -or @(Get-FixtureDevices).Count) {
    throw 'An earlier journal/device requires cleanup.'
}
$driver = Join-Path (Resolve-Path $ArtifactDirectory).Path 'driver'
Assert-UmdfFileManifest -Directory $driver -ManifestPath (Join-Path $ArtifactDirectory 'driver-hashes.json') `
    -RequiredFiles PtDisplayIdd.dll, PtDisplayIdd.inf, PtDisplayIdd.cat, PtDisplayIdd.cer, IddProbe.exe
$hashes = @(Get-Content (Join-Path $ArtifactDirectory 'driver-hashes.json') -Raw | ConvertFrom-Json)
$production = [IO.Path]::GetFullPath((Join-Path (Resolve-Path $ArtifactDirectory).Path 'production'))
$productionHashes = @(Get-Content (Join-Path $ArtifactDirectory 'production-hashes.json') -Raw | ConvertFrom-Json)
Assert-UmdfFileManifest -Directory $production -ManifestPath (Join-Path $ArtifactDirectory 'production-hashes.json') `
    -RequiredFiles PowerDisplay.IddPoc.ProductionProbe.exe, PowerDisplay.Lib.dll -Relative -Kind 'production probe'
$certificate = Read-UmdfPublicCertificate -Path (Join-Path $driver 'PtDisplayIdd.cer') -Subject $subject
$build = Get-Content (Join-Path $ArtifactDirectory 'build.json') -Raw | ConvertFrom-Json
if ($build.Platform -ne [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()) {
    throw 'Driver architecture does not match the execution guest.'
}
$state = [pscustomobject]@{
    HardwareId = $hardwareId
    Subject = $subject
    Thumbprint = $certificate.Thumbprint
    ImportedStores = @()
    BeforeInfs = @(Get-ChildItem "$env:WINDIR\INF" -Filter 'oem*.inf' -File | Select-Object -ExpandProperty Name)
}
Save-Journal
$started = Get-Date
try {
    foreach ($store in @('Root', 'TrustedPublisher')) {
        Add-UmdfTestTrust -State $state -StatePath $StatePath -CertificatePath (Join-Path $driver 'PtDisplayIdd.cer') -Store $store
    }
    Assert-UmdfPackageSignatures -DriverDirectory $driver -Files PtDisplayIdd.dll, PtDisplayIdd.cat, IddProbe.exe -Thumbprint $certificate.Thumbprint
    & (Join-Path $driver 'IddProbe.exe') --self-test |
        Set-Content (Join-Path $ResultsDirectory 'protocol-tests.json')
    if ($LASTEXITCODE) { throw 'The architecture-matched fixture protocol tests failed.' }
    & (Join-Path $driver 'IddProbe.exe') --create
    if ($LASTEXITCODE) { throw 'Root-device creation failed.' }
    & pnputil.exe /add-driver (Join-Path $driver 'PtDisplayIdd.inf') /install
    $installExit = $LASTEXITCODE
    $deadline = (Get-Date).AddSeconds(60)
    do {
        $devices = @(Get-FixtureDevices)
        $device = if ($devices.Count -eq 1) { $devices[0] } else { $null }
        $problem = if ($device) { (Get-PnpDeviceProperty -InstanceId $device.InstanceId -KeyName DEVPKEY_Device_ProblemCode).Data } else { $null }
        if ($device -and $device.Status -eq 'OK' -and $problem -eq 0) { break }
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)
    $secureBoot = try { Confirm-SecureBootUEFI } catch { Write-Warning $_; $_.Exception.Message }
    $publishedInfs = @(Find-OwnedInfs)
    $selectedInf = if ($device -and $device.Status -eq 'OK') {
        (Get-PnpDeviceProperty -InstanceId $device.InstanceId -KeyName DEVPKEY_Device_DriverInfPath).Data
    } else { $null }
    $driverVersion = if ($device -and $device.Status -eq 'OK') {
        (Get-PnpDeviceProperty -InstanceId $device.InstanceId -KeyName DEVPKEY_Device_DriverVersion).Data
    } else { $null }
    [ordered]@{
        Build = $build
        Hashes = $hashes
        ProductionHashes = $productionHashes
        OsVersion = [Environment]::OSVersion.Version.ToString()
        SecureBoot = $secureBoot
        BootConfiguration = (& bcdedit /enum '{current}') -join "`n"
        InstallExit = $installExit
        Devices = $devices | Select-Object InstanceId, Status
        ProblemCode = $problem
        PublishedInfs = $publishedInfs
        SelectedInf = $selectedInf
        DriverVersion = $driverVersion
    } | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $ResultsDirectory 'install.json')
    if ($installExit -ne 0 -or -not $device -or $device.Status -ne 'OK' -or $problem -ne 0) {
        throw "BLOCKED: IDD load failed; install=$installExit status=$($device.Status) problem=$problem."
    }
    if ($selectedInf -notin $publishedInfs -or $driverVersion -ne $build.DriverVersion) {
        throw 'BLOCKED: Windows did not bind the newly owned architecture-matched fixture package.'
    }
    $snapshotPath = Join-Path $ResultsDirectory 'initial-snapshot.json'
    $deadline = (Get-Date).AddSeconds(30)
    do {
        $snapshotJson = & (Join-Path $driver 'IddProbe.exe') --snapshot 2> (Join-Path $ResultsDirectory 'snapshot-readiness-error.txt')
        $snapshotExit = $LASTEXITCODE
        $ready = $false
        if ($snapshotExit -eq 0) {
            $snapshot = $snapshotJson | ConvertFrom-Json
            $ready = $snapshot.Version -eq 2 -and @($snapshot.Monitors).Count -eq 2 -and
                @($snapshot.Monitors | Where-Object { -not $_.Connected }).Count -eq 0
        }
        $snapshotJson | Set-Content $snapshotPath
        if ($ready) { break }
        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)
    if (-not $ready) { throw 'BLOCKED: The fixture did not finish adapter initialization and both monitor arrivals.' }
}
catch {
    $failure = $_
    Export-UmdfDriverDiagnostics -ResultsDirectory $ResultsDirectory -Started $started -EventLogs @{
        'Microsoft-Windows-CodeIntegrity-Operational.json' = 'Microsoft-Windows-CodeIntegrity/Operational'
        'Microsoft-Windows-Kernel-PnP-Configuration.json' = 'Microsoft-Windows-Kernel-PnP/Configuration'
    }
    throw $failure
}
