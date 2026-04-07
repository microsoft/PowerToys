# Project Context

- **Owner:** Michael Jolley
- **Project:** Command Palette (CmdPal) — a PowerToys module providing a searchable command palette with extensible plugin architecture
- **Stack:** C#, C++, WinUI 3, XAML, .NET 9, WinRT/CsWinRT, MSTest
- **Scope:** ONLY files within `src/modules/cmdpal/CommandPalette.slnf`
- **Created:** 2026-04-07

## Key Paths

- ViewModels: `src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/`
- Common: `src/modules/cmdpal/Microsoft.CmdPal.Common/`
- Extensions: `src/modules/cmdpal/ext/`
- Extension SDK: `src/modules/cmdpal/extensionsdk/`
- Keyboard Service: `src/modules/cmdpal/CmdPalKeyboardService/`
- Module Interface: `src/modules/cmdpal/CmdPalModuleInterface/`
- Terminal UI: `src/modules/cmdpal/Microsoft.Terminal.UI/`

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### Phase 1 — IExtensionService + CommandProviderWrapper refactor (2026-04-07)

- **Namespace ambiguity**: `IExtensionService` now exists in TWO namespaces: `Microsoft.CmdPal.Common.Services` (old, operates on `IExtensionWrapper`) and `Microsoft.CmdPal.UI.ViewModels.Services` (new, operates on `CommandProviderWrapper`/`TopLevelViewModel`). Both coexist until Phase 5. Any file importing both namespaces will need a fully-qualified name or alias.
- **ImplicitUsings is ENABLED** in the ViewModels project (`Microsoft.CmdPal.UI.ViewModels.csproj`), contrary to Common project conventions. No need to explicitly add `System.*` usings there.
- **LoggerMessage source-gen**: Added `Microsoft.Extensions.Logging.Abstractions` as direct PackageReference to ViewModels project for the `[LoggerMessage]` source generator. The type is available transitively (Common → Hosting → Logging), but the analyzer/generator requires a direct reference.
- **TopLevelViewModel still takes IServiceProvider** in its constructor — this blocks full removal of service locator from `CommandProviderWrapper.InitializeCommands`. Phase 2/3 must update TopLevelViewModel to accept explicit deps.
- **PinCommand/UnpinCommand/PinDockBand/UnpinDockBand** still take `IServiceProvider` and resolve `ISettingsService` from it. These methods are untouched in Phase 1 and will be updated in later phases.
- **Key files modified**: `CommandProviderWrapper.cs` (constructors, LoadTopLevelCommands, logging), new `Services/IExtensionService.cs`, `Microsoft.CmdPal.UI.ViewModels.csproj` (added Logging.Abstractions).

### Phase 2 — BuiltInExtensionService (2026-04-07)

- **BuiltInExtensionService** created at `Services/BuiltInExtensionService.cs`. Implements the new `IExtensionService` (ViewModels.Services namespace). Wraps each DI-injected `ICommandProvider` into a `CommandProviderWrapper` using the built-in constructor and fires lifecycle events (`OnCommandProviderAdded`, `OnCommandsAdded`, etc.).
- **ILoggerFactory injection**: The service takes `ILoggerFactory` rather than `ILogger<CommandProviderWrapper>` directly, so it can create a `ILogger<CommandProviderWrapper>` for the wrapper constructor and `ILogger<BuiltInExtensionService>` for itself.
- **Locking pattern**: Uses C# 13 `Lock` for synchronous sections and `SemaphoreSlim` for async-safe gating — same pattern as the jsonrpc reference branch.
- **Pre-existing build errors**: `TopLevelCommandManager.cs` lines 332 and 579 have CS0104 ambiguity between old and new `IExtensionService` interfaces. These are Phase 1 artifacts, not caused by this change. They'll resolve when TopLevelCommandManager is migrated in a later phase.
- **Key files created**: `Services/BuiltInExtensionService.cs`.

