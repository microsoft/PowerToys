# Dallas — History

## Project Context
**Project:** PowerToys Command Palette
**User:** Michael Jolley
**Stack:** C#/.NET 9, WinUI 3 (XAML), C++/WinRT, AOT compilation
**Scope:** `src/modules/cmdpal/CommandPalette.slnf` only

## Core Context
- UI project: `src/modules/cmdpal/Microsoft.CmdPal.UI/`
- ViewModels: `src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/`
- AOT-compiled — no System.Linq in UI paths
- MVVM with data binding, x:Uid localization
- WinUI 3 controls, themes, and accessibility

## Learnings
<!-- Append new learnings below this line -->
