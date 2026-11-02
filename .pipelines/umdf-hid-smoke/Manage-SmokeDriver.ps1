# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#requires -Version 7.0
param(
    [Parameter(Mandatory)][ValidateSet('Install', 'Cleanup')][string]$Action,
    [Parameter(Mandatory)][string]$StatePath,
    [Parameter(Mandatory)][string]$ResultsDirectory,
    [string]$ArtifactDirectory
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$hardwareId = 'ROOT\PtUmdfHidPrototype'
$subject = 'CN=PowerToys Isolated UMDF CI Smoke'
New-Item $ResultsDirectory, (Split-Path $StatePath -Parent) -ItemType Directory -Force | Out-Null
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
try {
    if (-not ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Driver provisioning requires an already elevated agent; no UAC is attempted.'
    }
}
finally {
    $identity.Dispose()
}

function Get-OwnedDevices {
    @(Get-PnpDevice -PresentOnly -ErrorAction Stop | Where-Object InstanceId -Like 'ROOT\HIDCLASS\*' | Where-Object {
        $ids = (Get-PnpDeviceProperty -InstanceId $_.InstanceId -KeyName DEVPKEY_Device_HardwareIds -ErrorAction Stop).Data
        $hardwareId -in $ids
    })
}

function Save-State {
    $state | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $StatePath
}

function Test-MachineCertificate {
    param([ValidateSet('Root', 'TrustedPublisher')][string]$Store, [string]$Thumbprint)
    $machineStore = [Security.Cryptography.X509Certificates.X509Store]::new(
        $Store, [Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)
    try {
        $machineStore.Open([Security.Cryptography.X509Certificates.OpenFlags]::ReadOnly -bor [Security.Cryptography.X509Certificates.OpenFlags]::OpenExistingOnly)
        $machineStore.Certificates.Thumbprint -contains $Thumbprint
    }
    finally { $machineStore.Close() }
}

function Write-TrustDiagnostics {
    param([string]$FailedStore, [string]$Failure)
    $command = Get-Command Import-Certificate
    $currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
    try {
        $context = [ordered]@{
            PSVersion = $PSVersionTable.PSVersion.ToString()
            PSEdition = $PSVersionTable.PSEdition
            ProcessArchitecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
            OsArchitecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
            OsVersion = [Environment]::OSVersion.Version.ToString()
            IsSystem = $currentIdentity.IsSystem
            IsAdmin = ([Security.Principal.WindowsPrincipal]::new($currentIdentity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
            ImpersonationLevel = $currentIdentity.ImpersonationLevel.ToString()
            SessionId = (Get-Process -Id $PID).SessionId
            ImportCommandType = $command.CommandType.ToString()
            ImportModule = $command.Module.Path
            ImportImplementation = 'certutil -f -addstore (initializes an absent machine store)'
            FailedStore = $FailedStore
            Failure = $Failure
            Stores = @()
            Policies = @()
            RegistryAccess = @()
        }
    }
    finally { $currentIdentity.Dispose() }
    foreach ($name in @('Root', 'TrustedPublisher')) {
        $store = [Security.Cryptography.X509Certificates.X509Store]::new($name, [Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)
        $openError = $null
        try {
            $store.Open([Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite -bor [Security.Cryptography.X509Certificates.OpenFlags]::OpenExistingOnly)
        }
        catch {
            $openError = $_.Exception.ToString()
        }
        finally { $store.Close() }
        $context.Stores += [pscustomobject]@{
            Name = $name
            ReadWriteOpenError = $openError
            CertificatePresent = Test-MachineCertificate -Store $name -Thumbprint $certificate.Thumbprint
        }
    }
    foreach ($path in @(
        'HKLM:\SOFTWARE\Policies\Microsoft\SystemCertificates',
        'HKLM:\SOFTWARE\Policies\Microsoft\SystemCertificates\TrustedPublisher',
        'HKLM:\SOFTWARE\Policies\Microsoft\SystemCertificates\TrustedPublisher\Safer',
        'HKLM:\SOFTWARE\Policies\Microsoft\SystemCertificates\Root',
        'HKLM:\SOFTWARE\Microsoft\SystemCertificates\TrustedPublisher\Safer',
        'HKLM:\SOFTWARE\Microsoft\SystemCertificates\Root\ProtectedRoots'
    )) {
        if (Test-Path $path) {
            try {
                $values = Get-ItemProperty $path | Select-Object Flags, SaferFlags, AuthRootAutoUpdateFlags, DisableRootAutoUpdate
                $context.Policies += [pscustomobject]@{ Path = $path; Values = $values }
            }
            catch {
                Write-Warning "Could not read certificate policy '$path': $_"
                $context.Policies += [pscustomobject]@{ Path = $path; ReadError = $_.Exception.ToString() }
            }
        }
    }
    $machineRegistry = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
        [Microsoft.Win32.RegistryHive]::LocalMachine, [Microsoft.Win32.RegistryView]::Registry64)
    try {
        foreach ($key in @(
            'SOFTWARE\Microsoft\SystemCertificates',
            'SOFTWARE\Microsoft\SystemCertificates\Root',
            'SOFTWARE\Microsoft\SystemCertificates\Root\Certificates',
            'SOFTWARE\Microsoft\SystemCertificates\TrustedPublisher',
            'SOFTWARE\Microsoft\SystemCertificates\TrustedPublisher\Certificates'
        )) {
            $access = [ordered]@{
                Key = "HKLM:\$key"
                Exists = Test-Path "HKLM:\$key"
                OwnerSid = $null
                Dacl = $null
                RequestedRights = 'ReadKey, CreateSubKey, SetValue (open only; no registry mutation)'
                WritableOpenError = $null
                ReadError = $null
            }
            if ($access.Exists) {
                try {
                    $acl = Get-Acl "HKLM:\$key"
                    $access.OwnerSid = $acl.GetOwner([Security.Principal.SecurityIdentifier]).Value
                    $access.Dacl = $acl.GetSecurityDescriptorSddlForm([Security.AccessControl.AccessControlSections]::Access)
                }
                catch { $access.ReadError = $_.Exception.ToString() }
                $writableKey = $null
                try {
                    $writableKey = $machineRegistry.OpenSubKey(
                        $key, [Microsoft.Win32.RegistryKeyPermissionCheck]::ReadWriteSubTree,
                        [Security.AccessControl.RegistryRights]::ReadKey -bor
                        [Security.AccessControl.RegistryRights]::CreateSubKey -bor
                        [Security.AccessControl.RegistryRights]::SetValue)
                    if ($null -eq $writableKey) { throw 'The existing registry key could not be opened.' }
                }
                catch { $access.WritableOpenError = $_.Exception.ToString() }
                finally { if ($null -ne $writableKey) { $writableKey.Dispose() } }
            }
            $context.RegistryAccess += [pscustomobject]$access
        }
    }
    finally { $machineRegistry.Dispose() }
    $context | ConvertTo-Json -Depth 7 | Tee-Object -FilePath (Join-Path $ResultsDirectory 'trust-diagnostics.json')
}

if ($Action -eq 'Cleanup') {
    if (-not (Test-Path -LiteralPath $StatePath)) {
        Write-Output 'No UMDF smoke state was created.'
        return
    }
    $state = Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json
    if ($state.HardwareId -ne $hardwareId -or $state.Subject -ne $subject -or $state.Thumbprint -notmatch '^[A-Fa-f0-9]{40}$') {
        throw 'Invalid UMDF smoke cleanup journal.'
    }
    if (@($state.InfNames | Where-Object { $_ -notmatch '^oem[0-9]+\.inf$' }).Count) {
        throw 'Invalid published driver filename in cleanup journal.'
    }
    if (@($state.ImportedStores | Where-Object { $_ -notin @('Root', 'TrustedPublisher') }).Count) {
        throw 'Unexpected certificate store in cleanup journal.'
    }
    $errors = [Collections.Generic.List[string]]::new()
    $ownedDevices = @(Get-OwnedDevices)
    foreach ($name in $state.InfNames) {
        $inf = Join-Path $env:WINDIR "INF\$name"
        if (Test-Path -LiteralPath $inf) {
            if (-not (Select-String -LiteralPath $inf -SimpleMatch $hardwareId -Quiet)) {
                throw "Refusing to remove unrelated driver package: $name"
            }
            & pnputil.exe /delete-driver $name /uninstall /force
            if ($LASTEXITCODE -notin @(0, 3010)) { $errors.Add("Package removal failed: $name, exit $LASTEXITCODE") }
            if (Test-Path -LiteralPath $inf) { $errors.Add("Published driver remains after removal: $name") }
        }
    }
    # HID keyboard/mouse roots can reject removal while their driver is active.
    foreach ($device in $ownedDevices) {
        & pnputil.exe /remove-device $device.InstanceId
        if ($LASTEXITCODE -notin @(0, 3010)) { $errors.Add("Device removal failed: $($device.InstanceId), exit $LASTEXITCODE") }
    }
    foreach ($store in $state.ImportedStores) {
        $path = "Cert:\LocalMachine\$store\$($state.Thumbprint)"
        if (Test-Path $path) {
            if ((Get-Item $path).Subject -ne $subject) { throw 'Refusing to remove an unrelated certificate.' }
            Remove-Item $path
        }
    }
    $remaining = @(Get-OwnedDevices)
    if ($remaining.Count) { $errors.Add('An owned UMDF device remains after cleanup.') }
    [pscustomobject]@{ Removed = $errors.Count -eq 0; Errors = @($errors) } |
        ConvertTo-Json -Depth 4 | Set-Content (Join-Path $ResultsDirectory 'driver-cleanup.json')
    if ($errors.Count) { throw ($errors -join '; ') }
    Remove-Item -LiteralPath $StatePath
    return
}

if ([string]::IsNullOrWhiteSpace($ArtifactDirectory)) { throw 'Install requires the staged platform artifact directory.' }
if (Test-Path -LiteralPath $StatePath) { throw 'An earlier UMDF smoke journal requires cleanup before installation.' }
if (@(Get-OwnedDevices).Count) { throw 'A pre-existing prototype device is present; refusing to overwrite it.' }
$artifact = (Resolve-Path $ArtifactDirectory).Path
$driver = Join-Path $artifact 'driver'
foreach ($file in Get-Content (Join-Path $artifact 'driver-hashes.json') -Raw | ConvertFrom-Json) {
    $name = Split-Path $file.Path -Leaf
    if ((Get-FileHash (Join-Path $driver $name) -Algorithm SHA256).Hash -ne $file.Hash) {
        throw "Staged driver hash mismatch: $name"
    }
}
$certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new((Join-Path $driver 'PtHidProbe.cer'))
if ($certificate.Subject -ne $subject) { throw 'Unexpected test certificate subject.' }
$state = [pscustomobject]@{
    HardwareId = $hardwareId
    Subject = $subject
    Thumbprint = $certificate.Thumbprint
    ImportedStores = @()
    InfNames = @()
}
Save-State
$before = @(Get-ChildItem (Join-Path $env:WINDIR 'INF') -Filter 'oem*.inf' -File | Select-Object -ExpandProperty Name)
$started = Get-Date
$currentStore = $null
try {
    Write-TrustDiagnostics
    foreach ($store in @('Root', 'TrustedPublisher')) {
        if (-not (Test-MachineCertificate -Store $store -Thumbprint $certificate.Thumbprint)) {
            $currentStore = $store
            $state.ImportedStores += $store
            Save-State
            Write-Output "Importing test certificate into Cert:\LocalMachine\$store with certutil"
            & (Join-Path $env:WINDIR 'System32\certutil.exe') -f -addstore $store (Join-Path $driver 'PtHidProbe.cer')
            if ($LASTEXITCODE -ne 0) { throw "Machine certificate import failed: store=$store, exit=$LASTEXITCODE." }
            if (-not (Test-MachineCertificate -Store $store -Thumbprint $certificate.Thumbprint)) {
                throw "The approved test certificate was not persisted in the requested machine store: $store"
            }
            Write-Output "Imported test certificate into Cert:\LocalMachine\$store"
        }
    }
    $currentStore = $null
    foreach ($file in @('VhidminiUm.dll', 'PtHidProbe.cat', 'CreateUmdfHidDevice.exe')) {
        $signature = Get-AuthenticodeSignature (Join-Path $driver $file)
        if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) {
            throw "Driver signature validation failed: $file, $($signature.Status)"
        }
    }
    & (Join-Path $driver 'CreateUmdfHidDevice.exe')
    if ($LASTEXITCODE -ne 0) { throw "Virtual HID root-device creation failed: $LASTEXITCODE." }
    & pnputil.exe /add-driver (Join-Path $driver 'PtHidProbe.inf') /install
    $installExit = $LASTEXITCODE
    $state.InfNames = @(Get-ChildItem (Join-Path $env:WINDIR 'INF') -Filter 'oem*.inf' -File |
        Where-Object { $_.Name -notin $before -and (Select-String -LiteralPath $_.FullName -SimpleMatch $hardwareId -Quiet) } |
        Select-Object -ExpandProperty Name)
    Save-State
    $deadline = (Get-Date).AddSeconds(60)
    do {
        $devices = @(Get-OwnedDevices)
        $device = if ($devices.Count -eq 1) { $devices[0] } else { $null }
        $problem = if ($device) { (Get-PnpDeviceProperty -InstanceId $device.InstanceId -KeyName DEVPKEY_Device_ProblemCode).Data } else { $null }
        $children = if ($device) { @((Get-PnpDeviceProperty -InstanceId $device.InstanceId -KeyName DEVPKEY_Device_Children).Data | Where-Object { $_ }) } else { @() }
        if ($device -and $device.Status -eq 'OK' -and $problem -eq 0 -and $children.Count -eq 3) { break }
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)
    $secureBoot = try { Confirm-SecureBootUEFI } catch { Write-Warning $_; "Unavailable: $($_.Exception.Message)" }
    $problemStatus = if ($device) { (Get-PnpDeviceProperty -InstanceId $device.InstanceId -KeyName DEVPKEY_Device_ProblemStatus).Data } else { $null }
    [pscustomobject]@{
        InstallExitCode = $installExit
        OsArchitecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
        ProcessArchitecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
        OsVersion = [Environment]::OSVersion.Version.ToString()
        SecureBoot = $secureBoot
        BootConfiguration = (& bcdedit /enum '{current}') -join "`n"
        InstanceId = $device.InstanceId
        Status = $device.Status
        ProblemCode = $problem
        ProblemStatus = $problemStatus
        Children = $children
        InfNames = $state.InfNames
    } | ConvertTo-Json -Depth 4 | Tee-Object -FilePath (Join-Path $ResultsDirectory 'driver-install.json')
    if ($installExit -ne 0 -or -not $device -or $device.Status -ne 'OK' -or $problem -ne 0 -or $children.Count -ne 3) {
        throw "UMDF smoke driver did not load: install=$installExit, status=$($device.Status), problem=$problem, NTSTATUS=$problemStatus."
    }
}
catch {
    $failure = $_
    $state.InfNames = @(Get-ChildItem (Join-Path $env:WINDIR 'INF') -Filter 'oem*.inf' -File |
        Where-Object { $_.Name -notin $before -and (Select-String -LiteralPath $_.FullName -SimpleMatch $hardwareId -Quiet) } |
        Select-Object -ExpandProperty Name)
    Save-State
    try {
        Write-TrustDiagnostics -FailedStore $currentStore -Failure $failure.Exception.ToString()
        Get-Content (Join-Path $env:WINDIR 'INF\setupapi.dev.log') -Tail 220 |
            Set-Content (Join-Path $ResultsDirectory 'setupapi-tail.log')
        Get-WinEvent -FilterHashtable @{ LogName = 'Microsoft-Windows-Kernel-PnP/Configuration'; StartTime = $started } |
            Where-Object Message -Match 'HIDCLASS|Vhidmini|PtUmdf' | Select-Object Id, TimeCreated, Message |
            ConvertTo-Json -Depth 3 | Set-Content (Join-Path $ResultsDirectory 'pnp-events.json')
    }
    catch {
        Write-Warning "Could not capture additional driver diagnostics: $_"
    }
    throw
}
