# Macro Flowchart — Plan 2: Engine Condition Evaluation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add condition evaluation and branch execution to `MacroEngine` so that a `Branch` step evaluates its conditions at runtime and executes the first matching path.

**Architecture:** New `IConditionEvaluator` interface + `ConditionEvaluator` impl using Win32 P/Invoke for app-focus checks and `System.Diagnostics.Process` for running-process checks. `MacroExecutor` receives the evaluator via constructor injection (optional, defaults to `ConditionEvaluator`). Tests use a `FakeConditionEvaluator` stub.

**Prerequisite:** Plan 1 (data model) must be complete — `StepType.Branch`, `MacroCondition`, `MacroBranch` must exist.

**Tech Stack:** C# 13, Win32 P/Invoke (`user32.dll`), `System.Diagnostics`, MSTest, Moq (available in test project).

---

## File Map

| Action | Path |
|--------|------|
| Create | `src/modules/Macro/MacroEngine/IConditionEvaluator.cs` |
| Create | `src/modules/Macro/MacroEngine/ConditionEvaluator.cs` |
| Modify | `src/modules/Macro/MacroEngine/MacroExecutor.cs` |
| Modify | `src/modules/Macro/MacroEngine/MacroEngineHost.cs` |
| Create | `src/modules/Macro/MacroEngine.Tests/FakeConditionEvaluator.cs` |
| Create | `src/modules/Macro/MacroEngine.Tests/BranchExecutorTests.cs` |

---

### Task 1: Define `IConditionEvaluator`

**Files:**
- Create: `src/modules/Macro/MacroEngine/IConditionEvaluator.cs`

- [ ] **Step 1: Create the interface**

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerToys.MacroCommon.Models;

namespace PowerToys.MacroEngine;

internal interface IConditionEvaluator
{
    bool Evaluate(MacroCondition condition);
}
```

- [ ] **Step 2: Build MacroEngine to verify no errors**

```
msbuild src/modules/Macro/MacroEngine/MacroEngine.csproj /p:Configuration=Debug /p:Platform=x64 /t:Build /m /nologo /v:minimal
```

Expected: `Build succeeded.`

---

### Task 2: Write failing `BranchExecutorTests` before implementing

**Files:**
- Create: `src/modules/Macro/MacroEngine.Tests/FakeConditionEvaluator.cs`
- Create: `src/modules/Macro/MacroEngine.Tests/BranchExecutorTests.cs`

- [ ] **Step 1: Create `FakeConditionEvaluator`**

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerToys.MacroCommon.Models;

namespace PowerToys.MacroEngine.Tests;

internal sealed class FakeConditionEvaluator : IConditionEvaluator
{
    private readonly Dictionary<string, bool> _results = [];

    public void Setup(ConditionType type, string? value, bool result)
    {
        _results[$"{type}:{value}"] = result;
    }

    public bool Evaluate(MacroCondition condition)
    {
        string key = $"{condition.Type}:{condition.ProcessName ?? condition.Time}";
        return _results.GetValueOrDefault(key, false);
    }
}
```

- [ ] **Step 2: Create `BranchExecutorTests`**

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.MacroCommon.Models;

namespace PowerToys.MacroEngine.Tests;

[TestClass]
public sealed class BranchExecutorTests
{
    private FakeSendInputHelper _input = null!;
    private FakeConditionEvaluator _evaluator = null!;
    private MacroExecutor _executor = null!;

    [TestInitialize]
    public void Init()
    {
        _input = new FakeSendInputHelper();
        _evaluator = new FakeConditionEvaluator();
        _executor = new MacroExecutor(_input, _evaluator);
    }

