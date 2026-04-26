# Macro Settings UI Design Spec

## Goal

Add a Macro settings page to the existing PowerToys Settings app, allowing users to create, edit, and delete macros with a full step-tree editor, hotkey capture, and app-scope filtering.

## Architecture

Follows the existing PowerToys Settings MVVM pattern exactly. No new projects. All new files live in `Settings.UI` and `Settings.UI.Library` is not used (macro files are the settings — no aggregate settings model needed).

### New files

| File | Purpose |
|------|---------|
| `Settings.UI/ViewModels/MacroViewModel.cs` | Page view model, owns macro list, IPC client |
| `Settings.UI/ViewModels/MacroStepViewModel.cs` | Wraps `MacroStep` for UI binding, recursive sub-steps |
| `Settings.UI/ViewModels/MacroEditViewModel.cs` | Dialog view model, owns editable step tree |
| `Settings.UI/SettingsXAML/Views/MacroPage.xaml` + `.cs` | Settings page: macro list with enable toggles |
| `Settings.UI/SettingsXAML/Views/MacroEditDialog.xaml` + `.cs` | Edit dialog: name, hotkey, app scope, step tree |
| `Settings.UI/Strings/en-us/Resources.resw` | New resource strings (additions only) |

### Modified files

| File | Change |
|------|--------|
| `Settings.UI/SettingsXAML/Views/ShellPage.xaml` | Add `NavigationViewItem` for Macro under System Tools group |
| `Settings.UI/Settings.UI.csproj` | Add `ProjectReference` to `MacroCommon.csproj` |
| `MacroCommon/Models/MacroDefinition.cs` | Add `bool IsEnabled { get; init; } = true` property |
| `MacroCommon/Serialization/MacroJsonContext.cs` | No change needed (source-gen picks up new property automatically) |

## Components

### MacroViewModel

- Inherits `PageViewModelBase`.
- `ModuleName` → `"Macro"`.
- Scans `%AppData%\Microsoft\PowerToys\Macros\*.json` on load via `MacroSerializer.Deserialize`.
- Exposes `ObservableCollection<MacroListItem>` where `MacroListItem` is a thin observable wrapper around `MacroDefinition`, binding `IsEnabled` (persisted in the JSON file via the new `MacroDefinition.IsEnabled` property).
- `FileSystemWatcher` on the Macros directory refreshes the collection on external changes (300ms debounce matching engine).
- IPC: lazily connects `JsonRpc` to pipe `"PowerToys.MacroEngine"` on first edit. If engine not running, `SuspendHotkeysAsync` / `ResumeHotkeysAsync` calls are silently swallowed.
- Commands: `NewMacroCommand`, `EditMacroCommand(MacroListItem)`, `DeleteMacroCommand(MacroListItem)`.
- On delete: `File.Delete("{id}.json")`, remove from collection. Engine FSW drops it.

### MacroStepViewModel

```csharp
// Observable wrapper around MacroStep for tree editing
class MacroStepViewModel : Observable
{
    StepType Type { get; set; }
    string? Key { get; set; }          // PressKey
    string? Text { get; set; }         // TypeText
    int? Ms { get; set; }              // Wait
    int? Count { get; set; }           // Repeat
    ObservableCollection<MacroStepViewModel> SubSteps { get; }  // Repeat children

    // Converts back to immutable MacroStep (recursive)
    MacroStep ToModel();

    // Creates from MacroStep (recursive)
    static MacroStepViewModel FromModel(MacroStep step);
}
```

### MacroEditViewModel

- Owns a `MacroDefinition` being edited (copy — original unchanged until OK).
- Exposes `HotkeySettings Hotkey` for binding to `ShortcutControl`.
- On dialog open: parses `MacroDefinition.Hotkey` string → `HotkeySettings` via `HotkeyStringToSettings()` helper.
- On OK: converts `HotkeySettings` → string via `HotkeySettingsToString()`. Validation is done by checking `HotkeySettings.IsEmpty` — no `KeyParser` dependency needed from Settings.UI. Writes `{id}.json` via `MacroSerializer.Serialize`.
- Exposes `ObservableCollection<MacroStepViewModel> Steps` built from `MacroStepViewModel.FromModel`.
- Commands: `AddStepCommand(StepType)`, `DeleteStepCommand(MacroStepViewModel)`, `AddSubStepCommand(MacroStepViewModel, StepType)`, `DeleteSubStepCommand(MacroStepViewModel parent, MacroStepViewModel child)`.

### MacroPage.xaml

`NavigablePage` → `SettingsPageControl` → `StackPanel`:

- Enable/disable toggle for the module (standard `ToggleSwitch` pattern).
- `SettingsGroup` "Macros" containing:
  - "New Macro" button at top.
  - `ItemsControl` bound to `ViewModel.Macros`:
    - Each item: `SettingsCard` with macro name, hotkey badge, enable `ToggleSwitch`, Edit button, Delete button.

### MacroEditDialog.xaml

`ContentDialog` with three sections:

**General:**
- `TextBox` → Name (required, non-empty validation)
- `ShortcutControl` → Hotkey (bound to `MacroEditViewModel.Hotkey`)
- `TextBox` → App Scope process name (e.g. `notepad.exe`; leave blank for global)

