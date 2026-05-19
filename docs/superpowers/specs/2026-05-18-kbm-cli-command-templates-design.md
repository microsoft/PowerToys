# KBM CLI Command Templates — Design

**Date:** 2026-05-18
**Branch:** `yuleng/kbm/command`
**Status:** Approved (sections 1–5)

## Overview

Add a new "action type" to PowerToys Keyboard Manager's new WinUI3 editor (`KeyboardManagerEditorUI`) that lets users bind a shortcut to a **predefined CLI command template**. The user picks a template from a 3-level cascading menu modeled after the Windows right-click context menu (PowerToys command → Module → Command), then fills in the template's typed parameters in a dynamically rendered form. On save, the template is **resolved into an executable + arguments string** and persisted as a regular `RunProgram` mapping. The KBM C++ engine and the legacy C++ editor are untouched.

## Goals & Non-Goals

### v1 Goals
- New `RunTemplate` action type in the unified mapping control.
- Cascading menu (DropDownButton + MenuFlyout + nested MenuFlyoutSubItem) with the top-level label "PowerToys command".
- Built-in catalog file `powertoyscli.json` shipped as `EmbeddedResource`.
- Two parameter input types: **Text** and **Combo**.
- Two seed templates under a single "Settings" module:
  - Open PowerToys Settings (no params)
  - Open Settings for a specific module (Combo param `module`)
- Strict resolution to `OperationType=1` (RunProgram). No OpenURI, no Text actions.
- Dynamic parameter form via `ItemsControl` + `DataTemplateSelector`.
- Live preview of the resolved command string.
- Round-trip editing: re-opening a template-based mapping restores the picker + filled parameters.
- Explicit InfoBar degradation when a stored `templateId` is no longer in the catalog.