### Phase 2a — BuiltInExtensionService (2026-04-07)

- **BuiltInExtensionService** created at `Services/BuiltInExtensionService.cs` (339 lines). Implements the new `IExtensionService` (ViewModels.Services namespace). Wraps each DI-injected `ICommandProvider` into a `CommandProviderWrapper` using the Phase 1 constructor signature and fires lifecycle events.
- **ILoggerFactory injection**: Takes `ILoggerFactory` to create `ILogger<BuiltInExtensionService>` for itself and `ILogger<CommandProviderWrapper>` for each wrapper. Single factory injection point, scales naturally if additional typed loggers needed.
- **Locking pattern**: Uses C# 13 `Lock` for synchronous sections and `SemaphoreSlim` for async-safe gating — same pattern as the jsonrpc reference branch.
- **Lifecycle events**: Fires `OnCommandProviderAdded`, `OnCommandsAdded`, `OnDockBandAdded`, etc. as wrappers are created and initialized.
- **Key files created**: `Services/BuiltInExtensionService.cs`.

### Phase 2b — WinRTExtensionService (2026-04-07)

- **WinRTExtensionService** created at `Services/WinRTExtensionService.cs` (1128 lines). Implements the new `IExtensionService` (ViewModels.Services namespace). Reconciles three sources: the jsonrpc branch's WinRTExtensionService (interface shape), Models/ExtensionService.cs (package catalog discovery), and TopLevelCommandManager (resilient timeout loading).
- **Resilient loading pattern**: Preserves the timeout-based start/load from TopLevelCommandManager with 4 constants: `ExtensionStartTimeout` (10s), `CommandLoadTimeout` (10s), `BackgroundStartTimeout` (60s), `BackgroundCommandLoadTimeout` (60s). Extensions that timeout on initial start are retried in background via `StartExtensionWhenReadyAsync`. Command loads that timeout are deferred via `AppendCommandsWhenReadyAsync`.
- **Inner result types**: `ExtensionStartResult` and `CommandLoadResult` are private nested classes with `[MemberNotNullWhen]` attributes for null-state analysis, matching the pattern from TopLevelCommandManager. `TopLevelObjectSets` holds separated commands and dock bands.
- **Event model**: `OnCommandsAdded`/`OnCommandsRemoved` fire with combined commands + dock bands (via `CombineTopLevelObjectSets` helper). Consumers are responsible for splitting.
- **IExtensionService ambiguity fix**: Added `using OldExtensionService = Microsoft.CmdPal.Common.Services.IExtensionService;` alias in TopLevelCommandManager.cs to resolve the CS0104 ambiguity from Phase 1. This allows TopLevelCommandManager to reference both old and new interfaces unambiguously.
- **IsExtensionResult**: Made it a private nested record struct inside WinRTExtensionService to avoid namespace collision with the same-named type in `Models/ExtensionService.cs`.
- **ILoggerFactory pattern**: Follows BuiltInExtensionService convention — takes `ILoggerFactory`, creates `ILogger<WinRTExtensionService>` for self and `ILogger<CommandProviderWrapper>` for wrapper construction.
- **Hot-install/uninstall**: PackageCatalog events are handled synchronously under `_catalogLock`, then async work fires via `Task.Run`. Uninstall correctly removes extensions, wrappers, and commands from all tracking lists and fires `OnCommandProviderRemoved` + `OnCommandsRemoved`.
- **Key files created**: `Services/WinRTExtensionService.cs`. **Key files modified**: `TopLevelCommandManager.cs` (+9 lines for using alias).

### Phase 3 — TopLevelCommandManager refactor to thin aggregator (2026-04-07)

