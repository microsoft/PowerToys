<#
.SYNOPSIS
Self-sign PowerToys sparse MSIX shell-extension packages with a machine-trusted TEST certificate so
they register on unsigned CI builds, letting UI tests exercise the real end-user workflow (the modern
Windows 11 context menu) instead of signing-free fallbacks.

.DESCRIPTION
CI PR-validation builds are produced with codeSign:false, so every sparse-MSIX shell extension
(ImageResizer / PowerRename / FileLocksmith / NewPlus context menus, the CmdPal PowerToysSparse
package) ships UNSIGNED. PowerToys registers these at module-enable time via
PackageManager.AddPackageByUriAsync, which requires a signature that chains to a trusted root, so on
CI they fail with 0x800B0100 (TRUST_E_NOSIGNATURE) and the modern context menu never appears.

Run this on the test agent AFTER the build is downloaded/installed and BEFORE PowerToys enables the
module. For every package it:
  1. reads the manifest Publisher subject,
  2. ensures a self-signed code-signing certificate with that exact subject exists,
  3. force-trusts that certificate (LocalMachine + CurrentUser Root and TrustedPeople), and
  4. signs the package with signtool.

This asserts NO security -- it is a test-only trust anchor for validating normal app usage. It only
signs packages that are not already validly signed (unless -Force), so real framework packages
(VCLibs, WindowsAppSDK) are left untouched.

.PARAMETER PackageRoot
One or more folders to search recursively for sparse packages. Missing folders are skipped, so you
can pass both the run-in-place build tree and the installed location:
  -PackageRoot "$(Pipeline.Workspace)\build-x64-Release", "$env:ProgramFiles\PowerToys\WinUI3Apps"

.PARAMETER Include
Filename patterns to sign. Defaults to *.msix and *.appx.

.PARAMETER Force
Re-sign even packages that already carry a valid signature.

.EXAMPLE
.\signSparsePackages.ps1 -PackageRoot "$env:ProgramFiles\PowerToys\WinUI3Apps"

.EXAMPLE
# Local sideloading into a UI-test VM runtime:
.\signSparsePackages.ps1 -PackageRoot "C:\PowerToysUiTestRun\PowerToys\WinUI3Apps"
#>
param(
    [Parameter(Mandatory = $true)]
    [string[]]$PackageRoot,

    [Parameter()]
    [string[]]$Include = @('*.msix', '*.appx'),

    [switch]$Force
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Find-SignTool {
    $cmd = Get-Command signtool -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $roots = @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin",
        "$env:ProgramFiles\Windows Kits\10\bin"
    )
    foreach ($root in $roots) {
        if (-not (Test-Path $root)) { continue }
        $versions = Get-ChildItem $root -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } |
            Sort-Object Name -Descending
        foreach ($v in $versions) {
            foreach ($arch in @('x64', 'x86', 'arm64')) {
                $candidate = Join-Path $v.FullName "$arch\signtool.exe"
                if (Test-Path $candidate) { return $candidate }
            }
        }
    }
    throw "signtool.exe not found. Install the Windows SDK."
}

# Read <Identity Publisher="..."> straight out of the .msix/.appx (a zip) without extracting it.
function Get-PackagePublisher {
    param([string]$PackagePath)

    $zip = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $entry = $zip.GetEntry('AppxManifest.xml')
        if (-not $entry) { return $null }
        $reader = New-Object System.IO.StreamReader($entry.Open())
        try { $xml = [xml]$reader.ReadToEnd() } finally { $reader.Dispose() }
        return $xml.Package.Identity.Publisher
    }
    finally {
        $zip.Dispose()
    }
}

function Import-CertTrust {
    param(
        [string]$CerPath,
        [string]$Thumbprint,
        [string]$StorePath,
        [switch]$Optional
    )

    if (Get-ChildItem $StorePath -ErrorAction SilentlyContinue | Where-Object { $_.Thumbprint -eq $Thumbprint }) {
        return $true
    }
    try {
        Import-Certificate -FilePath $CerPath -CertStoreLocation $StorePath -ErrorAction Stop | Out-Null
        return $true
    }
    catch {
        if ($Optional) {
            Write-Warning "Could not import test cert into $StorePath (admin may be required): $($_.Exception.Message)"
            return $false
        }
        throw
    }
}

