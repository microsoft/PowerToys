# Morpheus — History

## Core Context

- **Project:** PowerToys Command Palette — a WinUI 3 extensible command launcher with C++/WinRT SDK and 21 built-in extensions
- **Role:** ViewModel Dev
- **Joined:** 2026-03-10T03:10:51.245Z

## Learnings

<!-- Append learnings below -->
- **SettingsViewModel constructor is called directly** (not via DI) from 5 code-behind files. New optional params must default to `null` to avoid breaking existing call sites.
- **AOT constraint**: No System.Linq allowed. Use foreach loops for iteration. CompositeFormat is fine on net9.0.
- **DockMonitorConfig is a mutable class** (not a record). The VM mutates the config object in-place and then calls SaveSettings on the existing SettingsModel — same pattern as DockBandSettingsViewModel.
- **IMonitorService is registered as singleton** in App.xaml.cs DI container. Resolved via `App.Current.Services.GetService<IMonitorService>()`.
- **Existing stub pattern**: Trinity may create stub VMs for XAML compilation that Morpheus replaces with real implementations. Check for existing files before creating.

## Session Work (2026-03-10T15:43Z)

**Task:** Build DockMonitorConfigViewModel + update SettingsViewModel  
**Outcome:** Created full ViewModel with SideOverrideIndex binding (0–4 index mapping to nullable DockSide for "inherit" semantics). Updated SettingsViewModel with IMonitorService injection and RefreshMonitorConfigs(). Both projects build clean. Fixed pre-existing CS0169/SA1512 build warnings that were blocking Oracle's tests.

**Cross-agent awareness:**
- Trinity added 8 localization strings to support the UI bindings
- Oracle wrote 27 unit tests validating the DockMonitorConfig.ResolveSide() effective-config fallback logic
- Neo reviewed for AOT discipline (no LINQ, partial keywords)
