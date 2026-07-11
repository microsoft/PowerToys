// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CmdPal.Ext.WindowsSettings.Classes;
using Microsoft.CmdPal.Ext.WindowsSettings.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.WindowsSettings.UnitTests;

[TestClass]
public class WindowsSettingsPathHelperTest
{
    /// <summary>
    /// The path delimiter used by <see cref="WindowsSettingsPathHelper"/>.
    /// </summary>
    private const string PathDelimiterSequence = "\u0020\u0020\u02C3\u0020\u0020"; // = "<space><space><arrow><space><space>"

    [TestMethod]
    public void GeneratePathValuesWithSingleArea()
    {
        var setting = new WindowsSetting()
        {
            Name = "Set up USB game controllers",
            Type = "Control Panel",
            Areas = new List<string>() { "Devices and Printers" },
        };

        WindowsSettingsPathHelper.GeneratePathValues(setting);

        Assert.AreEqual("Devices and Printers", setting.JoinedAreaPath);
        Assert.AreEqual($"Control Panel{PathDelimiterSequence}Devices and Printers", setting.JoinedFullSettingsPath);
    }

    [TestMethod]
    public void GeneratePathValuesWithMultipleAreas()
    {
        var setting = new WindowsSetting()
        {
            Name = "Administrative Tools",
            Type = "Control Panel",
            Areas = new List<string>() { "System and Security", "Administrative Tools" },
        };

        WindowsSettingsPathHelper.GeneratePathValues(setting);

        Assert.AreEqual($"System and Security{PathDelimiterSequence}Administrative Tools", setting.JoinedAreaPath);
        Assert.AreEqual($"Control Panel{PathDelimiterSequence}System and Security{PathDelimiterSequence}Administrative Tools", setting.JoinedFullSettingsPath);
    }

    [TestMethod]
    public void GeneratePathValuesWithoutAreas()
    {
        var setting = new WindowsSetting()
        {
            Name = "File History",
            Type = "Control Panel",
        };

        WindowsSettingsPathHelper.GeneratePathValues(setting);

        Assert.AreEqual(string.Empty, setting.JoinedAreaPath);
        Assert.AreEqual("Control Panel", setting.JoinedFullSettingsPath);
    }

    [TestMethod]
    public void GeneratePathValuesSkipsSettingWithoutType()
    {
        var setting = new WindowsSetting()
        {
            Name = "Nameless",
            Areas = new List<string>() { "Some area" },
        };

        WindowsSettingsPathHelper.GeneratePathValues(setting);

        Assert.IsNull(setting.JoinedAreaPath);
        Assert.IsNull(setting.JoinedFullSettingsPath);
    }
}
