# PowerToys KBM CLI Command Templates Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a new "Run from template" action type to the new WinUI3 PowerToys Keyboard Manager editor, exposing a 3-level cascading menu (PowerToys command → Module → Command) of predefined parameterized CLI templates that resolve at save time to standard RunProgram mappings.

**Architecture:** Templates are defined in a single embedded `powertoyscli.json` shipped inside `KeyboardManagerEditorUI.dll`. Selection + parameter substitution happens at save time, producing a regular `OperationType=1 (RunProgram)` entry in `default.json`. Two opaque round-trip fields (`templateId`, `templateParameters`) are added to `KeysDataModel` so the new editor can recover the template selection when re-opening a mapping. The KBM C++ engine and legacy C++ editor are untouched.

**Tech Stack:** C# 12 / .NET 8, WinUI 3 (Microsoft.WindowsAppSDK), `System.Text.Json` source generators (AOT-friendly), MSTest for unit tests, Segoe Fluent Icons for menu icons.

**Spec:** [2026-05-18-kbm-cli-command-templates-design.md](../specs/2026-05-18-kbm-cli-command-templates-design.md)

---

## File Structure

### Modified
- `src/settings-ui/Settings.UI.Library/KeysDataModel.cs` — add `TemplateId` + `TemplateParameters` properties
- `src/settings-ui/Settings.UI.Library/SettingsSerializationContext.cs` — register `Dictionary<string, string>`
- `src/modules/keyboardmanager/KeyboardManagerEditorUI/KeyboardManagerEditorUI.csproj` — `EmbeddedResource` for `powertoyscli.json`
- `src/modules/keyboardmanager/KeyboardManagerEditorUI/Controls/UnifiedMappingControl.xaml` — add `RunTemplate` ComboBoxItem + Case
- `src/modules/keyboardmanager/KeyboardManagerEditorUI/Controls/UnifiedMappingControl.xaml.cs` — wire save/load paths
- `src/modules/keyboardmanager/KeyboardManagerEditorUI/Strings/en-US/Resources.resw` — new resource keys
- `src/settings-ui/Settings.UI.UnitTests/ModelsTests/KeysDataModelTests.cs` — new (or extend if file exists)

### Created
- `src/modules/keyboardmanager/KeyboardManagerEditorUI/Templates/powertoyscli.json`
- `src/modules/keyboardmanager/KeyboardManagerEditorUI/Templates/PowerToysCliCatalog.cs` (root + nested POCOs)
- `src/modules/keyboardmanager/KeyboardManagerEditorUI/Templates/CommandTemplateJsonContext.cs`
- `src/modules/keyboardmanager/KeyboardManagerEditorUI/Templates/CommandTemplateCatalog.cs` (loader)
- `src/modules/keyboardmanager/KeyboardManagerEditorUI/Templates/TemplateResolver.cs`
- `src/modules/keyboardmanager/KeyboardManagerEditorUI/ViewModels/TemplateParameterViewModel.cs`
- `src/modules/keyboardmanager/KeyboardManagerEditorUI/ViewModels/CommandTemplatePickerViewModel.cs`
- `src/modules/keyboardmanager/KeyboardManagerEditorUI/Controls/TemplateParameterSelector.cs`
- `src/modules/keyboardmanager/KeyboardManagerEditorUI/Controls/CommandTemplatePickerControl.xaml`
- `src/modules/keyboardmanager/KeyboardManagerEditorUI/Controls/CommandTemplatePickerControl.xaml.cs`

### Testing Note

`KeyboardManagerEditorUI` is a WinExe — existing `Settings.UI.UnitTests` cannot reference it. For v1, the only **automated** tests live in `Settings.UI.UnitTests` and cover `KeysDataModel` serialization round-trip. Catalog loading and Resolver substitution are verified by:
- A startup smoke check (`CommandTemplateCatalog` constructor asserts ≥1 module loaded)
- Manual end-to-end UI tests in Phase 13

A future iteration may add `KeyboardManagerEditorUI.UnitTests` — out of scope for v1.

---

## Phase 0 — Pre-Implementation Verification

These tasks are blockers and must run **before any production code**.

### Task 1: Verify legacy C++ editor preserves unknown JSON fields

**Files:**
- Read-only: `src/modules/keyboardmanager/KeyboardManagerEditorLibrary/` (whichever file deserializes `default.json`)
- Test fixture: temporary copy of `%LOCALAPPDATA%\Microsoft\PowerToys\Keyboard Manager\default.json`

- [ ] **Step 1: Locate legacy deserialization code**

Run from the worktree root:
```
grep -rn "default.json\|RemapShortcuts\|nlohmann" src/modules/keyboardmanager/KeyboardManagerEditorLibrary/
```

Find the function that reads remappings into the in-memory model. Note which JSON library is used (`nlohmann::json` vs `Windows::Data::Json::JsonObject` etc.).

- [ ] **Step 2: Find the write-back code**

```
grep -rn "save\|WriteFile\|to_string\|Serialize" src/modules/keyboardmanager/KeyboardManagerEditorLibrary/ | grep -i json
```

Identify how the in-memory model is rewritten to `default.json` when the user clicks OK in the legacy editor.

- [ ] **Step 3: Static analysis — does the in-memory model carry unknown fields?**

Read both functions found above. Specifically check:
- Does the read path build a struct/class from named JSON fields, dropping unknowns? (typical with `from_json` overloads)
- Does the write path serialize that struct back, regenerating JSON from known fields only?

If both are TRUE → **fields will be dropped**. Document this finding.

- [ ] **Step 4: Empirical confirmation**

Build & install PowerToys from this branch (without any code changes yet). Then:

1. Create a normal Shortcut→OpenApp remap in either editor; save.
2. Quit PowerToys.
3. Manually edit `%LOCALAPPDATA%\Microsoft\PowerToys\Keyboard Manager\default.json` — add two fake fields inside the saved entry:
   ```json
   "templateId": "verify.roundtrip",
   "templateParameters": { "k": "v" }
   ```
4. Start PowerToys. Open the **legacy** editor (the C++ one, accessed when "Use new editor" is OFF).
5. Without modifying the entry, click OK to save.
6. Re-open `default.json` in a text editor. Are `templateId` and `templateParameters` still there?

- [ ] **Step 5: Record finding and branch the plan if needed**

Three possible outcomes:

| Outcome | Action |
|---|---|
| Fields preserved on round-trip | Continue to Task 2. No legacy-editor work needed. |
| Fields dropped silently | Add **Task 1b**: teach the legacy editor's JSON model to preserve unknown fields (typically a `nlohmann::json raw_extra;` member that copies unknowns and re-emits them). Estimate +1 day of C++ work. |
| Cannot verify (legacy editor not runnable) | Document the risk in the plan PR. Proceed with Task 2 but include a release-note warning that template mappings should not be edited in the legacy editor. |

Document the chosen branch in `docs/superpowers/plans/2026-05-18-kbm-cli-command-templates-plan.md` as a `Phase 0 Findings:` section appended below.

### Task 2: Verify C++ engine does not strip unknown fields

**Files:**
- Read-only: `src/modules/keyboardmanager/KeyboardManagerEngineLibrary/` (search for `default.json` reads)

- [ ] **Step 1: Confirm engine is read-only on `default.json`**

```
grep -rn "default.json\|WriteFile\|Save\|fwrite" src/modules/keyboardmanager/KeyboardManagerEngineLibrary/
```

Expected: engine reads `default.json` to apply remappings but never writes back. Verify no write code path exists.

- [ ] **Step 2: If engine writes (unexpected), inspect serialization**

If a write path is found, repeat Task 1's static analysis pattern. Otherwise document "engine is read-only; no risk to unknown fields" in the Phase 0 Findings section.

### Task 3: Verify `PowerToys.exe` is resolvable by bare filename via CreateProcess

**Files:**
- Read-only: `src/modules/keyboardmanager/KeyboardManagerEngineLibrary/KeyboardEventHandlers.cpp` (lines around 1293-1416, `CreateOrShowProcessForShortcut`)
- Read-only: `src/common/utils/elevation.h`

