// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public partial class PageMenuCommandsTests
{
    [TestMethod]
    public void MenuCommands_StoreEntriesAndNotifyTheMenuKey()
    {
        var page = new MenuPage();
        var properties = ((IExtendedAttributesProvider)page).GetProperties();
        properties["Unrelated"] = "Keep me";
        var settings = new CommandContextItem(new NoOpCommand { Name = "Settings" });
        var separator = new Separator();
        var help = new CommandContextItem(new NoOpCommand { Name = "Help" });
        var notifications = CaptureNotifications(page);

        page.MenuCommands = [settings, separator, help];

        var stored = ((IEnumerable<object>)properties[WellKnownExtensionAttributes.PageMenuCommands]).ToArray();
        Assert.AreEqual(3, stored.Length);
        Assert.AreSame(settings, stored[0]);
        Assert.AreSame(separator, stored[1]);
        Assert.AreSame(help, stored[2]);
        CollectionAssert.AreEqual(new[] { WellKnownExtensionAttributes.PageMenuCommands }, notifications);
        Assert.AreEqual("Keep me", properties["Unrelated"]);
    }

    [TestMethod]
    public void MenuCommands_NotifyOnlyForChanges()
    {
        var settings = new CommandContextItem(new NoOpCommand { Name = "Settings" });
        var help = new CommandContextItem(new NoOpCommand { Name = "Help" });
        IContextItem[] commands = [settings];
        var page = new MenuPage { MenuCommands = commands };
        var notifications = CaptureNotifications(page);

        page.MenuCommands = commands;
        page.MenuCommands = [settings];
        commands[0] = help;
        page.MenuCommands = commands;
        page.MenuCommands = [];
        page.MenuCommands = [];
        page.MenuCommands = null!;

        Assert.AreEqual(2, notifications.Count);
        Assert.IsTrue(notifications.All(name => name == WellKnownExtensionAttributes.PageMenuCommands));
        Assert.AreEqual(0, page.MenuCommands.Length);
        Assert.IsFalse(page.GetProperties().ContainsKey(WellKnownExtensionAttributes.PageMenuCommands));
    }

    [TestMethod]
    public void Attributes_AreCreatedOnFirstUseAndReused()
    {
        var page = new MenuPage();
        var notifications = CaptureNotifications(page);

        page.MenuCommands = [];

        var properties = page.GetProperties();
        Assert.AreEqual(0, properties.Count);
        Assert.IsFalse(properties.IsReadOnly);
        Assert.AreSame(properties, page.GetProperties());
        Assert.AreEqual(0, notifications.Count);
    }

    [TestMethod]
    public void ToolkitPages_PublishAttributesOnlyWhenTheyDeclareTheProvider()
    {
        foreach (var pageType in new[] { typeof(Page), typeof(ListPage), typeof(DynamicListPage), typeof(ContentPage), typeof(ParametersPage) })
        {
            Assert.IsFalse(typeof(IExtendedAttributesProvider).IsAssignableFrom(pageType), pageType.Name);
        }

        Assert.IsTrue(new MenuPage() is IExtendedAttributesProvider);
    }

    private static List<string> CaptureNotifications(Page page)
    {
        List<string> notifications = [];
        page.PropChanged += (_, args) => notifications.Add(args.PropertyName);
        return notifications;
    }

    private sealed partial class MenuPage : Page, IExtendedAttributesProvider
    {
    }
}
