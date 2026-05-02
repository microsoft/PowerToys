// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.MacroEngine;

namespace PowerToys.MacroEngine.Tests;

[TestClass]
public sealed class KeyParserTests
{
    // ParseHotkey was removed in the MacroHotkeySettings refactor.
    // ParseKey and ParseKeyCombo remain (used for PressKey step execution).

    [TestMethod]
    public void ParseKeyCombo_CtrlC_ReturnsModifierAndMain()
    {
        var (mods, main) = KeyParser.ParseKeyCombo("Ctrl+C");
        Assert.AreEqual(1, mods.Count);
        Assert.AreEqual((ushort)0xA2, mods[0]); // VK_LCONTROL
        Assert.AreEqual((ushort)'C', main);
    }

    [TestMethod]
    public void ParseKey_Enter_Returns0x0D()
    {
        Assert.AreEqual((ushort)0x0D, KeyParser.ParseKey("Enter"));
    }

    [TestMethod]
    public void ParseKey_SingleChar_CaseInsensitive()
    {
        Assert.AreEqual(KeyParser.ParseKey("a"), KeyParser.ParseKey("A"));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void ParseKey_Unknown_Throws()
    {
        KeyParser.ParseKey("XYZ123");
    }

    [TestMethod]
    public void ParseKeyCombo_MultipleModifiers_ReturnsAll()
    {
        var (mods, main) = KeyParser.ParseKeyCombo("Ctrl+Shift+S");
        Assert.AreEqual(2, mods.Count);
        Assert.IsTrue(mods.Contains((ushort)0xA2)); // VK_LCONTROL
        Assert.IsTrue(mods.Contains((ushort)0xA0)); // VK_LSHIFT
        Assert.AreEqual((ushort)'S', main);
    }

    [TestMethod]
    public void ParseKey_DigitChar_ReturnsAsciiCode()
    {
        Assert.AreEqual((ushort)'5', KeyParser.ParseKey("5"));
    }

    [TestMethod]
    public void ParseKey_Alias_Esc_Works()
    {
        Assert.AreEqual((ushort)0x1B, KeyParser.ParseKey("Esc"));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void ParseKey_EmptyString_Throws()
    {
        KeyParser.ParseKey(string.Empty);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void ParseKeyCombo_NullInput_Throws()
    {
        KeyParser.ParseKeyCombo(null!);
    }
}
