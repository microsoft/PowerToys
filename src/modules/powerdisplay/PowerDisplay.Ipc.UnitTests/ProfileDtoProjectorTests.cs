// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Contracts;
using PowerDisplay.Ipc;
using PowerDisplay.Models;

namespace PowerDisplay.Ipc.UnitTests;

[TestClass]
public class ProfileDtoProjectorTests
{
    // ─── BuildProfileListResult ──────────────────────────────────────────────
    [TestMethod]
    public void BuildProfileListResult_EmptyProfiles_ReturnsEmptyList()
    {
        var profiles = new PowerDisplayProfiles();

        var result = ProfileDtoProjector.BuildProfileListResult(profiles);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Profiles.Count);
        Assert.AreEqual("profiles", result.Command);
    }

    [TestMethod]
    public void BuildProfileListResult_ProjectsNameMonitorCountAndLastModified()
    {
        var lastModified = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        var profile = new PowerDisplayProfile
        {
            Id = 1,
            Name = "Night",
            MonitorSettings = new List<ProfileMonitorSetting>
            {
                new ProfileMonitorSetting("MON-A", brightness: 30),
                new ProfileMonitorSetting("MON-B", brightness: 40),
            },
            LastModified = lastModified,
        };

        var profiles = new PowerDisplayProfiles();
        profiles.Profiles.Add(profile);

        var result = ProfileDtoProjector.BuildProfileListResult(profiles);

        Assert.AreEqual(1, result.Profiles.Count);
        var info = result.Profiles[0];
        Assert.AreEqual("Night", info.Name);
        Assert.AreEqual(2, info.MonitorCount);

        // ISO 8601 round-trip ("o") format, invariant culture — mirrors ProfilesCommand.Run
        Assert.AreEqual(lastModified.ToString("o", System.Globalization.CultureInfo.InvariantCulture), info.LastModified);
    }

    [TestMethod]
    public void BuildProfileListResult_NullProfiles_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => ProfileDtoProjector.BuildProfileListResult(null!));
    }

    [TestMethod]
    public void BuildProfileListResult_IncludesId()
    {
        var profiles = new PowerDisplayProfiles();
        var p = new PowerDisplayProfile("Gaming", new List<ProfileMonitorSetting>
        {
            new ProfileMonitorSetting("MON1", 50, null, null, null),
        });
        p.Id = 4;
        profiles.Profiles.Add(p);

        var result = ProfileDtoProjector.BuildProfileListResult(profiles);

        Assert.AreEqual(4, result.Profiles[0].Id);
        Assert.AreEqual("Gaming", result.Profiles[0].Name);
    }
}
