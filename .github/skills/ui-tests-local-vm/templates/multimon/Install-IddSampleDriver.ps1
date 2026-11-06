# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Installs the Windows-driver-samples IndirectDisplay driver in the guest and creates its root device.

.DESCRIPTION
Runs inside the guest as the administrator over PowerShell Direct, so it stays Windows PowerShell 5.1
compatible, including the C# it compiles.

The host builds the sample with signing off. This script signs the catalog with a guest-local
self-signed code-signing certificate and trusts that certificate in the guest LocalMachine Root and
TrustedPublisher stores, so the trust anchor exists only inside the disposable guest and no trust
prompt can block the session-0 install. Set-AuthenticodeSignature signs the catalog through the
system's catalog subject interface package, so no Windows SDK tool is copied into the guest. The
certificate is trusted before signing, so the signature validates as soon as it is written. The
driver package is staged with pnputil, and the
root-enumerated device is created with the SetupAPI sequence devcon uses for 'install', because
pnputil has no command that creates a device node.

The host copies its unsigned build to SourceRoot. The signed package lives in DriverRoot, and the
script refreshes it only when a source file differs from what the package was built from. A signed
catalog never matches its unsigned source byte for byte, so DriverRoot records the source catalog's
hash next to the signed copy.

Every step reads the current state first. A rerun with the same build, a trusted signature, and a
working device re-signs nothing and leaves the installed device untouched. The driver is installed
on the device only when the device is new, reports a problem code, or the package changed.

Problem code 52 means Windows rejected the driver signature. The script stops and names the
alternatives instead of changing Secure Boot or test signing itself, because both weaken the guest
and need the host to change the virtual machine's firmware.

When pnputil or the driver installation asks for a restart, the device waits at problem code 14
until the guest restarts. The script then returns RebootRequired instead of failing on that code.

.PARAMETER SourceRoot
Guest folder holding the host's unsigned <PackageName>.inf, <PackageName>.cat, and
<PackageName>.dll.

.PARAMETER DriverRoot
Guest folder holding the signed package that is installed.

.PARAMETER PackageName
Base name of the driver package's files.

.PARAMETER HardwareId
Hardware ID of the root device. The sample INF matches Root\IddSampleDriver.

.PARAMETER CertificateSubject
Subject of the guest-local signing certificate.

.PARAMETER ExpectedMonitorCount
Monitors the driver creates. The unmodified sample creates three.

.PARAMETER TimeoutSeconds
Upper bound on waiting for the device and its monitors after installation.

