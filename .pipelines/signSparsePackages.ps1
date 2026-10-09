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

.PARAMETER RequiredAuthenticodeFile
Filename patterns for unpackaged companion binaries that must be found and signed with the same
machine-trusted TEST identity. This is used for authenticated PowerToys IPC on unsigned CI builds.

.PARAMETER AuthenticodePublisher
Certificate subject used to sign RequiredAuthenticodeFile matches. The default matches the
Microsoft publisher identity required by PowerToys' Release IPC caller authentication.

.PARAMETER CertificateMarkerPath
Optional durable text file that receives each test certificate thumbprint used by this invocation.
CI uses the marker before signing and from an <c>always()</c> cleanup step to remove the trust anchor
and private key, including after an interrupted prior job.

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

    [Parameter()]
    [string[]]$RequiredAuthenticodeFile = @(),

    [Parameter()]
    [string]$AuthenticodePublisher = 'CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US',

    [Parameter()]
    [string]$CertificateMarkerPath,

    [switch]$Force,

    [switch]$SkipLocalTrust,

    [Parameter()]
    [string]$ExportCertificatePath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$testCertificateFriendlyName = 'PowerToys UI Test Signing'

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
# including the App Certification Kit). The pinned, signature-verified NuGet fallback is separate.
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

    return Select-SignToolByArch -Paths $found
}

# Last resort when the agent has no Windows SDK: fetch signtool from the public
# Microsoft.Windows.SDK.BuildTools NuGet package. Best-effort.
function Get-SignToolFromNuget {
    $nupkg = $null
    $archive = $null
    try {
        [xml]$centralPackages = Get-Content (Join-Path $PSScriptRoot '..\Directory.Packages.props') -Raw
        $versionNode = $centralPackages.SelectSingleNode(
            "/Project/ItemGroup/PackageVersion[@Include='Microsoft.Windows.SDK.BuildTools']")
        $version = if ($versionNode) { $versionNode.GetAttribute('Version') } else { $null }
        if ($version -notmatch '^\d+\.\d+\.\d+\.\d+$') {
            throw 'Microsoft.Windows.SDK.BuildTools must have a concrete four-part version in Directory.Packages.props.'
        }

        $dest = Join-Path $env:TEMP "pt-sdk-buildtools-$version"
        $nupkg = Join-Path $env:TEMP "sdk-buildtools-$version.nupkg"
        $archive = Join-Path $env:TEMP "sdk-buildtools-$version.zip"
        $packageFileName = "microsoft.windows.sdk.buildtools.$version.nupkg"
        $cachedPackage = @($env:NUGET_PACKAGES, (Join-Path $env:USERPROFILE '.nuget\packages')) |
            Where-Object { $_ } |
            ForEach-Object { Join-Path $_ "microsoft.windows.sdk.buildtools\$version\$packageFileName" } |
            Where-Object { Test-Path -LiteralPath $_ } |
            Select-Object -First 1
        if ($cachedPackage) {
            Write-Host "signtool not found on the agent; verifying cached Windows SDK BuildTools $version."
            Copy-Item -LiteralPath $cachedPackage -Destination $nupkg -Force
        }
        else {
            Write-Host "signtool not found on the agent; fetching Windows SDK BuildTools $version from NuGet."
            Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/microsoft.windows.sdk.buildtools/$version/$packageFileName" -OutFile $nupkg -UseBasicParsing
        }

        $verificationOutput = @(& dotnet nuget verify $nupkg --all 2>&1)
        if ($LASTEXITCODE -ne 0) {
            throw "NuGet signature verification failed for Microsoft.Windows.SDK.BuildTools ${version}: $($verificationOutput -join [Environment]::NewLine)"
        }
        $verificationOutput | ForEach-Object { Write-Host $_ }

        Copy-Item $nupkg $archive -Force
        Remove-Item $dest -Recurse -Force -ErrorAction SilentlyContinue
        Expand-Archive -Path $archive -DestinationPath $dest -Force
        $paths = Get-ChildItem -Path $dest -Recurse -Filter 'signtool.exe' -File -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty FullName
        return Select-SignToolByArch -Paths $paths
    }
    catch {
        Write-Warning "Could not obtain signtool from NuGet: $($_.Exception.Message)"
        return $null
    }
    finally {
        @($nupkg, $archive) | Where-Object { $_ } | ForEach-Object {
            Remove-Item -LiteralPath $_ -Force -ErrorAction SilentlyContinue
        }
    }
}

