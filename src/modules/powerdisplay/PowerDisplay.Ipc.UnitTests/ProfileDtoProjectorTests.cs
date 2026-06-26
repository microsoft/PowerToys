// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Contracts;
using PowerDisplay.Ipc;
using PowerDisplay.Models;
using PowerDisplay.ViewModels;

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

    // ─── BuildApplyProfileResult — exit-code aggregation ─────────────────────
    [TestMethod]
    public void BuildApplyProfileResult_AllApplied_ExitCodeOk()
    {
        var outcomes = new List<ProfileApplyOutcome>
        {
            new("MON-A", Connected: true, Changes: new[]
            {
                new CliProfileChange { Setting = "brightness", Value = 50, Display = "50%", Status = CliProfileChange.StatusApplied, Error = null },
                new CliProfileChange { Setting = "contrast", Value = 70, Display = "70%", Status = CliProfileChange.StatusApplied, Error = null },
            }),
        };

        var result = ProfileDtoProjector.BuildApplyProfileResult("Day", outcomes);

        Assert.AreEqual(CliExitCodes.Ok, result.ExitCode);
        Assert.AreEqual("Day", result.Profile);
    }

    [TestMethod]
    public void BuildApplyProfileResult_WorstOutcome_HardwareFailure_ExitCodeHardwareFailure()
    {
        // One monitor applied OK, another has a hardware failure → worst = HardwareFailure.
        var outcomes = new List<ProfileApplyOutcome>
        {
            new("MON-A", Connected: true, Changes: new[]
            {
                new CliProfileChange { Setting = "brightness", Value = 50, Display = "50%", Status = CliProfileChange.StatusApplied, Error = null },
            }),
            new("MON-B", Connected: true, Changes: new[]
            {
                new CliProfileChange { Setting = "contrast", Value = 70, Display = null, Status = CliProfileChange.StatusHardwareFailure, Error = "DDC write timed out" },
            }),
        };

        var result = ProfileDtoProjector.BuildApplyProfileResult("Night", outcomes);

        Assert.AreEqual(CliExitCodes.HardwareFailure, result.ExitCode);
    }

    [TestMethod]
    public void BuildApplyProfileResult_OutOfRange_ExitCodeOutOfRange()
    {
        var outcomes = new List<ProfileApplyOutcome>
        {
            new("MON-A", Connected: true, Changes: new[]
            {
                new CliProfileChange { Setting = "brightness", Value = 50, Display = "50%", Status = CliProfileChange.StatusApplied, Error = null },
                new CliProfileChange { Setting = "volume", Value = 150, Display = null, Status = CliProfileChange.StatusOutOfRange, Error = null },
            }),
        };

        var result = ProfileDtoProjector.BuildApplyProfileResult("Cinema", outcomes);

        Assert.AreEqual(CliExitCodes.OutOfRange, result.ExitCode);
    }

    [TestMethod]
    public void BuildApplyProfileResult_HardwareFailureDominatesOutOfRange()
    {
        // HardwareFailure must win over OutOfRange regardless of order.
        var outcomes = new List<ProfileApplyOutcome>
        {
            new("MON-A", Connected: true, Changes: new[]
            {
                new CliProfileChange { Setting = "brightness", Value = 150, Display = null, Status = CliProfileChange.StatusOutOfRange, Error = null },
                new CliProfileChange { Setting = "contrast", Value = 70, Display = null, Status = CliProfileChange.StatusHardwareFailure, Error = "I2C error" },
            }),
        };

        var result = ProfileDtoProjector.BuildApplyProfileResult("Profile", outcomes);

        Assert.AreEqual(CliExitCodes.HardwareFailure, result.ExitCode);
    }

    [TestMethod]
    public void BuildApplyProfileResult_UnsupportedOnly_ExitCodeOk()
    {
        // "unsupported" does NOT contribute to exit-code failures (mirrors ApplyProfileCommand).
        var outcomes = new List<ProfileApplyOutcome>
        {
            new("MON-A", Connected: true, Changes: new[]
            {
                new CliProfileChange { Setting = "brightness", Value = 50, Display = null, Status = CliProfileChange.StatusUnsupported, Error = null },
                new CliProfileChange { Setting = "contrast", Value = 70, Display = null, Status = CliProfileChange.StatusUnsupported, Error = null },
            }),
        };

        var result = ProfileDtoProjector.BuildApplyProfileResult("Profile", outcomes);

        Assert.AreEqual(CliExitCodes.Ok, result.ExitCode);
    }

    // ─── BuildApplyProfileResult — unconnected monitor ────────────────────────
    [TestMethod]
    public void BuildApplyProfileResult_UnconnectedMonitor_ConnectedFalseNoChanges()
    {
        var outcomes = new List<ProfileApplyOutcome>
        {
            new("MON-OFFLINE", Connected: false, Changes: Array.Empty<CliProfileChange>()),
        };

        var result = ProfileDtoProjector.BuildApplyProfileResult("Profile", outcomes);

        Assert.AreEqual(CliExitCodes.Ok, result.ExitCode);
        Assert.AreEqual(1, result.Monitors.Count);

        var mon = result.Monitors[0];
        Assert.IsFalse(mon.Connected);
        Assert.AreEqual("MON-OFFLINE", mon.Monitor.Id);
        Assert.AreEqual(0, mon.Changes.Count);
    }

    [TestMethod]
    public void BuildApplyProfileResult_MixedConnectedUnconnected_CorrectOutcomes()
    {
        var outcomes = new List<ProfileApplyOutcome>
        {
            new("MON-A",       Connected: true,  Changes: new[] { new CliProfileChange { Setting = "brightness", Value = 50, Display = "50%", Status = CliProfileChange.StatusApplied, Error = null } }),
            new("MON-OFFLINE", Connected: false, Changes: Array.Empty<CliProfileChange>()),
        };

        var result = ProfileDtoProjector.BuildApplyProfileResult("Profile", outcomes);

        Assert.AreEqual(CliExitCodes.Ok, result.ExitCode);
        Assert.AreEqual(2, result.Monitors.Count);
        Assert.IsTrue(result.Monitors[0].Connected);
        Assert.IsFalse(result.Monitors[1].Connected);
    }

    [TestMethod]
    public void BuildApplyProfileResult_NullOutcomes_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => ProfileDtoProjector.BuildApplyProfileResult("Profile", null!));
    }

    // ─── BuildApplyProfileResult — DTO field correctness ────────────────────

    /// <summary>
    /// The projector copies each <see cref="CliProfileChange"/> row verbatim (it only reads Status
    /// for the worst-outcome exit code), so one representative outcome pins the full per-row field
    /// pass-through: Setting, Status, Value, Display (populated and null), and Error (null and
    /// populated). Per-status exit-code behavior is covered by the exit-code tests above.
    /// </summary>
    [TestMethod]
    public void BuildApplyProfileResult_ChangeRowsCarryAllFieldsVerbatim()
    {
        const string errorMsg = "DDC SetVCP returned error 0x8";
        var outcomes = new List<ProfileApplyOutcome>
        {
            new("MON-A", Connected: true, Changes: new[]
            {
                new CliProfileChange { Setting = "brightness", Value = 75, Display = "75%", Status = CliProfileChange.StatusApplied, Error = null },
                new CliProfileChange { Setting = "contrast", Value = 60, Display = null, Status = CliProfileChange.StatusHardwareFailure, Error = errorMsg },
            }),
        };

        var result = ProfileDtoProjector.BuildApplyProfileResult("Profile", outcomes);

        var changes = result.Monitors[0].Changes;
        Assert.AreEqual(2, changes.Count);

        // Applied row: Value + Display carried, Error null.
        Assert.AreEqual("brightness", changes[0].Setting);
        Assert.AreEqual(CliProfileChange.StatusApplied, changes[0].Status);
        Assert.AreEqual(75, changes[0].Value);
        Assert.AreEqual("75%", changes[0].Display);
        Assert.IsNull(changes[0].Error);

        // Hardware-failure row: Value + Error carried, Display null.
        Assert.AreEqual("contrast", changes[1].Setting);
        Assert.AreEqual(CliProfileChange.StatusHardwareFailure, changes[1].Status);
        Assert.AreEqual(60, changes[1].Value);
        Assert.IsNull(changes[1].Display);
        Assert.AreEqual(errorMsg, changes[1].Error);
    }
}
