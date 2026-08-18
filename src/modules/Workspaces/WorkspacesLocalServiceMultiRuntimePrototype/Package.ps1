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
$runtime = Join-Path $root "artifacts\bin\x64\$Configuration\PtLsmrRuntime.exe"
if (-not (Test-Path $runtime)) {
    throw "Runtime build artifact is missing: $runtime"
}

$publisher = 'CN=PowerToys Workspaces LocalService Multi Runtime Prototype Test'
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
    New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
    $cerPath = Join-Path $packageRoot 'PtLsmr-TestOnly.cer'
    Export-Certificate -Cert $certificate -FilePath $cerPath -Force | Out-Null
    Import-Certificate -FilePath $cerPath -CertStoreLocation Cert:\CurrentUser\TrustedPeople | Out-Null
    if ($TrustMachine) {
        Import-Certificate -FilePath $cerPath -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
    }

    if (-not ('PtLsmr.PackageIdentityNative' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
namespace PtLsmr {
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
        public static string FullName(string name, string publisher, ushort major) {
            IntPtr n = Marshal.StringToHGlobalUni(name);
            IntPtr p = Marshal.StringToHGlobalUni(publisher);
            try {
                PACKAGE_ID id = new PACKAGE_ID {
                    processorArchitecture = 9,
                    version = new PACKAGE_VERSION { Major = major },
                    name = n,
                    publisher = p
                };
                UInt32 length = 0;
                int result = PackageFullNameFromId(ref id, ref length, IntPtr.Zero);
                if (result != 122) throw new InvalidOperationException("PackageFullNameFromId(size): " + result);
                IntPtr buffer = Marshal.AllocHGlobal(checked((int)length * 2));
                try {
                    result = PackageFullNameFromId(ref id, ref length, buffer);
                    if (result != 0) throw new InvalidOperationException("PackageFullNameFromId: " + result);
                    return Marshal.PtrToStringUni(buffer);
                } finally { Marshal.FreeHGlobal(buffer); }
            } finally {
                Marshal.FreeHGlobal(n); Marshal.FreeHGlobal(p);
            }
        }
    }
}
'@
    }

    $png = [Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=')
    $metadata = [ordered]@{
        packageName = 'Microsoft.PowerToys.WsLocalSvcMultiRt'
        publisher = $publisher
        packageFamily = $null
        packages = @{}
        certificatePath = $cerPath
        certificateThumbprint = $certificate.Thumbprint
    }
    foreach ($major in 1, 2) {
        $stage = Join-Path $packageRoot "stage-v$major"
        Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
        New-Item -ItemType Directory -Path (Join-Path $stage 'Assets') -Force | Out-Null
        Copy-Item $runtime (Join-Path $stage 'PtLsmrRuntime.exe')
        $manifest = (Get-Content (Join-Path $root 'Packaging\AppxManifest.template.xml') -Raw).
            Replace('@@VERSION@@', "$major.0.0.0")
        Set-Content -Path (Join-Path $stage 'AppxManifest.xml') -Value $manifest -Encoding utf8NoBOM
        foreach ($logo in 'StoreLogo.png', 'Square44x44Logo.png', 'Square150x150Logo.png') {
            [IO.File]::WriteAllBytes((Join-Path $stage "Assets\$logo"), $png)
        }
        $msix = Join-Path $packageRoot "PtLsmrRuntime-v$major.msix"
        & $makeappx pack /o /d $stage /p $msix
        if ($LASTEXITCODE -ne 0) { throw "makeappx failed for v$major." }
        & $signtool sign /fd SHA256 /sha1 $certificate.Thumbprint /s My $msix
        if ($LASTEXITCODE -ne 0) { throw "signtool failed for v$major." }
        & $signtool verify /pa /v $msix
        if ($LASTEXITCODE -ne 0 -and $TrustMachine) { throw "signtool verification failed for v$major." }
        $metadata.packages["v$major"] = [ordered]@{
            fullName = [PtLsmr.PackageIdentityNative]::FullName($metadata.packageName, $publisher, [uint16]$major)
            version = "$major.0.0.0"
            path = $msix
        }
    }
    $metadata | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $packageRoot 'packages.json') -Encoding utf8NoBOM
    Write-Host "Built and signed v1/v2; no package registration was created for the interactive user."
}
finally {
    Remove-Item "Cert:\CurrentUser\My\$($certificate.Thumbprint)" -Force -ErrorAction SilentlyContinue
}
