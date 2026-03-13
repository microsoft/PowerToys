# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Microsoft PowerToys** is a collection of utilities for power users to tune and streamline their Windows experience. The codebase includes 25+ utilities like FancyZones, PowerRename, Image Resizer, Command Palette, Keyboard Manager, and more.

## Build Commands

### Prerequisites
- Visual Studio 2022 17.4+
- Windows 10 1803+ (April 2018 Update or newer)
- Initialize submodules once: `git submodule update --init --recursive`
- Run automated setup: `.\tools\build\setup-dev-environment.ps1`

### Common Build Commands

| Task | Command |
|------|---------|
| First build / NuGet restore | `tools\build\build-essentials.cmd` |
| Build current folder | `tools\build\build.cmd` |
| Build with options | `.\tools\build\build.ps1 -Platform x64 -Configuration Release` |
| Build full solution | Open `PowerToys.slnx` in VS and build |
| Build installer (Release only) | `.\tools\build\build-installer.ps1 -Platform x64 -Configuration Release` |

**Important Build Rules:**
- Exit code 0 = success; non-zero = failure
- On failure, check `build.<config>.<platform>.errors.log` next to the solution/project
- For first build or missing NuGet packages, run `build-essentials.cmd` first
- Use one terminal per operation (build → test). Don't switch terminals mid-flow
- After making changes, `cd` to the project folder (`.csproj`/`.vcxproj`) before building

### VS Code Tasks
- Use `PT: Build Essentials (quick)` for fast runner + settings build
- Use `PT: Build (quick)` to build the current directory

## Testing

### Finding Tests
- Test projects follow the pattern: `<Product>*UnitTests`, `<Product>*UITests`, or `<Product>*FuzzTests`
- Located as sibling folders or 1-2 levels up from product code
- Examples: `src/modules/imageresizer/tests/ImageResizer.UnitTests.csproj`

### Running Tests
1. **Build the test project first**, wait for exit code 0
2. Run via VS Test Explorer (`Ctrl+E, T`) or `vstest.console.exe` with filters
3. **Avoid `dotnet test`** - use VS Test Explorer or vstest.console.exe