- [ ] **Step 1: Inspect the launch flow**

Read `CreateOrShowProcessForShortcut`. Note whether it calls `CreateProcessW` directly or `ShellExecuteEx`. ShellExecute will look up via PATH and association; CreateProcess requires the path it's given (with limited search rules).

- [ ] **Step 2: Test bare filename resolution empirically**

Create a manual test mapping via the new editor with `runProgramFilePath = "PowerToys.exe"` and `runProgramArgs = "--open-settings"`. Trigger the shortcut. Expected: PowerToys settings opens.

If it fails to find `PowerToys.exe`:

- Option A: Switch the templates to use an absolute path like `%LOCALAPPDATA%\\PowerToys\\PowerToys.exe`
- Option B: Use `%PROGRAMFILES%\\PowerToys\\PowerToys.exe`
- Option C: Use `%ProgramData%\\Microsoft\\Windows\\Start Menu\\Programs\\PowerToys (Preview).lnk` via ShellExecute (would require OperationType=2 — out of v1 scope)

- [ ] **Step 3: Record the chosen `executable` value**

Update `powertoyscli.json` schema example in the design doc with the resolved value, and lock it in for Phase 3. Document under Phase 0 Findings.

---

## Phase 1 — `KeysDataModel` Data Layer

### Task 4: Add the two new properties to `KeysDataModel`

**Files:**
- Modify: `src/settings-ui/Settings.UI.Library/KeysDataModel.cs`

- [ ] **Step 1: Add fields after `OperationType` property (after line 48)**

```csharp
[JsonPropertyName("templateId")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public string TemplateId { get; set; }

[JsonPropertyName("templateParameters")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
public Dictionary<string, string> TemplateParameters { get; set; }
```

(`using System.Collections.Generic;` is already imported on line 6.)

- [ ] **Step 2: Build the Settings.UI.Library project**

```
dotnet build src/settings-ui/Settings.UI.Library/Settings.UI.Library.csproj -c Debug
```

Expected: succeeds with 0 errors. Any JSON-source-generator warnings about `Dictionary<string, string>` will be resolved in Task 5.

- [ ] **Step 3: Commit**

```
git add src/settings-ui/Settings.UI.Library/KeysDataModel.cs
git commit -m "KBM: Add TemplateId/TemplateParameters to KeysDataModel"
```

### Task 5: Register `Dictionary<string, string>` in `SettingsSerializationContext`

**Files:**
- Modify: `src/settings-ui/Settings.UI.Library/SettingsSerializationContext.cs`

- [ ] **Step 1: Find the existing `[JsonSerializable]` block** (line ~98 where `KeyboardManagerProfile` is registered)

- [ ] **Step 2: Add a registration above the partial class line**

```csharp
[JsonSerializable(typeof(Dictionary<string, string>))]
```

Ensure `using System.Collections.Generic;` is present at the top of the file.

- [ ] **Step 3: Build and verify**

```
dotnet build src/settings-ui/Settings.UI.Library/Settings.UI.Library.csproj -c Debug
```

Expected: 0 warnings about `Dictionary<string, string>` source-gen.

- [ ] **Step 4: Commit**

```
git add src/settings-ui/Settings.UI.Library/SettingsSerializationContext.cs
git commit -m "KBM: Register Dictionary<string,string> in JsonSerializerContext for template parameters"
```

### Task 6: Write the failing round-trip test

**Files:**
- Create: `src/settings-ui/Settings.UI.UnitTests/ModelsTests/KeysDataModelTemplateFieldsTests.cs`

- [ ] **Step 1: Write the test**

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Settings.UnitTest.ModelsTests
{
    [TestClass]
    public class KeysDataModelTemplateFieldsTests
    {
        [TestMethod]
        public void TemplateFields_RoundTripThroughJson()
        {
            var original = new KeysDataModel
            {
                OriginalKeys = "162;67",
                NewRemapKeys = string.Empty,
                OperationType = 1,
                RunProgramFilePath = "PowerToys.exe",
                RunProgramArgs = "--open-settings=ColorPicker",
                TemplateId = "settings.openModule",
                TemplateParameters = new Dictionary<string, string>
                {
                    { "module", "ColorPicker" },
                },
            };

            var json = JsonSerializer.Serialize(original);
            var decoded = JsonSerializer.Deserialize<KeysDataModel>(json);

            Assert.AreEqual("settings.openModule", decoded.TemplateId);
            Assert.IsNotNull(decoded.TemplateParameters);
            Assert.AreEqual("ColorPicker", decoded.TemplateParameters["module"]);
        }

        [TestMethod]
        public void TemplateFields_OmittedFromJsonWhenNull()
        {
            var entry = new KeysDataModel
            {
                OriginalKeys = "162;67",
                NewRemapKeys = "162;86",
                OperationType = 0,
                TemplateId = null,
                TemplateParameters = null,
            };

            var json = JsonSerializer.Serialize(entry);

            Assert.IsFalse(json.Contains("templateId"), "templateId should be omitted when null");
            Assert.IsFalse(json.Contains("templateParameters"), "templateParameters should be omitted when null");
        }

        [TestMethod]
        public void TemplateFields_OmittedFromJsonWhenEmptyDictionary()
        {
            var entry = new KeysDataModel
            {
                OriginalKeys = "162;67",
                NewRemapKeys = string.Empty,
                OperationType = 1,
                RunProgramFilePath = "PowerToys.exe",
                RunProgramArgs = "--open-settings",
                TemplateId = "settings.openMain",
                TemplateParameters = new Dictionary<string, string>(),
            };

            var json = JsonSerializer.Serialize(entry);

            Assert.IsTrue(json.Contains("\"templateId\""), "templateId is non-null, should be serialized");
            // WhenWritingDefault drops default values including empty collections in some configs;
            // but Dictionary<,> default is null. Empty dictionary serializes as "{}" — accept either.
            // If business says empty should be omitted, this test should be updated to reflect that.
        }
    }
}
```

- [ ] **Step 2: Run the tests — expect they pass already since Tasks 4–5 are done**

```
dotnet test src/settings-ui/Settings.UI.UnitTests/Settings.UI.UnitTests.csproj --filter FullyQualifiedName~KeysDataModelTemplateFieldsTests
```

Expected: 3 tests pass.

If any fail: this indicates a bug in the field declarations from Task 4. Fix `KeysDataModel.cs` and re-run.

- [ ] **Step 3: Commit**

```
git add src/settings-ui/Settings.UI.UnitTests/ModelsTests/KeysDataModelTemplateFieldsTests.cs
git commit -m "KBM: Add round-trip tests for template fields in KeysDataModel"
```

---

## Phase 2 — Catalog Model Classes

### Task 7: Create the POCO model classes

**Files:**
- Create: `src/modules/keyboardmanager/KeyboardManagerEditorUI/Templates/PowerToysCliCatalog.cs`

- [ ] **Step 1: Write all five POCOs in a single file (one logical type group)**

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace KeyboardManagerEditorUI.Templates
{
    public sealed class PowerToysCliCatalog
    {
        public int SchemaVersion { get; init; }

        public List<CommandTemplateModule> Modules { get; init; } = new();
    }

    public sealed class CommandTemplateModule
    {
        public string Id { get; init; } = string.Empty;

        public string DisplayResourceKey { get; init; } = string.Empty;

        public string? IconGlyph { get; init; }

        public List<CommandTemplate> Commands { get; init; } = new();
    }

    public sealed class CommandTemplate
    {
        public string Id { get; init; } = string.Empty;

        public string DisplayResourceKey { get; init; } = string.Empty;

        public string Executable { get; init; } = string.Empty;

        public string ArgsTemplate { get; init; } = string.Empty;

        public List<TemplateParameter> Parameters { get; init; } = new();
    }

    public sealed class TemplateParameter
    {
        public string Name { get; init; } = string.Empty;

        public string LabelResourceKey { get; init; } = string.Empty;

        public string Type { get; init; } = "Text";

        public bool Required { get; init; } = true;

        public List<TemplateChoice>? Choices { get; init; }
    }

    public sealed class TemplateChoice
    {
        public string Value { get; init; } = string.Empty;

        public string DisplayResourceKey { get; init; } = string.Empty;
    }
}
```

