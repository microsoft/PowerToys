# Macro Flowchart — Plan 1: Data Model

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `Branch` step type, `MacroCondition`, and `MacroBranch` records to `MacroCommon`, wire them into JSON serialization, and verify with round-trip tests.

**Architecture:** Pure model layer — no engine or UI changes. New files in `MacroCommon/Models/`, one new attribute on `MacroStep`, one enum value in `StepType`, three new `[JsonSerializable]` registrations in `MacroJsonContext`. Tests live in the existing `MacroEngine.Tests` project which already references `MacroCommon`.

**Tech Stack:** C# 13 records, `System.Text.Json` source generation, MSTest.

---

## File Map

| Action | Path |
|--------|------|
| Create | `src/modules/Macro/MacroCommon/Models/ConditionType.cs` |
| Create | `src/modules/Macro/MacroCommon/Models/MacroCondition.cs` |
| Create | `src/modules/Macro/MacroCommon/Models/MacroBranch.cs` |
| Modify | `src/modules/Macro/MacroCommon/Models/StepType.cs` |
| Modify | `src/modules/Macro/MacroCommon/Models/MacroStep.cs` |
| Modify | `src/modules/Macro/MacroCommon/Serialization/MacroJsonContext.cs` |
| Create | `src/modules/Macro/MacroEngine.Tests/BranchSerializationTests.cs` |

---

### Task 1: Add `ConditionType` enum

**Files:**
- Create: `src/modules/Macro/MacroCommon/Models/ConditionType.cs`

- [ ] **Step 1: Create the file**

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace PowerToys.MacroCommon.Models;

public enum ConditionType
{
    [JsonStringEnumMemberName("app_is_focused")]
    AppIsFocused,

    [JsonStringEnumMemberName("app_is_running")]
    AppIsRunning,

    [JsonStringEnumMemberName("time_after")]
    TimeAfter,

    [JsonStringEnumMemberName("time_before")]
    TimeBefore,
}
```

- [ ] **Step 2: Build to verify no errors**

```
msbuild src/modules/Macro/MacroCommon/MacroCommon.csproj /p:Configuration=Debug /p:Platform=x64 /t:Build /m /nologo /v:minimal
```

Expected: `Build succeeded.`

---

### Task 2: Add `MacroCondition` record

**Files:**
- Create: `src/modules/Macro/MacroCommon/Models/MacroCondition.cs`

- [ ] **Step 1: Create the file**

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.MacroCommon.Models;

public sealed record MacroCondition
{
    public ConditionType Type { get; init; }

    public string? ProcessName { get; init; }

    public string? Time { get; init; }
}
```

- [ ] **Step 2: Build**

```
msbuild src/modules/Macro/MacroCommon/MacroCommon.csproj /p:Configuration=Debug /p:Platform=x64 /t:Build /m /nologo /v:minimal
```

Expected: `Build succeeded.`

---

### Task 3: Add `MacroBranch` record

**Files:**
- Create: `src/modules/Macro/MacroCommon/Models/MacroBranch.cs`

- [ ] **Step 1: Create the file**

`MacroBranch` pairs an optional condition with a list of steps. `Condition = null` means "else/default".

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.MacroCommon.Models;

public sealed record MacroBranch
{
    public MacroCondition? Condition { get; init; }

    public List<MacroStep> Steps { get; init; } = [];
}
```

- [ ] **Step 2: Build**

```
msbuild src/modules/Macro/MacroCommon/MacroCommon.csproj /p:Configuration=Debug /p:Platform=x64 /t:Build /m /nologo /v:minimal
```

Expected: `Build succeeded.`

---

### Task 4: Add `Branch` to `StepType`

**Files:**
- Modify: `src/modules/Macro/MacroCommon/Models/StepType.cs`

Current file ends with `Repeat,`. Add `Branch` after it.

- [ ] **Step 1: Write the failing serialization test first**

Create `src/modules/Macro/MacroEngine.Tests/BranchSerializationTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.MacroCommon.Models;
using PowerToys.MacroCommon.Serialization;

