# WinGet Publishing Guide

Complete step-by-step guide for publishing your Command Palette extension to WinGet for `winget install` discovery and installation.

## Why WinGet?

Publishing to WinGet enables:

- Users to install via `winget install YourPublisher.YourExtension`
- Discovery directly inside Command Palette's built-in browse experience
- Automatic update detection via WinGet manifests

## Step 1: Prepare the Project for Unpackaged Distribution

WinGet distribution uses an unpackaged (EXE-based) build instead of MSIX.

### Update `.csproj`

Remove any existing `<PublishProfile>` property and add unpackaged mode:

```xml
<PropertyGroup>
  <!-- Remove or comment out this line if present: -->
  <!-- <PublishProfile>win-$(Platform)</PublishProfile> -->

  <!-- Add this for unpackaged distribution: -->
  <WindowsPackageType>None</WindowsPackageType>
</PropertyGroup>
```

### Note Your CLSID

Find the `[Guid("...")]` attribute in your main `.cs` file (e.g., `SampleExtension.cs`):

```csharp
[Guid("YOUR-GUID-HERE")]
public sealed partial class SampleExtension : IExtension
```

You'll need this exact GUID for the installer script. It must match across all files.

## Step 2: Create Installer Scripts

### Inno Setup Script: `setup-template.iss`

Create this file in your project root. Replace all `TODO` placeholders with your values:

```iss
; Inno Setup script for Command Palette extension

#define MyAppName "TODO_YOUR_EXTENSION_NAME"
#define MyAppVersion "TODO_YOUR_VERSION"
#define MyAppPublisher "TODO_YOUR_PUBLISHER_NAME"
#define MyAppURL "TODO_YOUR_PROJECT_URL"
#define MyAppCLSID "TODO_YOUR_CLSID_WITH_BRACES"
; Example CLSID: {12345678-1234-1234-1234-123456789012}

[Setup]
AppId={#MyAppCLSID}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
OutputBaseFilename={#MyAppName}_{#MyAppVersion}_{#SetupSetting("ArchitecturesAllowed")}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
OutputDir=Installer

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
; Register the COM server for Command Palette discovery
Root: HKCU; Subkey: "Software\Classes\CLSID\{#MyAppCLSID}"; ValueType: string; ValueName: ""; ValueData: "{#MyAppName}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\CLSID\{#MyAppCLSID}\InprocServer32"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppName}.dll"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\CLSID\{#MyAppCLSID}\InprocServer32"; ValueType: string; ValueName: "ThreadingModel"; ValueData: "Both"; Flags: uninsdeletekey

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
```

> **Important:** The `AppId` must use your CLSID wrapped in braces. The registry entries register your extension's COM server so Command Palette can discover it.

### Build Script: `build-exe.ps1`

Create this PowerShell script in your project root:

```powershell
<#
.SYNOPSIS
    Builds EXE installers for x64 and ARM64 using dotnet publish and Inno Setup.
.DESCRIPTION
    Publishes the project for both architectures, then runs Inno Setup to create
    EXE installers suitable for WinGet submission.
#>

param(
    [string]$Configuration = "Release",
    [string]$Version = "0.0.1"
)

$ErrorActionPreference = "Stop"

$projectName = (Get-ChildItem -Filter "*.csproj" | Select-Object -First 1).BaseName
if (-not $projectName) {
    Write-Error "No .csproj file found in the current directory."
    exit 1
}

$architectures = @("x64", "arm64")

foreach ($arch in $architectures) {
    Write-Host "`n=== Building $arch ===" -ForegroundColor Cyan

    # Publish
    Write-Host "Publishing for $arch..."
    dotnet publish -c $Configuration -r "win-$arch" -o "publish" --self-contained=false
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet publish failed for $arch"
        exit 1
    }

    # Create installer
    Write-Host "Creating installer for $arch..."
    $issFile = "setup-template.iss"
    if (-not (Test-Path $issFile)) {
        Write-Error "Inno Setup script not found: $issFile"
        exit 1
    }

    $archFlag = if ($arch -eq "arm64") { "arm64" } else { "x64" }
    & "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" `
        /DMyAppVersion="$Version" `
        /DArchitecturesAllowed="$archFlag" `
        $issFile

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Inno Setup failed for $arch"
        exit 1
    }

    # Clean publish directory for next architecture
    Remove-Item -Recurse -Force "publish" -ErrorAction SilentlyContinue

    Write-Host "=== $arch complete ===" -ForegroundColor Green
}

Write-Host "`nInstallers created in the 'Installer' directory:" -ForegroundColor Cyan
Get-ChildItem -Path "Installer" -Filter "*.exe" | ForEach-Object { Write-Host "  $_" }
```

## Step 3: Build EXE Installers

Run the build script from your project directory:

```powershell
.\build-exe.ps1
```

This produces two EXE files in the `Installer` directory:

```
Installer\YourExtension_0.0.1_x64.exe
Installer\YourExtension_0.0.1_arm64.exe
```

Verify both installers work by running them locally and confirming your extension appears in Command Palette.

## Step 4: Create a GitHub Release

Tag your repository with the version and create a release with the EXE files:

```powershell
# Tag the release
git tag -a v0.0.1 -m "Release v0.0.1"
git push origin v0.0.1

