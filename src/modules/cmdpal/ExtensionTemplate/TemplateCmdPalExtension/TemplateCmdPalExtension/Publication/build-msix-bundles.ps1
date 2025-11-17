# Build MSIX Bundles Script for CmdPal Extension
# This script automates the process of building MSIX packages for x64 and ARM64 architectures
# and creating an MSIX bundle for distribution
# Version: 1.0

#Requires -Version 5.1

# Enable strict mode for better error detection
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  CmdPal Extension MSIX Builder" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# Determine project root (parent of Publication folder)
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectName = Split-Path -Leaf $projectRoot

Write-Host "Project Configuration:" -ForegroundColor Yellow
Write-Host "  Project Root: $projectRoot" -ForegroundColor Gray
Write-Host "  Project Name: $projectName" -ForegroundColor Gray
Write-Host ""

# Verify we're in the right location
$csprojPath = Join-Path $projectRoot "$projectName.csproj"
$manifestPath = Join-Path $projectRoot "Package.appxmanifest"

if (-not (Test-Path $csprojPath)) {
    Write-Host "ERROR: Could not find .csproj file at: $csprojPath" -ForegroundColor Red
    Write-Host ""
    Write-Host "This script must be run from the Publication folder within your project." -ForegroundColor Yellow
    Write-Host "Expected structure:" -ForegroundColor Gray
    Write-Host "  <ProjectRoot>\" -ForegroundColor Gray
    Write-Host "    <ProjectName>.csproj" -ForegroundColor Gray
    Write-Host "    Publication\" -ForegroundColor Gray
    Write-Host "      build-msix-bundles.ps1 (this script)" -ForegroundColor Gray
    Write-Host ""
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

Write-Host "  [OK] Project files validated" -ForegroundColor Green
Write-Host ""

# Extract version from Package.appxmanifest
Write-Host "Reading package information..." -ForegroundColor Cyan
try {
    [xml]$manifest = Get-Content $manifestPath -ErrorAction Stop
    $packageName = $manifest.Package.Identity.Name
    $packageVersion = $manifest.Package.Identity.Version
    
    Write-Host "  Package Name: $packageName" -ForegroundColor White
    Write-Host "  Version: $packageVersion" -ForegroundColor White
    Write-Host ""
}
catch {
    Write-Host "ERROR: Could not read Package.appxmanifest: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Press any key to exit..." -ForegroundColor Gray
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

# Ask user what to build
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  Build Options" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "What would you like to build?" -ForegroundColor Yellow
Write-Host ""
Write-Host "  [1] x64 MSIX only" -ForegroundColor White
Write-Host "  [2] ARM64 MSIX only" -ForegroundColor White
Write-Host "  [3] Complete Bundle (x64 + ARM64 + Bundle file)" -ForegroundColor White
Write-Host ""
Write-Host "Enter your choice (1-3): " -ForegroundColor Yellow -NoNewline
$buildChoice = Read-Host
Write-Host ""

# Validate choice
if ($buildChoice -notmatch '^[1-3]$') {
    Write-Host "ERROR: Invalid choice. Please enter 1, 2, or 3." -ForegroundColor Red
    Write-Host "Press any key to exit..." -ForegroundColor Gray
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

# Determine what to build
$buildX64 = $false
$buildARM64 = $false
$createBundle = $false

switch ($buildChoice) {
    "1" { 
        $buildX64 = $true
        Write-Host "Building: x64 MSIX only" -ForegroundColor Cyan
    }
    "2" { 
        $buildARM64 = $true
        Write-Host "Building: ARM64 MSIX only" -ForegroundColor Cyan
    }
    "3" { 
        $buildX64 = $true
        $buildARM64 = $true
        $createBundle = $true
        Write-Host "Building: Complete Bundle (x64 + ARM64 + Bundle)" -ForegroundColor Cyan
    }
}
Write-Host ""

# Clean previous builds (optional)
Write-Host "Do you want to clean previous builds? (Y/N): " -ForegroundColor Yellow -NoNewline
$cleanBuilds = Read-Host
if ($cleanBuilds -match '^[Yy]') {
    Write-Host ""
    Write-Host "Cleaning previous builds..." -ForegroundColor Cyan
    
    $appPackagesPath = Join-Path $projectRoot "AppPackages"
    if (Test-Path $appPackagesPath) {
        try {
            Remove-Item $appPackagesPath -Recurse -Force -ErrorAction Stop
            Write-Host "  [OK] Cleaned AppPackages folder" -ForegroundColor Green
        }
        catch {
            Write-Host "  [WARNING] Could not clean AppPackages: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
    
    # Clean old bundles in microsoft-store-resources folder
    $microsoftStoreResourcesPath = Join-Path $PSScriptRoot "microsoft-store-resources"
    if (Test-Path $microsoftStoreResourcesPath) {
        $oldBundles = Get-ChildItem $microsoftStoreResourcesPath -Filter "*.msixbundle" -ErrorAction SilentlyContinue
        if ($oldBundles) {
            foreach ($bundle in $oldBundles) {
                try {
                    Remove-Item $bundle.FullName -Force -ErrorAction Stop
                    Write-Host "  [OK] Removed old bundle: $($bundle.Name)" -ForegroundColor Green
                }
                catch {
                    Write-Host "  [WARNING] Could not remove $($bundle.Name): $($_.Exception.Message)" -ForegroundColor Yellow
                }
            }
        }
    }
    
    Write-Host ""
}
else {
    Write-Host ""
}

# Track built files for summary
$builtFiles = @()
$x64Msix = $null
$arm64Msix = $null

# Build x64 MSIX (if requested)
if ($buildX64) {
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host "  Building x64 MSIX Package" -ForegroundColor Cyan
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Running: dotnet build (x64)..." -ForegroundColor Yellow
    Write-Host "This may take a few seconds" -ForegroundColor Yellow
    Write-Host ""

    Push-Location $projectRoot
    try {
        $buildOutput = & dotnet build --configuration Release -p:GenerateAppxPackageOnBuild=true -p:Platform=x64 -p:AppxPackageDir="AppPackages\x64\" 2>&1
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host ""
            Write-Host "ERROR: x64 build failed with exit code $LASTEXITCODE" -ForegroundColor Red
            Write-Host ""
            Write-Host "Build output:" -ForegroundColor Gray
            $buildOutput | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
            Write-Host ""
            Write-Host "Press any key to exit..." -ForegroundColor Gray
            Pop-Location
            $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
            exit 1
        }
        
        Write-Host "  [SUCCESS] x64 build completed" -ForegroundColor Green
        Write-Host ""
    }
    catch {
        Write-Host ""
        Write-Host "ERROR: x64 build failed: $($_.Exception.Message)" -ForegroundColor Red
        Pop-Location
        Write-Host "Press any key to exit..." -ForegroundColor Gray
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
        exit 1
    }
    finally {
        Pop-Location
    }
}

# Build ARM64 MSIX (if requested)
if ($buildARM64) {
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host "  Building ARM64 MSIX Package" -ForegroundColor Cyan
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Running: dotnet build (ARM64)..." -ForegroundColor Yellow
    Write-Host "This may take a few seconds" -ForegroundColor Yellow
    Write-Host ""

    Push-Location $projectRoot
    try {
        $buildOutput = & dotnet build --configuration Release -p:GenerateAppxPackageOnBuild=true -p:Platform=ARM64 -p:AppxPackageDir="AppPackages\ARM64\" 2>&1
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host ""
            Write-Host "ERROR: ARM64 build failed with exit code $LASTEXITCODE" -ForegroundColor Red
            Write-Host ""
            Write-Host "Build output:" -ForegroundColor Gray
            $buildOutput | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
            Write-Host ""
            Write-Host "Press any key to exit..." -ForegroundColor Gray
            Pop-Location
            $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
            exit 1
        }
        
        Write-Host "  [SUCCESS] ARM64 build completed" -ForegroundColor Green
        Write-Host ""
    }
    catch {
        Write-Host ""
        Write-Host "ERROR: ARM64 build failed: $($_.Exception.Message)" -ForegroundColor Red
        Pop-Location
        Write-Host "Press any key to exit..." -ForegroundColor Gray
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
        exit 1
    }
    finally {
        Pop-Location
    }
}

# Locate MSIX files (if bundle creation is needed or for summary)
if ($createBundle -or $buildX64 -or $buildARM64) {
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host "  Locating MSIX Files" -ForegroundColor Cyan
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host ""

    Push-Location $projectRoot
    try {
        $msixFiles = Get-ChildItem "AppPackages" -Recurse -Filter "*.msix" -ErrorAction SilentlyContinue
        
        if (-not $msixFiles) {
            # Try alternate location
            Write-Host "  MSIX files not found in AppPackages, checking bin folder..." -ForegroundColor Yellow
            $msixFiles = Get-ChildItem "bin" -Recurse -Filter "*.msix" -ErrorAction SilentlyContinue
        }
        
        if ($buildX64 -and $buildARM64 -and $createBundle -and (-not $msixFiles -or $msixFiles.Count -lt 2)) {
            Write-Host "ERROR: Could not find both x64 and ARM64 MSIX files" -ForegroundColor Red
            Write-Host ""
            Write-Host "Expected files:" -ForegroundColor Gray
            Write-Host "  - ${packageName}_${packageVersion}_x64.msix" -ForegroundColor Gray
            Write-Host "  - ${packageName}_${packageVersion}_arm64.msix" -ForegroundColor Gray
            Write-Host ""
            
            if ($msixFiles) {
                Write-Host "Found files:" -ForegroundColor Yellow
                $msixFiles | ForEach-Object { Write-Host "  - $($_.FullName)" -ForegroundColor Gray }
                Write-Host ""
            }
            
            Pop-Location
            Write-Host "Press any key to exit..." -ForegroundColor Gray
            $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
            exit 1
        }
        
        if ($msixFiles) {
            Write-Host "  Found MSIX files:" -ForegroundColor Green
            $msixFiles | ForEach-Object { 
                $relativePath = $_.FullName -replace [regex]::Escape($projectRoot + "\"), ""
                Write-Host "    [OK] $relativePath" -ForegroundColor White 
            }
            Write-Host ""
        }
        
        # Find specific x64 and ARM64 files
        if ($buildX64) {
            $x64Msix = $msixFiles | Where-Object { $_.Name -match "_x64\.msix$" } | Select-Object -First 1
            if ($x64Msix) {
                $builtFiles += $x64Msix.FullName
            }
        }
        
        if ($buildARM64) {
            $arm64Msix = $msixFiles | Where-Object { $_.Name -match "_arm64\.msix$" } | Select-Object -First 1
            if ($arm64Msix) {
                $builtFiles += $arm64Msix.FullName
            }
        }
        
        # Validate files for bundle creation
        if ($createBundle) {
            if (-not $x64Msix) {
                Write-Host "ERROR: Could not find x64 MSIX file" -ForegroundColor Red
                Pop-Location
                Write-Host "Press any key to exit..." -ForegroundColor Gray
                $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
                exit 1
            }
            
            if (-not $arm64Msix) {
                Write-Host "ERROR: Could not find ARM64 MSIX file" -ForegroundColor Red
                Pop-Location
                Write-Host "Press any key to exit..." -ForegroundColor Gray
                $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
                exit 1
            }
        }
    }
    finally {
        Pop-Location
    }
}

# Create bundle (if requested)
if ($createBundle) {
    # Update bundle_mapping.txt
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host "  Updating bundle_mapping.txt" -ForegroundColor Cyan
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host ""

    $microsoftStoreResourcesPath = Join-Path $PSScriptRoot "microsoft-store-resources"
    $bundleMappingPath = Join-Path $microsoftStoreResourcesPath "bundle_mapping.txt"

    # Ensure microsoft-store-resources directory exists
    if (-not (Test-Path $microsoftStoreResourcesPath)) {
        Write-Host "  Creating microsoft-store-resources folder..." -ForegroundColor Yellow
        try {
            New-Item -Path $microsoftStoreResourcesPath -ItemType Directory -Force | Out-Null
            Write-Host "  [OK] Folder created" -ForegroundColor Green
        }
        catch {
            Write-Host "  [ERROR] Could not create folder: $($_.Exception.Message)" -ForegroundColor Red
        }
    }

    # Get relative paths from project root
    $x64RelativePath = $x64Msix.FullName -replace [regex]::Escape($projectRoot + "\"), ""
    $arm64RelativePath = $arm64Msix.FullName -replace [regex]::Escape($projectRoot + "\"), ""

    # Create bundle mapping content
    $line1 = "`"$x64RelativePath`" `"$($x64Msix.Name)`""
    $line2 = "`"$arm64RelativePath`" `"$($arm64Msix.Name)`""
    $bundleMappingContent = "[Files]`r`n$line1`r`n$line2"

    try {
        Set-Content -Path $bundleMappingPath -Value $bundleMappingContent -NoNewline -ErrorAction Stop
        Write-Host "  [SUCCESS] bundle_mapping.txt updated" -ForegroundColor Green
        Write-Host ""
        Write-Host "  Content:" -ForegroundColor Gray
        Write-Host "    [Files]" -ForegroundColor DarkGray
        Write-Host ('    "' + $x64RelativePath + '" "' + $x64Msix.Name + '"') -ForegroundColor DarkGray
        Write-Host ('    "' + $arm64RelativePath + '" "' + $arm64Msix.Name + '"') -ForegroundColor DarkGray
        Write-Host ""
    }
    catch {
        Write-Host "  [ERROR] Could not update bundle_mapping.txt: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "  Continuing with bundle creation..." -ForegroundColor Yellow
        Write-Host ""
    }

    # Find makeappx.exe
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host "  Creating MSIX Bundle" -ForegroundColor Cyan
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host ""

    Write-Host "Locating makeappx.exe..." -ForegroundColor Yellow

    $arch = switch ($env:PROCESSOR_ARCHITECTURE) { 
        "AMD64" { "x64" } 
        "x86" { "x86" } 
        "ARM64" { "arm64" } 
        default { "x64" } 
    }

    Write-Host "  Detected architecture: $arch" -ForegroundColor Gray

    $makeappxPath = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin\*\$arch\makeappx.exe" -ErrorAction SilentlyContinue | 
        Sort-Object Name -Descending | 
        Select-Object -First 1

    if (-not $makeappxPath) {
        Write-Host ""
        Write-Host "ERROR: makeappx.exe not found" -ForegroundColor Red
        Write-Host ""
        Write-Host "makeappx.exe is part of the Windows SDK." -ForegroundColor Yellow
        Write-Host "Please install the Windows SDK from:" -ForegroundColor Yellow
        Write-Host "  https://developer.microsoft.com/windows/downloads/windows-sdk/" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Or ensure the Windows SDK is installed with Visual Studio." -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Press any key to exit..." -ForegroundColor Gray
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
        exit 1
    }

    Write-Host "  [OK] Found: $($makeappxPath.FullName)" -ForegroundColor Green
    Write-Host ""

    # Create bundle
    $bundleFileName = "${packageName}_${packageVersion}_Bundle.msixbundle"
    $bundleOutputPath = Join-Path $microsoftStoreResourcesPath $bundleFileName

    Write-Host "Creating bundle: $bundleFileName" -ForegroundColor Yellow
    Write-Host ""

    Push-Location $projectRoot
    try {
        # Use absolute path to bundle_mapping.txt
        $bundleMappingAbsolute = Join-Path $microsoftStoreResourcesPath "bundle_mapping.txt"
        
        # Verify the mapping file exists
        if (-not (Test-Path $bundleMappingAbsolute)) {
            Write-Host "ERROR: bundle_mapping.txt not found at: $bundleMappingAbsolute" -ForegroundColor Red
            Pop-Location
            Write-Host "Press any key to exit..." -ForegroundColor Gray
            $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
            exit 1
        }
        
        $makeappxArgs = @(
            "bundle",
            "/v",
            "/f", "`"$bundleMappingAbsolute`"",
            "/p", "`"$bundleOutputPath`""
        )
        
        Write-Host "  Running: makeappx bundle /v /f `"$bundleMappingAbsolute`" /p `"$bundleOutputPath`"" -ForegroundColor Gray
        Write-Host ""
        
        # Run makeappx with proper quoting
        $bundleOutput = & $makeappxPath.FullName bundle /v /f $bundleMappingAbsolute /p $bundleOutputPath 2>&1
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host ""
            Write-Host "ERROR: Bundle creation failed with exit code $LASTEXITCODE" -ForegroundColor Red
            Write-Host ""
            Write-Host "Output:" -ForegroundColor Gray
            $bundleOutput | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
            Write-Host ""
            Pop-Location
            Write-Host "Press any key to exit..." -ForegroundColor Gray
            $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
            exit 1
        }
        
        Write-Host "  [SUCCESS] Bundle created" -ForegroundColor Green
        Write-Host ""
    }
    catch {
        Write-Host ""
        Write-Host "ERROR: Bundle creation failed: $($_.Exception.Message)" -ForegroundColor Red
        Pop-Location
        Write-Host "Press any key to exit..." -ForegroundColor Gray
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
        exit 1
    }
    finally {
        Pop-Location
    }

    # Verify bundle was created
    if (-not (Test-Path $bundleOutputPath)) {
        Write-Host "ERROR: Bundle file was not created at expected location" -ForegroundColor Red
        Write-Host "  Expected: $bundleOutputPath" -ForegroundColor Gray
        Write-Host ""
        Write-Host "Press any key to exit..." -ForegroundColor Gray
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
        exit 1
    }

    # Add bundle to built files
    $builtFiles += $bundleOutputPath
}

# Final Summary
Write-Host "================================================================" -ForegroundColor Green
Write-Host "  BUILD COMPLETED SUCCESSFULLY!" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Green
Write-Host ""

# Display what was built
if ($buildChoice -eq "1") {
    Write-Host "Built: x64 MSIX Package" -ForegroundColor Cyan
}
elseif ($buildChoice -eq "2") {
    Write-Host "Built: ARM64 MSIX Package" -ForegroundColor Cyan
}
elseif ($buildChoice -eq "3") {
    Write-Host "Built: Complete Bundle (x64 + ARM64 + Bundle file)" -ForegroundColor Cyan
}
Write-Host ""

Write-Host "Package Information:" -ForegroundColor Yellow
Write-Host "  Name:    $packageName" -ForegroundColor White
Write-Host "  Version: $packageVersion" -ForegroundColor White
Write-Host ""

# Display built files
Write-Host "Built Files:" -ForegroundColor Yellow
if ($x64Msix) {
    $x64Size = "{0:N2} MB" -f ((Get-Item $x64Msix.FullName).Length / 1MB)
    Write-Host "  [x64 MSIX]" -ForegroundColor Cyan
    Write-Host "    Location: $($x64Msix.FullName)" -ForegroundColor White
    Write-Host "    Size:     $x64Size" -ForegroundColor White
    Write-Host ""
}

if ($arm64Msix) {
    $arm64Size = "{0:N2} MB" -f ((Get-Item $arm64Msix.FullName).Length / 1MB)
    Write-Host "  [ARM64 MSIX]" -ForegroundColor Cyan
    Write-Host "    Location: $($arm64Msix.FullName)" -ForegroundColor White
    Write-Host "    Size:     $arm64Size" -ForegroundColor White
    Write-Host ""
}

if ($createBundle -and (Test-Path $bundleOutputPath)) {
    $bundleSize = "{0:N2} MB" -f ((Get-Item $bundleOutputPath).Length / 1MB)
    Write-Host "  [MSIX Bundle]" -ForegroundColor Cyan
    Write-Host "    Location: $bundleOutputPath" -ForegroundColor White
    Write-Host "    Size:     $bundleSize" -ForegroundColor White
    Write-Host ""
}

Write-Host "================================================================" -ForegroundColor Green
Write-Host ""

# Display appropriate next steps based on what was built
Write-Host "Next Steps:" -ForegroundColor Cyan
if ($createBundle) {
    Write-Host "  1. Test the bundle by installing it locally" -ForegroundColor Gray
    Write-Host "  2. Upload the bundle to Microsoft Store Partner Center" -ForegroundColor Gray
    Write-Host "  3. Or distribute via other channels" -ForegroundColor Gray
}
else {
    Write-Host "  1. Test the MSIX package by installing it locally" -ForegroundColor Gray
    if ($buildX64) {
        Write-Host "  2. Build ARM64 package (option 2) or complete bundle (option 3)" -ForegroundColor Gray
    }
    else {
        Write-Host "  2. Build x64 package (option 1) or complete bundle (option 3)" -ForegroundColor Gray
    }
    Write-Host "  3. Or distribute this individual package" -ForegroundColor Gray
}
Write-Host ""
Write-Host "Press any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