    [TestMethod]
    public async Task ExecuteBranch_FirstConditionTrue_ExecutesFirstBranch()
    {
        _evaluator.Setup(ConditionType.AppIsFocused, "notepad", true);

        var macro = new MacroDefinition
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
                            Condition = new MacroCondition { Type = ConditionType.AppIsFocused, ProcessName = "notepad" },
                            Steps = [new MacroStep { Type = StepType.TypeText, Text = "notepad branch" }],
                        },
                        new MacroBranch
                        {
                            Condition = null,
                            Steps = [new MacroStep { Type = StepType.TypeText, Text = "else branch" }],
                        },
                    ],
                },
            ],
        };

        await _executor.ExecuteAsync(macro, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "notepad branch" }, _input.Texts);
    }

    [TestMethod]
    public async Task ExecuteBranch_FirstConditionFalse_ElseBranchExecutes()
    {
        _evaluator.Setup(ConditionType.AppIsFocused, "notepad", false);

        var macro = new MacroDefinition
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
                            Condition = new MacroCondition { Type = ConditionType.AppIsFocused, ProcessName = "notepad" },
                            Steps = [new MacroStep { Type = StepType.TypeText, Text = "notepad branch" }],
                        },
                        new MacroBranch
                        {
                            Condition = null,
                            Steps = [new MacroStep { Type = StepType.TypeText, Text = "else branch" }],
                        },
                    ],
                },
            ],
        };

        await _executor.ExecuteAsync(macro, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "else branch" }, _input.Texts);
    }

    [TestMethod]
    public async Task ExecuteBranch_NoMatchAndNoElse_SkipsSilently()
    {
        _evaluator.Setup(ConditionType.AppIsFocused, "notepad", false);

        var macro = new MacroDefinition
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
                            Condition = new MacroCondition { Type = ConditionType.AppIsFocused, ProcessName = "notepad" },
                            Steps = [new MacroStep { Type = StepType.TypeText, Text = "notepad branch" }],
                        },
                    ],
                },
                new MacroStep { Type = StepType.TypeText, Text = "after branch" },
            ],
        };

        await _executor.ExecuteAsync(macro, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "after branch" }, _input.Texts);
    }

    [TestMethod]
    public async Task ExecuteBranch_SecondConditionMatches_FirstSkipped()
    {
        _evaluator.Setup(ConditionType.AppIsFocused, "notepad", false);
        _evaluator.Setup(ConditionType.AppIsRunning, "chrome", true);

        var macro = new MacroDefinition
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
                            Condition = new MacroCondition { Type = ConditionType.AppIsFocused, ProcessName = "notepad" },
                            Steps = [new MacroStep { Type = StepType.TypeText, Text = "notepad" }],
                        },
                        new MacroBranch
                        {
                            Condition = new MacroCondition { Type = ConditionType.AppIsRunning, ProcessName = "chrome" },
                            Steps = [new MacroStep { Type = StepType.TypeText, Text = "chrome" }],
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

        await _executor.ExecuteAsync(macro, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "chrome" }, _input.Texts);
    }

    [TestMethod]
    public async Task ExecuteBranch_MissingBranches_Throws()
    {
        var macro = new MacroDefinition
        {
            Steps = [new MacroStep { Type = StepType.Branch }],
        };

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _executor.ExecuteAsync(macro, CancellationToken.None));
    }

    [TestMethod]
    public async Task ExecuteBranch_BranchStepsExecuteInOrder()
    {
        _evaluator.Setup(ConditionType.AppIsRunning, "code", true);

        var macro = new MacroDefinition
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
                            Condition = new MacroCondition { Type = ConditionType.AppIsRunning, ProcessName = "code" },
                            Steps =
                            [
                                new MacroStep { Type = StepType.TypeText, Text = "first" },
                                new MacroStep { Type = StepType.TypeText, Text = "second" },
                            ],
                        },
                    ],
                },
            ],
        };

        await _executor.ExecuteAsync(macro, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "first", "second" }, _input.Texts);
    }
}
```

- [ ] **Step 3: Build test project — expect compile error (`MacroExecutor` constructor mismatch)**

```
msbuild src/modules/Macro/MacroEngine.Tests/MacroEngine.Tests.csproj /p:Configuration=Debug /p:Platform=x64 /t:Build /m /nologo /v:minimal
```

Expected: error — `MacroExecutor` has no constructor accepting `IConditionEvaluator`.

---

### Task 3: Implement `ConditionEvaluator`

**Files:**
- Create: `src/modules/Macro/MacroEngine/ConditionEvaluator.cs`

- [ ] **Step 1: Create the file**

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using ManagedCommon;
using PowerToys.MacroCommon.Models;

namespace PowerToys.MacroEngine;

internal sealed class ConditionEvaluator : IConditionEvaluator
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    public bool Evaluate(MacroCondition condition)
    {
        try
        {
            return condition.Type switch
            {
                ConditionType.AppIsFocused => EvaluateAppIsFocused(condition.ProcessName ?? string.Empty),
                ConditionType.AppIsRunning => EvaluateAppIsRunning(condition.ProcessName ?? string.Empty),
                ConditionType.TimeAfter => EvaluateTimeAfter(condition.Time ?? "00:00"),
                ConditionType.TimeBefore => EvaluateTimeBefore(condition.Time ?? "23:59"),
                _ => false,
            };
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"MacroEngine: condition evaluation failed: {ex.Message}");
            return false;
        }
    }

    private static bool EvaluateAppIsFocused(string processName)
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        GetWindowThreadProcessId(hwnd, out uint pid);
        try
        {
            using Process proc = Process.GetProcessById((int)pid);
            return string.Equals(proc.ProcessName, processName, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool EvaluateAppIsRunning(string processName) =>
        Process.GetProcessesByName(processName).Length > 0;

    private static bool EvaluateTimeAfter(string time) =>
        TimeSpan.TryParse(time, out TimeSpan t) && DateTime.Now.TimeOfDay > t;

    private static bool EvaluateTimeBefore(string time) =>
        TimeSpan.TryParse(time, out TimeSpan t) && DateTime.Now.TimeOfDay < t;
}
```

---

### Task 4: Update `MacroExecutor` to accept `IConditionEvaluator` and handle `Branch`

**Files:**
- Modify: `src/modules/Macro/MacroEngine/MacroExecutor.cs`

- [ ] **Step 1: Write the updated file**

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerToys.MacroCommon.Models;

namespace PowerToys.MacroEngine;

