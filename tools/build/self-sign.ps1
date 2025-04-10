param (
    [string]$architecture = "arm64",  # Default to x64 if not provided
    [string]$buildConfiguration = "Debug"  # Default to Debug if not provided
)

$signToolPath = $null
$architecture = "arm64"
$kitsRootPaths = @(
    "C:\Program Files (x86)\Windows Kits\10\bin",
    "C:\Program Files\Windows Kits\10\bin"
)

$signToolAvailable = Get-Command "signtool" -ErrorAction SilentlyContinue
if ($signToolAvailable) {
    Write-Host "SignTool is available in the system PATH."
    $signToolPath = "signtool"
}
else {
    Write-Host "Searching for latest SignTool matching architecture: $architecture"

    foreach ($root in $kitsRootPaths) {
        if (Test-Path $root) {
            $versions = Get-ChildItem -Path $root -Directory | Where-Object {
                $_.Name -match '^\d+\.\d+\.\d+\.\d+$'
            } | Sort-Object Name -Descending

            foreach ($version in $versions) {
                $candidatePath = Join-Path -Path $version.FullName -ChildPath $architecture
                $exePath = Join-Path -Path $candidatePath -ChildPath "signtool.exe"
                if (Test-Path $exePath) {
                    Write-Host "Found SignTool at: $exePath"
                    $signToolPath = $exePath
                    break
                }
            }

            if ($signToolPath) { break }
        }
    }

    if (!$signToolPath) {
        Write-Host "SignTool not found. Please ensure Windows SDK is installed."
        exit 1
    }
}

Write-Host "`nUsing SignTool: $signToolPath"

# Set the certificate subject and the ECDSA curve
$certSubject = "CN=PowerToysSelfSignedCert"

# Check if the certificate already exists in the current user's certificate store
$existingCert = Get-ChildItem -Path Cert:\CurrentUser\My | Where-Object { $_.Subject -like "*PowerToysSelfSignedCert*" }

if ($existingCert) {
    # If the certificate exists, use the existing certificate
    Write-Host "Certificate already exists, using the existing certificate"
    $cert = $existingCert
}
else {
    # If the certificate doesn't exist, create a new self-signed certificate
    Write-Host "Certificate does not exist, creating a new certificate..."
    try {
        $cert = New-SelfSignedCertificate -Subject $certSubject `
                                          -CertStoreLocation "Cert:\CurrentUser\My" `
                                          -KeyAlgorithm RSA `
                                          -Type CodeSigningCert `
                                          -HashAlgorithm SHA256
        Write-Host "New certificate created"

        # ✅ Trust the certificate by importing it into 'Trusted Root Certification Authorities'
        $trustedRootStore = "Cert:\CurrentUser\Root"
        $imported = Import-Certificate -FilePath (Export-Certificate -Cert $cert -FilePath "$env:TEMP\temp_cert.cer" -Force).FullName -CertStoreLocation $trustedRootStore

        if ($imported) {
            Write-Host "✅ Certificate successfully trusted (imported to Root store)"
        } else {
            Write-Host "⚠️ Failed to trust certificate"
        }
    }
    catch {
        Write-Host "❌ Error creating certificate: $_"
        exit 1
    }
}

# Output the thumbprint of the certificate (to confirm which certificate is being used)
Write-Host "Using certificate with thumbprint: $($cert.Thumbprint)"


$rootDirectory = (Split-Path -Parent(Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)))

# Dynamically build the directory path based on architecture and build configuration
$directoryPath = Join-Path $rootDirectory "$architecture\$buildConfiguration\WinUI3Apps\CmdPal\"

Write-Host "Directory path to search for .msix and .appx files: $directoryPath"

# Get all .msix and .appx files from the specified directory
$filePaths = Get-ChildItem -Path $directoryPath -Recurse | Where-Object {
    ($_.Extension -eq ".msix" -or $_.Extension -eq ".appx") -and
    ($_.Name -like "*$architecture*")
}

if ($filePaths.Count -eq 0) {
    Write-Host "No .msix or .appx files found in the directory."
}
else {
    # Iterate through each file and sign it
    foreach ($file in $filePaths) {
        Write-Host "Signing file: $($file.FullName)"
        
        # Use SignTool to sign the file
        $signToolCommand = "& `"$signToolPath`" sign /sha1 $($cert.Thumbprint) /fd SHA256 /t http://timestamp.digicert.com `"$($file.FullName)`""

        # Execute the sign command
        try {
            Invoke-Expression $signToolCommand
            Write-Host "File $($file.Name) has been successfully signed!"
        }
        catch {
            Write-Host "Error signing file $($file.Name): $_"
        }
    }
}

Write-Host "Signing process completed."