# Create release and upload assets (requires GitHub CLI)
gh release create v0.0.1 `
    "Installer\YourExtension_0.0.1_x64.exe" `
    "Installer\YourExtension_0.0.1_arm64.exe" `
    --title "v0.0.1" `
    --notes "Initial release of YourExtension for Command Palette."
```

After creating the release, copy the download URLs for both EXE files — you'll need them for the WinGet submission.

## Step 5: Submit to WinGet

Use `wingetcreate` to generate a WinGet manifest and submit a pull request:

```powershell
wingetcreate new "<URL_TO_x64.exe>" "<URL_TO_arm64.exe>"
```

`wingetcreate` will interactively prompt you for:

| Prompt | Example Value |
|--------|---------------|
| **PackageIdentifier** | `YourPublisher.YourExtension` |
| **PackageVersion** | `0.0.1` |
| **PackageLocale** | `en-US` |
| **Publisher** | `Your Name` |
| **PackageName** | `YourExtension for Command Palette` |
| **License** | `MIT` |
| **ShortDescription** | `A Command Palette extension that does X` |

After answering all prompts, `wingetcreate` will create a PR against the [winget-pkgs](https://github.com/microsoft/winget-pkgs) repository.

## Step 6: Add the Command Palette Tag (CRITICAL)

> **This step is required for your extension to appear in Command Palette's browse experience.**

After `wingetcreate` generates the manifest files, you **must** edit each `.locale.*.yaml` file to add the Command Palette tag.

In every locale YAML file (e.g., `YourPublisher.YourExtension.locale.en-US.yaml`), add:

```yaml
Tags:
- windows-commandpalette-extension
```

Example of a complete locale file with the tag:

```yaml
# yaml-language-server: $schema=https://aka.ms/winget-manifest.defaultLocale.1.6.0.schema.json
PackageIdentifier: YourPublisher.YourExtension
PackageVersion: 0.0.1
PackageLocale: en-US
Publisher: Your Name
PackageName: YourExtension for Command Palette
License: MIT
ShortDescription: A Command Palette extension that does X
Tags:
- windows-commandpalette-extension
ManifestType: defaultLocale
ManifestVersion: 1.6.0
```

Without this tag, Command Palette will not discover your extension in its browse experience.

## Step 7: Ensure WindowsAppSdk Dependency

Your WinGet manifest must declare a dependency on the Windows App SDK so it gets installed automatically. In the `installer.yaml` manifest file, add:

```yaml
Dependencies:
  PackageDependencies:
  - PackageIdentifier: Microsoft.WindowsAppRuntime.1.7
    MinimumVersion: 7001.632.252.0
```

> **Note:** Update the version number to match the Windows App SDK version your project targets. Check your `.csproj` for the `WindowsAppSDK` package version.

## Step 8: GitHub Actions Automation (Optional)

Automate your build, release, and WinGet submission process with GitHub Actions.

### Release Workflow: `.github/workflows/release-extension.yml`

```yaml
name: Release Extension

on:
  push:
    tags:
      - 'v*'

permissions:
  contents: write

env:
  PROJECT_NAME: YourExtension
  DOTNET_VERSION: '9.0.x'

