// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToysExtension.Modules;
using Windows.System;
using static Common.UI.SettingsDeepLink;

namespace Microsoft.CmdPal.Ext.PowerToys.UnitTests;

[TestClass]
public class ModuleAdministratorShortcutTests
{
    [TestMethod]
    [DataRow(true, true)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(false, false)]
    public void ModuleCommands_ExposeAdministratorShortcutOnlyWhenEnabled(bool hosts, bool enabled)
    {
        var expectedModule = hosts ? SettingsWindow.Hosts : SettingsWindow.EnvironmentVariables;
        ModuleCommandProvider provider = hosts
            ? new HostsModuleCommandProvider(module =>
            {
                Assert.AreEqual(expectedModule, module);
                return enabled;
            })
            : new EnvironmentVariablesModuleCommandProvider(module =>
            {
                Assert.AreEqual(expectedModule, module);
                return enabled;
            });

        var items = provider.BuildCommands().ToArray();

        Assert.HasCount(enabled ? 3 : 1, items);
        if (enabled)
        {
            var admin = items[0].MoreCommands.OfType<CommandContextItem>().Single();
            var adminCommand = admin.Command;
            var standaloneCommand = items[1].Command;
            Assert.IsNotNull(adminCommand);
            Assert.IsNotNull(standaloneCommand);
            Assert.AreEqual(standaloneCommand.Id, adminCommand.Id);
            Assert.AreEqual(KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: (int)VirtualKey.Enter), admin.RequestedShortcut);
        }
        else
        {
            Assert.HasCount(0, items[0].MoreCommands);
        }
    }
}
