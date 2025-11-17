# One-Time Publication Setup Script for CmdPal Extension
# This script collects Microsoft Store publication information and updates project files
# Version: 1.1

#Requires -Version 5.1

# Enable strict mode for better error detection
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  Microsoft Store Publication Setup" -ForegroundColor Cyan
Write-Host "  CmdPal Extension Publisher" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# Path to the project files
$projectRoot = Split-Path -Parent $PSScriptRoot
$csprojPath = Join-Path $projectRoot "TemplateCmdPalExtension.csproj"
$manifestPath = Join-Path $projectRoot "Package.appxmanifest"

Write-Host "Validating project structure..." -ForegroundColor Cyan
Write-Host "  Project Root: $projectRoot" -ForegroundColor Gray

# Verify files exist with detailed error messages
if (-not (Test-Path $csprojPath)) {
    Write-Host ""
    Write-Host "ERROR: Could not find .csproj file" -ForegroundColor Red
    Write-Host "  Expected location: $csprojPath" -ForegroundColor Gray
    Write-Host ""
    Write-Host "This script must be run from the Publication folder within your project." -ForegroundColor Yellow
    Write-Host "Please navigate to: <YourProject>\Publication\" -ForegroundColor Yellow
    Write-Host "Press any key to exit..." -ForegroundColor Gray
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

if (-not (Test-Path $manifestPath)) {
    Write-Host ""
    Write-Host "ERROR: Could not find Package.appxmanifest file" -ForegroundColor Red
    Write-Host "  Expected location: $manifestPath" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Your project structure may be incomplete or corrupted." -ForegroundColor Yellow
    Write-Host "Please ensure Package.appxmanifest exists in your project root." -ForegroundColor Yellow
    Write-Host "Press any key to exit..." -ForegroundColor Gray
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

Write-Host "  [OK] .csproj file found" -ForegroundColor Green
Write-Host "  [OK] Package.appxmanifest file found" -ForegroundColor Green
Write-Host ""

# Create backup directory if it doesn't exist
$backupDir = Join-Path $projectRoot "Publication\Backups"
if (-not (Test-Path $backupDir)) {
    try {
        New-Item -Path $backupDir -ItemType Directory -Force | Out-Null
        Write-Host "Created backup directory: $backupDir" -ForegroundColor Gray
    }
    catch {
        Write-Host "WARNING: Could not create backup directory. Proceeding without backups." -ForegroundColor Yellow
        $backupDir = $null
    }
}

# Create timestamped backups
if ($backupDir) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    try {
        Copy-Item $csprojPath -Destination (Join-Path $backupDir "TemplateCmdPalExtension.csproj.$timestamp.bak") -Force
        Copy-Item $manifestPath -Destination (Join-Path $backupDir "Package.appxmanifest.$timestamp.bak") -Force
        Write-Host "Backup created: $timestamp" -ForegroundColor Gray
        Write-Host ""
    }
    catch {
        Write-Host "WARNING: Could not create backup files. Proceeding anyway." -ForegroundColor Yellow
        Write-Host ""
    }
}

Write-Host "This script will collect information needed to publish your extension" -ForegroundColor White
Write-Host "to the Microsoft Store. You can find this information in your" -ForegroundColor White
Write-Host "Microsoft Partner Center account." -ForegroundColor White
Write-Host ""
Write-Host "IMPORTANT: Have your Partner Center information ready before proceeding." -ForegroundColor Yellow
Write-Host "  - Package Identity Name" -ForegroundColor Gray
Write-Host "  - Publisher Certificate Name" -ForegroundColor Gray
Write-Host "  - Reserved App Name" -ForegroundColor Gray
Write-Host "  - Publisher Display Name" -ForegroundColor Gray
Write-Host ""
Write-Host "TIP: You can find this in Partner Center > Product Management > Product Identity" -ForegroundColor Cyan
Write-Host ""

# Prompt to continue
Write-Host "Do you want to continue? (Y/N): " -ForegroundColor Yellow -NoNewline
$continue = Read-Host
if ($continue -notmatch '^[Yy]') {
    Write-Host ""
    Write-Host "Setup cancelled by user." -ForegroundColor Yellow
    Write-Host "Press any key to exit..." -ForegroundColor Gray
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 0
}
Write-Host ""

# Function to validate package identity name format
function Test-PackageIdentityName {
    param([string]$name)
    
    # Package identity name rules:
    # - Between 3 and 50 characters
    # - Can contain: letters, numbers, periods, hyphens
    # - Cannot start/end with period
    # - Cannot have consecutive periods
    
    if ([string]::IsNullOrWhiteSpace($name)) { return $false }
    if ($name.Length -lt 3 -or $name.Length -gt 50) { return $false }
    if ($name -match '^\.|\.$|\.\.') { return $false }
    if ($name -notmatch '^[a-zA-Z0-9.-]+$') { return $false }
    
    return $true
}

# Function to validate publisher certificate format
function Test-PublisherFormat {
    param([string]$publisher)
    
    if ([string]::IsNullOrWhiteSpace($publisher)) { return $false }
    
    # Should start with CN= and follow distinguished name format
    if ($publisher -notmatch '^CN=.+') { return $false }
    
    # Check for valid characters in DN
    if ($publisher -match '[<>]') { return $false }
    
    return $true
}

# Function to validate display name
function Test-DisplayName {
    param([string]$name)
    
    if ([string]::IsNullOrWhiteSpace($name)) { return $false }
    
    # Display name should be reasonable length and not contain control characters
    if ($name.Length -lt 1 -or $name.Length -gt 256) { return $false }
    if ($name -match '[\x00-\x1F\x7F]') { return $false }
    
    return $true
}

# Collect Microsoft Store Package Identity Name
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  Step 1: Package Identity Name" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Enter Microsoft Store Package/Identity/Name:" -ForegroundColor Yellow
Write-Host "  Location: Partner Center > Product Identity > Package/Identity/Name" -ForegroundColor Gray
Write-Host ""
Write-Host "  Format Requirements:" -ForegroundColor Gray
Write-Host "    - 3-50 characters" -ForegroundColor DarkGray
Write-Host "    - Letters, numbers, periods, hyphens only" -ForegroundColor DarkGray
Write-Host "    - Cannot start/end with period or have consecutive periods" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  Example: Publisher.MyAwesomeExtension" -ForegroundColor DarkGray
Write-Host ""

$packageIdentityName = ""
$maxAttempts = 3
$attempt = 0

do {
    $attempt++
    Write-Host "Package Identity Name" -NoNewline -ForegroundColor Yellow
    if ($attempt -gt 1) {
        Write-Host " (Attempt $attempt of $maxAttempts)" -NoNewline -ForegroundColor Red
    }
    Write-Host ": " -NoNewline -ForegroundColor Yellow
    $packageIdentityName = Read-Host
    
    if ([string]::IsNullOrWhiteSpace($packageIdentityName)) {
        Write-Host "  [ERROR] Package Identity Name cannot be empty." -ForegroundColor Red
        Write-Host ""
    }
    elseif (-not (Test-PackageIdentityName $packageIdentityName)) {
        Write-Host "  [ERROR] Invalid Package Identity Name format." -ForegroundColor Red
        Write-Host "  Please ensure it meets the format requirements listed above." -ForegroundColor Yellow
        Write-Host ""
    }
    else {
        Write-Host "  [OK] Package Identity Name accepted: $packageIdentityName" -ForegroundColor Green
        Write-Host ""
        break
    }
    
    if ($attempt -ge $maxAttempts) {
        Write-Host ""
        Write-Host "Maximum attempts reached. Please verify your Partner Center information and try again." -ForegroundColor Red
        Write-Host "Press any key to exit..." -ForegroundColor Gray
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
        exit 1
    }
} while ($true)

# Collect Microsoft Store Package Identity Publisher
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  Step 2: Publisher Certificate" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Enter Microsoft Store Package/Identity/Publisher:" -ForegroundColor Yellow
Write-Host "  Location: Partner Center > Product Identity > Package/Identity/Publisher" -ForegroundColor Gray
Write-Host ""
Write-Host "  Format Requirements:" -ForegroundColor Gray
Write-Host "    - Must start with 'CN=' (Certificate Name)" -ForegroundColor DarkGray
Write-Host "    - This is the publisher certificate distinguished name" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  Example: CN=12345678-1234-1234-1234-123456789012" -ForegroundColor DarkGray
Write-Host "  Example: CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US" -ForegroundColor DarkGray
Write-Host ""

$packageIdentityPublisher = ""
$attempt = 0

do {
    $attempt++
    Write-Host "Publisher Certificate" -NoNewline -ForegroundColor Yellow
    if ($attempt -gt 1) {
        Write-Host " (Attempt $attempt of $maxAttempts)" -NoNewline -ForegroundColor Red
    }
    Write-Host ": " -NoNewline -ForegroundColor Yellow
    $packageIdentityPublisher = Read-Host
    
    if ([string]::IsNullOrWhiteSpace($packageIdentityPublisher)) {
        Write-Host "  [ERROR] Publisher cannot be empty." -ForegroundColor Red
        Write-Host ""
    }
    elseif (-not (Test-PublisherFormat $packageIdentityPublisher)) {
        if ($packageIdentityPublisher -notmatch '^CN=') {
            Write-Host "  [ERROR] Publisher must start with 'CN='." -ForegroundColor Red
            Write-Host "  Copy the entire string from Partner Center, including 'CN='." -ForegroundColor Yellow
        }
        else {
            Write-Host "  [ERROR] Invalid publisher format." -ForegroundColor Red
            Write-Host "  Please ensure you copied the complete certificate name from Partner Center." -ForegroundColor Yellow
        }
        Write-Host ""
    }
    else {
        Write-Host "  [OK] Publisher certificate accepted" -ForegroundColor Green
        Write-Host ""
        break
    }
    
    if ($attempt -ge $maxAttempts) {
        Write-Host ""
        Write-Host "Maximum attempts reached. Please verify your Partner Center information and try again." -ForegroundColor Red
        Write-Host "Press any key to exit..." -ForegroundColor Gray
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
        exit 1
    }
} while ($true)

# Collect Reserved Display Name
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  Step 3: Display Name" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Enter the reserved Display Name from Partner Center:" -ForegroundColor Yellow
Write-Host "  Location: Partner Center > Product Management > Store Listing" -ForegroundColor Gray
Write-Host ""
Write-Host "  This is the app name visible to users in the Microsoft Store." -ForegroundColor Gray
Write-Host "  It must match EXACTLY what you reserved in Partner Center." -ForegroundColor Gray
Write-Host ""
Write-Host "  Example: My Awesome CmdPal Extension" -ForegroundColor DarkGray
Write-Host ""

$displayName = ""
$attempt = 0

do {
    $attempt++
    Write-Host "Display Name" -NoNewline -ForegroundColor Yellow
    if ($attempt -gt 1) {
        Write-Host " (Attempt $attempt of $maxAttempts)" -NoNewline -ForegroundColor Red
    }
    Write-Host ": " -NoNewline -ForegroundColor Yellow
    $displayName = Read-Host
    
    if ([string]::IsNullOrWhiteSpace($displayName)) {
        Write-Host "  [ERROR] Display Name cannot be empty." -ForegroundColor Red
        Write-Host ""
    }
    elseif (-not (Test-DisplayName $displayName)) {
        Write-Host "  [ERROR] Invalid Display Name." -ForegroundColor Red
        Write-Host "  Display name must be 1-256 characters and cannot contain control characters." -ForegroundColor Yellow
        Write-Host ""
    }
    else {
        Write-Host "  [OK] Display Name accepted: $displayName" -ForegroundColor Green
        Write-Host ""
        break
    }
    
    if ($attempt -ge $maxAttempts) {
        Write-Host ""
        Write-Host "Maximum attempts reached. Please try again with valid information." -ForegroundColor Red
        Write-Host "Press any key to exit..." -ForegroundColor Gray
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
        exit 1
    }
} while ($true)

# Collect Publisher Display Name
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  Step 4: Publisher Display Name" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Enter Microsoft Store Package/Properties/PublisherDisplayName:" -ForegroundColor Yellow
Write-Host "  Location: Partner Center > Product Identity > Package/Properties" -ForegroundColor Gray
Write-Host ""
Write-Host "  This is your company or developer name shown to users." -ForegroundColor Gray
Write-Host ""
Write-Host "  Example: Contoso Software Inc." -ForegroundColor DarkGray
Write-Host "  Example: Jessica Cha" -ForegroundColor DarkGray
Write-Host ""

$publisherDisplayName = ""
$attempt = 0

do {
    $attempt++
    Write-Host "Publisher Display Name" -NoNewline -ForegroundColor Yellow
    if ($attempt -gt 1) {
        Write-Host " (Attempt $attempt of $maxAttempts)" -NoNewline -ForegroundColor Red
    }
    Write-Host ": " -NoNewline -ForegroundColor Yellow
    $publisherDisplayName = Read-Host
    
    if ([string]::IsNullOrWhiteSpace($publisherDisplayName)) {
        Write-Host "  [ERROR] Publisher Display Name cannot be empty." -ForegroundColor Red
        Write-Host ""
    }
    elseif (-not (Test-DisplayName $publisherDisplayName)) {
        Write-Host "  [ERROR] Invalid Publisher Display Name." -ForegroundColor Red
        Write-Host "  Publisher name must be 1-256 characters and cannot contain control characters." -ForegroundColor Yellow
        Write-Host ""
    }
    else {
        Write-Host "  [OK] Publisher Display Name accepted: $publisherDisplayName" -ForegroundColor Green
        Write-Host ""
        break
    }
    
    if ($attempt -ge $maxAttempts) {
        Write-Host ""
        Write-Host "Maximum attempts reached. Please try again with valid information." -ForegroundColor Red
        Write-Host "Press any key to exit..." -ForegroundColor Gray
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
        exit 1
    }
} while ($true)

# Check for required assets
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  Step 5: Validating Required Assets" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Checking for Microsoft Store required asset images..." -ForegroundColor Yellow
Write-Host ""

$assetsPath = Join-Path $projectRoot "Assets"

# Check if Assets folder exists
if (-not (Test-Path $assetsPath)) {
    Write-Host "  [ERROR] Assets folder not found at: $assetsPath" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Please create the Assets folder and add the required images." -ForegroundColor Yellow
    Write-Host "  Press any key to continue anyway (you'll need to add assets later)..." -ForegroundColor Gray
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    Write-Host ""
}

$requiredAssets = @(
    @{ Name = "StoreLogo.png"; Size = "50x50"; Description = "Store logo for listings" },
    @{ Name = "Square150x150Logo.scale-200.png"; Size = "300x300"; Description = "Medium tile" },
    @{ Name = "Square44x44Logo.scale-200.png"; Size = "88x88"; Description = "App list icon" },
    @{ Name = "Wide310x150Logo.scale-200.png"; Size = "620x300"; Description = "Wide tile" },
    @{ Name = "SplashScreen.scale-200.png"; Size = "1240x600"; Description = "Splash screen" },
    @{ Name = "StoreLogo.scale-100.png"; Size = "50x50"; Description = "Store logo (100% scale)" }
)

$missingAssets = @()
$foundAssets = @()

Write-Host "  Asset Validation Results:" -ForegroundColor Cyan
Write-Host "  " -NoNewline
Write-Host ("{0,-45} {1,-15} {2}" -f "File", "Size", "Status") -ForegroundColor Gray
Write-Host "  " -NoNewline
Write-Host ("{0,-45} {1,-15} {2}" -f "----", "----", "------") -ForegroundColor DarkGray

foreach ($asset in $requiredAssets) {
    $assetPath = Join-Path $assetsPath $asset.Name
    $statusPrefix = "  "
    
    if (Test-Path $assetPath) {
        try {
            $fileInfo = Get-Item $assetPath
            $fileSize = "{0:N2} KB" -f ($fileInfo.Length / 1KB)
            
            Write-Host "  " -NoNewline
            Write-Host ("{0,-45} {1,-15} " -f $asset.Name, $asset.Size) -NoNewline -ForegroundColor White
            Write-Host "[OK]" -ForegroundColor Green
            
            $foundAssets += $asset.Name
        }
        catch {
            Write-Host "  " -NoNewline
            Write-Host ("{0,-45} {1,-15} " -f $asset.Name, $asset.Size) -NoNewline -ForegroundColor White
            Write-Host "[WARNING]" -ForegroundColor Yellow
            Write-Host "         (File exists but couldn't read properties)" -ForegroundColor DarkGray
            $foundAssets += $asset.Name
        }
    }
    else {
        Write-Host "  " -NoNewline
        Write-Host ("{0,-45} {1,-15} " -f $asset.Name, $asset.Size) -NoNewline -ForegroundColor White
        Write-Host "[MISSING]" -ForegroundColor Red
        Write-Host "         ($($asset.Description))" -ForegroundColor DarkGray
        $missingAssets += $asset
    }
}

Write-Host ""
Write-Host "  Summary: " -NoNewline -ForegroundColor Cyan
Write-Host "$($foundAssets.Count) of $($requiredAssets.Count) assets found" -ForegroundColor White

if ($missingAssets.Count -gt 0) {
    Write-Host ""
    Write-Host "  [WARNING] $($missingAssets.Count) asset(s) missing" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  The Microsoft Store requires specific image assets for your app listing." -ForegroundColor Gray
    Write-Host "  You'll need to add these before you can publish." -ForegroundColor Gray
    Write-Host ""
    Write-Host "  TIP: Use the Windows App SDK project templates or design tools to create" -ForegroundColor Cyan
    Write-Host "       properly sized assets. Each image must be exactly the size specified." -ForegroundColor Cyan
    Write-Host ""
}
else {
    Write-Host "  [OK] All required assets are present!" -ForegroundColor Green
    Write-Host ""
}

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  Updating Project Files..." -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "The script will now update your project files with the information you provided." -ForegroundColor White
Write-Host "Original files have been backed up in the Publication\Backups folder." -ForegroundColor Gray
Write-Host ""

# Update Package.appxmanifest
Write-Host "[1/2] Updating Package.appxmanifest..." -ForegroundColor Cyan

try {
    $manifestContent = Get-Content $manifestPath -Raw -ErrorAction Stop
}
catch {
    Write-Host "  [ERROR] Could not read Package.appxmanifest: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "  The file may be locked by another process." -ForegroundColor Yellow
    Write-Host "  Press any key to exit..." -ForegroundColor Gray
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

# Backup original content
$manifestBackup = $manifestContent
$manifestUpdateCount = 0

# Update Identity element (Name and Publisher)
Write-Host "  Updating Identity Name..." -NoNewline -ForegroundColor Gray
$identityNamePattern = '(?<=<Identity\s+Name=")[^"]*'
if ($manifestContent -match $identityNamePattern) {
    $oldValue = $Matches[0]
    $identityNameUpdated = $manifestContent -replace $identityNamePattern, $packageIdentityName
    if ($identityNameUpdated -ne $manifestContent) {
        $manifestContent = $identityNameUpdated
        $manifestUpdateCount++
        Write-Host " [OK]" -ForegroundColor Green
        Write-Host "    Changed from: '$oldValue'" -ForegroundColor DarkGray
        Write-Host "    Changed to:   '$packageIdentityName'" -ForegroundColor DarkGray
    }
    else {
        Write-Host " [NO CHANGE]" -ForegroundColor Yellow
    }
}
else {
    Write-Host " [WARNING] Could not find Identity Name attribute" -ForegroundColor Yellow
}

Write-Host "  Updating Publisher..." -NoNewline -ForegroundColor Gray
$publisherPattern = '(?<=Publisher=")[^"]*(?=")'
if ($manifestContent -match $publisherPattern) {
    $oldValue = $Matches[0]
    $identityPublisherUpdated = $manifestContent -replace $publisherPattern, $packageIdentityPublisher
    if ($identityPublisherUpdated -ne $manifestContent) {
        $manifestContent = $identityPublisherUpdated
        $manifestUpdateCount++
        Write-Host " [OK]" -ForegroundColor Green
        Write-Host "    Changed from: '$oldValue'" -ForegroundColor DarkGray
        Write-Host "    Changed to:   '$packageIdentityPublisher'" -ForegroundColor DarkGray
    }
    else {
        Write-Host " [NO CHANGE]" -ForegroundColor Yellow
    }
}
else {
    Write-Host " [WARNING] Could not find Publisher attribute" -ForegroundColor Yellow
}

# Update Properties (DisplayName and PublisherDisplayName)
Write-Host "  Updating Display Name..." -NoNewline -ForegroundColor Gray
$displayNamePattern = '(?<=<DisplayName>)[^<]*(?=</DisplayName>)'
if ($manifestContent -match $displayNamePattern) {
    $oldValue = $Matches[0]
    $displayNameUpdated = $manifestContent -replace $displayNamePattern, $displayName
    if ($displayNameUpdated -ne $manifestContent) {
        $manifestContent = $displayNameUpdated
        $manifestUpdateCount++
        Write-Host " [OK]" -ForegroundColor Green
        Write-Host "    Changed from: '$oldValue'" -ForegroundColor DarkGray
        Write-Host "    Changed to:   '$displayName'" -ForegroundColor DarkGray
    }
    else {
        Write-Host " [NO CHANGE]" -ForegroundColor Yellow
    }
}
else {
    Write-Host " [WARNING] Could not find DisplayName element" -ForegroundColor Yellow
}

Write-Host "  Updating Publisher Display Name..." -NoNewline -ForegroundColor Gray
$publisherDisplayNamePattern = '(?<=<PublisherDisplayName>)[^<]*(?=</PublisherDisplayName>)'
if ($manifestContent -match $publisherDisplayNamePattern) {
    $oldValue = $Matches[0]
    $publisherDisplayNameUpdated = $manifestContent -replace $publisherDisplayNamePattern, $publisherDisplayName
    if ($publisherDisplayNameUpdated -ne $manifestContent) {
        $manifestContent = $publisherDisplayNameUpdated
        $manifestUpdateCount++
        Write-Host " [OK]" -ForegroundColor Green
        Write-Host "    Changed from: '$oldValue'" -ForegroundColor DarkGray
        Write-Host "    Changed to:   '$publisherDisplayName'" -ForegroundColor DarkGray
    }
    else {
        Write-Host " [NO CHANGE]" -ForegroundColor Yellow
    }
}
else {
    Write-Host " [WARNING] Could not find PublisherDisplayName element" -ForegroundColor Yellow
}

# Also update the VisualElements DisplayName
Write-Host "  Updating VisualElements Display Name..." -NoNewline -ForegroundColor Gray
$visualElementsPattern = '(?<=<uap:VisualElements[^>]*DisplayName=")[^"]*'
if ($manifestContent -match $visualElementsPattern) {
    $oldValue = $Matches[0]
    if ($oldValue -ne $displayName) {
        $visualElementsUpdated = $manifestContent -replace $visualElementsPattern, $displayName
        if ($visualElementsUpdated -ne $manifestContent) {
            $manifestContent = $visualElementsUpdated
            $manifestUpdateCount++
            Write-Host " [OK]" -ForegroundColor Green
        }
        else {
            Write-Host " [NO CHANGE]" -ForegroundColor Yellow
        }
    }
    else {
        Write-Host " [ALREADY SET]" -ForegroundColor Green
    }
}
else {
    Write-Host " [SKIP]" -ForegroundColor Gray
}

# Write the updated manifest
if ($manifestContent -ne $manifestBackup) {
    try {
        Set-Content -Path $manifestPath -Value $manifestContent -NoNewline -ErrorAction Stop
        Write-Host ""
        Write-Host "  [SUCCESS] Package.appxmanifest updated ($manifestUpdateCount changes)" -ForegroundColor Green
    }
    catch {
        Write-Host ""
        Write-Host "  [ERROR] Could not write to Package.appxmanifest: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "  The file may be read-only or locked by another process." -ForegroundColor Yellow
        Write-Host "  Press any key to exit..." -ForegroundColor Gray
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
        exit 1
    }
}
else {
    Write-Host ""
    Write-Host "  [INFO] No changes were needed for Package.appxmanifest" -ForegroundColor Cyan
}
Write-Host ""

# Update .csproj file
Write-Host "[2/2] Updating TemplateCmdPalExtension.csproj..." -ForegroundColor Cyan

try {
    $csprojContent = Get-Content $csprojPath -Raw -ErrorAction Stop
}
catch {
    Write-Host "  [ERROR] Could not read .csproj file: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "  The file may be locked by another process." -ForegroundColor Yellow
    Write-Host "  Press any key to exit..." -ForegroundColor Gray
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

# Backup original content
$csprojBackup = $csprojContent
$csprojUpdateCount = 0

# Check if Store properties are commented or uncommented
Write-Host "  Checking Store property configuration..." -NoNewline -ForegroundColor Gray
$storePropsCommentedPattern = '<!--\s*<AppxPackageIdentityName>YOUR_PACKAGE_IDENTITY_NAME_HERE</AppxPackageIdentityName>\s*<AppxPackagePublisher>YOUR_PACKAGE_IDENTITY_PUBLISHER_HERE</AppxPackagePublisher>\s*<AppxPackageVersion>[^<]*</AppxPackageVersion>\s*-->'

if ($csprojContent -match $storePropsCommentedPattern) {
    Write-Host " [COMMENTED]" -ForegroundColor Yellow
    Write-Host "  Uncommenting and updating Store properties..." -ForegroundColor Gray
    
    # Uncomment and update the Store-specific properties
    $replacement = "<AppxPackageIdentityName>$packageIdentityName</AppxPackageIdentityName>`n    <AppxPackagePublisher>$packageIdentityPublisher</AppxPackagePublisher>`n    <AppxPackageVersion>0.0.1.0</AppxPackageVersion>"
    
    $csprojContent = $csprojContent -replace $storePropsCommentedPattern, $replacement
    $csprojUpdateCount++
    Write-Host "    [OK] Store properties uncommented and updated" -ForegroundColor Green
}
else {
    Write-Host " [UNCOMMENTED]" -ForegroundColor Green
    Write-Host "  Updating existing Store property values..." -ForegroundColor Gray
    
    # Try updating already-uncommented properties
    $identityNamePattern = '(?<=<AppxPackageIdentityName>)[^<]*(?=</AppxPackageIdentityName>)'
    if ($csprojContent -match $identityNamePattern) {
        $oldValue = $Matches[0]
        if ($oldValue -ne $packageIdentityName) {
            $csprojContent = $csprojContent -replace $identityNamePattern, $packageIdentityName
            $csprojUpdateCount++
            Write-Host "    Updated AppxPackageIdentityName" -ForegroundColor Green
        }
    }
    
    $publisherPattern = '(?<=<AppxPackagePublisher>)[^<]*(?=</AppxPackagePublisher>)'
    if ($csprojContent -match $publisherPattern) {
        $oldValue = $Matches[0]
        if ($oldValue -ne $packageIdentityPublisher) {
            $csprojContent = $csprojContent -replace $publisherPattern, $packageIdentityPublisher
            $csprojUpdateCount++
            Write-Host "    Updated AppxPackagePublisher" -ForegroundColor Green
        }
    }
}

# Uncomment the PrepareAssets Target section (using (?s) for multi-line matching)
Write-Host "  Checking PrepareAssets Target..." -NoNewline -ForegroundColor Gray
$targetPattern = '(?s)<!--\s*(<Target Name="PrepareAssets".*?</Target>)\s*-->'

if ($csprojContent -match $targetPattern) {
    Write-Host " [COMMENTED]" -ForegroundColor Yellow
    Write-Host "  Uncommenting PrepareAssets Target..." -ForegroundColor Gray
    
    $targetReplacement = '$1'
    $targetUpdated = $csprojContent -replace $targetPattern, $targetReplacement
    
    if ($targetUpdated -ne $csprojContent) {
        $csprojContent = $targetUpdated
        $csprojUpdateCount++
        Write-Host "    [OK] PrepareAssets Target uncommented" -ForegroundColor Green
    }
    else {
        Write-Host "    [WARNING] Could not uncomment PrepareAssets Target" -ForegroundColor Yellow
    }
}
else {
    Write-Host " [ALREADY UNCOMMENTED]" -ForegroundColor Green
}

# Write the updated csproj
if ($csprojContent -ne $csprojBackup) {
    try {
        Set-Content -Path $csprojPath -Value $csprojContent -NoNewline -ErrorAction Stop
        Write-Host ""
        Write-Host "  [SUCCESS] TemplateCmdPalExtension.csproj updated ($csprojUpdateCount changes)" -ForegroundColor Green
    }
    catch {
        Write-Host ""
        Write-Host "  [ERROR] Could not write to .csproj file: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "  The file may be read-only or locked by another process." -ForegroundColor Yellow
        Write-Host "  Press any key to exit..." -ForegroundColor Gray
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
        exit 1
    }
}
else {
    Write-Host ""
    Write-Host "  [INFO] No changes were needed for TemplateCmdPalExtension.csproj" -ForegroundColor Cyan
}
Write-Host ""

# Display summary
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "  CONFIGURATION SUMMARY" -ForegroundColor White
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "  Package Identity Name:" -ForegroundColor Gray
Write-Host "    $packageIdentityName" -ForegroundColor White
Write-Host ""
Write-Host "  Publisher:" -ForegroundColor Gray

# Truncate publisher if too long
$publisherDisplay = if ($packageIdentityPublisher.Length -gt 80) {
    $packageIdentityPublisher.Substring(0, 77) + "..."
} else {
    $packageIdentityPublisher
}
Write-Host "    $publisherDisplay" -ForegroundColor White
Write-Host ""
Write-Host "  Display Name:" -ForegroundColor Gray
Write-Host "    $displayName" -ForegroundColor White
Write-Host ""
Write-Host "  Publisher Display Name:" -ForegroundColor Gray
Write-Host "    $publisherDisplayName" -ForegroundColor White
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host ""

# Display modified files
Write-Host "Modified Files:" -ForegroundColor Yellow
$manifestRelative = $manifestPath -replace [regex]::Escape($projectRoot), "."
$csprojRelative = $csprojPath -replace [regex]::Escape($projectRoot), "."
Write-Host "  ✓ $manifestRelative" -ForegroundColor Green
Write-Host "  ✓ $csprojRelative" -ForegroundColor Green
Write-Host ""

if ($backupDir) {
    Write-Host "Backup Location:" -ForegroundColor Yellow
    $backupRelative = $backupDir -replace [regex]::Escape($projectRoot), "."
    Write-Host "  $backupRelative" -ForegroundColor Gray
    Write-Host ""
}

# Asset status
if ($missingAssets.Count -gt 0) {
    Write-Host "=================================================================" -ForegroundColor Red
    Write-Host "  ACTION REQUIRED: Missing Assets" -ForegroundColor Yellow
    Write-Host "=================================================================" -ForegroundColor Red
    Write-Host "  $($missingAssets.Count) required asset(s) are missing. Add them before publishing:" -ForegroundColor White
    Write-Host ""
    
    foreach ($asset in $missingAssets) {
        Write-Host "    * $($asset.Name) ($($asset.Size))" -ForegroundColor White
    }
    
    Write-Host "=================================================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "Asset Creation Tips:" -ForegroundColor Cyan
    Write-Host "  * Use PNG format with transparency where appropriate" -ForegroundColor Gray
    Write-Host "  * Follow Microsoft Store asset guidelines" -ForegroundColor Gray
    Write-Host "  * Reference: https://learn.microsoft.com/windows/apps/design/style/app-icons-and-logos" -ForegroundColor DarkCyan
    Write-Host ""
}
else {
    Write-Host "  [OK] All required assets are present" -ForegroundColor Green
    Write-Host ""
}

# Final success message with conditional messaging
Write-Host "=================================================================" -ForegroundColor Green
if ($missingAssets.Count -gt 0) {
    Write-Host "  Setup Complete - Action Required" -ForegroundColor Yellow
    Write-Host "=================================================================" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  Your project has been configured for Microsoft Store publishing." -ForegroundColor White
    Write-Host "  However, you need to add $($missingAssets.Count) missing asset(s) before publishing." -ForegroundColor Yellow
}
else {
    Write-Host "  Setup Completed Successfully!" -ForegroundColor Green
    Write-Host "=================================================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Your extension is ready for Microsoft Store publishing!" -ForegroundColor White
    Write-Host "  All configuration and assets are in place." -ForegroundColor Green
}
Write-Host ""
Write-Host "Press any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")