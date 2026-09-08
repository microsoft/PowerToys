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
    private static readonly Guid DayId = new("4f7d8a46-9e11-43e0-bbc6-450402bb8abc");
    private static readonly Guid NightId = new("a5f24452-4389-4f76-b6dd-4434f6c9c124");
    private static readonly Guid MissingId = new("de7e65ef-92c5-480c-a263-5059d99d2409");

    [TestMethod]
    public void GetProfileIdForTheme_DisabledOrEmptyId_ReturnsNull()
    {
        var properties = new LightSwitchProperties();
        properties.EnableDarkModeProfile.Value = false;
        properties.DarkModeProfileId.Value = NightId;
        properties.EnableLightModeProfile.Value = true;

        Assert.IsNull(LightSwitchProfileReferenceHelper.GetProfileIdForTheme(properties, isLightMode: false));
        Assert.IsNull(LightSwitchProfileReferenceHelper.GetProfileIdForTheme(properties, isLightMode: true));
    }

    [TestMethod]
    public void GetProfileIdForTheme_EnabledIds_ReturnsThemeId()
    {
        var properties = new LightSwitchProperties();
        properties.EnableDarkModeProfile.Value = true;
        properties.DarkModeProfileId.Value = NightId;
        properties.EnableLightModeProfile.Value = true;
        properties.LightModeProfileId.Value = DayId;

        Assert.AreEqual(NightId, LightSwitchProfileReferenceHelper.GetProfileIdForTheme(properties, isLightMode: false));
        Assert.AreEqual(DayId, LightSwitchProfileReferenceHelper.GetProfileIdForTheme(properties, isLightMode: true));
    }

    [TestMethod]
    public void ReconcileReferences_LegacyNames_MigratesIdsAndClearsNames()
    {
        var properties = new LightSwitchProperties();
        properties.DarkModeProfile.Value = "Night";
        properties.LightModeProfile.Value = "Day";

        Assert.IsTrue(LightSwitchProfileReferenceHelper.ReconcileReferences(properties, Profiles()));
        Assert.AreEqual(NightId, properties.DarkModeProfileId.Value);
        Assert.AreEqual(DayId, properties.LightModeProfileId.Value);
        Assert.AreEqual(string.Empty, properties.DarkModeProfile.Value);
        Assert.AreEqual(string.Empty, properties.LightModeProfile.Value);
    }

    [TestMethod]
    public void ReconcileReferences_LegacyNumericIds_UsesPersistedMappingBeforeNames()
    {
        var properties = new LightSwitchProperties();
        properties.DarkModeProfileId.LegacyId = 7;
        properties.DarkModeProfile.Value = "Day";
        properties.LightModeProfileId.LegacyId = 4;

        Assert.IsTrue(LightSwitchProfileReferenceHelper.ReconcileReferences(properties, Profiles()));
        Assert.AreEqual(NightId, properties.DarkModeProfileId.Value);
        Assert.AreEqual(DayId, properties.LightModeProfileId.Value);
        Assert.IsNull(properties.DarkModeProfileId.LegacyId);
        Assert.IsNull(properties.LightModeProfileId.LegacyId);
        Assert.AreEqual(string.Empty, properties.DarkModeProfile.Value);
        Assert.IsFalse(LightSwitchProfileReferenceHelper.ReconcileReferences(properties, Profiles()));
    }

    [TestMethod]
    public void ReconcileReferences_ValidUuid_ClearsLegacyReferencesAndBecomesIdempotent()
    {
        var properties = new LightSwitchProperties();
        properties.DarkModeProfileId.Value = NightId;
        properties.DarkModeProfileId.LegacyId = 4;
        properties.DarkModeProfile.Value = "Day";

        Assert.IsTrue(LightSwitchProfileReferenceHelper.ReconcileReferences(properties, Profiles()));
        Assert.AreEqual(NightId, properties.DarkModeProfileId.Value);
        Assert.IsNull(properties.DarkModeProfileId.LegacyId);
        Assert.AreEqual(string.Empty, properties.DarkModeProfile.Value);
        Assert.IsFalse(LightSwitchProfileReferenceHelper.ReconcileReferences(properties, Profiles()));
    }

    [TestMethod]
    public void ReconcileReferences_MissingUuid_PreservesReferenceWithoutFallingBack()
    {
        var properties = new LightSwitchProperties();
        properties.DarkModeProfileId.Value = MissingId;
        properties.DarkModeProfileId.LegacyId = 7;
        properties.DarkModeProfile.Value = "Night";

        Assert.IsFalse(LightSwitchProfileReferenceHelper.ReconcileReferences(properties, Profiles()));
        Assert.AreEqual(MissingId, properties.DarkModeProfileId.Value);
        Assert.AreEqual(7, properties.DarkModeProfileId.LegacyId);
        Assert.AreEqual("Night", properties.DarkModeProfile.Value);
    }

    [TestMethod]
    public void ReconcileReferences_UnknownLegacyId_PreservesReferenceWithoutNameFallback()
    {
        var properties = new LightSwitchProperties();
        properties.DarkModeProfileId.LegacyId = 99;
        properties.DarkModeProfile.Value = "Night";

        Assert.IsFalse(LightSwitchProfileReferenceHelper.ReconcileReferences(properties, Profiles()));
        Assert.AreEqual(Guid.Empty, properties.DarkModeProfileId.Value);
        Assert.AreEqual(99, properties.DarkModeProfileId.LegacyId);
        Assert.AreEqual("Night", properties.DarkModeProfile.Value);
    }

    [TestMethod]
    public void ReconcileReferences_UnknownLegacyName_PreservesNameForRetry()
    {
        var properties = new LightSwitchProperties();
        properties.DarkModeProfile.Value = "Unavailable";

        Assert.IsFalse(LightSwitchProfileReferenceHelper.ReconcileReferences(properties, Profiles()));
        Assert.AreEqual(Guid.Empty, properties.DarkModeProfileId.Value);
        Assert.AreEqual("Unavailable", properties.DarkModeProfile.Value);
    }

    [TestMethod]
    public void ReconcileReferences_EmptyReferences_RemainUnchanged()
    {
        Assert.IsFalse(LightSwitchProfileReferenceHelper.ReconcileReferences(new LightSwitchProperties(), Profiles()));
    }

    [TestMethod]
    public void SetProfileId_StoresIdAndClearsAllLegacyReferences()
    {
        var idProperty = new ProfileIdProperty { LegacyId = 3 };
        var legacyNameProperty = new StringProperty("Old Name");

        Assert.IsTrue(LightSwitchProfileReferenceHelper.SetProfileId(idProperty, legacyNameProperty, NightId));
        Assert.AreEqual(NightId, idProperty.Value);
        Assert.IsNull(idProperty.LegacyId);
        Assert.AreEqual(string.Empty, legacyNameProperty.Value);
    }

    [TestMethod]
    public void SetProfileId_ExplicitNone_ClearsUnresolvedLegacyReferences()
    {
        var idProperty = new ProfileIdProperty { LegacyId = 7 };
        var legacyNameProperty = new StringProperty("Night");

        Assert.IsTrue(LightSwitchProfileReferenceHelper.SetProfileId(idProperty, legacyNameProperty, Guid.Empty));
        Assert.AreEqual(Guid.Empty, idProperty.Value);
        Assert.IsNull(idProperty.LegacyId);
        Assert.AreEqual(string.Empty, legacyNameProperty.Value);
    }

    [TestMethod]
    public void SetProfileId_UnchangedIdWithoutLegacyReferences_ReturnsFalse()
    {
        Assert.IsFalse(LightSwitchProfileReferenceHelper.SetProfileId(new ProfileIdProperty(NightId), new StringProperty(), NightId));
    }

    [TestMethod]
    public void ClearProfileIdReferences_ClearsOnlyMatchingIdsAndTheirFallbacks()
    {
        var properties = new LightSwitchProperties();
        properties.DarkModeProfileId.Value = NightId;
        properties.LightModeProfileId.Value = DayId;
        properties.DarkModeProfile.Value = "Legacy dark";
        properties.LightModeProfile.Value = "Legacy light";

        Assert.IsTrue(LightSwitchProfileReferenceHelper.ClearProfileIdReferences(properties, NightId));
        Assert.AreEqual(Guid.Empty, properties.DarkModeProfileId.Value);
        Assert.AreEqual(DayId, properties.LightModeProfileId.Value);
        Assert.AreEqual(string.Empty, properties.DarkModeProfile.Value);
        Assert.AreEqual("Legacy light", properties.LightModeProfile.Value);
    }

    [TestMethod]
    public void ClearProfileIdReferences_MatchingUnmigratedNumericId_ClearsIt()
    {
        var properties = new LightSwitchProperties();
        properties.DarkModeProfileId.LegacyId = 7;
        properties.DarkModeProfile.Value = "Night";
        properties.LightModeProfileId.Value = DayId;
        properties.LightModeProfileId.LegacyId = 7;

        Assert.IsTrue(LightSwitchProfileReferenceHelper.ClearProfileIdReferences(properties, NightId, legacyId: 7));
        Assert.AreEqual(Guid.Empty, properties.DarkModeProfileId.Value);
        Assert.IsNull(properties.DarkModeProfileId.LegacyId);
        Assert.AreEqual(string.Empty, properties.DarkModeProfile.Value);
        Assert.AreEqual(DayId, properties.LightModeProfileId.Value);
        Assert.AreEqual(7, properties.LightModeProfileId.LegacyId);
    }

    [TestMethod]
    public void ClearProfileIdReferences_EmptyId_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            LightSwitchProfileReferenceHelper.ClearProfileIdReferences(new LightSwitchProperties(), Guid.Empty));
    }

    private static PowerDisplayProfiles Profiles()
    {
        return new PowerDisplayProfiles
        {
            Profiles = new List<PowerDisplayProfile>
            {
                new PowerDisplayProfile("Day", new List<ProfileMonitorSetting>()) { Id = DayId, LegacyId = 4, Order = 0 },
                new PowerDisplayProfile("Night", new List<ProfileMonitorSetting>()) { Id = NightId, LegacyId = 7, Order = 1 },
            },
        };
    }
}
