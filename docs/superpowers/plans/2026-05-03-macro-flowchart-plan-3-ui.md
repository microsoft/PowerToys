# Macro Flowchart — Plan 3: UI

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the linear `ListView` in `MacroEditDialog` with a recursive `MacroFlowChart` UserControl that renders regular steps as cards and branch steps as side-by-side lane columns.

**Architecture:** New `MacroConditionViewModel` and `MacroBranchViewModel` hold condition and lane state. `MacroStepViewModel` gains `IsBranch` and `Branches`. `MacroFlowChart` is a UserControl with a `DataTemplateSelector` that picks between a compact card template (regular steps) and a lane-grid template (branch steps). Lane columns each embed a nested `MacroFlowChart` — this is the recursion. `MacroEditDialog` replaces its entire `ListView` + `DataTemplate` block with `<controls:MacroFlowChart Steps="{x:Bind ViewModel.Steps}" />`.

**Prerequisite:** Plan 1 (data model) complete. `StepType.Branch`, `MacroCondition`, `MacroBranch` must exist.

**Tech Stack:** WinUI 3, C# 13, x:Bind, `DataTemplateSelector`, MSTest.

---

## File Map

| Action | Path |
|--------|------|
| Create | `src/settings-ui/Settings.UI/ViewModels/MacroConditionViewModel.cs` |
| Create | `src/settings-ui/Settings.UI/ViewModels/MacroBranchViewModel.cs` |
| Modify | `src/settings-ui/Settings.UI/ViewModels/MacroStepViewModel.cs` |
| Modify | `src/settings-ui/Settings.UI/ViewModels/MacroEditViewModel.cs` |
| Create | `src/settings-ui/Settings.UI/SettingsXAML/Controls/MacroFlowChart/MacroFlowChart.xaml` |
| Create | `src/settings-ui/Settings.UI/SettingsXAML/Controls/MacroFlowChart/MacroFlowChart.xaml.cs` |
| Modify | `src/settings-ui/Settings.UI/SettingsXAML/Views/MacroEditDialog.xaml` |
| Modify | `src/settings-ui/Settings.UI/SettingsXAML/Views/MacroEditDialog.xaml.cs` |
| Modify | `src/settings-ui/Settings.UI/Strings/en-us/Resources.resw` |
| Modify | `src/settings-ui/Settings.UI.UnitTests/ViewModelTests/MacroStepViewModelTests.cs` |

---

### Task 1: Add `MacroConditionViewModel`

**Files:**
- Create: `src/settings-ui/Settings.UI/ViewModels/MacroConditionViewModel.cs`

- [ ] **Step 1: Write the file**

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using PowerToys.MacroCommon.Models;

namespace Microsoft.PowerToys.Settings.UI.ViewModels;

public sealed class MacroConditionViewModel : Observable
{
    private ConditionType _type;
    private string _value = string.Empty;
    private bool _isElse;

    public ConditionType Type
    {
        get => _type;
        set => Set(ref _type, value);
    }

    public string Value
    {
        get => _value;
        set => Set(ref _value, value);
    }

    public bool IsElse
    {
        get => _isElse;
        set => Set(ref _isElse, value);
    }

    public MacroCondition? ToModel()
    {
        if (_isElse)
        {
            return null;
        }

        return new MacroCondition
        {
            Type = _type,
            ProcessName = _type is ConditionType.AppIsFocused or ConditionType.AppIsRunning ? _value : null,
            Time = _type is ConditionType.TimeAfter or ConditionType.TimeBefore ? _value : null,
        };
    }

    public static MacroConditionViewModel FromModel(MacroCondition? condition)
    {
        if (condition is null)
        {
            return new MacroConditionViewModel { IsElse = true };
        }

        return new MacroConditionViewModel
        {
            Type = condition.Type,
            Value = condition.ProcessName ?? condition.Time ?? string.Empty,
        };
    }
}
```

---

### Task 2: Add `MacroBranchViewModel`

**Files:**
- Create: `src/settings-ui/Settings.UI/ViewModels/MacroBranchViewModel.cs`

- [ ] **Step 1: Write the file**

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.ObjectModel;
using System.Linq;
using PowerToys.MacroCommon.Models;

namespace Microsoft.PowerToys.Settings.UI.ViewModels;

public sealed class MacroBranchViewModel
{
    public MacroConditionViewModel Condition { get; } = new();

    public ObservableCollection<MacroStepViewModel> Steps { get; } = [];

    public MacroBranch ToModel() => new()
    {
        Condition = Condition.ToModel(),
        Steps = Steps.Count > 0 ? [.. Steps.Select(s => s.ToModel())] : [],
    };

    public static MacroBranchViewModel FromModel(MacroBranch branch)
    {
        var vm = new MacroBranchViewModel();
        if (branch.Condition is null)
        {
            vm.Condition.IsElse = true;
        }
        else
        {
            vm.Condition.Type = branch.Condition.Type;
            vm.Condition.Value = branch.Condition.ProcessName ?? branch.Condition.Time ?? string.Empty;
        }

        foreach (MacroStep step in branch.Steps)
        {
            vm.Steps.Add(MacroStepViewModel.FromModel(step));
        }

        return vm;
    }
}
```

---

