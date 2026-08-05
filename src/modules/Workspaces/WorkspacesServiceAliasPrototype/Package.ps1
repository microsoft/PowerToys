[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [ValidateSet('v1', 'v2')]
    [string]$StageVersion = 'v1',
    [switch]$SkipBuild,
    [switch]$SkipStage,
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
    throw "Windows SDK makeappx.exe was not found."
}
$makeappx = Join-Path $sdk.FullName 'x64\makeappx.exe'
$signtool = Join-Path $sdk.FullName 'x64\signtool.exe'
$worker = Join-Path $root "artifacts\bin\x64\$Configuration\PtAliasProtoWorker.exe"
if (-not (Test-Path $worker)) {
    throw "Worker build artifact is missing: $worker"
}

$publisher = 'CN=PowerToys PtAliasProto Test'
$certificate = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert |
    Where-Object { $_.Subject -eq $publisher -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date).AddDays(7) } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1
if (-not $certificate) {
    $certificate = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $publisher `
        -CertStoreLocation Cert:\CurrentUser\My `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -HashAlgorithm SHA256 `
        -KeyExportPolicy NonExportable `
        -NotAfter (Get-Date).AddYears(2)
}

try {
$packageRoot = Join-Path $root 'artifacts\packages'
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
$cerPath = Join-Path $packageRoot 'PtAliasProto-TestOnly.cer'
Export-Certificate -Cert $certificate -FilePath $cerPath -Force | Out-Null
Import-Certificate -FilePath $cerPath -CertStoreLocation Cert:\CurrentUser\TrustedPeople | Out-Null
if ($TrustMachine) {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "-TrustMachine requires an elevated PowerShell session."
    }
    Import-Certificate -FilePath $cerPath -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
}

if (-not ('PtAliasProto.PackageIdentityNative' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
namespace PtAliasProto {
    public static class PackageIdentityNative {
        [StructLayout(LayoutKind.Sequential)]
        private struct PACKAGE_ID {
            public UInt32 reserved;
            public UInt32 processorArchitecture;
            public UInt64 version;
            public IntPtr name;
            public IntPtr publisher;
            public IntPtr resourceId;
            public IntPtr publisherId;
        }
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode)]
        private static extern int PackageFullNameFromId(ref PACKAGE_ID id, ref UInt32 length, IntPtr fullName);
        public static string FullName(string name, string publisher, UInt16 major) {
            IntPtr n = Marshal.StringToHGlobalUni(name);
            IntPtr p = Marshal.StringToHGlobalUni(publisher);
            try {
                PACKAGE_ID id = new PACKAGE_ID {
                    processorArchitecture = 9,
                    version = ((UInt64)major << 48),
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
                } finally {
                    Marshal.FreeHGlobal(buffer);
                }
            } finally {
                Marshal.FreeHGlobal(n);
                Marshal.FreeHGlobal(p);
            }
        }
    }
}
'@
}

$png = [Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=')
$metadata = [ordered]@{
    packageName = 'Microsoft.PowerToys.PtAliasProto'
    publisher = $publisher
    alias = 'PtAliasProtoWorker.exe'
    certificatePath = $cerPath
    certificateThumbprint = $certificate.Thumbprint
    invalidUnstagedFullName = [PtAliasProto.PackageIdentityNative]::FullName(
        'Microsoft.PowerToys.PtAliasProto',
        $publisher,
        [uint16]3)
    packages = @{}
}
$interactiveBefore = @(Get-AppxPackage -Name $metadata.packageName -ErrorAction SilentlyContinue).PackageFullName

foreach ($major in 1, 2) {
    $version = "$major.0.0.0"
    $stage = Join-Path $packageRoot "stage-v$major"
    Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path (Join-Path $stage 'Assets') -Force | Out-Null
    Copy-Item $worker (Join-Path $stage 'PtAliasProtoWorker.exe')
    $manifest = (Get-Content (Join-Path $root 'Packaging\AppxManifest.template.xml') -Raw).Replace('@@VERSION@@', $version)
    Set-Content -Path (Join-Path $stage 'AppxManifest.xml') -Value $manifest -Encoding utf8NoBOM
    [IO.File]::WriteAllBytes((Join-Path $stage 'Assets\StoreLogo.png'), $png)
    [IO.File]::WriteAllBytes((Join-Path $stage 'Assets\Square44x44Logo.png'), $png)
    [IO.File]::WriteAllBytes((Join-Path $stage 'Assets\Square150x150Logo.png'), $png)
    $msix = Join-Path $packageRoot "PtAliasProto-v$major.msix"
    & $makeappx pack /o /d $stage /p $msix
    if ($LASTEXITCODE -ne 0) { throw "makeappx failed for v$major." }
    & $signtool sign /fd SHA256 /sha1 $certificate.Thumbprint /s My $msix
    if ($LASTEXITCODE -ne 0) { throw "signtool failed for v$major." }
    & $signtool verify /pa /v $msix
    if ($LASTEXITCODE -ne 0) {
        $signature = Get-AuthenticodeSignature $msix
        if (-not $signature.SignerCertificate -or $signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) {
            throw "signtool verification failed for v$major and the signer thumbprint did not match."
        }
        Write-Warning "v$major signature is structurally present but its test-only root is not machine-trusted. Use -TrustMachine before service-account registration."
    }
    $fullName = [PtAliasProto.PackageIdentityNative]::FullName($metadata.packageName, $publisher, [uint16]$major)
    $metadata.packages["v$major"] = [ordered]@{
        version = $version
        fullName = $fullName
        path = $msix
    }
}

$interactiveAfter = @(Get-AppxPackage -Name $metadata.packageName -ErrorAction SilentlyContinue).PackageFullName
$newInteractiveRegistrations = @($interactiveAfter | Where-Object { $_ -notin $interactiveBefore })
if ($newInteractiveRegistrations.Count -ne 0) {
    throw "Packaging unexpectedly registered the package for the interactive user: $($newInteractiveRegistrations -join ', ')"
}
$metadata | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $packageRoot 'packages.json') -Encoding utf8NoBOM
if (-not $SkipStage) {
    Add-AppxPackage -Path $metadata.packages.$StageVersion.path -Stage -ForceUpdateFromAnyVersion
}
Write-Host "Built signed v1/v2 packages and staged $StageVersion. Interactive-user registration count added: 0"
Write-Host "Metadata: $(Join-Path $packageRoot 'packages.json')"
}
finally {
    if ($certificate -and $certificate.HasPrivateKey) {
        Remove-Item "Cert:\CurrentUser\My\$($certificate.Thumbprint)" -Force -ErrorAction SilentlyContinue
    }
}
