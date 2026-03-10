# Squad Team

> powertoys

## Coordinator

| Name | Role | Notes |
|------|------|-------|
| Squad | Coordinator | Routes work, enforces handoffs and reviewer gates. |

## Members

| Name | Role | Charter | Status |
|------|------|---------|--------|
| Neo | Lead | `.squad/agents/neo/charter.md` | ✅ Active |
| Trinity | UI Dev | `.squad/agents/trinity/charter.md` | ✅ Active |
| Morpheus | ViewModel Dev | `.squad/agents/morpheus/charter.md` | ✅ Active |
| Tank | Extensions Dev | `.squad/agents/tank/charter.md` | ✅ Active |
| Oracle | Tester | `.squad/agents/oracle/charter.md` | ✅ Active |
| Scribe | Session Logger | `.squad/agents/scribe/charter.md` | 📋 Silent |
| Ralph | Work Monitor | `.squad/agents/ralph/charter.md` | 🔄 Monitor |

## Project Context

- **Project:** PowerToys Command Palette (CmdPal)
- **Solution filter:** `src/modules/cmdpal/CommandPalette.slnf`
- **Created:** 2026-03-10

### Key Projects

| Project | Owner | Path |
|---------|-------|------|
| CmdPal.UI (WinUI 3 app) | Trinity | `src/modules/cmdpal/Microsoft.CmdPal.UI/` |
| CmdPal.UI.ViewModels | Morpheus | `src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/` |
| CmdPal.Common | Morpheus | `src/modules/cmdpal/Microsoft.CmdPal.Common/` |
| Extensions SDK (C++/WinRT) | Tank | `src/modules/cmdpal/extensionsdk/` |
| Extensions Toolkit (C#) | Tank | `src/modules/cmdpal/extensionsdk/Microsoft.CommandPalette.Extensions.Toolkit/` |
| Built-in Extensions (17) | Tank | `src/modules/cmdpal/ext/` |
| CmdPalKeyboardService (C++) | Tank | `src/modules/cmdpal/CmdPalKeyboardService/` |
| CmdPalModuleInterface (C++) | Tank | `src/modules/cmdpal/CmdPalModuleInterface/` |
| Terminal UI (C++) | Tank | `src/modules/cmdpal/Microsoft.Terminal.UI/` |
| Tests (14 test projects) | Oracle | `src/modules/cmdpal/Tests/` |
