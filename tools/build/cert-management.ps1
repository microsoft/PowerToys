param (
    [string]$certSubject = "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US"
)

function Import-And-VerifyCertificate {
    param (
        [string]$cerPath,
        [string]$storePath
    )

    $thumbprint = (Get-PfxCertificate -FilePath $cerPath).Thumbprint

    $existingCert = Get-ChildItem -Path $storePath | Where-Object { $_.Thumbprint -eq $thumbprint }
    if ($existingCert) {
        Write-Host "✅ Certificate already exists in $storePath"
        return $true
    }

    try {
        $null = Import-Certificate -FilePath $cerPath -CertStoreLocation $storePath -ErrorAction Stop
    } catch {
        Write-Warning "❌ Failed to import certificate to $storePath : $_"
        return $false
    }

    $imported = Get-ChildItem -Path $storePath | Where-Object { $_.Thumbprint -eq $thumbprint }
    if ($imported) {
        Write-Host "✅ Certificate successfully imported to $storePath"
        return $true
    } else {
        Write-Warning "❌ Certificate not found in $storePath after import"
        return $false
    }
}

$cert = Get-ChildItem -Path Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq $certSubject } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if (-not $cert) {
    Write-Host "📜 Certificate not found. Creating a new one..."

    $cert = New-SelfSignedCertificate -Subject $certSubject `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -KeyAlgorithm RSA `
        -Type CodeSigningCert `
        -HashAlgorithm SHA256

    if (-not $cert) {
        Write-Error "❌ Failed to create a new certificate."
        exit 1
    }

    Write-Host "✔️ New certificate created with thumbprint: $($cert.Thumbprint)"
}
else {
    Write-Host "📌 Using existing certificate with thumbprint: $($cert.Thumbprint)"
}

# Step 2: Export and trust it in necessary stores
$cerPath = "$env:TEMP\temp_cert.cer"
Export-Certificate -Cert $cert -FilePath $cerPath -Force

if (-not (Import-And-VerifyCertificate -cerPath $cerPath -storePath "Cert:\CurrentUser\TrustedPeople")) { exit 1 }
if (-not (Import-And-VerifyCertificate -cerPath $cerPath -storePath "Cert:\CurrentUser\Root")) { exit 1 }
if (-not (Import-And-VerifyCertificate -cerPath $cerPath -storePath "Cert:\LocalMachine\Root")) {
    Write-Warning "⚠️ Failed to import to LocalMachine\Root (admin may be required)"
}

# Return the certificate object
return $cert