namespace PowerToys.MacroEngine.Tests;

[TestClass]
public sealed class BranchSerializationTests
{
    [TestMethod]
    public void Serialize_BranchStep_JsonContainsBranchSnakeCase()
    {
        var def = new MacroDefinition
        {
            Steps =
            [
                new MacroStep
                {
                    Type = StepType.Branch,
                    Branches =
                    [
                        new MacroBranch
                        {
                            Condition = new MacroCondition
                            {
                                Type = ConditionType.AppIsFocused,
                                ProcessName = "notepad",
                            },
                            Steps = [new MacroStep { Type = StepType.PressKey, Key = "Ctrl+S" }],
                        },
                    ],
                },
            ],
        };

        var json = MacroSerializer.Serialize(def);

        Assert.IsTrue(json.Contains("\"branch\""), $"Expected 'branch' in JSON, got:\n{json}");
        Assert.IsTrue(json.Contains("\"app_is_focused\""), $"Expected 'app_is_focused' in JSON, got:\n{json}");
        Assert.IsTrue(json.Contains("\"notepad\""), $"Expected 'notepad' in JSON, got:\n{json}");
    }

    [TestMethod]
    public void Serialize_BranchStep_RoundTrips()
    {
        var def = new MacroDefinition
        {
            Steps =
            [
                new MacroStep
                {
                    Type = StepType.Branch,
                    Branches =
                    [
                        new MacroBranch
                        {
                            Condition = new MacroCondition
                            {
                                Type = ConditionType.AppIsFocused,
                                ProcessName = "notepad",
                            },
                            Steps = [new MacroStep { Type = StepType.PressKey, Key = "Ctrl+S" }],
                        },
                        new MacroBranch
                        {
                            Condition = null,
                            Steps = [new MacroStep { Type = StepType.TypeText, Text = "else" }],
                        },
                    ],
                },
            ],
        };

        var json = MacroSerializer.Serialize(def);
        var result = MacroSerializer.Deserialize(json);

        Assert.AreEqual(1, result.Steps.Count);
        var branch = result.Steps[0];
        Assert.AreEqual(StepType.Branch, branch.Type);
        Assert.IsNotNull(branch.Branches);
        Assert.AreEqual(2, branch.Branches!.Count);
        Assert.AreEqual(ConditionType.AppIsFocused, branch.Branches[0].Condition!.Type);
        Assert.AreEqual("notepad", branch.Branches[0].Condition!.ProcessName);
        Assert.AreEqual(1, branch.Branches[0].Steps.Count);
        Assert.AreEqual("Ctrl+S", branch.Branches[0].Steps[0].Key);
        Assert.IsNull(branch.Branches[1].Condition);
        Assert.AreEqual("else", branch.Branches[1].Steps[0].Text);
    }

    [TestMethod]
    public void Serialize_TimeAfterCondition_RoundTrips()
    {
        var def = new MacroDefinition
        {
            Steps =
            [
                new MacroStep
                {
                    Type = StepType.Branch,
                    Branches =
                    [
                        new MacroBranch
                        {
                            Condition = new MacroCondition
                            {
                                Type = ConditionType.TimeAfter,
                                Time = "15:00",
                            },
                            Steps = [],
                        },
                    ],
                },
            ],
        };

        var json = MacroSerializer.Serialize(def);
        Assert.IsTrue(json.Contains("\"time_after\""), $"Expected snake_case in JSON, got:\n{json}");
        Assert.IsTrue(json.Contains("\"15:00\""), $"Expected time value in JSON, got:\n{json}");

        var result = MacroSerializer.Deserialize(json);
        Assert.AreEqual(ConditionType.TimeAfter, result.Steps[0].Branches![0].Condition!.Type);
        Assert.AreEqual("15:00", result.Steps[0].Branches[0].Condition!.Time);
    }
}
```

- [ ] **Step 2: Build test project — expect compile error (StepType.Branch missing)**

```
msbuild src/modules/Macro/MacroEngine.Tests/MacroEngine.Tests.csproj /p:Configuration=Debug /p:Platform=x64 /t:Build /m /nologo /v:minimal
```

Expected: error `'StepType' does not contain a definition for 'Branch'`

- [ ] **Step 3: Add `Branch` to `StepType.cs`**

File: `src/modules/Macro/MacroCommon/Models/StepType.cs`

Full file after change:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace PowerToys.MacroCommon.Models;

public enum StepType
{
    [JsonStringEnumMemberName("press_key")]
    PressKey,

    [JsonStringEnumMemberName("type_text")]
    TypeText,

    [JsonStringEnumMemberName("wait")]
    Wait,

    [JsonStringEnumMemberName("repeat")]
    Repeat,

    [JsonStringEnumMemberName("branch")]
    Branch,
}
```

