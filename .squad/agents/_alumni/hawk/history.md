# Project Context

- **Owner:** Michael Jolley
- **Project:** Command Palette (CmdPal) — a PowerToys module providing a searchable command palette with extensible plugin architecture
- **Stack:** C#, C++, WinUI 3, XAML, .NET 9, WinRT/CsWinRT
- **Scope:** ONLY files within src/modules/cmdpal/CommandPalette.slnf solution filter
- **Key components:**
  - Tests: 13 unit test projects + UI tests in src/modules/cmdpal/Tests/
    - Microsoft.CmdPal.Common.UnitTests
    - Microsoft.CmdPal.Ext.Apps.UnitTests
    - Microsoft.CmdPal.Ext.Bookmarks.UnitTests
    - Microsoft.CmdPal.Ext.Calc.UnitTests
    - Microsoft.CmdPal.Ext.ClipboardHistory.UnitTests
    - Microsoft.CmdPal.Ext.Registry.UnitTests
    - Microsoft.CmdPal.Ext.RemoteDesktop.UnitTests
    - Microsoft.CmdPal.Ext.Shell.UnitTests
    - Microsoft.CmdPal.Ext.System.UnitTests
    - Microsoft.CmdPal.Ext.TimeDate.UnitTests
    - Microsoft.CmdPal.Ext.WebSearch.UnitTests
    - Microsoft.CmdPal.Ext.WindowWalker.UnitTests
    - Microsoft.CmdPal.UI.ViewModels.UnitTests
    - Microsoft.CmdPal.UITests
    - Microsoft.CommandPalette.Extensions.Toolkit.UnitTests
  - Shared test base: Microsoft.CmdPal.Ext.UnitTestBase
- **Created:** 2026-03-19

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-03-19 - Persistence Service Tests (Phase 5)

**Test Files Created:**
- `src/modules/cmdpal/Tests/Microsoft.CmdPal.UI.ViewModels.UnitTests/PersistenceServiceTests.cs`
- `src/modules/cmdpal/Tests/Microsoft.CmdPal.UI.ViewModels.UnitTests/SettingsServiceTests.cs`
- `src/modules/cmdpal/Tests/Microsoft.CmdPal.UI.ViewModels.UnitTests/AppStateServiceTests.cs`

**Test Patterns:**
- For testing PersistenceService with AOT-compatible serialization: Created a simple `TestModel` with its own `JsonSerializationContext` to avoid dependencies on the full SettingsModel/AppStateModel serialization contexts
- For testing services that use IPersistenceService: Mock IPersistenceService using Moq, setup Load() to return test instances
- PersistenceService.Save() does NOT create missing directories - callers must create parent directories (e.g., SettingsService calls Directory.CreateDirectory in SettingsJsonPath())

**WinUI3 Test Limitations:**
- SettingsModel constructor initializes DockSettings which uses `Microsoft.UI.Colors.Transparent`
- This requires WinUI3 runtime COM registration (REGDB_E_CLASSNOTREG error in console test runners)
- SettingsService tests fail in vstest.console but may pass in VS Test Explorer with WinUI3 host
- Workaround attempted: Deserializing from minimal JSON still triggers constructor
- **Decision**: Document limitation in test class; SettingsService tests require WinUI3 runtime

**Test Coverage:**
- PersistenceService: 7 tests (Load with missing file, valid JSON, invalid JSON; Save create, merge, postProcess, directory handling)
- AppStateService: 6 tests (Constructor loads state, State property, Save delegates, Save raises event, event arguments, always raises)
- SettingsService: 9 tests (Constructor, Settings property, Save delegation, hotReload event behavior, Reload, event arguments) - **LIMITED by WinUI3 dependency**

**Build & Run:**
- Build: `& "C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe" /restore /p:Platform=x64 /p:Configuration=Debug`
- Run: `vstest.console.exe` with test DLL at `x64\Debug\WinUI3Apps\CmdPal\tests\Microsoft.CmdPal.UI.ViewModels.UnitTests.dll`
- **Result**: 14/14 tests pass for PersistenceService + AppStateService (SettingsService tests require WinUI3 runtime)

### 2026-03-19 — Test Suite Complete: 23 Tests Written (14 Passing)

Completed unit test suite for persistence services (Phase 5):
- **PersistenceService tests:** 7/7 passing (load/save/merge/postProcess patterns)
- **AppStateService tests:** 6/6 passing (state lifecycle and events)
- **SettingsService tests:** 9 tests written (require WinUI3 runtime, documented limitation)
- **WinUI3 Test Fix (Coordinator):** Fixed Colors.Transparent dependency in models; upgraded to WinUI3 test infrastructure, all 43 tests passing

**Test Patterns:** Established AOT serialization pattern (TestModel + TestJsonSerializationContext), mock service pattern (IPersistenceService mocking), event validation patterns

**Quality:** 14/14 console tests passing, 9 additional tests available with WinUI3 runtime, patterns documented for future service tests
