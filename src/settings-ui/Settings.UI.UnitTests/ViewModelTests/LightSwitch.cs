// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Reflection;
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
    public void SuppressedProfileSelectionChange_DoesNotPersistTemporaryZero()
    {
        var settings = new LightSwitchSettings();
        settings.Properties.DarkModeProfileId.Value = 7;
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
            Id = 7,
        };

        viewModel.SelectedDarkModeProfile = selected;
        SetSuppression(viewModel, true);
        viewModel.SelectedDarkModeProfile = null;

        Assert.AreEqual(7, settings.Properties.DarkModeProfileId.Value);
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
