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
                new ProfileChangeOutcome("brightness", 50, "50%",  CliProfileChange.StatusApplied,     null),
                new ProfileChangeOutcome("contrast",   70, "70%",  CliProfileChange.StatusApplied,     null),
            }),
        };

        var result = ProfileDtoProjector.BuildApplyProfileResult("Day", outcomes);

        Assert.AreEqual(CliExitCodes.Ok, result.ExitCode);
        Assert.IsTrue(result.Ok);
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
                new ProfileChangeOutcome("brightness", 50, "50%", CliProfileChange.StatusApplied,        null),
            }),
            new("MON-B", Connected: true, Changes: new[]
            {
                new ProfileChangeOutcome("contrast",   70, null,  CliProfileChange.StatusHardwareFailure, "DDC write timed out"),
            }),
        };

        var result = ProfileDtoProjector.BuildApplyProfileResult("Night", outcomes);

        Assert.AreEqual(CliExitCodes.HardwareFailure, result.ExitCode);
        Assert.IsFalse(result.Ok);
    }

    [TestMethod]
    public void BuildApplyProfileResult_OutOfRange_ExitCodeOutOfRange()
    {
        var outcomes = new List<ProfileApplyOutcome>
        {
            new("MON-A", Connected: true, Changes: new[]
            {
                new ProfileChangeOutcome("brightness", 50,  "50%", CliProfileChange.StatusApplied,    null),
                new ProfileChangeOutcome("volume",     150, null,  CliProfileChange.StatusOutOfRange,  null),
            }),
        };

        var result = ProfileDtoProjector.BuildApplyProfileResult("Cinema", outcomes);

        Assert.AreEqual(CliExitCodes.OutOfRange, result.ExitCode);
        Assert.IsFalse(result.Ok);
    }

    [TestMethod]
    public void BuildApplyProfileResult_HardwareFailureDominatesOutOfRange()
    {
        // HardwareFailure must win over OutOfRange regardless of order.
        var outcomes = new List<ProfileApplyOutcome>
        {
            new("MON-A", Connected: true, Changes: new[]
            {
                new ProfileChangeOutcome("brightness", 150, null,  CliProfileChange.StatusOutOfRange,      null),
                new ProfileChangeOutcome("contrast",   70,  null,  CliProfileChange.StatusHardwareFailure, "I2C error"),
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
                new ProfileChangeOutcome("brightness",       50, null, CliProfileChange.StatusUnsupported, null),
                new ProfileChangeOutcome("contrast",         70, null, CliProfileChange.StatusUnsupported, null),
            }),
        };

        var result = ProfileDtoProjector.BuildApplyProfileResult("Profile", outcomes);

        Assert.AreEqual(CliExitCodes.Ok, result.ExitCode);
        Assert.IsTrue(result.Ok);
    }

    // ─── BuildApplyProfileResult — unconnected monitor ────────────────────────
    [TestMethod]
    public void BuildApplyProfileResult_UnconnectedMonitor_ConnectedFalseNoChanges()
    {
        var outcomes = new List<ProfileApplyOutcome>
        {
            new("MON-OFFLINE", Connected: false, Changes: Array.Empty<ProfileChangeOutcome>()),
        };

        var result = ProfileDtoProjector.BuildApplyProfileResult("Profile", outcomes);

        Assert.AreEqual(CliExitCodes.Ok, result.ExitCode);
        Assert.IsTrue(result.Ok);
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
            new("MON-A",       Connected: true,  Changes: new[] { new ProfileChangeOutcome("brightness", 50, "50%", CliProfileChange.StatusApplied, null) }),
            new("MON-OFFLINE", Connected: false, Changes: Array.Empty<ProfileChangeOutcome>()),
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
    [TestMethod]
    public void BuildApplyProfileResult_ChangeRowsCarrySettingAndStatus()
    {
        var outcomes = new List<ProfileApplyOutcome>
        {
            new("MON-A", Connected: true, Changes: new[]
            {
                new ProfileChangeOutcome("brightness",        50, "50%", CliProfileChange.StatusApplied,     null),
                new ProfileChangeOutcome("color-temperature", 5,  null,  CliProfileChange.StatusUnsupported,  null),
            }),
        };

        var result = ProfileDtoProjector.BuildApplyProfileResult("Profile", outcomes);

        var changes = result.Monitors[0].Changes;
        Assert.AreEqual(2, changes.Count);
        Assert.AreEqual("brightness",        changes[0].Setting);
        Assert.AreEqual(CliProfileChange.StatusApplied,     changes[0].Status);
        Assert.AreEqual("color-temperature", changes[1].Setting);
        Assert.AreEqual(CliProfileChange.StatusUnsupported, changes[1].Status);
    }

    /// <summary>
    /// Verifies that Value, Display, and Error are populated on the CliProfileChange
    /// DTO — these were always 0/null before the review fix.
    /// </summary>
    [TestMethod]
    public void BuildApplyProfileResult_Applied_ValueAndDisplayPopulated_ErrorNull()
    {
        var outcomes = new List<ProfileApplyOutcome>
        {
            new("MON-A", Connected: true, Changes: new[]
            {
                new ProfileChangeOutcome("brightness", 75, "75%", CliProfileChange.StatusApplied, null),
            }),
        };

        var result = ProfileDtoProjector.BuildApplyProfileResult("Profile", outcomes);

        var change = result.Monitors[0].Changes[0];
        Assert.AreEqual(75,    change.Value);
        Assert.AreEqual("75%", change.Display);
        Assert.IsNull(change.Error);
        Assert.AreEqual(CliProfileChange.StatusApplied, change.Status);
    }

    /// <summary>
    /// Verifies that Value and Error are populated and Display is null on
    /// hardware-failure — mirrors ApplyProfileCommand.ApplyContinuousAsync behavior.
    /// </summary>
    [TestMethod]
    public void BuildApplyProfileResult_HardwareFailure_ValueAndErrorPopulated_DisplayNull()
    {
        const string errorMsg = "DDC SetVCP returned error 0x8";
        var outcomes = new List<ProfileApplyOutcome>
        {
            new("MON-A", Connected: true, Changes: new[]
            {
                new ProfileChangeOutcome("contrast", 60, null, CliProfileChange.StatusHardwareFailure, errorMsg),
            }),
        };

        var result = ProfileDtoProjector.BuildApplyProfileResult("Profile", outcomes);

        var change = result.Monitors[0].Changes[0];
        Assert.AreEqual(60,       change.Value);
        Assert.IsNull(change.Display);
        Assert.AreEqual(errorMsg, change.Error);
        Assert.AreEqual(CliProfileChange.StatusHardwareFailure, change.Status);
    }

    /// <summary>
    /// Verifies that Value is populated and Display/Error are null on
    /// out-of-range — mirrors ApplyProfileCommand behavior.
    /// </summary>
    [TestMethod]
    public void BuildApplyProfileResult_OutOfRange_ValuePopulated_DisplayAndErrorNull()
    {
        var outcomes = new List<ProfileApplyOutcome>
        {
            new("MON-A", Connected: true, Changes: new[]
            {
                new ProfileChangeOutcome("volume", 150, null, CliProfileChange.StatusOutOfRange, null),
            }),
        };

        var result = ProfileDtoProjector.BuildApplyProfileResult("Profile", outcomes);

        var change = result.Monitors[0].Changes[0];
        Assert.AreEqual(150, change.Value);
        Assert.IsNull(change.Display);
        Assert.IsNull(change.Error);
        Assert.AreEqual(CliProfileChange.StatusOutOfRange, change.Status);
    }

    /// <summary>
    /// Verifies that Value is populated and Display/Error are null on
    /// unsupported — mirrors ApplyProfileCommand behavior.
    /// </summary>
    [TestMethod]
    public void BuildApplyProfileResult_Unsupported_ValuePopulated_DisplayAndErrorNull()
    {
        var outcomes = new List<ProfileApplyOutcome>
        {
            new("MON-A", Connected: true, Changes: new[]
            {
                new ProfileChangeOutcome("brightness", 40, null, CliProfileChange.StatusUnsupported, null),
            }),
        };

        var result = ProfileDtoProjector.BuildApplyProfileResult("Profile", outcomes);

        var change = result.Monitors[0].Changes[0];
        Assert.AreEqual(40, change.Value);
        Assert.IsNull(change.Display);
        Assert.IsNull(change.Error);
        Assert.AreEqual(CliProfileChange.StatusUnsupported, change.Status);
    }

    /// <summary>
    /// Verifies that color-temperature applied display uses
    /// MonitorDtoProjector.FormatDiscrete(0x14, value) format (e.g. "6500K (0x05)").
    /// The projector simply passes through whatever Display string is set in ProfileChangeOutcome,
    /// so this test confirms the contract is carried end-to-end.
    /// </summary>
    [TestMethod]
    public void BuildApplyProfileResult_ColorTemperatureApplied_DisplayIsFormatDiscrete()
    {
        // FormatDiscrete(0x14, 0x05) → "6500K (0x05)" per MonitorDtoProjectorTests.FormatDiscrete_KnownValue
        var outcomes = new List<ProfileApplyOutcome>
        {
            new("MON-A", Connected: true, Changes: new[]
            {
                new ProfileChangeOutcome("color-temperature", 0x05, "6500K (0x05)", CliProfileChange.StatusApplied, null),
            }),
        };

        var result = ProfileDtoProjector.BuildApplyProfileResult("Profile", outcomes);

        var change = result.Monitors[0].Changes[0];
        Assert.AreEqual(0x05,           change.Value);
        Assert.AreEqual("6500K (0x05)", change.Display);
        Assert.IsNull(change.Error);
    }

    // ─── not-found signal (for Task 2.5 / IPC handler) ───────────────────────
    /// <summary>
    /// Documents the contract: ApplyProfileWithOutcomesAsync returns null when the
    /// profile name is unknown. BuildApplyProfileResult must NOT be called with null — the IPC
    /// handler (Task 2.5) maps null to CliErrorResult(ArgumentError/exit 7).
    /// This test verifies BuildApplyProfileResult still throws ArgumentNullException if null is
    /// passed (i.e., the projector does NOT silently accept null as empty).
    /// </summary>
    [TestMethod]
    public void BuildApplyProfileResult_NullOutcomes_ThrowsArgumentNullException_NotFoundSignal()
    {
        // The IPC handler must check for null BEFORE calling BuildApplyProfileResult.
        // If it accidentally passes null here, ArgumentNullException is the signal.
        Assert.ThrowsException<ArgumentNullException>(
            () => ProfileDtoProjector.BuildApplyProfileResult("UnknownProfile", null!));
    }
}
