// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public class LightSwitchProfileSettingsUpdaterTests
{
    [TestMethod]
    public void ReconcileAndSend_LegacyReference_SendsMigratedIdWithoutName()
    {
        var settings = new LightSwitchSettings();
        settings.Properties.DarkModeProfile.Value = "Night";
        var messages = new List<string>();

        var changed = LightSwitchProfileSettingsUpdater.ReconcileAndSend(
            settings,
            Profiles(("Night", 7)),
            message =>
            {
                messages.Add(message);
                return 0;
            });

        Assert.IsTrue(changed);
        Assert.AreEqual(1, messages.Count);
        Assert.AreEqual(7, settings.Properties.DarkModeProfileId.Value);
        Assert.AreEqual(string.Empty, settings.Properties.DarkModeProfile.Value);
        StringAssert.Contains(messages[0], "\"darkModeProfileId\":{\"value\":7}");
        StringAssert.Contains(messages[0], "\"darkModeProfile\":{\"value\":\"\"}");
    }

    [TestMethod]
    public void ReconcileAndSend_IdOnlyReference_DoesNotSend()
    {
        var settings = new LightSwitchSettings();
        settings.Properties.DarkModeProfileId.Value = 7;
        var sendCount = 0;

        var changed = LightSwitchProfileSettingsUpdater.ReconcileAndSend(
            settings,
            Profiles(("Night", 7)),
            _ =>
            {
                sendCount++;
                return 0;
            });

        Assert.IsFalse(changed);
        Assert.AreEqual(0, sendCount);
    }

    [TestMethod]
    public void ReconcileAndSend_StaleId_ClearsReferenceAndSendsOnce()
    {
        var settings = new LightSwitchSettings();
        settings.Properties.DarkModeProfileId.Value = 7;
        settings.Properties.DarkModeProfile.Value = "Night";
        var messages = new List<string>();

        var changed = LightSwitchProfileSettingsUpdater.ReconcileAndSend(
            settings,
            Profiles(),
            message =>
            {
                messages.Add(message);
                return 0;
            });

        Assert.IsTrue(changed);
        Assert.AreEqual(1, messages.Count);
        Assert.AreEqual(0, settings.Properties.DarkModeProfileId.Value);
        Assert.AreEqual(string.Empty, settings.Properties.DarkModeProfile.Value);
    }

    private static PowerDisplayProfiles Profiles(params (string Name, int Id)[] items)
    {
        var profiles = new PowerDisplayProfiles();
        foreach (var (name, id) in items)
        {
            profiles.Profiles.Add(new PowerDisplayProfile(
                name,
                new List<ProfileMonitorSetting>
                {
                    new ProfileMonitorSetting("MON1", 50, null, null, null),
                })
            {
                Id = id,
            });
        }

        return profiles;
    }
}
