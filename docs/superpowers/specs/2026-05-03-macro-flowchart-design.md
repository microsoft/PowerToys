# Macro Flowchart Editor Design

**Goal:** Replace the linear step list in `MacroEditDialog` with a flowchart editor that supports conditional branching, where conditions are evaluated automatically at hotkey trigger time.

**Architecture:** Three sequential layers — data model (new Branch step type + Condition types), engine (condition evaluation + branch execution), and UI (recursive `MacroFlowChart` UserControl with lane-based branch rendering).

**Tech Stack:** C# 13 / WinUI 3, System.Text.Json source-gen, Win32 P/Invoke for app-focus checks.

---

## 1. Data Model

### New types in `MacroCommon`

```csharp
// MacroCondition.cs
public enum ConditionType { AppIsFocused, AppIsRunning, TimeAfter, TimeBefore }

public record MacroCondition(
    ConditionType Type,
    string? ProcessName,   // AppIsFocused, AppIsRunning
    string? Time           // TimeAfter, TimeBefore — "HH:mm" format
);

// MacroBranch.cs
public record MacroBranch(
    MacroCondition? Condition,   // null = else/default
    List<MacroStep> Steps
);
```

### Changes to existing types

**`StepType` enum** — add `Branch`.

**`MacroStep` record** — add nullable field:
```csharp
public List<MacroBranch>? Branches { get; init; }
```

**`MacroJsonContext`** — add `[JsonSerializable]` for `MacroCondition`, `MacroBranch`, `List<MacroBranch>`.

### Execution semantics

A `Branch` step iterates its `Branches` in order. The first branch whose `Condition` evaluates to `true` executes. A branch with `Condition = null` acts as else and always matches. If no branch matches and there is no else branch, the step is silently skipped.

---

## 2. Engine — Condition Evaluation

### New files in `MacroEngine`

**`IConditionEvaluator.cs`**
```csharp
internal interface IConditionEvaluator
{
    bool Evaluate(MacroCondition condition);
}
```

**`ConditionEvaluator.cs`** — implements `IConditionEvaluator`:

| Condition | Implementation |
|-----------|---------------|
| `AppIsFocused(processName)` | `GetForegroundWindow()` → `GetWindowThreadProcessId()` → `Process.GetProcessById(pid).ProcessName` |
| `AppIsRunning(processName)` | `Process.GetProcessesByName(processName).Length > 0` |
| `TimeAfter(HH:mm)` | `DateTime.Now.TimeOfDay > TimeSpan.Parse(time)` |
| `TimeBefore(HH:mm)` | `DateTime.Now.TimeOfDay < TimeSpan.Parse(time)` |

Failures (process access denied, invalid time format) return `false` and log a warning.

### Changes to `MacroExecutor`

Constructor receives `IConditionEvaluator` (injected). New `Branch` case in `ExecuteStepAsync`:

```csharp
StepType.Branch => ExecuteBranch(step, ct),
```

```csharp
private async Task ExecuteBranch(MacroStep step, CancellationToken ct)
{
    var branches = step.Branches ?? throw new InvalidOperationException("Branch step missing Branches.");
    foreach (var branch in branches)
    {
        if (branch.Condition is null || _evaluator.Evaluate(branch.Condition))
        {
            foreach (var sub in branch.Steps)
                await ExecuteStepAsync(sub, ct);
            return;
        }
    }
}
```

### Changes to `MacroEngineHost`

Pass `new ConditionEvaluator()` into `MacroExecutor` constructor.

### Test seam

`IConditionEvaluator` is injected — unit tests pass a stub that returns predetermined values per condition.

---

## 3. UI — Flowchart Editor

### New files in `Settings.UI`

**`MacroFlowChart.xaml` / `MacroFlowChart.xaml.cs`** — `UserControl`, recursive.

Renders a vertical list of step nodes. Each node type has a `DataTemplate`:

| Step type | Rendering |
|-----------|-----------|
| PressKey / TypeText / Wait / Repeat | Compact card (same as current list item) |
| Branch | Horizontal `Grid` of N lane columns — see below |

Between adjacent nodes: a thin vertical connector line (4px wide, `SystemFillColorAttentionBrush`).

**Branch node layout:**

```
┌─────────────────────────────────────────────────┐
│  Branch  [+ Add lane]                           │
├──────────────┬──────────────┬───────────────────┤
│ App focused: │ Time after:  │ else              │
│ notepad.exe  │ 15:00        │                   │
├──────────────┼──────────────┼───────────────────┤
│ [steps...]   │ [steps...]   │ [steps...]        │
│ + Add step   │ + Add step   │ + Add step        │
└──────────────┴──────────────┴───────────────────┘
        │               │               │
        └───────────────┴───────────────┘
                        │
                   (flow continues)
```

Lane columns use equal `*` `ColumnDefinition` widths. Minimum 2 lanes (if + else). Max lanes: unlimited, scroll horizontally if > 4.

Each lane contains:
- **Condition header**: `ComboBox` (condition type) + `TextBox` (value). Else lane shows "else" label, no inputs.
- **`MacroFlowChart` instance** bound to `MacroBranchViewModel.Steps` (recursive).
- **"+ Add step" `MenuFlyout` button** (same step types as current Add Step button).

**`[+ Add lane]` button** appends a new `MacroBranchViewModel` with a blank condition. If the last existing lane is an else lane, the new lane is inserted before it.

### New ViewModels

**`MacroConditionViewModel`**
- `ConditionType Type` (bindable, drives ComboBox)
- `string Value` (process name or time string)
- `bool IsElse` — true when `Type` is unset / null condition
- `MacroCondition ToCondition()` — returns null if `IsElse`

**`MacroBranchViewModel`**
- `MacroConditionViewModel Condition`
- `ObservableCollection<MacroStepViewModel> Steps`

**`MacroStepViewModel`** — add:
- `ObservableCollection<MacroBranchViewModel>? Branches`
- Static factory `FromDefinition(MacroStep)` populates branches recursively

### Changes to `MacroEditDialog`

Replace the `ListView` + step `DataTemplate` block with:

```xml
<controls:MacroFlowChart Steps="{x:Bind ViewModel.Steps}" />
```

`MacroEditViewModel.Steps` type remains `ObservableCollection<MacroStepViewModel>` — no change to the public API.

### Changes to `MacroEditViewModel`

`ToDefinition()` serializes `MacroStepViewModel.Branches` into `MacroStep.Branches` recursively (mirrors existing `Steps[]` serialization for `Repeat`).

---

## 4. Scope Decomposition — Implementation Plans

Three sequential plans, each independently buildable and testable:

1. **Model** — `StepType.Branch`, `MacroCondition`, `MacroBranch`, serialization, model unit tests.
2. **Engine** — `IConditionEvaluator`, `ConditionEvaluator`, `MacroExecutor` branch execution, engine unit tests with stub evaluator.
3. **UI** — `MacroConditionViewModel`, `MacroBranchViewModel`, `MacroFlowChart` UserControl, `MacroEditDialog` swap, manual smoke test.

---

## 5. Out of Scope

- Nested branch nodes inside branch lanes (no branch-within-branch for v1)
- Condition combining (`AND`/`OR` logic)
- Clipboard content conditions
- Drag-and-drop reordering of lanes
- Undo/redo
