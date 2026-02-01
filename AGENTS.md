---
description: 'Top-level AI contributor guidance for developing PowerToys - a collection of Windows productivity utilities'
applyTo: '**'
---

# PowerToys – AI Contributor Guide

This is the top-level guidance for AI contributions to PowerToys. Keep changes atomic, follow existing patterns, and cite exact paths in PRs.

## Overview

PowerToys is a set of utilities for power users to tune and streamline their Windows experience.

| Area | Location | Description |
|------|----------|-------------|
| Runner | `src/runner/` | Main executable, tray icon, module loader, hotkey management |
| Settings UI | `src/settings-ui/` | WinUI/WPF configuration app communicating via named pipes |
| Modules | `src/modules/` | Individual PowerToys utilities (each in its own subfolder) |
| Common Libraries | `src/common/` | Shared code: logging, IPC, settings, DPI, telemetry, utilities |
| Build Tools | `tools/build/` | Build scripts and automation |
| Documentation | `doc/devdocs/` | Developer documentation |
| Installer | `installer/` | WiX-based installer projects |

For architecture details and module types, see [Architecture Overview](doc/devdocs/core/architecture.md).

## Conventions

For detailed coding conventions, see:
- [Coding Guidelines](doc/devdocs/development/guidelines.md) – Dependencies, testing, PR management
- [Coding Style](doc/devdocs/development/style.md) – Formatting, C++/C#/XAML style rules
- [Logging](doc/devdocs/development/logging.md) – C++ spdlog and C# Logger usage

### Component-Specific Instructions

These instruction files are automatically applied when working in their respective areas:
- [Runner & Settings UI](.github/instructions/runner-settings-ui.instructions.md) – IPC contracts, schema migrations
- [Common Libraries](.github/instructions/common-libraries.instructions.md) – ABI stability, shared code guidelines

## Build

### Prerequisites

- Visual Studio 2022 17.4+ or Visual Studio 2026
- Windows 10 1803+ (April 2018 Update or newer)
- Initialize submodules once: `git submodule update --init --recursive`

### Build Commands

| Task | Command |
|------|---------|
| First build / NuGet restore | `tools\build\build-essentials.cmd` |
| Build current folder | `tools\build\build.cmd` |
| Build with options | `build.ps1 -Platform x64 -Configuration Release` |

### Build Discipline

1. One terminal per operation (build → test). Do not switch or open new ones mid-flow
2. After making changes, `cd` to the project folder that changed (`.csproj`/`.vcxproj`)
3. Use scripts to build: `tools/build/build.ps1` or `tools/build/build.cmd`
4. For first build or missing NuGet packages, run `build-essentials.cmd` first
5. **Exit code 0 = success; non-zero = failure** – treat this as absolute
6. On failure, read the errors log: `build.<config>.<platform>.errors.log`
7. Do not start tests or launch Runner until the build succeeds

### Build Logs

Located next to the solution/project being built:
- `build.<configuration>.<platform>.errors.log` – errors only (check this first)
- `build.<configuration>.<platform>.all.log` – full log
- `build.<configuration>.<platform>.trace.binlog` – for MSBuild Structured Log Viewer

For complete details, see [Build Guidelines](tools/build/BUILD-GUIDELINES.md).

## Tests

### Test Discovery

- Find test projects by product code prefix (e.g., `FancyZones`, `AdvancedPaste`)
- Look for sibling folders or 1-2 levels up named `<Product>*UnitTests` or `<Product>*UITests`

### Running Tests

1. **Build the test project first**, wait for exit code 0
2. Run via VS Test Explorer (`Ctrl+E, T`) or `vstest.console.exe` with filters
3. **Avoid `dotnet test`** in this repo – use VS Test Explorer or vstest.console.exe

### Test Types

| Type | Requirements | Setup |
|------|--------------|-------|
| Unit Tests | Standard dev environment | None |
| UI Tests | WinAppDriver v1.2.1, Developer Mode | Install from [WinAppDriver releases](https://github.com/microsoft/WinAppDriver/releases/tag/v1.2.1) |
| Fuzz Tests | OneFuzz, .NET 8 | See [Fuzzing Tests](doc/devdocs/tools/fuzzingtesting.md) |

### Test Discipline

1. Add or adjust tests when changing behavior
2. If tests skipped, state why (e.g., comment-only change, string rename)
3. New modules handling file I/O or user input **must** implement fuzzing tests

### Special Requirements

- **Mouse Without Borders**: Requires 2+ physical computers (not VMs)
- **Multi-monitor utilities**: Test with 2+ monitors, different DPI settings

For UI test setup details, see [UI Tests](doc/devdocs/development/ui-tests.md).

## Boundaries

### Ask for Clarification When

- Ambiguous spec after scanning relevant docs
- Cross-module impact (shared enum/struct) is unclear
- Security, elevation, or installer changes involved
- GPO or policy handling modifications needed

### Areas Requiring Extra Care

| Area | Concern | Reference |
|------|---------|-----------|
| `src/common/` | ABI breaks | [Common Libraries Instructions](.github/instructions/common-libraries.instructions.md) |
| `src/runner/`, `src/settings-ui/` | IPC contracts, schema | [Runner & Settings UI Instructions](.github/instructions/runner-settings-ui.instructions.md) |
| Installer files | Release impact | Careful review required |
| Elevation/GPO logic | Security | Confirm no regression in policy handling |

### What NOT to Do

- Don't merge incomplete features into main (use feature branches)
- Don't break IPC/JSON contracts without updating both runner and settings-ui
- Don't add noisy logs in hot paths
- Don't introduce third-party deps without PM approval and `NOTICE.md` update

## Validation Checklist

Before finishing, verify:

- [ ] Build clean with exit code 0
- [ ] Tests updated and passing locally
- [ ] No unintended ABI breaks or schema changes
- [ ] IPC contracts consistent between runner and settings-ui
- [ ] New dependencies added to `NOTICE.md`
- [ ] PR is atomic (one logical change), with issue linked

## Documentation Index

### Core Architecture
- [Architecture Overview](doc/devdocs/core/architecture.md)
- [Runner](doc/devdocs/core/runner.md)
- [Settings System](doc/devdocs/core/settings/readme.md)
- [Module Interface](doc/devdocs/modules/interface.md)

### Development
- [Coding Guidelines](doc/devdocs/development/guidelines.md)
- [Coding Style](doc/devdocs/development/style.md)
- [Logging](doc/devdocs/development/logging.md)
- [UI Tests](doc/devdocs/development/ui-tests.md)
- [Fuzzing Tests](doc/devdocs/tools/fuzzingtesting.md)

### Build & Tools
- [Build Guidelines](tools/build/BUILD-GUIDELINES.md)
- [Tools Overview](doc/devdocs/tools/readme.md)

### Instructions (Auto-Applied)
- [Runner & Settings UI](.github/instructions/runner-settings-ui.instructions.md)
- [Common Libraries](.github/instructions/common-libraries.instructions.md)
