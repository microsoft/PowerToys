# Microsoft Store Publishing Guide

Complete step-by-step guide for publishing your Command Palette extension to the Microsoft Store.

## Step 1: Set Up Microsoft Store

1. Go to [Partner Center](https://partner.microsoft.com/dashboard/home)
2. Navigate to **Apps and Games** → **New product** → **MSIX or PWA app**
3. Reserve your app name (e.g., `My Extension for Command Palette`)
4. Once created, go to **Product Management** → **Product Identity**
5. Copy these three values — you'll need them in the next step:

| Partner Center Field | Where It Goes |
|---------------------|---------------|
| **Package/Identity/Name** | `Package.appxmanifest` → `Identity Name` and `.csproj` → `AppxPackageIdentityName` |
| **Package/Identity/Publisher** | `Package.appxmanifest` → `Identity Publisher` and `.csproj` → `AppxPackagePublisher` |
| **Package/Properties/PublisherDisplayName** | `Package.appxmanifest` → `Properties PublisherDisplayName` |

## Step 2: Prepare the Extension

### Update `Package.appxmanifest`

Replace the placeholder identity values with your Partner Center values:

```xml
<Identity
  Name="YOUR_PACKAGE_IDENTITY_NAME_HERE"
  Publisher="YOUR_PACKAGE_IDENTITY_PUBLISHER_HERE"
  Version="0.0.1.0" />
```

And update the publisher display name:

```xml
<Properties>
  <DisplayName>Your Extension Name</DisplayName>
  <PublisherDisplayName>YOUR_PUBLISHER_DISPLAY_NAME_HERE</PublisherDisplayName>
  <!-- ... -->
</Properties>
```

### Update `.csproj`

Add or update the following properties in your `.csproj` file:

```xml
<PropertyGroup>
  <AppxPackageIdentityName>YOUR_PACKAGE_IDENTITY_NAME_HERE</AppxPackageIdentityName>
  <AppxPackagePublisher>YOUR_PACKAGE_IDENTITY_PUBLISHER_HERE</AppxPackagePublisher>
  <AppxPackageVersion>0.0.1.0</AppxPackageVersion>
</PropertyGroup>
```

### Update Image Assets ItemGroup

Ensure all image assets are included in the package by updating the `ItemGroup`:

```xml
<ItemGroup>
  <Content Include="Assets\**\*.png" />
</ItemGroup>
```

> **Tip:** The `Assets` folder should contain your Store logos and extension icons at the required sizes (44x44, 150x150, etc.). You can generate these from a single high-resolution image.

## Step 3: Build MSIX Packages

Build for both x64 and ARM64 architectures:

```powershell
# x64 build
dotnet build --configuration Release -p:GenerateAppxPackageOnBuild=true -p:Platform=x64 -p:AppxPackageDir="AppPackages\x64\"

# ARM64 build
dotnet build --configuration Release -p:GenerateAppxPackageOnBuild=true -p:Platform=ARM64 -p:AppxPackageDir="AppPackages\ARM64\"
```

Verify the MSIX files were created:

```powershell
dir AppPackages -Recurse -Filter "*.msix"
```

You should see two `.msix` files, one for each architecture.

## Step 4: Create MSIX Bundle

### Create the bundle mapping file

Create a file named `bundle_mapping.txt` that maps each MSIX to its architecture:

```text
[Files]
"AppPackages\x64\YourExtension_0.0.1.0_x64\YourExtension_0.0.1.0_x64.msix" "YourExtension_0.0.1.0_x64.msix"
"AppPackages\ARM64\YourExtension_0.0.1.0_ARM64\YourExtension_0.0.1.0_ARM64.msix" "YourExtension_0.0.1.0_ARM64.msix"
```

> **Note:** Update the paths and filenames to match your actual build output. Check the `AppPackages` directory structure after building.

### Run makeappx

```powershell
makeappx bundle /f bundle_mapping.txt /p YourExtension_0.0.1.0_Bundle.msixbundle
```

> **Tip:** `makeappx.exe` is included with the Windows SDK. If it's not in your PATH, find it at:
> `C:\Program Files (x86)\Windows Kits\10\bin\<version>\x64\makeappx.exe`

## Step 5: Submit to Partner Center

1. Go to [Partner Center](https://partner.microsoft.com/dashboard/home)
2. Navigate to your app → **Start a new submission**
3. In **Packages**, upload your `.msixbundle` file
4. In **Store Listings** → **Description**, include a note like:

   > `YourExtension` integrates with the Windows Command Palette to provide [describe your extension's functionality]. Requires PowerToys with Command Palette enabled.

5. In **Notes for certification**, add testing instructions:

   > This extension requires Microsoft PowerToys (available from the Microsoft Store or https://github.com/microsoft/PowerToys) with the Command Palette feature enabled. To test:
   > 1. Install PowerToys and enable Command Palette
   > 2. Install this extension
   > 3. Open Command Palette (Win+Alt+Space by default)
   > 4. Search for [your extension's commands]

6. Set **Availability** and pricing as appropriate
7. Click **Submit for certification**

Certification typically takes 1–3 business days.

## Validation Checklist

Before submitting, verify:

- [ ] Partner Center identity values match exactly in both `Package.appxmanifest` and `.csproj`
- [ ] `AppxPackageVersion` is set correctly and incremented from any previous submission
- [ ] Both x64 and ARM64 MSIX files are built successfully
- [ ] MSIX bundle is created without errors
- [ ] Extension installs and runs correctly from the MSIX package locally
- [ ] Store listing includes clear description mentioning Command Palette integration
- [ ] Testing instructions mention the PowerToys/Command Palette prerequisite
- [ ] All required Store logos and screenshots are provided
- [ ] Privacy policy URL is set (if your extension accesses network or user data)

## Store-Only Discovery Limitations

> **Important:** Command Palette cannot currently search for extensions published only to the Microsoft Store via its built-in browse experience. Users can find Store-published extensions through:
>
> - Direct Store link shared by the developer
> - The Store's extension tag URL:
>   ```
>   ms-windows-store://assoc/?Tags=AppExtension-com.microsoft.commandpalette
>   ```
> - Searching the Store app directly
>
> For discoverability within Command Palette's browse experience, also publish to WinGet.
> See [winget-publishing.md](winget-publishing.md) for details.

## Updating Your Extension

To publish an update:

1. Increment the version in `.csproj` (`AppxPackageVersion`) and `Package.appxmanifest`
2. Rebuild MSIX packages for both architectures
3. Recreate the MSIX bundle with updated filenames
4. Create a new submission in Partner Center and upload the new bundle
5. Submit for certification

The Store will automatically update users who have installed your extension.
