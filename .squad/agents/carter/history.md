# Project Context

- **Owner:** Michael Jolley
- **Project:** Command Palette (CmdPal) — a PowerToys module providing a searchable command palette with extensible plugin architecture
- **Stack:** C#, C++, WinUI 3, XAML, .NET 9, WinRT/CsWinRT, MSTest
- **Scope:** ONLY files within `src/modules/cmdpal/CommandPalette.slnf`
- **Created:** 2026-04-07

## Key Paths

- UI: `src/modules/cmdpal/Microsoft.CmdPal.UI/`
- ViewModels: `src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/`
- Styles: `src/modules/cmdpal/Microsoft.CmdPal.UI/Styles/`
- Controls: `src/modules/cmdpal/Microsoft.CmdPal.UI/Controls/`
- Pages: `src/modules/cmdpal/Microsoft.CmdPal.UI/Pages/`

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### Phase 1 Update — Teal'c (Core Dev) completed 2026-04-07

**NEW IExtensionService + CommandProviderWrapper refactor**
- IExtensionService now in TWO namespaces: `Microsoft.CmdPal.Common.Services` (old, IExtensionWrapper) and `Microsoft.CmdPal.UI.ViewModels.Services` (new, CommandProviderWrapper/TopLevelViewModel). Coexist until Phase 5.
- CommandProviderWrapper updated: explicit DI (HotkeyManager, AliasManager, ILogger<T>), 20 LoggerMessage source-gen methods.
- Key files: CommandProviderWrapper.cs, new Services/IExtensionService.cs, ViewModels.csproj (+Logging.Abstractions).
