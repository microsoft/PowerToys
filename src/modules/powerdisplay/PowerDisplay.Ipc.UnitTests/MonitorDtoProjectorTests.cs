// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Models;
using PowerDisplay.Contracts;
using PowerDisplay.Ipc;
using PowerDisplay.Models;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Ipc.UnitTests;

[TestClass]
public class MonitorDtoProjectorTests
{
    // ─── Helpers ──────────────────────────────────────────────────────────────
    private static readonly IReadOnlySet<string> EmptyHidden =
        new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates a minimal monitor with brightness support and a GDI device name.</summary>
    private static Monitor MakeMon(int number, string id, string name = "TestMon", string gdi = @"\\.\DISPLAY1")
        => new()
        {
            MonitorNumber = number,
            Id = id,
            Name = name,
            CommunicationMethod = "DDC/CI",
            GdiDeviceName = gdi,
            Capabilities = MonitorCapabilities.Brightness,
            ReadValues = MonitorReadFlags.Brightness | MonitorReadFlags.Orientation,
            CurrentBrightness = 42,
        };

    // ─── ExcludeHidden ────────────────────────────────────────────────────────
    [TestMethod]
    public void BuildListResult_ExcludesHiddenMonitors()
    {
        var monitors = new List<Monitor>
        {
            new() { Id = "A", MonitorNumber = 1, Name = "Mon A", Capabilities = MonitorCapabilities.Brightness },
            new() { Id = "B", MonitorNumber = 2, Name = "Mon B", Capabilities = MonitorCapabilities.Brightness },
        };
        var hidden = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "B" };

        var result = MonitorDtoProjector.BuildListResult(monitors, hidden);

