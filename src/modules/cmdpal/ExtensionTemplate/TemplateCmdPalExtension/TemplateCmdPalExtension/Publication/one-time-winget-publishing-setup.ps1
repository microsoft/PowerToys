# One-Time WinGet Publication Setup Script for CmdPal Extension
# This script collects information and updates files needed for WinGet publication via EXE installer
# Version: 1.0

#Requires -Version 5.1


# Enable strict mode for better error detection
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  WinGet Publication Setup (EXE Installer)" -ForegroundColor Cyan
Write-Host "  CmdPal Extension Publisher" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# Path to the project files
$publicationRoot = $PSScriptRoot
$projectRoot = Split-Path -Parent $publicationRoot
$projectName = Split-Path -Leaf $projectRoot
$wingetResourcesPath = Join-Path $PSScriptRoot "winget-resources"

Write-Host "Validating project structure..." -ForegroundColor Cyan
Write-Host "  Publication Root: $publicationRoot" -ForegroundColor Gray
Write-Host "  Project Root: $projectRoot" -ForegroundColor Gray
Write-Host "  Project Name: $projectName" -ForegroundColor Gray
Write-Host ""

# Verify required files exist
$csprojPath = Join-Path $projectRoot "$projectName.csproj"
$manifestPath = Join-Path $projectRoot "Package.appxmanifest"
$extensionCsPath = Join-Path $projectRoot "$projectName.cs"

$buildExePath = Join-Path $wingetResourcesPath "build-exe.ps1"
$setupTemplatePath = Join-Path $wingetResourcesPath "setup-template.iss"
$releaseYmlPath = Join-Path $wingetResourcesPath "release-extension.yml"

