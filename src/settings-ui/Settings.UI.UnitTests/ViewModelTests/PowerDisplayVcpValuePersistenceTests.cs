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

[TestClass]
public class PowerDisplayVcpValuePersistenceTests
{
    [TestMethod]
    public void DisabledVcpValues_ChangesSaveAndSendOnceWhileRefreshingPresets()
    {
        var monitor = CreateMonitor();
        var namedEvents = new List<string>();
        var operationOrder = new List<string>();
        using var viewModel = CreateViewModel(out _, out var settingsUtils, out var ipcMessages, monitor, namedEvents: namedEvents, operationOrder: operationOrder);
        Assert.AreEqual(2, monitor.ColorPresetsForDisplay.Count);

        monitor.DisabledVcpValues = new List<VcpValueBlock>
        {
            new() { VcpCode = 0x14, Values = new() { 0x05 } },
        };

        settingsUtils.Verify(
            utils => utils.SaveSettings(It.IsAny<string>(), PowerDisplaySettings.ModuleName, SettingsUtils.DefaultFileName),
            Times.Once);
        Assert.AreEqual(1, ipcMessages.Count);
        Assert.AreEqual(1, namedEvents.Count);
        Assert.AreEqual("save,ipc,signal", string.Join(',', operationOrder));
        var savedBlock = GetSavedSettings(settingsUtils).Properties.Monitors.Single().DisabledVcpValues.Single();
        Assert.AreEqual((byte)0x14, savedBlock.VcpCode);
        Assert.AreEqual(0x05, savedBlock.Values.Single());
        Assert.AreEqual(0x08, monitor.ColorPresetsForDisplay.Single().VcpValue);
    }

    [TestMethod]
    public void ReloadMonitors_ChangedRestrictionsDoNotSaveOrSend()
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
        updatedMonitor.DisabledVcpValues = new List<VcpValueBlock>
        {
            new() { VcpCode = 0x14, Values = new() { 0x05 } },
        };
        updatedSettings.Properties.Monitors.Add(updatedMonitor);
        settingsUtils.Setup(utils => utils.GetSettingsOrDefault<PowerDisplaySettings>(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(updatedSettings);

        reloadMonitors();

        Assert.AreSame(monitor, viewModel.Monitors.Single());
        Assert.IsTrue(monitor.EnableColorTemperature);
        Assert.AreEqual(0x05, monitor.DisabledVcpValues.Single().Values.Single());
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