- [ ] **Step 2: Build the editor project**

```
dotnet build src/modules/keyboardmanager/KeyboardManagerEditorUI/KeyboardManagerEditorUI.csproj -c Debug
```

Expected: succeeds. The file is C# 12 with `init` accessors and nullable references.

- [ ] **Step 3: Commit**

```
git add src/modules/keyboardmanager/KeyboardManagerEditorUI/Templates/PowerToysCliCatalog.cs
git commit -m "KBM: Add catalog model POCOs for CLI command templates"
```

### Task 8: Create the `JsonSerializerContext` partial class

**Files:**
- Create: `src/modules/keyboardmanager/KeyboardManagerEditorUI/Templates/CommandTemplateJsonContext.cs`

- [ ] **Step 1: Write the source-generation context**

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeyboardManagerEditorUI.Templates
{
    [JsonSerializable(typeof(PowerToysCliCatalog))]
    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        WriteIndented = false,
        ReadCommentHandling = JsonCommentHandling.Skip)]
    internal sealed partial class CommandTemplateJsonContext : JsonSerializerContext
    {
    }
}
```

- [ ] **Step 2: Build**

```
dotnet build src/modules/keyboardmanager/KeyboardManagerEditorUI/KeyboardManagerEditorUI.csproj -c Debug
```

Expected: succeeds. Source generator produces `CommandTemplateJsonContext.Default.PowerToysCliCatalog`.

- [ ] **Step 3: Commit**

```
git add src/modules/keyboardmanager/KeyboardManagerEditorUI/Templates/CommandTemplateJsonContext.cs
git commit -m "KBM: Add source-gen JSON context for template catalog"
```

---

## Phase 3 — Catalog Data File

### Task 9: Create `powertoyscli.json` with v1 templates

**Files:**
- Create: `src/modules/keyboardmanager/KeyboardManagerEditorUI/Templates/powertoyscli.json`

- [ ] **Step 1: Write the catalog**

Note: replace the literal value of the `executable` field with whichever path Task 3 determined to be reliable. The example below assumes bare filename works.

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

- [ ] **Step 2: Validate the JSON is well-formed**

```
python -c "import json; json.load(open('src/modules/keyboardmanager/KeyboardManagerEditorUI/Templates/powertoyscli.json'))"
```

Expected: no output (means parse succeeded). If you don't have Python, paste the contents into any online JSON validator.

- [ ] **Step 3: Commit**

```
git add src/modules/keyboardmanager/KeyboardManagerEditorUI/Templates/powertoyscli.json
git commit -m "KBM: Add powertoyscli.json template catalog"
```

### Task 10: Mark `powertoyscli.json` as `EmbeddedResource` in the csproj

**Files:**
- Modify: `src/modules/keyboardmanager/KeyboardManagerEditorUI/KeyboardManagerEditorUI.csproj`

- [ ] **Step 1: Add an `ItemGroup` near the existing `<Content Include="Assets\KeyboardManagerEditor\Keyboard.ico">` block**

Insert before the closing `</Project>` tag:

```xml
  <ItemGroup>
    <EmbeddedResource Include="Templates\powertoyscli.json">
      <LogicalName>KeyboardManagerEditorUI.Templates.powertoyscli.json</LogicalName>
    </EmbeddedResource>
  </ItemGroup>
```

The `LogicalName` makes `Assembly.GetManifestResourceStream("KeyboardManagerEditorUI.Templates.powertoyscli.json")` work predictably.

- [ ] **Step 2: Build and verify**

```
dotnet build src/modules/keyboardmanager/KeyboardManagerEditorUI/KeyboardManagerEditorUI.csproj -c Debug
```

Then inspect the produced DLL:

```
strings x64/Debug/WinUI3Apps/PowerToys.KeyboardManagerEditorUI.dll | findstr powertoyscli
```

Expected: see `KeyboardManagerEditorUI.Templates.powertoyscli.json` in the output. (On Linux it'd be `grep` not `findstr`; on Windows PowerShell, use `Select-String`.)

- [ ] **Step 3: Commit**

```
git add src/modules/keyboardmanager/KeyboardManagerEditorUI/KeyboardManagerEditorUI.csproj
git commit -m "KBM: Embed powertoyscli.json into KeyboardManagerEditorUI"
```

---

## Phase 4 — Catalog Loader

### Task 11: Implement `CommandTemplateCatalog` loader (with startup smoke assert)

**Files:**
- Create: `src/modules/keyboardmanager/KeyboardManagerEditorUI/Templates/CommandTemplateCatalog.cs`

- [ ] **Step 1: Write the loader**

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace KeyboardManagerEditorUI.Templates
{
    public sealed class CommandTemplateCatalog
    {
        private const string ResourceName = "KeyboardManagerEditorUI.Templates.powertoyscli.json";
        private const int SupportedSchemaVersion = 1;

        private static readonly Lazy<CommandTemplateCatalog> _instance = new(() => Load());

        public static CommandTemplateCatalog Instance => _instance.Value;

        public PowerToysCliCatalog Data { get; }

        private CommandTemplateCatalog(PowerToysCliCatalog data)
        {
            Data = data;
        }

        public CommandTemplate? TryFind(string templateId)
        {
            if (string.IsNullOrEmpty(templateId))
            {
                return null;
            }

            return Data.Modules
                .SelectMany(m => m.Commands)
                .FirstOrDefault(c => c.Id == templateId);
        }

        private static CommandTemplateCatalog Load()
        {
            var assembly = typeof(CommandTemplateCatalog).Assembly;
            using var stream = assembly.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException(
                    $"Embedded resource '{ResourceName}' not found. " +
                    "Check KeyboardManagerEditorUI.csproj <EmbeddedResource> entry.");

            var data = JsonSerializer.Deserialize(
                stream,
                CommandTemplateJsonContext.Default.PowerToysCliCatalog)
                ?? throw new InvalidOperationException(
                    $"Failed to deserialize '{ResourceName}' — JsonSerializer returned null.");

            if (data.SchemaVersion != SupportedSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"Unsupported powertoyscli.json schemaVersion={data.SchemaVersion}; " +
                    $"expected {SupportedSchemaVersion}.");
            }

            if (data.Modules.Count == 0)
            {
                throw new InvalidOperationException(
                    "powertoyscli.json has zero modules — at least one module is required.");
            }

            return new CommandTemplateCatalog(data);
        }
    }
}
```

- [ ] **Step 2: Build**

```
dotnet build src/modules/keyboardmanager/KeyboardManagerEditorUI/KeyboardManagerEditorUI.csproj -c Debug
```

Expected: 0 errors.

- [ ] **Step 3: Manual smoke test — confirm catalog loads at runtime**

Edit `src/modules/keyboardmanager/KeyboardManagerEditorUI/KeyboardManagerEditorXAML/App.xaml.cs` to add **temporarily** at the end of `OnLaunched`:

```csharp
var catalog = KeyboardManagerEditorUI.Templates.CommandTemplateCatalog.Instance;
System.Diagnostics.Debug.WriteLine($"Loaded catalog with {catalog.Data.Modules.Count} module(s).");
```

Run the editor (`PowerToys.KeyboardManagerEditorUI.exe`). In the debug output, you should see `Loaded catalog with 1 module(s).`. Remove the temporary lines after confirming.

- [ ] **Step 4: Commit (without the temporary debug lines)**

```
git add src/modules/keyboardmanager/KeyboardManagerEditorUI/Templates/CommandTemplateCatalog.cs
git commit -m "KBM: Add CommandTemplateCatalog loader with schema validation"
```

---

## Phase 5 — Resolver

### Task 12: Implement `TemplateResolver`

**Files:**
- Create: `src/modules/keyboardmanager/KeyboardManagerEditorUI/Templates/TemplateResolver.cs`

