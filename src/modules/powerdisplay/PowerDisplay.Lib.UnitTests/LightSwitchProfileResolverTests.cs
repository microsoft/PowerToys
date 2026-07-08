// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public class LightSwitchProfileResolverTests
{
    private static PowerDisplayProfiles Profiles(params (string Name, int Id)[] items)
    {
        var profiles = new PowerDisplayProfiles();
        foreach (var (name, id) in items)
        {
            profiles.Profiles.Add(new PowerDisplayProfile(name, new List<ProfileMonitorSetting>
            {
                new ProfileMonitorSetting("MON1", 50, null, null, null),
            })
            {
                Id = id,
            });
        }

        return profiles;
    }

    [TestMethod]
    public void Resolve_ExistingPositiveId_ReturnsThatId()
    {
        var profiles = Profiles(("A", 1), ("B", 2));
        Assert.AreEqual(2, LightSwitchProfileResolver.Resolve(2, "ignored-when-id-set", profiles));
    }

    [TestMethod]
    public void Resolve_StaleId_ReturnsNull_AndDoesNotFallBackToName()
    {
        // Id 9 does not exist; a profile named "A" does. A stale id must NOT silently resolve to it.
        var profiles = Profiles(("A", 1));
        Assert.IsNull(LightSwitchProfileResolver.Resolve(9, "A", profiles));
    }

    [TestMethod]
    public void Resolve_NoId_ResolvesByName()
    {
        var profiles = Profiles(("A", 1), ("B", 2));
        Assert.AreEqual(2, LightSwitchProfileResolver.Resolve(0, "B", profiles));
    }

    [TestMethod]
    public void Resolve_NoId_NoneEmptyOrMissingName_ReturnsNull()
    {
        var profiles = Profiles(("A", 1));
        Assert.IsNull(LightSwitchProfileResolver.Resolve(0, LightSwitchProfileResolver.NoneSentinel, profiles));
        Assert.IsNull(LightSwitchProfileResolver.Resolve(0, string.Empty, profiles));
        Assert.IsNull(LightSwitchProfileResolver.Resolve(0, "missing", profiles));
    }

    [TestMethod]
    public void HasReference_TrueForIdOrRealName_FalseForNoneOrEmpty()
    {
        Assert.IsTrue(LightSwitchProfileResolver.HasReference(3, null));
        Assert.IsTrue(LightSwitchProfileResolver.HasReference(0, "Night"));
        Assert.IsFalse(LightSwitchProfileResolver.HasReference(0, string.Empty));
        Assert.IsFalse(LightSwitchProfileResolver.HasReference(0, LightSwitchProfileResolver.NoneSentinel));
    }

    [TestMethod]
    public void MigrateNamesToIds_NameResolves_StoresIdAndResyncsName_AndIsIdempotent()
    {
        var profiles = Profiles(("Day", 4), ("Night", 7));
        var props = new LightSwitchProperties();
        props.LightModeProfile.Value = "Day";
        props.DarkModeProfile.Value = "Night";

        Assert.IsTrue(LightSwitchProfileResolver.MigrateNamesToIds(props, profiles));
        Assert.AreEqual(4, props.LightModeProfileId.Value);
        Assert.AreEqual(7, props.DarkModeProfileId.Value);

        Assert.IsFalse(LightSwitchProfileResolver.MigrateNamesToIds(props, profiles)); // already migrated
    }

    [TestMethod]
    public void MigrateNamesToIds_UnknownName_ClearsToNone()
    {
        var profiles = Profiles(("Day", 4));
        var props = new LightSwitchProperties();
        props.DarkModeProfile.Value = "Deleted";

        Assert.IsTrue(LightSwitchProfileResolver.MigrateNamesToIds(props, profiles));
        Assert.AreEqual(0, props.DarkModeProfileId.Value);
        Assert.AreEqual(string.Empty, props.DarkModeProfile.Value);
    }
}