internal sealed class MacroExecutor
{
    private readonly ISendInputHelper _input;
    private readonly IConditionEvaluator _evaluator;

    internal MacroExecutor(ISendInputHelper input, IConditionEvaluator? evaluator = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        _input = input;
        _evaluator = evaluator ?? new ConditionEvaluator();
    }

    public async Task ExecuteAsync(MacroDefinition macro, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(macro);
        foreach (var step in macro.Steps)
        {
            ct.ThrowIfCancellationRequested();
            await ExecuteStepAsync(step, ct);
        }
    }

    private Task ExecuteStepAsync(MacroStep step, CancellationToken ct) =>
        step.Type switch
        {
            StepType.PressKey => ExecutePressKey(step),
            StepType.TypeText => ExecuteTypeText(step),
            StepType.Wait => ExecuteWait(step, ct),
            StepType.Repeat => ExecuteRepeat(step, ct),
            StepType.Branch => ExecuteBranch(step, ct),
            _ => throw new InvalidOperationException($"Unknown step type: {step.Type}"),
        };

    private Task ExecutePressKey(MacroStep step)
    {
        _input.PressKeyCombo(step.Key
            ?? throw new InvalidOperationException("PressKey step missing Key."));
        return Task.CompletedTask;
    }

    private Task ExecuteTypeText(MacroStep step)
    {
        _input.TypeText(step.Text
            ?? throw new InvalidOperationException("TypeText step missing Text."));
        return Task.CompletedTask;
    }

    private static Task ExecuteWait(MacroStep step, CancellationToken ct) =>
        Task.Delay(step.Ms ?? throw new InvalidOperationException("Wait step missing Ms."), ct);

    private async Task ExecuteRepeat(MacroStep step, CancellationToken ct)
    {
        int count = step.Count
            ?? throw new InvalidOperationException("Repeat step missing Count.");
        var subSteps = step.Steps
            ?? throw new InvalidOperationException("Repeat step missing Steps.");
        for (int i = 0; i < count; i++)
        {
            ct.ThrowIfCancellationRequested();
            foreach (var sub in subSteps)
            {
                ct.ThrowIfCancellationRequested();
                await ExecuteStepAsync(sub, ct);
            }
        }
    }

    private async Task ExecuteBranch(MacroStep step, CancellationToken ct)
    {
        var branches = step.Branches
            ?? throw new InvalidOperationException("Branch step missing Branches.");
        foreach (var branch in branches)
        {
            ct.ThrowIfCancellationRequested();
            if (branch.Condition is null || _evaluator.Evaluate(branch.Condition))
            {
                foreach (var sub in branch.Steps)
                {
                    ct.ThrowIfCancellationRequested();
                    await ExecuteStepAsync(sub, ct);
                }

                return;
            }
        }
    }
}
```

---

### Task 5: Update `MacroEngineHost` to pass `ConditionEvaluator`

**Files:**
- Modify: `src/modules/Macro/MacroEngine/MacroEngineHost.cs`

The `MacroEngineHost` internal constructor creates `MacroExecutor`. Pass a `ConditionEvaluator` explicitly so the seam is visible — production always uses the real evaluator.

- [ ] **Step 1: Update the constructor**

In `MacroEngineHost.cs`, find the internal constructor:

```csharp
internal MacroEngineHost(ISendInputHelper input, IAppScopeChecker scopeChecker)
{
    _executor = new MacroExecutor(input);
```

Change to:

```csharp
internal MacroEngineHost(ISendInputHelper input, IAppScopeChecker scopeChecker)
{
    _executor = new MacroExecutor(input, new ConditionEvaluator());
```

- [ ] **Step 2: Build `MacroEngine`**

```
msbuild src/modules/Macro/MacroEngine/MacroEngine.csproj /p:Configuration=Debug /p:Platform=x64 /t:Build /m /nologo /v:minimal
```

Expected: `Build succeeded.`

- [ ] **Step 3: Build test project**

```
msbuild src/modules/Macro/MacroEngine.Tests/MacroEngine.Tests.csproj /p:Configuration=Debug /p:Platform=x64 /t:Build /m /nologo /v:minimal
```

Expected: `Build succeeded.`

- [ ] **Step 4: Run all engine tests**

```
x64\Debug\tests\MacroEngine.Tests\PowerToys.MacroEngine.Tests.exe
```

Expected: All existing tests pass + 6 new `BranchExecutorTests` pass. Zero failures.

- [ ] **Step 5: Commit**

```bash
git add src/modules/Macro/MacroEngine/IConditionEvaluator.cs
git add src/modules/Macro/MacroEngine/ConditionEvaluator.cs
git add src/modules/Macro/MacroEngine/MacroExecutor.cs
git add src/modules/Macro/MacroEngine/MacroEngineHost.cs
git add src/modules/Macro/MacroEngine.Tests/FakeConditionEvaluator.cs
git add src/modules/Macro/MacroEngine.Tests/BranchExecutorTests.cs
git commit -m "feat(macro-engine): add IConditionEvaluator, ConditionEvaluator, Branch execution in MacroExecutor"
```