        Assert.AreEqual(1, result.Monitors.Count);
        Assert.AreEqual("A", result.Monitors[0].Id);
    }

    [TestMethod]
    public void BuildListResult_AllHidden_ReturnsEmptyList()
    {
        var monitors = new List<Monitor>
        {
            new() { Id = "A", MonitorNumber = 1 },
            new() { Id = "B", MonitorNumber = 2 },
        };
        var hidden = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "A", "B" };

        var result = MonitorDtoProjector.BuildListResult(monitors, hidden);

        Assert.AreEqual(0, result.Monitors.Count);
    }

    [TestMethod]
    public void BuildListResult_NoneHidden_ReturnsAll()
    {
        var monitors = new List<Monitor>
        {
            MakeMon(1, "A"),
            MakeMon(2, "B"),
        };

        var result = MonitorDtoProjector.BuildListResult(monitors, EmptyHidden);

        Assert.AreEqual(2, result.Monitors.Count);
    }

    // ─── List entry projection ────────────────────────────────────────────────
    [TestMethod]
    public void BuildListResult_EntryCopiesMonitorFields()
    {
        var monitor = new Monitor
        {
            MonitorNumber = 3,
            Id = "MON-3",
            Name = "Dell U2722D",
            CommunicationMethod = "DDC/CI",
            GdiDeviceName = @"\\.\DISPLAY3",
            Capabilities = MonitorCapabilities.Brightness | MonitorCapabilities.Contrast,
        };

        var result = MonitorDtoProjector.BuildListResult(new List<Monitor> { monitor }, EmptyHidden);
        var entry = result.Monitors[0];

        Assert.AreEqual(3, entry.Number);
        Assert.AreEqual("MON-3", entry.Id);
        Assert.AreEqual("Dell U2722D", entry.Name);
        Assert.AreEqual("DDC/CI", entry.Method);
    }

    // ─── BuildGetResult — no selector path ───────────────────────────────────
    [TestMethod]
    public void BuildGetResult_NoSelector_ReturnsAllVisibleMonitors()
    {
        var monitors = new List<Monitor> { MakeMon(1, "A"), MakeMon(2, "B") };

        var (result, error) = MonitorDtoProjector.BuildGetResult(monitors, EmptyHidden, number: null, id: null, settingFilter: null);

        Assert.IsNull(error);
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result!.Monitors.Count);
    }

    [TestMethod]
    public void BuildGetResult_NoSelector_UnknownSetting_YieldsArgumentError()
    {
        var monitors = new List<Monitor> { MakeMon(1, "A") };

        var (result, error) = MonitorDtoProjector.BuildGetResult(monitors, EmptyHidden, number: null, id: null, settingFilter: "bogus");

        Assert.IsNull(result);
        Assert.IsNotNull(error);
        Assert.AreEqual(CliErrorCodes.ArgumentError, error!.Error.Code);
        Assert.AreEqual(CliExitCodes.ArgumentError, error.Error.ExitCode);
        Assert.IsNull(error.Monitor);
    }

    [TestMethod]
    public void BuildGetResult_NoSelector_TrulyUnknownSetting_MessageContainsOriginalCase()
    {
        var monitors = new List<Monitor> { MakeMon(1, "A") };

        var (_, error) = MonitorDtoProjector.BuildGetResult(monitors, EmptyHidden, number: null, id: null, settingFilter: "BRIGHTNESSS");

        Assert.IsNotNull(error);
        Assert.AreEqual(CliMessageIds.UnknownSetting, error!.Error.MessageId);
        Assert.AreEqual("BRIGHTNESSS", error.Error.Value);
    }

    // ─── BuildGetResult — selected path ──────────────────────────────────────
    [TestMethod]
    public void BuildGetResult_UnknownMonitorNumber_YieldsMonitorNotFound()
    {
        var monitors = new List<Monitor> { MakeMon(1, "A") };

        var (result, error) = MonitorDtoProjector.BuildGetResult(monitors, EmptyHidden, number: 9, id: null, settingFilter: null);

        Assert.IsNull(result);
        Assert.IsNotNull(error);
        Assert.AreEqual(CliErrorCodes.MonitorNotFound, error!.Error.Code);
        Assert.AreEqual(CliExitCodes.MonitorNotFound, error.Error.ExitCode);
    }

    [TestMethod]
    public void BuildGetResult_UnknownMonitorId_YieldsMonitorNotFound()
    {
        var monitors = new List<Monitor> { MakeMon(1, "A") };

        var (result, error) = MonitorDtoProjector.BuildGetResult(monitors, EmptyHidden, number: null, id: "Z", settingFilter: null);

        Assert.IsNull(result);
        Assert.IsNotNull(error);
        Assert.AreEqual(CliErrorCodes.MonitorNotFound, error!.Error.Code);
    }

    [TestMethod]
    public void BuildGetResult_ByNumber_ReturnsOneEntryForThatMonitor()
    {
        var monitors = new List<Monitor> { MakeMon(1, "A"), MakeMon(2, "B") };

        var (result, error) = MonitorDtoProjector.BuildGetResult(monitors, EmptyHidden, number: 2, id: null, settingFilter: null);

        Assert.IsNull(error);
        Assert.AreEqual(1, result!.Monitors.Count);
        Assert.AreEqual(2, result.Monitors[0].Monitor.Number);
    }

    [TestMethod]
    public void BuildGetResult_ById_ReturnsOneEntryForThatMonitor()
    {
        var monitors = new List<Monitor> { MakeMon(1, "A"), MakeMon(2, "B") };

        var (result, error) = MonitorDtoProjector.BuildGetResult(monitors, EmptyHidden, number: null, id: "B", settingFilter: null);

        Assert.IsNull(error);
        Assert.AreEqual(1, result!.Monitors.Count);
        Assert.AreEqual("B", result.Monitors[0].Monitor.Id);
    }

    [TestMethod]
    public void BuildGetResult_HiddenMonitorTargeted_ReturnsMonitorNotFound()
    {
        var monitors = new List<Monitor> { MakeMon(1, "A") };
        var hidden = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "A" };

        var (result, error) = MonitorDtoProjector.BuildGetResult(monitors, hidden, number: 1, id: null, settingFilter: null);

        Assert.IsNull(result);
        Assert.AreEqual(CliExitCodes.MonitorNotFound, error!.Error.ExitCode);
    }

    [TestMethod]
    public void BuildGetResult_BothSelectors_IdWins()
    {
        var monitors = new List<Monitor> { MakeMon(1, "A"), MakeMon(2, "B") };

        var (result, error) = MonitorDtoProjector.BuildGetResult(monitors, EmptyHidden, number: 1, id: "B", settingFilter: null);

        Assert.IsNull(error);
        Assert.AreEqual("B", result!.Monitors[0].Monitor.Id);
    }

    // ─── BuildGetResult — setting projection ─────────────────────────────────
    [TestMethod]
    public void BuildGetResult_AllSettingsPresent_CountMatchesAllSettingNames()
    {
        var monitors = new List<Monitor> { MakeMon(1, "A") };

        var (result, _) = MonitorDtoProjector.BuildGetResult(monitors, EmptyHidden, number: null, id: null, settingFilter: null);

        Assert.AreEqual(CliSettingNames.All.Length, result!.Monitors[0].Settings.Count);
    }

    [TestMethod]
    public void BuildGetResult_BrightnessSupported_DisplayIsPercentageString()
    {
        var monitor = MakeMon(1, "A");
        monitor.CurrentBrightness = 75;
        var monitors = new List<Monitor> { monitor };

        var (result, _) = MonitorDtoProjector.BuildGetResult(monitors, EmptyHidden, number: null, id: null, settingFilter: "brightness");

        var setting = result!.Monitors[0].Settings[0];
        Assert.AreEqual("brightness", setting.Setting);
        Assert.IsTrue(setting.Supported);
        Assert.AreEqual("75%", setting.Display);
    }

    [TestMethod]
    public void BuildGetResult_SupportedButUnread_OmitsDisplay()
    {
        var monitor = new Monitor
        {
            MonitorNumber = 1,
            Id = "A",
            Capabilities = MonitorCapabilities.Brightness,
            ReadValues = MonitorReadFlags.None, // supported but not read
            CurrentBrightness = 50,
        };
        var monitors = new List<Monitor> { monitor };

        var (result, _) = MonitorDtoProjector.BuildGetResult(monitors, EmptyHidden, number: null, id: null, settingFilter: "brightness");

        var setting = result!.Monitors[0].Settings[0];
        Assert.IsTrue(setting.Supported);
        Assert.IsNull(setting.Display);
    }

    [TestMethod]
    public void BuildGetResult_UnsupportedSetting_SupportedFalseAndNullValue()
    {
        // Contrast is not in MonitorCapabilities.Brightness
        var monitor = new Monitor
        {
            MonitorNumber = 1,
            Id = "A",
            Capabilities = MonitorCapabilities.Brightness,
            ReadValues = MonitorReadFlags.Brightness,
        };
        var monitors = new List<Monitor> { monitor };

        var (result, _) = MonitorDtoProjector.BuildGetResult(monitors, EmptyHidden, number: null, id: null, settingFilter: "contrast");

        var setting = result!.Monitors[0].Settings[0];
        Assert.IsFalse(setting.Supported);
        Assert.IsNull(setting.Display);
    }

    [TestMethod]
    public void BuildGetResult_OrientationDisplay_IsDegreesNotIndex()
    {
        var monitor = MakeMon(1, "A");
        monitor.Orientation = 1; // index 1 = 90 degrees
        var monitors = new List<Monitor> { monitor };

        var (result, _) = MonitorDtoProjector.BuildGetResult(monitors, EmptyHidden, number: null, id: null, settingFilter: "orientation");

        var setting = result!.Monitors[0].Settings[0];
        Assert.AreEqual("orientation", setting.Setting);
        Assert.AreEqual("90°", setting.Display);
    }

    [TestMethod]
    public void BuildGetResult_SettingFilterIsCaseInsensitive()
    {
        var monitors = new List<Monitor> { MakeMon(1, "A") };

        var (result, error) = MonitorDtoProjector.BuildGetResult(monitors, EmptyHidden, number: null, id: null, settingFilter: "Brightness");

        Assert.IsNull(error);
        Assert.AreEqual(1, result!.Monitors[0].Settings.Count);
        Assert.AreEqual("brightness", result.Monitors[0].Settings[0].Setting);
    }

    // ─── BuildCapabilitiesResult ──────────────────────────────────────────────
    [TestMethod]
    public void BuildCapabilitiesResult_NoSelector_YieldsSelectorMissing()
    {
        var monitors = new List<Monitor> { MakeMon(1, "A") };

        var (result, error) = MonitorDtoProjector.BuildCapabilitiesResult(monitors, EmptyHidden, number: null, id: null);

        Assert.IsNull(result);
        Assert.AreEqual(CliErrorCodes.SelectorMissing, error!.Error.Code);
        Assert.AreEqual(CliExitCodes.SelectorMissing, error.Error.ExitCode);
    }

    [TestMethod]
    public void BuildCapabilitiesResult_UnknownMonitorNumber_YieldsMonitorNotFound()
    {
        var monitors = new List<Monitor> { MakeMon(1, "A") };

        var (result, error) = MonitorDtoProjector.BuildCapabilitiesResult(monitors, EmptyHidden, number: 9, id: null);

        Assert.IsNull(result);
        Assert.AreEqual(CliErrorCodes.MonitorNotFound, error!.Error.Code);
    }

    [TestMethod]
    public void BuildCapabilitiesResult_NoVcpCaps_ReturnsEmptyVcpCodes()
    {
        var monitor = MakeMon(1, "A");
        monitor.VcpCapabilitiesInfo = null;
        var monitors = new List<Monitor> { monitor };

        var (result, error) = MonitorDtoProjector.BuildCapabilitiesResult(monitors, EmptyHidden, number: 1, id: null);

        Assert.IsNull(error);
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result!.VcpCodes.Count);
    }

    [TestMethod]
    public void BuildCapabilitiesResult_MethodGoesInTopLevel_NotInMonitorRef()
    {
        var monitor = MakeMon(1, "A");
        monitor.CommunicationMethod = "DDC/CI";
        var monitors = new List<Monitor> { monitor };

        var (result, _) = MonitorDtoProjector.BuildCapabilitiesResult(monitors, EmptyHidden, number: 1, id: null);

        Assert.AreEqual("DDC/CI", result!.CommunicationMethod);
        Assert.IsNull(result.Monitor.Method, "Method should be null on the monitor ref for capabilities");
    }

    [TestMethod]
    public void BuildCapabilitiesResult_WithVcpCaps_ProjectsCodesAndFormatsDiscrete()
    {
        var monitor = MakeMon(1, "A");
        var caps = new VcpCapabilities();

        // Add brightness (continuous) and color-temperature (discrete with known values)
        caps.SupportedVcpCodes[0x10] = new VcpCodeInfo(0x10, "Brightness");
        caps.SupportedVcpCodes[0x14] = new VcpCodeInfo(0x14, "Select Color Preset", new List<int> { 0x05 });
        monitor.VcpCapabilitiesInfo = caps;
        var monitors = new List<Monitor> { monitor };

        var (result, error) = MonitorDtoProjector.BuildCapabilitiesResult(monitors, EmptyHidden, number: 1, id: null);

        Assert.IsNull(error);
        Assert.AreEqual(2, result!.VcpCodes.Count);

        var brightness = result.VcpCodes[0]; // sorted: 0x10 comes before 0x14
        Assert.AreEqual("0x10", brightness.Code);
        Assert.IsTrue(brightness.Continuous);
        Assert.IsNull(brightness.DiscreteValues);

        var colorTemp = result.VcpCodes[1];
        Assert.AreEqual("0x14", colorTemp.Code);
        Assert.IsFalse(colorTemp.Continuous);
        Assert.IsNotNull(colorTemp.DiscreteValues);
        Assert.AreEqual(1, colorTemp.DiscreteValues!.Count);

        // FormatDiscrete(0x14, 0x05) → "6500K (0x05)"
        Assert.AreEqual("6500K (0x05)", colorTemp.DiscreteValues[0]);
    }

    [TestMethod]
    public void BuildCapabilitiesResult_SettingFilter_ReturnsOnlyMatchingCode()
    {
        var monitor = MakeMon(1, "A");
        var caps = new VcpCapabilities();
        caps.SupportedVcpCodes[0x14] = new VcpCodeInfo(0x14, "Select Color Preset", new List<int> { 0x05 });
        caps.SupportedVcpCodes[0x60] = new VcpCodeInfo(0x60, "Input Source", new List<int> { 0x11 });
        monitor.VcpCapabilitiesInfo = caps;
        var monitors = new List<Monitor> { monitor };

        var (result, error) = MonitorDtoProjector.BuildCapabilitiesResult(
            monitors, EmptyHidden, number: 1, id: null, settingFilter: "input-source", customMappings: null);

        Assert.IsNull(error);
        Assert.AreEqual(1, result!.VcpCodes.Count);
        Assert.AreEqual("0x60", result.VcpCodes[0].Code);
    }

    [TestMethod]
    public void BuildCapabilitiesResult_SettingFilter_NonDiscrete_ReturnsArgumentError()
    {
        var monitor = MakeMon(1, "A");
        var caps = new VcpCapabilities();
        caps.SupportedVcpCodes[0x10] = new VcpCodeInfo(0x10, "Brightness");
        monitor.VcpCapabilitiesInfo = caps;
        var monitors = new List<Monitor> { monitor };

        var (result, error) = MonitorDtoProjector.BuildCapabilitiesResult(
            monitors, EmptyHidden, number: 1, id: null, settingFilter: "brightness", customMappings: null);

        Assert.IsNull(result);
        Assert.IsNotNull(error);
        Assert.AreEqual(CliErrorCodes.ArgumentError, error!.Error.Code);
    }

    [TestMethod]
    public void BuildCapabilitiesResult_CustomMapping_UsesCustomName()
    {
        var monitor = MakeMon(1, "A");
        var caps = new VcpCapabilities();
        caps.SupportedVcpCodes[0x60] = new VcpCodeInfo(0x60, "Input Source", new List<int> { 0x11 });
        monitor.VcpCapabilitiesInfo = caps;
        var monitors = new List<Monitor> { monitor };
        var custom = new List<CustomVcpValueMapping>
        {
            new() { VcpCode = 0x60, Value = 0x11, CustomName = "Living Room TV", ApplyToAll = true },
        };

        var (result, error) = MonitorDtoProjector.BuildCapabilitiesResult(
            monitors, EmptyHidden, number: 1, id: null, settingFilter: null, customMappings: custom);

        Assert.IsNull(error);
        var inputCode = result!.VcpCodes[0];
        Assert.AreEqual("0x60", inputCode.Code);
        Assert.IsNotNull(inputCode.DiscreteValues);
        Assert.AreEqual("Living Room TV (0x11)", inputCode.DiscreteValues![0]);
    }

    [TestMethod]
    public void BuildGetResult_CustomMapping_UsesCustomName()
    {
        var monitor = MakeMon(1, "A");
        var caps = new VcpCapabilities();
        caps.SupportedVcpCodes[0x60] = new VcpCodeInfo(0x60, "Input Source", new List<int> { 0x11 });
        monitor.VcpCapabilitiesInfo = caps;
        monitor.ReadValues |= MonitorReadFlags.InputSource;
        monitor.CurrentInputSource = 0x11;
        var monitors = new List<Monitor> { monitor };
        var custom = new List<CustomVcpValueMapping>
        {
            new() { VcpCode = 0x60, Value = 0x11, CustomName = "Living Room TV", ApplyToAll = true },
        };

        var (result, error) = MonitorDtoProjector.BuildGetResult(
            monitors, EmptyHidden, number: null, id: null, settingFilter: "input-source", customMappings: custom);

        Assert.IsNull(error);
        var setting = result!.Monitors[0].Settings[0];
        Assert.AreEqual("input-source", setting.Setting);
        Assert.AreEqual("Living Room TV (0x11)", setting.Display);
    }

    // ─── ResolveMonitor ───────────────────────────────────────────────────────
    [TestMethod]
    public void ResolveMonitor_NoSelector_ReturnsSelectorMissing()
    {
        var monitors = new List<Monitor> { MakeMon(1, "A") };

        var (monitor, error) = MonitorDtoProjector.ResolveMonitor(monitors, null, null);

        Assert.IsNull(monitor);
        Assert.AreEqual(CliErrorCodes.SelectorMissing, error!.Code);
    }

    [TestMethod]
    public void ResolveMonitor_ByNumber_FindsCorrectMonitor()
    {
        var monitors = new List<Monitor> { MakeMon(1, "A"), MakeMon(2, "B") };

        var (monitor, error) = MonitorDtoProjector.ResolveMonitor(monitors, 2, null);

        Assert.IsNull(error);
        Assert.AreEqual("B", monitor!.Id);
    }

    [TestMethod]
    public void ResolveMonitor_ById_FindsCorrectMonitor()
    {
        var monitors = new List<Monitor> { MakeMon(1, "A"), MakeMon(2, "B") };

        var (monitor, error) = MonitorDtoProjector.ResolveMonitor(monitors, null, "B");

        Assert.IsNull(error);
        Assert.AreEqual("B", monitor!.Id);
    }

    [TestMethod]
    public void ResolveMonitor_BothSelectors_IdWins()
    {
        var monitors = new List<Monitor> { MakeMon(1, "A"), MakeMon(2, "B") };

        var (monitor, error) = MonitorDtoProjector.ResolveMonitor(monitors, 1, "B");

        Assert.IsNull(error);
        Assert.AreEqual("B", monitor!.Id);
    }

    [TestMethod]
    public void ResolveMonitor_BothSelectors_IdNotFound_ReturnsError()
    {
        var monitors = new List<Monitor> { MakeMon(1, "A") };

        var (monitor, error) = MonitorDtoProjector.ResolveMonitor(monitors, 1, "Z");

        Assert.IsNull(monitor);
        Assert.AreEqual(CliErrorCodes.MonitorNotFound, error!.Code);
    }

    // ─── FormatDiscrete / OrientationDegrees ─────────────────────────────────
    [TestMethod]
    public void FormatDiscrete_KnownValue_ReturnsNameAndHex()
    {
        // 0x14:0x05 = "6500K"
        var s = MonitorDtoProjector.FormatDiscrete(0x14, 0x05);
        Assert.AreEqual("6500K (0x05)", s);
    }

    [TestMethod]
    public void FormatDiscrete_UnknownValue_ReturnsHexOnly()
    {
        var s = MonitorDtoProjector.FormatDiscrete(0x14, 0xFF);
        Assert.AreEqual("0xFF", s);
    }

    [TestMethod]
    public void OrientationDegrees_Index0_Returns0Degrees()
        => Assert.AreEqual("0°", MonitorDtoProjector.OrientationDegrees(0));

    [TestMethod]
    public void OrientationDegrees_Index1_Returns90Degrees()
        => Assert.AreEqual("90°", MonitorDtoProjector.OrientationDegrees(1));

    [TestMethod]
    public void OrientationDegrees_Index2_Returns180Degrees()
        => Assert.AreEqual("180°", MonitorDtoProjector.OrientationDegrees(2));

    [TestMethod]
    public void OrientationDegrees_Index3_Returns270Degrees()
        => Assert.AreEqual("270°", MonitorDtoProjector.OrientationDegrees(3));

    [TestMethod]
    public void OrientationDegrees_UnknownIndex_ReturnsIndexLabel()
        => Assert.AreEqual("index 7", MonitorDtoProjector.OrientationDegrees(7));
}