---

### Task 5: Add `Branches` to `MacroStep`

**Files:**
- Modify: `src/modules/Macro/MacroCommon/Models/MacroStep.cs`

- [ ] **Step 1: Add `Branches` property**

Full file after change:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.MacroCommon.Models;

public sealed record MacroStep
{
    public StepType Type { get; init; }

    public string? Key { get; init; }

    public string? Text { get; init; }

    public int? Ms { get; init; }

    public int? Count { get; init; }

    public List<MacroStep>? Steps { get; init; }

    public List<MacroBranch>? Branches { get; init; }
}
```

---

### Task 6: Register new types in `MacroJsonContext`

**Files:**
- Modify: `src/modules/Macro/MacroCommon/Serialization/MacroJsonContext.cs`

`MacroJsonContext` uses source generation — every type that appears in the serialization graph must be explicitly registered.

- [ ] **Step 1: Add three `[JsonSerializable]` attributes**

Full file after change:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using PowerToys.MacroCommon.Models;

namespace PowerToys.MacroCommon.Serialization;

// UseStringEnumConverter = true does NOT auto-apply snake_case to enum member names in
// source generation. Every StepType member must carry [JsonStringEnumMemberName("snake_value")]
// explicitly. See StepType.cs.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(MacroDefinition))]
[JsonSerializable(typeof(MacroHotkeySettings))]
[JsonSerializable(typeof(MacroCondition))]
[JsonSerializable(typeof(MacroBranch))]
[JsonSerializable(typeof(List<MacroBranch>))]
internal sealed partial class MacroJsonContext : JsonSerializerContext
{
}
```

- [ ] **Step 2: Build MacroCommon**

```
msbuild src/modules/Macro/MacroCommon/MacroCommon.csproj /p:Configuration=Debug /p:Platform=x64 /t:Build /m /nologo /v:minimal
```

Expected: `Build succeeded.`

- [ ] **Step 3: Build test project**

```
msbuild src/modules/Macro/MacroEngine.Tests/MacroEngine.Tests.csproj /p:Configuration=Debug /p:Platform=x64 /t:Build /m /nologo /v:minimal
```

Expected: `Build succeeded.` (no more compile errors)

- [ ] **Step 4: Run tests**

```
x64\Debug\tests\MacroEngine.Tests\PowerToys.MacroEngine.Tests.exe
```

Expected: All existing tests pass + 3 new `BranchSerializationTests` pass.

- [ ] **Step 5: Commit**

```bash
git add src/modules/Macro/MacroCommon/Models/ConditionType.cs
git add src/modules/Macro/MacroCommon/Models/MacroCondition.cs
git add src/modules/Macro/MacroCommon/Models/MacroBranch.cs
git add src/modules/Macro/MacroCommon/Models/StepType.cs
git add src/modules/Macro/MacroCommon/Models/MacroStep.cs
git add src/modules/Macro/MacroCommon/Serialization/MacroJsonContext.cs
git add src/modules/Macro/MacroEngine.Tests/BranchSerializationTests.cs
git commit -m "feat(macro-model): add Branch step type, MacroCondition, MacroBranch with JSON serialization"
```
