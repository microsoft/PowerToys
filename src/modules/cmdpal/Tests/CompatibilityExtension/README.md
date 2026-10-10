# Published SDK compatibility extensions

Independent Command Palette extensions exercise the same six UI scenarios while using different published `Microsoft.CommandPalette.Extensions` packages restored from the repository's `PowerToysPublicDependencies` feed. Each package contains the Toolkit and the SDK; there are no references to the in-repo SDK or host projects.

| Project | Pinned SDK | Command Palette command |
| --- | --- | --- |
| Sdk010 | 0.1.0 | Compatibility SDK 0.1.0 |
| Sdk020 | 0.2.0 | Compatibility SDK 0.2.0 |
| Sdk050 | 0.5.250829002 | Compatibility SDK 0.5.250829002 |
| Sdk090 | 0.9.260303001 | Compatibility SDK 0.9.260303001 |
| Sdk012 | 0.12.260812002 | Compatibility SDK 0.12.260812002 |

SDK 0.2.0 and 0.12.260812002 require access to unsaved upstream packages until those versions are saved in the internal feed. An HTTP 401 for either version means feed authentication or upstream package saving is still required.

Package identities, COM class IDs, executables and provider IDs are distinct, so the extensions can be installed together. The fixtures are deliberately outside `PowerToys.slnx` and the product installer. Open `CompatibilityExtension.slnx` to work on them.

## Build

Use the normal PowerToys Visual Studio build prerequisites and .NET 10 SDK. From this directory:

```powershell
& ..\..\..\..\..\tools\build\build.ps1 -Path . -Platform x64 -Configuration Debug -ExtraArgs '/m:1', '/nr:false'
.\Test-Fixtures.ps1 -Platform x64 -Configuration Debug
```

Use `ARM64` for an ARM64 machine. Do not run the fixture checks for another architecture on the build machine. Builds generate unsigned MSIX packages under `AppPackages/<platform>/<configuration>/<project>/` and development layouts under each project's `bin` directory. Both .NET and Windows App SDK are self-contained. Native AOT is not enabled; these fixtures isolate SDK compatibility, not compilation modes.

Local `Directory.Build.*`, `Directory.Packages.props` and `packages.lock.json` files isolate and pin the dependencies. Package sources are inherited from the repository root `nuget.config`. The only remote restore source is `PowerToysPublicDependencies`; the .NET SDK can also add its local framework packs. Do not update an existing baseline when the host SDK changes. Add a new project and identity for a new baseline. To intentionally refresh supporting dependencies, restore once with `/p:RestoreLockedMode=false` and review all lock-file changes.

## Install for UI testing

With Developer Mode enabled, deploy each project using Visual Studio's Deploy command, or register its generated development layout:

```powershell
Add-AppxPackage -Register '<project>\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AppxManifest.xml'
```

Use the generated `AppxManifest.xml`, not the source `Package.appxmanifest`. Keep the registered build output in place. Reload extensions in Command Palette, then search for each `Compatibility SDK ...` command. Registration is an explicit manual step; building and running `Test-Fixtures.ps1` do not install anything.

To remove only these fixtures:

```powershell
Get-AppxPackage -Name 'Microsoft.PowerToys.CmdPal.Compatibility.*' | Remove-AppxPackage
```

## UI regression checklist

Run each row for every extension in the solution against the candidate host. Compare the same built extension packages against a known-good host when investigating a regression. Record the host version, SDK label, theme, scale, and failing step. Preserve the fixture binaries during the comparison.

| Page | Steps and expected result |
| --- | --- |
| 01 - List and actions | Navigate with arrow keys. Enter increments `Counter: 0` to `Counter: 1` without closing the page. Open the context menu; Reset and Ctrl+R restore zero. Show toast displays `Compatibility toast`. Open the confirmation; Cancel preserves the count, Reset clears it. Nested navigation opens Markdown and Back returns. Long text and tags do not overlap. |
| 02 - Search and filters | All shows five items. `app` shows Apple. `zzz` shows the no-match title, subtitle and icon. Clear the query; Vegetables shows Carrot and Potato, Fruit shows three items, All restores five. Repeat search/filter changes and return to the home page; the filter must disappear. |
| 03 - Details | Alpha shows formatted body, SDK metadata and tags. Beta replaces the body and clears Alpha metadata. Gamma has no stale Alpha or Beta details. Repeat using keyboard and mouse selection. |
| 04 - Markdown | Headings, emphasis, bullets, numbered list, table, code block and quote render. The Update content context action increments the revision in place. |
| 05 - Form | Tab through Name, Color, Enabled and Submit. Change Name to Grace, Color to Green, and turn Enabled off. Submit keeps the page open and displays `Submitted: Grace; color: green; enabled: false`. Repeat with different values; the result updates. |
| 06 - Empty list | The empty title, subtitle and icon appear, including while typing. Back returns to the scenario list. |

Repeat in light and dark themes and at different display scales. Close and reopen Command Palette, reload extensions, and reopen each fixture to cover process lifetime and navigation cleanup.

## Automated fixture checks

`Test-Fixtures.ps1` selects projects from `CompatibilityExtension.slnx` and checks distinct identities, manifest/COM agreement, the resolved NuGet source and SDK version, absence of project dependencies, and hashes of the Toolkit, native SDK and WinMD files. It starts each executable in a verification mode that exercises the scenario data, command results, property/item notifications, search/filter transitions, form payload validation and repeated disposal.

These checks validate the test fixture. They do not validate packaged COM activation, host rendering, keyboard routing, UI Automation, or live extension reload. The UI checklist remains necessary.