### Test Types
- **Unit Tests**: Standard dev environment, no extra setup
- **UI Tests**: Require WinAppDriver v1.2.1 and Developer Mode ([download](https://github.com/microsoft/WinAppDriver/releases/tag/v1.2.1))
- **Fuzz Tests**: OneFuzz + .NET 8, required for modules handling file I/O or user input

### Test Discipline
- Add or adjust tests when changing behavior
- New modules handling file I/O or user input **must** implement fuzzing tests
- State why tests were skipped if applicable (e.g., comment-only change)

## Architecture

### Repository Structure

```
src/
├── runner/           # Main PowerToys.exe, tray icon, module loader, hotkey management
├── settings-ui/      # WinUI configuration app (communicates via named pipes)
├── modules/          # Individual utilities (each in subfolder)
│   ├── AdvancedPaste/
│   ├── fancyzones/
│   ├── imageresizer/
│   ├── keyboardmanager/
│   ├── launcher/     # PowerToys Run
│   └── ...
├── common/           # Shared code: logging, IPC, settings, DPI, telemetry
└── dsc/              # Desired State Configuration support

tools/build/          # Build scripts and automation
doc/devdocs/          # Developer documentation
installer/            # WiX-based installer projects
```

### Module Types

1. **Simple Modules** (e.g., Mouse Pointer Crosshairs, Find My Mouse)
   - Entirely contained in the module interface DLL
   - No external application

2. **External Application Launchers** (e.g., Color Picker)
   - Start a separate application (often WPF/WinUI)
   - Handle hotkey events
   - Communicate via named pipes or IPC

3. **Context Handler Modules** (e.g., PowerRename, Image Resizer)
   - Shell extensions for File Explorer
   - Add right-click context menu entries

4. **Registry-based Modules** (e.g., Power Preview)
   - Register preview handlers and thumbnail providers
   - Modify registry during enable/disable

### Module Interface

All PowerToys modules implement a standardized interface (`src/modules/interface/`) that defines:
- Hotkey structure
- Name and key for the utility
- Enable/disable functionality
- Configuration management
- Telemetry settings
- GPO configuration

### Settings System

- **Runner** (`src/runner/`) loads modules and manages their lifecycle
- **Settings UI** (`src/settings-ui/`) is a separate process using WinUI 3
- Communication via **named pipes** (IPC) between runner and settings
- Settings stored as JSON files in `%LOCALAPPDATA%\Microsoft\PowerToys\`
- Schema migrations must maintain backward compatibility

**Important**: When modifying IPC contracts or JSON schemas:
- Update both runner and settings-ui
- Maintain backward compatibility
- See [doc/devdocs/core/settings/runner-ipc.md](doc/devdocs/core/settings/runner-ipc.md)

## Development Workflow

### Making Changes

1. **Before starting**: Ensure there's an issue to track the work
2. **Read the file first**: Always use Read tool before modifying files
3. **Follow existing patterns**: Match the style and structure of surrounding code
4. **Atomic PRs**: One logical change per PR, no drive-by refactors
5. **Build discipline**:
   - `cd` to project folder after making changes
   - Build using `tools/build/build.cmd`
   - Wait for exit code 0 before proceeding
6. **Test changes**: Build and run tests for affected modules
7. **Update signing**: Add new DLLs/executables to `.pipelines/ESRPSigning_core.json`

### CLI Tools

Several modules now have CLI support (FancyZones, Image Resizer, File Locksmith):
- Use **System.CommandLine** library for argument parsing
- Follow `--kebab-case` for long options, `-x` for short
- Exit codes: 0 = success, non-zero = failure
- Log to both console and file using `ManagedCommon.Logger`
- Reference: [doc/devdocs/cli-conventions.md](doc/devdocs/cli-conventions.md)

### Localization

- Localization is handled exclusively by internal Microsoft team
- **Do not** submit PRs for localization changes
- File issues for localization bugs instead

## Code Style and Conventions

### Style Enforcement

- **C#**: Use `src/.editorconfig` and StyleCop.Analyzers (enforced in build)
- **C++**: Use `.clang-format` (press `Ctrl+K Ctrl+D` in VS to format)
- **XAML**: Use XamlStyler (`.\.pipelines\applyXamlStyling.ps1 -Main`)

### Formatting

- Follow existing patterns in the file you're editing
- For new code, follow Modern C++ practices and [C++ Core Guidelines](https://github.com/isocpp/CppCoreGuidelines)
- C++ formatting script: `src/codeAnalysis/format_sources.ps1`

### Logging

- **C++**: Use spdlog (SPD logs) via `src/common/logger/`
- **C#**: Use `ManagedCommon.Logger`
- **Critical**: Keep hot paths quiet (no logging in hooks or tight loops)
- Detailed guidance: [doc/devdocs/development/logging.md](doc/devdocs/development/logging.md)

### Dependencies

- MIT license generally acceptable; other licenses require PM approval
- All external packages must be listed in `NOTICE.md`
- Update `Directory.Packages.props` for NuGet packages (centralized package management)
- Sign new DLLs by adding to signing config

## Critical Areas Requiring Extra Care

| Area | Concern | Reference |
|------|---------|-----------|
| `src/common/` | ABI breaks affect all modules | [.github/instructions/common-libraries.instructions.md](.github/instructions/common-libraries.instructions.md) |
| `src/runner/`, `src/settings-ui/` | IPC contracts, schema migrations | [.github/instructions/runner-settings-ui.instructions.md](.github/instructions/runner-settings-ui.instructions.md) |
| Installer files | Release impact | Careful review required |
| Elevation/GPO logic | Security implications | Confirm no policy handling regression |

## Key Development Rules

### Do
- Add tests when changing behavior
- Follow existing code patterns
- Use atomic PRs (one logical change)
- Ask for clarification when spec is ambiguous
- Check exit codes (`0` = success)
- Read files before modifying them
- Update `NOTICE.md` when adding dependencies

### Don't
- Don't break IPC/JSON contracts without updating both runner and settings-ui
- Don't add noisy logs in hot paths (hooks, tight loops)
- Don't introduce third-party dependencies without PM approval
- Don't merge incomplete features into main (use feature branches)
- Don't use `dotnet test` (use VS Test Explorer or vstest.console.exe)
- Don't skip hooks (--no-verify) unless explicitly requested

## Special Testing Requirements

- **Mouse Without Borders**: Requires 2+ physical computers (not VMs)
- **Multi-monitor utilities**: Test with 2+ monitors, different DPI settings
- **File I/O or user input modules**: Must implement fuzzing tests

## Running PowerToys

### Debug Build
- After building, run `x64\Debug\PowerToys.exe` directly
- Some modules (PowerRename, ImageResizer, File Explorer extensions) require full installation

### Release Build
- Build the installer: `.\tools\build\build-installer.ps1 -Platform x64 -Configuration Release`
- Install from `installer\` output folder

## Common Issues

### Build Failures
1. Check `build.<config>.<platform>.errors.log`
2. Ensure submodules are initialized: `git submodule update --init --recursive`
3. Run `build-essentials.cmd` to restore NuGet packages
4. Check Visual Studio has required workloads (import `.vsconfig`)

### Missing DLLs at Runtime
- Some modules require installation via the installer to register COM handlers/shell extensions
- Build and install from `installer/` folder

## Documentation Index

### Essential Reading
- [Architecture Overview](doc/devdocs/core/architecture.md)
- [Coding Guidelines](doc/devdocs/development/guidelines.md)
- [Coding Style](doc/devdocs/development/style.md)
- [Build Guidelines](tools/build/BUILD-GUIDELINES.md)
- [Module Interface](doc/devdocs/modules/interface.md)

### Advanced Topics
- [Runner](doc/devdocs/core/runner.md)
- [Settings System](doc/devdocs/core/settings/readme.md)
- [Logging](doc/devdocs/development/logging.md)
- [UI Tests](doc/devdocs/development/ui-tests.md)
- [Fuzzing Tests](doc/devdocs/tools/fuzzingtesting.md)
- [Installer](doc/devdocs/core/installer.md)

### Module-Specific Docs
- Individual modules: `doc/devdocs/modules/<module-name>.md`
- PowerToys Run plugins: `doc/devdocs/modules/launcher/plugins/`

## Validation Checklist

Before finishing work:
- [ ] Build clean with exit code 0
- [ ] Tests updated and passing locally
- [ ] No unintended ABI breaks or schema changes
- [ ] IPC contracts consistent between runner and settings-ui
- [ ] New dependencies added to `NOTICE.md`
- [ ] New binaries added to signing config (`.pipelines/ESRPSigning_core.json`)
- [ ] PR is atomic (one logical change), with issue linked
- [ ] Code follows existing patterns and style guidelines
