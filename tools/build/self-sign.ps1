#https://learn.microsoft.com/en-us/windows/msix/package/signing-known-issues
# 1. Build the powertoys as usual.
# 2. Call this script to sign the msix package.
# First time run needs admin permission to trust the certificate.

param (
    [string]$architecture = "x64", # Default to x64 if not provided
    [string]$buildConfiguration = "Debug"  # Default to Debug if not provided
)

$signToolPath = $null
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
                $candidatePath = Join-Path -Path $version.FullName -ChildPath "x86"
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
$certSubject = "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US"

# Check if the certificate already exists in the current user's certificate store
$existingCert = Get-ChildItem -Path Cert:\CurrentUser\My |
Where-Object { $_.Subject -eq $certSubject } |
Sort-Object NotAfter -Descending |
Select-Object -First 1

if ($existingCert) {
    # If the certificate exists, use the existing certificate
    Write-Host "Certificate already exists, using the existing certificate"
    $cert = $existingCert
}
else {    
    # If the certificate doesn't exist, create a new self-signed certificate
    Write-Host "Certificate does not exist, creating a new certificate..."
    $cert = New-SelfSignedCertificate -Subject $certSubject `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -KeyAlgorithm RSA `
        -Type CodeSigningCert `
        -HashAlgorithm SHA256
}

function Import-And-VerifyCertificate {
    param (
        [string]$cerPath,
        [string]$storePath
    )

    $thumbprint = (Get-PfxCertificate -FilePath $cerPath).Thumbprint

    # ✅ Step 1: Check if already exists in store
    $existingCert = Get-ChildItem -Path $storePath | Where-Object { $_.Thumbprint -eq $thumbprint }

    if ($existingCert) {
        Write-Host "✅ Certificate already exists in $storePath"
        return $true
    }

    # 🚀 Step 2: Try to import if not already there
    try {
        $null = Import-Certificate -FilePath $cerPath -CertStoreLocation $storePath -ErrorAction Stop
    }
    catch {
        Write-Warning "❌ Failed to import certificate to $storePath : $_"
        return $false
    }

    # 🔁 Step 3: Verify again
    $imported = Get-ChildItem -Path $storePath | Where-Object { $_.Thumbprint -eq $thumbprint }

    if ($imported) {
        Write-Host "✅ Certificate successfully imported to $storePath"
        return $true
    }
    else {
        Write-Warning "❌ Certificate not found in $storePath after import"
        return $false
    }
}

$cerPath = "$env:TEMP\temp_cert.cer"
Export-Certificate -Cert $cert -FilePath $cerPath -Force
# used for sign code/msix
# CurrentUser\TrustedPeople
if (-not (Import-And-VerifyCertificate -cerPath $cerPath -storePath "Cert:\CurrentUser\TrustedPeople")) {
    exit 1
}

# CurrentUser\Root
if (-not (Import-And-VerifyCertificate -cerPath $cerPath -storePath "Cert:\CurrentUser\Root")) {
    exit 1
}

# LocalMachine\Root
if (-not (Import-And-VerifyCertificate -cerPath $cerPath -storePath "Cert:\LocalMachine\Root")) {
    Write-Warning "⚠️ Failed to import to LocalMachine\Root (admin may be required)"
    exit 1
}


# Output the thumbprint of the certificate (to confirm which certificate is being used)
Write-Host "Using certificate with thumbprint: $($cert.Thumbprint)"


$rootDirectory = (Split-Path -Parent(Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)))

# Dynamically build the directory path based on architecture and build configuration
# $directoryPath = Join-Path $rootDirectory "$architecture\$buildConfiguration\WinUI3Apps\CmdPal\"
$directoryPath = Join-Path $rootDirectory "$architecture\$buildConfiguration\WinUI3Apps\CmdPal\"

if (-not (Test-Path $directoryPath)) {
    Write-Error "Path to search for msix files does not exist: $directoryPath"
    exit 1
}

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
        }
        catch {
            Write-Host "Error signing file $($file.Name): $_"
        }
    }
}

Write-Host "Signing process completed."