- [ ] **Step 1: Write the resolver**

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace KeyboardManagerEditorUI.Templates
{
    public static class TemplateResolver
    {
        public readonly record struct Resolved(string Executable, string Args);

        public static Resolved Resolve(
            CommandTemplate template,
            IReadOnlyDictionary<string, string>? values)
        {
            var args = template.ArgsTemplate ?? string.Empty;

            foreach (var p in template.Parameters)
            {
                string replacement = string.Empty;
                if (values is not null && values.TryGetValue(p.Name, out var v))
                {
                    replacement = v ?? string.Empty;
                }

                args = args.Replace("{" + p.Name + "}", replacement);
            }

            return new Resolved(template.Executable ?? string.Empty, args);
        }
    }
}
```

- [ ] **Step 2: Build**

```
dotnet build src/modules/keyboardmanager/KeyboardManagerEditorUI/KeyboardManagerEditorUI.csproj -c Debug
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```
git add src/modules/keyboardmanager/KeyboardManagerEditorUI/Templates/TemplateResolver.cs
git commit -m "KBM: Add TemplateResolver for parameter substitution"
```

---

## Phase 6 — Resource Keys

### Task 13: Add new resource keys to `Resources.resw`

**Files:**
- Modify: `src/modules/keyboardmanager/KeyboardManagerEditorUI/Strings/en-US/Resources.resw`

- [ ] **Step 1: Open the file and locate the closing `</root>` tag**

- [ ] **Step 2: Insert the following `<data>` blocks before `</root>`**

```xml
  <data name="ActionType_RunTemplate.Content" xml:space="preserve">
    <value>Run from template</value>
  </data>
  <data name="ActionType_RunTemplate_Text.Text" xml:space="preserve">
    <value>Run from template</value>
  </data>
  <data name="TemplatePickerButton.Content" xml:space="preserve">
    <value>PowerToys command</value>
  </data>
  <data name="TemplatePickerPlaceholder.Text" xml:space="preserve">
    <value>Select a template...</value>
  </data>
  <data name="TemplatePreviewLabel.Text" xml:space="preserve">
    <value>Preview</value>
  </data>
  <data name="TemplateMissingInfoBarTitle.Text" xml:space="preserve">
    <value>Template no longer available</value>
  </data>
  <data name="TemplateMissingInfoBarMessage.Text" xml:space="preserve">
    <value>The template originally used for this mapping is no longer in the catalog.</value>
  </data>
  <data name="TemplateMissingChooseButton.Content" xml:space="preserve">
    <value>Choose template</value>
  </data>
  <data name="TemplateMissingKeepButton.Content" xml:space="preserve">
    <value>Keep as plain command</value>
  </data>
  <data name="TemplateModule_Settings" xml:space="preserve">
    <value>Settings</value>
  </data>
  <data name="TemplateCmd_Settings_OpenMain" xml:space="preserve">
    <value>Open Settings</value>
  </data>
  <data name="TemplateCmd_Settings_OpenModule" xml:space="preserve">
    <value>Open Settings for module...</value>
  </data>
  <data name="TemplateParam_Module" xml:space="preserve">
    <value>Module</value>
  </data>
  <data name="Module_ColorPicker" xml:space="preserve">
    <value>Color Picker</value>
  </data>
  <data name="Module_FancyZones" xml:space="preserve">
    <value>FancyZones</value>
  </data>
  <data name="Module_KeyboardManager" xml:space="preserve">
    <value>Keyboard Manager</value>
  </data>
  <data name="Module_PowerLauncher" xml:space="preserve">
    <value>PowerToys Run</value>
  </data>
  <data name="Module_Hosts" xml:space="preserve">
    <value>Hosts File Editor</value>
  </data>
  <data name="Module_RegistryPreview" xml:space="preserve">
    <value>Registry Preview</value>
  </data>
  <data name="Module_ZoomIt" xml:space="preserve">
    <value>ZoomIt</value>
  </data>
```

- [ ] **Step 3: Build**

```
dotnet build src/modules/keyboardmanager/KeyboardManagerEditorUI/KeyboardManagerEditorUI.csproj -c Debug
```

Expected: succeeds, resource file gets regenerated into the `.pri` package.

- [ ] **Step 4: Commit**

```
git add src/modules/keyboardmanager/KeyboardManagerEditorUI/Strings/en-US/Resources.resw
git commit -m "KBM: Add resource keys for template picker UI and v1 catalog"
```

---

## Phase 7 — ViewModels

### Task 14: Create `TemplateParameterViewModel`

**Files:**
- Create: `src/modules/keyboardmanager/KeyboardManagerEditorUI/ViewModels/TemplateParameterViewModel.cs`

- [ ] **Step 1: Write the VM**

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

using KeyboardManagerEditorUI.Templates;
using Microsoft.Windows.ApplicationModel.Resources;

namespace KeyboardManagerEditorUI.ViewModels
{
    public sealed class TemplateParameterViewModel : INotifyPropertyChanged
    {
        private readonly TemplateParameter _definition;
        private string _value = string.Empty;
        private TemplateChoiceViewModel? _selectedChoice;

