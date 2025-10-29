# PowerToys sparse package identity

This document describes how to build, sign, register, and consume the shared sparse MSIX package that grants package identity to select Win32 components of PowerToys.

## Package overview

The sparse package lives under `src/PackageIdentity`. It produces a payload-free MSIX whose `Identity` matches `Microsoft.PowerToys.SparseApp`. The manifest contains one entry per Win32 surface that should run with identity (for example Settings, PowerOCR, Image Resizer).

> The MSIX contains only metadata. When the package is registered you must point `-ExternalLocation` to the output folder that hosts the Win32 binaries (for example `x64\Release`).

## Building the sparse package locally

Two options are available:

- Build the utility project from Visual Studio: `PackageIdentity.vcxproj` defines a `GenerateSparsePackage` target that runs before `PrepareForBuild` and invokes the helper script automatically.
- Invoke the helper script directly from PowerShell:

```powershell
$repoRoot = "C:/git/PowerToys"
pwsh "$repoRoot/src/PackageIdentity/BuildSparsePackage.ps1" -Platform x64 -Configuration Release
```

Supported switches:

- `-Clean` removes previous `bin`/`obj` outputs and uninstalls existing installation.
- `-ForceCert` regenerates the local dev certificate (.pfx/.cer/.pwd/.thumbprint) under `src/PackageIdentity/.user`.
- `-NoSign` skips signing. The MSIX still builds but must be signed before deployment.
- `-CIBuild` (or setting `$env:CIBuild = 'true'`) keeps the manifest publisher intact and skips the local cert substitution.

The script determines the proper `makeappx.exe` for the host build machine (x64 on typical developer boxes) and creates `PowerToysSparse.msix` in `{repo}\<Platform>\<Configuration>`.

> After packaging finishes, the helper also emits `src/PackageIdentity/.user/PowerToysSparse.publisher.txt`. This file mirrors the publisher string Windows will see once the sparse package is registered, which downstream projects can read to stay in sync when generating their own manifests.

## Local signing basics

When `-NoSign` is not used the script generates (or reuses) a development certificate and signs the package via `signtool.exe`:

1. Artifacts are stored in `src/PackageIdentity/.user/PowerToysSparse.certificate.sample.*` (`.cer` and `.thumbprint`).
2. Install the `.cer` into `CurrentUser` → `TrustedPeople` (and `TrustedRoot`, if necessary) so Windows trusts the signature:

   ```powershell
   $repoRoot = "C:/git/PowerToys"
   Import-Certificate -FilePath "$repoRoot/src/PackageIdentity/.user/PowerToysSparse.certificate.sample.cer" -CertStoreLocation Cert:\CurrentUser\TrustedPeople
   ```

3. The private key stays in the current user's personal certificate store.

## Registering or unregistering the package

After `PowerToysSparse.msix` is generated:

```powershell
# First time registration
$repoRoot = "C:/git/PowerToys"
$outputRoot = Join-Path $repoRoot "x64/Release"
Add-AppxPackage -Path (Join-Path $outputRoot "PowerToysSparse.msix") -ExternalLocation $outputRoot

# Re-register after manifest tweaks only
Add-AppxPackage -Register (Join-Path $repoRoot "src/PackageIdentity/AppxManifest.xml") -ExternalLocation $outputRoot -ForceApplicationShutdown

# Remove the sparse identity
Get-AppxPackage -Name Microsoft.PowerToys.SparseApp | Remove-AppxPackage
```

`-ExternalLocation` should match the output folder that contains the Win32 executables declared in the manifest. Re-run registration whenever the manifest or executable layout changes.

## CI-specific guidance

- Pass `-CIBuild` to `BuildSparsePackage.ps1` (or build with `msbuild PackageIdentity.vcxproj /p:CIBuild=true`). This prevents the script from rewriting the manifest publisher to the local dev certificate subject.
- The project automatically adds `-NoSign` only when `$(CIBuild)` is `true`. Local Debug and Release builds are signed with the development certificate.
- Make sure the agent trusts whichever certificate signs the package. If the package remains unsigned (`-NoSign`) it cannot be installed on test machines until it is signed.

## Consuming the identity from other components

1. Add a new `<Application>` entry inside `src/PackageIdentity/AppxManifest.xml`. Use a unique `Id` (for example `PowerToys.MyModuleUI`) and set `Executable` to the Win32 binary relative to the `-ExternalLocation` root.
2. Ensure the binary is copied into the platform/configuration output folder (`x64\Release`, `ARM64\Debug`, etc.) so the sparse package can locate it.
3. Embed a sparse identity manifest in the Win32 binary so it binds to the MSIX identity at runtime. The manifest must declare an `<msix>` element with `packageName="Microsoft.PowerToys.SparseApp"`, `applicationId` matching the `<Application Id>`, and a `publisher` that matches the sparse package. Keep the manifest’s publisher in sync with `src/PackageIdentity/.user/PowerToysSparse.publisher.txt` (emitted by `BuildSparsePackage.ps1`). See `src/modules/imageresizer/ui/ImageResizerUI.csproj` for an example that points `ApplicationManifest` to `ImageResizerUI.dev.manifest` for local builds and switches to `ImageResizerUI.prod.manifest` when `$(CIBuild)` is `true`.
4. Register or re-register the sparse package so Windows learns about the new application Id.
5. To launch the Win32 surface with identity, use the `shell:AppsFolder` activation form (for example: `shell:AppsFolder\Microsoft.PowerToys.SparseApp_<PackageFamilyName>!PowerToys.MyModuleUI`) or activate it via `IApplicationActivationManager::ActivateApplication` using the same AppUserModelID.

   - For locally built packages, resolve the `<PackageFamilyName>` with `Get-AppxPackage -Name Microsoft.PowerToys.SparseApp | Select-Object -ExpandProperty PackageFamilyName`.
   - Store-distributed builds use `Microsoft.PowerToys.SparseApp_8wekyb3d8bbwe`. Local developer builds created by this script typically use a different family name derived from the dev certificate.

6. Context menu handlers or other launchers should fall back to the unpackaged executable path for environments where the sparse package is not present.

## Troubleshooting tips

- `Program 'makeappx.exe' failed to run`: make sure you are running an x64 PowerShell host. The script now chooses the appropriate makeappx automatically; update your repo if the log still points to an ARM64 binary.
- `HRESULT 0x800B0109 (trust failure)`: install the development certificate into both `TrustedPeople` and `TrustedRoot` stores for the current user.
- Stale registration: remove the package with `Remove-AppxPackage` and re-run the script with `-Clean` to rebuild from scratch.
