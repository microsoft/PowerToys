// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.UnitTests.BackwardsCompatibility;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Models;

namespace ViewModelTests;

[TestClass]
public class LightSwitch
{
    [TestMethod]
    public void UnrelatedSettingChange_PreservesUnresolvedNumericReferenceInIpc()
    {
        var settings = new LightSwitchSettings();
        settings.Properties.DarkModeProfileId.LegacyId = 7;
        var messages = new List<string>();
        var viewModel = CreateViewModel(settings, message =>
        {
            messages.Add(message);
            return 0;
        });

        viewModel.EnableDarkModeProfile = true;

        Assert.AreEqual(1, messages.Count);
        using var document = JsonDocument.Parse(messages[0]);
        Assert.AreEqual(7, document.RootElement.GetProperty("powertoys").GetProperty("LightSwitch").GetProperty("properties").GetProperty("darkModeProfileId").GetProperty("value").GetInt32());
    }

    [TestMethod]
    public void ExplicitlyClearingSelection_ClearsLegacyFallbackAndPersistsEmptyUuid()
    {
        var settings = new LightSwitchSettings();
        var id = Guid.NewGuid();
        settings.Properties.DarkModeProfileId.Value = id;
        var messages = new List<string>();
        var viewModel = CreateViewModel(settings, message =>
        {
            messages.Add(message);
            return 0;
        });
        viewModel.SelectedDarkModeProfile = new PowerDisplayProfile { Id = id, Name = "Night" };
        settings.Properties.DarkModeProfileId.LegacyId = 7;
        settings.Properties.DarkModeProfile.Value = "Night";

        viewModel.SelectedDarkModeProfile = null;

        Assert.AreEqual(Guid.Empty, settings.Properties.DarkModeProfileId.Value);
        Assert.IsNull(settings.Properties.DarkModeProfileId.LegacyId);
        Assert.AreEqual(string.Empty, settings.Properties.DarkModeProfile.Value);
        Assert.AreEqual(1, messages.Count);
    }

    [TestMethod]
    public void SuppressedProfileSelectionChange_DoesNotPersistTemporaryEmptyId()
    {
        var id = Guid.NewGuid();
        var settings = new LightSwitchSettings();
        settings.Properties.DarkModeProfileId.Value = id;
        var messages = new List<string>();
        var viewModel = CreateViewModel(settings, message =>
        {
            messages.Add(message);
            return 0;
        });
        var selected = new PowerDisplayProfile(
            "Night",
            new List<ProfileMonitorSetting>
            {
                new ProfileMonitorSetting("MON1", 50, null, null, null),
            })
        {
            Id = id,
        };

        viewModel.SelectedDarkModeProfile = selected;
        SetSuppression(viewModel, true);
        viewModel.SelectedDarkModeProfile = null;

        Assert.AreEqual(id, settings.Properties.DarkModeProfileId.Value);
        Assert.AreEqual(0, messages.Count);
    }

    private static LightSwitchViewModel CreateViewModel(
        LightSwitchSettings settings,
        System.Func<string, int> sendConfigMessage)
    {
        var generalSettingsRepository =
            new BackCompatTestProperties.MockSettingsRepository<GeneralSettings>(
                ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>().Object);

        return new LightSwitchViewModel(
            generalSettingsRepository,
            settings,
            sendConfigMessage);
    }

    private static void SetSuppression(LightSwitchViewModel viewModel, bool value)
    {
        var field = typeof(LightSwitchViewModel).GetField(
            "_suppressProfileSelectionPersistence",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        field.SetValue(viewModel, value);
    }
}
