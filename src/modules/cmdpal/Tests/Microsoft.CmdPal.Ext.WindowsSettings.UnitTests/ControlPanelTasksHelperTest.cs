// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.Ext.WindowsSettings.Classes;
using Microsoft.CmdPal.Ext.WindowsSettings.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.WindowsSettings.UnitTests;

[TestClass]
public class ControlPanelTasksHelperTest
{
    private static WindowsSettings.Classes.WindowsSettings CreateSettings(params WindowsSetting[] settings)
    {
        return new WindowsSettings.Classes.WindowsSettings()
        {
            Settings = settings.ToList(),
        };
    }

    private static WindowsSetting CreateTask(string name, string command = "::{26EE0668-A00A-44D7-9371-BEB064C98683}\\0\\::{ED7BA470-8E54-465E-825C-99712043E01C}\\{00000000-0000-0000-0000-000000000001}")
    {
        return new WindowsSetting()
        {
            Name = name,
            Command = command,
            Type = "Control Panel",
        };
    }

    [TestMethod]
    public void MergeAddsNewTasks()
    {
        var settings = CreateSettings(CreateTask("Existing setting"));
        var tasks = new List<WindowsSetting>()
        {
            CreateTask("Set up USB game controllers"),
            CreateTask("Restore your files with File History"),
        };

        var added = ControlPanelTasksHelper.MergeIntoSettings(settings, tasks);

        Assert.AreEqual(2, added);
        Assert.AreEqual(3, settings.Settings.Count());
    }

    [TestMethod]
    public void MergeSkipsDuplicateNamesCaseInsensitive()
    {
        var settings = CreateSettings(CreateTask("File History"));
        var tasks = new List<WindowsSetting>()
        {
            CreateTask("file history"),
            CreateTask("FILE HISTORY"),
        };

        var added = ControlPanelTasksHelper.MergeIntoSettings(settings, tasks);

        Assert.AreEqual(0, added);
        Assert.AreEqual(1, settings.Settings.Count());
    }

    [TestMethod]
    public void MergeSkipsNamesMatchingAlternativeNames()
    {
        var existing = CreateTask("Device Manager");
        existing.AltNames = new List<string>() { "Hardware Manager" };
        var settings = CreateSettings(existing);

        var tasks = new List<WindowsSetting>()
        {
            CreateTask("Hardware Manager"),
        };

        var added = ControlPanelTasksHelper.MergeIntoSettings(settings, tasks);

        Assert.AreEqual(0, added);
        Assert.AreEqual(1, settings.Settings.Count());
    }

    [TestMethod]
    public void MergeSkipsEntriesWithoutNameOrCommand()
    {
        var settings = CreateSettings();
        var tasks = new List<WindowsSetting>()
        {
            CreateTask(string.Empty),
            CreateTask("Valid name", command: string.Empty),
            CreateTask("Another valid name"),
        };

        var added = ControlPanelTasksHelper.MergeIntoSettings(settings, tasks);

        Assert.AreEqual(1, added);
        Assert.AreEqual("Another valid name", settings.Settings.Single().Name);
    }

    [TestMethod]
    public void MergeReturnsZeroForEmptyTaskList()
    {
        var settings = CreateSettings(CreateTask("Existing setting"));

        var added = ControlPanelTasksHelper.MergeIntoSettings(settings, new List<WindowsSetting>());

        Assert.AreEqual(0, added);
        Assert.AreEqual(1, settings.Settings.Count());
    }

    [TestMethod]
    public void MergeDeduplicatesWithinTaskList()
    {
        var settings = CreateSettings();
        var tasks = new List<WindowsSetting>()
        {
            CreateTask("Mouse settings"),
            CreateTask("Mouse settings"),
        };

        var added = ControlPanelTasksHelper.MergeIntoSettings(settings, tasks);

        Assert.AreEqual(1, added);
        Assert.AreEqual(1, settings.Settings.Count());
    }

    [TestMethod]
    public void MergedTasksKeepShellItemCommandPrefix()
    {
        var settings = CreateSettings();
        var tasks = new List<WindowsSetting>() { CreateTask("Set up USB game controllers") };

        ControlPanelTasksHelper.MergeIntoSettings(settings, tasks);

        var merged = settings.Settings.Single();
        Assert.IsTrue(merged.Command.StartsWith(ControlPanelTasksHelper.ShellItemCommandPrefix, System.StringComparison.Ordinal));
    }

    [TestMethod]
    public void MergePublishesANewListInsteadOfMutatingTheOldOne()
    {
        // The merge runs on a background task while the search page may be
        // enumerating the current list, so it has to publish a new list
        // instead of mutating the visible one.
        var settings = CreateSettings(CreateTask("Existing setting"));
        var published = settings.Settings;

        var added = ControlPanelTasksHelper.MergeIntoSettings(settings, new List<WindowsSetting>() { CreateTask("New task") });

        Assert.AreEqual(1, added);
        Assert.AreEqual(1, published.Count());
        Assert.AreNotSame(published, settings.Settings);
    }

    [TestMethod]
    public void ParseKeywordsSplitsAndTrims()
    {
        var result = ControlPanelTasksHelper.ParseKeywords("adjust; joystick;controllers ; devices;");

        Assert.IsNotNull(result);
        CollectionAssert.AreEqual(
            new List<string>() { "adjust", "joystick", "controllers", "devices" },
            result.ToList());
    }

    [TestMethod]
    public void ParseKeywordsDeduplicatesCaseInsensitive()
    {
        var result = ControlPanelTasksHelper.ParseKeywords("back;Back;BACK;restore");

        Assert.IsNotNull(result);
        CollectionAssert.AreEqual(new List<string>() { "back", "restore" }, result.ToList());
    }

    [TestMethod]
    public void ParseKeywordsReturnsNullWhenNothingUsable()
    {
        Assert.IsNull(ControlPanelTasksHelper.ParseKeywords(null));
        Assert.IsNull(ControlPanelTasksHelper.ParseKeywords(string.Empty));
        Assert.IsNull(ControlPanelTasksHelper.ParseKeywords("   "));
        Assert.IsNull(ControlPanelTasksHelper.ParseKeywords(";; ; ;"));
    }
}
