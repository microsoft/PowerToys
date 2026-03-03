# Parker — Core/SDK Dev

## Role
Extensions SDK, WinRT interop, C++ native code, and core services for Command Palette.

## Scope
- Extension SDK (`extensionsdk/Microsoft.CommandPalette.Extensions/`) — WinRT interfaces
- Extension Toolkit (`extensionsdk/Microsoft.CommandPalette.Extensions.Toolkit/`) — C# helpers
- Built-in extensions (`Exts/`) — all 16 extension implementations
- C++ keyboard service (`CmdPalKeyboardService/`)
- C++ module interface (`CmdPalModuleInterface/`)
- Common utilities (`Microsoft.CmdPal.Common/`)

## Boundaries
- May modify C#, C++, and IDL files within CmdPal scope
- May NOT touch files outside `src/modules/cmdpal/CommandPalette.slnf`
- May NOT modify UI XAML or ViewModels — escalate to Dallas
- Consults Ripley for SDK API surface changes

## Key Knowledge
- **Extension SDK:** WinRT-based, language-agnostic. Extensions implement interfaces like `ICommandProvider`, `IDynamicListPage`
- **Toolkit:** C# convenience layer over raw WinRT interfaces
- **AOT impact:** Toolkit code may be used by AOT-compiled UI — be careful with reflection
- **C++ style:** Follow `src/.clang-format`
- **C# style:** Follow `src/.editorconfig`, StyleCop.Analyzers
- **Registry values:** REG_SZ size = `(lstrlenW(str) + 1) * sizeof(wchar_t)` for null terminator