### Task 3: Extend `MacroStepViewModel` for Branch

**Files:**
- Modify: `src/settings-ui/Settings.UI/ViewModels/MacroStepViewModel.cs`

- [ ] **Step 1: Write the failing tests first**

In `src/settings-ui/Settings.UI.UnitTests/ViewModelTests/MacroStepViewModelTests.cs`, add these test methods to the existing `MacroStepViewModelTests` class (append before the closing `}`):

```csharp
[TestMethod]
public void IsBranch_OnlyTrueForBranch()
{
    Assert.IsTrue(new MacroStepViewModel { Type = StepType.Branch }.IsBranch);
    Assert.IsFalse(new MacroStepViewModel { Type = StepType.PressKey }.IsBranch);
}

[TestMethod]
public void FromModel_Branch_RoundTrip()
{
    var step = new MacroStep
    {
        Type = StepType.Branch,
        Branches =
        [
            new MacroBranch
            {
                Condition = new MacroCondition { Type = ConditionType.AppIsFocused, ProcessName = "notepad" },
                Steps = [new MacroStep { Type = StepType.TypeText, Text = "hello" }],
            },
            new MacroBranch
            {
                Condition = null,
                Steps = [new MacroStep { Type = StepType.PressKey, Key = "Esc" }],
            },
        ],
    };

    var vm = MacroStepViewModel.FromModel(step);

    Assert.AreEqual(StepType.Branch, vm.Type);
    Assert.IsTrue(vm.IsBranch);
    Assert.AreEqual(2, vm.Branches.Count);
    Assert.IsFalse(vm.Branches[0].Condition.IsElse);
    Assert.AreEqual(ConditionType.AppIsFocused, vm.Branches[0].Condition.Type);
    Assert.AreEqual("notepad", vm.Branches[0].Condition.Value);
    Assert.AreEqual(1, vm.Branches[0].Steps.Count);
    Assert.IsTrue(vm.Branches[1].Condition.IsElse);

    var result = vm.ToModel();
    Assert.AreEqual(StepType.Branch, result.Type);
    Assert.IsNotNull(result.Branches);
    Assert.AreEqual(2, result.Branches!.Count);
    Assert.AreEqual(ConditionType.AppIsFocused, result.Branches[0].Condition!.Type);
    Assert.AreEqual("notepad", result.Branches[0].Condition!.ProcessName);
    Assert.AreEqual("hello", result.Branches[0].Steps[0].Text);
    Assert.IsNull(result.Branches[1].Condition);
    Assert.AreEqual("Esc", result.Branches[1].Steps[0].Key);
}

[TestMethod]
public void ToModel_BranchWithNoBranches_ReturnsNullBranches()
{
    var vm = new MacroStepViewModel { Type = StepType.Branch };
    var model = vm.ToModel();
    Assert.IsNull(model.Branches);
}
```

- [ ] **Step 2: Build tests — expect compile errors (`IsBranch`, `Branches` missing)**

```
msbuild "src/settings-ui/Settings.UI.UnitTests/Settings.UI.UnitTests.csproj" /p:Configuration=Debug /p:Platform=x64 /t:Build /m /nologo /v:minimal
```

Expected: errors about `IsBranch` and `Branches` not found on `MacroStepViewModel`.

- [ ] **Step 3: Update `MacroStepViewModel`**

Full updated file:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using PowerToys.MacroCommon.Models;

namespace Microsoft.PowerToys.Settings.UI.ViewModels;

public sealed class MacroStepViewModel : Observable
{
    private StepType _type;
    private string? _key;
    private string? _text;
    private int? _ms;
    private int? _count;

    public StepType Type
    {
        get => _type;
        set
        {
            if (Set(ref _type, value))
            {
                OnPropertyChanged(nameof(TypeLabel));
                OnPropertyChanged(nameof(IsPressKey));
                OnPropertyChanged(nameof(IsTypeText));
                OnPropertyChanged(nameof(IsWait));
                OnPropertyChanged(nameof(IsRepeat));
                OnPropertyChanged(nameof(IsBranch));
            }
        }
    }

    public string? Key
    {
        get => _key;
        set => Set(ref _key, value);
    }

    public string? Text
    {
        get => _text;
        set => Set(ref _text, value);
    }

    public int? Ms
    {
        get => _ms;
        set => Set(ref _ms, value);
    }

    public int? Count
    {
        get => _count;
        set => Set(ref _count, value);
    }

    public ObservableCollection<MacroStepViewModel> SubSteps { get; } = [];

    public ObservableCollection<MacroBranchViewModel> Branches { get; } = [];

    public double MsDouble
    {
        get => _ms ?? 0;
        set
        {
            Ms = Math.Max(0, (int)value);
            OnPropertyChanged();
        }
    }

    public double CountDouble
    {
        get => Math.Max(1, _count ?? 1);
        set
        {
            Count = Math.Max(1, (int)value);
            OnPropertyChanged();
        }
    }

    public string TypeLabel => Type switch
    {
        StepType.PressKey => "Key",
        StepType.TypeText => "Text",
        StepType.Wait => "Wait (ms)",
        StepType.Repeat => "Repeat",
        StepType.Branch => "Branch",
        _ => Type.ToString(),
    };

