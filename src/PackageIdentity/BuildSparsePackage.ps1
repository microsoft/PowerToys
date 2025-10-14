#Requires -Version 5.1

[CmdletBinding()]
Param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("arm64", "x64")]
    [string]$Platform = "x64",
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    
    [switch]$Clean,
    [switch]$ForceCert,
    [switch]$NoSign
)

# PowerToys sparse packaging helper.
# Generates a sparse MSIX (no payload) that grants package identity to selected Win32 components.
# Multiple applications (PowerOCR, Settings UI, etc.) can share this single sparse identity.

$ErrorActionPreference = 'Stop'

# Configuration constants - centralized management
$script:Config = @{
    IdentityName   = "Microsoft.PowerToys.SparseApp"
    SparseMsixName = "PowerToysSparse.msix"
    CertPrefix     = "PowerToysSparse"
    CertSubject    = 'CN=PowerToys Dev, O=PowerToys, L=Redmond, S=Washington, C=US'
    CertValidMonths = 12
}

#region Helper Functions

function Find-WindowsSDKTool {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ToolName,
        
        [Parameter(Mandatory=$false)]
        [string]$Architecture = "x64"
    )
    
    # Simple fallback: check common Windows SDK locations
    $commonPaths = @(
        "${env:ProgramFiles}\Windows Kits\10\bin\*\$Architecture\$ToolName",
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x86\$ToolName"  # SignTool fallback
    )
    
    foreach ($pattern in $commonPaths) {
        $found = Get-ChildItem $pattern -ErrorAction SilentlyContinue | 
                 Sort-Object Name -Descending | 
                 Select-Object -First 1
        if ($found) {
            Write-BuildLog "Found $ToolName at: $($found.FullName)" -Level Info
            return $found.FullName
        }
    }
    
    throw "$ToolName not found. Please ensure Windows SDK is installed."
}

function Test-CertificateValidity {
    param([string]$PfxPath, [string]$PasswordFile)
    
    if (-not (Test-Path $PfxPath) -or -not (Test-Path $PasswordFile)) { return $false }
    
    try {
        $password = (Get-Content $PasswordFile -Raw).Trim()
        $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($PfxPath, $password)
        $isValid = $cert.HasPrivateKey -and $cert.NotAfter -gt (Get-Date)
        $cert.Dispose()
        return $isValid
    } catch {
        return $false
    }
}

function Write-BuildLog {
    param([string]$Message, [string]$Level = "Info")
    
    $colors = @{ Error = "Red"; Warning = "Yellow"; Success = "Green"; Info = "Cyan" }
    $color = if ($colors.ContainsKey($Level)) { $colors[$Level] } else { "White" }
    
    Write-Host "[$(Get-Date -f 'HH:mm:ss')] $Message" -ForegroundColor $color
}

function Stop-FileProcesses {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$FilePath
    )
    
    # This function is kept for compatibility but simplified since 
    # the staging directory approach resolves the file lock issues
    Write-Verbose "File process check for: $FilePath"
}

#endregion

# Environment diagnostics for troubleshooting
Write-BuildLog "Starting PackageIdentity build process..." -Level Info
Write-BuildLog "PowerShell Version: $($PSVersionTable.PSVersion)" -Level Info
try {
    $execPolicy = Get-ExecutionPolicy
    Write-BuildLog "Execution Policy: $execPolicy" -Level Info
} catch {
    Write-BuildLog "Execution Policy: Unable to determine (MSBuild environment)" -Level Info
}
Write-BuildLog "Current User: $env:USERNAME" -Level Info
Write-BuildLog "Build Platform: $Platform, Configuration: $Configuration" -Level Info

# Check for Visual Studio environment
if ($env:VSINSTALLDIR) {
    Write-BuildLog "Running in Visual Studio environment: $env:VSINSTALLDIR" -Level Info
}

# Ensure certificate provider is available
try {
    # Force load certificate provider for MSBuild environment
    if (-not (Get-PSProvider -PSProvider Certificate -ErrorAction SilentlyContinue)) {
        Write-BuildLog "Loading certificate provider..." -Level Warning
        Import-Module Microsoft.PowerShell.Security -Force
    }
    if (-not (Test-Path 'Cert:\CurrentUser')) {
        Write-BuildLog "Certificate drive not available, attempting to initialize..." -Level Warning
        Import-Module PKI -ErrorAction SilentlyContinue
        # Try to access the certificate store to force initialization
        Get-ChildItem "Cert:\CurrentUser\My" -ErrorAction SilentlyContinue | Out-Null
    }
} catch {
    Write-BuildLog "Note: Certificate provider setup may need manual configuration: $_" -Level Warning
}

