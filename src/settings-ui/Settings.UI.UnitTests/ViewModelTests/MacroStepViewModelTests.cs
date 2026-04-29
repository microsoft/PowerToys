// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.MacroCommon.Models;

namespace ViewModelTests;

[TestClass]
public sealed class MacroStepViewModelTests
{
    [TestMethod]
    public void FromModel_PressKey_RoundTrip()
    {
        var step = new MacroStep { Type = StepType.PressKey, Key = "Ctrl+C" };
        var vm = MacroStepViewModel.FromModel(step);
        var result = vm.ToModel();
        Assert.AreEqual(StepType.PressKey, result.Type);
        Assert.AreEqual("Ctrl+C", result.Key);
    }

    [TestMethod]
    public void FromModel_TypeText_RoundTrip()
    {
        var step = new MacroStep { Type = StepType.TypeText, Text = "Hello" };
        var vm = MacroStepViewModel.FromModel(step);
        var result = vm.ToModel();
        Assert.AreEqual(StepType.TypeText, result.Type);
        Assert.AreEqual("Hello", result.Text);
    }

    [TestMethod]
    public void FromModel_Wait_RoundTrip()
    {
        var step = new MacroStep { Type = StepType.Wait, Ms = 500 };
        var vm = MacroStepViewModel.FromModel(step);
        var result = vm.ToModel();
        Assert.AreEqual(StepType.Wait, result.Type);
        Assert.AreEqual(500, result.Ms);
    }

    [TestMethod]
    public void FromModel_Repeat_RoundTrip()
    {
        var step = new MacroStep
        {
            Type = StepType.Repeat,
            Count = 3,
            Steps = [new MacroStep { Type = StepType.PressKey, Key = "Tab" }],
        };
        var vm = MacroStepViewModel.FromModel(step);
        Assert.AreEqual(1, vm.SubSteps.Count);
        Assert.AreEqual(StepType.PressKey, vm.SubSteps[0].Type);
        var result = vm.ToModel();
        Assert.AreEqual(StepType.Repeat, result.Type);
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(1, result.Steps?.Count);
        Assert.AreEqual("Tab", result.Steps![0].Key);
    }

    [TestMethod]
    public void FromModel_NestedRepeat_RoundTrip()
    {
        var step = new MacroStep
        {
            Type = StepType.Repeat,
            Count = 2,
            Steps =
            [
                new MacroStep
                {
                    Type = StepType.Repeat,
                    Count = 3,
                    Steps = [new MacroStep { Type = StepType.TypeText, Text = "x" }],
                },
            ],
        };
        var vm = MacroStepViewModel.FromModel(step);
        Assert.AreEqual(1, vm.SubSteps.Count);
        Assert.AreEqual(StepType.Repeat, vm.SubSteps[0].Type);
        Assert.AreEqual(1, vm.SubSteps[0].SubSteps.Count);

        var result = vm.ToModel();
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(1, result.Steps!.Count);
        Assert.AreEqual(3, result.Steps[0].Count);
        Assert.AreEqual("x", result.Steps[0].Steps![0].Text);
    }

    [TestMethod]
    public void MsDouble_GetSet_Syncs()
    {
        var vm = new MacroStepViewModel { Type = StepType.Wait, Ms = 100 };
        Assert.AreEqual(100.0, vm.MsDouble);
        vm.MsDouble = 200.0;
        Assert.AreEqual(200, vm.Ms);
    }

    [TestMethod]
    public void CountDouble_GetSet_Syncs()
    {
        var vm = new MacroStepViewModel { Type = StepType.Repeat, Count = 5 };
        Assert.AreEqual(5.0, vm.CountDouble);
        vm.CountDouble = 3.0;
        Assert.AreEqual(3, vm.Count);
    }

    [TestMethod]
    public void IsPressKey_OnlyTrueForPressKey()
    {
        Assert.IsTrue(new MacroStepViewModel { Type = StepType.PressKey }.IsPressKey);
        Assert.IsFalse(new MacroStepViewModel { Type = StepType.TypeText }.IsPressKey);
    }

    [TestMethod]
    public void IsRepeat_OnlyTrueForRepeat()
    {
        Assert.IsTrue(new MacroStepViewModel { Type = StepType.Repeat }.IsRepeat);
        Assert.IsFalse(new MacroStepViewModel { Type = StepType.Wait }.IsRepeat);
    }

    [TestMethod]
    public void ToModel_NonRepeat_NullSubSteps()
    {
        var vm = new MacroStepViewModel { Type = StepType.PressKey, Key = "A" };
        var model = vm.ToModel();
        Assert.IsNull(model.Steps);
    }

    [TestMethod]
    public void ToModel_RepeatWithEmptySubSteps_NullSteps()
    {
        var vm = new MacroStepViewModel { Type = StepType.Repeat, Count = 2 };
        var model = vm.ToModel();
        Assert.IsNull(model.Steps);
    }
}
