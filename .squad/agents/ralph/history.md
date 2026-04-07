# Project Context

- **Owner:** Michael Jolley
- **Project:** Command Palette (CmdPal) — PowerToys module
- **Created:** 2026-03-19

## Learnings

<!-- Append new learnings below. -->

### Phase 1 Update — Teal'c (Core Dev) completed 2026-04-07

**NEW IExtensionService + CommandProviderWrapper refactor**
- CommandProviderWrapper updated: explicit DI (HotkeyManager, AliasManager, ILogger<T>), 20 LoggerMessage source-gen methods.
- New IExtensionService interface in Microsoft.CmdPal.UI.ViewModels.Services (distinct from old IExtensionService in Common.Services).
- Coexists until Phase 5. Key files: CommandProviderWrapper.cs, Services/IExtensionService.cs, ViewModels.csproj.
