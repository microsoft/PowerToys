---
description: 'PowerToys AI contributor guidance'
---

# PowerToys – Copilot Instructions

PowerToys is a collection of Windows productivity utilities. The repo is a mixed C++ / C# / WinUI 3 codebase targeting Windows 10 1803+.

## Build

**Prerequisites:** Visual Studio 2022 17.4+ (or VS 2026), then once: `git submodule update --init --recursive`

```powershell
# First build or after NuGet changes — restores packages and builds essentials
tools\build\build-essentials.cmd

# Build the project you changed (cd to its folder first)
cd src\modules\fancyzones\FancyZonesLib
tools\build\build.cmd

# Build with explicit platform/config
tools\build\build.ps1 -Platform x64 -Configuration Release
```

On failure check `build.<config>.<platform>.errors.log` next to the project.

## Tests

**Do not use `dotnet test`** — use VS Test Explorer (`Ctrl+E, T`) or `vstest.console.exe`.

Test projects live as siblings or 1-2 levels up from the product code, named `<Product>*UnitTests` (e.g., `src/modules/AdvancedPaste/AdvancedPaste.UnitTests/`). Build the test project before running.

```powershell
# Run a single test by fully qualified name
vstest.console.exe <path-to-test.dll> /Tests:MyTestMethod

# Run tests matching a filter
vstest.console.exe <path-to-test.dll> /TestCaseFilter:"FullyQualifiedName~FancyZones"
```

Test framework: **MSTest**. Mocking: **Moq**.

## Formatting & Style

| Language | Config | Format command |
|----------|--------|----------------|
| C# | `src/.editorconfig` + StyleCop.Analyzers | IDE auto-format (4-space indent, block-scoped namespaces, PascalCase types/methods) |
| C++ | `src/.clang-format` | `src\codeAnalysis\format_sources.ps1` (auto-detects git-modified files) |
| XAML | `src/Settings.XamlStyler` | `.\.pipelines\applyXamlStyling.ps1 -Main` |

C++ follows [C++ Core Guidelines](https://github.com/isocpp/CppCoreGuidelines). New code uses Modern C++; modified code matches surrounding style.

## Architecture

**Runner** (`src/runner/`) — the main process. Loads module DLLs, manages the tray icon, and handles global hotkeys. Communicates with Settings UI over **Windows Named Pipes** using JSON messages.

**Settings UI** (`src/settings-ui/`) — WinUI 3 / WPF app for configuration. Changes to IPC/JSON contracts must update both Runner and Settings UI in the same PR.

**Modules** (`src/modules/<name>/`) — each utility is a separate module implementing the `PowertoyModuleIface` C++ interface (create → enable → get/set config → disable → destroy). Module types:
- **Simple** — self-contained DLL loaded by Runner
- **External launcher** — DLL that spawns a separate app process
- **Context handler** — shell extension (registered via registry)

**Common libraries** (`src/common/`) — shared logging, IPC, settings, DPI, telemetry. Changes here affect the entire repo; search all callers before modifying public APIs.

## Key Conventions

- **Atomic PRs**: one logical change per PR, no drive-by refactors
- **Add tests** when changing behavior; if skipped, state why
- **No logging in hot paths** (hooks, timers, tight loops)
- **New dependencies** require MIT license (or PM approval) and a `NOTICE.md` update
- **Settings schema changes** need migration logic and serialization tests
- **Modules with file I/O or user input** must implement fuzzing tests
- **C++ resource conversion**: C++ modules convert `.resx` → `.rc`; WinUI 3 uses `.resw`

## Logging

**C++:** `spdlog` via `init_logger()` early in startup, then `Logger::info/warn/error/debug`.

**C#:** `ManagedCommon.Logger.InitializeLogger("\\ModuleName\\Logs")`, then `Logger.LogError/Warning/Info/Debug/Trace()`. Use the second parameter `true` for low-privilege Explorer extensions.

Logs: `%LOCALAPPDATA%\Microsoft\PowerToys\Logs`

## Sensitive Areas

| Area | Concern |
|------|---------|
| `src/common/` | ABI breaks — update all callers |
| `src/runner/` ↔ `src/settings-ui/` | IPC contract — keep both sides in sync |
| Installer (`installer/`) | Release impact — careful review |
| Elevation / GPO logic | Security — confirm no policy regression |

## Further Reading

- [AGENTS.md](../AGENTS.md) — full AI contributor guide with validation checklist
- [Architecture](../doc/devdocs/core/architecture.md) — module types and runtime flow
- [Build Guidelines](../tools/build/BUILD-GUIDELINES.md) — build scripts and logs
- [Coding Style](../doc/devdocs/development/style.md) — formatting details