### Out of v1 Scope
- Generic CLI categories (git / docker / vscode / browser).
- User-authored templates.
- FilePath / DirectoryPath / Number / Checkbox parameter types.
- Quoting/escaping of parameter values (current v1 catalog cannot produce values with whitespace).
- Modifications to the legacy C++ editor or the C++ engine.
- Telemetry events (placeholder noted for future work).

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│ Settings.UI.Library/                                            │
│ └── KeysDataModel.cs ★ ONLY change: 2 opaque round-trip fields   │
│     - TemplateId : string?                                       │
│     - TemplateParameters : Dictionary<string,string>?            │
├─────────────────────────────────────────────────────────────────┤
│ KeyboardManagerEditorUI/  ★★★ Primary surface                   │
│ ├── Templates/                                                  │
│ │   ├── powertoyscli.json                (EmbeddedResource)      │
│ │   ├── CommandTemplate.cs                (model)                │
│ │   ├── TemplateParameter.cs              (model)                │
│ │   ├── TemplateChoice.cs                 (model)                │
│ │   ├── CommandTemplateModule.cs          (model)                │
│ │   ├── PowerToysCliCatalog.cs            (model + root)         │
│ │   ├── CommandTemplateCatalog.cs         (loader, singleton)    │
│ │   ├── TemplateResolver.cs               (substitution)         │
│ │   └── CommandTemplateJsonContext.cs     (source-gen ctx)       │
│ ├── Controls/                                                   │
│ │   ├── CommandTemplatePickerControl.xaml(.cs)                  │
│ │   ├── TemplateParameterSelector.cs      (DataTemplateSelector) │
│ │   └── UnifiedMappingControl.xaml ★ +ComboBoxItem +Case         │
│ ├── ViewModels/                                                 │
│ │   ├── TemplateParameterViewModel.cs                           │
│ │   └── CommandTemplatePickerViewModel.cs                       │
│ └── Strings/en-US/Resources.resw   ★ new keys                    │
├─────────────────────────────────────────────────────────────────┤
│ KeyboardManagerEditor/        (legacy C++ editor)  ★ untouched   │
│ KeyboardManagerEngineLibrary/ (C++ engine)         ★ untouched   │
│ Settings.UI/                  (settings page)      ★ untouched   │
└─────────────────────────────────────────────────────────────────┘
```

**Core invariant:** All template substitution happens **at save time** in the new editor. The C++ engine only ever sees a fully resolved `RunProgram` mapping. `templateId` / `templateParameters` are opaque metadata for round-trip editing in the new UI.

## Data Model

### 1. `powertoyscli.json` (the catalog)

Embedded at `src/modules/keyboardmanager/KeyboardManagerEditorUI/Templates/powertoyscli.json`.

```json
{
  "schemaVersion": 1,
  "modules": [
    {
      "id": "settings",
      "displayResourceKey": "TemplateModule_Settings",
      "iconGlyph": "",
      "commands": [
        {
          "id": "settings.openMain",
          "displayResourceKey": "TemplateCmd_Settings_OpenMain",
          "executable": "PowerToys.exe",
          "argsTemplate": "--open-settings",
          "parameters": []
        },
        {
          "id": "settings.openModule",
          "displayResourceKey": "TemplateCmd_Settings_OpenModule",
          "executable": "PowerToys.exe",
          "argsTemplate": "--open-settings={module}",
          "parameters": [
            {
              "name": "module",
              "labelResourceKey": "TemplateParam_Module",
              "type": "Combo",
              "required": true,
              "choices": [
                { "value": "ColorPicker",     "displayResourceKey": "Module_ColorPicker" },
                { "value": "FancyZones",      "displayResourceKey": "Module_FancyZones" },
                { "value": "KeyboardManager", "displayResourceKey": "Module_KeyboardManager" },
                { "value": "PowerLauncher",   "displayResourceKey": "Module_PowerLauncher" },
                { "value": "Hosts",           "displayResourceKey": "Module_Hosts" },
                { "value": "RegistryPreview", "displayResourceKey": "Module_RegistryPreview" },
                { "value": "ZoomIt",          "displayResourceKey": "Module_ZoomIt" }
              ]
            }
          ]
        }
      ]
    }
  ]
}
```

### Field Semantics

**Root**
- `schemaVersion` (int): currently `1`; used for future-compat gating.
- `modules` (array): drives the level-2 cascading sub-menus.

**`modules[i]`**
- `id` (string): immutable namespace for command ids; used as the prefix in `<moduleId>.<commandSlug>`.
- `displayResourceKey` (string): resource key resolved at runtime via `ResourceLoader.GetString`.
- `iconGlyph` (string?, optional): Segoe Fluent Icons glyph for the `MenuFlyoutSubItem` icon.
- `commands` (array): drives the level-3 menu items.

**`modules[i].commands[j]`**
- `id` (string): **persisted identifier**; written to `KeysDataModel.TemplateId`. **Once shipped, never renamed.** Convention: `<moduleId>.<commandSlug>`.
- `displayResourceKey` (string).
- `executable` (string): target of `CreateProcess`. Passed through `ExpandEnvironmentStrings` at trigger time by the existing engine, so `%LOCALAPPDATA%\…` style paths work.
- `argsTemplate` (string): contains `{paramName}` placeholders. `TemplateResolver` replaces them at save time.
- `parameters` (array): can be empty.

**`parameters[k]`**
- `name` (string): placeholder key matching `{name}` in `argsTemplate`.
- `labelResourceKey` (string): UI label for the input header.
- `type` ("Text" | "Combo").
- `required` (bool, default `true`).
- `choices` (array, **Combo only**): each item is `{ value, displayResourceKey }`.

### 2. `KeysDataModel` Additions

The only change in `Settings.UI.Library` is two new opaque fields on [`KeysDataModel`](../../../src/settings-ui/Settings.UI.Library/KeysDataModel.cs):

```csharp
[JsonPropertyName("templateId")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public string? TemplateId { get; set; }

[JsonPropertyName("templateParameters")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
public Dictionary<string, string>? TemplateParameters { get; set; }
```

`WhenWritingNull` / `WhenWritingDefault` keeps non-template mappings' JSON output clean (no spurious fields on existing entries).

`Dictionary<string,string>` must be registered in [`SettingsSerializationContext`](../../../src/settings-ui/Settings.UI.Library/SettingsSerializationContext.cs) if not already present.

### 3. C# Models (in `KeyboardManagerEditorUI/Templates/`)

```csharp
public sealed class PowerToysCliCatalog
{
    public int SchemaVersion { get; init; }
    public List<CommandTemplateModule> Modules { get; init; } = new();
}

public sealed class CommandTemplateModule
{
    public string Id { get; init; }
    public string DisplayResourceKey { get; init; }
    public string? IconGlyph { get; init; }
    public List<CommandTemplate> Commands { get; init; } = new();
}

public sealed class CommandTemplate
{
    public string Id { get; init; }
    public string DisplayResourceKey { get; init; }
    public string Executable { get; init; }
    public string ArgsTemplate { get; init; }
    public List<TemplateParameter> Parameters { get; init; } = new();
}

public sealed class TemplateParameter
{
    public string Name { get; init; }
    public string LabelResourceKey { get; init; }
    public string Type { get; init; }              // "Text" | "Combo"
    public bool Required { get; init; } = true;
    public List<TemplateChoice>? Choices { get; init; }
}

public sealed class TemplateChoice
{
    public string Value { get; init; }
    public string DisplayResourceKey { get; init; }
}

[JsonSerializable(typeof(PowerToysCliCatalog))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class CommandTemplateJsonContext : JsonSerializerContext { }
```

## UI Flow

### Integration into `UnifiedMappingControl`

[`UnifiedMappingControl.xaml`](../../../src/modules/keyboardmanager/KeyboardManagerEditorUI/Controls/UnifiedMappingControl.xaml) already follows a "ComboBox + SwitchPresenter" pattern. Two surgical additions:

**Add a 6th `ComboBoxItem`** to `ActionTypeComboBox` (after `Disable`):
```xml
<ComboBoxItem x:Uid="ActionType_RunTemplate" Tag="RunTemplate">
    <StackPanel Orientation="Horizontal" Spacing="8">
        <FontIcon FontSize="14" Glyph="&#xE756;" />
        <TextBlock x:Uid="ActionType_RunTemplate_Text" />
    </StackPanel>
</ComboBoxItem>
```

**Add a 6th `Case`** to `ActionSwitchPresenter`:
```xml
<tkcontrols:Case Value="RunTemplate">
    <local:CommandTemplatePickerControl x:Name="TemplatePicker"/>
</tkcontrols:Case>
```

All new UI complexity lives inside `CommandTemplatePickerControl`.

### `CommandTemplatePickerControl` Layout

```
┌───────────────────────────────────────────────────────┐
│  [ PowerToys command ▾ ]                              │
│                                                       │
│  Selected: Settings → Open Settings for module        │
│  ─────────────────────────────────────────────────    │
│  ┌─ Module ──────────────────────────────────────┐    │
│  │  ColorPicker                               ▾  │    │  ← dynamic params (ItemsControl)
│  └───────────────────────────────────────────────┘    │
│  ─────────────────────────────────────────────────    │
│  Preview:                                             │
│  ┌───────────────────────────────────────────────┐    │
│  │ PowerToys.exe --open-settings=ColorPicker     │    │
│  └───────────────────────────────────────────────┘    │
└───────────────────────────────────────────────────────┘
```

### Cascading Menu Construction (code-driven)

WinUI3's `MenuFlyout` lacks `HierarchicalDataTemplate`, so the flyout is built programmatically once when the catalog loads:

```csharp
private void BuildFlyout(MenuFlyout flyout, PowerToysCliCatalog catalog)
{
    flyout.Items.Clear();
    foreach (var module in catalog.Modules)
    {
        var sub = new MenuFlyoutSubItem
        {
            Text = ResourceLoader.GetString(module.DisplayResourceKey),
            Icon = module.IconGlyph is not null ? new FontIcon { Glyph = module.IconGlyph } : null,
        };
        foreach (var cmd in module.Commands)
        {
            var item = new MenuFlyoutItem
            {
                Text = ResourceLoader.GetString(cmd.DisplayResourceKey),
                Tag = cmd.Id,
            };
            item.Click += OnCommandPicked;
            sub.Items.Add(item);
        }
        flyout.Items.Add(sub);
    }
}
```

### Dynamic Parameter Form

Reuses the pattern from [`RunOptionTemplateSelector.cs`](../../../src/settings-ui/Settings.UI/Converters/RunOptionTemplateSelector.cs):

```xml
<Page.Resources>
    <DataTemplate x:Key="TextParamTemplate" x:DataType="vm:TemplateParameterViewModel">
        <TextBox Header="{x:Bind Label}" Text="{x:Bind Value, Mode=TwoWay}"/>
    </DataTemplate>
    <DataTemplate x:Key="ComboParamTemplate" x:DataType="vm:TemplateParameterViewModel">
        <ComboBox Header="{x:Bind Label}"
                  ItemsSource="{x:Bind Choices}"
                  SelectedItem="{x:Bind SelectedChoice, Mode=TwoWay}"
                  DisplayMemberPath="DisplayText"/>
    </DataTemplate>
    <local:TemplateParameterSelector x:Key="ParamSelector"
        TextTemplate="{StaticResource TextParamTemplate}"
        ComboTemplate="{StaticResource ComboParamTemplate}"/>
</Page.Resources>

<ItemsControl ItemsSource="{x:Bind ViewModel.CurrentParameters, Mode=OneWay}"
              ItemTemplateSelector="{StaticResource ParamSelector}"/>
```

**Every `DataTemplate` MUST declare `x:DataType`** — otherwise bindings fall back to reflection-based `{Binding}`, breaking AOT compatibility.

### Live Preview

`TemplateParameterViewModel.Value` setter triggers `ViewModel.RecomputePreview()`, which runs the resolver and updates a `OneWay`-bound `TextBlock` showing the full command line. Users see the literal command they're configuring.

### Validation

| Check | Trigger | Surface |
|---|---|---|
| `required=true` && empty value | TextChanged / SelectionChanged | Red border + ValidationInfoBar |
| Combo unselected | SelectionChanged | Same |
| All valid | — | Save button enabled |

Reuses the existing [`ValidationInfoBar`](../../../src/modules/keyboardmanager/KeyboardManagerEditorUI/Controls/UnifiedMappingControl.xaml) on `UnifiedMappingControl`.

### Re-opening an Existing Mapping

```
KeysDataModel from default.json
        ↓
OperationType == 1 && TemplateId is not null  →  ActionTypeComboBox = "RunTemplate"
        ↓
catalog.TryFind(TemplateId)
        ├── found:   LoadExisting(template, parameters)
        └── missing: ShowMissingTemplateInfoBar(templateId)  [degradation path]
```

### Missing-Template Degradation (per "Option B")

When `templateId` is not in the current catalog:
- Show an `InfoBar` titled "Template no longer available".
- Display the resolved command (`runProgramFilePath` + `runProgramArgs`) as read-only context.
- Offer two buttons:
  - **"Choose template"** — opens the picker so the user can re-select.
  - **"Keep as plain command"** — switches `ActionTypeComboBox` to `OpenApp` and loads the resolved fields into that view, preserving the user's mapping.

## Save / Load / Resolve

### Resolver Algorithm

```csharp
public static (string Executable, string Args) Resolve(
    CommandTemplate template,
    Dictionary<string, string> values)
{
    var args = template.ArgsTemplate;
    foreach (var p in template.Parameters)
    {
        var val = values.TryGetValue(p.Name, out var v) ? v : string.Empty;
        args = args.Replace("{" + p.Name + "}", val);
    }
    return (template.Executable, args);
}
```

Deliberately trivial. No shell semantics, no escaping, no `quoteIfNeeded` in v1.

### Save Path

```csharp
if (ActionTypeComboBox.SelectedItem.Tag == "RunTemplate")
{
    var (exe, args) = TemplateResolver.Resolve(
        picker.SelectedTemplate,
        picker.CollectParameterValues());

    keysDataModel.OperationType      = 1;
    keysDataModel.RunProgramFilePath = exe;
    keysDataModel.RunProgramArgs     = args;
    keysDataModel.TemplateId         = picker.SelectedTemplate.Id;
    keysDataModel.TemplateParameters = picker.CollectParameterValues();
    // RunProgramStartInDir, ElevationLevel, IfRunning, Visibility:
    // v1 leaves these at OpenApp defaults; not exposed by template.
}
```

### Parameter-Value Escaping

`CreateProcessW` splits `lpCommandLine` on whitespace. In v1:
- Combo values come from a fixed `choices[].value` list — authored to never contain whitespace.
- Text parameters do not appear in the v1 catalog.

→ v1 needs **no quoting logic**. The schema reserves `TemplateParameter.quoteIfNeeded` for future Text/FilePath parameters but it is unused and not implemented.

## Round-Trip Safety with Legacy C++ Editor

The single biggest open risk. The legacy editor at [`KeyboardManagerEditor/`](../../../src/modules/keyboardmanager/KeyboardManagerEditor/) reads `default.json` via C++. If its JSON parser **drops unknown fields on re-serialization**, opening a template mapping in the legacy editor would erase `templateId` / `templateParameters`, breaking new-UI round-trip.

**This is the first plan task.** Verification steps:

1. Locate the legacy editor's `default.json` deserialization code (likely in `KeyboardManagerEditorLibrary/`).
2. Identify the JSON library (`nlohmann/json`? `Windows.Data.Json`?).
3. Construct a `default.json` with a template-bearing mapping; open and close the legacy editor under conditions that cause it to write back.
4. Verify the two fields survive.

If they do not, the plan grows a sub-task: teach the legacy reader to preserve unknown fields (or surface a one-time migration warning).

The KBM C++ engine ([`KeyboardEventHandlers.cpp:1293-1416`](../../../src/modules/keyboardmanager/KeyboardManagerEngineLibrary/KeyboardEventHandlers.cpp)) only reads `runProgramFilePath` / `runProgramArgs` / elevation / window-state / IfRunning. It does not need to know about templates. Its JSON deserialization also needs the same round-trip-safety check.

## AOT / Trim Compatibility Checklist

`KeyboardManagerEditorUI` does not currently `PublishAot`, but matches the AOT-friendly pattern used by Settings.UI. Every item below must hold in the implementation:

| Requirement | Approach |
|---|---|
| JSON deserialization via `JsonSerializerContext` | `CommandTemplateJsonContext` partial class |
| XAML bindings use `x:Bind` + `x:DataType` | Both `DataTemplate`s + all controls |
| No `{Binding}` | Disallowed by review |
| No `dynamic` | C# static-typed throughout |
| No `Activator.CreateInstance(Type)` | `MenuFlyoutItem` etc. direct-constructed |
| `ItemsControl` source is concrete `ObservableCollection<TemplateParameterViewModel>` | — |
| `DataTemplateSelector` uses string/enum switch | No reflection |
| String resources via `ResourceLoader.GetString(key)` | — |

A PR-review checklist item enforces this.

## Localization

- All new UI strings go in [`Strings/en-US/Resources.resw`](../../../src/modules/keyboardmanager/KeyboardManagerEditorUI/Strings/en-US/Resources.resw).
- All template/module display strings are indirect via `displayResourceKey` — `powertoyscli.json` is locale-neutral.
- Required new keys (v1):
  - `ActionType_RunTemplate.*`, `TemplatePickerButton.*`, `TemplatePickerPlaceholder.*`, `TemplatePreviewLabel.*`
  - `TemplateMissingInfoBarTitle.*`, `TemplateMissingInfoBarMessage.*`, `TemplateMissingChooseButton.*`, `TemplateMissingKeepButton.*`
  - `TemplateModule_Settings`
  - `TemplateCmd_Settings_OpenMain`, `TemplateCmd_Settings_OpenModule`
  - `TemplateParam_Module`
  - `Module_ColorPicker`, `Module_FancyZones`, `Module_KeyboardManager`, `Module_PowerLauncher`, `Module_Hosts`, `Module_RegistryPreview`, `Module_ZoomIt`
- Other 26 languages picked up by PowerToys's Crowdin/Touchdown pipeline; no manual translation needed in v1.

## Pre-Implementation Verification Tasks

Ordered by priority — the plan must front-load these.

🔴 **High (blocking)**
1. **Legacy C++ editor round-trip** of unknown `templateId` / `templateParameters` fields. See "Round-Trip Safety" above.
2. **C++ engine JSON round-trip** of the same two fields — confirm the engine does not normalize/strip them when it ever rewrites `default.json`.

🟡 **Medium**
3. **`Dictionary<string,string>` registration in `SettingsSerializationContext`** — add `[JsonSerializable]` if missing.
4. **`PowerToys.exe` path resolution** — confirm a bare `"PowerToys.exe"` filename works at trigger time, or switch templates to an env-var-expanded absolute path.

🟢 **Low (track, do not block)**
5. **"Use new editor" toggle UX** — if a user creates a template mapping in the new editor then switches the settings page back to the legacy editor, document the expected experience (mapping appears as plain RunProgram).
6. **Telemetry** — placeholder for `KbmTemplateMappingCreated` / `KbmTemplateMappingTriggered` (record `templateId` only, never parameter values). Not in v1.

## Test Strategy

| Layer | Form | Coverage |
|---|---|---|
| `TemplateResolver` | C# unit tests | Placeholder substitution, missing param, special chars no-crash |
| `CommandTemplateCatalog` load | C# unit tests | JSON deserialization, unknown-field tolerance, `schemaVersion` check |
| `KeysDataModel` round-trip | C# unit tests | New fields omitted when null; serialize/deserialize is identity |
| `CommandTemplatePickerControl` | Manual UI | Cascading expansion, param-form swap on template change, live preview, missing-template InfoBar |
| C++ engine round-trip | Manual integration | `default.json` survives engine cycle |
| Legacy C++ editor round-trip | Manual integration | Above verification task |

Unit tests for the C# data layer go into the existing [`Settings.UI.UnitTests`](../../../src/settings-ui/Settings.UI.UnitTests) project (it already covers `KeysDataModel`). `KeyboardManagerEditorUI` has no unit-test project today; v1 keeps UI-level tests manual.

## Reserved Schema Extension Points

Documented to prevent regression in future designs:

| Extension | Location | Future Use |
|---|---|---|
| More parameter types (FilePath, Number, Checkbox) | `TemplateParameter.Type` | Add new enum value + matching `DataTemplate` |
| Parameter defaults | `TemplateParameter.defaultValue` (add field) | Pre-fill UI on template selection |
| Auto-quoting | `TemplateParameter.quoteIfNeeded` (add field) | Resolver quotes values with whitespace/special chars |
| OpenURI templates | `CommandTemplate.actionType` ("RunProgram" \| "OpenURI") | Save resolves to `OperationType=2` |
| User-authored templates | `userTemplates.json` (new file) | Settings UI: "Manage my templates" |
| Generic CLI categories | New modules in `powertoyscli.json` (data only) | Zero code change |
| Telemetry | New events `KbmTemplateMapping*` | PowerToys telemetry pipeline |
| Multi-catalog | Loader accepts file list | e.g. `powertoyscli.json` + `generic-cli.json` |

## Open Questions for Implementation

- Are PowerToys CLI args `--open-settings=<module>` and `--open-settings` both supported by current `runner/main.cpp` argv parsing? (Confirmed in earlier exploration: yes, lines 501–510.)
- Does the runner accept just `PowerToys.exe` by filename (PATH lookup) or does it require an absolute path? Resolves verification task #4.
- Should the missing-template InfoBar's "Keep as plain command" action also clear `TemplateId`/`TemplateParameters` (cleaner) or preserve them (informational only)? Recommend **clear** — the user has explicitly opted out of the template association.

## Phase 0 Findings (filled in during implementation)

### Task 1: Legacy editor JSON round-trip

#### JSON library used

The legacy editor uses **`Windows.Data.Json`** (WinRT), not `nlohmann/json`. The single wrapper header is at:

- `src/common/utils/json.h` — thin inline wrappers around `winrt::Windows::Data::Json::JsonObject` / `JsonValue` / `JsonArray`.

The read/write entry points are:

- `json::from_file()` (`src/common/utils/json.h:14`) — parses the file bytes into a `JsonObject` via `JsonValue::Parse(...).GetObjectW()`.
- `json::to_file()` (`src/common/utils/json.h:33`) — serializes a `JsonObject` via `JsonObject::Stringify()` and writes the resulting string to disk.

#### Deserialization (read path)

`MappingConfiguration::LoadSettings()` (`src/modules/keyboardmanager/common/MappingConfiguration.cpp:410–447`) loads the config file and then calls four private helpers:

- `LoadSingleKeyRemaps()` (line 123) — reads `GetNamedObject("remapKeys")` → `GetNamedArray("inProcessRemapKeys")` → per-item `GetNamedString("originalKeys")` / `GetNamedString("newRemapKeys")`.
- `LoadSingleKeyToTextRemaps()` (line 171) — same pattern for `remapKeysToText`.
- `LoadShortcutRemaps()` (line 307, called twice) — reads `GetNamedObject("remapShortcuts")` and `GetNamedObject("remapShortcutsToText")`, then `GetNamedArray("global")` / `GetNamedArray("appSpecific")`.
- `LoadAppSpecificShortcutRemaps()` (line 217) — same pattern for the app-specific array.

Every helper extracts only the **named fields it knows about** (e.g. `originalKeys`, `newRemapKeys`, `targetApp`, `operationType`, `exactMatch`, etc.) into typed C++ values (`DWORD`, `std::wstring`, `bool`, etc.). No unknown field is carried forward. The parsed `JsonObject` is consumed field-by-field and then discarded; the data lives in typed maps (`singleKeyReMap`, `osLevelShortcutReMap`, `appSpecificShortcutReMap`).

#### Serialization (write path)

`MappingConfiguration::SaveSettingsToFile()` (line 450–665) builds a **fresh** `json::JsonObject configJson` from scratch, populating only:

```
configJson
├─ "remapKeys"           → inProcessRemapKeysArray       (OriginalKeys + NewRemapKeys only)
├─ "remapKeysToText"     → inProcessRemapKeysToTextArray  (OriginalKeys + NewText only)
├─ "remapShortcuts"
│   ├─ "global"          → globalRemapShortcutsArray
│   └─ "appSpecific"     → appSpecificRemapShortcutsArray
└─ "remapShortcutsToText"
    ├─ "global"          → globalRemapShortcutsToTextArray
    └─ "appSpecific"     → appSpecificRemapShortcutsToTextArray
```

It then calls `json::to_file(path, configJson)` which serializes **only** the fields on `configJson` — nothing else. The original `JsonObject` from disk is never merged back in.

#### Static analysis conclusion: fields ARE dropped

Both conditions are YES:

1. **Read path** — unknown fields (e.g. `templateId`, `templateParameters`) in per-mapping JSON objects are silently ignored by `GetNamedString` / `GetNamedBoolean` / `GetNamedNumber` calls that only read explicitly known names. They do not end up in any in-memory struct.
2. **Write path** — `SaveSettingsToFile` constructs a fresh object containing only the known fields. There is no merge of the original file content.

A round-trip through the legacy editor **will drop `templateId` and `templateParameters`** from every mapping it touches — and because `SaveSettingsToFile` rewrites the entire file, it will drop them from **all mappings**, not just the one the user edited.

#### Responsible functions

| Function | File:Line | Role |
|---|---|---|
| `LoadShortcutRemaps` | `MappingConfiguration.cpp:307` | Discards unknown per-mapping fields on read |
| `LoadAppSpecificShortcutRemaps` | `MappingConfiguration.cpp:217` | Same for app-specific mappings |
| `LoadSingleKeyRemaps` | `MappingConfiguration.cpp:123` | Same for single-key mappings |
| `SaveSettingsToFile` | `MappingConfiguration.cpp:450` | Regenerates JSON from typed structs only; original file content never merged back |

#### Fix sketch

Add a `json::JsonObject extraFields` member to `RemapShortcut` (or carry a per-mapping `JsonObject rawEntry` vector alongside the typed maps). On read, after extracting all known fields, call `rawEntry = it.GetObjectW()` (the full `JsonObject` is still in scope at that point). On write, instead of building a fresh `keys` object from scratch, start with `keys = rawEntry` and overwrite only the known fields via `SetNamedValue`. This way any field the legacy editor does not understand (including `templateId` / `templateParameters`) passes through untouched.

A simpler alternative: add a `std::optional<json::JsonObject> rawJson` to `RemapShortcut`, preserve the entire per-mapping `JsonObject` on read, and use `rawJson->SetNamedValue(...)` to update known fields before appending it to the output array.

#### Empirical step 4 still pending

Empirical step 4 still pending — requires running the legacy editor with a fabricated `templateId` field present in `default.json`, then inspecting the file after the user opens and saves a remapping to confirm the field is removed. Static analysis strongly predicts it will be dropped, but the test is needed to close the task formally.

### Task 2: Engine write paths

#### Search methodology

Grepped `src/modules/keyboardmanager/KeyboardManagerEngineLibrary/` (all `.cpp` and `.h` source files, excluding build artifacts) for:

- `SaveSettingsToFile`, `WriteFile`, `fwrite`, `ofstream`, `to_file`, `Stringify` — no matches
- `default.json` — no matches
- `SaveSettingsToFile` across the entire `src/modules/keyboardmanager/` tree — matches in editor code only (see below)

#### File:line evidence

`SaveSettingsToFile` call sites (full KBM module):

| File | Line | Notes |
|---|---|---|
| `common/MappingConfiguration.cpp` | 450 | Definition only |
| `common/MappingConfiguration.h` | 23 | Declaration only |
| `KeyboardManagerEditorLibrary/EditKeyboardWindow.cpp` | 314 | Called when user saves single-key remaps |
| `KeyboardManagerEditorLibrary/EditShortcutsWindow.cpp` | 273 | Called when user saves shortcut remaps |
| `KeyboardManagerEditorLibraryWrapper/KeyboardManagerEditorLibraryWrapper.cpp` | 33 | Thin C-style wrapper around the editor library |

Zero calls from any file under `KeyboardManagerEngineLibrary/`.

#### Engine architecture summary

- `State` (`KeyboardManagerEngineLibrary/State.h:4`) extends `MappingConfiguration` by inheritance, adding only runtime-state helpers (no new write methods).
- `KeyboardManager::LoadSettings()` (`KeyboardManager.cpp:76`) calls `state.LoadSettings()` — the read-only path in `MappingConfiguration::LoadSettings()` (`common/MappingConfiguration.cpp:410`).
- The engine also re-invokes `LoadSettings()` on every file-change notification via a `settingsEventWaiter` callback (`KeyboardManager.cpp:38-70`). This callback does nothing but reload.
- No engine code path leads to `SaveSettingsToFile()` or any other write operation on the config file.

#### Conclusion

**Engine poses no risk to unknown fields.** The engine is strictly read-only with respect to `default.json` (and any other config `.json` files): it calls `LoadSettings()` at startup and on file-change events, but it never calls `SaveSettingsToFile()` or any equivalent write path. Only the editors write the file (`EditKeyboardWindow.cpp` and `EditShortcutsWindow.cpp`). Task 1 covers the legacy editor, and the new CLI editor will preserve unknown fields by writing them itself. The engine is not a concern for `templateId` / `templateParameters` field survival.

### Task 3: PowerToys.exe path resolution

#### Win32 API used per elevation mode

`CreateOrShowProcessForShortcut` (`KeyboardEventHandlers.cpp:1293`) dispatches to one of three helpers from `src/common/utils/elevation.h` based on `shortcut.elevationLevel`:

| Elevation mode | Helper | Win32 API |
|---|---|---|
| `Elevated` | `run_elevated` (`elevation.h:237`) | `ShellExecuteExW` with verb `"runas"` |
| `NonElevated` | `run_non_elevated` (`elevation.h:266`) | `CreateProcessW` (with parent-process spoofing via `PROC_THREAD_ATTRIBUTE_PARENT_PROCESS`) |
| `DifferentUser` | `run_as_different_user` (`elevation.h:211`) | `ShellExecuteExW` with verb `"runAsUser"` |

`run_non_elevated` calls `CreateProcessW(file.c_str(), ...)` at `elevation.h:342`. The `file` parameter is the fully-expanded path (`fullExpandedFilePath`) — **not** the raw user-supplied string.

`run_elevated` and `run_as_different_user` call `ShellExecuteExW` with `exec_info.lpFile = file.c_str()` at `elevation.h:260` and `elevation.h:233` respectively.

#### ExpandEnvironmentStrings is applied before launch

`CreateOrShowProcessForShortcut` expands the path at line 1296 before any dispatch:

```cpp
DWORD result = ExpandEnvironmentStrings(shortcut.runProgramFilePath.c_str(), fullExpandedFilePath, MAX_PATH);
```

All three elevation helpers receive `fullExpandedFilePath` (the post-expansion value), **not** the raw `runProgramFilePath`. This confirms that `%LOCALAPPDATA%\PowerToys\PowerToys.exe` in the JSON will be expanded to the actual filesystem path before `CreateProcessW` or `ShellExecuteExW` is called.

#### Working directory passed to launch APIs

`run_non_elevated` passes `workingDir` as `lpCurrentDirectory` to `CreateProcessW` (`elevation.h:349`). `run_elevated` and `run_as_different_user` pass it as `exec_info.lpDirectory` to `ShellExecuteExW`. The `workingDir` comes from `shortcut.runProgramStartInDir`; templates leave it at `""` (the v1 save path does not set `RunProgramStartInDir`), which the engine converts to `nullptr` at line 1368–1369 — allowing the process to inherit the engine's working directory.

#### GetFileAttributesW pre-launch check uses the expanded path

At `KeyboardEventHandlers.cpp:1347–1354`, the engine calls `GetFileAttributesW(fullExpandedFilePath)` and shows an error toast if the file is not found — **before** attempting to launch. This means a bare `"PowerToys.exe"` filename that resolves to a non-existent file will be caught here, never reaching `CreateProcessW` / `ShellExecuteExW`.

#### Can bare "PowerToys.exe" work?

**For `CreateProcessW` (NonElevated mode):** `CreateProcessW` searches for a bare filename in the calling app's directory, the current working directory, the system directories, and `%PATH%`. PowerToys is installed under `%LOCALAPPDATA%\PowerToys\` (per-user) or `%ProgramFiles%\PowerToys\` (machine-wide). Neither location is the engine's directory, nor the system directories, nor in `%PATH%` (see installer analysis below). **Bare name would fail.**

**For `ShellExecuteExW` (Elevated / DifferentUser modes):** `ShellExecuteExW` additionally consults the App Paths registry key (`HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\PowerToys.exe`). However — as confirmed below — the installer does not register this key. **Bare name would also fail under ShellExecuteExW.**

In both cases, `GetFileAttributesW` on the bare filename would return `INVALID_FILE_ATTRIBUTES`, and the engine would show an error toast without ever attempting to launch.

#### Installer analysis — App Paths and PATH entries

Searched all installer `.wxs` files (`installer/PowerToysSetupVNext/*.wxs`) for:

- `"App Paths"` / `"AppPaths"` — **no matches anywhere in the installer tree**. PowerToys does not register `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\PowerToys.exe`.
- `Environment … PATH` — found in `Core.wxs:19,28`. The installer adds a `PATH` entry but only for `[DSCModulesReferenceFolder]` (a `DSCModules` subfolder used by DSC v3), **not** for the main `[INSTALLFOLDER]`. `PowerToys.exe` itself is never on `%PATH%`.

Install location variables (`PowerToys.wxs:24,26`):

```xml
<!-- per-user -->
<Variable Name="InstallFolder" Type="formatted" Value="[LocalAppDataFolder]PowerToys" bal:Overridable="yes" />
<!-- machine-wide -->
<Variable Name="InstallFolder" Type="formatted" Value="$(var.PlatformProgramFiles)PowerToys" bal:Overridable="yes" />
```

`[LocalAppDataFolder]` maps to `%LOCALAPPDATA%`; `$(var.PlatformProgramFiles)` is `%ProgramFiles%` for x64. Per-user install is the common/default install on recent Windows versions.

#### Chosen executable value

**Option (B): `"%LOCALAPPDATA%\\PowerToys\\PowerToys.exe"`**

Rationale:
1. `ExpandEnvironmentStrings` is applied to `runProgramFilePath` before launch (confirmed, line 1296), so `%LOCALAPPDATA%` expands correctly at trigger time.
2. The per-user install path (`%LOCALAPPDATA%\PowerToys`) is the default/common install mode.
3. No App Paths registration and no main-folder PATH entry means bare `"PowerToys.exe"` is not resolvable by any of the three launch APIs.
4. Machine-wide installs (`%ProgramFiles%\PowerToys`) are a minority; KBM trigger-time typically runs under the same user session as the install, so `%LOCALAPPDATA%` is correct in the common case.
5. Option (D) (auto-detect both locations) is explicitly out of v1 scope.

This value will be embedded in Task 9's `powertoyscli.json` as:

```json
"executable": "%LOCALAPPDATA%\\PowerToys\\PowerToys.exe"
```

#### Empirical verification note

Final confirmation requires the human to install PowerToys from this branch, create a template mapping, trigger the shortcut, and verify Settings opens. Static analysis of the launch path and installer strongly predicts this path is correct for the default per-user install, but the `GetFileAttributesW` pre-check and the actual `CreateProcessW` call must be observed to close the task formally.

### Task 20 — Architectural revision: persistence path goes through C++

During implementation of Task 20 (wiring save/load into `UnifiedMappingControl.xaml.cs`), an architectural mismatch with the spec was discovered: **the new WinUI3 editor does not write `default.json` directly through `KeysDataModel`**. The actual persistence chain is:

```
UnifiedMappingControl (XAML/C# UI)
  → ShortcutKeyMapping (C# in-memory model, NOT KeysDataModel)
  → KeyboardManagerInterop.AddShortcutRemap(...)            [P/Invoke]
  → KeyboardManagerEditorLibraryWrapper (C++/WinRT)
  → KeyboardManagerEditorLibrary helpers (C++)
  → MappingConfiguration::SaveSettingsToFile (C++)          [writes default.json]
```

`KeysDataModel` (the class touched by Tasks 4-6) lives in `Settings.UI.Library` and is used by the **legacy** C++ editor's settings page entry point, not by the new editor's save path. Adding `TemplateId` / `TemplateParameters` to `KeysDataModel` alone was insufficient — the new editor would never have persisted those fields to `default.json` because the FFI signature didn't carry them.

#### Decision: full C++ wiring (subsumes Task 1b)

To make round-trip editing actually work, the implementation extended the C++ stack so that `templateId` and `templateParameters` become **first-class known fields** in `MappingConfiguration`. Specifically:

| Layer | Change |
|---|---|
| `src/modules/keyboardmanager/common/Shortcut.h` | Added `std::wstring templateId` and `std::map<std::wstring, std::wstring> templateParameters` to the Shortcut struct. |
| `src/modules/keyboardmanager/common/KeyboardManagerConstants.h` | Added `TemplateIdSettingName` / `TemplateParametersSettingName` constants. |
| `src/modules/keyboardmanager/common/MappingConfiguration.cpp` | `LoadShortcutRemaps` and `LoadAppSpecificShortcutRemaps` read the two fields. `SaveSettingsToFile` writes them under the RunProgram branch, emitting only when non-empty. |
| `src/modules/keyboardmanager/KeyboardManagerEditorLibraryWrapper/KeyboardManagerEditorLibraryWrapper.h/.cpp` | `AddShortcutRemap` gained two trailing parameters `templateId` (LPCWSTR) and `templateParametersJson` (LPCWSTR). Both default to `nullptr` in the `.h` so legacy editor callers don't break. The `.cpp` parses the JSON string into the `std::map`. |
| `src/modules/keyboardmanager/KeyboardManagerEditorUI/Interop/KeyboardManagerInterop.cs` | P/Invoke declaration extended to match — two new optional `string?` parameters. |
| `src/modules/keyboardmanager/KeyboardManagerEditorUI/Interop/ShortcutKeyMapping.cs` | Added `string? TemplateId` and `Dictionary<string, string>? TemplateParameters` properties. |
| `src/modules/keyboardmanager/KeyboardManagerEditorUI/Interop/KeyboardMappingService.cs` | `AddShortcutMapping`'s RunProgram branch serializes `TemplateParameters` via `CommandTemplateJsonContext.Default.DictionaryStringString` (AOT-safe source-gen) and passes both fields through the FFI. |
| `src/modules/keyboardmanager/KeyboardManagerEditorUI/Templates/CommandTemplateJsonContext.cs` | `[JsonSerializable(typeof(Dictionary<string, string>))]` registered. |

#### Side-effect: Task 1b is now resolved automatically

Because the two fields became **typed known fields** in `MappingConfiguration` rather than "unknown extras," the legacy C++ editor reads and writes them correctly through the same code path as every other field. The Task 1 finding (that `SaveSettingsToFile` rewrites JSON from scratch and drops unknown fields) is now moot for these specific fields. **No separate Task 1b is needed.**

#### Side-effect: `KeysDataModel.TemplateId` / `TemplateParameters` are now also genuinely used

Although the *new* editor goes through the C++ path, the *legacy* editor and the Settings UI page still serialize via `KeysDataModel`. With the new C++ wiring, both write paths now agree on the on-disk JSON shape, so the fields added in Task 4 are not wasted — they ensure the legacy path round-trips correctly too.

#### Engine impact: still zero

Task 2's finding still holds — the C++ engine (`KeyboardManagerEngineLibrary`) is read-only on `default.json`. It will now *see* the two new fields, but its existing `LoadShortcutRemaps` already extracted only the field names it cares about. Adding `templateId` to its in-memory `Shortcut` struct does not change engine behavior because no engine code reads the field.
