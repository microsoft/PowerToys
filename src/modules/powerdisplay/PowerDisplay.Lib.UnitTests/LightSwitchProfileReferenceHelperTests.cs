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
public class LightSwitchProfileReferenceHelperTests
{
    [TestMethod]
    public void GetProfileIdForTheme_DisabledOrZeroId_ReturnsNull()
    {
        var properties = new LightSwitchProperties();
        properties.EnableDarkModeProfile.Value = false;
        properties.DarkModeProfileId.Value = 7;
        properties.EnableLightModeProfile.Value = true;
        properties.LightModeProfileId.Value = 0;

        Assert.IsNull(LightSwitchProfileReferenceHelper.GetProfileIdForTheme(properties, isLightMode: false));
        Assert.IsNull(LightSwitchProfileReferenceHelper.GetProfileIdForTheme(properties, isLightMode: true));
    }

    [TestMethod]
    public void GetProfileIdForTheme_EnabledPositiveIds_ReturnsThemeId()
    {
        var properties = new LightSwitchProperties();
        properties.EnableDarkModeProfile.Value = true;
        properties.DarkModeProfileId.Value = 7;
        properties.EnableLightModeProfile.Value = true;
        properties.LightModeProfileId.Value = 4;

        Assert.AreEqual(7, LightSwitchProfileReferenceHelper.GetProfileIdForTheme(properties, isLightMode: false));
        Assert.AreEqual(4, LightSwitchProfileReferenceHelper.GetProfileIdForTheme(properties, isLightMode: true));
    }

    [TestMethod]
    public void ReconcileReferences_LegacyNames_MigratesIdsAndClearsNames()
    {
        var properties = new LightSwitchProperties();
        properties.DarkModeProfile.Value = "Night";
        properties.LightModeProfile.Value = "Day";

        Assert.IsTrue(LightSwitchProfileReferenceHelper.ReconcileReferences(
            properties,
            Profiles(("Day", 4), ("Night", 7))));
        Assert.AreEqual(7, properties.DarkModeProfileId.Value);
        Assert.AreEqual(4, properties.LightModeProfileId.Value);
        Assert.AreEqual(string.Empty, properties.DarkModeProfile.Value);
        Assert.AreEqual(string.Empty, properties.LightModeProfile.Value);
    }

    [TestMethod]
    public void ReconcileReferences_ValidId_ClearsLegacyNameAndBecomesIdempotent()
    {
        var properties = new LightSwitchProperties();
        properties.DarkModeProfileId.Value = 7;
        properties.DarkModeProfile.Value = "Night";
        var profiles = Profiles(("Night", 7));

        Assert.IsTrue(LightSwitchProfileReferenceHelper.ReconcileReferences(properties, profiles));
        Assert.AreEqual(7, properties.DarkModeProfileId.Value);
        Assert.AreEqual(string.Empty, properties.DarkModeProfile.Value);
        Assert.IsFalse(LightSwitchProfileReferenceHelper.ReconcileReferences(properties, profiles));
    }

    [TestMethod]
    public void ReconcileReferences_StaleId_DoesNotFallBackToLegacyName()
    {
        var properties = new LightSwitchProperties();
        properties.DarkModeProfileId.Value = 99;
        properties.DarkModeProfile.Value = "Night";

        Assert.IsTrue(LightSwitchProfileReferenceHelper.ReconcileReferences(
            properties,
            Profiles(("Night", 7))));
        Assert.AreEqual(0, properties.DarkModeProfileId.Value);
        Assert.AreEqual(string.Empty, properties.DarkModeProfile.Value);
    }

    [TestMethod]
    public void ReconcileReferences_UnknownLegacyName_ClearsNameAndLeavesZeroId()
    {
        var properties = new LightSwitchProperties();
        properties.DarkModeProfile.Value = "Deleted";

        Assert.IsTrue(LightSwitchProfileReferenceHelper.ReconcileReferences(properties, Profiles()));
        Assert.AreEqual(0, properties.DarkModeProfileId.Value);
        Assert.AreEqual(string.Empty, properties.DarkModeProfile.Value);
    }

    [TestMethod]
    public void ReconcileReferences_EmptyReferences_RemainUnchanged()
    {
        var properties = new LightSwitchProperties();

        Assert.IsFalse(LightSwitchProfileReferenceHelper.ReconcileReferences(properties, Profiles()));
    }

    [TestMethod]
    public void SetProfileId_StoresIdAndClearsLegacyName()
    {
        var idProperty = new IntProperty(3);
        var legacyNameProperty = new StringProperty("Old Name");

        Assert.IsTrue(LightSwitchProfileReferenceHelper.SetProfileId(
            idProperty,
            legacyNameProperty,
            7));
        Assert.AreEqual(7, idProperty.Value);
        Assert.AreEqual(string.Empty, legacyNameProperty.Value);
    }

    [TestMethod]
    public void SetProfileId_UnchangedIdAndEmptyLegacyName_ReturnsFalse()
    {
        var idProperty = new IntProperty(7);
        var legacyNameProperty = new StringProperty(string.Empty);

        Assert.IsFalse(LightSwitchProfileReferenceHelper.SetProfileId(
            idProperty,
            legacyNameProperty,
            7));
    }

    [TestMethod]
    public void SetProfileId_NegativeId_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            LightSwitchProfileReferenceHelper.SetProfileId(
                new IntProperty(0),
                new StringProperty(string.Empty),
                -1));
    }

    [TestMethod]
    public void ClearProfileIdReferences_ClearsOnlyMatchingIds()
    {
        var properties = new LightSwitchProperties();
        properties.DarkModeProfileId.Value = 7;
        properties.LightModeProfileId.Value = 4;
        properties.DarkModeProfile.Value = "Legacy dark";
        properties.LightModeProfile.Value = "Legacy light";

        Assert.IsTrue(LightSwitchProfileReferenceHelper.ClearProfileIdReferences(properties, 7));
        Assert.AreEqual(0, properties.DarkModeProfileId.Value);
        Assert.AreEqual(4, properties.LightModeProfileId.Value);
        Assert.AreEqual("Legacy dark", properties.DarkModeProfile.Value);
        Assert.AreEqual("Legacy light", properties.LightModeProfile.Value);
    }

    [TestMethod]
    public void ClearProfileIdReferences_BothMatchingIds_ClearsBothAndKeepsLegacyNames()
    {
        var properties = new LightSwitchProperties();
        properties.DarkModeProfileId.Value = 7;
        properties.LightModeProfileId.Value = 7;
        properties.DarkModeProfile.Value = "Legacy dark";
        properties.LightModeProfile.Value = "Legacy light";

        Assert.IsTrue(LightSwitchProfileReferenceHelper.ClearProfileIdReferences(properties, 7));
        Assert.AreEqual(0, properties.DarkModeProfileId.Value);
        Assert.AreEqual(0, properties.LightModeProfileId.Value);
        Assert.AreEqual("Legacy dark", properties.DarkModeProfile.Value);
        Assert.AreEqual("Legacy light", properties.LightModeProfile.Value);
    }

    [TestMethod]
    public void ClearProfileIdReferences_NonPositiveId_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            LightSwitchProfileReferenceHelper.ClearProfileIdReferences(
                new LightSwitchProperties(),
                0));
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
