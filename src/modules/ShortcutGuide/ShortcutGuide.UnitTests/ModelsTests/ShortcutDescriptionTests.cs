// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using ShortcutGuide.Models;

namespace ShortcutGuide.UnitTests.ModelsTests;

[TestClass]
public sealed class ShortcutDescriptionTests
{
    [TestMethod]
    public void Equals_IdenticalDescriptions_ReturnsTrue()
    {
        var a = new ShortcutDescription(ctrl: true, shift: true, alt: false, win: true, keys: ["A", "B"]);
        var b = new ShortcutDescription(ctrl: true, shift: true, alt: false, win: true, keys: ["A", "B"]);

        Assert.IsTrue(a.Equals(b));
    }

    [TestMethod]
    [DataRow(true, false, false, false, false, false, false, false)]
    [DataRow(false, false, true, false, false, false, false, false)]
    [DataRow(false, false, false, false, false, true, false, false)]
    [DataRow(false, false, false, false, false, false, false, true)]
    public void Equals_DifferentModifier_ReturnsFalse(
        bool ctrlA, bool ctrlB, bool shiftA, bool shiftB, bool altA, bool altB, bool winA, bool winB)
    {
        var a = new ShortcutDescription(ctrl: ctrlA, shift: shiftA, alt: altA, win: winA, keys: []);
        var b = new ShortcutDescription(ctrl: ctrlB, shift: shiftB, alt: altB, win: winB, keys: []);

        Assert.IsFalse(a.Equals(b));
    }

    [TestMethod]
    public void Equals_DifferentKeys_ReturnsFalse()
    {
        var a = new ShortcutDescription(ctrl: false, shift: false, alt: false, win: false, keys: ["A"]);
        var b = new ShortcutDescription(ctrl: false, shift: false, alt: false, win: false, keys: ["B"]);

        Assert.IsFalse(a.Equals(b));
    }

    [TestMethod]
    public void Equals_DifferentKeyCount_ReturnsFalse()
    {
        var a = new ShortcutDescription(ctrl: false, shift: false, alt: false, win: false, keys: ["A"]);
        var b = new ShortcutDescription(ctrl: false, shift: false, alt: false, win: false, keys: ["A", "B"]);

        Assert.IsFalse(a.Equals(b));
    }

    [TestMethod]
    public void Equals_Null_ReturnsFalse()
    {
        var a = new ShortcutDescription(ctrl: true, shift: false, alt: false, win: false, keys: []);

        Assert.IsFalse(a.Equals(null));
    }

    [TestMethod]
    public void OperatorEquals_IdenticalDescriptions_ReturnsTrue()
    {
        var a = new ShortcutDescription(ctrl: true, shift: false, alt: true, win: false, keys: ["X"]);
        var b = new ShortcutDescription(ctrl: true, shift: false, alt: true, win: false, keys: ["X"]);

        Assert.IsTrue(a == b);
    }

    [TestMethod]
    public void OperatorNotEquals_DifferentDescriptions_ReturnsTrue()
    {
        var a = new ShortcutDescription(ctrl: true, shift: false, alt: false, win: false, keys: []);
        var b = new ShortcutDescription(ctrl: false, shift: false, alt: false, win: false, keys: []);

        Assert.IsTrue(a != b);
    }

    [TestMethod]
    public void OperatorEquals_BothNull_ReturnsTrue()
    {
        ShortcutDescription? a = null;
        ShortcutDescription? b = null;

        Assert.IsTrue(a == b);
    }

    [TestMethod]
    public void OperatorEquals_OneNull_ReturnsFalse()
    {
        ShortcutDescription? a = new ShortcutDescription(ctrl: false, shift: false, alt: false, win: false, keys: []);
        ShortcutDescription? b = null;

        Assert.IsFalse(a == b);
        Assert.IsFalse(b == a);
    }

    [TestMethod]
    public void GetHashCode_EqualDescriptions_ProduceSameHashCode()
    {
        var a = new ShortcutDescription(ctrl: true, shift: false, alt: true, win: false, keys: ["Z"]);
        var b = new ShortcutDescription(ctrl: true, shift: false, alt: true, win: false, keys: ["Z"]);

        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }
}
