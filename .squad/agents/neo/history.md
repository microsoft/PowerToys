# Neo — History

## Core Context

- **Project:** PowerToys Command Palette — a WinUI 3 extensible command launcher with C++/WinRT SDK and 21 built-in extensions
- **Role:** Lead
- **Joined:** 2026-03-10T03:10:51.238Z

## Learnings

<!-- Append learnings below -->

### Multi-Monitor Dock Architecture Review (2025-07)

**Architecture:**
- Multi-monitor dock uses a `DockWindowManager` → per-monitor `DockWindow` pattern
- `IMonitorService` (ViewModel layer) / `MonitorService` (UI layer via Win32 `EnumDisplayMonitors`)
- `DockMonitorConfig` provides per-monitor enable/side-override, falls back to global `DockSettings.Side`
- `DockWindowManager` lives in ShellPage, wired via `ShowHideDockMessage`

**Key file paths:**
- `src/modules/cmdpal/Microsoft.CmdPal.UI/Dock/DockWindowManager.cs` — multi-monitor orchestrator
- `src/modules/cmdpal/Microsoft.CmdPal.UI/Services/MonitorService.cs` — Win32 monitor enumeration
- `src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/Models/IMonitorService.cs` — interface
- `src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/Models/MonitorInfo.cs` — record + ScreenRect
- `src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/Settings/DockSettings.cs` — DockMonitorConfig class

**Fixes applied:**
- Marked `DockWindowManager` as `partial` (CsWinRT1028 AOT warning)
- Replaced LINQ `.Where().ToList()` with manual loop (AOT safety)
- Added `List<DockMonitorConfig>` to `JsonSerializationContext` for explicit AOT serialization

**Patterns:**
- This project is AOT-compiled — avoid LINQ in new code
- `NativeMethods.txt` declares Win32 APIs for CsWin32 source generator
- DI registration of `IMonitorService` is in `App.xaml.cs` → `AddUIServices()`
- `DockWindow` parameterless ctor is required by WinUI; the `MonitorInfo` ctor chains via `: this()`
