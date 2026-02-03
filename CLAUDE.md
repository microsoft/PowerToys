# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

| Task | Command |
|------|---------|
| First build / NuGet restore | `tools\build\build-essentials.cmd` |
| Build current folder | `tools\build\build.cmd` |
| Build with options | `tools\build\build.ps1 -Platform x64 -Configuration Release` |

**Build discipline:**
- `cd` to the project folder (`.csproj`/`.vcxproj`) before building
- Exit code 0 = success; non-zero = failure
- On failure, check `build.<config>.<platform>.errors.log`
- Run `build-essentials.cmd` first if NuGet packages are missing

## Running Tests

- Build test project first, wait for exit code 0
- Run via VS Test Explorer (`Ctrl+E, T`) or `vstest.console.exe`
- **Avoid `dotnet test`** - use VS Test Explorer or vstest.console.exe
- Test projects are named `<Product>*UnitTests` or `<Product>*UITests`
- UI tests require WinAppDriver v1.2.1 and Developer Mode enabled

## Architecture

PowerToys is a collection of Windows productivity utilities with a modular architecture.

| Component | Location | Purpose |
|-----------|----------|---------|
| Runner | `src/runner/` | Main executable - module loader, tray icon, hotkey management |
| Settings UI | `src/settings-ui/` | WinUI/WPF configuration app |
| Modules | `src/modules/` | Individual utilities (30+ modules) |
| Common Libraries | `src/common/` | Shared code: logging, IPC, settings, utilities |

**Module types:**
1. Simple modules - entirely in module interface DLL (e.g., Mouse Pointer Crosshairs)
2. External application launchers - start separate executables (e.g., Color Picker)
3. Context handler modules - File Explorer shell extensions (e.g., Power Rename)
4. Registry-based modules - preview/thumbnail handlers (e.g., Power Preview)

**IPC:** Runner ↔ Settings UI communicate via Windows Named Pipes using JSON messages.

## Critical Guidelines

**IPC contracts:** When modifying JSON message format between Runner and Settings UI:
- Update both `src/runner/` and `src/settings-ui/` in the same PR
- Add migration logic for settings schema changes
- Test both directions of communication

**Common libraries (`src/common/`):** Changes have wide impact:
- Avoid breaking public headers/APIs; update all callers if changed
- No logging in hot paths (hooks, timers, tight loops)
- New dependencies require PM approval and must be added to `NOTICE.md`

**Style enforcement:**
- C++: `src/.clang-format` (run `src/codeAnalysis/format_sources.ps1`)
- C#: `src/.editorconfig` with StyleCop.Analyzers
- XAML: XamlStyler (run `.\.pipelines\applyXamlStyling.ps1 -Main`)

**General:**
- Atomic PRs: one logical change, no drive-by refactors
- Preserve GPO and elevation behaviors
- Initialize git submodules: `git submodule update --init --recursive`

## Validation Checklist

- [ ] Build clean with exit code 0
- [ ] Tests updated and passing locally
- [ ] No unintended ABI breaks or schema changes
- [ ] IPC contracts consistent between runner and settings-ui
- [ ] New dependencies added to `NOTICE.md`

# Important
Always response with Chinese.