.EXAMPLE
Invoke-Command -VMName PowerToysUiTest-Win11 -Credential $credential -FilePath .\Install-IddSampleDriver.ps1 -ArgumentList 'C:\PowerToysUiTestRun\Multimon\driver-source', 'C:\PowerToysUiTestRun\Multimon\driver', 'IddSampleDriver'
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateNotNullOrEmpty()]
    [string]$SourceRoot,
    [Parameter(Mandatory, Position = 1)]
    [ValidateNotNullOrEmpty()]
    [string]$DriverRoot,
    [Parameter(Mandatory, Position = 2)]
    [ValidatePattern('^[A-Za-z0-9_-]+$')]
    [string]$PackageName,
    [ValidateNotNullOrEmpty()]
    [string]$HardwareId = 'Root\IddSampleDriver',
    [ValidateNotNullOrEmpty()]
    [string]$CertificateSubject = 'CN=PowerToys UI-test IddSample signing',
    [ValidateRange(1, 16)]
    [int]$ExpectedMonitorCount = 3,
    [ValidateRange(10, 600)]
    [int]$TimeoutSeconds = 90
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($null -eq ('PowerToysUiTestVm.RootDeviceInstaller' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace PowerToysUiTestVm
{
    public static class RootDeviceInstaller
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVINFO_DATA
        {
            public int cbSize;
            public Guid ClassGuid;
            public int DevInst;
            public IntPtr Reserved;
        }

        private const int DICD_GENERATE_ID = 0x1;
        private const int SPDRP_HARDWAREID = 0x1;
        private const int DIF_REGISTERDEVICE = 0x19;
        private const int INSTALLFLAG_FORCE = 0x1;
        private const int MAX_CLASS_NAME_LEN = 32;
        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiGetINFClassW(
            string infName, out Guid classGuid, StringBuilder className, int classNameSize, out int requiredSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern IntPtr SetupDiCreateDeviceInfoList(ref Guid classGuid, IntPtr hwndParent);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiCreateDeviceInfoW(
            IntPtr deviceInfoSet, string deviceName, ref Guid classGuid, string deviceDescription, IntPtr hwndParent, int creationFlags,
            ref SP_DEVINFO_DATA deviceInfoData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiSetDeviceRegistryPropertyW(
            IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, int property, byte[] propertyBuffer, int propertyBufferSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiCallClassInstaller(int installFunction, IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("newdev.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool UpdateDriverForPlugAndPlayDevicesW(
            IntPtr hwndParent, string hardwareId, string fullInfPath, int installFlags, out bool rebootRequired);

        // Registers a ROOT-enumerated device node carrying the hardware ID, as devcon 'install' does.
        public static void CreateRootDevice(string infPath, string hardwareId)
        {
            Guid classGuid;
            StringBuilder className = new StringBuilder(MAX_CLASS_NAME_LEN);
            int requiredSize;
            if (!SetupDiGetINFClassW(infPath, out classGuid, className, className.Capacity, out requiredSize))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "SetupDiGetINFClass failed for " + infPath);
            }

            IntPtr deviceInfoSet = SetupDiCreateDeviceInfoList(ref classGuid, IntPtr.Zero);
            if (deviceInfoSet == InvalidHandleValue)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "SetupDiCreateDeviceInfoList failed");
            }

            try
            {
                SP_DEVINFO_DATA deviceInfoData = new SP_DEVINFO_DATA();
                deviceInfoData.cbSize = Marshal.SizeOf(typeof(SP_DEVINFO_DATA));
                if (!SetupDiCreateDeviceInfoW(
                    deviceInfoSet, className.ToString(), ref classGuid, null, IntPtr.Zero, DICD_GENERATE_ID, ref deviceInfoData))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "SetupDiCreateDeviceInfo failed");
                }

                // REG_MULTI_SZ: the ID, its terminator, and the list terminator.
                byte[] hardwareIdList = Encoding.Unicode.GetBytes(hardwareId + "\0\0");
                if (!SetupDiSetDeviceRegistryPropertyW(
                    deviceInfoSet, ref deviceInfoData, SPDRP_HARDWAREID, hardwareIdList, hardwareIdList.Length))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "SetupDiSetDeviceRegistryProperty(SPDRP_HARDWAREID) failed");
                }

                if (!SetupDiCallClassInstaller(DIF_REGISTERDEVICE, deviceInfoSet, ref deviceInfoData))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "SetupDiCallClassInstaller(DIF_REGISTERDEVICE) failed");
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }
        }

        // Installs the INF on every present device matching the hardware ID. Returns whether a reboot is needed.
        public static bool UpdateDriver(string infPath, string hardwareId)
        {
            bool rebootRequired;
            if (!UpdateDriverForPlugAndPlayDevicesW(IntPtr.Zero, hardwareId, infPath, INSTALLFLAG_FORCE, out rebootRequired))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "UpdateDriverForPlugAndPlayDevices failed for " + hardwareId);
            }

            return rebootRequired;
        }
    }
}
'@
}

function Get-SampleDevice {
    param([Parameter(Mandatory)][string]$Id)

    @(Get-CimInstance -ClassName Win32_PnPEntity | Where-Object { $_.HardwareID -contains $Id })
}

function Get-MonitorDevice {
    @(Get-CimInstance -ClassName Win32_PnPEntity -Filter "PNPClass = 'Monitor'" |
        Where-Object { $_.ConfigManagerErrorCode -eq 0 })
}

$infName = "$PackageName.inf"
$catalogName = "$PackageName.cat"
$binaryName = "$PackageName.dll"
foreach ($name in $infName, $catalogName, $binaryName) {
    $required = Join-Path $SourceRoot $name
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required file is missing: $required"
    }
}
New-Item -ItemType Directory -Path $DriverRoot -Force | Out-Null
$infPath = Join-Path $DriverRoot $infName
$catalogPath = Join-Path $DriverRoot $catalogName
$catalogSourceHashPath = Join-Path $DriverRoot "$catalogName.source-sha256"