$certCache = @{}
function Get-TrustedSigningCert {
    param([string]$Subject)

    if ($certCache.ContainsKey($Subject)) { return $certCache[$Subject] }

    $cert = Get-ChildItem Cert:\CurrentUser\My |
        Where-Object { $_.Subject -eq $Subject -and $_.HasPrivateKey } |
        Sort-Object NotAfter -Descending | Select-Object -First 1

    if (-not $cert) {
        Write-Host "Creating self-signed test certificate for: $Subject"
        $cert = New-SelfSignedCertificate -Subject $Subject `
            -CertStoreLocation Cert:\CurrentUser\My `
            -KeyAlgorithm RSA -KeyLength 2048 `
            -Type CodeSigningCert -HashAlgorithm SHA256 `
            -NotAfter (Get-Date).AddYears(1)
    }

    # Force-trust so AddPackageByUriAsync accepts the signature. A self-signed cert is its own root,
    # so it must live in a Root store (chain) and TrustedPeople (AppX sideload allow-list). Use the
    # LocalMachine stores: they import silently and the elevated CI test agent can write them.
    # CurrentUser\Root is deliberately NOT used -- importing into the user Root store raises a CryptoAPI
    # consent dialog that fails non-interactively ("UI is not allowed in this operation"), even elevated.
    $cerPath = Join-Path $env:TEMP ("pt-test-signer-{0}.cer" -f $cert.Thumbprint)
    Export-Certificate -Cert $cert -FilePath $cerPath -Force | Out-Null

    $rootTrusted = Import-CertTrust -CerPath $cerPath -Thumbprint $cert.Thumbprint -StorePath 'Cert:\LocalMachine\Root' -Optional
    Import-CertTrust -CerPath $cerPath -Thumbprint $cert.Thumbprint -StorePath 'Cert:\LocalMachine\TrustedPeople' -Optional | Out-Null
    Import-CertTrust -CerPath $cerPath -Thumbprint $cert.Thumbprint -StorePath 'Cert:\CurrentUser\TrustedPeople' -Optional | Out-Null
    if (-not $rootTrusted) {
        Write-Warning "Could not establish machine root trust for '$Subject' (run elevated). Signed packages may not register."
    }

    $certCache[$Subject] = $cert
    return $cert
}

$signtool = Find-SignTool
Write-Host "Using signtool: $signtool"

$packages = @()
foreach ($root in $PackageRoot) {
    if (-not (Test-Path $root)) {
        Write-Host "Skipping missing package root: $root"
        continue
    }
    $packages += Get-ChildItem -Path $root -Recurse -File -Include $Include -ErrorAction SilentlyContinue
}
$packages = $packages | Sort-Object FullName -Unique

if (-not $packages) {
    Write-Host "No packages found under: $($PackageRoot -join ', ')"
    return
}

$signed = 0
foreach ($pkg in $packages) {
    if (-not $Force) {
        $existing = Get-AuthenticodeSignature -FilePath $pkg.FullName
        if ($existing.Status -eq 'Valid') {
            Write-Host "Already validly signed, skipping: $($pkg.Name)"
            continue
        }
    }

    $publisher = $null
    try { $publisher = Get-PackagePublisher -PackagePath $pkg.FullName } catch { }
    if (-not $publisher) {
        Write-Host "No manifest publisher, skipping: $($pkg.Name)"
        continue
    }

    $cert = Get-TrustedSigningCert -Subject $publisher
    Write-Host "Signing $($pkg.Name)  (Publisher: $publisher)"
    & $signtool sign /fd SHA256 /sha1 $cert.Thumbprint $pkg.FullName
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "signtool failed for $($pkg.Name) (exit $LASTEXITCODE)"
        continue
    }

    $verify = Get-AuthenticodeSignature -FilePath $pkg.FullName
    if ($verify.Status -eq 'Valid') {
        $signed++
    }
    else {
        Write-Warning "Signature not Valid after signing $($pkg.Name): $($verify.Status)"
    }
}

Write-Host "Signed $signed package(s) with a trusted test certificate."