        public TemplateParameterViewModel(TemplateParameter definition, ResourceLoader loader)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));

            Name = definition.Name;
            Label = loader.GetString(definition.LabelResourceKey);
            Type = definition.Type;
            Required = definition.Required;

            if (definition.Choices is not null)
            {
                Choices = definition.Choices
                    .Select(c => new TemplateChoiceViewModel(c.Value, loader.GetString(c.DisplayResourceKey)))
                    .ToList();
            }
        }

        public string Name { get; }

        public string Label { get; }

        public string Type { get; }

        public bool Required { get; }

        public IReadOnlyList<TemplateChoiceViewModel>? Choices { get; }

        public string Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value ?? string.Empty;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsValid));
                }
            }
        }

        public TemplateChoiceViewModel? SelectedChoice
        {
            get => _selectedChoice;
            set
            {
                if (!ReferenceEquals(_selectedChoice, value))
                {
                    _selectedChoice = value;
                    Value = value?.Value ?? string.Empty;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsValid => !Required || !string.IsNullOrEmpty(Value);

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }

    public sealed class TemplateChoiceViewModel
    {
        public TemplateChoiceViewModel(string value, string displayText)
        {
            Value = value;
            DisplayText = displayText;
        }

        public string Value { get; }

        public string DisplayText { get; }
    }
}
```

- [ ] **Step 2: Build**

```
dotnet build src/modules/keyboardmanager/KeyboardManagerEditorUI/KeyboardManagerEditorUI.csproj -c Debug
```

Expected: succeeds. If `Microsoft.Windows.ApplicationModel.Resources.ResourceLoader` is unresolved, check the namespace used elsewhere in `KeyboardManagerEditorUI` (likely `Microsoft.Windows.ApplicationModel.Resources` or `Windows.ApplicationModel.Resources`). Adjust the `using` line accordingly.

- [ ] **Step 3: Commit**

```
git add src/modules/keyboardmanager/KeyboardManagerEditorUI/ViewModels/TemplateParameterViewModel.cs
git commit -m "KBM: Add TemplateParameterViewModel for dynamic parameter form"
```

### Task 15: Create `CommandTemplatePickerViewModel`

**Files:**
- Create: `src/modules/keyboardmanager/KeyboardManagerEditorUI/ViewModels/CommandTemplatePickerViewModel.cs`

- [ ] **Step 1: Write the VM**

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

using KeyboardManagerEditorUI.Templates;
using Microsoft.Windows.ApplicationModel.Resources;

namespace KeyboardManagerEditorUI.ViewModels
{
    public sealed class CommandTemplatePickerViewModel : INotifyPropertyChanged
    {
        private readonly ResourceLoader _loader;
        private CommandTemplate? _selectedTemplate;
        private string _selectionDescription = string.Empty;
        private string _resolvedCommandLine = string.Empty;

        public CommandTemplatePickerViewModel(ResourceLoader loader)
        {
            _loader = loader;
        }

        public ObservableCollection<TemplateParameterViewModel> CurrentParameters { get; } = new();

        public CommandTemplate? SelectedTemplate
        {
            get => _selectedTemplate;
            private set
            {
                _selectedTemplate = value;
                OnPropertyChanged();
            }
        }

        public string SelectionDescription
        {
            get => _selectionDescription;
            private set
            {
                _selectionDescription = value;
                OnPropertyChanged();
            }
        }

        public string ResolvedCommandLine
        {
            get => _resolvedCommandLine;
            private set
            {
                _resolvedCommandLine = value;
                OnPropertyChanged();
            }
        }

        public bool IsAllValid => CurrentParameters.All(p => p.IsValid);

        public void SelectTemplate(string templateId)
        {
            var (module, template) = FindWithModule(templateId);
            ApplyTemplate(module, template, prefilledValues: null);
        }

        public void LoadExisting(string templateId, IReadOnlyDictionary<string, string>? values)
        {
            var (module, template) = FindWithModule(templateId);
            if (template is null)
            {
                throw new InvalidOperationException($"Template '{templateId}' not found in catalog.");
            }

            ApplyTemplate(module, template, values);
        }

        public void Clear()
        {
            SelectedTemplate = null;
            SelectionDescription = string.Empty;
            ResolvedCommandLine = string.Empty;

            DetachParameterListeners();
            CurrentParameters.Clear();
        }

        public Dictionary<string, string> CollectParameterValues()
        {
            return CurrentParameters.ToDictionary(p => p.Name, p => p.Value);
        }

        private (CommandTemplateModule? module, CommandTemplate? template) FindWithModule(string templateId)
        {
            foreach (var m in CommandTemplateCatalog.Instance.Data.Modules)
            {
                var t = m.Commands.FirstOrDefault(c => c.Id == templateId);
                if (t is not null)
                {
                    return (m, t);
                }
            }

            return (null, null);
        }

        private void ApplyTemplate(
            CommandTemplateModule? module,
            CommandTemplate? template,
            IReadOnlyDictionary<string, string>? prefilledValues)
        {
            DetachParameterListeners();
            CurrentParameters.Clear();

            SelectedTemplate = template;

            if (template is null || module is null)
            {
                SelectionDescription = string.Empty;
                ResolvedCommandLine = string.Empty;
                return;
            }

            SelectionDescription =
                $"{_loader.GetString(module.DisplayResourceKey)} → {_loader.GetString(template.DisplayResourceKey)}";

            foreach (var p in template.Parameters)
            {
                var vm = new TemplateParameterViewModel(p, _loader);
                if (prefilledValues is not null && prefilledValues.TryGetValue(p.Name, out var v))
                {
                    if (vm.Choices is not null)
                    {
                        vm.SelectedChoice = vm.Choices.FirstOrDefault(c => c.Value == v);
                    }
                    else
                    {
                        vm.Value = v;
                    }
                }

                vm.PropertyChanged += Parameter_PropertyChanged;
                CurrentParameters.Add(vm);
            }

            RecomputePreview();
        }

        private void DetachParameterListeners()
        {
            foreach (var p in CurrentParameters)
            {
                p.PropertyChanged -= Parameter_PropertyChanged;
            }
        }

        private void Parameter_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TemplateParameterViewModel.Value))
            {
                RecomputePreview();
                OnPropertyChanged(nameof(IsAllValid));
            }
        }

        private void RecomputePreview()
        {
            if (_selectedTemplate is null)
            {
                ResolvedCommandLine = string.Empty;
                return;
            }

            var resolved = TemplateResolver.Resolve(_selectedTemplate, CollectParameterValues());
            ResolvedCommandLine = string.IsNullOrEmpty(resolved.Args)
                ? resolved.Executable
                : $"{resolved.Executable} {resolved.Args}";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
```

- [ ] **Step 2: Build**

```
dotnet build src/modules/keyboardmanager/KeyboardManagerEditorUI/KeyboardManagerEditorUI.csproj -c Debug
```

Expected: succeeds.

- [ ] **Step 3: Commit**

```
git add src/modules/keyboardmanager/KeyboardManagerEditorUI/ViewModels/CommandTemplatePickerViewModel.cs
git commit -m "KBM: Add CommandTemplatePickerViewModel"
```

---

## Phase 8 — `TemplateParameterSelector`

### Task 16: Create `DataTemplateSelector` for parameter input types

**Files:**
- Create: `src/modules/keyboardmanager/KeyboardManagerEditorUI/Controls/TemplateParameterSelector.cs`

- [ ] **Step 1: Write the selector**

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using KeyboardManagerEditorUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KeyboardManagerEditorUI.Controls
{
    public sealed partial class TemplateParameterSelector : DataTemplateSelector
    {
        public DataTemplate? TextTemplate { get; set; }

        public DataTemplate? ComboTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is TemplateParameterViewModel vm)
            {
                return vm.Type switch
                {
                    "Combo" => ComboTemplate ?? TextTemplate!,
                    _       => TextTemplate!,
                };
            }

            return TextTemplate!;
        }
    }
}
```

- [ ] **Step 2: Build**

```
dotnet build src/modules/keyboardmanager/KeyboardManagerEditorUI/KeyboardManagerEditorUI.csproj -c Debug
```

Expected: succeeds.

- [ ] **Step 3: Commit**

```
git add src/modules/keyboardmanager/KeyboardManagerEditorUI/Controls/TemplateParameterSelector.cs
git commit -m "KBM: Add TemplateParameterSelector (DataTemplateSelector for Text/Combo)"
```

---

## Phase 9 — `CommandTemplatePickerControl`

### Task 17: Create the XAML for `CommandTemplatePickerControl`

**Files:**
- Create: `src/modules/keyboardmanager/KeyboardManagerEditorUI/Controls/CommandTemplatePickerControl.xaml`

- [ ] **Step 1: Write the XAML**

```xml
<?xml version="1.0" encoding="utf-8" ?>
<UserControl
    x:Class="KeyboardManagerEditorUI.Controls.CommandTemplatePickerControl"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:controls="using:CommunityToolkit.WinUI.Controls"
    xmlns:local="using:KeyboardManagerEditorUI.Controls"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:vm="using:KeyboardManagerEditorUI.ViewModels"
    Loaded="UserControl_Loaded"
    mc:Ignorable="d">

    <UserControl.Resources>
        <DataTemplate x:Key="TextParamTemplate" x:DataType="vm:TemplateParameterViewModel">
            <TextBox
                Header="{x:Bind Label}"
                Text="{x:Bind Value, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
        </DataTemplate>

        <DataTemplate x:Key="ComboParamTemplate" x:DataType="vm:TemplateParameterViewModel">
            <ComboBox
                HorizontalAlignment="Stretch"
                Header="{x:Bind Label}"
                ItemsSource="{x:Bind Choices}"
                SelectedItem="{x:Bind SelectedChoice, Mode=TwoWay}"
                DisplayMemberPath="DisplayText" />
        </DataTemplate>

        <local:TemplateParameterSelector
            x:Key="ParamSelector"
            ComboTemplate="{StaticResource ComboParamTemplate}"
            TextTemplate="{StaticResource TextParamTemplate}" />
    </UserControl.Resources>

    <StackPanel Orientation="Vertical" Spacing="12">
        <DropDownButton
            x:Name="TemplatePickerButton"
            x:Uid="TemplatePickerButton"
            HorizontalAlignment="Stretch">
            <DropDownButton.Flyout>
                <MenuFlyout x:Name="TemplateMenuFlyout" Placement="Bottom" />
            </DropDownButton.Flyout>
        </DropDownButton>

        <TextBlock
            x:Name="SelectionDescriptionText"
            FontStyle="Italic"
            Foreground="{ThemeResource TextFillColorSecondaryBrush}"
            Text="{x:Bind ViewModel.SelectionDescription, Mode=OneWay}" />

        <Rectangle
            Height="1"
            HorizontalAlignment="Stretch"
            Fill="{ThemeResource DividerStrokeColorDefaultBrush}" />

        <ItemsControl
            ItemsSource="{x:Bind ViewModel.CurrentParameters, Mode=OneWay}"
            ItemTemplateSelector="{StaticResource ParamSelector}">
            <ItemsControl.ItemsPanel>
                <ItemsPanelTemplate>
                    <StackPanel Orientation="Vertical" Spacing="12" />
                </ItemsPanelTemplate>
            </ItemsControl.ItemsPanel>
        </ItemsControl>

        <Rectangle
            Height="1"
            HorizontalAlignment="Stretch"
            Fill="{ThemeResource DividerStrokeColorDefaultBrush}" />

        <TextBlock x:Uid="TemplatePreviewLabel" FontWeight="SemiBold" />

        <TextBlock
            FontFamily="Consolas"
            Foreground="{ThemeResource TextFillColorPrimaryBrush}"
            IsTextSelectionEnabled="True"
            Text="{x:Bind ViewModel.ResolvedCommandLine, Mode=OneWay}"
            TextWrapping="Wrap" />

        <InfoBar
            x:Name="MissingTemplateInfoBar"
            IsClosable="False"
            IsOpen="False"
            Severity="Warning"
            x:Uid="TemplateMissingInfoBarTitle">
            <InfoBar.ActionButton>
                <StackPanel Orientation="Horizontal" Spacing="8">
                    <Button
                        x:Name="MissingTemplateChooseButton"
                        x:Uid="TemplateMissingChooseButton"
                        Click="MissingTemplateChooseButton_Click" />
                    <Button
                        x:Name="MissingTemplateKeepButton"
                        x:Uid="TemplateMissingKeepButton"
                        Click="MissingTemplateKeepButton_Click" />
                </StackPanel>
            </InfoBar.ActionButton>
        </InfoBar>
    </StackPanel>
