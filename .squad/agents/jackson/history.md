# Project Context

- **Owner:** Michael Jolley
- **Project:** Command Palette (CmdPal) — a PowerToys module providing a searchable command palette with extensible plugin architecture
- **Stack:** C#, C++, WinUI 3, XAML, .NET 9, WinRT/CsWinRT, MSTest
- **Scope:** ONLY files within `src/modules/cmdpal/CommandPalette.slnf`
- **Created:** 2026-04-07

## Key Paths

- Tests: `src/modules/cmdpal/Tests/`
- Test Base: `src/modules/cmdpal/Tests/Microsoft.CmdPal.Ext.UnitTestsBase/`
- ViewModel Tests: `src/modules/cmdpal/Tests/Microsoft.CmdPal.UI.ViewModels.UnitTests/`
- UI Tests: `src/modules/cmdpal/Tests/Microsoft.CmdPal.UITests/`
- Common Tests: `src/modules/cmdpal/Tests/Microsoft.CmdPal.Common.UnitTests/`
- Toolkit Tests: `src/modules/cmdpal/Tests/Microsoft.CommandPalette.Extensions.Toolkit.UnitTests/`

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### Phase 1 Update — Teal'c (Core Dev) completed 2026-04-07

**NEW IExtensionService + CommandProviderWrapper refactor**
- IExtensionService now in TWO namespaces: `Microsoft.CmdPal.Common.Services` (old, IExtensionWrapper) and `Microsoft.CmdPal.UI.ViewModels.Services` (new, CommandProviderWrapper/TopLevelViewModel). Coexist until Phase 5.
- CommandProviderWrapper updated: explicit DI (HotkeyManager, AliasManager, ILogger<T>), 20 LoggerMessage source-gen methods.
- TopLevelViewModel still takes IServiceProvider — blocks full removal of service locator from InitializeCommands.
- Key files: CommandProviderWrapper.cs, new Services/IExtensionService.cs, ViewModels.csproj (+Logging.Abstractions).
- **Tests must be updated** when CommandProviderWrapper signature changes are consumed by TopLevelCommandManager.

### Phase 1 Tests — Jackson (Tester) completed 2026-04-07

**IExtensionService architecture unit tests**
- Created `BuiltInExtensionServiceTests.cs` (9 tests) and `TopLevelCommandManagerTests.cs` (16 tests).
- **BuiltInExtensionService**: Testable end-to-end using `StubCommandProvider : CommandProvider` from Toolkit. Returns empty TopLevelCommands/FallbackCommands. Events fire even with empty command sets.
- **TopLevelCommandManager**: Tested via `FakeExtensionService` implementing `IExtensionService`. Manual event firing verifies subscription/unsubscription, provider tracking, reload lifecycle, and message handlers.
- **CsWinRT1028 gotcha**: Any class implementing WinRT interfaces (e.g., `CommandProvider`) AND any enclosing parent class must be marked `partial`. Nested test doubles trigger this.
- **SA1512 strictly enforced**: No blank line after single-line comments (including section separators like `// ── foo ──`). Remove blank lines or use `<summary>` doc comments instead.
- **SA1516 strictly enforced**: Event declarations in a class must each be separated by a blank line.
- **Mocking approach**: Moq for `ISettingsService`; manual fakes for `IExtensionService` (need event-firing control). `NullLogger<T>` / `NullLoggerFactory.Instance` for logging deps. `null!` for `HotkeyManager`/`AliasManager` when only stored but not invoked.
- **Phase 1 bridge fix**: Added `SettingsOnlyServiceProvider` nested class in `CommandProviderWrapper` to bridge `ISettingsService` → `IServiceProvider` for `TopLevelViewModel` constructor. Fixes CS1503 compile error. Remove in Phase 2.
- **BuiltInExtensionService CS0067**: `OnCommandProviderRemoved` event declared by interface but never raised yet (stop is a no-op for built-ins). Suppressed with `#pragma warning disable`.
- **SettingsModel**: Use `JsonSerializer.Deserialize("{}", JsonSerializationContext.Default.SettingsModel)` to create test instances without WinUI3 `Colors.Transparent` COM dependency.
- **Phase 6 completion**: 25 unit tests (9 + 16), build passes exit code 0, zero errors. Decision doc written and merged. Ready for Phase 2 integration.
