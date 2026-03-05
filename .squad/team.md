# Squad — Command Palette Team

## Project Context

**Project:** Microsoft PowerToys — Command Palette Module
**User:** Michael Jolley
**Stack:** C#/.NET 9, WinUI 3 (XAML), C++/WinRT, AOT compilation
**Scope:** `src/modules/cmdpal/CommandPalette.slnf` — nothing outside this boundary
**Architecture:** Extensible plugin-based launcher with WinRT SDK, 16 built-in extensions, MVVM UI

## Boundary Rule

⚠️ **NEVER touch files outside `src/modules/cmdpal/CommandPalette.slnf`.** This includes:
- No changes to `src/runner/`, `src/settings-ui/`, `src/common/`, or any other module
- No changes to installer, build scripts, or solution-level files
- If a task requires changes outside this boundary, STOP and escalate to Michael

## Members

| Name | Role | Specialization | Badge |
|------|------|----------------|-------|
| Ripley | Lead | Architecture, code review, scope decisions | 🏗️ Lead |
| Dallas | UI Dev | WinUI 3, XAML, ViewModels, AOT | ⚛️ Frontend |
| Parker | Core/SDK Dev | Extensions SDK, WinRT interop, C++ native | 🔧 Backend |
| Lambert | Tester | Unit tests, extension tests, edge cases | 🧪 Tester |
| Kane | C# Extension Dev | Built-in CmdPal extensions, WinGet pattern, MSIX | 🔧 Backend |
| Ash | React/Reconciler Specialist | React reconciler, @raycast/api shim, bridge layer | 🔧 Backend |
| Scribe | Session Logger | Memory, decisions, session logs | 📋 Scribe |
| Ralph | Work Monitor | Work queue, backlog, keep-alive | 🔄 Monitor |

## Build

```
cd src/modules/cmdpal
tools\build\build.cmd
```

## Key Paths

- **UI App:** `src/modules/cmdpal/Microsoft.CmdPal.UI/`
- **ViewModels:** `src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/`
- **Common:** `src/modules/cmdpal/Microsoft.CmdPal.Common/`
- **Extension SDK:** `src/modules/cmdpal/extensionsdk/Microsoft.CommandPalette.Extensions/`
- **Extension Toolkit:** `src/modules/cmdpal/extensionsdk/Microsoft.CommandPalette.Extensions.Toolkit/`
- **Keyboard Service:** `src/modules/cmdpal/CmdPalKeyboardService/`
- **Module Interface:** `src/modules/cmdpal/CmdPalModuleInterface/`
- **Built-in Extensions:** `src/modules/cmdpal/Exts/`
- **Tests:** Various `*UnitTests` projects alongside each component
