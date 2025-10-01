Param(
    [Parameter(Mandatory=$false)]
    [string]$Platform = "x64",
    [Parameter(Mandatory=$false)]
    [string]$Configuration = "Release",
    [switch]$Clean,
    [switch]$ForceCert,
    [switch]$NoSign
)

# PowerOCR adaptation of Windows AI Foundry sparse packaging sample.
# Generates a sparse MSIX (no payload) that points to the existing unpackaged PowerOCR binaries
# in the specified build output folder so the process can obtain package identity.
#
# OUTPUT ARTIFACT (placeholder): PowerOCRSparse.msix
# Adjust constants / paths below if your layout differs.

$ErrorActionPreference = 'Stop'

# Project root folder (script now lives in PowerOCR.PackagingSparse, so go up to PowerOCR)
$ProjectRoot = Join-Path $PSScriptRoot "..\PowerOCR"
$UserFolder = Join-Path $ProjectRoot '.user'
if (-not (Test-Path $UserFolder)) { New-Item -ItemType Directory -Path $UserFolder | Out-Null }

# Filenames (adapt as needed)
$SparseMsixName = 'PowerOCRSparse.msix'            # Must match constant in App.xaml.cs later
$CertPwdFile    = Join-Path $UserFolder 'PowerOCR.certificate.sample.pwd'
$CertThumbFile  = Join-Path $UserFolder 'PowerOCR.certificate.sample.thumbprint'
$CertCerFile    = Join-Path $UserFolder 'PowerOCR.certificate.sample.cer'
$CertPfxFile    = Join-Path $UserFolder 'PowerOCR.certificate.sample.pfx'

# Clean option: remove bin/obj and uninstall existing sparse package if present
if ($Clean) {
    Write-Host "Cleaning build artifacts..." -ForegroundColor Cyan
    foreach ($d in 'bin','obj') {
        $target = Join-Path $ProjectRoot $d
        if (Test-Path $target) { Remove-Item $target -Recurse -Force }
    }
    Write-Host "Attempting to remove existing sparse package (best effort)" -ForegroundColor Cyan
    try { Get-AppxPackage -Name 'Microsoft.PowerToys.PowerOCR' | Remove-AppxPackage } catch {}
}

# Force certificate regeneration if requested
if ($ForceCert -and (Test-Path $UserFolder)) {
    Write-Host "ForceCert specified: removing existing certificate artifacts..." -ForegroundColor Yellow
    Get-ChildItem -Path $UserFolder | ForEach-Object { if ($_.PSIsContainer) { Remove-Item $_.FullName -Recurse -Force } else { Remove-Item $_.FullName -Force } }
}

# Ensure dev cert (development only; not for production use) - skip if NoSign specified
if (-not $NoSign -and -not (Test-Path $CertPfxFile)) {
    Write-Host "Generating development certificate..." -ForegroundColor Cyan

    # Clear stale files
    Get-ChildItem -Path $UserFolder | ForEach-Object { if ($_.PSIsContainer) { Remove-Item $_.FullName -Recurse -Force } else { Remove-Item $_.FullName -Force } }
    if (-not (Test-Path $UserFolder)) { New-Item -ItemType Directory -Path $UserFolder | Out-Null }

    $charSet = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@'
    $passwordLength = 20
    $securePwd = New-Object -TypeName System.Security.SecureString
    for ($i=0; $i -lt $passwordLength; $i++) { $securePwd.AppendChar($charSet[(Get-Random -Maximum $charSet.Length)]) }
    $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePwd)
    $plainPwd = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    # Save password without trailing newline to avoid sign tool parsing issues
    Set-Content -Path $CertPwdFile -Value $plainPwd -Force -NoNewline

    $certStore = 'cert:\\CurrentUser\\My'
    $now = Get-Date
    $expiration = $now.AddMonths(12)
    $subject = 'CN=PowerOCR Dev, O=PowerToys, L=Redmond, S=Washington, C=US'
    $friendlyName = "PowerOCR Sparse Dev Cert Create=$now"
    $keyFriendly = "PowerOCR Sparse Dev Key Create=$now"
    $eku_oid = '2.5.29.37'
    $eku_value = '1.3.6.1.5.5.7.3.3,1.3.6.1.4.1.311.10.3.13'
    $eku = "$eku_oid={text}$eku_value"
    $cert = New-SelfSignedCertificate -CertStoreLocation $certStore -NotAfter $expiration -Subject $subject -FriendlyName $friendlyName -KeyFriendlyName $keyFriendly -KeyDescription $keyFriendly -TextExtension $eku

    Set-Content -Path $CertThumbFile -Value $cert.Thumbprint -Force
    Export-Certificate -Cert $cert -FilePath $CertCerFile -Force | Out-Null
    Export-PfxCertificate -Cert (Join-Path $certStore $cert.Thumbprint) -FilePath $CertPfxFile -Password (ConvertTo-SecureString -String $plainPwd -AsPlainText -Force) | Out-Null
}