# Project root folder (now set to current script folder for local builds)
$ProjectRoot = $PSScriptRoot
$UserFolder = Join-Path $ProjectRoot '.user'
if (-not (Test-Path $UserFolder)) { New-Item -ItemType Directory -Path $UserFolder | Out-Null }

# Certificate file paths using configuration
$prefix = $script:Config.CertPrefix
$CertPwdFile, $CertThumbFile, $CertCerFile, $CertPfxFile = @('.pwd', '.thumbprint', '.cer', '.pfx') | 
    ForEach-Object { Join-Path $UserFolder "$prefix.certificate.sample$_" }

# Clean option: remove bin/obj and uninstall existing sparse package if present
if ($Clean) {
    Write-BuildLog "Cleaning build artifacts..." -Level Info
    'bin','obj' | ForEach-Object { 
        $target = Join-Path $ProjectRoot $_
        if (Test-Path $target) { Remove-Item $target -Recurse -Force }
    }
    Write-BuildLog "Attempting to remove existing sparse package (best effort)" -Level Info
    try { Get-AppxPackage -Name $script:Config.IdentityName | Remove-AppxPackage } catch {}
}

# Force certificate regeneration if requested
if ($ForceCert -and (Test-Path $UserFolder)) {
    Write-BuildLog "ForceCert specified: removing existing certificate artifacts..." -Level Warning
    Remove-Item $UserFolder -Recurse -Force
    New-Item -ItemType Directory -Path $UserFolder | Out-Null
}

# Ensure dev cert (development only; not for production use) - skip if NoSign specified
$needNewCert = -not $NoSign -and (-not (Test-Path $CertPfxFile) -or $ForceCert -or -not (Test-CertificateValidity -PfxPath $CertPfxFile -PasswordFile $CertPwdFile))

