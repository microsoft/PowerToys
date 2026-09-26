// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using ShortcutGuide.Models;

namespace ShortcutGuide.UnitTests.ModelsTests;

[TestClass]
public sealed class ShortcutEntryTests
{
    [TestMethod]
    public void Equals_IdenticalEntries_ReturnsTrue()
    {
        var desc = new ShortcutDescription(ctrl: true, shift: false, alt: false, win: true, keys: ["S"]);
        var a = new ShortcutEntry("Open settings", "Configure", false, [desc]);
        var b = new ShortcutEntry("Open settings", "Configure", false, [desc]);

        Assert.IsTrue(a.Equals(b));
    }

    [TestMethod]
    public void Equals_DifferentName_ReturnsFalse()
    {
        var a = new ShortcutEntry("Action A", null, false, []);
        var b = new ShortcutEntry("Action B", null, false, []);

        Assert.IsFalse(a.Equals(b));
    }

    [TestMethod]
    public void Equals_DifferentDescription_ReturnsFalse()
    {
        var a = new ShortcutEntry("Open", "Desc A", false, []);
        var b = new ShortcutEntry("Open", "Desc B", false, []);

        Assert.IsFalse(a.Equals(b));
    }

    [TestMethod]
    public void Equals_DifferentShortcutCount_ReturnsFalse()
    {
        var desc = new ShortcutDescription(ctrl: false, shift: false, alt: false, win: true, keys: ["A"]);
        var a = new ShortcutEntry("Open", null, false, [desc]);
        var b = new ShortcutEntry("Open", null, false, []);

        Assert.IsFalse(a.Equals(b));
    }

    [TestMethod]
    public void Equals_DifferentShortcutContent_ReturnsFalse()
    {
        var descA = new ShortcutDescription(ctrl: true, shift: false, alt: false, win: false, keys: ["A"]);
        var descB = new ShortcutDescription(ctrl: false, shift: false, alt: false, win: true, keys: ["B"]);
        var a = new ShortcutEntry("Open", null, false, [descA]);
        var b = new ShortcutEntry("Open", null, false, [descB]);

        Assert.IsFalse(a.Equals(b));
    }

    [TestMethod]
    public void Equals_Null_ReturnsFalse()
    {
        var a = new ShortcutEntry("Open", null, false, []);

        Assert.IsFalse(a.Equals(null));
    }

    [TestMethod]
    public void OperatorEquals_IdenticalEntries_ReturnsTrue()
    {
        var a = new ShortcutEntry("Open", "Desc", false, []);
        var b = new ShortcutEntry("Open", "Desc", false, []);

        Assert.IsTrue(a == b);
    }

    [TestMethod]
    public void OperatorNotEquals_DifferentEntries_ReturnsTrue()
    {
        var a = new ShortcutEntry("Open", null, false, []);
        var b = new ShortcutEntry("Close", null, false, []);

        Assert.IsTrue(a != b);
    }

    [TestMethod]
    public void OperatorEquals_BothNull_ReturnsTrue()
    {
        ShortcutEntry? a = null;
        ShortcutEntry? b = null;

        Assert.IsTrue(a == b);
    }

    [TestMethod]
    public void OperatorEquals_OneNull_ReturnsFalse()
    {
        ShortcutEntry? a = new ShortcutEntry("Open", null, false, []);
        ShortcutEntry? b = null;

        Assert.IsFalse(a == b);
        Assert.IsFalse(b == a);
    }

    [TestMethod]
    public void GetHashCode_EqualEntries_ProduceSameHashCode()
    {
        var desc = new ShortcutDescription(ctrl: true, shift: false, alt: false, win: false, keys: ["T"]);
        var a = new ShortcutEntry("Task", "Do it", false, [desc]);
        var b = new ShortcutEntry("Task", "Do it", false, [desc]);

        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }
}
