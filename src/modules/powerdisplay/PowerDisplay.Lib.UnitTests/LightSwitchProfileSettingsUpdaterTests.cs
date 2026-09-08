// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public class LightSwitchProfileSettingsUpdaterTests
{
    private static readonly Guid ProfileId = new("4f7d8a46-9e11-43e0-bbc6-450402bb8abc");
    private static readonly Guid OtherId = new("a5f24452-4389-4f76-b6dd-4434f6c9c124");

    [TestMethod]
    public void ClearDeletedProfileAndSend_LegacyName_ResolvesBeforeClearing()
    {
        var settings = new LightSwitchSettings();
        settings.Properties.DarkModeProfile.Value = "Night";
        var profiles = new PowerDisplayProfiles
        {
            Profiles = new List<PowerDisplayProfile>
            {
                new PowerDisplayProfile { Id = ProfileId, Name = "Night", Order = 0 },
                new PowerDisplayProfile { Id = OtherId, Name = "Night", Order = 1 },
            },
        };
        var sendCount = 0;

        Assert.IsTrue(LightSwitchProfileSettingsUpdater.ClearDeletedProfileAndSend(
            settings,
            ProfileId,
            _ => ++sendCount,
            profilesBeforeDeletion: profiles));

        Assert.AreEqual(Guid.Empty, settings.Properties.DarkModeProfileId.Value);
        Assert.AreEqual(string.Empty, settings.Properties.DarkModeProfile.Value);
        Assert.AreEqual(1, sendCount);
    }

    [TestMethod]
    public void ClearDeletedProfileAndSend_NameResolvesAnotherProfile_PreservesAndSendsMigratedReference()
    {
        var settings = new LightSwitchSettings();
        settings.Properties.DarkModeProfile.Value = "Night";
        var profiles = new PowerDisplayProfiles
        {
            Profiles = new List<PowerDisplayProfile>
            {
                new PowerDisplayProfile { Id = OtherId, Name = "Night", Order = 0 },
                new PowerDisplayProfile { Id = ProfileId, Name = "Night", Order = 1 },
            },
        };
        var sendCount = 0;

        Assert.IsTrue(LightSwitchProfileSettingsUpdater.ClearDeletedProfileAndSend(
            settings,
            ProfileId,
            _ => ++sendCount,
            profilesBeforeDeletion: profiles));

        Assert.AreEqual(OtherId, settings.Properties.DarkModeProfileId.Value);
        Assert.AreEqual(string.Empty, settings.Properties.DarkModeProfile.Value);
        Assert.AreEqual(1, sendCount);
    }

    [TestMethod]
    public void ClearDeletedProfileAndSend_LegacyNumericReference_ClearsAndSends()
    {
        var settings = new LightSwitchSettings();
        settings.Properties.DarkModeProfileId.LegacyId = 7;
        var sendCount = 0;

        Assert.IsTrue(LightSwitchProfileSettingsUpdater.ClearDeletedProfileAndSend(
            settings,
            ProfileId,
            _ => ++sendCount,
            legacyId: 7));

        Assert.AreEqual(Guid.Empty, settings.Properties.DarkModeProfileId.Value);
        Assert.IsNull(settings.Properties.DarkModeProfileId.LegacyId);
        Assert.AreEqual(1, sendCount);
    }

    [TestMethod]
    public void ClearDeletedProfileAndSend_MatchingIds_ClearsAndSendsOnce()
    {
        var settings = new LightSwitchSettings();
        settings.Properties.DarkModeProfileId.Value = ProfileId;
        settings.Properties.LightModeProfileId.Value = ProfileId;
        settings.Properties.DarkModeProfile.Value = "Legacy dark";
        settings.Properties.LightModeProfile.Value = "Legacy light";
        var messages = new List<string>();

        var changed = LightSwitchProfileSettingsUpdater.ClearDeletedProfileAndSend(
            settings,
            ProfileId,
            message =>
            {
                messages.Add(message);
                return 0;
            });

        Assert.IsTrue(changed);
        Assert.AreEqual(Guid.Empty, settings.Properties.DarkModeProfileId.Value);
        Assert.AreEqual(Guid.Empty, settings.Properties.LightModeProfileId.Value);
        Assert.AreEqual(string.Empty, settings.Properties.DarkModeProfile.Value);
        Assert.AreEqual(string.Empty, settings.Properties.LightModeProfile.Value);
        Assert.AreEqual(1, messages.Count);
    }

    [TestMethod]
    public void ClearDeletedProfileAndSend_NonMatchingIds_DoesNotSend()
    {
        var settings = new LightSwitchSettings();
        settings.Properties.DarkModeProfileId.Value = OtherId;
        settings.Properties.LightModeProfileId.Value = OtherId;
        var sendCount = 0;

        var changed = LightSwitchProfileSettingsUpdater.ClearDeletedProfileAndSend(
            settings,
            ProfileId,
            _ =>
            {
                sendCount++;
                return 0;
            });

        Assert.IsFalse(changed);
        Assert.AreEqual(OtherId, settings.Properties.DarkModeProfileId.Value);
        Assert.AreEqual(OtherId, settings.Properties.LightModeProfileId.Value);
        Assert.AreEqual(0, sendCount);
    }

    [TestMethod]
    public void ClearDeletedProfileAndSend_EmptyDeletedProfileId_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            LightSwitchProfileSettingsUpdater.ClearDeletedProfileAndSend(
                new LightSwitchSettings(),
                Guid.Empty,
                _ => 0));
    }
}
