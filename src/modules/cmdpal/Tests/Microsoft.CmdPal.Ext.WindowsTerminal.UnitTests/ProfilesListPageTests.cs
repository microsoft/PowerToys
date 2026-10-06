// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Ext.WindowsTerminal.Commands;
using Microsoft.CmdPal.Ext.WindowsTerminal.Helpers;
using Microsoft.CmdPal.Ext.WindowsTerminal.Pages;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.WindowsTerminal.UnitTests;

[TestClass]
public class ProfilesListPageTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void ProfileItem_RequestsAdministratorShortcut(bool openNewTab, bool openQuake)
    {
        var terminal = new TerminalPackage("TestTerminal!App", new Version(1, 0), "Terminal", string.Empty, string.Empty, string.Empty);
        var profile = new TerminalProfile(terminal, "PowerShell", Guid.NewGuid(), false, string.Empty);
        var settings = new AppSettingsManager(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "settings.json"));

        var item = ProfilesListPage.CreateProfileItem(profile, openNewTab, openQuake, settings);

        Assert.AreEqual(profile.Name, item.Title);
        Assert.IsInstanceOfType<LaunchProfileCommand>(item.Command);
        var admin = item.MoreCommands.OfType<CommandContextItem>().Single();
        Assert.IsInstanceOfType<LaunchProfileAsAdminCommand>(admin.Command);
        Assert.AreEqual(WellKnownKeyChords.RunAsAdministrator, admin.RequestedShortcut);
    }
}
