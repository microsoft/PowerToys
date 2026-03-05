# JavaScript Extension Settings - Implementation Decision

**Date:** 2025-01-24
**Agent:** Dallas (UI Dev)
**Requested by:** Michael Jolley

## Context

Added configuration settings to allow users to customize where Command Palette discovers JavaScript extensions and specify a custom Node.js path.

## Implementation Details

### Settings Added to SettingsModel.cs

1. **JavaScriptExtensionPaths** (`List<string>`)
   - Default value: `["%LOCALAPPDATA%\\Microsoft\\PowerToys\\CommandPalette\\JSExtensions"]`
   - Allows users to configure multiple folder paths for JS extension discovery
   - Serializes as JSON array

2. **NodeJsPath** (`string?`)
   - Default value: `null` (will use system PATH detection)
   - Optional custom path to node.exe
   - Nullable to indicate system default should be used when not specified

### Serialization Support

Added `[JsonSerializable(typeof(string[]))]` to `JsonSerializationContext.cs` to ensure proper serialization/deserialization of the string array type.

### Settings System Architecture

**Settings Persistence:**
- Stored in JSON file at: `%LOCALAPPDATA%\Microsoft\PowerToys\CommandPalette\settings.json`
- Uses System.Text.Json with source generators for AOT compatibility
- SettingsService handles load/save with migration support

**Key Classes:**
- `SettingsModel` - POCO with all settings properties (must have public setters for JSON deserialization)
- `SettingsService` - Manages persistence, migrations, and change notifications
- `JsonSerializationContext` - Source-generated JSON serialization context

**Important Notes:**
- All new property types must be added to `JsonSerializationContext`
- Setters must be public or deserialization will fail silently
- Settings changes trigger `SettingsChanged` event

## Current State

- Settings properties added and compile successfully in isolation
- Ready for integration with JavaScriptExtensionService (once it compiles)
- No UI components added yet (as requested - focus on model layer only)

## Next Steps

1. Update JavaScriptExtensionService to read these settings
2. Replace hardcoded `GetDefaultExtensionsPath()` with settings-based path enumeration
3. Implement Node.js path resolution using NodeJsPath setting
4. Add settings UI page for users to configure these values
5. Add validation for path existence and Node.js binary verification

## Files Modified

- `src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/SettingsModel.cs`
- `src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/JsonSerializationContext.cs`
