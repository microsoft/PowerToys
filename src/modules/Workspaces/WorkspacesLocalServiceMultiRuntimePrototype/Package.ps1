[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipBuild,
    [switch]$TrustMachine
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if (-not $SkipBuild) {
    & (Join-Path $root 'Build.ps1') -Configuration $Configuration
}

$sdkRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
$sdk = Get-ChildItem $sdkRoot -Directory |
    Where-Object { Test-Path (Join-Path $_.FullName 'x64\makeappx.exe') } |
    Sort-Object Name -Descending |
    Select-Object -First 1
if (-not $sdk) {
    throw 'Windows SDK makeappx.exe was not found.'
}
$makeappx = Join-Path $sdk.FullName 'x64\makeappx.exe'
$signtool = Join-Path $sdk.FullName 'x64\signtool.exe'
$binRoot = Join-Path $root "artifacts\bin\x64\$Configuration"
$updater = Join-Path $binRoot 'PtPuvrUpdater.exe'
$deploymentHelper = Join-Path $binRoot 'PtPuvrDeploymentHelper.exe'
$runtime1 = Join-Path $binRoot 'PtPuvrRuntime.exe'
$runtime2 = Join-Path $binRoot 'runtime-track-2\PtPuvrRuntime.exe'
foreach ($binary in $updater, $deploymentHelper, $runtime1, $runtime2) {
    if (-not (Test-Path $binary)) {
        throw "Build artifact is missing: $binary"
    }
}

$expectedVersions = @{
    $updater = '5.0.0.0'
    $deploymentHelper = '5.0.0.0'
    $runtime1 = '1.0.0.0'
    $runtime2 = '2.0.0.0'
}
foreach ($entry in $expectedVersions.GetEnumerator()) {
    $actual = (Get-Item $entry.Key).VersionInfo.FileVersion
    if ($actual -ne $entry.Value) {
        throw "Unexpected FileVersion for $($entry.Key): expected $($entry.Value), actual $actual"
    }
}

$publisher = 'CN=PowerToys Workspaces Packaged Updater Virtual Runtime Prototype Test'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if ($TrustMachine -and -not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw '-TrustMachine requires an elevated PowerShell session.'
}
$certificate = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject $publisher `
    -CertStoreLocation Cert:\CurrentUser\My `
    -KeyAlgorithm RSA `
    -KeyLength 2048 `
    -HashAlgorithm SHA256 `
    -KeyExportPolicy NonExportable `
    -NotAfter (Get-Date).AddYears(2)

try {
    $packageRoot = Join-Path $root 'artifacts\packages'
    $bundleRoot = Join-Path $root 'artifacts\simulated-bundles'
    New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $bundleRoot -Force | Out-Null
    $cerPath = Join-Path $packageRoot 'PtPuvr-TestOnly.cer'
    Export-Certificate -Cert $certificate -FilePath $cerPath -Force | Out-Null
    Import-Certificate -FilePath $cerPath -CertStoreLocation Cert:\CurrentUser\TrustedPeople | Out-Null
    if ($TrustMachine) {
        Import-Certificate -FilePath $cerPath -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
    }

    if (-not ('PtPuvr.PackageIdentityNative' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
namespace PtPuvr {
    public static class PackageIdentityNative {
        [StructLayout(LayoutKind.Sequential)]
        private struct PACKAGE_VERSION {
            public ushort Revision, Build, Minor, Major;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct PACKAGE_ID {
            public UInt32 reserved, processorArchitecture;
            public PACKAGE_VERSION version;
            public IntPtr name, publisher, resourceId, publisherId;
        }
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode)]
        private static extern int PackageFullNameFromId(ref PACKAGE_ID id, ref UInt32 length, IntPtr fullName);
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode)]
        private static extern int PackageFamilyNameFromId(ref PACKAGE_ID id, ref UInt32 length, IntPtr familyName);
        private static PACKAGE_ID Id(string name, string publisher, ushort major) {
            return new PACKAGE_ID {
                processorArchitecture = 9,
                version = new PACKAGE_VERSION { Major = major },
                name = Marshal.StringToHGlobalUni(name),
                publisher = Marshal.StringToHGlobalUni(publisher)
            };
        }
        private static string Convert(string name, string publisher, ushort major, bool family) {
            PACKAGE_ID id = Id(name, publisher, major);
            try {
                UInt32 length = 0;
                int result = family
                    ? PackageFamilyNameFromId(ref id, ref length, IntPtr.Zero)
                    : PackageFullNameFromId(ref id, ref length, IntPtr.Zero);
                if (result != 122) throw new InvalidOperationException("Package identity size: " + result);
                IntPtr buffer = Marshal.AllocHGlobal(checked((int)length * 2));
                try {
                    result = family
                        ? PackageFamilyNameFromId(ref id, ref length, buffer)
                        : PackageFullNameFromId(ref id, ref length, buffer);
                    if (result != 0) throw new InvalidOperationException("Package identity: " + result);
                    return Marshal.PtrToStringUni(buffer);
                } finally { Marshal.FreeHGlobal(buffer); }
            } finally {
                Marshal.FreeHGlobal(id.name);
                Marshal.FreeHGlobal(id.publisher);
            }
        }
        public static string FullName(string name, string publisher, ushort major) {
            return Convert(name, publisher, major, false);
        }
        public static string FamilyName(string name, string publisher, ushort major) {
            return Convert(name, publisher, major, true);
        }
    }
}
'@
    }

    $png = [Convert]::FromBase64String(
        'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=')

    function New-StageRoot([string]$name) {
        $stage = Join-Path $packageRoot $name
        Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
        New-Item -ItemType Directory -Path (Join-Path $stage 'Assets') -Force | Out-Null
        foreach ($logo in 'StoreLogo.png', 'Square44x44Logo.png', 'Square150x150Logo.png') {
            [IO.File]::WriteAllBytes((Join-Path $stage "Assets\$logo"), $png)
        }
        return $stage
    }

    function New-SignedPackage([string]$stage, [string]$destination) {
        & $makeappx pack /o /d $stage /p $destination
        if ($LASTEXITCODE -ne 0) { throw "makeappx failed for $destination." }
        & $signtool sign /fd SHA256 /sha1 $certificate.Thumbprint /s My $destination
        if ($LASTEXITCODE -ne 0) { throw "signtool failed for $destination." }
        & $signtool verify /pa /v $destination
        if ($LASTEXITCODE -ne 0 -and $TrustMachine) {
            throw "signtool verification failed for $destination."
        }
    }

    $runtimeDefinitions = @(
        [ordered]@{
            track = 1
            packageName = 'Microsoft.PowerToys.WsPuvr.Runtime1'
            version = '1.0.0.0'
            binary = $runtime1
            packageFile = 'PtPuvrRuntime-Track1-1.0.0.0.msix'
            simulatedPowerToysVersion = '0.101'
        },
        [ordered]@{
            track = 2
            packageName = 'Microsoft.PowerToys.WsPuvr.Runtime2'
            version = '2.0.0.0'
            binary = $runtime2
            packageFile = 'PtPuvrRuntime-Track2-2.0.0.0.msix'
            simulatedPowerToysVersion = '0.110'
        }
    )

    $metadata = [ordered]@{
        publisher = $publisher
        updater = $null
        runtimes = @{}
        simulatedBundles = @{}
        certificatePath = $cerPath
        certificateThumbprint = $certificate.Thumbprint
    }

    foreach ($definition in $runtimeDefinitions) {
        $stage = New-StageRoot "stage-runtime-track-$($definition.track)"
        Copy-Item -LiteralPath $definition.binary -Destination (Join-Path $stage 'PtPuvrRuntime.exe')
        $manifest = (Get-Content (Join-Path $root 'Packaging\AppxManifest.template.xml') -Raw).
            Replace('@@PACKAGE_NAME@@', $definition.packageName).
            Replace('@@VERSION@@', $definition.version).
            Replace('@@TRACK@@', [string]$definition.track)
        Set-Content -Path (Join-Path $stage 'AppxManifest.xml') -Value $manifest -Encoding utf8NoBOM
        $msix = Join-Path $packageRoot $definition.packageFile
        New-SignedPackage $stage $msix
        $metadata.runtimes["track$($definition.track)"] = [ordered]@{
            packageName = $definition.packageName
            familyName = [PtPuvr.PackageIdentityNative]::FamilyName(
                $definition.packageName, $publisher, [uint16]$definition.track)
            fullName = [PtPuvr.PackageIdentityNative]::FullName(
                $definition.packageName, $publisher, [uint16]$definition.track)
            packageVersion = $definition.version
            fileVersion = (Get-Item $definition.binary).VersionInfo.FileVersion
            path = $msix
            sha256 = (Get-FileHash -Algorithm SHA256 $msix).Hash
        }
    }

    $updaterStage = New-StageRoot 'stage-updater'
    Copy-Item -LiteralPath $updater -Destination (Join-Path $updaterStage 'PtPuvrUpdater.exe')
    Copy-Item -LiteralPath $deploymentHelper `
        -Destination (Join-Path $updaterStage 'PtPuvrDeploymentHelper.exe')
    Copy-Item -LiteralPath (Join-Path $root 'Packaging\UpdaterAppxManifest.template.xml') `
        -Destination (Join-Path $updaterStage 'AppxManifest.xml')
    $updaterMsix = Join-Path $packageRoot 'PtPuvrUpdater-5.0.0.0.msix'
    New-SignedPackage $updaterStage $updaterMsix
    $updaterHash = (Get-FileHash -Algorithm SHA256 $updaterMsix).Hash
    $metadata.updater = [ordered]@{
        packageName = 'Microsoft.PowerToys.WsPuvr.Updater'
        familyName = [PtPuvr.PackageIdentityNative]::FamilyName(
            'Microsoft.PowerToys.WsPuvr.Updater', $publisher, [uint16]5)
        fullName = [PtPuvr.PackageIdentityNative]::FullName(
            'Microsoft.PowerToys.WsPuvr.Updater', $publisher, [uint16]5)
        packageVersion = '5.0.0.0'
        fileVersion = (Get-Item $updater).VersionInfo.FileVersion
        path = $updaterMsix
        sha256 = $updaterHash
    }

    foreach ($definition in $runtimeDefinitions) {
        $bundle = Join-Path $bundleRoot "PowerToys-$($definition.simulatedPowerToysVersion)"
        Remove-Item -LiteralPath $bundle -Recurse -Force -ErrorAction SilentlyContinue
        New-Item -ItemType Directory -Path $bundle -Force | Out-Null
        $updaterCopy = Join-Path $bundle 'PtPuvrUpdater-5.0.0.0.msix'
        $runtimeCopy = Join-Path $bundle $definition.packageFile
        Copy-Item -LiteralPath $updaterMsix -Destination $updaterCopy
        Copy-Item -LiteralPath (
            $metadata.runtimes["track$($definition.track)"].path) -Destination $runtimeCopy
        $copiedUpdaterHash = (Get-FileHash -Algorithm SHA256 $updaterCopy).Hash
        if ($copiedUpdaterHash -ne $updaterHash) {
            throw "Updater artifact changed in simulated PowerToys $($definition.simulatedPowerToysVersion) bundle."
        }
        $metadata.simulatedBundles["PowerToys-$($definition.simulatedPowerToysVersion)"] = [ordered]@{
            updaterPath = $updaterCopy
            updaterSha256 = $copiedUpdaterHash
            runtimeTrack = $definition.track
            runtimePath = $runtimeCopy
        }
    }

    $metadata | ConvertTo-Json -Depth 8 |
        Set-Content (Join-Path $packageRoot 'packages.json') -Encoding utf8NoBOM
    Write-Host 'Built updater 5.0.0.0 and independently versioned runtime tracks 1/2.'
    Write-Host 'The simulated PowerToys 0.101 and 0.110 bundles contain byte-identical updater MSIX files.'
}
finally {
    Remove-Item "Cert:\CurrentUser\My\$($certificate.Thumbprint)" -Force -ErrorAction SilentlyContinue
}
