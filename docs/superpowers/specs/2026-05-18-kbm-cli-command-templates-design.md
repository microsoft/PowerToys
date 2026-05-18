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