# Build (restore + compile). This assumes msbuild is on PATH (VS Developer Prompt) or dev shell.
Write-Host "Building PowerOCR ($Platform $Configuration)..." -ForegroundColor Cyan
#msbuild /restore /p:Platform=$Platform /p:Configuration=$Configuration "$ProjectRoot\PowerOCR.csproj"
#msbuild /p:Platform=$Platform /p:Configuration=$Configuration "$ProjectRoot\PowerOCR.csproj"

# Determine output directory (adjust if TFM changes)
#$tfmFolder = 'net8.0-windows10.0.22621.0'
#$outDir = Join-Path $ProjectRoot "bin/$Platform/$Configuration/$tfmFolder"
$outDir = Join-Path $ProjectRoot "bin/"
if (-not (Test-Path $outDir)) { throw "Expected output directory not found: $outDir" }

# PowerOCR.PackagingSparse folder (where this script resides) containing the sparse manifest and assets
$sparseDir = $PSScriptRoot
$manifestPath = Join-Path $sparseDir 'AppxManifest.xml'
if (-not (Test-Path $manifestPath)) { throw "Missing AppxManifest.xml in PowerOCR.PackagingSparse folder: $manifestPath" }

# Ensure Images folder and placeholder logos exist in PowerOCR.PackagingSparse
$imagesDir = Join-Path $sparseDir 'Images'
if (-not (Test-Path $imagesDir)) { New-Item -ItemType Directory -Path $imagesDir | Out-Null }
$placeholders = @('StoreLogo.png','Square150x150Logo.png','Square44x44Logo.png')
foreach ($img in $placeholders) {
    $dest = Join-Path $imagesDir $img
    if (-not (Test-Path $dest)) {
        # 1x1 transparent PNG placeholder
        [IO.File]::WriteAllBytes($dest,[Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8Xw8AAn8B9n7DZ1gAAAAASUVORK5CYII='))
    }
}

# Copy executable to PowerOCR.PackagingSparse folder (manifest expects PowerToys.PowerOCR.exe)


# Pack sparse MSIX from PowerOCR.PackagingSparse folder
$msixPath = Join-Path $outDir $SparseMsixName
Write-Host "Packing sparse MSIX from $sparseDir -> $msixPath" -ForegroundColor Cyan
MakeAppx.exe pack /d $sparseDir /p $msixPath /nv /o

# Sign package (skip if NoSign specified for CI scenarios)
if ($NoSign) {
    Write-Host "Skipping signing (NoSign specified for CI build)" -ForegroundColor Yellow
} else {
    $plainPwd = (Get-Content -Path $CertPwdFile -Raw).Trim()
    Write-Host "Signing sparse MSIX..." -ForegroundColor Cyan
    & SignTool.exe sign /fd SHA256 /a /f $CertPfxFile /p $plainPwd $msixPath
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "SignTool failed (exit $LASTEXITCODE). If this is a password mismatch, run with -ForceCert to regenerate the dev certificate and retry."
        exit $LASTEXITCODE
    }
}

Write-Host "`nPackage created: $msixPath" -ForegroundColor Green

if ($NoSign) {
    Write-Host "UNSIGNED package created for CI build. Sign before deployment." -ForegroundColor Yellow
} else {
    Write-Host "Install the dev certificate (once): $CertCerFile" -ForegroundColor Yellow
}

Write-Host "Register sparse package:" -ForegroundColor Yellow
Write-Host "  Add-AppxPackage -Register `"$msixPath`" -ExternalLocation `"$outDir`"" -ForegroundColor Yellow