$packageChanged = $false
foreach ($name in $infName, $binaryName) {
    $source = Join-Path $SourceRoot $name
    $target = Join-Path $DriverRoot $name
    if (-not (Test-Path -LiteralPath $target -PathType Leaf) -or
        (Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash) {
        Copy-Item -LiteralPath $source -Destination $target -Force
        $packageChanged = $true
    }
}
$sourceCatalogHash = (Get-FileHash -LiteralPath (Join-Path $SourceRoot $catalogName)).Hash
$recordedCatalogHash = ''
if (Test-Path -LiteralPath $catalogSourceHashPath -PathType Leaf) {
    $recordedCatalogHash = (Get-Content -LiteralPath $catalogSourceHashPath -Raw).Trim()
}

$certificate = Get-ChildItem -Path Cert:\LocalMachine\My -CodeSigningCert |
    Where-Object { $_.Subject -eq $CertificateSubject -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date).AddDays(30) } |
    Sort-Object -Property NotAfter -Descending |
    Select-Object -First 1
$certificateCreated = $false
if ($null -eq $certificate) {
    $certificate = New-SelfSignedCertificate -Type CodeSigningCert -Subject $CertificateSubject `
        -CertStoreLocation Cert:\LocalMachine\My -KeyExportPolicy NonExportable -NotAfter (Get-Date).AddYears(2)
    $certificateCreated = $true
}

# LocalMachine stores take the certificate without the confirmation dialog CurrentUser\Root raises.
$publicCertificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($certificate.RawData)
foreach ($storeName in @('Root', 'TrustedPublisher')) {
    $store = New-Object -TypeName System.Security.Cryptography.X509Certificates.X509Store -ArgumentList $storeName, 'LocalMachine'
    $store.Open('ReadWrite')
    try {
        if (@($store.Certificates | Where-Object { $_.Thumbprint -eq $certificate.Thumbprint }).Count -eq 0) {
            $store.Add($publicCertificate)
        }
    }
    finally {
        $store.Close()
    }
}

$signedByGuest = $false
if (Test-Path -LiteralPath $catalogPath -PathType Leaf) {
    $catalogSignature = Get-AuthenticodeSignature -LiteralPath $catalogPath
    $signedByGuest = $catalogSignature.Status -eq 'Valid' -and
        $null -ne $catalogSignature.SignerCertificate -and
        $catalogSignature.SignerCertificate.Thumbprint -eq $certificate.Thumbprint
}
$catalogSigned = $false
if (-not $signedByGuest -or $recordedCatalogHash -ne $sourceCatalogHash) {
    # The hash record is written only after the signature verifies, so an interrupted run re-signs.
    Remove-Item -LiteralPath $catalogSourceHashPath -Force -ErrorAction SilentlyContinue
    Copy-Item -LiteralPath (Join-Path $SourceRoot $catalogName) -Destination $catalogPath -Force
    # The returned status reflects chain trust, not whether a signature was written, so the file is
    # read back for both.
    [void](Set-AuthenticodeSignature -LiteralPath $catalogPath -Certificate $certificate -HashAlgorithm SHA256)
    $catalogSignature = Get-AuthenticodeSignature -LiteralPath $catalogPath
    $signerThumbprint = '<none>'
    if ($null -ne $catalogSignature.SignerCertificate) {
        $signerThumbprint = $catalogSignature.SignerCertificate.Thumbprint
    }
    if ($catalogSignature.Status -ne 'Valid' -or $signerThumbprint -ne $certificate.Thumbprint) {
        throw "The catalog signature is $($catalogSignature.Status) with signer $signerThumbprint after signing with $($certificate.Thumbprint): $($catalogSignature.StatusMessage)"
    }
    Set-Content -LiteralPath $catalogSourceHashPath -Value $sourceCatalogHash -Encoding Ascii
    $catalogSigned = $true
    $packageChanged = $true
}

# pnputil returns 0 for a newly added package and for one already in the driver store, and 3010
# (ERROR_SUCCESS_REBOOT_REQUIRED) when Windows needs a restart to finish.
$pnputilOutput = & pnputil.exe /add-driver $infPath 2>&1
if ($LASTEXITCODE -notin @(0, 3010)) {
    throw "pnputil /add-driver exited $LASTEXITCODE for ${infPath}: $($pnputilOutput -join ' ')"
}
$rebootRequired = $LASTEXITCODE -eq 3010

$devices = @(Get-SampleDevice -Id $HardwareId)
if ($devices.Count -gt 1) {
    throw "$($devices.Count) devices carry $HardwareId ($(($devices | ForEach-Object { $_.DeviceID }) -join ', ')). Remove the extras with pnputil /remove-device and rerun."
}
$deviceCreated = $false
if ($devices.Count -eq 0) {
    [PowerToysUiTestVm.RootDeviceInstaller]::CreateRootDevice($infPath, $HardwareId)
    $deviceCreated = $true
}

# A working device keeps its driver unless the package changed. The forced install replaces a driver
# of the same version, which a changed build of the sample keeps.
$driverUpdated = $deviceCreated -or $packageChanged -or $devices[0].ConfigManagerErrorCode -ne 0
if ($driverUpdated -and [PowerToysUiTestVm.RootDeviceInstaller]::UpdateDriver($infPath, $HardwareId)) {
    $rebootRequired = $true
}

# A device passes through transient problem codes while it starts, so a non-zero code counts as final
# only once that code has held for the settle window.
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$device = $null
$problemCode = 0
$problemSince = $null
do {
    Start-Sleep -Seconds 2
    $device = Get-SampleDevice -Id $HardwareId | Select-Object -First 1
    $monitors = @(Get-MonitorDevice)
    $settled = $false
    if ($null -ne $device -and $device.ConfigManagerErrorCode -ne 0) {
        if ($null -eq $problemSince -or $device.ConfigManagerErrorCode -ne $problemCode) {
            $problemCode = $device.ConfigManagerErrorCode
            $problemSince = Get-Date
        }
        $settled = ((Get-Date) - $problemSince).TotalSeconds -ge 15
    }
    else {
        $problemSince = $null
    }
    $ready = $null -ne $device -and $device.ConfigManagerErrorCode -eq 0 -and $monitors.Count -ge ($ExpectedMonitorCount + 1)
} while (-not $settled -and -not $ready -and (Get-Date) -lt $deadline)

if ($null -eq $device) {
    throw "No device carrying $HardwareId exists after installation."
}
if ($device.ConfigManagerErrorCode -eq 52) {
    throw @"
BLOCKED: $($device.DeviceID) reports problem code 52. Windows rejected the driver signature even with the guest-local certificate trusted.
Nothing was changed to work around it. The choices, each a decision for a person:
  1. Test signing: stop the guest, run Set-VMFirmware -VMName <name> -EnableSecureBoot Off on the host, run 'bcdedit /set testsigning on' in the guest, restart, and rerun this script.
  2. A driver signed by a publicly trusted certificate instead of the sample.
"@
}
# A pending restart holds the device at problem code 14, "the device cannot work properly until the
# computer is restarted", so that code is the expected state then and the restart is reported instead.
$restartPending = $rebootRequired -and $device.ConfigManagerErrorCode -in @(0, 14)
if ($device.ConfigManagerErrorCode -ne 0 -and -not $restartPending) {
    $restartNote = ''
    if ($rebootRequired) {
        $restartNote = ' Windows also requested a restart.'
    }
    throw "$($device.DeviceID) reports problem code $($device.ConfigManagerErrorCode).$restartNote Inspect it with: pnputil /enum-devices /instanceid `"$($device.DeviceID)`" /problem"
}
if ($rebootRequired) {
    Write-Warning 'Windows requested a restart to finish the driver installation. Restart the guest before applying a layout.'
}
elseif ($monitors.Count -lt ($ExpectedMonitorCount + 1)) {
    throw "The device started, but $($monitors.Count) monitors are present instead of $($ExpectedMonitorCount + 1): $(($monitors | ForEach-Object { $_.DeviceID }) -join ', '). The guest may lack a render adapter IddCx accepts."
}

[pscustomobject]@{
    DeviceInstanceId       = $device.DeviceID
    ProblemCode            = [int]$device.ConfigManagerErrorCode
    DeviceCreated          = $deviceCreated
    CatalogSigned          = $catalogSigned
    PackageChanged         = $packageChanged
    DriverUpdated          = $driverUpdated
    CertificateCreated     = $certificateCreated
    CertificateThumbprint  = $certificate.Thumbprint
    RebootRequired         = $rebootRequired
    Monitors               = @($monitors | ForEach-Object { "$($_.Name) [$($_.DeviceID)]" })
}
