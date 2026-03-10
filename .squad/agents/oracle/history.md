# Oracle — History

## Core Context

- **Project:** PowerToys Command Palette — a WinUI 3 extensible command launcher with C++/WinRT SDK and 21 built-in extensions
- **Role:** Tester
- **Joined:** 2026-03-10T03:10:51.251Z

## Learnings

<!-- Append learnings below -->

### Multi-monitor dock tests (2026-03-10)
- Test project: `Tests/Microsoft.CmdPal.UI.ViewModels.UnitTests` — MSTest + Moq, references ViewModels project
- Added `DockMultiMonitorTests.cs` with 27 tests covering `ScreenRect`, `MonitorInfo`, `DockMonitorConfig.ResolveSide`, `DockSettings` defaults, effective-config fallback logic, and `DockBandSettings` resolve pattern
- `DockWindowManager` is not directly unit-testable (creates `DockWindow` UI objects); tested the pure data logic it depends on instead
- Pre-existing build errors in ViewModels project (`CS0169` in SettingsViewModel, `SA1512` in DockMonitorConfigViewModel) block full test build — not caused by test code
- Pattern: `record struct ScreenRect` and `sealed record MonitorInfo` support value equality out of the box — good for assertions

## Session Work (2026-03-10T15:43Z)

**Task:** Write unit tests for multi-monitor dock types  
**Outcome:** Created DockMultiMonitorTests.cs with 27 tests covering ScreenRect, MonitorInfo, DockMonitorConfig.ResolveSide, DockSettings defaults, and DockBandSettings. Tests compile clean. Identified and reported pre-existing CS0169/SA1512 build errors; Morpheus fixed these. All 27 tests pass.

**Cross-agent awareness:**
- Morpheus implemented DockMonitorConfig mutable-class pattern and ResolveSide() method; tests validated the effective-config fallback chain
- Trinity added UI bindings for the 5-item SideOverrideIndex ComboBox (0–4 index mapping)
- Neo enforced AOT discipline and reviewed test code for LINQ violations