if (-not (Test-Path $csprojPath)) {
    Write-Host "ERROR: Could not find .csproj file at: $csprojPath" -ForegroundColor Red
    Write-Host "Press any key to exit..." -ForegroundColor Gray
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

if (-not (Test-Path $manifestPath)) {
    Write-Host "ERROR: Could not find Package.appxmanifest at: $manifestPath" -ForegroundColor Red
    Write-Host "Press any key to exit..." -ForegroundColor Gray
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

if (-not (Test-Path $buildExePath)) {
    Write-Host "ERROR: Could not find build-exe.ps1 at: $buildExePath" -ForegroundColor Red
    Write-Host "Press any key to exit..." -ForegroundColor Gray
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

if (-not (Test-Path $setupTemplatePath)) {
    Write-Host "ERROR: Could not find setup-template.iss at: $setupTemplatePath" -ForegroundColor Red
    Write-Host "Press any key to exit..." -ForegroundColor Gray
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

if (-not (Test-Path $releaseYmlPath)) {
    Write-Host "ERROR: Could not find release-extension.yml at: $releaseYmlPath" -ForegroundColor Red
    Write-Host "Press any key to exit..." -ForegroundColor Gray
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

Write-Host "  [OK] All required files found" -ForegroundColor Green
Write-Host ""

# Create backup directory
$backupDir = Join-Path $wingetResourcesPath "Backups"
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
        Copy-Item $buildExePath -Destination (Join-Path $backupDir "build-exe.ps1.$timestamp.bak") -Force
        Copy-Item $setupTemplatePath -Destination (Join-Path $backupDir "setup-template.iss.$timestamp.bak") -Force
        Copy-Item $releaseYmlPath -Destination (Join-Path $backupDir "release-extension.yml.$timestamp.bak") -Force
        Write-Host "Backups created: $timestamp" -ForegroundColor Gray
        Write-Host ""
    }
    catch {
        Write-Host "WARNING: Could not create backup files. Proceeding anyway." -ForegroundColor Yellow
        Write-Host ""
    }
}

# Read existing project information
Write-Host "Reading project information..." -ForegroundColor Cyan
try {
    [xml]$manifest = Get-Content $manifestPath -ErrorAction Stop
    $packageName = $manifest.Package.Identity.Name
    $packageVersion = $manifest.Package.Identity.Version
    $displayName = $manifest.Package.Properties.DisplayName
    $publisherDisplayName = $manifest.Package.Properties.PublisherDisplayName
    
    Write-Host "  Current Package Name: $packageName" -ForegroundColor White
    Write-Host "  Current Version: $packageVersion" -ForegroundColor White
    Write-Host "  Current Display Name: $displayName" -ForegroundColor White
    Write-Host "  Current Publisher: $publisherDisplayName" -ForegroundColor White
    Write-Host ""
}
catch {
    Write-Host "ERROR: Could not read Package.appxmanifest: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Press any key to exit..." -ForegroundColor Gray
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

# Extract GUID/CLSID from extension class
Write-Host "Reading extension GUID..." -ForegroundColor Cyan
try {
    $extensionCsContent = Get-Content $extensionCsPath -Raw -ErrorAction Stop
    if ($extensionCsContent -match '\[Guid\("([A-F0-9-]+)"\)\]') {
        $extensionGuid = $Matches[1]
        Write-Host "  Extension GUID: $extensionGuid" -ForegroundColor White
        Write-Host ""
    }
    else {
        Write-Host "ERROR: Could not find GUID in $projectName.cs" -ForegroundColor Red
        Write-Host "Press any key to exit..." -ForegroundColor Gray
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
        exit 1
    }
}
catch {
    Write-Host "ERROR: Could not read $projectName.cs: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Press any key to exit..." -ForegroundColor Gray
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

Write-Host "This script will configure your extension for WinGet publication using EXE installer." -ForegroundColor White
Write-Host ""
Write-Host "The following information will be collected:" -ForegroundColor White
Write-Host "  - GitHub Repository URL (for releases)" -ForegroundColor Gray
Write-Host "  - Developer/Publisher Name" -ForegroundColor Gray
Write-Host ""
Write-Host "The script will update:" -ForegroundColor Yellow
Write-Host "  - build-exe.ps1 (build script)" -ForegroundColor Gray
Write-Host "  - setup-template.iss (Inno Setup installer script)" -ForegroundColor Gray
Write-Host "  - release-extension.yml (GitHub Actions workflow)" -ForegroundColor Gray
Write-Host ""

# Function to validate URL
function Test-GitHubUrl {
    param([string]$url)
    
    if ([string]::IsNullOrWhiteSpace($url)) { return $false }
    if ($url -notmatch '^https://github\.com/[a-zA-Z0-9_-]+/[a-zA-Z0-9_.-]+/?$') { return $false }
    
    return $true
}

# Function to validate developer name
function Test-DeveloperName {
    param([string]$name)
    
    if ([string]::IsNullOrWhiteSpace($name)) { return $false }
    if ($name.Length -lt 1 -or $name.Length -gt 256) { return $false }
    
    return $true
}

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

# Collect GitHub Repository URL
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  Step 1: GitHub Repository URL" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Enter your GitHub repository URL:" -ForegroundColor Yellow
Write-Host "  This is where your extension's releases will be published." -ForegroundColor Gray
Write-Host ""
Write-Host "  Format: https://github.com/username/repository" -ForegroundColor DarkGray
Write-Host "  Example: https://github.com/johndoe/MyAwesomeExtension" -ForegroundColor DarkGray
Write-Host ""

$githubRepoUrl = ""
$maxAttempts = 3
$attempt = 0

do {
    $attempt++
    Write-Host "GitHub Repository URL" -NoNewline -ForegroundColor Yellow
    if ($attempt -gt 1) {
        Write-Host " (Attempt $attempt of $maxAttempts)" -NoNewline -ForegroundColor Red
    }
    Write-Host ": " -NoNewline -ForegroundColor Yellow
    $githubRepoUrl = Read-Host
    
    if ([string]::IsNullOrWhiteSpace($githubRepoUrl)) {
        Write-Host "  [ERROR] GitHub Repository URL cannot be empty." -ForegroundColor Red
        Write-Host ""
    }
    elseif (-not (Test-GitHubUrl $githubRepoUrl)) {
        Write-Host "  [ERROR] Invalid GitHub URL format." -ForegroundColor Red
        Write-Host "  Please use format: https://github.com/username/repository" -ForegroundColor Yellow
        Write-Host ""
    }
    else {
        Write-Host "  [OK] GitHub Repository URL accepted: $githubRepoUrl" -ForegroundColor Green
        Write-Host ""
        break
    }
    
    if ($attempt -ge $maxAttempts) {
        Write-Host ""
        Write-Host "Maximum attempts reached. Please try again with a valid GitHub URL." -ForegroundColor Red
        Write-Host "Press any key to exit..." -ForegroundColor Gray
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
        exit 1
    }
} while ($true)

# Collect Developer Name
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  Step 2: Developer/Publisher Name" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Enter your developer or publisher name:" -ForegroundColor Yellow
Write-Host "  This will appear in the EXE installer as the publisher." -ForegroundColor Gray
Write-Host ""
Write-Host "  IMPORTANT: If you published to Microsoft Store, this should match" -ForegroundColor Yellow
Write-Host "  the PublisherDisplayName from your Store configuration." -ForegroundColor Yellow
Write-Host ""
Write-Host "  Example: John Doe" -ForegroundColor DarkGray
Write-Host "  Example: Contoso Software" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  Current value from manifest: $publisherDisplayName" -ForegroundColor Cyan
Write-Host ""

$developerName = ""
$attempt = 0

do {
    $attempt++
    Write-Host "Developer Name" -NoNewline -ForegroundColor Yellow
    if ($attempt -gt 1) {
        Write-Host " (Attempt $attempt of $maxAttempts)" -NoNewline -ForegroundColor Red
    }
    Write-Host " [press Enter to use default]: " -NoNewline -ForegroundColor Yellow
    $input = Read-Host
    
    if ([string]::IsNullOrWhiteSpace($input)) {
        $developerName = $publisherDisplayName
        Write-Host "  [OK] Using default: $developerName" -ForegroundColor Green
        Write-Host ""
        break
    }
    elseif (-not (Test-DeveloperName $input)) {
        Write-Host "  [ERROR] Invalid developer name." -ForegroundColor Red
        Write-Host ""
    }
    else {
        $developerName = $input
        Write-Host "  [OK] Developer name accepted: $developerName" -ForegroundColor Green
        Write-Host ""
        break
    }
    
    if ($attempt -ge $maxAttempts) {
        Write-Host ""
        Write-Host "Maximum attempts reached. Using default: $publisherDisplayName" -ForegroundColor Yellow
        $developerName = $publisherDisplayName
        Write-Host ""
        break
    }
} while ($true)

# Update files
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  Updating Configuration Files..." -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# Update build-exe.ps1
Write-Host "[1/3] Updating build-exe.ps1..." -ForegroundColor Cyan
try {
    $buildExeContent = Get-Content $buildExePath -Raw -ErrorAction Stop
    
    # Update ExtensionName default value
    $buildExeContent = $buildExeContent -replace '\[string\]\$ExtensionName = "UPDATE"', "[string]`$ExtensionName = `"$projectName`""
    
    # Update Version default value
    $buildExeContent = $buildExeContent -replace '\[string\]\$Version = "UPDATE"', "[string]`$Version = `"$packageVersion`""
    
    Set-Content -Path $buildExePath -Value $buildExeContent -NoNewline -ErrorAction Stop
    Write-Host "  [SUCCESS] build-exe.ps1 updated" -ForegroundColor Green
    Write-Host ""
}
catch {
    Write-Host "  [ERROR] Could not update build-exe.ps1: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
}

# Update setup-template.iss
Write-Host "[2/3] Updating setup-template.iss..." -ForegroundColor Cyan
try {
    $setupTemplateContent = Get-Content $setupTemplatePath -Raw -ErrorAction Stop
    
    # Update version
    $setupTemplateContent = $setupTemplateContent -replace '#define AppVersion ".*"', "#define AppVersion `"$packageVersion`""
    
    # Update AppId GUID
    $setupTemplateContent = $setupTemplateContent -replace 'AppId=\{\{GUID-HERE\}\}', "AppId={{$extensionGuid}}"
    
    # Update AppName (DISPLAY_NAME)
    $setupTemplateContent = $setupTemplateContent -replace 'AppName=DISPLAY_NAME', "AppName=$displayName"
    
    # Update AppPublisher (DEVELOPER_NAME)
    $setupTemplateContent = $setupTemplateContent -replace 'AppPublisher=DEVELOPER_NAME', "AppPublisher=$developerName"
    
    # Update DefaultDirName (EXTENSION_NAME)
    $setupTemplateContent = $setupTemplateContent -replace 'DefaultDirName=\{autopf\}\\EXTENSION_NAME', "DefaultDirName={autopf}\$projectName"
    
    # Update OutputBaseFilename (EXTENSION_NAME)
    $setupTemplateContent = $setupTemplateContent -replace 'OutputBaseFilename=EXTENSION_NAME-Setup', "OutputBaseFilename=$projectName-Setup"
    
    # Update Icon name (DISPLAY_NAME)
    $setupTemplateContent = $setupTemplateContent -replace 'Name: "\{group\}\\DISPLAY_NAME"', "Name: `"{group}\$displayName`""
    
    # Update Icon filename (EXTENSION_NAME)
    $setupTemplateContent = $setupTemplateContent -replace 'Filename: "\{app\}\\EXTENSION_NAME\.exe"', "Filename: `"{app}\$projectName.exe`""
    
    # Update Registry CLSID entries
    $setupTemplateContent = $setupTemplateContent -replace 'CLSID\\CLSID-HERE', "CLSID\{{$extensionGuid}}"
    $setupTemplateContent = $setupTemplateContent -replace '\{\{CLSID-HERE\}\}', "{{$extensionGuid}}"
    
    # Update Registry ValueData (EXTENSION_NAME)
    $setupTemplateContent = $setupTemplateContent -replace 'ValueData: "EXTENSION_NAME"', "ValueData: `"$projectName`""
    
    # Update LocalServer32 ValueData
    $setupTemplateContent = $setupTemplateContent -replace 'ValueData: "\{app\}\\EXTENSION_NAME\.exe', "ValueData: `"{app}\$projectName.exe"
    
    Set-Content -Path $setupTemplatePath -Value $setupTemplateContent -NoNewline -ErrorAction Stop
    Write-Host "  [SUCCESS] setup-template.iss updated" -ForegroundColor Green
    Write-Host ""
}
catch {
    Write-Host "  [ERROR] Could not update setup-template.iss: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
}

# Update release-extension.yml
Write-Host "[3/3] Updating release-extension.yml..." -ForegroundColor Cyan
try {
    $releaseYmlContent = Get-Content $releaseYmlPath -Raw -ErrorAction Stop
    
    # Update workflow name
    $releaseYmlContent = $releaseYmlContent -replace 'name: CmdPal Extension - Build EXE Installer', "name: $displayName - Build EXE Installer"
    
    # Update environment variables with actual values
    $releaseYmlContent = $releaseYmlContent -replace "DISPLAY_NAME: \$\{\{ vars\.DISPLAY_NAME \|\| 'DISPLAY_NAME' \}\}", "DISPLAY_NAME: `${{ vars.DISPLAY_NAME || '$displayName' }}"
    $releaseYmlContent = $releaseYmlContent -replace "EXTENSION_NAME: \$\{\{ vars\.EXTENSION_NAME \|\| 'EXTENSION_NAME' \}\}", "EXTENSION_NAME: `${{ vars.EXTENSION_NAME || '$projectName' }}"
    $releaseYmlContent = $releaseYmlContent -replace "FOLDER_NAME: \$\{\{ vars\.FOLDER_NAME \|\| 'FOLDER_NAME' \}\}", "FOLDER_NAME: `${{ vars.FOLDER_NAME || '$projectName' }}"
    $releaseYmlContent = $releaseYmlContent -replace "GITHUB_REPO_URL: \$\{\{ vars\.GITHUB_REPO_URL \|\| 'GITHUB_REPO_URL' \}\}", "GITHUB_REPO_URL: `${{ vars.GITHUB_REPO_URL || '$githubRepoUrl' }}"
    
    Set-Content -Path $releaseYmlPath -Value $releaseYmlContent -NoNewline -ErrorAction Stop
    Write-Host "  [SUCCESS] release-extension.yml updated" -ForegroundColor Green
    Write-Host ""
}
catch {
    Write-Host "  [ERROR] Could not update release-extension.yml: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
}

# Display summary
Write-Host "================================================================" -ForegroundColor Green
Write-Host "  CONFIGURATION SUMMARY" -ForegroundColor White
Write-Host "================================================================" -ForegroundColor Green
Write-Host "  Extension Name:" -ForegroundColor Gray
Write-Host "    $projectName" -ForegroundColor White
Write-Host ""
Write-Host "  Display Name:" -ForegroundColor Gray
Write-Host "    $displayName" -ForegroundColor White
Write-Host ""
Write-Host "  Version:" -ForegroundColor Gray
Write-Host "    $packageVersion" -ForegroundColor White
Write-Host ""
Write-Host "  Developer:" -ForegroundColor Gray
Write-Host "    $developerName" -ForegroundColor White
Write-Host ""
Write-Host "  Extension GUID:" -ForegroundColor Gray
Write-Host "    $extensionGuid" -ForegroundColor White
Write-Host ""
Write-Host "  GitHub Repository:" -ForegroundColor Gray
Write-Host "    $githubRepoUrl" -ForegroundColor White
Write-Host "================================================================" -ForegroundColor Green
Write-Host ""

# Display modified files
Write-Host "Updated Files:" -ForegroundColor Yellow
$buildExeRelative = $buildExePath -replace [regex]::Escape($projectRoot + "\"), ""
$setupTemplateRelative = $setupTemplatePath -replace [regex]::Escape($projectRoot + "\"), ""
$releaseYmlRelative = $releaseYmlPath -replace [regex]::Escape($projectRoot + "\"), ""
Write-Host "  [OK] $buildExeRelative" -ForegroundColor Green
Write-Host "  [OK] $setupTemplateRelative" -ForegroundColor Green
Write-Host "  [OK] $releaseYmlRelative" -ForegroundColor Green
Write-Host ""

if ($backupDir) {
    Write-Host "Backup Location:" -ForegroundColor Yellow
    $backupRelative = $backupDir -replace [regex]::Escape($projectRoot + "\"), ""
    Write-Host "  $backupRelative" -ForegroundColor Gray
    Write-Host ""
}

# Move files to correct locations
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  Moving Files to Correct Locations" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "The following file will be moved:" -ForegroundColor Yellow
Write-Host "  - release-extension.yml → .github/workflows/ (2 levels up)" -ForegroundColor Gray
Write-Host ""
Write-Host "The following files will remain in winget-resources:" -ForegroundColor Yellow
Write-Host "  - build-exe.ps1" -ForegroundColor Gray
Write-Host "  - setup-template.iss" -ForegroundColor Gray
Write-Host ""

# Calculate destination paths
# From: TemplateCmdPalExtension/Publication/winget-resources/
# release-extension.yml → TemplateCmdPalExtension/.github/workflows/ (2 levels up from Publication)

# GitHub workflows directory (2 levels up from Publication)
$solutionRoot = Split-Path -Parent $projectRoot
$githubWorkflowsDir = Join-Path $solutionRoot ".github\workflows"
$releaseYmlDestination = Join-Path $githubWorkflowsDir "release-extension.yml"

Write-Host "Destination:" -ForegroundColor Yellow
Write-Host "  release-extension.yml → $releaseYmlDestination" -ForegroundColor Gray
Write-Host ""

# Move release-extension.yml
Write-Host "Moving release-extension.yml..." -ForegroundColor Cyan
try {
    if (-not (Test-Path $githubWorkflowsDir)) {
        Write-Host "  Creating .github/workflows directory..." -ForegroundColor Gray
        New-Item -Path $githubWorkflowsDir -ItemType Directory -Force | Out-Null
    }
    
    if (Test-Path $releaseYmlDestination) {
        Write-Host "  [WARNING] Destination file exists, overwriting..." -ForegroundColor Yellow
    }
    Move-Item $releaseYmlPath -Destination $releaseYmlDestination -Force -ErrorAction Stop
    Write-Host "  [SUCCESS] Moved to: $releaseYmlDestination" -ForegroundColor Green
}
catch {
    Write-Host "  [ERROR] Could not move release-extension.yml: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Verify file was moved
Write-Host "Verifying file..." -ForegroundColor Cyan

if (Test-Path $releaseYmlDestination) {
    Write-Host "  [OK] release-extension.yml exists at destination" -ForegroundColor Green
}
else {
    Write-Host "  [ERROR] release-extension.yml NOT found at destination" -ForegroundColor Red
}
Write-Host ""

# Final instructions
Write-Host "================================================================" -ForegroundColor Green
Write-Host "  Setup Completed Successfully!" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Files have been configured:" -ForegroundColor Yellow
Write-Host "  Updated (in winget-resources):" -ForegroundColor Cyan
Write-Host "    $buildExePath" -ForegroundColor White
Write-Host "    $setupTemplatePath" -ForegroundColor White
Write-Host ""
Write-Host "  Moved to GitHub workflows:" -ForegroundColor Cyan
Write-Host "    $releaseYmlDestination" -ForegroundColor White
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Review the configured files to ensure correctness" -ForegroundColor Gray
Write-Host "  2. Add and commit files and push to Github" -ForegroundColor Gray
Write-Host "  3. Follow instructions at https://learn.microsoft.com//windows/powertoys/command-palette/publish-extension" -ForegroundColor Gray
Write-Host ""
Write-Host "Press any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
