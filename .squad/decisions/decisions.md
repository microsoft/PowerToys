# Decisions

## Remove Raw Model DI Registrations

**Author:** Snake Eyes  
**Date:** 2026-03-20  
**Status:** Implemented  

### Context

In iteration 1, we extracted SettingsService/AppStateService but kept backward-compatibility bridge registrations:
```csharp
services.AddSingleton(sp => sp.GetRequiredService<ISettingsService>().Settings);
services.AddSingleton(sp => sp.GetRequiredService<IAppStateService>().State);
```

These allowed consumers to resolve `SettingsModel`/`AppStateModel` directly from DI, but violated the principle that consumers should depend on service interfaces, not raw models.

### Decision

Remove bridge registrations and update all ~30+ consumers to use `ISettingsService`/`IAppStateService` interfaces exclusively.

#### Convenience Property Pattern

To minimize code churn (~100+ field references), used private convenience properties:
```csharp
#pragma warning disable SA1300
private SettingsModel _settings => _settingsService.Settings;
#pragma warning restore SA1300
```

This is an expression-bodied property that always reads the current value from the service (correct for hot-reload), unlike the old readonly field that was captured once at construction.

#### IApplicationInfoService Injection

Replaced static `Utilities.BaseSettingsPath("Microsoft.CmdPal")` calls in SettingsService/AppStateService with injected `IApplicationInfoService.ConfigDirectory`, improving testability.

### Impact

- No consumer can resolve `SettingsModel` or `AppStateModel` from DI anymore
- All consumers depend on `ISettingsService` or `IAppStateService` interfaces
- Settings hot-reload is semantically correct (always reads current value)
- Test projects use `Mock<IApplicationInfoService>` for path configuration

### Risks

- SA1300 pragma suppressions add minor noise (6 files in ViewModels, 4 in UI)
- If a new consumer is added that needs settings, it must inject `ISettingsService` — cannot take a shortcut via raw model resolution

## SettingsOnlyServiceProvider Bridge for Phase 1

**Author:** Jackson (Tester/QA)  
**Status:** IMPLEMENTED (Phase 6)  
**Date:** 2026-04-07

### Context

The Phase 1 refactor introduced `InitializeCommands` in `CommandProviderWrapper` which passes `ISettingsService` to the `TopLevelViewModel` constructor. However, `TopLevelViewModel` still expects `IServiceProvider` (Phase 2 work). This caused a CS1503 compile error that blocked all test and production builds.

### Decision

Added a private nested `SettingsOnlyServiceProvider` class inside `CommandProviderWrapper` that wraps `ISettingsService` into a minimal `IServiceProvider`. This unblocks compilation while the TODO for Phase 2 remains.

```csharp
private sealed class SettingsOnlyServiceProvider(ISettingsService settingsService) : IServiceProvider
{
    public object? GetService(Type serviceType) =>
        serviceType == typeof(ISettingsService) ? settingsService : null;
}
```

### Consequences

- Build is unblocked for all downstream consumers (tests, CI).
- `TopLevelViewModel` calls to `GetService<HotkeyManager>()` will return `null` for providers loaded through this path (built-in providers via `BuiltInExtensionService`). The `Hotkey` setter will throw `NullReferenceException` if invoked on built-in commands — this is acceptable because built-in commands historically don't support hotkey assignment.
- **Must be removed in Phase 2** when `TopLevelViewModel` accepts explicit DI parameters.

### Alternatives Considered

1. **Leave the compile error** — blocks all builds; unacceptable.
2. **Full Phase 2 refactor now** — out of scope for test task; changes 15+ files.
3. **Pass full DI container** — requires plumbing `IServiceProvider` through `BuiltInExtensionService`; over-engineers the bridge.

## User Directive: Pragma Warning Suppression

**Author:** Michael Jolley (via Copilot)  
**Date:** 2026-03-20  
**Status:** Active

Never suppress warnings (`#pragma warning disable`) without prompting the user for confirmation first. It is normally the wrong choice.

## Old IExtensionService Removed — Single Canonical Interface

**Author:** Teal'c (Core Dev)  
**Date:** 2026-04-07  
**Status:** IMPLEMENTED (Phase 4-5 complete)

### Context

The old `IExtensionService` (in `Microsoft.CmdPal.Common.Services`) and its implementation `ExtensionService` (in `ViewModels/Models/`) have been fully replaced by the new `IExtensionService` (in `ViewModels/Services/`) with two implementations: `BuiltInExtensionService` and `WinRTExtensionService`.

### Decision

Delete the old interface and implementation. Update all remaining consumers to use the new multi-service architecture:

- `MainWindow.MainWindow_Closed` iterates `GetServices<IExtensionService>()` to signal all implementations on shutdown.
- `App.xaml.cs` registers only the two new services; old registration removed.
- The `NewExtensionService` alias is no longer needed anywhere — `IExtensionService` is now unambiguous.

### Consequences