if ($needNewCert) {
    Write-BuildLog "Generating development certificate (prefix=$($script:Config.CertPrefix))..." -Level Info

    # Clear stale files and generate password
    if (Test-Path $UserFolder) { Remove-Item $UserFolder -Recurse -Force }
    New-Item -ItemType Directory -Path $UserFolder | Out-Null

    # Generate random password
    $plainPwd = -join ((1..20) | ForEach-Object { 
        'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@'[(Get-Random -Max 64)]
    })
    # Save password and create certificate
    Set-Content -Path $CertPwdFile -Value $plainPwd -Force -NoNewline

    $now = Get-Date
    $friendlyName = "PowerToys Dev Sparse Cert Create=$($now.ToString('yyyy-MM-dd HH:mm'))"
    
    $cert = New-SelfSignedCertificate -CertStoreLocation 'cert:\CurrentUser\My' `
        -NotAfter $now.AddMonths($script:Config.CertValidMonths) `
        -Subject $script:Config.CertSubject `
        -FriendlyName $friendlyName `
        -KeyFriendlyName $friendlyName `
        -KeyDescription $friendlyName `
        -TextExtension '2.5.29.37={text}1.3.6.1.5.5.7.3.3,1.3.6.1.4.1.311.10.3.13'

    # Export certificate files
    Set-Content -Path $CertThumbFile -Value $cert.Thumbprint -Force
    Export-Certificate -Cert $cert -FilePath $CertCerFile -Force | Out-Null
    Export-PfxCertificate -Cert "cert:\CurrentUser\My\$($cert.Thumbprint)" -FilePath $CertPfxFile `
        -Password (ConvertTo-SecureString -String $plainPwd -AsPlainText -Force) | Out-Null
}

# Determine output directory - using PowerToys standard structure
# Navigate to PowerToys root (two levels up from src/PackageIdentity)
$PowerToysRoot = Split-Path (Split-Path $ProjectRoot -Parent) -Parent
$outDir = Join-Path $PowerToysRoot "$Platform\$Configuration"

if (-not (Test-Path $outDir)) {
    Write-BuildLog "Creating output directory: $outDir" -Level Info
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

# PackageIdentity folder (this script location) containing the sparse manifest and assets
$sparseDir = $PSScriptRoot
$manifestPath = Join-Path $sparseDir 'AppxManifest.xml'
if (-not (Test-Path $manifestPath)) { throw "Missing AppxManifest.xml in PackageIdentity folder: $manifestPath" }

# Find MakeAppx.exe from Windows SDK
try {
    $makeAppxPath = Find-WindowsSDKTool -ToolName "makeappx.exe" -Architecture $Platform
} catch {
    Write-Error "MakeAppx.exe not found. Please ensure Windows SDK is installed."
    exit 1
}

# Pack sparse MSIX from PackageIdentity folder
$msixPath = Join-Path $outDir $script:Config.SparseMsixName

# Clean up existing MSIX file
if (Test-Path $msixPath) {
    Write-BuildLog "Removing existing MSIX file..." -Level Info
    try {
        Remove-Item $msixPath -Force -ErrorAction Stop
        Write-BuildLog "Successfully removed existing MSIX file" -Level Success
    } catch {
        Write-BuildLog "Warning: Could not remove existing MSIX file: $_" -Level Warning
    }
}

# Create a clean staging directory to avoid file lock issues
$stagingDir = Join-Path $outDir "staging"
if (Test-Path $stagingDir) {
    Remove-Item $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null

try {
    Write-BuildLog "Creating clean staging directory for packaging..." -Level Info
    
    # Copy only essential files to staging directory to avoid file locks
    $essentialFiles = @(
        "AppxManifest.xml"
        "Images\*"
    )
    
    foreach ($filePattern in $essentialFiles) {
        $sourcePath = Join-Path $sparseDir $filePattern
        $relativePath = $filePattern
        
        if ($filePattern.Contains('\')) {
            $targetDir = Join-Path $stagingDir (Split-Path $relativePath -Parent)
            if (-not (Test-Path $targetDir)) {
                New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
            }
        }
        
        if ($filePattern.EndsWith('\*')) {
            # Copy directory contents
            $sourceDir = $sourcePath.TrimEnd('\*')
            $targetDir = Join-Path $stagingDir (Split-Path $relativePath.TrimEnd('\*') -Parent)
            if (Test-Path $sourceDir) {
                Copy-Item -Path "$sourceDir\*" -Destination $targetDir -Force -ErrorAction SilentlyContinue
            }
        } else {
            # Copy single file
            $targetPath = Join-Path $stagingDir $relativePath
            if (Test-Path $sourcePath) {
                Copy-Item -Path $sourcePath -Destination $targetPath -Force -ErrorAction SilentlyContinue
            }
        }
    }
    
    Write-BuildLog "Staging directory prepared with essential files only" -Level Success
    
    # Pack MSIX using staging directory
    Write-BuildLog "Packing sparse MSIX ($($script:Config.SparseMsixName)) from staging -> $msixPath" -Level Info
    
    & $makeAppxPath pack /d $stagingDir /p $msixPath /nv /o
    
    if ($LASTEXITCODE -eq 0 -and (Test-Path $msixPath)) {
        Write-BuildLog "MSIX packaging completed successfully" -Level Success
    } else {
        Write-BuildLog "MakeAppx failed with exit code $LASTEXITCODE" -Level Error
        exit 1
    }
} finally {
    # Clean up staging directory
    if (Test-Path $stagingDir) {
        try {
            Remove-Item $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
            Write-BuildLog "Cleaned up staging directory" -Level Info
        } catch {
            Write-BuildLog "Warning: Could not clean up staging directory: $_" -Level Warning
        }
    }
}

# Sign package (skip if NoSign specified for CI scenarios)
if ($NoSign) {
    Write-BuildLog "Skipping signing (NoSign specified for CI build)" -Level Warning
} else {
    # Use certificate thumbprint for signing (safer, no password)
    $certThumbprint = (Get-Content -Path $CertThumbFile -Raw).Trim()
    try {
        $signToolPath = Find-WindowsSDKTool -ToolName "signtool.exe"
    } catch {
        Write-Error "SignTool.exe not found. Please ensure Windows SDK is installed."
        exit 1
    }
    Write-BuildLog "Signing sparse MSIX using cert thumbprint $certThumbprint..." -Level Info
    & $signToolPath sign /fd SHA256 /sha1 $certThumbprint $msixPath
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "SignTool failed (exit $LASTEXITCODE). Ensure the certificate is in CurrentUser\\My and try -ForceCert if needed."
        exit $LASTEXITCODE
    }
}

Write-BuildLog "`nPackage created: $msixPath" -Level Success

if ($NoSign) {
    Write-BuildLog "UNSIGNED package created for CI build. Sign before deployment." -Level Warning
} else {
    Write-BuildLog "Install the dev certificate (once): $CertCerFile" -Level Info
    Write-BuildLog "Identity Name: $($script:Config.IdentityName)" -Level Info
}

Write-BuildLog "Register sparse package:" -Level Info
Write-BuildLog "  Add-AppxPackage -Register `"$msixPath`" -ExternalLocation `"$outDir`"" -Level Warning
Write-BuildLog "(If already installed and you changed manifest only): Add-AppxPackage -Register `"$manifestPath`" -ExternalLocation `"$outDir`" -ForceApplicationShutdown" -Level Warning
