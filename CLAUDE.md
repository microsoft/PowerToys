# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Response language

Always respond in Chinese (中文).

This repo is **Microsoft PowerToys** – a Windows-only collection of 30+ productivity utilities. Mostly C++ and C# (WinUI 3 / WPF). Windows-only build (Visual Studio 2022 17.4+). See [AGENTS.md](AGENTS.md) for the full contributor guide; this file highlights the essentials.

## Build

One-terminal rule: don't switch terminals mid build→test flow. `cd` into the changed project's folder (the one with the `.csproj`/`.vcxproj`) before building.

| Task | Command |
|------|---------|
| First build / NuGet restore / essentials (runner + settings) | `tools\build\build-essentials.cmd` |
| Build any project or solution in current folder | `tools\build\build.cmd` |
| Build with explicit options | `tools\build\build.ps1 -Platform x64 -Configuration Release` |
| Restore only | `tools\build\build.ps1 -RestoreOnly` |
| Full installer pipeline (rarely needed) | `tools\build\build-installer.ps1` |

Scripts auto-detect platform (x64/arm64) and initialize the VS Dev environment. **Exit code 0 = success; treat non-zero as absolute failure** — do not run tests or launch the runner until the build succeeds.

On failure, logs are written next to the solution/project:
- `build.<config>.<platform>.errors.log` — read this first
- `build.<config>.<platform>.all.log` — full log
- `build.<config>.<platform>.trace.binlog` — MSBuild Structured Log Viewer

Submodules must be initialized once: `git submodule update --init --recursive`.

## Tests

- Test projects are named `<Product>*UnitTests` or `<Product>*UITests`, typically as siblings or 1–2 levels up from the product code (e.g., `FancyZones`, `AdvancedPaste`).
- **Avoid `dotnet test`** in this repo. Use VS Test Explorer (`Ctrl+E, T`) or `vstest.console.exe` with filters.
- Always **build the test project first** (exit code 0) before running.
- UI tests need WinAppDriver v1.2.1 + Developer Mode. Fuzz tests use OneFuzz + .NET 8 (required for new modules that handle file I/O or user input).
- Mouse Without Borders requires 2+ physical machines (not VMs). Multi-monitor utilities need 2+ monitors with mixed DPI.

## Architecture

PowerToys is a **Runner + Modules + Settings UI** system. Understanding which layer you're in matters because the rules differ.

