// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.MacroCommon.Models;

namespace ViewModelTests;

[TestClass]
public sealed class MacroEditViewModelTests
{
    [TestMethod]
    public void Constructor_Default_HasEmptyName()
    {
        MacroEditViewModel vm = new();
        Assert.AreEqual(string.Empty, vm.Name);
        Assert.IsTrue(vm.HasValidationError);
    }

    [TestMethod]
    public void Constructor_WithDefinition_PopulatesFields()
    {
        MacroDefinition def = new()
        {
            Id = "abc",
            Name = "My Macro",
            Hotkey = "Ctrl+F9",
            AppScope = "notepad.exe",
        };
        MacroEditViewModel vm = new(def);

        Assert.AreEqual("My Macro", vm.Name);
        Assert.AreEqual("notepad.exe", vm.AppScope);
        Assert.AreEqual("Ctrl+F9", vm.HotkeyText);
    }

    [TestMethod]
    public void HasValidationError_FalseWhenNameSet()
    {
        MacroEditViewModel vm = new() { Name = "My Macro" };
        Assert.IsFalse(vm.HasValidationError);
    }

    [TestMethod]
    public void HasValidationError_TrueWhenNameWhitespace()
    {
        MacroEditViewModel vm = new() { Name = "   " };
        Assert.IsTrue(vm.HasValidationError);
    }

    [TestMethod]
    public void ToDefinition_PreservesOriginalId()
    {
        MacroDefinition def = new() { Id = "keep-me", Name = "X" };
        MacroEditViewModel vm = new(def) { Name = "Updated" };
        MacroDefinition result = vm.ToDefinition();
        Assert.AreEqual("keep-me", result.Id);
        Assert.AreEqual("Updated", result.Name);
    }

    [TestMethod]
    public void ToDefinition_HotkeyRoundTrip()
    {
        MacroDefinition def = new() { Name = "X", Hotkey = "Ctrl+Shift+F5" };
        MacroEditViewModel vm = new(def);
        MacroDefinition result = vm.ToDefinition();
        Assert.AreEqual("Ctrl+Shift+F5", result.Hotkey);
    }

    [TestMethod]
    public void ToDefinition_AppScopeBlank_SerializesNull()
    {
        MacroEditViewModel vm = new() { Name = "X", AppScope = "   " };
        MacroDefinition result = vm.ToDefinition();
        Assert.IsNull(result.AppScope);
    }

    [TestMethod]
    public void AddStep_AddsToCollection()
    {
        MacroEditViewModel vm = new() { Name = "X" };
        vm.AddStep(StepType.PressKey);
        Assert.AreEqual(1, vm.Steps.Count);
        Assert.AreEqual(StepType.PressKey, vm.Steps[0].Type);
    }

    [TestMethod]
    public void DeleteStep_RemovesFromCollection()
    {
        MacroEditViewModel vm = new() { Name = "X" };
        vm.AddStep(StepType.TypeText);
        MacroStepViewModel step = vm.Steps[0];
        vm.DeleteStep(step);
        Assert.AreEqual(0, vm.Steps.Count);
    }

    [TestMethod]
    public void AddSubStep_AddsToRepeatSubSteps()
    {
        MacroEditViewModel vm = new() { Name = "X" };
        vm.AddStep(StepType.Repeat);
        MacroStepViewModel repeat = vm.Steps[0];
        vm.AddSubStep(repeat, StepType.Wait);
        Assert.AreEqual(1, repeat.SubSteps.Count);
        Assert.AreEqual(StepType.Wait, repeat.SubSteps[0].Type);
    }

    [TestMethod]
    public void DeleteSubStep_RemovesFromRepeatSubSteps()
    {
        MacroEditViewModel vm = new() { Name = "X" };
        vm.AddStep(StepType.Repeat);
        MacroStepViewModel repeat = vm.Steps[0];
        vm.AddSubStep(repeat, StepType.PressKey);
        MacroStepViewModel sub = repeat.SubSteps[0];
        vm.DeleteSubStep(repeat, sub);
        Assert.AreEqual(0, repeat.SubSteps.Count);
    }

    [TestMethod]
    public void ToDefinition_PreservesIsEnabled_False()
    {
        var definition = new MacroDefinition { Name = "x", IsEnabled = false };
        var vm = new MacroEditViewModel(definition);
        Assert.IsFalse(vm.ToDefinition().IsEnabled);
    }

    [TestMethod]
    public void ToDefinition_DefaultIsEnabled_True()
    {
        var vm = new MacroEditViewModel();
        Assert.IsTrue(vm.ToDefinition().IsEnabled);
    }

    [TestMethod]
    public void ToDefinition_Steps_RoundTrip()
    {
        MacroDefinition def = new()
        {
            Name = "X",
            Steps =
            [
                new MacroStep { Type = StepType.TypeText, Text = "Hello" },
                new MacroStep
                {
                    Type = StepType.Repeat,
                    Count = 2,
                    Steps = [new MacroStep { Type = StepType.PressKey, Key = "Tab" }],
                },
            ],
        };
        MacroEditViewModel vm = new(def);
        MacroDefinition result = vm.ToDefinition();

        Assert.AreEqual(2, result.Steps.Count);
        Assert.AreEqual("Hello", result.Steps[0].Text);
        Assert.AreEqual(2, result.Steps[1].Count);
        Assert.AreEqual("Tab", result.Steps[1].Steps![0].Key);
    }
}