    public bool IsPressKey => Type == StepType.PressKey;

    public bool IsTypeText => Type == StepType.TypeText;

    public bool IsWait => Type == StepType.Wait;

    public bool IsRepeat => Type == StepType.Repeat;

    public bool IsBranch => Type == StepType.Branch;

    public static MacroStepViewModel FromModel(MacroStep step)
    {
        MacroStepViewModel vm = new()
        {
            Type = step.Type,
            Key = step.Key,
            Text = step.Text,
            Ms = step.Ms,
            Count = step.Count,
        };

        if (step.Steps != null)
        {
            foreach (MacroStep sub in step.Steps)
            {
                vm.SubSteps.Add(FromModel(sub));
            }
        }

        if (step.Branches != null)
        {
            foreach (MacroBranch branch in step.Branches)
            {
                vm.Branches.Add(MacroBranchViewModel.FromModel(branch));
            }
        }

        return vm;
    }

    public MacroStep ToModel() => new()
    {
        Type = Type,
        Key = Key,
        Text = Text,
        Ms = Ms,
        Count = Count,
        Steps = SubSteps.Count > 0 ? [.. SubSteps.Select(s => s.ToModel())] : null,
        Branches = Branches.Count > 0 ? [.. Branches.Select(b => b.ToModel())] : null,
    };
}
```

- [ ] **Step 4: Build and run tests**

```
msbuild "src/settings-ui/Settings.UI.UnitTests/Settings.UI.UnitTests.csproj" /p:Configuration=Debug /p:Platform=x64 /t:Build /m /nologo /v:minimal
```

Run the test executable (path uses `$(Configuration)\$(Platform)\tests\SettingsTests\`):

```
Debug\x64\tests\SettingsTests\PowerToys.Settings.UnitTests.exe
```

Expected: all existing tests pass + 3 new branch tests pass.

---

### Task 4: Update `MacroEditViewModel` for branch operations

**Files:**
- Modify: `src/settings-ui/Settings.UI/ViewModels/MacroEditViewModel.cs`

Add `AddBranch`, `DeleteBranch`, `AddBranchStep`, `DeleteBranchStep` methods. These are called by `MacroFlowChart` code-behind.

- [ ] **Step 1: Write the updated file**

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using PowerToys.MacroCommon.Models;

namespace Microsoft.PowerToys.Settings.UI.ViewModels;

public sealed class MacroEditViewModel : Observable
{
    private string _name;
    private HotkeySettings? _hotkey;
    private string? _appScope;
    private bool _isEnabled;

    public MacroEditViewModel()
        : this(new MacroDefinition())
    {
    }

    public MacroEditViewModel(MacroDefinition definition)
    {
        OriginalId = definition.Id;
        _name = definition.Name;
        _hotkey = MacroHotkeyConverter.ToHotkeySettings(definition.Hotkey);
        _appScope = definition.AppScope;
        _isEnabled = definition.IsEnabled;
        Steps = new ObservableCollection<MacroStepViewModel>(
            definition.Steps.Select(MacroStepViewModel.FromModel));
    }

    public string OriginalId { get; }

    public string Name
    {
        get => _name;
        set
        {
            if (Set(ref _name, value))
            {
                OnPropertyChanged(nameof(HasValidationError));
                OnPropertyChanged(nameof(IsValid));
            }
        }
    }

    public HotkeySettings? Hotkey
    {
        get => _hotkey;
        set => Set(ref _hotkey, value);
    }

    public string? AppScope
    {
        get => _appScope;
        set => Set(ref _appScope, value);
    }

    public ObservableCollection<MacroStepViewModel> Steps { get; }

    public bool HasValidationError => string.IsNullOrWhiteSpace(_name);

    public bool IsValid => !HasValidationError;

    public MacroDefinition ToDefinition() => new()
    {
        Id = OriginalId,
        Name = Name.Trim(),
        Hotkey = MacroHotkeyConverter.ToMacroHotkeySettings(Hotkey),
        AppScope = string.IsNullOrWhiteSpace(AppScope) ? null : AppScope!.Trim(),
        IsEnabled = _isEnabled,
        Steps = [.. Steps.Select(s => s.ToModel())],
    };

    public void AddStep(StepType type) =>
        Steps.Add(new MacroStepViewModel { Type = type });

    public void DeleteStep(MacroStepViewModel step) =>
        Steps.Remove(step);

    public void AddSubStep(MacroStepViewModel parent, StepType type) =>
        parent.SubSteps.Add(new MacroStepViewModel { Type = type });

    public void DeleteSubStep(MacroStepViewModel parent, MacroStepViewModel child) =>
        parent.SubSteps.Remove(child);

    public void AddBranch(MacroStepViewModel branchStep)
    {
        var vm = new MacroBranchViewModel();
        vm.Condition.IsElse = false;
        branchStep.Branches.Add(vm);
    }

    public void DeleteBranch(MacroStepViewModel branchStep, MacroBranchViewModel branch) =>
        branchStep.Branches.Remove(branch);

    public void AddBranchStep(MacroBranchViewModel branch, StepType type) =>
        branch.Steps.Add(new MacroStepViewModel { Type = type });

    public void DeleteBranchStep(MacroBranchViewModel branch, MacroStepViewModel step) =>
        branch.Steps.Remove(step);
}
```