function Get-VerifiedSignTool {
    $path = Find-SignTool
    if (-not $path) { $path = Get-SignToolFromNuget }
    if (-not $path) {
        throw 'signtool.exe not found and could not be fetched from NuGet. Install the Windows SDK.'
    }

    $signature = Get-AuthenticodeSignature -FilePath $path
    if ($signature.Status -ne 'Valid' -or
        -not $signature.SignerCertificate -or
        $signature.SignerCertificate.Subject -notmatch '(^|,\s*)O=Microsoft Corporation(,|$)') {
        throw "signtool.exe is not validly signed by Microsoft: $path"
    }

    return $path
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
        Where-Object {
            $_.Subject -eq $Subject -and
            $_.HasPrivateKey -and
            $_.FriendlyName -eq $testCertificateFriendlyName
        } |
        Sort-Object NotAfter -Descending | Select-Object -First 1

    if (-not $cert) {
        Write-Host "Creating self-signed test certificate for: $Subject"
        $cert = New-SelfSignedCertificate -Subject $Subject `
            -CertStoreLocation Cert:\CurrentUser\My `
            -FriendlyName $testCertificateFriendlyName `
            -KeyAlgorithm RSA -KeyLength 2048 `
            -Type CodeSigningCert -HashAlgorithm SHA256 `
            -NotAfter (Get-Date).AddYears(1)
    }

    if ($CertificateMarkerPath) {
        $markerParent = Split-Path $CertificateMarkerPath -Parent
        if ($markerParent -and -not (Test-Path $markerParent)) {
            New-Item $markerParent -ItemType Directory -Force | Out-Null
        }

        $recordedThumbprints = if (Test-Path $CertificateMarkerPath) {
            @(Get-Content $CertificateMarkerPath | Where-Object { $_ })
        } else {
            @()
        }
        if ($recordedThumbprints -notcontains $cert.Thumbprint) {
            Add-Content -Path $CertificateMarkerPath -Value $cert.Thumbprint -Encoding ascii
        }
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

$requiredAuthenticodeFiles = @()
foreach ($pattern in ($RequiredAuthenticodeFile | Where-Object { $_ } | Select-Object -Unique)) {
    $matches = @()
    foreach ($root in $PackageRoot) {
        if (Test-Path $root) {
            $matches += Get-ChildItem -Path $root -Recurse -File -Filter $pattern -ErrorAction SilentlyContinue
        }
    }

    $matches = @($matches | Sort-Object FullName -Unique)
    if ($matches.Count -eq 0) {
        throw "Required Authenticode file '$pattern' was not found under: $($PackageRoot -join ', ')."
    }

    $requiredAuthenticodeFiles += $matches
}
$requiredAuthenticodeFiles = @($requiredAuthenticodeFiles | Sort-Object FullName -Unique)

if (-not $packages -and $RequiredPackage.Count -gt 0) {
    throw "No packages found under '$($PackageRoot -join ', ')' while requiring: $($RequiredPackage -join ', ')."
}

if (-not $packages -and $requiredAuthenticodeFiles.Count -eq 0) {
    Write-Host "No packages or required Authenticode files found under: $($PackageRoot -join ', ')"
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
        $signtool = Get-VerifiedSignTool
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

if ($requiredAuthenticodeFiles.Count -gt 0) {
    if (-not $signtool) {
        $signtool = Get-VerifiedSignTool
        Write-Host "Using signtool: $signtool"
    }

    $filesToSign = @($requiredAuthenticodeFiles | Where-Object {
        $existing = Get-AuthenticodeSignature -FilePath $_.FullName
        $Force -or $existing.Status -ne 'Valid'
    })
    $testSignedPaths = @{}
    $cert = if ($filesToSign.Count -gt 0) {
        Get-TrustedSigningCert -Subject $AuthenticodePublisher
    } else {
        $null
    }

    foreach ($file in $filesToSign) {

        Write-Host "Signing companion binary: $($file.FullName)"
        & $signtool sign /fd SHA256 /sha1 $cert.Thumbprint $file.FullName
        if ($LASTEXITCODE -ne 0) {
            throw "signtool failed for required Authenticode file '$($file.FullName)' (exit $LASTEXITCODE)."
        }

        $testSignedPaths[$file.FullName] = $true
    }

    $invalidAuthenticodeFiles = @($requiredAuthenticodeFiles | Where-Object {
        $signature = Get-AuthenticodeSignature -FilePath $_.FullName
        $signerName = if ($signature.SignerCertificate) {
            $signature.SignerCertificate.GetNameInfo([Security.Cryptography.X509Certificates.X509NameType]::SimpleName, $false)
        } else {
            $null
        }
        if ($testSignedPaths.ContainsKey($_.FullName)) {
            -not $signature.SignerCertificate -or
                $signature.SignerCertificate.Thumbprint -ne $cert.Thumbprint -or
                (-not $SkipLocalTrust -and $signature.Status -ne 'Valid')
        } else {
            -not $signature.SignerCertificate -or
                $signerName -ne 'Microsoft Corporation' -or
                $signature.Status -ne 'Valid'
        }
    })
    if ($invalidAuthenticodeFiles.Count -gt 0) {
        throw "Required Authenticode file(s) are not signed with the trusted test identity: $($invalidAuthenticodeFiles.FullName -join ', ')."
    }

    Write-Host "Verified required Authenticode file(s): $($requiredAuthenticodeFiles.FullName -join ', ')"
}

Write-Host "Signed $signed package(s) with a trusted test certificate."
