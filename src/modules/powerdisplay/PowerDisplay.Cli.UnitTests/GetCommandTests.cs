// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Cli.Commands;
using PowerDisplay.Cli.Output;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Cli.UnitTests;

[TestClass]
public class GetCommandTests
{
    private sealed class CapturingOutput : ICliOutput
    {
        public CliGetResult? LastGetResult { get; private set; }

        public CliErrorResult? LastErrorResult { get; private set; }

        public void WriteListResult(CliListResult result)
        {
        }

        public void WriteSetResult(CliSetResult result)
        {
        }

        public void WriteGetResult(CliGetResult result) => LastGetResult = result;

        public void WriteCapabilitiesResult(CliCapabilitiesResult result)
        {
        }

        public void WriteError(CliErrorResult result) => LastErrorResult = result;

        public void WriteWarning(string message)
        {
        }
    }

    private static Monitor Sample(int number, string id, string name, string method)
    {
        var m = new Monitor
        {
            MonitorNumber = number,
            Id = id,
            Name = name,
            CommunicationMethod = method,
            CurrentBrightness = 30,
            CurrentContrast = 50,
            CurrentVolume = 70,
            CurrentColorTemperature = 0x05,
            CurrentInputSource = 0x11,
            CurrentPowerState = 0x01,
            GdiDeviceName = @"\\.\DISPLAY1",
        };
        m.Capabilities = PowerDisplay.Common.Models.MonitorCapabilities.Brightness;
        return m;
    }

    [TestMethod]
    public void EmitAll_EveryMonitorAppearsAsEntry_WithMethod()
    {
        var monitors = new List<Monitor>
        {
            Sample(1, "\\\\?\\DISPLAY#A", "Dell", "DDC/CI"),
            Sample(2, "\\\\?\\DISPLAY#B", "Internal", "WMI"),
        };
        var output = new CapturingOutput();

        var exit = GetCommand.EmitAll(monitors, settingFilter: null, output);

        Assert.AreEqual(0, exit);
        Assert.IsNotNull(output.LastGetResult);
        Assert.AreEqual(2, output.LastGetResult!.Monitors.Count);

        Assert.AreEqual(1, output.LastGetResult.Monitors[0].Monitor.Number);
        Assert.AreEqual("Dell", output.LastGetResult.Monitors[0].Monitor.Name);
        Assert.AreEqual("DDC/CI", output.LastGetResult.Monitors[0].Monitor.Method);

        Assert.AreEqual(2, output.LastGetResult.Monitors[1].Monitor.Number);
        Assert.AreEqual("WMI", output.LastGetResult.Monitors[1].Monitor.Method);
    }

    [TestMethod]
    public void EmitAll_PerEntryHasAllSettings()
    {
        var monitors = new List<Monitor> { Sample(1, "\\\\?\\DISPLAY#A", "Dell", "DDC/CI") };
        var output = new CapturingOutput();

        var exit = GetCommand.EmitAll(monitors, settingFilter: null, output);

        Assert.AreEqual(0, exit);
        Assert.AreEqual(GetCommand.AllSettingNames.Length, output.LastGetResult!.Monitors[0].Settings.Count);
    }

    [TestMethod]
    public void EmitAll_WithSettingFilter_OnlyEmitsThatSetting()
    {
        var monitors = new List<Monitor> { Sample(1, "\\\\?\\DISPLAY#A", "Dell", "DDC/CI") };
        var output = new CapturingOutput();

        var exit = GetCommand.EmitAll(monitors, settingFilter: "brightness", output);

        Assert.AreEqual(0, exit);
        Assert.AreEqual(1, output.LastGetResult!.Monitors[0].Settings.Count);
        Assert.AreEqual("brightness", output.LastGetResult.Monitors[0].Settings[0].Setting);
        Assert.AreEqual("30%", output.LastGetResult.Monitors[0].Settings[0].Display);
    }

    [TestMethod]
    public void EmitAll_EmptyMonitorList_ReturnsEmptyResult()
    {
        var output = new CapturingOutput();

        var exit = GetCommand.EmitAll([], settingFilter: null, output);

        Assert.AreEqual(0, exit);
        Assert.IsNotNull(output.LastGetResult);
        Assert.AreEqual(0, output.LastGetResult!.Monitors.Count);
    }

    [TestMethod]
    public void TextOutput_RendersProtocolAndIdHeader()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var writer = new TextCliOutput(stdout, stderr);

        var result = new CliGetResult
        {
            Monitors =
            [
                new CliGetMonitorEntry
                {
                    Monitor = new CliMonitorRef
                    {
                        Number = 1,
                        Id = "\\\\?\\DISPLAY#A",
                        Name = "Dell",
                        Method = "DDC/CI",
                    },
                    Settings =
                    [
                        new CliSettingValue { Setting = "brightness", Raw = 30, Display = "30%", Supported = true },
                    ],
                },
            ],
        };

        writer.WriteGetResult(result);

        var text = stdout.ToString();
        StringAssert.Contains(text, "Monitor 1 (Dell)");
        StringAssert.Contains(text, "protocol");
        StringAssert.Contains(text, "DDC/CI");
        StringAssert.Contains(text, "id");
        StringAssert.Contains(text, "\\\\?\\DISPLAY#A");
        StringAssert.Contains(text, "brightness");
        StringAssert.Contains(text, "30%");
    }
}