---

### Task 5: Add resource strings for `MacroFlowChart`

**Files:**
- Modify: `src/settings-ui/Settings.UI/Strings/en-us/Resources.resw`

- [ ] **Step 1: Add strings before the closing `</root>` tag**

Append these `<data>` entries. Find the last `</data>` before `</root>` and insert after it:

```xml
  <data name="MacroFlowChart_AddBranch.Content" xml:space="preserve">
    <value>+ Add branch</value>
    <comment>Button label to add a new condition branch lane</comment>
  </data>
  <data name="MacroFlowChart_DeleteBranch.ToolTipService.ToolTip" xml:space="preserve">
    <value>Delete branch</value>
    <comment>Tooltip for the delete button on a branch lane</comment>
  </data>
  <data name="MacroFlowChart_ElseLabel.Text" xml:space="preserve">
    <value>else</value>
    <comment>Label shown in the condition header when a branch lane has no condition (default/else path)</comment>
  </data>
  <data name="MacroFlowChart_AddStep.Content" xml:space="preserve">
    <value>+ Add step</value>
    <comment>Button label inside a branch lane to add a step</comment>
  </data>
  <data name="MacroFlowChart_ConditionType_AppIsFocused.Content" xml:space="preserve">
    <value>App is focused</value>
    <comment>ComboBox option: condition type where the named app must be in the foreground</comment>
  </data>
  <data name="MacroFlowChart_ConditionType_AppIsRunning.Content" xml:space="preserve">
    <value>App is running</value>
    <comment>ComboBox option: condition type where the named app must be running (any window)</comment>
  </data>
  <data name="MacroFlowChart_ConditionType_TimeAfter.Content" xml:space="preserve">
    <value>Time after</value>
    <comment>ComboBox option: condition type where current time must be after the specified time</comment>
  </data>
  <data name="MacroFlowChart_ConditionType_TimeBefore.Content" xml:space="preserve">
    <value>Time before</value>
    <comment>ComboBox option: condition type where current time must be before the specified time</comment>
  </data>
  <data name="MacroFlowChart_ConditionValue.PlaceholderText" xml:space="preserve">
    <value>e.g. notepad.exe or 15:00</value>
    <comment>Placeholder text for the condition value input (process name or HH:mm time)</comment>
  </data>
```

---

### Task 6: Create `MacroFlowChart` UserControl

**Files:**
- Create: `src/settings-ui/Settings.UI/SettingsXAML/Controls/MacroFlowChart/MacroFlowChart.xaml`
- Create: `src/settings-ui/Settings.UI/SettingsXAML/Controls/MacroFlowChart/MacroFlowChart.xaml.cs`

- [ ] **Step 1: Create `MacroFlowChart.xaml.cs`**

The code-behind defines the `Steps` dependency property, the `DataTemplateSelector`, and the click handlers for add/delete actions.

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PowerToys.MacroCommon.Models;

namespace Microsoft.PowerToys.Settings.UI.Controls;

public sealed partial class MacroFlowChart : UserControl
{
    public static readonly DependencyProperty StepsProperty =
        DependencyProperty.Register(
            nameof(Steps),
            typeof(System.Collections.ObjectModel.ObservableCollection<MacroStepViewModel>),
            typeof(MacroFlowChart),
            new PropertyMetadata(null));

    public static readonly DependencyProperty EditViewModelProperty =
        DependencyProperty.Register(
            nameof(EditViewModel),
            typeof(MacroEditViewModel),
            typeof(MacroFlowChart),
            new PropertyMetadata(null));

    public System.Collections.ObjectModel.ObservableCollection<MacroStepViewModel>? Steps
    {
        get => (System.Collections.ObjectModel.ObservableCollection<MacroStepViewModel>?)GetValue(StepsProperty);
        set => SetValue(StepsProperty, value);
    }

    public MacroEditViewModel? EditViewModel
    {
        get => (MacroEditViewModel?)GetValue(EditViewModelProperty);
        set => SetValue(EditViewModelProperty, value);
    }

    public MacroFlowChart()
    {
        InitializeComponent();
    }

    // Called when the "Add step" MenuFlyoutItem is clicked inside a regular step list
    private void AddStep_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item &&
            item.Tag is string tag &&
            Enum.TryParse<StepType>(tag, out StepType type))
        {
            EditViewModel?.AddStep(type);
        }
    }

    // Called when the delete button on a top-level step is clicked
    private void DeleteStep_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is MacroStepViewModel step)
        {
            EditViewModel?.DeleteStep(step);
        }
    }

    // Called when "Add branch" is clicked on a Branch step header
    private void AddBranch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is MacroStepViewModel branchStep)
        {
            EditViewModel?.AddBranch(branchStep);
        }
    }

    // Called when the delete button on a branch lane is clicked
    private void DeleteBranch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn &&
            btn.Tag is (MacroStepViewModel branchStep, MacroBranchViewModel branch))
        {
            EditViewModel?.DeleteBranch(branchStep, branch);
        }
    }

    // Called when "Add step" inside a branch lane is clicked
    private void AddBranchStep_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item &&
            item.Tag is string tag &&
            Enum.TryParse<StepType>(tag, out StepType type) &&
            item.Parent is MenuFlyout flyout &&
            flyout.Target is Button btn &&
            btn.Tag is MacroBranchViewModel branch)
        {
            EditViewModel?.AddBranchStep(branch, type);
        }
    }

    // Called when delete step inside a branch lane is clicked
    private void DeleteBranchStep_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn &&
            btn.Tag is (MacroBranchViewModel branch, MacroStepViewModel step))
        {
            EditViewModel?.DeleteBranchStep(branch, step);
        }
    }
}
```

- [ ] **Step 2: Create `MacroFlowChart.xaml`**

```xml
<!-- Copyright (c) Microsoft Corporation -->
<!-- The Microsoft Corporation licenses this file to you under the MIT license. -->
<!-- See the LICENSE file in the project root for more information. -->

