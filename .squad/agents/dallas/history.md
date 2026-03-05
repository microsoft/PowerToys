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

### 2025-01-24: Settings System Architecture

**Settings Persistence Pattern:**
- Settings stored as JSON at `%LOCALAPPDATA%\Microsoft\PowerToys\CommandPalette\settings.json`
- `SettingsModel.cs` - POCO with all settings properties
- `SettingsService.cs` - Handles load/save/migrations/change notifications
- `JsonSerializationContext.cs` - Source-generated JSON context for AOT

**Critical Requirements:**
- All property setters MUST be public (JSON deserialization fails silently otherwise)
- All new types MUST be added to `JsonSerializationContext` with `[JsonSerializable]` attribute
- Default values are set via property initializers in SettingsModel
- Environment variables in paths (like `%LOCALAPPDATA%`) are stored as-is, resolved at runtime

**Settings Change Flow:**
- UI modifies SettingsModel properties
- Calls SettingsService.SaveSettings(model)
- Triggers SettingsChanged event for subscribers
- Services react to settings changes

**Added Settings:**
- `JavaScriptExtensionPaths` - List<string> with default `["%LOCALAPPDATA%\\Microsoft\\PowerToys\\CommandPalette\\JSExtensions"]`
- `NodeJsPath` - Nullable string for custom Node.js path (null = use PATH)

