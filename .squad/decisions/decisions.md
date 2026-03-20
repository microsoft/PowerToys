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