<UserControl
    x:Class="Microsoft.PowerToys.Settings.UI.Controls.MacroFlowChart"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:controls="using:Microsoft.PowerToys.Settings.UI.Controls"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:tkconverters="using:CommunityToolkit.WinUI.Converters"
    xmlns:viewmodels="using:Microsoft.PowerToys.Settings.UI.ViewModels"
    mc:Ignorable="d">

    <UserControl.Resources>
        <tkconverters:BoolToVisibilityConverter x:Key="BoolToVisibilityConverter" />

        <!-- Regular step card template (PressKey / TypeText / Wait / Repeat) -->
        <DataTemplate x:Key="RegularStepTemplate" x:DataType="viewmodels:MacroStepViewModel">
            <Border
                Margin="0,0,0,4"
                Padding="8,6"
                BorderBrush="{ThemeResource DividerStrokeColorDefaultBrush}"
                BorderThickness="1"
                CornerRadius="4">
                <Grid ColumnSpacing="8">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="80" />
                        <ColumnDefinition Width="*" />
                        <ColumnDefinition Width="32" />
                    </Grid.ColumnDefinitions>
                    <TextBlock
                        Grid.Column="0"
                        VerticalAlignment="Center"
                        Text="{x:Bind TypeLabel, Mode=OneWay}" />
                    <TextBox
                        Grid.Column="1"
                        PlaceholderText="e.g. Ctrl+C"
                        Text="{x:Bind Key, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                        Visibility="{x:Bind IsPressKey, Converter={StaticResource BoolToVisibilityConverter}, Mode=OneWay}" />
                    <TextBox
                        Grid.Column="1"
                        PlaceholderText="Text to type"
                        Text="{x:Bind Text, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                        Visibility="{x:Bind IsTypeText, Converter={StaticResource BoolToVisibilityConverter}, Mode=OneWay}" />
                    <NumberBox
                        Grid.Column="1"
                        Maximum="60000"
                        Minimum="0"
                        PlaceholderText="milliseconds"
                        SpinButtonPlacementMode="Inline"
                        Value="{x:Bind MsDouble, Mode=TwoWay}"
                        Visibility="{x:Bind IsWait, Converter={StaticResource BoolToVisibilityConverter}, Mode=OneWay}" />
                    <StackPanel
                        Grid.Column="1"
                        Spacing="4"
                        Visibility="{x:Bind IsRepeat, Converter={StaticResource BoolToVisibilityConverter}, Mode=OneWay}">
                        <NumberBox
                            Maximum="999"
                            Minimum="1"
                            PlaceholderText="repeat count"
                            SpinButtonPlacementMode="Inline"
                            Value="{x:Bind CountDouble, Mode=TwoWay}" />
                    </StackPanel>
                    <Button
                        Grid.Column="2"
                        Width="32"
                        Height="32"
                        Padding="0"
                        Click="DeleteStep_Click"
                        Content="&#x2715;"
                        Tag="{x:Bind}"
                        ToolTipService.ToolTip="Delete step" />
                </Grid>
            </Border>
        </DataTemplate>

        <!-- Branch step template — horizontal lane columns -->
        <DataTemplate x:Key="BranchStepTemplate" x:DataType="viewmodels:MacroStepViewModel">
            <Border
                Margin="0,0,0,4"
                BorderBrush="{ThemeResource SystemFillColorAttentionBrush}"
                BorderThickness="1"
                CornerRadius="4">
                <StackPanel>
                    <!-- Branch header row -->
                    <Grid Padding="8,6" Background="{ThemeResource SystemFillColorAttentionBackgroundBrush}">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*" />
                            <ColumnDefinition Width="Auto" />
                            <ColumnDefinition Width="32" />
                        </Grid.ColumnDefinitions>
                        <TextBlock
                            Grid.Column="0"
                            VerticalAlignment="Center"
                            Style="{StaticResource BodyStrongTextBlockStyle}"
                            Text="Branch" />
                        <Button
                            Grid.Column="1"
                            x:Uid="MacroFlowChart_AddBranch"
                            Margin="0,0,8,0"
                            Click="AddBranch_Click"
                            Tag="{x:Bind}" />
                        <Button
                            Grid.Column="2"
                            Width="32"
                            Height="32"
                            Padding="0"
                            Click="DeleteStep_Click"
                            Content="&#x2715;"
                            Tag="{x:Bind}"
                            ToolTipService.ToolTip="Delete branch step" />
                    </Grid>

                    <!-- Lane columns -->
                    <ScrollViewer HorizontalScrollBarVisibility="Auto" VerticalScrollBarVisibility="Disabled">
                        <ItemsControl ItemsSource="{x:Bind Branches, Mode=OneWay}">
                            <ItemsControl.ItemsPanel>
                                <ItemsPanelTemplate>
                                    <StackPanel Orientation="Horizontal" />
                                </ItemsPanelTemplate>
                            </ItemsControl.ItemsPanel>
                            <ItemsControl.ItemTemplate>
                                <DataTemplate x:DataType="viewmodels:MacroBranchViewModel">
                                    <Border
                                        MinWidth="200"
                                        Padding="8"
                                        BorderBrush="{ThemeResource DividerStrokeColorDefaultBrush}"
                                        BorderThickness="0,0,1,0">
                                        <StackPanel Spacing="6">

                                            <!-- Condition header: else label OR type+value -->
                                            <StackPanel
                                                Spacing="4"
                                                Visibility="{x:Bind Condition.IsElse, Converter={StaticResource BoolToVisibilityConverter}, Mode=OneWay}">
                                                <TextBlock
                                                    x:Uid="MacroFlowChart_ElseLabel"
                                                    FontStyle="Italic"
                                                    Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
                                            </StackPanel>
                                            <StackPanel Spacing="4">
                                                <!-- Shown when NOT else -->
                                                <ComboBox
                                                    HorizontalAlignment="Stretch"
                                                    SelectedItem="{x:Bind Condition.Type, Mode=TwoWay}">
                                                    <x:String>AppIsFocused</x:String>
                                                    <x:String>AppIsRunning</x:String>
                                                    <x:String>TimeAfter</x:String>
                                                    <x:String>TimeBefore</x:String>
                                                </ComboBox>
                                                <TextBox
                                                    x:Uid="MacroFlowChart_ConditionValue"
                                                    Text="{x:Bind Condition.Value, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
                                            </StackPanel>

                                            <!-- Steps in this lane (recursive MacroFlowChart) -->
                                            <ItemsControl ItemsSource="{x:Bind Steps, Mode=OneWay}">
                                                <ItemsControl.ItemTemplate>
                                                    <DataTemplate x:DataType="viewmodels:MacroStepViewModel">
                                                        <Border
                                                            Margin="0,0,0,4"
                                                            Padding="6,4"
                                                            BorderBrush="{ThemeResource DividerStrokeColorDefaultBrush}"
                                                            BorderThickness="1"
                                                            CornerRadius="4">
                                                            <Grid ColumnSpacing="6">
                                                                <Grid.ColumnDefinitions>
                                                                    <ColumnDefinition Width="60" />
                                                                    <ColumnDefinition Width="*" />
                                                                    <ColumnDefinition Width="28" />
                                                                </Grid.ColumnDefinitions>
                                                                <TextBlock
                                                                    Grid.Column="0"
                                                                    VerticalAlignment="Center"
                                                                    FontSize="12"
                                                                    Text="{x:Bind TypeLabel, Mode=OneWay}" />
                                                                <TextBox
                                                                    Grid.Column="1"
                                                                    PlaceholderText="e.g. Ctrl+C"
                                                                    Text="{x:Bind Key, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                                                                    Visibility="{x:Bind IsPressKey, Converter={StaticResource BoolToVisibilityConverter}, Mode=OneWay}" />
                                                                <TextBox
                                                                    Grid.Column="1"
                                                                    PlaceholderText="Text to type"
                                                                    Text="{x:Bind Text, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                                                                    Visibility="{x:Bind IsTypeText, Converter={StaticResource BoolToVisibilityConverter}, Mode=OneWay}" />
                                                                <NumberBox
                                                                    Grid.Column="1"
                                                                    Maximum="60000"
                                                                    Minimum="0"
                                                                    PlaceholderText="ms"
                                                                    SpinButtonPlacementMode="Inline"
                                                                    Value="{x:Bind MsDouble, Mode=TwoWay}"
                                                                    Visibility="{x:Bind IsWait, Converter={StaticResource BoolToVisibilityConverter}, Mode=OneWay}" />
                                                                <NumberBox
                                                                    Grid.Column="1"
                                                                    Maximum="999"
                                                                    Minimum="1"
                                                                    PlaceholderText="count"
                                                                    SpinButtonPlacementMode="Inline"
                                                                    Value="{x:Bind CountDouble, Mode=TwoWay}"
                                                                    Visibility="{x:Bind IsRepeat, Converter={StaticResource BoolToVisibilityConverter}, Mode=OneWay}" />
                                                                <Button
                                                                    Grid.Column="2"
                                                                    Width="28"
                                                                    Height="28"
                                                                    Padding="0"
                                                                    Click="DeleteBranchStep_Click"
                                                                    Content="&#x2715;"
                                                                    ToolTipService.ToolTip="Delete step" />
                                                            </Grid>
                                                        </Border>
                                                    </DataTemplate>
                                                </ItemsControl.ItemTemplate>
                                            </ItemsControl>

                                            <!-- Add step button inside lane -->
                                            <Button x:Uid="MacroFlowChart_AddStep">
                                                <Button.Flyout>
                                                    <MenuFlyout>
                                                        <MenuFlyoutItem Click="AddBranchStep_Click" Tag="PressKey" Text="Press Key" />
                                                        <MenuFlyoutItem Click="AddBranchStep_Click" Tag="TypeText" Text="Type Text" />
                                                        <MenuFlyoutItem Click="AddBranchStep_Click" Tag="Wait" Text="Wait" />
                                                    </MenuFlyout>
                                                </Button.Flyout>
                                            </Button>

                                        </StackPanel>
                                    </Border>
                                </DataTemplate>
                            </ItemsControl.ItemTemplate>
                        </ItemsControl>
                    </ScrollViewer>
                </StackPanel>
            </Border>
        </DataTemplate>

    </UserControl.Resources>

    <!-- Main step list with template selector -->
    <StackPanel Spacing="0">
        <ItemsControl x:Name="StepsList" ItemsSource="{x:Bind Steps, Mode=OneWay}">
            <ItemsControl.ItemTemplateSelector>
                <controls:MacroStepTemplateSelector
                    BranchTemplate="{StaticResource BranchStepTemplate}"
                    RegularTemplate="{StaticResource RegularStepTemplate}" />
            </ItemsControl.ItemTemplateSelector>
        </ItemsControl>

        <!-- Add step button (for top-level steps) -->
        <Button x:Uid="MacroEdit_AddStep">
            <Button.Flyout>
                <MenuFlyout>
                    <MenuFlyoutItem Click="AddStep_Click" Tag="PressKey" Text="Press Key" />
                    <MenuFlyoutItem Click="AddStep_Click" Tag="TypeText" Text="Type Text" />
                    <MenuFlyoutItem Click="AddStep_Click" Tag="Wait" Text="Wait" />
                    <MenuFlyoutItem Click="AddStep_Click" Tag="Repeat" Text="Repeat" />
                    <MenuFlyoutItem Click="AddStep_Click" Tag="Branch" Text="Branch" />
                </MenuFlyout>
            </Button.Flyout>
        </Button>
    </StackPanel>
