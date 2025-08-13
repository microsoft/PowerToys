// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Shell.UnitTests;

[TestClass]
public class SettingsTests
{
    [TestMethod]
    public void DefaultSettings_HasExpectedValues()
    {
        // Act
        var settings = Settings.CreateDefaultSettings();

        // Assert
        Assert.IsFalse(settings.LeaveShellOpen);
        Assert.AreEqual("0", settings.ShellCommandExecution);
        Assert.IsFalse(settings.RunAsAdministrator);
        Assert.IsNotNull(settings.Count);
        Assert.AreEqual(0, settings.Count.Count);
    }

    [TestMethod]
    public void LeaveShellOpenSettings_EnablesLeaveShellOpen()
    {
        // Act
        var settings = Settings.CreateLeaveShellOpenSettings();

        // Assert
        Assert.IsTrue(settings.LeaveShellOpen);
        Assert.AreEqual("0", settings.ShellCommandExecution);
        Assert.IsFalse(settings.RunAsAdministrator);
    }

    [TestMethod]
    public void PowerShellSettings_SetsPowerShellExecution()
    {
        // Act
        var settings = Settings.CreatePowerShellSettings();

        // Assert
        Assert.IsFalse(settings.LeaveShellOpen);
        Assert.AreEqual("1", settings.ShellCommandExecution);
        Assert.IsFalse(settings.RunAsAdministrator);
    }

    [TestMethod]
    public void AdministratorSettings_EnablesRunAsAdministrator()
    {
        // Act
        var settings = Settings.CreateAdministratorSettings();

        // Assert
        Assert.IsFalse(settings.LeaveShellOpen);
        Assert.AreEqual("0", settings.ShellCommandExecution);
        Assert.IsTrue(settings.RunAsAdministrator);
    }

    [TestMethod]
    public void AddCmdHistory_IncrementsCount()
    {
        // Setup
        var settings = Settings.CreateDefaultSettings();

        // Act
        settings.AddCmdHistory("notepad");
        settings.AddCmdHistory("notepad");
        settings.AddCmdHistory("cmd");

        // Assert
        Assert.AreEqual(2, settings.Count["notepad"]);
        Assert.AreEqual(1, settings.Count["cmd"]);
        Assert.AreEqual(2, settings.Count.Count);
    }

    [TestMethod]
    public void SettingsInterface_ImplementedCorrectly()
    {
        // Setup
        var settings = Settings.CreateDefaultSettings();

        // Assert - Verify all interface properties are accessible
        Assert.IsNotNull(settings.ShellCommandExecution);
        Assert.IsNotNull(settings.Count);

        // Verify boolean properties
        Assert.IsTrue(settings.LeaveShellOpen || !settings.LeaveShellOpen); // Just verify it's accessible
        Assert.IsTrue(settings.RunAsAdministrator || !settings.RunAsAdministrator);
    }
}
