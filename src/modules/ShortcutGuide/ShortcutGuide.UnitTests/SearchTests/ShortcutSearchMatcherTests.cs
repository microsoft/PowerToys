// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using ShortcutGuide.Helpers;
using ShortcutGuide.Models;

namespace ShortcutGuide.UnitTests.SearchTests;

[TestClass]
public sealed class ShortcutSearchMatcherTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Matches_EmptyQuery_ReturnsTrue(string? query)
    {
        Assert.IsTrue(ShortcutSearchMatcher.Matches(CreateShortcut(), query));
    }

    [TestMethod]
    [DataRow("TOGGLE")]
    [DataRow("virtual desktops")]
    public void Matches_NameOrDescription_IsCaseInsensitive(string query)
    {
        var shortcut = CreateShortcut(name: "Toggle desktop", description: "Manage Virtual Desktops");

        Assert.IsTrue(ShortcutSearchMatcher.Matches(shortcut, query));
    }

    [TestMethod]
    [DataRow("windows", true, false, false, false)]
    [DataRow("control", false, true, false, false)]
    [DataRow("alt", false, false, true, false)]
    [DataRow("shift", false, false, false, true)]
    public void Matches_ModifierSemanticName_ReturnsTrue(string query, bool win, bool ctrl, bool alt, bool shift)
    {
        var shortcut = CreateShortcut(
            shortcutDescriptions: [new ShortcutDescription(ctrl, shift, alt, win, ["K"])]);

        Assert.IsTrue(ShortcutSearchMatcher.Matches(shortcut, query));
    }

    [TestMethod]
    [DataRow("K", "K")]
    [DataRow("F1", "112")]
    [DataRow("Esc", "<Escape>")]
    [DataRow("Num", "<TASKBAR1-9>")]
    [DataRow("<", "<LessThan>")]
    [DataRow("Left Arrow", "<Left>")]
    public void Matches_DisplayedKeyLabel_ReturnsTrue(string query, string key)
    {
        var shortcut = CreateShortcut(
            shortcutDescriptions: [new ShortcutDescription(false, false, false, false, [key])]);

        Assert.IsTrue(ShortcutSearchMatcher.Matches(shortcut, query));
    }

    [TestMethod]
    public void Matches_UnrelatedQuery_ReturnsFalse()
    {
        var shortcut = CreateShortcut(
            name: "Open settings",
            description: "Configure the app",
            shortcutDescriptions: [new ShortcutDescription(true, false, false, true, ["I"])]);

        Assert.IsFalse(ShortcutSearchMatcher.Matches(shortcut, "screenshot"));
    }

    private static ShortcutEntry CreateShortcut(
        string name = "Toggle desktop",
        string? description = "Manage desktops",
        ShortcutDescription[]? shortcutDescriptions = null)
    {
        return new ShortcutEntry(name, description, false, shortcutDescriptions ?? []);
    }
}