</UserControl>
```

- [ ] **Step 3: Add `MacroStepTemplateSelector` to `MacroFlowChart.xaml.cs`**

Append this class at the bottom of the file (outside the `MacroFlowChart` class, inside the namespace):

```csharp
public sealed class MacroStepTemplateSelector : DataTemplateSelector
{
    public DataTemplate? RegularTemplate { get; set; }

    public DataTemplate? BranchTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item) =>
        item is MacroStepViewModel { IsBranch: true } ? BranchTemplate : RegularTemplate;

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) =>
        SelectTemplateCore(item);
}
```

- [ ] **Step 4: Build `Settings.UI` to verify no XAML or CS errors**

```
msbuild "src/settings-ui/Settings.UI/PowerToys.Settings.csproj" /p:Configuration=Debug /p:Platform=x64 /t:Build /m /nologo /v:minimal
```

Expected: `Build succeeded.` — no XAML parse errors, no CS compiler errors.

---

### Task 7: Replace `MacroEditDialog` steps section with `MacroFlowChart`

**Files:**
- Modify: `src/settings-ui/Settings.UI/SettingsXAML/Views/MacroEditDialog.xaml`
- Modify: `src/settings-ui/Settings.UI/SettingsXAML/Views/MacroEditDialog.xaml.cs`

- [ ] **Step 1: Update `MacroEditDialog.xaml`**

Replace the entire `<!-- Steps -->` `<StackPanel>` block (lines 52–195 in the original — everything from `<StackPanel Spacing="4">` that contains `StepsList` through the closing `</StackPanel>` before `<InfoBar`) with:

```xml
            <!-- Steps -->
            <StackPanel Spacing="4">
                <TextBlock
                    x:Uid="MacroEdit_StepsGroup"
                    Style="{StaticResource BodyStrongTextBlockStyle}" />

                <controls:MacroFlowChart
                    EditViewModel="{x:Bind ViewModel}"
                    Steps="{x:Bind ViewModel.Steps}" />
            </StackPanel>
