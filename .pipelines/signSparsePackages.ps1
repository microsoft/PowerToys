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
    -PackageRoot "$(Pipeline.Workspace)\build-x64-Release", "$env:ProgramFiles\PowerToys"

.PARAMETER Include
Filename patterns to sign. Defaults to *.msix and *.appx.

.PARAMETER RequiredPackage
Filename patterns that must be found and end with a Valid signature. Missing, unsigned, or untrusted
matches make the script fail after attempting all packages.

.PARAMETER Force
Re-sign even packages that already carry a valid signature.

.PARAMETER SkipLocalTrust
Sign without importing the test certificate into this machine's trust stores. Use when signing on a
build host and registering the package somewhere else (for example packaging a UI-test payload on the
host and running it in a VM), so the build machine never gains a test trust anchor.

.PARAMETER ExportCertificatePath
Write the public certificate to this path so the machine that registers the package can trust it.

.EXAMPLE
.\signSparsePackages.ps1 -PackageRoot "$env:ProgramFiles\PowerToys" `
    -RequiredPackage ImageResizerContextMenuPackage.msix

.EXAMPLE
# Local sideloading into a UI-test VM runtime:
.\signSparsePackages.ps1 -PackageRoot "C:\PowerToysUiTestRun\PowerToys"

.EXAMPLE
# Sign a payload on the host, trust it only inside the VM that will register it:
.\signSparsePackages.ps1 -PackageRoot "X:\payload\product" -SkipLocalTrust `
    -ExportCertificatePath "X:\payload\pt-test-signer.cer"
#>
param(
    [Parameter(Mandatory = $true)]
    [string[]]$PackageRoot,

    [Parameter()]
    [string[]]$Include = @('*.msix', '*.appx'),

    [Parameter()]
    [string[]]$RequiredPackage = @(),

    [switch]$Force,

    [switch]$SkipLocalTrust,

    [Parameter()]
    [string]$ExportCertificatePath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Select-SignToolByArch {
    param([string[]]$Paths)

    $paths = @($Paths | Where-Object { $_ } | Select-Object -Unique)
    if (-not $paths) { return $null }
    $archPref = @($env:PROCESSOR_ARCHITECTURE, 'x64', 'x86', 'arm64') |
        ForEach-Object { $_.ToLower() } | Select-Object -Unique
    foreach ($arch in $archPref) {
        $match = $paths | Where-Object { $_ -match "\\$arch\\" } | Select-Object -First 1
        if ($match) { return $match }
    }
    return $paths[0]
}

# Locate signtool.exe on the agent: PATH, then any Windows Kits install (all versions/layouts,
# including the App Certification Kit), then a restored SDK BuildTools NuGet package.
function Find-SignTool {
    $cmd = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $found = @()
    $kitRoots = @(
        "${env:ProgramFiles(x86)}\Windows Kits",
        "$env:ProgramFiles\Windows Kits",
        "$env:ProgramW6432\Windows Kits"
    ) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -Unique
    foreach ($root in $kitRoots) {
        # Scope to bin\ and the App Certification Kit so the huge Include\ / Lib\ trees are skipped.
        $scopes = Get-ChildItem -Path $root -Directory -Recurse -Depth 1 -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -eq 'bin' -or $_.Name -eq 'App Certification Kit' } |
            Select-Object -ExpandProperty FullName
        foreach ($scope in $scopes) {
            $found += Get-ChildItem -Path $scope -Recurse -Filter 'signtool.exe' -File -ErrorAction SilentlyContinue |
                Select-Object -ExpandProperty FullName
        }
    }

    $nugetRoots = @($env:NUGET_PACKAGES, (Join-Path $env:USERPROFILE '.nuget\packages')) |
        Where-Object { $_ } |
        ForEach-Object { Join-Path $_ 'microsoft.windows.sdk.buildtools' } |
        Where-Object { Test-Path $_ } | Select-Object -Unique
    foreach ($root in $nugetRoots) {
        $found += Get-ChildItem -Path $root -Recurse -Filter 'signtool.exe' -File -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty FullName
    }

    return Select-SignToolByArch -Paths $found
}

