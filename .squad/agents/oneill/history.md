# Project Context

- **Owner:** Michael Jolley
- **Project:** Command Palette (CmdPal) — a PowerToys module providing a searchable command palette with extensible plugin architecture
- **Stack:** C#, C++, WinUI 3, XAML, .NET 9, WinRT/CsWinRT, MSTest
- **Scope:** ONLY files within `src/modules/cmdpal/CommandPalette.slnf`
- **Created:** 2026-04-07

## Key Paths

- UI: `src/modules/cmdpal/Microsoft.CmdPal.UI/`
- ViewModels: `src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/`
- Common: `src/modules/cmdpal/Microsoft.CmdPal.Common/`
- Extensions: `src/modules/cmdpal/ext/`
- Extension SDK: `src/modules/cmdpal/extensionsdk/`
- Tests: `src/modules/cmdpal/Tests/`

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### Phase 1 Update — Teal'c (Core Dev) completed 2026-04-07

**NEW IExtensionService + CommandProviderWrapper refactor**
- IExtensionService now in TWO namespaces: `Microsoft.CmdPal.Common.Services` (old, IExtensionWrapper) and `Microsoft.CmdPal.UI.ViewModels.Services` (new, CommandProviderWrapper/TopLevelViewModel). Coexist until Phase 5.
- CommandProviderWrapper updated: explicit DI (HotkeyManager, AliasManager, ILogger<T>), 20 LoggerMessage source-gen methods.
- TopLevelViewModel still takes IServiceProvider — blocks full removal of service locator from InitializeCommands.
- Key files: CommandProviderWrapper.cs, new Services/IExtensionService.cs, ViewModels.csproj (+Logging.Abstractions).
- **Phase 2 will update TopLevelCommandManager** to use new IExtensionService and pass explicit deps to CommandProviderWrapper.

### Phase 3 Update — Teal'c (Core Dev) completed 2026-04-07

**TopLevelCommandManager refactored to thin event-driven aggregator**
- TopLevelCommandManager rewritten: 870→416 lines. No longer loads built-ins or WinRT directly; subscribes to IExtensionService events.
- Pin/Unpin methods in CommandProviderWrapper now take ISettingsService explicitly (no service locator).
- DI wiring in App.xaml.cs: both BuiltInExtensionService and WinRTExtensionService registered as IExtensionService.
- PowerToysRootPageService: LoadExtensionsCommand.Execute now the single bootstrap point.
- Net change: +323 insertions, -666 deletions (-343 lines). All functionality preserved. Code review ready.
