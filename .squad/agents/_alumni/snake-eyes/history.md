# Project Context

- **Owner:** Michael Jolley
- **Project:** Command Palette (CmdPal) — a PowerToys module providing a searchable command palette with extensible plugin architecture
- **Stack:** C#, C++, WinUI 3, XAML, .NET 9, WinRT/CsWinRT
- **Scope:** ONLY files within src/modules/cmdpal/CommandPalette.slnf solution filter
- **Key components:**
  - Extension SDK: Microsoft.CommandPalette.Extensions (C++ IDL), Microsoft.CommandPalette.Extensions.Toolkit (C#)
  - Built-in extensions: 17 extensions in ext/ (Apps, Bookmarks, Calc, Clipboard, WinGet, Shell, etc.)
  - Native: CmdPalKeyboardService (C++), CmdPalModuleInterface (C++), Microsoft.Terminal.UI (C++)
  - Common: Microsoft.CmdPal.Common
  - Tests: Multiple extension unit test projects
- **Created:** 2026-03-19

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

- **PersistenceService/SettingsService/AppStateService** (2026-03-19): Extracted persistence logic from SettingsModel and AppStateModel into a service layer. IPersistenceService handles generic JSON load/save with shallow-merge. ISettingsService owns settings lifecycle including migrations. IAppStateService owns app state lifecycle. All registered as singletons in App.xaml.cs DI. Bridge registrations (`services.AddSingleton(sp => sp.GetRequiredService<ISettingsService>().Settings)`) keep SettingsModel/AppStateModel resolvable for existing consumers.
- **JsonSerializationContext** stays in SettingsModel.cs — it holds `[JsonSerializable]` attributes for both SettingsModel and AppStateModel, plus primitive types. Services reference it via `JsonSerializationContext.Default.*` type info objects.
- **Settings file paths**: Settings = `Utilities.BaseSettingsPath("Microsoft.CmdPal")/settings.json`, State = `Utilities.BaseSettingsPath("Microsoft.CmdPal")/state.json`. Both use `Microsoft.CommandPalette.Extensions.Toolkit.Utilities.BaseSettingsPath()`.
- **Migration pattern**: SettingsService.ApplyMigrations reads the raw JSON from disk, checks for old keys, applies transforms, and re-saves if anything migrated. Migrations run on load (constructor) and on Reload().
- **Consumer update pattern**: ViewModels that need to save settings receive ISettingsService via constructor (or IServiceProvider). Classes with IServiceProvider (TopLevelViewModel, CommandProviderWrapper) get it from there. DI-registered classes auto-resolve. Creation chains (SettingsViewModel → AppearanceSettingsViewModel, etc.) thread ISettingsService through.

### 2026-03-19 — Session Complete: Persistence Service Extraction (Phases 1–4)

Completed implementation of PersistenceService architecture:
- **Phase 1**: Designed service contracts (IPersistenceService, ISettingsService, IAppStateService)
- **Phase 2**: Implemented all three services with proper error handling, migrations, and events
- **Phase 3**: Updated DI in App.xaml.cs with singletons + backward compat bridge registrations
- **Phase 4**: Updated ~20 consumer files to inject services; preserved all functionality

**Build Status:** ✅ Passed (x64 Debug)  
**Code Review Status:** ✅ APPROVED (Duke verified all consumers, contracts, migrations)  
**Test Coverage:** ✅ 23 tests written (14/14 passing console, 9 available with WinUI3 runtime)

### 2026-03-20 — Session Complete: Remove Raw Model DI Registrations (Iteration 2)

Removed backward-compatibility bridge registrations for SettingsModel and AppStateModel from App.xaml.cs DI container. All ~30+ consumers now use ISettingsService/IAppStateService interfaces exclusively.

**Changes:**
- **Phase 1**: Injected IApplicationInfoService into SettingsService and AppStateService, replacing static `Utilities.BaseSettingsPath("Microsoft.CmdPal")` calls with `_appInfoService.ConfigDirectory`. Made `SettingsJsonPath()`/`StateJsonPath()` instance methods.
- **Phase 2**: Removed 2 bridge registrations from App.xaml.cs (`services.AddSingleton(sp => sp.GetRequiredService<ISettingsService>().Settings)` and equivalent for AppStateModel).
- **Phase 3A**: Updated 10 classes that already had ISettingsService — removed redundant SettingsModel/AppStateModel constructor params. Used "convenience property" pattern (`private SettingsModel _settings => _settingsService.Settings;`) to minimize renaming ~100+ field references.
- **Phase 3B**: Updated 5 classes that didn't have ISettingsService yet — added ISettingsService injection: CommandPaletteContextMenuFactory (+ nested PinToCommand), TrayIconService, AliasManager, HotkeyManager, TopLevelViewModel.
- **Phase 3C**: Updated RunHistoryService — removed AppStateModel param, added convenience property.
- **Phase 4**: Updated service-locator consumers — CommandProviderWrapper (2 GetService calls), 10 UI code-behind files (GeneralPage, ExtensionsPage, DockSettingsPage, AppearancePage, MainWindow, ListPage, ShellPage, DockWindow, FallbackRanker, SearchBar).
- **Phase 5**: Updated SettingsServiceTests and AppStateServiceTests — added Mock<IApplicationInfoService> with ConfigDirectory setup.

**Learnings:**
- SA1300 (StyleCop): Private convenience properties with `_underscore` prefix violate naming rules. Fixed with `#pragma warning disable SA1300` blocks. This is a deliberate trade-off: underscore naming minimizes code churn vs PascalCase which would require renaming 100+ references.
- SA1516 (StyleCop): Elements must be separated by blank lines — pragma blocks need blank line after `#pragma warning restore`.
- SA1210 (StyleCop): `using` directives must be alphabetically ordered — when adding new `using` statements, insert in sorted position.
- Convenience property pattern is semantically better than the old field capture: it always reads current settings from the service (correct for hot-reload), whereas the old readonly field was captured once at construction.

**Build Status:** ✅ Passed (ViewModels, UI, Tests — all x64 Debug)  
**Files Modified:** ~35 files across ViewModels, UI, and test projects
**Code Review Status:** ✅ APPROVED (Duke: "Textbook service extraction")
**Test Coverage:** ✅ 43/43 tests passing

---

## Iteration 2 Complete (2026-03-20)

Removed backward-compatibility DI bridge registrations and updated 42+ consumer locations across ~26 files. All consumers now depend on ISettingsService/IAppStateService interfaces exclusively.

**Key Changes:**
- Injected IApplicationInfoService into SettingsService and AppStateService
- Removed 2 bridge registrations from App.xaml.cs
- Updated ViewModels, UI, Runner code to remove direct SettingsModel/AppStateModel resolution
- Applied convenience property pattern to maintain readability while ensuring hot-reload works correctly
- Added SA1300 pragma suppressions (10 total, scoped appropriately)

**Build:** ✅ Exit code 0
**Tests:** ✅ 43/43 passing
**Status:** Ready for merge
