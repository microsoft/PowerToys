# Local PowerToys Extension Development

This guide is for iterating on `src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.PowerToys/Microsoft.CmdPal.Ext.PowerToys.csproj`.

The extension is registered through the shared sparse package defined in `src/PackageIdentity/AppxManifest.xml`. That manifest declares `Microsoft.CmdPal.Ext.PowerToys.exe` at the sparse package root, so the sparse package and the extension must be built for the same platform and configuration, for example `x64\Debug`.

## Local development loop

1. Build `src/PackageIdentity/PackageIdentity.vcxproj`.

   This creates `PowerToysSparse.msix` in the repo output root for the selected platform and configuration, and prints the `Add-AppxPackage` command you should run next.

2. Trust the development certificate before running `Add-AppxPackage`.

   The `PackageIdentity` build creates or reuses `src/PackageIdentity/.user/PowerToysSparse.certificate.sample.cer`.

   Import it into `CurrentUser\TrustedPeople`:

   ```powershell
   $repoRoot = "C:/git/PowerToys"
   Import-Certificate -FilePath "$repoRoot/src/PackageIdentity/.user/PowerToysSparse.certificate.sample.cer" -CertStoreLocation Cert:\CurrentUser\TrustedPeople
   ```

   If Windows still reports a trust failure such as `0x800B0109`, also import the same certificate into `Cert:\CurrentUser\TrustedRoot`.

3. Run the `Add-AppxPackage` command printed by the `PackageIdentity` build.

   That registers `Microsoft.PowerToys.SparseApp` as a sparse package and points it at the matching output root through `-ExternalLocation`.

   The command will look like this:

   ```powershell
   Add-AppxPackage -Path "<repo>\<Platform>\<Configuration>\PowerToysSparse.msix" -ExternalLocation "<repo>\<Platform>\<Configuration>"
   ```

4. Build `src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.PowerToys/Microsoft.CmdPal.Ext.PowerToys.csproj` in the same platform and configuration.

   This project writes `Microsoft.CmdPal.Ext.PowerToys.exe` directly into the sparse package root, such as `x64\Debug` or `ARM64\Debug`. That matches the `Executable="Microsoft.CmdPal.Ext.PowerToys.exe"` entry in `src/PackageIdentity/AppxManifest.xml`.

5. Restart Command Palette.

   Close any running CmdPal instance and launch it again so it reloads app extensions and picks up the rebuilt `Microsoft.CmdPal.Ext.PowerToys` binaries.

## When to repeat each step

- Rebuild and re-register `PackageIdentity` when the sparse package manifest changes, the signing certificate changes, or you switch to a different output root such as `ARM64\Debug`.
- For normal code changes in `Microsoft.CmdPal.Ext.PowerToys`, rebuilding the extension project and restarting CmdPal is enough.
