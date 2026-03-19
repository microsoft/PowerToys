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