```

Also add the `controls` namespace to the `<ContentDialog>` opening tag (after the existing `xmlns` declarations):

```xml
    xmlns:controls="using:Microsoft.PowerToys.Settings.UI.Controls"
```

Full `MacroEditDialog.xaml` after the change:

```xml
<!-- Copyright (c) Microsoft Corporation -->
<!-- The Microsoft Corporation licenses this file to you under the MIT license. -->
<!-- See the LICENSE file in the project root for more information. -->

<ContentDialog
    x:Class="Microsoft.PowerToys.Settings.UI.Views.MacroEditDialog"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:controls="using:Microsoft.PowerToys.Settings.UI.Controls"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:tkcontrols="using:CommunityToolkit.WinUI.Controls"
    xmlns:tkconverters="using:CommunityToolkit.WinUI.Converters"
    xmlns:viewmodels="using:Microsoft.PowerToys.Settings.UI.ViewModels"
    MinWidth="480"
    CloseButtonText="Cancel"
    DefaultButton="Primary"
    IsPrimaryButtonEnabled="{x:Bind ViewModel.IsValid, Mode=OneWay}"
    PrimaryButtonText="Save"
    mc:Ignorable="d">

    <ContentDialog.Resources>
        <tkconverters:BoolToVisibilityConverter x:Key="BoolToVisibilityConverter" />
    </ContentDialog.Resources>

    <ScrollViewer VerticalScrollBarVisibility="Auto">
        <StackPanel Padding="0,8,0,8" Spacing="16">

            <!-- Name -->
            <tkcontrols:SettingsCard x:Uid="MacroEdit_NameLabel">
                <TextBox
                    x:Name="NameBox"
                    MinWidth="200"
                    Text="{x:Bind ViewModel.Name, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
            </tkcontrols:SettingsCard>

            <!-- Hotkey -->
            <tkcontrols:SettingsCard x:Uid="MacroEdit_HotkeyLabel">
                <controls:MacroHotkeyControl
                    Hotkey="{x:Bind ViewModel.Hotkey, Mode=TwoWay}" />
            </tkcontrols:SettingsCard>

            <!-- App Scope -->
            <tkcontrols:SettingsCard x:Uid="MacroEdit_AppScopeLabel">
                <TextBox
                    MinWidth="200"
                    PlaceholderText="e.g. notepad.exe (leave blank for global)"
                    Text="{x:Bind ViewModel.AppScope, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
            </tkcontrols:SettingsCard>

            <!-- Steps -->
            <StackPanel Spacing="4">
                <TextBlock
                    x:Uid="MacroEdit_StepsGroup"
                    Style="{StaticResource BodyStrongTextBlockStyle}" />

                <controls:MacroFlowChart
                    EditViewModel="{x:Bind ViewModel}"
                    Steps="{x:Bind ViewModel.Steps}" />
            </StackPanel>

            <InfoBar
                IsClosable="False"
                IsOpen="{x:Bind ViewModel.HasValidationError, Mode=OneWay}"
                Severity="Error"
                Title="Name is required." />

        </StackPanel>
    </ScrollViewer>
