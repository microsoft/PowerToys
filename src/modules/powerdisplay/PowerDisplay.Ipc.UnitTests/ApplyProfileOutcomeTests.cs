// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Models;
using PowerDisplay.Contracts;
using PowerDisplay.ViewModels;

namespace PowerDisplay.Ipc.UnitTests;

/// <summary>
/// Tests for <see cref="MainViewModel.TryRestoreWithOutcomeAsync"/>, the apply-profile outcomes
/// validator. Focused on the discrete (color-temperature) supported-set check that makes
/// apply-profile agree with the <c>set</c> command.
/// </summary>
[TestClass]
public class ApplyProfileOutcomeTests
{
    private static readonly int[] ColorPresetSet = { 0x01, 0x05 };

    private int _applyCalls;

    private Task<MonitorOperationResult> RecordingApply(string id, int value, CancellationToken ct)
    {
        _applyCalls++;
        return Task.FromResult(MonitorOperationResult.Success());
    }

    private Task<CliProfileChange?> RunColorTemp(int value, IReadOnlyList<int>? supportedValues)
        => MainViewModel.TryRestoreWithOutcomeAsync(
            savedValue: value,
            supportsHardware: true,
            settingName: CliSettingNames.ColorTemperature,
            monitorId: "MON1",
            formatDisplay: v => $"0x{v:X2}",
            applyAsync: RecordingApply,
            supportedValues: supportedValues,
            ct: CancellationToken.None);

    [TestMethod]
    public async Task ColorTemperature_ValueNotInSupportedSet_ReportsOutOfRange_AndSkipsWrite()
    {
        // A profile value the monitor does not advertise must be rejected before any hardware write,
        // matching `set` — not attempted and reported as a hardware failure.
        var outcome = await RunColorTemp(0x99, ColorPresetSet);

        Assert.IsNotNull(outcome);
        Assert.AreEqual(CliProfileChange.StatusOutOfRange, outcome!.Status);
        Assert.AreEqual(0, _applyCalls, "hardware write must not be attempted for an unsupported value");
    }

    [TestMethod]
    public async Task ColorTemperature_ValueInSupportedSet_Applies()
    {
        var outcome = await RunColorTemp(0x05, ColorPresetSet);

        Assert.IsNotNull(outcome);
        Assert.AreEqual(CliProfileChange.StatusApplied, outcome!.Status);
        Assert.AreEqual(1, _applyCalls);
    }

    [TestMethod]
    public async Task ColorTemperature_NoAdvertisedSet_AppliesWithinByteRange()
    {
        // Monitor did not advertise a set → fall back to the byte-range guard (write proceeds).
        var outcome = await RunColorTemp(0x05, supportedValues: null);

        Assert.IsNotNull(outcome);
        Assert.AreEqual(CliProfileChange.StatusApplied, outcome!.Status);
        Assert.AreEqual(1, _applyCalls);
    }

    [TestMethod]
    public async Task Continuous_OutOfRangeValue_ReportsOutOfRange_AndSkipsWrite()
    {
        var outcome = await MainViewModel.TryRestoreWithOutcomeAsync(
            savedValue: 150,
            supportsHardware: true,
            settingName: CliSettingNames.Brightness,
            monitorId: "MON1",
            formatDisplay: v => v + "%",
            applyAsync: RecordingApply,
            supportedValues: null,
            ct: CancellationToken.None);

        Assert.IsNotNull(outcome);
        Assert.AreEqual(CliProfileChange.StatusOutOfRange, outcome!.Status);
        Assert.AreEqual(0, _applyCalls);
    }
}
