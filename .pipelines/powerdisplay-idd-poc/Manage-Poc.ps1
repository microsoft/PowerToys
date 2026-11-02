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
if (-not $IsolatedGuest) { throw 'Explicit isolated-guest approval is required.' }
$computer = Get-CimInstance Win32_ComputerSystem
if ($computer.Manufacturer -ne 'Microsoft Corporation' -or $computer.Model -ne 'Virtual Machine') {
    throw 'This prototype may be provisioned only in an approved Hyper-V guest.'
}
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
try {
    if (-not ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Use the separate, already elevated guest provisioning channel.'
    }
}
finally { $identity.Dispose() }
$hardwareId = 'ROOT\PtDisplayIddPoc'
$subject = 'CN=PowerToys Isolated IDD POC'
New-Item $ResultsDirectory, (Split-Path $StatePath -Parent) -ItemType Directory -Force | Out-Null

function Get-FixtureDevices {
    @(Get-PnpDevice -PresentOnly | Where-Object InstanceId -Like 'ROOT\DISPLAY\*' | Where-Object {
        $hardwareId -in (Get-PnpDeviceProperty -InstanceId $_.InstanceId -KeyName DEVPKEY_Device_HardwareIds).Data
    })
}
function Save-Journal {
    $temporary = "$StatePath.tmp"
    $state | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $temporary
    Move-Item -LiteralPath $temporary -Destination $StatePath -Force
}
function Find-OwnedInfs {
    @(Get-ChildItem "$env:WINDIR\INF" -Filter 'oem*.inf' -File | Where-Object {
        $_.Name -notin $state.BeforeInfs -and (Select-String -LiteralPath $_.FullName -SimpleMatch $hardwareId -Quiet)
    } | Select-Object -ExpandProperty Name)
}

if ($Action -eq 'Cleanup') {
    if (-not (Test-Path -LiteralPath $StatePath)) { throw 'No ownership journal; nothing may be removed.' }
    $state = Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json
    if ($state.HardwareId -ne $hardwareId -or $state.Subject -ne $subject -or
        $state.Thumbprint -notmatch '^[A-Fa-f0-9]{40}$' -or
        @($state.ImportedStores | Where-Object { $_ -notin @('Root', 'TrustedPublisher') }).Count -or
        @($state.BeforeInfs | Where-Object { $_ -notmatch '^oem[0-9]+\.inf$' }).Count) {
        throw 'Invalid ownership journal.'
    }
    $errors = [Collections.Generic.List[string]]::new()
    foreach ($device in Get-FixtureDevices) {
        & pnputil.exe /remove-device $device.InstanceId
        if ($LASTEXITCODE -notin @(0, 3010)) { $errors.Add("Device removal failed: $LASTEXITCODE") }
    }
    foreach ($name in Find-OwnedInfs) {
        & pnputil.exe /delete-driver $name /uninstall /force
        if ($LASTEXITCODE -notin @(0, 3010) -or (Test-Path "$env:WINDIR\INF\$name")) {
            $errors.Add("Package remains: $name")
        }
    }
    if (@(Get-FixtureDevices).Count) { $errors.Add('Fixture device remains.') }
    # Keep test trust while a package still needs cleanup.
    if ($errors.Count -eq 0) {
        foreach ($store in $state.ImportedStores) {
            $path = "Cert:\LocalMachine\$store\$($state.Thumbprint)"
            if (Test-Path $path) {
                if ((Get-Item $path).Subject -ne $subject) { throw 'Refusing to remove unrelated trust.' }
                Remove-Item $path
            }
        }
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
$hashes = @(Get-Content (Join-Path $ArtifactDirectory 'driver-hashes.json') -Raw | ConvertFrom-Json)
foreach ($required in @('PtDisplayIdd.dll', 'PtDisplayIdd.inf', 'PtDisplayIdd.cat', 'PtDisplayIdd.cer', 'IddProbe.exe')) {
    $entry = @($hashes | Where-Object { (Split-Path $_.Path -Leaf) -eq $required })
    if ($entry.Count -ne 1 -or (Get-FileHash (Join-Path $driver $required) -Algorithm SHA256).Hash -ne $entry[0].Hash) {
        throw "Missing or mismatched artifact hash: $required"
    }
}
$production = Join-Path (Resolve-Path $ArtifactDirectory).Path 'production'
$productionHashes = @(Get-Content (Join-Path $ArtifactDirectory 'production-hashes.json') -Raw | ConvertFrom-Json)
if (@($productionHashes | Where-Object Path -EQ 'PowerDisplay.IddPoc.ProductionProbe.exe').Count -ne 1 -or
    @($productionHashes | Where-Object Path -EQ 'PowerDisplay.Lib.dll').Count -ne 1) {
    throw 'The production probe manifest is incomplete.'
}
foreach ($file in $productionHashes) {
    $path = [IO.Path]::GetFullPath((Join-Path $production $file.Path))
    if (-not $path.StartsWith("$production\", [StringComparison]::OrdinalIgnoreCase) -or
        (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.Hash) {
        throw "Missing or mismatched production probe hash: $($file.Path)"
    }
}
$certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new((Join-Path $driver 'PtDisplayIdd.cer'))
if ($certificate.Subject -ne $subject -or $certificate.NotAfter -lt (Get-Date) -or $certificate.HasPrivateKey) {
    throw 'Unexpected, expired, or non-public test certificate.'
}
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
        if (-not (Test-Path "Cert:\LocalMachine\$store\$($certificate.Thumbprint)")) {
            $state.ImportedStores += $store
            Save-Journal
            & certutil.exe -f -addstore $store (Join-Path $driver 'PtDisplayIdd.cer')
            if ($LASTEXITCODE) { throw "Certificate import failed: $store" }
        }
    }
    foreach ($file in @('PtDisplayIdd.dll', 'PtDisplayIdd.cat', 'IddProbe.exe')) {
        $signature = Get-AuthenticodeSignature (Join-Path $driver $file)
        if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) {
            throw "Invalid package signature: $file"
        }
    }
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
        PublishedInfs = @(Find-OwnedInfs)
    } | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $ResultsDirectory 'install.json')
    if ($installExit -ne 0 -or -not $device -or $device.Status -ne 'OK' -or $problem -ne 0) {
        throw "BLOCKED: IDD load failed; install=$installExit status=$($device.Status) problem=$problem."
    }
    & (Join-Path $driver 'IddProbe.exe') --snapshot | Set-Content (Join-Path $ResultsDirectory 'initial-snapshot.json')
    if ($LASTEXITCODE) { throw 'BLOCKED: Read-only callback evidence interface unavailable.' }
}
catch {
    $failure = $_
    foreach ($log in @('Microsoft-Windows-CodeIntegrity/Operational', 'Microsoft-Windows-Kernel-PnP/Configuration')) {
        try {
            Get-WinEvent -FilterHashtable @{ LogName = $log; StartTime = $started } -ErrorAction Stop |
                Select-Object Id, TimeCreated, Message | ConvertTo-Json -Depth 4 |
                Set-Content (Join-Path $ResultsDirectory (($log -replace '[/\\]', '-') + '.json'))
        }
        catch { Write-Warning "Diagnostic log unavailable: $log`: $_" }
    }
    Get-Content "$env:WINDIR\INF\setupapi.dev.log" -Tail 350 |
        Set-Content (Join-Path $ResultsDirectory 'setupapi-tail.log')
    throw $failure
}
