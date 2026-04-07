# Project Context

- **Owner:** Michael Jolley
- **Project:** Command Palette (CmdPal) — a PowerToys module providing a searchable command palette with extensible plugin architecture
- **Stack:** C#, C++, WinUI 3, XAML, .NET 9, WinRT/CsWinRT
- **Scope:** ONLY files within src/modules/cmdpal/CommandPalette.slnf solution filter
- **Key components:**
  - Core UI: Microsoft.CmdPal.UI (WinUI 3), Microsoft.CmdPal.UI.ViewModels
  - Common: Microsoft.CmdPal.Common
  - Native: CmdPalKeyboardService (C++), CmdPalModuleInterface (C++), Microsoft.Terminal.UI (C++)
  - Extension SDK: Microsoft.CommandPalette.Extensions (C++ IDL), Microsoft.CommandPalette.Extensions.Toolkit (C#)
  - Built-in extensions: 17 extensions in ext/ (Apps, Bookmarks, Calc, Clipboard, WinGet, Shell, etc.)
  - Tests: 13 unit test projects + UI tests
- **Created:** 2026-03-19

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-03-19: Persistence Service Extraction Pattern

**Architecture Decision:** Extracted persistence logic from SettingsModel and AppStateModel into dedicated service layer:
- `IPersistenceService` — Generic JSON file I/O with shallow-merge and AOT-compatible JsonTypeInfo<T>
- `ISettingsService` / `SettingsService` — Settings lifecycle (load/save/migration/events)
- `IAppStateService` / `AppStateService` — App state lifecycle (load/save/events)

**Key Patterns:**
- Models (SettingsModel/AppStateModel) now pure data bags with no I/O logic
- Services handle persistence, migration, and change notification via TypedEventHandler
- DI registration provides both service interfaces AND raw model instances for backward compat
- Event sender changed from `SettingsModel` to `ISettingsService` — all subscribers updated
- Migration logic preserved in SettingsService with deprecated key cleanup via postProcessMerge callback
- Shallow merge strategy preserves unknown JSON keys for forward compatibility
- All ~19 consumer files updated to inject service interfaces where needed

**File Paths:**
- Services: `src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/Services/`
- Models: `src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/SettingsModel.cs`, `AppStateModel.cs`
- DI: `src/modules/cmdpal/Microsoft.CmdPal.UI/App.xaml.cs` lines 183-220
- Major consumers: CommandProviderWrapper, MainListPage, ThemeService, DockViewModel, all settings ViewModels

**Quality:** No functionality lost, contracts stable, migration preserved, all consumers updated correctly. Approved for merge.

### 2026-03-19 — Code Review Complete: Persistence Service Extraction

Reviewed all phases 1–4 of persistence service extraction:
- Verified all 19 consumer files updated correctly (grep exhaustive search confirmed)
- Confirmed event sender type changes (SettingsModel → ISettingsService) propagated to all 8+ subscribers
- Validated DI registration with backward compat bridge
- Confirmed migration logic (HotkeyGoesHome → AutoGoHomeInterval) preserved intact
- Verified AOT compatibility (JsonTypeInfo<T> usage throughout)
- Checked error handling (Logger integration comprehensive)

**Verdict:** APPROVED — Architecture is sound, implementation correct, no ABI breaks, no lost functionality, ready to merge.

### 2026-03-20 — Code Review Complete: Remove Raw Model DI Registrations (Iteration 2)

Reviewed iteration 2 diff: removal of bridge DI registrations, IApplicationInfoService injection, and 42+ consumer updates.

**Findings:**
- ✓ DI registration removal clean and complete
- ✓ IApplicationInfoService injection correctly implemented
- ✓ 42+ consumer updates follow consistent pattern
- ✓ SA1300 pragmas appropriately scoped to private convenience properties
- ✓ No breaking changes to public APIs
- ✓ Settings hot-reload semantics correct (expression-bodied properties always read current value)
- ✓ Test updates (Mock<IApplicationInfoService>) proper
- ✓ Build clean, 43/43 tests passing

**Verdict:** APPROVED — "Textbook service extraction." No issues found, ready for merge.
