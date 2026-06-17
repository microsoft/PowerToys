// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ShortcutGuide.Converters;
using ShortcutGuide.Models;

namespace ShortcutGuide.UnitTests.ConvertersTests;

[TestClass]
public sealed class ShortcutDescriptionToKeysConverterTests
{
    private static List<object> Convert(ShortcutDescription description)
        => new ShortcutDescriptionToKeysConverter().GetKeysList(description);

    [TestMethod]
    [DataRow("<0>")]
    [DataRow("<1>")]
    [DataRow("<8>")]
    [DataRow("<9>")]
    public void GetKeysList_LiteralDigitKey_IsPassedThroughVerbatim(string key)
    {
        // A literal digit key (e.g. Ctrl+9 "switch to last tab") is authored with the
        // <N> convention so it is not parsed as a virtual-key code (VK 9 is Tab, VK 1 is
        // the left mouse button, VK 0 is undefined). The converter forwards the token
        // unchanged; KeyVisual strips the angle brackets when rendering.
        var result = Convert(new ShortcutDescription(ctrl: true, shift: false, alt: false, win: false, keys: [key]));

        CollectionAssert.AreEqual(new object[] { "Ctrl", key }, result);
    }

    [TestMethod]
    public void GetKeysList_Modifiers_AreEmittedBeforeKeysInWinCtrlAltShiftOrder()
    {
        // Win -> 92, Ctrl -> "Ctrl", Alt -> "Alt", Shift -> 16, then the keys.
        var result = Convert(new ShortcutDescription(ctrl: true, shift: true, alt: true, win: true, keys: ["A"]));

        CollectionAssert.AreEqual(new object[] { 92, "Ctrl", "Alt", 16, "A" }, result);
    }

    [TestMethod]
    public void GetKeysList_NonNumericKey_IsPassedThroughVerbatim()
    {
        // Non-numeric key strings (e.g. the "1 - 8" tab-range) render as-is.
        var result = Convert(new ShortcutDescription(ctrl: true, shift: false, alt: false, win: false, keys: ["1 - 8"]));

        CollectionAssert.AreEqual(new object[] { "Ctrl", "1 - 8" }, result);
    }

    [TestMethod]
    public void GetKeysList_ArrowNameKey_MapsToVirtualKeyCode()
    {
        // Named arrow keys map to their VK codes (Up -> 38), independent of the digit handling.
        var result = Convert(new ShortcutDescription(ctrl: false, shift: false, alt: false, win: false, keys: ["Up"]));

        CollectionAssert.AreEqual(new object[] { 38 }, result);
    }
}
