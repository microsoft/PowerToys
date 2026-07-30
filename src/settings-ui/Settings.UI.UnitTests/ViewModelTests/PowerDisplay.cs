// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.UnitTests.BackwardsCompatibility;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Models;

namespace ViewModelTests;

// SA1649: file is named PowerDisplay.cs to match the module naming convention used throughout
// ViewModelTests (FancyZones.cs, ColorPicker.cs, etc.).
[TestClass]
public class PowerDisplay
{
    [TestMethod]
    public void MouseWheelMode_DefaultsToDisabled()
    {
        using var viewModel = CreateViewModel(out _);

        Assert.AreEqual(
            (int)MouseWheelControlMode.Disabled,
            viewModel.MouseWheelControlModeIndex);
    }

    [TestMethod]
    public void MouseWheelMode_SetPrimaryDisplay_PersistsAndRoundTrips()
    {
        using var viewModel = CreateViewModel(out var settings);

        viewModel.MouseWheelControlModeIndex = (int)MouseWheelControlMode.PrimaryDisplay;

        Assert.AreEqual(
            MouseWheelControlMode.PrimaryDisplay,
            settings.Properties.MouseWheelControlMode);
        Assert.AreEqual(
            (int)MouseWheelControlMode.PrimaryDisplay,
            viewModel.MouseWheelControlModeIndex);
    }

    [TestMethod]
    public void MouseWheelMode_SetAllDisplays_PersistsAndRoundTrips()
    {
        using var viewModel = CreateViewModel(out var settings);

        viewModel.MouseWheelControlModeIndex = (int)MouseWheelControlMode.AllDisplays;

        Assert.AreEqual(
            MouseWheelControlMode.AllDisplays,
            settings.Properties.MouseWheelControlMode);
        Assert.AreEqual(
            (int)MouseWheelControlMode.AllDisplays,
            viewModel.MouseWheelControlModeIndex);
    }

    // The ComboBox in PowerDisplayPage.xaml binds SelectedIndex straight to the enum value, so the
    // declared item order (Off, Primary display, All displays) is load-bearing. Pin the numbering
    // here: inserting a new mode anywhere but at the end would silently remap existing settings.
    [TestMethod]
    public void MouseWheelMode_EnumValues_MatchComboBoxItemOrder()
    {
        Assert.AreEqual(0, (int)MouseWheelControlMode.Disabled);
        Assert.AreEqual(1, (int)MouseWheelControlMode.PrimaryDisplay);
        Assert.AreEqual(2, (int)MouseWheelControlMode.AllDisplays);
    }

    [TestMethod]
    public void MouseWheelMode_UnsupportedIndex_IsIgnored()
    {
        using var viewModel = CreateViewModel(out var settings);
        var changedProperties = new List<string>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.MouseWheelControlModeIndex = 99;

        Assert.AreEqual(
            MouseWheelControlMode.Disabled,
            settings.Properties.MouseWheelControlMode);
        CollectionAssert.Contains(
            changedProperties,
            nameof(PowerDisplayViewModel.MouseWheelControlModeIndex));
    }

    private static PowerDisplayViewModel CreateViewModel(out PowerDisplaySettings settings)
    {
        var powerDisplaySettingsUtils =
            ISettingsUtilsMocks.GetStubSettingsUtils<PowerDisplaySettings>();
        var generalSettingsUtils =
            ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>();

        settings = powerDisplaySettingsUtils.Object.GetSettingsOrDefault<PowerDisplaySettings>(
            PowerDisplaySettings.ModuleName);

        return new PowerDisplayViewModel(
            powerDisplaySettingsUtils.Object,
            new BackCompatTestProperties.MockSettingsRepository<GeneralSettings>(
                generalSettingsUtils.Object),
            new BackCompatTestProperties.MockSettingsRepository<PowerDisplaySettings>(
                powerDisplaySettingsUtils.Object),
            _ => 0,
            (_, _) => { });
    }
}