jobs:
  build:
    strategy:
      matrix:
        arch: [x64, arm64]
    runs-on: windows-latest

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Install Inno Setup
        run: choco install innosetup -y --no-progress

      - name: Detect version
        id: version
        run: |
          $tag = "${{ github.ref_name }}" -replace '^v', ''
          echo "VERSION=$tag" >> $env:GITHUB_OUTPUT

      - name: Publish
        run: |
          dotnet publish -c Release -r win-${{ matrix.arch }} -o publish --self-contained=false

      - name: Create installer
        run: |
          & "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" `
            /DMyAppVersion="${{ steps.version.outputs.VERSION }}" `
            /DArchitecturesAllowed="${{ matrix.arch }}" `
            setup-template.iss

      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: installer-${{ matrix.arch }}
          path: Installer/*.exe

  release:
    needs: build
    runs-on: ubuntu-latest

    steps:
      - name: Download all artifacts
        uses: actions/download-artifact@v4
        with:
          path: artifacts
          merge-multiple: true

      - name: Create GitHub Release
        uses: softprops/action-gh-release@v2
        with:
          files: artifacts/*.exe
          generate_release_notes: true

  winget-update:
    needs: release
    runs-on: windows-latest

    steps:
      - name: Detect version
        id: version
        run: |
          $tag = "${{ github.ref_name }}" -replace '^v', ''
          echo "VERSION=$tag" >> $env:GITHUB_OUTPUT

      - name: Update WinGet manifest
        run: |
          $baseUrl = "https://github.com/${{ github.repository }}/releases/download/${{ github.ref_name }}"
          wingetcreate update YourPublisher.YourExtension `
            --version ${{ steps.version.outputs.VERSION }} `
            --urls "$baseUrl/${{ env.PROJECT_NAME }}_${{ steps.version.outputs.VERSION }}_x64.exe" "$baseUrl/${{ env.PROJECT_NAME }}_${{ steps.version.outputs.VERSION }}_arm64.exe" `
            --submit `
            --token ${{ secrets.WINGET_PAT }}
```

### Required Secrets

| Secret | Description |
|--------|-------------|
| `WINGET_PAT` | GitHub Personal Access Token with `public_repo` scope, used by `wingetcreate` to submit PRs to `microsoft/winget-pkgs` |

### How It Works

1. **Push a version tag** (e.g., `git tag v0.0.2 && git push origin v0.0.2`)
2. **Build job** runs in parallel for x64 and ARM64, creating EXE installers
3. **Release job** creates a GitHub Release and uploads the EXE files
4. **WinGet update job** automatically submits an updated manifest to `winget-pkgs`

> **Note:** The `winget-update` job uses `wingetcreate update` (not `new`) because it assumes you've already submitted your initial manifest manually. For the first submission, follow Steps 5–7 above.

## Validation Checklist

Before submitting to WinGet, verify:

- [ ] `.csproj` has `<WindowsPackageType>None</WindowsPackageType>` set
- [ ] CLSID in `setup-template.iss` matches the `[Guid("...")]` in your main `.cs` file
- [ ] Both x64 and ARM64 EXE installers build successfully
- [ ] Installer registers the COM server correctly (check `HKCU\Software\Classes\CLSID\{your-clsid}`)
- [ ] Extension appears in Command Palette after installing via EXE
- [ ] Extension is removed from Command Palette after uninstalling
- [ ] GitHub Release contains both EXE files with correct download URLs
- [ ] WinGet manifest includes `windows-commandpalette-extension` tag
- [ ] WinGet manifest includes `WindowsAppRuntime` dependency
- [ ] `winget validate` passes on all manifest files

## Updating Your Extension on WinGet

For subsequent releases:

```powershell
wingetcreate update YourPublisher.YourExtension `
    --version "0.0.2" `
    --urls "<URL_TO_NEW_x64.exe>" "<URL_TO_NEW_arm64.exe>" `
    --submit
```

Or simply push a new version tag if you've set up the GitHub Actions workflow above.

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Extension not appearing in CmdPal browse | Verify the `windows-commandpalette-extension` tag is in your locale YAML |
| COM registration fails | Check that the CLSID matches exactly and registry paths are correct |
| `wingetcreate` validation errors | Run `winget validate --manifest <path>` and fix reported issues |
| Installer doesn't run silently | Add `/VERYSILENT /SUPPRESSMSGBOXES` flags for silent install support |
| Missing WindowsAppSdk at runtime | Ensure the `PackageDependencies` section is in your installer manifest |
