// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.UnitTests.BackwardsCompatibility;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
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

    [TestMethod]
    [DataRow(nameof(MonitorInfo.EnableContrast))]
    [DataRow(nameof(MonitorInfo.EnableVolume))]
    [DataRow(nameof(MonitorInfo.EnableInputSource))]
    [DataRow(nameof(MonitorInfo.EnableRotation))]
    [DataRow(nameof(MonitorInfo.EnableColorTemperature))]
    [DataRow(nameof(MonitorInfo.EnablePowerState))]
    [DataRow(nameof(MonitorInfo.IsHidden))]
    public void MonitorUserPreference_ChangesSaveAndSendOnce(string propertyName)
    {
        var monitor = CreateMonitor();
        var namedEvents = new List<string>();
        var operationOrder = new List<string>();
        using var viewModel = CreateViewModel(out _, out var settingsUtils, out var ipcMessages, monitor, namedEvents: namedEvents, operationOrder: operationOrder);
        var property = typeof(MonitorInfo).GetProperty(propertyName);

        property.SetValue(monitor, true);

        settingsUtils.Verify(
            utils => utils.SaveSettings(It.IsAny<string>(), PowerDisplaySettings.ModuleName, SettingsUtils.DefaultFileName),
            Times.Once);
        Assert.AreEqual(1, ipcMessages.Count);
        Assert.AreEqual(1, namedEvents.Count);
        Assert.AreEqual("save,ipc,signal", string.Join(',', operationOrder));
        Assert.IsTrue((bool)property.GetValue(GetSavedSettings(settingsUtils).Properties.Monitors.Single()));
    }

    [TestMethod]
    public void MonitorRuntimeAndDerivedProperties_DoNotSaveOrSend()
    {
        var monitor = CreateMonitor();
        var namedEvents = new List<string>();
        using var viewModel = CreateViewModel(out _, out var settingsUtils, out var ipcMessages, monitor, namedEvents: namedEvents);

        monitor.Name = "Updated monitor";
        monitor.CurrentBrightness = 75;
        monitor.ColorTemperatureVcp = 0x08;
        monitor.SupportsColorTemperature = false;
        monitor.CapabilitiesRaw = "(vcp(10))";
        monitor.VcpCodesFormatted = new List<VcpCodeDisplayInfo>();

        settingsUtils.Verify(
            utils => utils.SaveSettings(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        Assert.AreEqual(0, ipcMessages.Count);
        Assert.AreEqual(0, namedEvents.Count);
    }

    [TestMethod]
    public void ReloadMonitors_ChangedUserPreferencesDoNotSaveOrSend()
    {
        var monitor = CreateMonitor();
        Action reloadMonitors = null;
        var namedEvents = new List<string>();
        using var viewModel = CreateViewModel(
            out _,
            out var settingsUtils,
            out var ipcMessages,
            monitor,
            (_, callback) => reloadMonitors = callback,
            namedEvents);
        var updatedSettings = new PowerDisplaySettings();
        var updatedMonitor = CreateMonitor();
        updatedMonitor.EnableColorTemperature = true;
        updatedMonitor.EnableInputSource = true;
        updatedMonitor.CapabilitiesRaw = "(vcp(10 14(05 08) 60(11)))";
        updatedSettings.Properties.Monitors.Add(updatedMonitor);
        settingsUtils.Setup(utils => utils.GetSettingsOrDefault<PowerDisplaySettings>(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(updatedSettings);

        reloadMonitors();

        Assert.AreSame(monitor, viewModel.Monitors.Single());
        Assert.IsTrue(monitor.EnableColorTemperature);
        Assert.IsTrue(monitor.EnableInputSource);
        Assert.AreEqual(updatedMonitor.CapabilitiesRaw, monitor.CapabilitiesRaw);
        settingsUtils.Verify(
            utils => utils.SaveSettings(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        Assert.AreEqual(0, ipcMessages.Count);
        Assert.AreEqual(0, namedEvents.Count);
    }

    private static MonitorInfo CreateMonitor()
    {
        return new MonitorInfo
        {
            Id = @"\\?\DISPLAY#TEST001#1",
            SupportsColorTemperature = true,
            ColorTemperatureVcp = 0x05,
            VcpCodesFormatted = new List<VcpCodeDisplayInfo>
            {
                new()
                {
                    Code = "0x14",
                    ValueList = new()
                    {
                        new() { Value = "0x05", Name = "6500 K" },
                        new() { Value = "0x08", Name = "9300 K" },
                    },
                },
            },
        };
    }

    private static PowerDisplaySettings GetSavedSettings(Mock<SettingsUtils> settingsUtils)
    {
        var save = settingsUtils.Invocations.Single(invocation => invocation.Method.Name == nameof(SettingsUtils.SaveSettings));
        return JsonSerializer.Deserialize((string)save.Arguments[0], SettingsSerializationContext.Default.PowerDisplaySettings);
    }

    private static PowerDisplayViewModel CreateViewModel(out PowerDisplaySettings settings)
        => CreateViewModel(out settings, out _, out _);

    private static PowerDisplayViewModel CreateViewModel(
        out PowerDisplaySettings settings,
        out Mock<SettingsUtils> settingsUtils,
        out List<string> ipcMessages,
        MonitorInfo monitor = null,
        Action<string, Action> waitForEventLoop = null,
        List<string> namedEvents = null,
        List<string> operationOrder = null)
    {
        var powerDisplaySettingsUtils =
            ISettingsUtilsMocks.GetStubSettingsUtils<PowerDisplaySettings>();
        var generalSettingsUtils =
            ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>();

        settings = powerDisplaySettingsUtils.Object.GetSettingsOrDefault<PowerDisplaySettings>(
            PowerDisplaySettings.ModuleName);
        if (monitor != null)
        {
            settings.Properties.Monitors.Add(monitor);
        }

        settingsUtils = powerDisplaySettingsUtils;
        var messages = new List<string>();
        ipcMessages = messages;
        var events = namedEvents ?? new List<string>();
        var operations = operationOrder ?? new List<string>();
        powerDisplaySettingsUtils.Setup(utils => utils.SaveSettings(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, _) => operations.Add("save"));

        return new PowerDisplayViewModel(
            powerDisplaySettingsUtils.Object,
            new BackCompatTestProperties.MockSettingsRepository<GeneralSettings>(
                generalSettingsUtils.Object),
            new BackCompatTestProperties.MockSettingsRepository<PowerDisplaySettings>(
                powerDisplaySettingsUtils.Object),
            message =>
            {
                messages.Add(message);
                operations.Add("ipc");
                return 0;
            },
            waitForEventLoop ?? ((_, _) => { }),
            eventName =>
            {
                events.Add(eventName);
                operations.Add("signal");
            });
    }
}