- **TopLevelCommandManager rewritten** from 870-line monolith to ~415-line thin event-driven aggregator. No longer loads built-ins or WinRT extensions directly; subscribes to `IExtensionService` events instead.
- **Constructor takes explicit deps**: `IEnumerable<IExtensionService>`, `ISettingsService`, `TaskScheduler`, `ILogger<TopLevelCommandManager>`. No more `IServiceProvider` service locator.
- **IPageContext implementation**: TopLevelCommandManager now implements `IPageContext` explicitly. `ProviderContext` returns `CommandProviderContext.Empty` since the aggregator has no single provider context.
- **Pin methods updated**: `CommandProviderWrapper.PinCommand`, `UnpinCommand`, `PinDockBand`, `UnpinDockBand` now take `ISettingsService` directly instead of `IServiceProvider`. The `using Microsoft.Extensions.DependencyInjection;` was removed.
- **DI registration**: App.xaml.cs registers both `BuiltInExtensionService` and `WinRTExtensionService` as `IExtensionService` (ViewModels.Services namespace) via `NewExtensionService` alias to avoid ambiguity with old `IExtensionService` (Common.Services).
- **PowerToysRootPageService updated**: `PreLoadAsync()` now calls `LoadExtensionsCommand.Execute(null)` (which signals all services). `PostLoadRootPageAsync()` awaits the execution task. Removed old `LoadBuiltinsAsync()` call.
- **StyleCop SA1512 enforcement**: Section header comments (`// ── ... ──`) must NOT be followed by a blank line in this repo. Comments are placed directly above the first member.
- **Pre-existing errors unchanged**: CS1503 (TopLevelViewModel still takes IServiceProvider), CS0067 (BuiltInExtensionService.OnCommandProviderRemoved unused), SA1512 in BuiltInExtensionService — all pre-existing Phase 2 issues.
- **Key files modified**: `TopLevelCommandManager.cs` (complete rewrite), `CommandProviderWrapper.cs` (pin methods + StyleCop), `App.xaml.cs` (DI registrations), `PowerToysRootPageService.cs` (lifecycle calls).

### Phase 5 — Old IExtensionService deletion and consumer migration (2026-04-07)

- **Old types deleted**: `Models/ExtensionService.cs` (old implementation) and `Common/Services/IExtensionService.cs` (old interface) removed via `git rm`. All discovery/catalog logic now lives in `WinRTExtensionService`.
- **MainWindow shutdown updated**: `MainWindow_Closed` now calls `serviceProvider.GetServices<IExtensionService>()` and iterates all implementations, calling `SignalStopExtensionsAsync()` on each. This ensures both `BuiltInExtensionService` and `WinRTExtensionService` are signaled on shutdown.
- **App.xaml.cs cleaned up**: Removed old `IExtensionService, ExtensionService` DI registration from `AddCoreServices`. Removed `NewExtensionService` alias (no longer needed since old interface is gone). Removed `using Microsoft.CmdPal.UI.ViewModels.Models;` (only needed for old `ExtensionService`). DI registrations now use `IExtensionService` directly (from `ViewModels.Services` namespace).
- **IExtensionService doc comment updated**: Removed "Coexists with Common.Services.IExtensionService until Phase 5" note — that migration is now complete.
- **PowerToysRootPageService unchanged**: `IExtensionWrapper` references in this file are correct — `IExtensionWrapper` is NOT being deleted (only `IExtensionService` and `ExtensionService` are). `SetActiveExtension(IExtensionWrapper?)` remains for foreground-rights handoff.
- **MS DI multiple registrations pattern**: Two `AddSingleton<IExtensionService, T>` registrations are resolved as `IEnumerable<IExtensionService>` via `GetServices<T>()`. This is standard MS DI behavior.
- **Pre-existing errors unchanged**: Same 3 errors (CS1503, CS0067, SA1512) from earlier phases remain — none introduced by this change.
- **Key files modified**: `MainWindow.xaml.cs` (shutdown path), `App.xaml.cs` (DI cleanup, alias removal), `Services/IExtensionService.cs` (doc comment). **Key files deleted**: `Models/ExtensionService.cs`, `Common/Services/IExtensionService.cs`.
