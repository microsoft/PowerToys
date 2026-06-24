<#
.SYNOPSIS
Ensures a code signing certificate exists and is trusted in all necessary certificate stores.

.DESCRIPTION
This script provides two functions:

1. EnsureCertificate:
   - Searches for an existing code signing certificate by subject name.
   - If not found, creates a new self-signed certificate.
   - Exports the certificate and attempts to import it into:
     - CurrentUser\TrustedPeople
     - CurrentUser\Root
     - LocalMachine\Root (admin privileges may be required)

2. ImportAndVerifyCertificate:
   - Imports a `.cer` file into the specified certificate store if not already present.
   - Verifies the certificate is successfully imported by checking thumbprint.

This is useful in build or signing pipelines to ensure a valid and trusted certificate is available before signing MSIX or executable files.

.PARAMETER certSubject
The subject name of the certificate to search for or create. Default is:
"CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US"

.PARAMETER cerPath
(ImportAndVerifyCertificate only) The file path to a `.cer` certificate file to import.

.PARAMETER storePath
(ImportAndVerifyCertificate only) The destination certificate store path (e.g. Cert:\CurrentUser\Root).

.EXAMPLE
$cert = EnsureCertificate

Ensures the default certificate exists and is trusted, and returns the certificate object.

.EXAMPLE
ImportAndVerifyCertificate -cerPath "$env:TEMP\temp_cert.cer" -storePath "Cert:\CurrentUser\Root"

Imports a certificate into the CurrentUser Root store and verifies its presence.

.NOTES
- For full trust, administrative privileges may be needed to import into LocalMachine\Root.
- Certificates are created using RSA and SHA256 and marked as CodeSigningCert.
#>

function ImportAndVerifyCertificate {
    param (
        [string]$cerPath,
        [string]$storePath
    )

    $thumbprint = (Get-PfxCertificate -FilePath $cerPath).Thumbprint

    $existingCert = Get-ChildItem -Path $storePath | Where-Object { $_.Thumbprint -eq $thumbprint }
    if ($existingCert) {
        Write-Host "Certificate already exists in $storePath"
        return $true
    }

    try {
        $null = Import-Certificate -FilePath $cerPath -CertStoreLocation $storePath -ErrorAction Stop
    } catch {
        if ($_.Exception.Message -match "Access is denied" -or $_.Exception.InnerException.Message -match "Access is denied") {
            Write-Warning "Access denied to $storePath. Attempting to import with admin privileges..."
            try {
                Start-Process powershell -ArgumentList "-NoProfile", "-Command", "& { Import-Certificate -FilePath '$cerPath' -CertStoreLocation '$storePath' }" -Verb RunAs -Wait
            } catch {
                Write-Warning "Failed to request admin privileges: $_"
                return $false
            }
        } else {
            Write-Warning "Failed to import certificate to $storePath : $_"
            return $false
        }
    }

    $imported = Get-ChildItem -Path $storePath | Where-Object { $_.Thumbprint -eq $thumbprint }
    if ($imported) {
        Write-Host "Certificate successfully imported to $storePath"
        return $true
    } else {
        Write-Warning "Certificate not found in $storePath after import"
        return $false
    }
}

function EnsureCertificate {
    param (
        [string]$certSubject = "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US"
    )

    $cert = Get-ChildItem -Path Cert:\CurrentUser\My |
        Where-Object { $_.Subject -eq $certSubject } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1

    if (-not $cert) {
        Write-Host "Certificate not found. Creating a new one..."

        $cert = New-SelfSignedCertificate -Subject $certSubject `
            -CertStoreLocation "Cert:\CurrentUser\My" `
            -KeyAlgorithm RSA `
            -Type CodeSigningCert `
            -HashAlgorithm SHA256

        if (-not $cert) {
            Write-Error "Failed to create a new certificate."
            return $null
        }

        Write-Host "New certificate created with thumbprint: $($cert.Thumbprint)"
    }
    else {
        Write-Host "Using existing certificate with thumbprint: $($cert.Thumbprint)"
    }

    $cerPath = "$env:TEMP\temp_cert.cer"
    [void](Export-Certificate -Cert $cert -FilePath $cerPath -Force)

    if (-not (ImportAndVerifyCertificate -cerPath $cerPath -storePath "Cert:\CurrentUser\TrustedPeople")) { return $null }
    if (-not (ImportAndVerifyCertificate -cerPath $cerPath -storePath "Cert:\CurrentUser\Root")) { return $null }
    if (-not (ImportAndVerifyCertificate -cerPath $cerPath -storePath "Cert:\LocalMachine\Root")) {
        Write-Warning "Failed to import to LocalMachine\Root (admin may be required)"
        return $null
    }

    return $cert
}

function Export-CertificateFiles {
    param (
        [System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate,
        [string]$CerPath,
        [string]$PfxPath,
        [securestring]$PfxPassword
    )

    if (-not $Certificate) {
        Write-Error "No certificate provided to export."
        return
    }

    if ($CerPath) {
        try {
            Export-Certificate -Cert $Certificate -FilePath $CerPath -Force | Out-Null
            Write-Host "Exported CER to: $CerPath"
        } catch {
            Write-Warning "Failed to export CER file: $_"
        }
    }

    if ($PfxPath -and $PfxPassword) {
        try {
            Export-PfxCertificate -Cert $Certificate -FilePath $PfxPath -Password $PfxPassword -Force | Out-Null
            Write-Host "Exported PFX to: $PfxPath"
        } catch {
            Write-Warning "Failed to export PFX file: $_"
        }
    }

    if (-not $CerPath -and -not $PfxPath) {
        Write-Warning "No output path specified. Nothing was exported."
    }
}

# Main execution when script is run directly
if ($MyInvocation.InvocationName -ne '.') {
    Write-Host "=== PowerToys Certificate Management ===" -ForegroundColor Green
    Write-Host ""
    
    # Ensure certificate exists and is trusted
    Write-Host "Checking for existing certificate or creating new one..." -ForegroundColor Yellow
    $cert = EnsureCertificate
    
    if ($cert) {
        # Export the certificate to a .cer file
        $exportPath = Join-Path (Get-Location) "PowerToys-CodeSigning.cer"
        Write-Host ""
        Write-Host "Exporting certificate..." -ForegroundColor Yellow
        Export-CertificateFiles -Certificate $cert -CerPath $exportPath
        
        Write-Host ""
        Write-Host "=== IMPORTANT NOTES ===" -ForegroundColor Red
        Write-Host "The certificate has been exported to: $exportPath" -ForegroundColor White
        Write-Host ""
        Write-Host "To use this certificate for code signing, you need to:" -ForegroundColor Yellow
        Write-Host "1. Import this certificate into 'Trusted People' store" -ForegroundColor White
        Write-Host "2. Import this certificate into 'Trusted Root Certification Authorities' store" -ForegroundColor White
        Write-Host "Certificate Details:" -ForegroundColor Green
        Write-Host "Subject: $($cert.Subject)" -ForegroundColor White
        Write-Host "Thumbprint: $($cert.Thumbprint)" -ForegroundColor White
        Write-Host "Valid Until: $($cert.NotAfter)" -ForegroundColor White
    } else {
        Write-Error "Failed to create or find certificate. Please check the error messages above."
        exit 1
    }
}