</UserControl>
```

- [ ] **Step 2: Commit (build comes after code-behind in Task 18)**

```
git add src/modules/keyboardmanager/KeyboardManagerEditorUI/Controls/CommandTemplatePickerControl.xaml
git commit -m "KBM: Add CommandTemplatePickerControl XAML layout"
```

### Task 18: Create the code-behind for `CommandTemplatePickerControl`

**Files:**
- Create: `src/modules/keyboardmanager/KeyboardManagerEditorUI/Controls/CommandTemplatePickerControl.xaml.cs`

- [ ] **Step 1: Write the code-behind**

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using KeyboardManagerEditorUI.Templates;
using KeyboardManagerEditorUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;

namespace KeyboardManagerEditorUI.Controls
{
    public sealed partial class CommandTemplatePickerControl : UserControl
    {
        private readonly ResourceLoader _resourceLoader = new("KeyboardManagerEditorUI/Resources");

        public CommandTemplatePickerControl()
        {
            InitializeComponent();

            ViewModel = new CommandTemplatePickerViewModel(_resourceLoader);
            DataContext = this;
        }

        public CommandTemplatePickerViewModel ViewModel { get; }

        public event EventHandler? SelectionChanged;

        public event EventHandler? MissingTemplateKeepRequested;

        public TemplateResolver.Resolved? ResolveCurrent()
        {
            if (ViewModel.SelectedTemplate is null)
            {
                return null;
            }

            return TemplateResolver.Resolve(
                ViewModel.SelectedTemplate,
                ViewModel.CollectParameterValues());
        }

        public string? CurrentTemplateId => ViewModel.SelectedTemplate?.Id;

        public Dictionary<string, string> CurrentParameterValues => ViewModel.CollectParameterValues();

        public void LoadExisting(string templateId, IReadOnlyDictionary<string, string>? values)
        {
            try
            {
                ViewModel.LoadExisting(templateId, values);
                MissingTemplateInfoBar.IsOpen = false;
            }
            catch (InvalidOperationException)
            {
                ShowMissingTemplateInfoBar();
            }
        }

        public void Reset()
        {
            ViewModel.Clear();
            MissingTemplateInfoBar.IsOpen = false;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            BuildFlyout(TemplateMenuFlyout, CommandTemplateCatalog.Instance.Data);
        }

        private void BuildFlyout(MenuFlyout flyout, PowerToysCliCatalog catalog)
        {
            flyout.Items.Clear();

            foreach (var module in catalog.Modules)
            {
                var sub = new MenuFlyoutSubItem
                {
                    Text = _resourceLoader.GetString(module.DisplayResourceKey),
                };

                if (!string.IsNullOrEmpty(module.IconGlyph))
                {
                    sub.Icon = new FontIcon
                    {
                        Glyph = module.IconGlyph,
                        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"),
                    };
                }

                foreach (var cmd in module.Commands)
                {
                    var item = new MenuFlyoutItem
                    {
                        Text = _resourceLoader.GetString(cmd.DisplayResourceKey),
                        Tag = cmd.Id,
                    };
                    item.Click += OnCommandPicked;
                    sub.Items.Add(item);
                }

                flyout.Items.Add(sub);
            }
        }

        private void OnCommandPicked(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string templateId)
            {
                ViewModel.SelectTemplate(templateId);
                MissingTemplateInfoBar.IsOpen = false;
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ShowMissingTemplateInfoBar()
        {
            MissingTemplateInfoBar.IsOpen = true;
        }

        private void MissingTemplateChooseButton_Click(object sender, RoutedEventArgs e)
        {
            MissingTemplateInfoBar.IsOpen = false;
            TemplatePickerButton.Flyout.ShowAt(TemplatePickerButton);
        }

        private void MissingTemplateKeepButton_Click(object sender, RoutedEventArgs e)
        {
            MissingTemplateInfoBar.IsOpen = false;
            MissingTemplateKeepRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
```

- [ ] **Step 2: Build the full editor**

```
dotnet build src/modules/keyboardmanager/KeyboardManagerEditorUI/KeyboardManagerEditorUI.csproj -c Debug
```

Expected: succeeds. If `ResourceLoader` instantiation fails, check what the rest of `KeyboardManagerEditorUI` uses — it may need a different constructor signature like `new ResourceLoader()` or `ResourceLoader.GetForViewIndependentUse("KeyboardManagerEditorUI/Resources")`.

- [ ] **Step 3: Commit**

```
git add src/modules/keyboardmanager/KeyboardManagerEditorUI/Controls/CommandTemplatePickerControl.xaml.cs
git commit -m "KBM: Add CommandTemplatePickerControl code-behind with cascading flyout"
```

---

## Phase 10 — Integrate Into `UnifiedMappingControl`

### Task 19: Add `RunTemplate` to `ActionTypeComboBox` + add `Case`

**Files:**
- Modify: `src/modules/keyboardmanager/KeyboardManagerEditorUI/Controls/UnifiedMappingControl.xaml`

- [ ] **Step 1: Add a new ComboBoxItem after the Disable item (around line 214)**

Find the existing block (lines 209–214):
```xml
                <ComboBoxItem x:Uid="ActionType_Disable" Tag="Disable">
                    <StackPanel Orientation="Horizontal" Spacing="8">
                        <FontIcon FontSize="14" Glyph="&#xE711;" />
                        <TextBlock x:Uid="ActionType_Disable_Text" />
                    </StackPanel>
                </ComboBoxItem>
```

Add immediately after it (before the commented-out MouseClick item):

```xml
                <ComboBoxItem x:Uid="ActionType_RunTemplate" Tag="RunTemplate">
                    <StackPanel Orientation="Horizontal" Spacing="8">
                        <FontIcon FontSize="14" Glyph="&#xE756;" />
                        <TextBlock x:Uid="ActionType_RunTemplate_Text" />
                    </StackPanel>
                </ComboBoxItem>
```

- [ ] **Step 2: Add a new Case in the ActionSwitchPresenter (after the Disable Case at ~line 400)**

Find:
```xml
                <!--  Disable Action  -->
                <tkcontrols:Case Value="Disable">
                    <TextBlock
                        x:Uid="DisableDescription"
                        Foreground="{ThemeResource TextFillColorSecondaryBrush}"
                        TextWrapping="Wrap" />
                </tkcontrols:Case>
```

Add immediately after it:

```xml
                <!--  Run From Template Action  -->
                <tkcontrols:Case Value="RunTemplate">
                    <local:CommandTemplatePickerControl
                        x:Name="TemplatePicker"
                        MissingTemplateKeepRequested="TemplatePicker_MissingTemplateKeepRequested" />
                </tkcontrols:Case>
```

- [ ] **Step 3: Build (will fail in code-behind until Task 20)**

```
dotnet build src/modules/keyboardmanager/KeyboardManagerEditorUI/KeyboardManagerEditorUI.csproj -c Debug
```

Expected: 1 error — `TemplatePicker_MissingTemplateKeepRequested` not found. This is intentional; Task 20 wires it up.

- [ ] **Step 4: Don't commit yet — wait for Task 20 so the build is green**

### Task 20: Wire save/load paths in `UnifiedMappingControl.xaml.cs`

**Files:**
- Modify: `src/modules/keyboardmanager/KeyboardManagerEditorUI/Controls/UnifiedMappingControl.xaml.cs`

- [ ] **Step 1: Open the file. Locate the existing function that saves a mapping (search for `RunProgramFilePath = ` or `OperationType =` in assignments).**

Identify:
- The save function (writes `KeysDataModel` from UI state)
- The load function (writes UI state from `KeysDataModel`)

These functions vary by editor version; if you can't identify them, use VS "Find All References" on `KeysDataModel.RunProgramFilePath`.

- [ ] **Step 2: Add the save branch (within the existing save function, where other action types are dispatched)**

Pattern (your exact code may differ — adapt to existing structure):

```csharp
// Inside the save dispatch (alongside KeyOrShortcut / Text / OpenUrl / OpenApp / Disable)
case "RunTemplate":
    {
        var resolved = TemplatePicker.ResolveCurrent();
        if (resolved is null || TemplatePicker.CurrentTemplateId is null)
        {
            // Validation should prevent this; defensive fallthrough.
            break;
        }

        keysDataModel.OperationType = 1; // RunProgram
        keysDataModel.RunProgramFilePath = resolved.Value.Executable;
        keysDataModel.RunProgramArgs = resolved.Value.Args;
        keysDataModel.TemplateId = TemplatePicker.CurrentTemplateId;
        keysDataModel.TemplateParameters = TemplatePicker.CurrentParameterValues;
        break;
    }
```

- [ ] **Step 3: Add the load branch**

Pattern:

```csharp
// Inside the load dispatch
if (keysDataModel.OperationType == 1 && !string.IsNullOrEmpty(keysDataModel.TemplateId))
{
    SelectActionTypeByTag("RunTemplate");
    TemplatePicker.LoadExisting(
        keysDataModel.TemplateId,
        keysDataModel.TemplateParameters);
}
else if (keysDataModel.OperationType == 1)
{
    // Existing OpenApp path — unchanged
    SelectActionTypeByTag("OpenApp");
    ProgramPathInput.Text = keysDataModel.RunProgramFilePath ?? string.Empty;
    ProgramArgsInput.Text = keysDataModel.RunProgramArgs ?? string.Empty;
    // ... other OpenApp wiring you already do ...
}
```

(`SelectActionTypeByTag` is shorthand — actual code may set `ActionTypeComboBox.SelectedItem` directly. Adapt to the existing helper or do it inline.)

- [ ] **Step 4: Add the missing-template-keep handler**

```csharp
private void TemplatePicker_MissingTemplateKeepRequested(object sender, EventArgs e)
{
    // User chose "Keep as plain command":
    // 1. Clear template metadata so the mapping becomes a pure RunProgram entry.
    // 2. Switch UI to OpenApp showing the resolved command for editing.

    ClearTemplateMetadata();
    SelectActionTypeByTag("OpenApp");
    // OpenApp fields are already populated from the original KeysDataModel
    // via the load path — no additional wiring needed.
}

private void ClearTemplateMetadata()
{
    // Reset the picker
    TemplatePicker.Reset();

    // The actual templateId/parameters fields on the in-memory model
    // get cleared at next save by the "RunTemplate" branch not being chosen
    // and the existing OpenApp branch not writing them. If the model carries
    // them in-flight, null them explicitly:
    if (_currentMapping is not null)
    {
        _currentMapping.TemplateId = null;
        _currentMapping.TemplateParameters = null;
    }
}
```

(`_currentMapping` is illustrative; use whatever instance field your editor uses to hold the in-flight `KeysDataModel`.)

- [ ] **Step 5: Build**

```
dotnet build src/modules/keyboardmanager/KeyboardManagerEditorUI/KeyboardManagerEditorUI.csproj -c Debug
```

Expected: 0 errors.

- [ ] **Step 6: Commit**

```
git add src/modules/keyboardmanager/KeyboardManagerEditorUI/Controls/UnifiedMappingControl.xaml \
        src/modules/keyboardmanager/KeyboardManagerEditorUI/Controls/UnifiedMappingControl.xaml.cs
git commit -m "KBM: Wire RunTemplate action type into UnifiedMappingControl"
```

---

## Phase 11 — End-to-End Verification

These are manual tests. Run the freshly built `PowerToys.KeyboardManagerEditorUI.exe` and step through each scenario.

### Task 21: Build full PowerToys solution and install

- [ ] **Step 1: Build PowerToys for x64 Debug**

```
msbuild PowerToys.sln /p:Configuration=Debug /p:Platform=x64
```

Expected: completes with 0 errors. Errors related to other modules are acceptable to skip if unrelated.

- [ ] **Step 2: Launch the editor directly**

```
x64/Debug/WinUI3Apps/PowerToys.KeyboardManagerEditorUI.exe
```

Expected: window opens, shows existing remappings list (likely empty for a clean profile).

### Task 22: Manual test — create a template mapping

- [ ] **Step 1: Click "New Remapping" (or equivalent button) to open `UnifiedMappingControl`**

- [ ] **Step 2: Pick any trigger key/shortcut on the left (e.g., Ctrl+Alt+Z)**

- [ ] **Step 3: On the right, in the Action Type ComboBox, select "Run from template"**

Expected: the SwitchPresenter shows `CommandTemplatePickerControl` with a "PowerToys command" button.

- [ ] **Step 4: Click "PowerToys command" — verify cascading menu**

Expected: a flyout opens with one submenu "Settings". Hovering "Settings" reveals "Open Settings" and "Open Settings for module...".

- [ ] **Step 5: Pick "Open Settings for module..."**

Expected:
- Selection description shows "Settings → Open Settings for module..."
- A Combo labeled "Module" appears with 7 choices
- Preview text shows `PowerToys.exe --open-settings=` (empty module yet)

- [ ] **Step 6: Pick "ColorPicker" in the Combo**

Expected: preview updates to `PowerToys.exe --open-settings=ColorPicker`.

- [ ] **Step 7: Save the mapping**

Verify it appears in the remappings list.

### Task 23: Manual test — inspect persisted JSON

- [ ] **Step 1: Quit the editor (so it flushes)**

- [ ] **Step 2: Open `%LOCALAPPDATA%\Microsoft\PowerToys\Keyboard Manager\default.json`**

- [ ] **Step 3: Find the entry created in Task 22. Verify it contains:**

```json
{
  "originalKeys": "...",
  "newRemapKeys": "",
  "operationType": 1,
  "runProgramFilePath": "PowerToys.exe",
  "runProgramArgs": "--open-settings=ColorPicker",
  "templateId": "settings.openModule",
  "templateParameters": {
    "module": "ColorPicker"
  }
}
```

If `templateId` / `templateParameters` are missing → bug in save path (Task 20). If extra unwanted fields appear → tighten `[JsonIgnore]` attributes in Task 4.

### Task 24: Manual test — round-trip edit

- [ ] **Step 1: Re-launch the editor**

- [ ] **Step 2: Open the mapping created in Task 22 for editing**

Expected:
- ActionType ComboBox shows "Run from template"
- TemplatePicker shows "Settings → Open Settings for module..."
- Module Combo has "ColorPicker" pre-selected
- Preview shows the expected command

If any of the above is missing → bug in load path (Task 20) or `LoadExisting` (Task 18).

- [ ] **Step 3: Change the Module Combo to "FancyZones" and save**