**Steps:** Recursive `ItemsControl` (not `TreeView`):
```
root ItemsControl
  DataTemplateSelector by StepType:
    PressKey  → [drag handle] [⌨] [TextBox: key combo] [delete]
    TypeText  → [drag handle] [T] [TextBox: text]       [delete]
    Wait      → [drag handle] [⏱] [NumberBox: ms]       [delete]
    Repeat    → [drag handle] [🔁] [NumberBox: count]   [delete]
                └─ indented ItemsControl (sub-steps, same templates)
                   └─ [+ Add Sub-Step] MenuFlyoutButton
[+ Add Step] MenuFlyoutButton (PressKey | TypeText | Wait | Repeat)
```

Drag-and-drop reorder: `CanDragItems=True`, `AllowDrop=True` per `ItemsControl`. Reorder within same parent only (no cross-level drag in v1).

**Dialog buttons:** Primary = "Save" (validates + writes JSON), Secondary = "Cancel" (discards).

### Navigation entry

Add to `ShellPage.xaml` under the `SystemToolsNavigationItem` group (same group as AdvancedPaste, Awake, etc.):

```xml
<NavigationViewItem
    x:Name="MacroNavigationItem"
    x:Uid="Shell_Macro"
    helpers:NavHelper.NavigateTo="views:MacroPage"
    AutomationProperties.AutomationId="MacroNavItem"
    Icon="{ui:BitmapIcon Source=/Assets/Settings/Icons/Macro.png}" />
```

Add `Shell_Macro.Content` = "Macro" to `Resources.resw`.

## IsEnabled Behavior

`MacroDefinition.IsEnabled` (default `true`) is persisted in the JSON file. On engine reload, `HotkeyManager` skips `RegisterHotKey` for any macro where `IsEnabled = false`. The enable toggle on the settings card writes the updated JSON immediately (no dialog required) and the engine FSW reloads within 300ms.

## Data Flow

### Load
`MacroViewModel` constructor → scan `%AppData%\Microsoft\PowerToys\Macros\*.json` → deserialize each → populate collection. FSW refreshes on external changes.

### Create
"New Macro" → `SuspendHotkeysAsync` → open `MacroEditDialog` (blank `MacroDefinition`) → on Save: `MacroSerializer.Serialize` writes `{newGuid}.json` → `ResumeHotkeysAsync` → engine FSW picks up within 300ms.

### Edit
"Edit" on card → `SuspendHotkeysAsync` → open `MacroEditDialog` with copy of macro → on Save: overwrite `{id}.json` → `ResumeHotkeysAsync` → engine FSW reloads.

### Delete
"Delete" on card → confirm flyout → `File.Delete("{id}.json")` → remove from collection → engine FSW drops macro.

### HotkeySettings ↔ string conversion

```csharp
// string "Ctrl+Shift+F5" → HotkeySettings
HotkeySettings HotkeyStringToSettings(string? hotkey)
{
    if (string.IsNullOrEmpty(hotkey)) return new HotkeySettings();
    // parse modifiers and main key from "Mod+Mod+Key" format
    // set Win/Ctrl/Alt/Shift booleans + Code on HotkeySettings
}

// HotkeySettings → string "Ctrl+Shift+F5"
string? HotkeySettingsToString(HotkeySettings settings)
{
    // build modifier prefix + key name
    // return null if no key set
}
```

## Error Handling

| Scenario | Behavior |
|----------|----------|
| Malformed JSON on load | Skip file, log warning (matches engine behavior) |
| IPC pipe unavailable | Swallow `IOException`/`TimeoutException`, proceed without suspend/resume |
| No hotkey set on Save | Check `HotkeySettings.IsEmpty`; show inline error in dialog, block Save |
| Empty macro name on Save | Show inline error, block Save |
| File write failure | Show error `ContentDialog` |
| Duplicate hotkey | No validation in Plan 2 (engine handles last-registered-wins) |

## Testing

### Unit tests (`Settings.UI.UnitTests`)

- `MacroViewModelTests`: load from fake directory, create/edit/delete round-trip, FSW refresh
- `MacroStepViewModelTests`: `FromModel`/`ToModel` round-trip for all step types, nested Repeat
- `HotkeyConversionTests`: `HotkeyStringToSettings` ↔ `HotkeySettingsToString` round-trip for common combos (`Ctrl+C`, `Ctrl+Shift+F5`, `Win+Alt+G`, null/empty)

### Manual smoke test

1. Open PowerToys Settings → Macro appears in left nav under Input Tools
2. Create macro: PressKey + TypeText steps, hotkey `Ctrl+F12`, no app scope → `{id}.json` appears in `%AppData%\Microsoft\PowerToys\Macros\`
3. Edit macro → hotkey capture does not fire existing macro → save → engine reloads
4. Add Repeat step with 3 sub-steps → renders indented, drag reorder within Repeat works
5. Delete macro → JSON file removed → engine drops macro
6. Malformed JSON file in Macros dir → page loads, bad file silently skipped