- **One canonical `IExtensionService`** lives at `ViewModels.Services.IExtensionService`. No more namespace ambiguity.
- **`IExtensionWrapper` unchanged** — still in `Common.Services`, still used by `WinRTExtensionService` internally and by `PowerToysRootPageService` for foreground rights.
- Any future extension service must implement `ViewModels.Services.IExtensionService` and register via `AddSingleton<IExtensionService, T>` to be automatically picked up by `TopLevelCommandManager` and the shutdown path.

## Dual IExtensionService Namespaces — Coexistence Plan (ARCHIVED)

**Author:** Teal'c (Core Dev)  
**Date:** 2026-04-07  
**Status:** ARCHIVED (Replaced by single canonical interface decision)

### Context

Phase 1 introduces a new `IExtensionService` interface in `Microsoft.CmdPal.UI.ViewModels.Services` that operates on `CommandProviderWrapper` and `TopLevelViewModel`. The existing `IExtensionService` in `Microsoft.CmdPal.Common.Services` operates on raw `IExtensionWrapper`.

### Decision

Both interfaces coexist until Phase 5. Any file that imports both namespaces must use fully-qualified names or aliases to disambiguate. The new interface is the target for BuiltInExtensionService (Phase 2) and WinRTExtensionService (Phase 3). The old interface is removed in Phase 5 when TopLevelCommandManager is dismantled.

### Consequences

- CS0104 namespace ambiguity in `TopLevelCommandManager.cs` resolved via `using OldExtensionService = Microsoft.CmdPal.Common.Services.IExtensionService;` alias in Phase 2b.
- `InitializeCommands` passes `ISettingsService` where `TopLevelViewModel` expects `IServiceProvider` — fixed when TopLevelViewModel is updated.
- `CommandProviderWrapper` now requires `HotkeyManager`, `AliasManager`, and `ILogger<CommandProviderWrapper>` in all constructors — updated by BuiltInExtensionService and WinRTExtensionService in Phase 2.

## BuiltInExtensionService Takes ILoggerFactory

**Author:** Teal'c (Core Dev)  
**Date:** 2026-04-07  
**Status:** IMPLEMENTED

### Context

`BuiltInExtensionService` needs to create `CommandProviderWrapper` instances, which require `ILogger<CommandProviderWrapper>`. The service itself also needs `ILogger<BuiltInExtensionService>`.

### Decision

Inject `ILoggerFactory` instead of individual logger instances. The service calls `loggerFactory.CreateLogger<T>()` for both its own logger and the wrapper logger. This avoids requiring two separate `ILogger<T>` constructor parameters and scales naturally if additional typed loggers are needed.

### Consequences

- DI registration must provide `ILoggerFactory` (standard in `Microsoft.Extensions.Hosting`)
- Single factory injection point instead of multiple typed loggers
- Consistent with how ASP.NET Core services typically obtain loggers
- Pattern also adopted by WinRTExtensionService for consistency

## WinRTExtensionService — Resilient Loading Absorbed from TopLevelCommandManager

**Author:** Teal'c (Core Dev)  
**Date:** 2026-04-07  
**Status:** IMPLEMENTED

### Decision

The WinRT extension lifecycle (start, command load, hot-install, hot-uninstall) is now owned by `WinRTExtensionService`, absorbing the resilient timeout-based loading logic previously embedded in `TopLevelCommandManager`.

### Key Points

1. **Timeout constants preserved as-is**: `ExtensionStartTimeout` (10s), `CommandLoadTimeout` (10s), `BackgroundStartTimeout` (60s), `BackgroundCommandLoadTimeout` (60s). These match the existing TopLevelCommandManager values. Changing them later should only require updating WinRTExtensionService.

2. **Events carry combined items**: `OnCommandsAdded`/`OnCommandsRemoved` fire with merged commands + dock bands in a single `IEnumerable<TopLevelViewModel>`. TopLevelCommandManager (or its replacement) must split them when migrating to consume these events.

3. **ILoggerFactory, not ILogger<T>**: Both `BuiltInExtensionService` and `WinRTExtensionService` take `ILoggerFactory` to create typed loggers for themselves and for `CommandProviderWrapper` instances. This is the established pattern for services that create other DI-dependent objects.

4. **IExtensionService ambiguity**: Added `using OldExtensionService = ...` alias in TopLevelCommandManager.cs to resolve the CS0104 ambiguity between old and new `IExtensionService` interfaces. This is a stopgap until TopLevelCommandManager is migrated to use the new interface exclusively.

### Consequences

- TopLevelCommandManager's `LoadExtensionsAsync`, `TryStartExtensionAsync`, `StartExtensionWhenReadyAsync`, `TryLoadCommandsAsync`, `AppendCommandsWhenReadyAsync`, and associated inner types are now **duplicated** between TopLevelCommandManager and WinRTExtensionService. A later phase must remove these from TopLevelCommandManager when it's migrated to consume IExtensionService events.
- Hot-install/uninstall events still run synchronous discovery under `_catalogLock` (blocking `Task.Run().Result` call for `IsValidCmdPalExtensionAsync`). This matches the existing pattern in `Models/ExtensionService.cs` and should be revisited if PackageCatalog events prove to be UI-thread-bound.