</ContentDialog>
```

- [ ] **Step 2: Update `MacroEditDialog.xaml.cs`**

Remove the old step-handling code. The new code-behind only needs to initialize the ViewModel — all step operations are now handled by `MacroFlowChart`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views;

public sealed partial class MacroEditDialog : ContentDialog
{
    public MacroEditViewModel ViewModel { get; }

    public MacroEditDialog(MacroEditViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
```

- [ ] **Step 3: Build `Settings.UI`**

```
msbuild "src/settings-ui/Settings.UI/PowerToys.Settings.csproj" /p:Configuration=Debug /p:Platform=x64 /t:Build /m /nologo /v:minimal
```

Expected: `Build succeeded.`

- [ ] **Step 4: Build and run all UI unit tests**

```
msbuild "src/settings-ui/Settings.UI.UnitTests/Settings.UI.UnitTests.csproj" /p:Configuration=Debug /p:Platform=x64 /t:Build /m /nologo /v:minimal
```

Run the test executable (at `Debug\x64\tests\SettingsTests\PowerToys.Settings.UnitTests.exe`). Expected: all tests pass.

---

### Task 8: Smoke test and commit

- [ ] **Step 1: Launch PowerToys Settings**

```
x64\Debug\WinUI3Apps\PowerToys.Settings.exe Macro
```

- [ ] **Step 2: Manual smoke test checklist**

- [ ] Macro page loads without crash
- [ ] "New macro" dialog opens — shows Name, Hotkey, AppScope, and the flowchart area
- [ ] "Add step" menu shows: Press Key, Type Text, Wait, Repeat, **Branch**
- [ ] Adding a Branch step shows a lane column with condition header + "Add step" + "+ Add branch"
- [ ] Clicking "+ Add branch" adds a second lane
- [ ] Adding a step inside a lane shows it in that lane only
- [ ] Saving a macro with a Branch step writes valid JSON to `%APPDATA%\Microsoft\PowerToys\Macros\`
- [ ] Reopening the edit dialog for a saved Branch macro re-loads the branches correctly

- [ ] **Step 3: Commit**

```bash
git add src/settings-ui/Settings.UI/ViewModels/MacroConditionViewModel.cs
git add src/settings-ui/Settings.UI/ViewModels/MacroBranchViewModel.cs
git add src/settings-ui/Settings.UI/ViewModels/MacroStepViewModel.cs
git add src/settings-ui/Settings.UI/ViewModels/MacroEditViewModel.cs
git add "src/settings-ui/Settings.UI/SettingsXAML/Controls/MacroFlowChart/MacroFlowChart.xaml"
git add "src/settings-ui/Settings.UI/SettingsXAML/Controls/MacroFlowChart/MacroFlowChart.xaml.cs"
git add src/settings-ui/Settings.UI/SettingsXAML/Views/MacroEditDialog.xaml
git add src/settings-ui/Settings.UI/SettingsXAML/Views/MacroEditDialog.xaml.cs
git add src/settings-ui/Settings.UI/Strings/en-us/Resources.resw
git add src/settings-ui/Settings.UI.UnitTests/ViewModelTests/MacroStepViewModelTests.cs
git commit -m "feat(macro-ui): replace step ListView with MacroFlowChart; add Branch step type with condition lanes"
```
