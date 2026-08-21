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
$updater5 = Join-Path $binRoot 'PtPuvrUpdater.exe'
$updater6 = Join-Path $binRoot 'updater-v6\PtPuvrUpdater.exe'
$runtime1 = Join-Path $binRoot 'PtPuvrRuntime.exe'
$runtime2 = Join-Path $binRoot 'runtime-track-2\PtPuvrRuntime.exe'
foreach ($binary in $updater5, $updater6, $runtime1, $runtime2) {
    if (-not (Test-Path -LiteralPath $binary -PathType Leaf)) {
        throw "Build artifact is missing: $binary"
    }
}

$expectedVersions = @{
    $updater5 = '5.0.0.0'
    $updater6 = '6.0.0.0'
    $runtime1 = '1.0.0.0'
    $runtime2 = '2.0.0.0'
}
foreach ($entry in $expectedVersions.GetEnumerator()) {
    $actual = (Get-Item -LiteralPath $entry.Key).VersionInfo.FileVersion
    if ($actual -ne $entry.Value) {
        throw "Unexpected FileVersion for $($entry.Key): expected $($entry.Value), actual $actual"
    }
}

$publisher =
    'CN=PowerToys Workspaces Packaged Payload Updater Virtual Runtime Prototype Test'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if ($TrustMachine -and
    -not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
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
    Remove-Item -LiteralPath $packageRoot -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $bundleRoot -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $bundleRoot -Force | Out-Null
    $cerPath = Join-Path $packageRoot 'PtPuvr-TestOnly.cer'
    Export-Certificate -Cert $certificate -FilePath $cerPath -Force | Out-Null
    Import-Certificate `
        -FilePath $cerPath `
        -CertStoreLocation Cert:\CurrentUser\TrustedPeople | Out-Null
    if ($TrustMachine) {
        Import-Certificate `
            -FilePath $cerPath `
            -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
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
        if ($LASTEXITCODE -ne 0) {
            throw "makeappx failed for $destination."
        }
        & $signtool sign /fd SHA256 /sha1 $certificate.Thumbprint /s My $destination
        if ($LASTEXITCODE -ne 0) {
            throw "signtool failed for $destination."
        }
        & $signtool verify /pa /v $destination
        if ($LASTEXITCODE -ne 0 -and $TrustMachine) {
            throw "signtool verification failed for $destination."
        }
    }

    function Sign-File([string]$path) {
        & $signtool sign /fd SHA256 /sha1 $certificate.Thumbprint /s My $path
        if ($LASTEXITCODE -ne 0) {
            throw "signtool failed for $path."
        }
        & $signtool verify /pa /v $path
        if ($LASTEXITCODE -ne 0 -and $TrustMachine) {
            throw "signtool verification failed for $path."
        }
    }

    $updaterDefinitions = @(
        [ordered]@{
            major = 5
            version = '5.0.0.0'
            binary = $updater5
            packageFile = 'PtPuvrUpdater-5.0.0.0.msix'
        },
        [ordered]@{
            major = 6
            version = '6.0.0.0'
            binary = $updater6
            packageFile = 'PtPuvrUpdater-6.0.0.0.msix'
        }
    )

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

    $updaterPackages = @{}
    foreach ($definition in $updaterDefinitions) {
        $stage = New-StageRoot "stage-updater-$($definition.major)"
        $stagedUpdater = Join-Path $stage 'PtPuvrUpdater.exe'
        Copy-Item -LiteralPath $definition.binary -Destination $stagedUpdater
        Sign-File $stagedUpdater
        $manifest = (
            Get-Content (
                Join-Path $root 'Packaging\UpdaterPayloadAppxManifest.template.xml'
            ) -Raw
        ).Replace('@@VERSION@@', $definition.version)
        Set-Content `
            -Path (Join-Path $stage 'AppxManifest.xml') `
            -Value $manifest `
            -Encoding utf8NoBOM
        $msix = Join-Path $packageRoot $definition.packageFile
        New-SignedPackage $stage $msix
        $updaterPackages["v$($definition.major)"] = [ordered]@{
            packageName = 'Microsoft.PowerToys.WsPuvr.RawUpdater'
            familyName = [PtPuvr.PackageIdentityNative]::FamilyName(
                'Microsoft.PowerToys.WsPuvr.RawUpdater',
                $publisher,
                [uint16]$definition.major)
            fullName = [PtPuvr.PackageIdentityNative]::FullName(
                'Microsoft.PowerToys.WsPuvr.RawUpdater',
                $publisher,
                [uint16]$definition.major)
            standaloneVersion = $definition.version
            packageVersion = $definition.version
            fileVersion = (Get-Item -LiteralPath $stagedUpdater).VersionInfo.FileVersion
            path = $msix
            sha256 = (Get-FileHash -LiteralPath $msix -Algorithm SHA256).Hash
            payloadSha256 = (
                Get-FileHash -LiteralPath $stagedUpdater -Algorithm SHA256).Hash
        }
    }

    $metadata = [ordered]@{
        publisher = $publisher
        updater = [ordered]@{
            artifactType = 'msix-staged-raw-scm'
            packageName = $updaterPackages.v5.packageName
            familyName = $updaterPackages.v5.familyName
            fullName = $updaterPackages.v5.fullName
            standaloneVersion = $updaterPackages.v5.standaloneVersion
            packageVersion = $updaterPackages.v5.packageVersion
            fileVersion = $updaterPackages.v5.fileVersion
            path = $updaterPackages.v5.path
            sha256 = $updaterPackages.v5.sha256
            payloadSha256 = $updaterPackages.v5.payloadSha256
            signerSubject = $publisher
            upgrade = $updaterPackages.v6
        }
        runtimes = @{}
        simulatedBundles = @{}
        certificatePath = $cerPath
        certificateThumbprint = $certificate.Thumbprint
    }

    foreach ($definition in $runtimeDefinitions) {
        $stage = New-StageRoot "stage-runtime-track-$($definition.track)"
        Copy-Item `
            -LiteralPath $definition.binary `
            -Destination (Join-Path $stage 'PtPuvrRuntime.exe')
        $manifest = (Get-Content (Join-Path $root 'Packaging\AppxManifest.template.xml') -Raw).
            Replace('@@PACKAGE_NAME@@', $definition.packageName).
            Replace('@@VERSION@@', $definition.version).
            Replace('@@TRACK@@', [string]$definition.track)
        Set-Content `
            -Path (Join-Path $stage 'AppxManifest.xml') `
            -Value $manifest `
            -Encoding utf8NoBOM
        $msix = Join-Path $packageRoot $definition.packageFile
        New-SignedPackage $stage $msix
        $metadata.runtimes["track$($definition.track)"] = [ordered]@{
            packageName = $definition.packageName
            familyName = [PtPuvr.PackageIdentityNative]::FamilyName(
                $definition.packageName,
                $publisher,
                [uint16]$definition.track)
            fullName = [PtPuvr.PackageIdentityNative]::FullName(
                $definition.packageName,
                $publisher,
                [uint16]$definition.track)
            packageVersion = $definition.version
            fileVersion = (Get-Item -LiteralPath $definition.binary).VersionInfo.FileVersion
            path = $msix
            sha256 = (Get-FileHash -LiteralPath $msix -Algorithm SHA256).Hash
        }
    }

    foreach ($definition in $runtimeDefinitions) {
        $bundleName = "PowerToys-$($definition.simulatedPowerToysVersion)"
        $bundle = Join-Path $bundleRoot $bundleName
        New-Item -ItemType Directory -Path $bundle -Force | Out-Null
        $updaterCopy = Join-Path $bundle 'PtPuvrUpdater-5.0.0.0.msix'
        $runtimeCopy = Join-Path $bundle $definition.packageFile
        Copy-Item -LiteralPath $metadata.updater.path -Destination $updaterCopy
        Copy-Item `
            -LiteralPath $metadata.runtimes["track$($definition.track)"].path `
            -Destination $runtimeCopy
        $copiedUpdaterHash = (
            Get-FileHash -LiteralPath $updaterCopy -Algorithm SHA256).Hash
        if ($copiedUpdaterHash -ne $metadata.updater.sha256) {
            throw "Updater artifact changed in simulated $bundleName bundle."
        }
        $metadata.simulatedBundles[$bundleName] = [ordered]@{
            updaterPath = $updaterCopy
            updaterSha256 = $copiedUpdaterHash
            runtimeTrack = $definition.track
            runtimePath = $runtimeCopy
        }
    }

    $metadata |
        ConvertTo-Json -Depth 8 |
        Set-Content (Join-Path $packageRoot 'packages.json') -Encoding utf8NoBOM
    Write-Host 'Built raw-SCM updater MSIX versions 5/6 and runtime MSIX tracks 1/2.'
    Write-Host 'The simulated PowerToys 0.101 and 0.110 bundles contain byte-identical updater v5 MSIX files.'
}
finally {
    Remove-Item `
        "Cert:\CurrentUser\My\$($certificate.Thumbprint)" `
        -Force `
        -ErrorAction SilentlyContinue
}
