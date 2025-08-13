// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.Ext.Shell.Pages;
using Microsoft.CmdPal.Ext.UnitTestBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.Ext.Shell.UnitTests;

[TestClass]
public class QueryTests : CommandPaletteUnitTestBase
{
    [TestMethod]
    public void ValidateSettingsManager()
    {
        // Setup
        var settings = Settings.CreateDefaultSettings();

        // Assert
        Assert.IsNotNull(settings);
        Assert.IsFalse(settings.LeaveShellOpen);
        Assert.AreEqual("0", settings.ShellCommandExecution);
    }

    [TestMethod]
    public void ValidateSettingsWithDifferentConfiguration()
    {
        // Setup
        var settings = Settings.CreatePowerShellSettings();

        // Assert
        Assert.IsNotNull(settings);
        Assert.AreEqual("1", settings.ShellCommandExecution); // PowerShell
    }

    [TestMethod]
    public void ValidateHistoryFunctionality()
    {
        // Setup
        var settings = Settings.CreateDefaultSettings();

        // Act
        settings.AddCmdHistory("test-command");

        // Assert
        Assert.AreEqual(1, settings.Count["test-command"]);
    }
}