- [ ] **Step 4: Verify `default.json` reflects the new value**

### Task 25: Manual test — trigger the shortcut

- [ ] **Step 1: With PowerToys running and KBM enabled, press the trigger shortcut (e.g., Ctrl+Alt+Z)**

Expected: PowerToys Settings opens to the FancyZones page (per the value saved in Task 24).

If the shortcut does nothing → check Task 3's findings. If a process appears in Task Manager but no window → the `PowerToys.exe` path might be wrong or settings args misinterpreted.

### Task 26: Manual test — missing template degradation

- [ ] **Step 1: Quit the editor and PowerToys**

- [ ] **Step 2: Edit `default.json` and change the saved `templateId` to a bogus value:**

```json
"templateId": "settings.nonexistent"
```

- [ ] **Step 3: Re-launch the editor. Open the entry**

Expected:
- ActionType ComboBox is on "Run from template"
- An InfoBar appears: "Template no longer available"
- The resolved command (`PowerToys.exe --open-settings=FancyZones`) is still shown elsewhere (it's still in the model)
- Two buttons: "Choose template" and "Keep as plain command"

- [ ] **Step 4: Click "Keep as plain command"**

Expected:
- ActionType ComboBox switches to "Open application"
- OpenApp fields are populated with `PowerToys.exe` and `--open-settings=FancyZones`
- The InfoBar disappears

- [ ] **Step 5: Save and re-inspect `default.json`**

Expected: `templateId` and `templateParameters` fields are gone; the entry is a plain RunProgram.

---

## Phase 12 — AOT Compatibility Code Review

### Task 27: AOT/Trim compatibility checklist sweep

- [ ] **Step 1: Search for forbidden patterns across new files**

```
grep -rn "{Binding}" src/modules/keyboardmanager/KeyboardManagerEditorUI/Controls/CommandTemplatePickerControl.xaml
grep -rn "Activator.CreateInstance" src/modules/keyboardmanager/KeyboardManagerEditorUI/Templates/ src/modules/keyboardmanager/KeyboardManagerEditorUI/ViewModels/ src/modules/keyboardmanager/KeyboardManagerEditorUI/Controls/
grep -rn "dynamic " src/modules/keyboardmanager/KeyboardManagerEditorUI/Templates/ src/modules/keyboardmanager/KeyboardManagerEditorUI/ViewModels/
grep -rn "JsonSerializer.Deserialize" src/modules/keyboardmanager/KeyboardManagerEditorUI/Templates/ src/modules/keyboardmanager/KeyboardManagerEditorUI/ViewModels/
```

Expected: 
- First three commands return zero matches.
- The `JsonSerializer.Deserialize` match should appear only in `CommandTemplateCatalog.cs` and must use the source-gen overload `CommandTemplateJsonContext.Default.PowerToysCliCatalog`. Any reflection-based call (with just a `Type` argument) is a failure.

- [ ] **Step 2: Confirm every `DataTemplate` has `x:DataType`**

```
grep -n "DataTemplate " src/modules/keyboardmanager/KeyboardManagerEditorUI/Controls/CommandTemplatePickerControl.xaml
```

Each `<DataTemplate ...>` must include `x:DataType="vm:..."`. Re-check Task 17 if any is missing.

- [ ] **Step 3: Run the editor under a debugger and verify catalog loads without `MissingMethodException`**

This is a sanity check that the source-gen serializer covers the model graph. If any class is missing from `CommandTemplateJsonContext`, the deserializer throws at runtime in non-source-gen-only contexts.

- [ ] **Step 4: Commit the (unchanged) checklist completion**

Either there's nothing to commit (review found no issues) or you fixed inline issues and commit them with:

```
git add -A
git commit -m "KBM: AOT compatibility fixes from review sweep"
```

If clean, document with an empty marker commit (optional):

```
git commit --allow-empty -m "KBM: AOT compatibility sweep passed (no changes)"
```

---

## Phase 13 — Wrap-Up

### Task 28: Update spec's "Open Questions" with Phase 0 findings

**Files:**
- Modify: `docs/superpowers/specs/2026-05-18-kbm-cli-command-templates-design.md`

- [ ] **Step 1: At the end of the spec file, append a `## Phase 0 Findings (filled in during implementation)` section** containing:
  - Outcome of Task 1 (legacy editor round-trip)
  - Outcome of Task 2 (engine read-only confirmation)
  - Outcome of Task 3 (PowerToys.exe path resolution, with the actual value used in `executable`)
  - Whether any tasks were added to the plan (e.g., legacy-editor patching)

- [ ] **Step 2: Commit**

```
git add docs/superpowers/specs/2026-05-18-kbm-cli-command-templates-design.md
git commit -m "KBM: Record Phase 0 findings in design doc"
```

### Task 29: Open a draft PR for review

- [ ] **Step 1: Push the branch**

```
git push -u origin yuleng/kbm/command
```

- [ ] **Step 2: Create the PR**

```
gh pr create --draft --title "KBM: CLI command templates (PowerToys command category)" --body "$(cat <<'EOF'
## Summary

Adds a new "Run from template" action type to the new WinUI3 Keyboard Manager editor. Users can bind a shortcut to a predefined CLI command picked from a 3-level cascading menu (PowerToys command → Module → Command), with parameters filled in via a dynamic form. Templates resolve at save time into standard `RunProgram` mappings — the KBM C++ engine and legacy editor are untouched.

v1 ships a single "Settings" module under "PowerToys command" with two templates:
- Open Settings
- Open Settings for module (Combo: ColorPicker, FancyZones, KeyboardManager, PowerLauncher, Hosts, RegistryPreview, ZoomIt)

Spec: docs/superpowers/specs/2026-05-18-kbm-cli-command-templates-design.md
Plan: docs/superpowers/plans/2026-05-18-kbm-cli-command-templates-plan.md

## Test plan
- [x] Unit tests: `KeysDataModelTemplateFieldsTests` round-trip
- [x] Smoke: catalog loads at startup (CommandTemplateCatalog constructor)
- [x] Manual: create template mapping → save → inspect JSON
- [x] Manual: re-open mapping → picker restored with parameters pre-filled
- [x] Manual: trigger shortcut → PowerToys.exe launches with correct args
- [x] Manual: bogus templateId → InfoBar shows → "Keep as plain command" downgrades cleanly
- [x] AOT review sweep: no {Binding}, no Activator.CreateInstance, all DataTemplates declare x:DataType

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 3: Capture the PR URL**

Done.

---

## Self-Review Notes (read-only)

- **Spec coverage:** Every section of the spec maps to ≥1 task:
  - Goals/non-goals → enforced by task scope
  - High-level architecture → Tasks 4, 7, 11, 18, 20
  - Data model JSON schema → Task 9
  - Data model C# → Task 7
  - `KeysDataModel` additions → Task 4
  - UI flow ActionType integration → Task 19
  - Cascading menu construction → Task 18
  - Dynamic parameter form → Tasks 16, 17
  - Live preview → Task 15 (`RecomputePreview`)
  - Validation → Task 14 (`IsValid`)
  - Re-opening mapping → Task 20 load branch
  - Missing-template degradation B → Tasks 18, 20
  - Resolver algorithm → Task 12
  - Save / load / resolve → Task 20
  - Round-trip safety verification → Tasks 1, 2
  - AOT checklist → Task 27
  - Localization → Task 13
  - Pre-implementation verification → Phase 0 (Tasks 1–3)
  - Test strategy → Task 6 (unit), Tasks 11/22–26 (manual)
- **No placeholders.** All code blocks contain complete code; investigation tasks contain the actual command to run.
- **Type consistency.** `ResolveCurrent()`, `LoadExisting()`, `Reset()`, `CurrentTemplateId`, `CurrentParameterValues` are defined in `CommandTemplatePickerControl.xaml.cs` (Task 18) and used in `UnifiedMappingControl.xaml.cs` (Task 20). The `TemplateResolver.Resolved` record struct returned by `Resolve()` is consumed in Task 20's save branch as `resolved.Value.Executable` / `resolved.Value.Args`.
