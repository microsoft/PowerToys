// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests;

[TestClass]
public sealed class MacroHotkeyConverterTests
{
    [TestMethod]
    public void ToHotkeySettings_Null_ReturnsEmpty()
    {
        var hs = MacroHotkeyConverter.ToHotkeySettings(null);
        Assert.AreEqual(0, hs.Code);
        Assert.IsFalse(hs.Ctrl);
    }

    [TestMethod]
    public void ToHotkeySettings_Empty_ReturnsEmpty()
    {
        var hs = MacroHotkeyConverter.ToHotkeySettings(string.Empty);
        Assert.AreEqual(0, hs.Code);
    }

    [TestMethod]
    public void ToHotkeySettings_CtrlF12_Correct()
    {
        var hs = MacroHotkeyConverter.ToHotkeySettings("Ctrl+F12");
        Assert.IsTrue(hs.Ctrl);
        Assert.IsFalse(hs.Shift);
        Assert.IsFalse(hs.Alt);
        Assert.IsFalse(hs.Win);
        Assert.AreEqual(0x7B, hs.Code); // F12
    }

    [TestMethod]
    public void ToHotkeySettings_CtrlShiftV_Correct()
    {
        var hs = MacroHotkeyConverter.ToHotkeySettings("Ctrl+Shift+V");
        Assert.IsTrue(hs.Ctrl);
        Assert.IsTrue(hs.Shift);
        Assert.IsFalse(hs.Alt);
        Assert.AreEqual((int)'V', hs.Code);
    }

    [TestMethod]
    public void ToHotkeySettings_WinAltG_Correct()
    {
        var hs = MacroHotkeyConverter.ToHotkeySettings("Win+Alt+G");
        Assert.IsTrue(hs.Win);
        Assert.IsTrue(hs.Alt);
        Assert.IsFalse(hs.Ctrl);
        Assert.AreEqual((int)'G', hs.Code);
    }

    [TestMethod]
    public void FromHotkeySettings_Null_ReturnsNull()
    {
        Assert.IsNull(MacroHotkeyConverter.FromHotkeySettings(null));
    }

    [TestMethod]
    public void FromHotkeySettings_NoCode_ReturnsNull()
    {
        var hs = new HotkeySettings(false, true, false, false, 0);
        Assert.IsNull(MacroHotkeyConverter.FromHotkeySettings(hs));
    }

    [TestMethod]
    public void FromHotkeySettings_CtrlF12_Correct()
    {
        var hs = new HotkeySettings(false, true, false, false, 0x7B);
        Assert.AreEqual("Ctrl+F12", MacroHotkeyConverter.FromHotkeySettings(hs));
    }

    [TestMethod]
    public void RoundTrip_CtrlShiftF5()
    {
        const string original = "Ctrl+Shift+F5";
        var hs = MacroHotkeyConverter.ToHotkeySettings(original);
        var result = MacroHotkeyConverter.FromHotkeySettings(hs);
        Assert.AreEqual(original, result);
    }

    [TestMethod]
    public void RoundTrip_WinAltG()
    {
        const string original = "Win+Alt+G";
        var hs = MacroHotkeyConverter.ToHotkeySettings(original);
        var result = MacroHotkeyConverter.FromHotkeySettings(hs);
        Assert.AreEqual(original, result);
    }

    [TestMethod]
    public void ToHotkeySettings_CaseInsensitive_Modifiers()
    {
        var hs = MacroHotkeyConverter.ToHotkeySettings("ctrl+shift+F1");
        Assert.IsTrue(hs.Ctrl);
        Assert.IsTrue(hs.Shift);
        Assert.AreEqual(0x70, hs.Code); // F1
    }

    [TestMethod]
    public void ToHotkeySettings_SingleFunctionKey_NoModifiers()
    {
        var hs = MacroHotkeyConverter.ToHotkeySettings("F12");
        Assert.IsFalse(hs.Win);
        Assert.IsFalse(hs.Ctrl);
        Assert.IsFalse(hs.Alt);
        Assert.IsFalse(hs.Shift);
        Assert.AreEqual(0x7B, hs.Code); // F12
    }

    [TestMethod]
    public void ToHotkeySettings_DigitKey_Correct()
    {
        var hs = MacroHotkeyConverter.ToHotkeySettings("Ctrl+5");
        Assert.IsTrue(hs.Ctrl);
        Assert.AreEqual(0x35, hs.Code); // VK code for digit '5'
    }

    [TestMethod]
    public void ToHotkeySettings_NamedKey_Enter()
    {
        var hs = MacroHotkeyConverter.ToHotkeySettings("Enter");
        Assert.AreEqual(0x0D, hs.Code);
        Assert.IsFalse(hs.Ctrl);
    }

    [TestMethod]
    public void FromHotkeySettings_AliasedKey_NormalizesToCanonical()
    {
        // "Escape" and "Esc" both map to 0x1B.
        // Round-trip normalizes to whichever alias was stored first in VkToName.
        // This test documents (and pins) that behavior.
        var hsFromEscape = MacroHotkeyConverter.ToHotkeySettings("Escape");
        var hsFromEsc = MacroHotkeyConverter.ToHotkeySettings("Esc");

        // Both should produce the same VK code
        Assert.AreEqual(hsFromEscape.Code, hsFromEsc.Code);
        Assert.AreEqual(0x1B, hsFromEscape.Code);

        // FromHotkeySettings returns a canonical name (whichever was first in dict)
        var canonical = MacroHotkeyConverter.FromHotkeySettings(hsFromEscape);
        Assert.IsNotNull(canonical);
        Assert.IsTrue(
            canonical == "Escape" || canonical == "Esc",
            $"Expected 'Escape' or 'Esc' but got '{canonical}'");
    }
}