- **`src/runner/`** (C++) — `PowerToys.exe`: tray icon, module loader, hotkey broker, update/elevation workflow, IPC bridge to Settings UI. Module discovery lives in `src/runner/main.cpp` — update it when adding/removing modules.
- **`src/settings-ui/`** (C#, WinUI/WPF) — The configuration app. Talks to the Runner over **named pipes using JSON messages**. Any change to an IPC message shape or persisted settings schema must be mirrored on both sides in the same PR, with a migration when persisted shape changes.
- **`src/modules/<Name>/`** — Each utility. Every module implements the module interface (`src/modules/interface/`) so the Runner can enable/disable it, load hotkeys, and read/write settings. Four module flavors exist:
  1. **Simple** (e.g., Find My Mouse) — all logic inside the interface DLL.
  2. **External app launcher** (e.g., Color Picker) — interface DLL spawns a separate process, IPC via named pipes.
  3. **Context handler / shell extension** (e.g., PowerRename) — File Explorer integration, Win11 context menu via MSIX.
  4. **Registry-based** (e.g., Power Preview) — registers preview/thumbnail handlers, toggles registry keys on enable/disable.
- **`src/common/`** (C++) — Shared code: spdlog-based logging, named-pipe IPC primitives (`two_way_pipe_message_ipc.h`), JSON helpers, DPI, telemetry. **Treat as ABI-sensitive**: grep the repo for callers before changing a public header, and watch hot paths — no logging in hooks or tight loops.
- **`installer/`** — WiX-based installers. Changes here have release impact.
- **`src/gpo/`** — Group Policy definitions. Elevation and GPO logic warrants extra care.

Other top-level `src/` folders worth knowing: `logging/`, `dsc/` (PowerShell DSC), `codeAnalysis/`, `Monaco/`, `ActionRunner/`, `Update/`.

### Cross-cutting concerns

- **IPC contract changes**: update `src/runner/` and `src/settings-ui/` in the same PR, preserve backward compatibility where possible, add migration for persisted settings shape changes.
- **Resource files**: WPF uses `.resx`; WinUI 3 uses `.resw`; C++ modules need `.resx → .rc` conversion (see `tools/build/convert-resx-to-rc.ps1`). PRI file names must be overridden to avoid flattening conflicts.
- **New third-party deps**: require PM approval and a `NOTICE.md` update. Must be MIT-licensed or approved.

## Style

- **C#** — `src/.editorconfig` + StyleCop.Analyzers.
- **C++** — `src/.clang-format`; Modern C++ / Core Guidelines.
- **XAML** — XamlStyler (`.\.pipelines\applyXamlStyling.ps1 -Main`).
- **Logging** — C++ uses spdlog (`Logger::info/warn/error/debug`, init via `init_logger()`). Keep hot paths quiet.

## Key rules (from AGENTS.md)

- **Atomic PRs**: one logical change, no drive-by refactors.
- **Ask before** touching: installer, elevation, update workflow, GPO/policy handling, third-party deps, serialization formats, `src/common/` public APIs.
- **Don't** merge incomplete features to main, break IPC contracts without updating both sides, add noisy logs in hot paths.

## Component-specific instructions (auto-applied by path)

- [Runner & Settings UI](.github/instructions/runner-settings-ui.instructions.md) — IPC contracts, schema migrations.
- [Common Libraries](.github/instructions/common-libraries.instructions.md) — ABI stability.

## Module-specific notes

### PowerDisplay windowing (flyout + IdentifyWindow)

混合 DPI 多屏下的窗口定位约定（2026-04 修复）：

- **坐标一律用绝对虚屏物理像素**（`WorkArea.X/Y + …`），配**单参** `AppWindow.MoveAndResize(rect)`。
- **不要用** `AppWindow.MoveAndResize(rect, displayArea)` 双参重载——其坐标语义未公开文档化，历史上踩过"副屏位置错位"的坑。
- **DPI 换算用目标屏** `GetDpiForMonitor`（不用当前窗口 DPI），`DpiSuppressor` 抑制 `WM_DPICHANGED` 防双重缩放。
- `WindowEx` 在 XAML 设 `MinWidth="0" MinHeight="0"`，避免跨 DPI 移动时最小尺寸夹持。
- 参考模式：CmdPal 的 `WindowPositionHelper` + `MoveAndResizeDpiAware`。
- 调试指南：[doc/devdocs/modules/powerdisplay/flyout-positioning-debug.md](doc/devdocs/modules/powerdisplay/flyout-positioning-debug.md) —— 混合 DPI 环境下验证步骤、`[FlyoutPos]` 日志字段含义、判定准则、异常模式对照表。

## Further reading

- [AGENTS.md](AGENTS.md) — full contributor guide (authoritative)
- [doc/devdocs/core/architecture.md](doc/devdocs/core/architecture.md) — module types and runner responsibilities
- [doc/devdocs/development/style.md](doc/devdocs/development/style.md) — coding style details
- [doc/devdocs/development/logging.md](doc/devdocs/development/logging.md) — logging conventions
- [tools/build/BUILD-GUIDELINES.md](tools/build/BUILD-GUIDELINES.md) — build script reference
- [doc/devdocs/modules/powerdisplay/flyout-positioning-debug.md](doc/devdocs/modules/powerdisplay/flyout-positioning-debug.md) — PowerDisplay 定位调试指南
