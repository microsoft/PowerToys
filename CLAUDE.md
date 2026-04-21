# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Communication

Always respond in Chinese (中文).

## Project Overview

PowerToys is a collection of Windows productivity utilities written in C++ and C#. The main solution is `PowerToys.slnx`. Each utility is a "module" loaded by the Runner via a standardized DLL interface.

## Architecture

- **Runner** (`src/runner/`): Main executable (PowerToys.exe). Loads module DLLs, manages hotkeys, shows tray icon, bridges modules to Settings UI via named pipes (JSON IPC).
- **Settings UI** (`src/settings-ui/`): WinUI/WPF configuration app. Communicates with Runner over named pipes. Changes to IPC contracts must update both sides in the same PR.
- **Modules** (`src/modules/`): ~30 individual utilities, each implementing the module interface (`src/modules/interface/`). Four module types: simple (self-contained DLL), external app launcher (separate process + IPC), context handler (shell extension), registry-based (preview handlers).
- **Common** (`src/common/`): Shared libraries — logging, IPC, settings serialization, DPI utilities, telemetry, JSON/string helpers. Changes here affect the entire codebase.
- **Installer** (`installer/`): WiX-based installer projects. Separate solution at `installer/PowerToysSetup.slnx`.

## Build Commands

Prerequisites: Visual Studio 2022 17.4+ or VS 2026, Windows 10 1803+. Initialize submodules once: `git submodule update --init --recursive`.

| Task | Command |
|------|---------|
| First build / NuGet restore | `tools\build\build-essentials.cmd` |
| Build current folder's project | `cd` to the `.csproj`/`.vcxproj` folder, then `tools\build\build.cmd` |
| Build with options | `tools\build\build.ps1 -Platform x64 -Configuration Release` |
| Full installer build | `tools\build\build-installer.ps1 -Platform x64 -Configuration Release -PerUser true -InstallerSuffix wix5` |
| Format XAML | `.\.pipelines\applyXamlStyling.ps1 -Main` |
| Format C++ | `src\codeAnalysis\format_sources.ps1` (formats git-modified files) |

Build logs appear next to the solution/project: `build.<config>.<platform>.errors.log` (check first), `.all.log`, `.trace.binlog`.

## Testing

- **Do NOT use `dotnet test`** — use VS Test Explorer (`Ctrl+E, T`) or `vstest.console.exe` with filters.
- Build the test project first (exit code 0) before running tests.
- Test projects are named `<Product>*UnitTests` or `<Product>*UITests`, typically sibling folders or 1-2 levels up from the product code.
- UI Tests require WinAppDriver v1.2.1 and Developer Mode enabled.

## Style and Formatting

- **C++**: `src/.clang-format` — auto-format with `Ctrl+K Ctrl+D` in VS or run `format_sources.ps1`.
- **C#**: `src/.editorconfig` + StyleCop.Analyzers.
- **XAML**: XamlStyler — VS extension or `applyXamlStyling.ps1`.
- Follow existing style in modified files. New code should follow Modern C++ / C++ Core Guidelines.

## Logging

- **C++**: spdlog via `init_logger()`. Include `spdlog.props` in `.vcxproj`. Use `Logger::info/warn/error/debug`.
- **C#**: `ManagedCommon.Logger`. Call `Logger.InitializeLogger("\\Module\\Logs")` at startup. Use `Logger.LogInfo/LogWarning/LogError/LogDebug`.
- Log files: `%LOCALAPPDATA%\Microsoft\PowerToys\Logs`. Low-privilege processes use `%USERPROFILE%\AppData\LocalLow\Microsoft\PowerToys`.
- No logging in hot paths (hooks, tight loops, timers).

## Key Constraints

- Atomic PRs: one logical change per PR, no drive-by refactors.
- IPC/JSON contract changes must update both `src/runner/` and `src/settings-ui/` together.
- Changes to `src/common/` public APIs: grep the entire codebase for usages and update all callers.
- New third-party dependencies must be MIT-licensed (or PM-approved) and added to `NOTICE.md`.
- Settings schema changes require migration logic and serialization tests.
- New modules with file I/O or user input must include fuzzing tests.

## Documentation

- [Architecture](doc/devdocs/core/architecture.md), [Runner](doc/devdocs/core/runner.md), [Settings](doc/devdocs/core/settings/readme.md)
- [Coding Guidelines](doc/devdocs/development/guidelines.md), [Style](doc/devdocs/development/style.md), [Logging](doc/devdocs/development/logging.md)
- [Build Guidelines](tools/build/BUILD-GUIDELINES.md), [Module Interface](doc/devdocs/modules/interface.md)
- [AGENTS.md](AGENTS.md) — full AI contributor guide with detailed conventions
