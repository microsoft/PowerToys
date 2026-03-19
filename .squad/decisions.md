# Squad Decisions

## Active Decisions

### 2026-03-19: PersistenceService / SettingsService / AppStateService Extraction

**Author:** Snake Eyes (Extensions Dev)  
**Status:** APPROVED  
**Reviewer:** Duke

Extract persistence logic from SettingsModel and AppStateModel into a three-layer service architecture:

1. **IPersistenceService / PersistenceService** — Generic JSON load/save with shallow-merge strategy (preserves unknown keys). AOT-compatible via `JsonTypeInfo<T>`.
2. **ISettingsService / SettingsService** — Owns SettingsModel lifecycle: load, migrate, save (with deprecated key cleanup), Reload(), SettingsChanged event.
3. **IAppStateService / AppStateService** — Owns AppStateModel lifecycle: load, save, StateChanged event.

All three registered as DI singletons. Bridge registrations expose raw model instances for backward compatibility:
```csharp
services.AddSingleton(sp => sp.GetRequiredService<ISettingsService>().Settings);
services.AddSingleton(sp => sp.GetRequiredService<IAppStateService>().State);
```

**Consequences:**
- Models are now pure data bags (no persistence methods, no events, no static fields)
- Consumer ViewModels receive `ISettingsService` for save operations instead of calling static methods
- SettingsChanged event moved from `TypedEventHandler<SettingsModel, object?>` to `TypedEventHandler<ISettingsService, SettingsModel>`
- JsonSerializationContext stays in SettingsModel.cs (co-located with types it serializes)

**Quality Gates:**
- ✅ All consumers identified via grep and updated (~20 files)
- ✅ Event semantics verified (sender type, subscribers)
- ✅ Migration logic preserved (HotkeyGoesHome → AutoGoHomeInterval)
- ✅ DI registration correct (services + backward compat bridge)
- ✅ Error handling adequate (Logger integration)
- ✅ AOT compatibility maintained (JsonTypeInfo<T> usage)

---

### 2026-03-19: WinUI3 Runtime Dependency in ViewModels Unit Tests

**Author:** Hawk (Tester/QA)  
**Status:** ACCEPTED

SettingsModel constructor initializes DockSettings, which uses `Microsoft.UI.Colors.Transparent`. This requires WinUI3 runtime COM registration, causing tests to fail in console test runners.

**Decision:** Accept limitation and document clearly. SettingsService tests require WinUI3 runtime via VS Test Explorer (may work in CI with proper runner). Alternative options (refactoring models, creating test-specific alternatives) require significant production code changes for minimal benefit.

**Implementation:**
- ✅ Added XML doc comment to SettingsServiceTests explaining limitation
- ✅ All PersistenceService tests pass (7/7)
- ✅ All AppStateService tests pass (6/6)
- ⚠️ SettingsService tests available for WinUI3-enabled test runners (9 tests)

**Future Work:**
- Test SettingsService in CI pipeline with WinUI3 test runner if available
- Monitor .NET 10+ for improved WinUI3 test support

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
