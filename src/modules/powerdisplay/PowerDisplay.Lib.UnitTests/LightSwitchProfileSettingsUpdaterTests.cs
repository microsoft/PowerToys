// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PowerDisplay.UnitTests;

[TestClass]
public class LightSwitchProfileSettingsUpdaterTests
{
    [TestMethod]
    public void ClearDeletedProfileAndSend_MatchingIds_ClearsAndSendsOnce()
    {
        var settings = new LightSwitchSettings();
        settings.Properties.DarkModeProfileId.Value = 7;
        settings.Properties.LightModeProfileId.Value = 7;
        settings.Properties.DarkModeProfile.Value = "Legacy dark";
        settings.Properties.LightModeProfile.Value = "Legacy light";
        var messages = new List<string>();

        var changed = LightSwitchProfileSettingsUpdater.ClearDeletedProfileAndSend(
            settings,
            7,
            message =>
            {
                messages.Add(message);
                return 0;
            });

        Assert.IsTrue(changed);
        Assert.AreEqual(0, settings.Properties.DarkModeProfileId.Value);
        Assert.AreEqual(0, settings.Properties.LightModeProfileId.Value);
        Assert.AreEqual("Legacy dark", settings.Properties.DarkModeProfile.Value);
        Assert.AreEqual("Legacy light", settings.Properties.LightModeProfile.Value);
        Assert.AreEqual(1, messages.Count);
    }

    [TestMethod]
    public void ClearDeletedProfileAndSend_NonMatchingIds_DoesNotSend()
    {
        var settings = new LightSwitchSettings();
        settings.Properties.DarkModeProfileId.Value = 5;
        settings.Properties.LightModeProfileId.Value = 6;
        var sendCount = 0;

        var changed = LightSwitchProfileSettingsUpdater.ClearDeletedProfileAndSend(
            settings,
            7,
            _ =>
            {
                sendCount++;
                return 0;
            });

        Assert.IsFalse(changed);
        Assert.AreEqual(5, settings.Properties.DarkModeProfileId.Value);
        Assert.AreEqual(6, settings.Properties.LightModeProfileId.Value);
        Assert.AreEqual(0, sendCount);
    }

    [TestMethod]
    public void ClearDeletedProfileAndSend_ZeroDeletedProfileId_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            LightSwitchProfileSettingsUpdater.ClearDeletedProfileAndSend(
                new LightSwitchSettings(),
                0,
                _ => 0));
    }
}