# Last resort when the agent has no Windows SDK: fetch signtool from the public
# Microsoft.Windows.SDK.BuildTools NuGet package (cached in TEMP across runs). Best-effort.
function Get-SignToolFromNuget {
    try {
        $index = Invoke-RestMethod 'https://api.nuget.org/v3-flatcontainer/microsoft.windows.sdk.buildtools/index.json' -UseBasicParsing
        $version = @($index.versions | Where-Object { $_ -match '^\d+\.\d+\.\d+\.\d+$' })[-1]
        if (-not $version) { return $null }

        $dest = Join-Path $env:TEMP "pt-sdk-buildtools-$version"
        if (-not (Get-ChildItem -Path $dest -Recurse -Filter 'signtool.exe' -File -ErrorAction SilentlyContinue)) {
            Write-Host "signtool not found on the agent; fetching Windows SDK BuildTools $version from NuGet."
            $nupkg = Join-Path $env:TEMP "sdk-buildtools-$version.zip"
            Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/microsoft.windows.sdk.buildtools/$version/microsoft.windows.sdk.buildtools.$version.nupkg" -OutFile $nupkg -UseBasicParsing
            Expand-Archive -Path $nupkg -DestinationPath $dest -Force
            Remove-Item $nupkg -Force -ErrorAction SilentlyContinue
        }
        $paths = Get-ChildItem -Path $dest -Recurse -Filter 'signtool.exe' -File -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty FullName
        return Select-SignToolByArch -Paths $paths
    }
    catch {
        Write-Warning "Could not obtain signtool from NuGet: $($_.Exception.Message)"
        return $null
    }
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

    if ($ExportCertificatePath) {
        $exportParent = Split-Path $ExportCertificatePath -Parent
        if ($exportParent -and -not (Test-Path $exportParent)) {
            New-Item $exportParent -ItemType Directory -Force | Out-Null
        }
        Copy-Item $cerPath $ExportCertificatePath -Force
        Write-Host "Exported public certificate to: $ExportCertificatePath"
    }

    if ($SkipLocalTrust) {
        Write-Host "Skipping local trust for '$Subject'; trust the exported certificate where the package is registered."
        $certCache[$Subject] = $cert
        return $cert
    }

    $rootTrusted = Import-CertTrust -CerPath $cerPath -Thumbprint $cert.Thumbprint -StorePath 'Cert:\LocalMachine\Root' -Optional
    Import-CertTrust -CerPath $cerPath -Thumbprint $cert.Thumbprint -StorePath 'Cert:\LocalMachine\TrustedPeople' -Optional | Out-Null
    Import-CertTrust -CerPath $cerPath -Thumbprint $cert.Thumbprint -StorePath 'Cert:\CurrentUser\TrustedPeople' -Optional | Out-Null
    if (-not $rootTrusted) {
        Write-Warning "Could not establish machine root trust for '$Subject' (run elevated). Signed packages may not register."
    }

    $certCache[$Subject] = $cert
    return $cert
}

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
    if ($RequiredPackage.Count -gt 0) {
        throw "No packages found under '$($PackageRoot -join ', ')' while requiring: $($RequiredPackage -join ', ')."
    }

    Write-Host "No packages found under: $($PackageRoot -join ', ')"
    return
}

$requiredPackages = @()
foreach ($pattern in ($RequiredPackage | Where-Object { $_ } | Select-Object -Unique)) {
    $matches = @($packages | Where-Object { $_.Name -like $pattern })
    if ($matches.Count -eq 0) {
        throw "Required sparse package '$pattern' was not found under: $($PackageRoot -join ', ')."
    }

    $requiredPackages += $matches
}
$requiredPackages = @($requiredPackages | Sort-Object FullName -Unique)

$signed = 0
$signtool = $null
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

    if (-not $signtool) {
        $signtool = Find-SignTool
        if (-not $signtool) { $signtool = Get-SignToolFromNuget }
        if (-not $signtool) { throw 'signtool.exe not found and could not be fetched from NuGet. Install the Windows SDK.' }
        Write-Host "Using signtool: $signtool"
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

if ($requiredPackages.Count -gt 0) {
    $invalidRequiredPackages = @($requiredPackages | Where-Object {
        (Get-AuthenticodeSignature -FilePath $_.FullName).Status -ne 'Valid'
    })
    if ($invalidRequiredPackages.Count -gt 0) {
        throw "Required sparse package(s) are not validly signed and trusted: $($invalidRequiredPackages.FullName -join ', ')."
    }

    Write-Host "Verified required sparse package(s): $($requiredPackages.FullName -join ', ')"
}

Write-Host "Signed $signed package(s) with a trusted test certificate."
