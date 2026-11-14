// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.WindowsSettings.Classes;
using Microsoft.CmdPal.Ext.WindowsSettings.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.WindowsSettings.UnitTests;

[TestClass]
public class WindowsSettingsSearchTest
{
    private static readonly WindowsSetting _accessibilityDisplay = new()
    {
        Name = "Display",
        Areas = ["Accessibility"],
        Type = "Settings",
        Command = "ms-settings:easeofaccess-display",
        JoinedFullSettingsPath = "Settings > Accessibility",
    };

    private static readonly WindowsSetting _systemDisplay = new()
    {
        Name = "Display",
        Areas = ["System"],
        Type = "Settings",
        Command = "ms-settings:display",
        JoinedFullSettingsPath = "Settings > System",
        IsPreferredForNameSearch = true,
    };

    [TestMethod]
    public void QueryKeepsDistinctDestinationsWithTheSameTitle()
    {
        var page = CreatePage(_accessibilityDisplay, _systemDisplay);

        var results = page.Query("Display");

        Assert.AreEqual(2, results.Count);
        Assert.AreEqual("Settings > System", results[0].Subtitle);
        Assert.AreEqual("Settings > Accessibility", results[1].Subtitle);
    }

    [TestMethod]
    public void QueryPrefersPrimaryDestinationForNamePrefixMatch()
    {
        var page = CreatePage(_accessibilityDisplay, _systemDisplay);

        var results = page.Query("Disp");

        Assert.AreEqual(2, results.Count);
        Assert.AreEqual("Settings > System", results[0].Subtitle);
        Assert.AreEqual("Settings > Accessibility", results[1].Subtitle);
    }

    [TestMethod]
    public void QueryPrefersWindowsSettingsForExactMatchOnly()
    {
        var controlPanelSound = new WindowsSetting
        {
            Name = "Sound",
            Type = "Control Panel",
            Command = "control /name Microsoft.Sound",
            JoinedFullSettingsPath = "Control Panel > Hardware and Sound",
        };
        var windowsSettingsSound = new WindowsSetting
        {
            Name = "Sound",
            Type = "Settings",
            Command = "ms-settings:sound",
            JoinedFullSettingsPath = "Settings > System",
        };
        var page = CreatePage(controlPanelSound, windowsSettingsSound);

        var exactResults = page.Query("Sound");
        var prefixResults = page.Query("Sou");

        Assert.AreEqual("Settings > System", exactResults[0].Subtitle);
        Assert.AreEqual("Control Panel > Hardware and Sound", prefixResults[0].Subtitle);
    }

    [TestMethod]
    public void QueryDeduplicatesTheSameDestination()
    {
        var duplicate = new WindowsSetting
        {
            Name = "Display",
            Type = "Settings",
            Command = _systemDisplay.Command,
        };
        var page = CreatePage(_systemDisplay, duplicate);

        var results = page.Query("Display");

        Assert.AreEqual(1, results.Count);
    }

    [TestMethod]
    public async Task FallbackReturnsSeparateResultsForMultipleExactMatches()
    {
        var settings = CreateSettings(_accessibilityDisplay, _systemDisplay);
        var fallback = new FallbackWindowsSettingsItem(settings);

        var result = await ((IFallbackCommandItem3)fallback).QueryHandler.QueryAsync(new FallbackQueryArgs("Display", "query-id", 10));

        Assert.HasCount(2, result.Items);
        Assert.AreEqual("Settings > System", result.Items[0].Subtitle);
        Assert.AreEqual("Settings > Accessibility", result.Items[1].Subtitle);
    }

    private static WindowsSettingsListPage CreatePage(params WindowsSetting[] settings)
    {
        return new WindowsSettingsListPage(CreateSettings(settings));
    }

    private static WindowsSettings.Classes.WindowsSettings CreateSettings(params WindowsSetting[] settings)
    {
        return new WindowsSettings.Classes.WindowsSettings
        {
            Settings = settings.ToList(),
        };
    }
}
