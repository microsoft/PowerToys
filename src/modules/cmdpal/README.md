# ![cmdpal logo](./Microsoft.CmdPal.UI/Assets/Stable/StoreLogo.scale-100.png) Command Palette

Windows Command Palette ("CmdPal") is the next iteration of PowerToys Run. With extensibility at its core, the Command Palette is your one-stop launcher to start _anything_.

By default, CmdPal is bound to <kbd>Win+Alt+Space</kbd>.

## Architecture overview

CmdPal is a Windows App SDK (WinUI 3) host app that discovers and loads extension providers out-of-proc.

- **Host UI**: `Microsoft.CmdPal.UI` (WinUI 3) + view models in `Microsoft.CmdPal.UI.ViewModels` and shared logic in `Microsoft.CmdPal.Core.*`.
- **Extension contract**: `Microsoft.CommandPalette.Extensions` (WinRT interfaces). Providers typically run in a separate process as a COM local server.
- **Extension helpers**: `Microsoft.CommandPalette.Extensions.Toolkit` (in-repo) and a NuGet-distributed variant (`JPSoftworks.CommandPalette.Extensions.Toolkit`) used by some extensions.

### Extension discovery & the sparse identity package

CmdPal uses the Windows AppExtension mechanism to find extensions registered as `Name="com.microsoft.commandpalette"`:

- The host declares `windows.appExtensionHost` in `src/modules/cmdpal/Microsoft.CmdPal.UI/Package.appxmanifest`.
- Extensions declare `windows.appExtension` and provide activation metadata (one or more CLSIDs under `<CmdPalProvider>` in the extension's properties).

PowerToys ships its built-in provider (`Microsoft.CmdPal.Ext.PowerToys.exe`) via the shared **sparse MSIX identity** package:

- `src/PackageIdentity/AppxManifest.xml` registers the extension executable as both:
  - `windows.comServer` (COM local server; launched with `-RegisterProcessAsComServer`)
  - `windows.appExtension` (so CmdPal can discover it via `AppExtensionCatalog`)
- The extension's `PublicFolder="Public"` maps to `src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.PowerToys/Public`.

For local development where you want CmdPal to pick up the in-box PowerToys provider, you generally need the sparse package registered; see `src/PackageIdentity/readme.md`.

## Creating an extension

The fastest way to get started is just to run the "Create extension" command in the palette itself. That'll prompt you for a project name and a Display Name, and where you want to place your project. Then just open the `sln` it produces. You should be ready to go.

The official API documentation can be found [on this docs site](https://learn.microsoft.com/windows/powertoys/command-palette/extensibility-overview).

We've also got samples, so that you can see how the APIs in-action.

* We've got [generic samples] in the repo
* We've got [real samples] in the repo too
* And we've even got [real extensions that we've "shipped" already]

> [!info]
> The Command Palette is currently in preview. Many features of the API are not yet fully implemented. We may introduce breaking API changes before CmdPal itself is v1.0.0

## Building CmdPal

### Install & Build PowerToys

1. Follow the install and build instructions for [PowerToys](https://github.com/microsoft/PowerToys/tree/main/doc/devdocs#compiling-powertoys)

### Load & Build

1. In Visual Studio, in the Solution Explorer Pane, confirm that all of the files/projects in `src\modules\CommandPalette` and `src\common\CalculatorEngineCommon` do not have `(unloaded)` on the right side
    1. If any file has `(unloaded)`, right click on file and select `Reload Project`
1. Now you can right click on one of the project below to `Build` and then `Deploy`:

Projects of interest are:
* `Microsoft.CmdPal.UI`: This is the main project for CmdPal. Build and run this to get the CmdPal.
* `Microsoft.CommandPalette.Extensions`: This is the official extension interface. 
  * This is designed to be language-agnostic. Any programming language which supports implementing WinRT interfaces should be able to implement the WinRT interface. 
* `Microsoft.CommandPalette.Extensions.Toolkit`: This is a C# helper library for creating extensions. This makes writing extensions easier.
* Everything under "SampleExtensions": These are example plugins to demo how to author extensions. Deploy any number of these, to get a feel for how the extension API works.

## Adding a new PowerToys module command

The in-box PowerToys provider (`Microsoft.CmdPal.Ext.PowerToys`) surfaces commands per module via small provider classes:

- Provider implementations live in `src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.PowerToys/Modules` (see `FancyZonesModuleCommandProvider.cs` for a representative example).
- Providers are aggregated in `src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.PowerToys/Helpers/ModuleCommandCatalog.cs`.
- The resulting `ListItem`s power both the PowerToys page (`PowerToysListPage`) and fallback search results (`PowerToysCommandsProvider.FallbackCommands`).

### Typical effort

- **5–15 minutes**: Add a simple command (open settings, launch a module) by reusing `OpenInSettingsCommand` / `LaunchModuleCommand`.
- **1–2+ hours**: Add deeper integration (dynamic pages, thumbnails/hero images, extra state queries, multi-step flows).

### Checklist (minimal path)

1. Add a new `*ModuleCommandProvider` deriving from `ModuleCommandProvider` and return `ListItem`s.
2. Register it by adding the provider to the `Providers` array in `ModuleCommandCatalog`.
3. If you need module-specific APIs, add the corresponding `*.ModuleServices` project reference to `src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.PowerToys/Microsoft.CmdPal.Ext.PowerToys.csproj`.
4. Pick an icon:
   - Reuse Settings icons via `SettingsWindow.ModuleIcon()` / `PowerToysResourcesHelper.IconFromSettingsIcon(...)` (the extension project already copies `Settings.UI` icons at build time).
   - Or use a Segoe Fluent glyph: `new IconInfo("\uE7F4")` is already used for monitor-related FancyZones UI.
5. If the module is gated by enablement, ensure `ModuleEnablementService` knows the right settings key and (optionally) update `PowerToysResourcesHelper` mappings for display name/icon.

You do **not** need to touch the sparse package manifest for new commands (only when adding new executables/COM servers to ship under the sparse identity).

### Footnotes and other links

* [Initial SDK Spec]

[^1]: you'll almost definitely want to do a `git init` in that directory, and set up a git repo to track your work. 


[Initial SDK Spec]: ./doc/initial-sdk-spec/initial-sdk-spec.md
[generic samples]: ./ext/SamplePagesExtension 
[real samples]: ./ext/ProcessMonitorExtension
[real extensions that we've "shipped" already]: https://github.com/zadjii/CmdPalExtensions/blob/main/src/extensions
