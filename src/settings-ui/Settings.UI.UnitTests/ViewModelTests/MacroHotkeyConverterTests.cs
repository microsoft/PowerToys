// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.MacroCommon.Models;

namespace ViewModelTests;

[TestClass]
public sealed class MacroHotkeyConverterTests
{
    // ── ToMacroHotkeySettings ──────────────────────────────────────────────────
    [TestMethod]
    public void ToMacroHotkeySettings_Null_ReturnsNull()
    {
        Assert.IsNull(MacroHotkeyConverter.ToMacroHotkeySettings(null));
    }

    [TestMethod]
    public void ToMacroHotkeySettings_NoCode_ReturnsNull()
    {
        // A HotkeySettings with Code=0 is "empty" — treat as no hotkey.
        var hs = new HotkeySettings(false, true, false, false, 0);
        Assert.IsNull(MacroHotkeyConverter.ToMacroHotkeySettings(hs));
    }

    [TestMethod]
    public void ToMacroHotkeySettings_CtrlF9_CorrectFields()
    {
        var hs = new HotkeySettings(false, true, false, false, 0x78); // Win=false Ctrl=true Alt=false Shift=false F9
        var macroHotkey = MacroHotkeyConverter.ToMacroHotkeySettings(hs);

        Assert.IsNotNull(macroHotkey);
        Assert.IsFalse(macroHotkey!.Win);
        Assert.IsTrue(macroHotkey!.Ctrl);
        Assert.IsFalse(macroHotkey!.Alt);
        Assert.IsFalse(macroHotkey!.Shift);
        Assert.AreEqual(0x78, macroHotkey!.Code);
    }

    [TestMethod]
    public void ToMacroHotkeySettings_AllModifiers_CorrectFields()
    {
        var hs = new HotkeySettings(true, true, true, true, 0x41); // All mods + A
        var macroHotkey = MacroHotkeyConverter.ToMacroHotkeySettings(hs);

        Assert.IsNotNull(macroHotkey);
        Assert.IsTrue(macroHotkey!.Win);
        Assert.IsTrue(macroHotkey!.Ctrl);
        Assert.IsTrue(macroHotkey!.Alt);
        Assert.IsTrue(macroHotkey!.Shift);
        Assert.AreEqual(0x41, macroHotkey!.Code);
    }

    // ── ToHotkeySettings ──────────────────────────────────────────────────────
    [TestMethod]
    public void ToHotkeySettings_Null_ReturnsNull()
    {
        Assert.IsNull(MacroHotkeyConverter.ToHotkeySettings(null));
    }

    [TestMethod]
    public void ToHotkeySettings_CtrlF9_CorrectFields()
    {
        var macroHotkey = new MacroHotkeySettings(false, true, false, false, 0x78);
        var hs = MacroHotkeyConverter.ToHotkeySettings(macroHotkey);

        Assert.IsNotNull(hs);
        Assert.IsFalse(hs!.Win);
        Assert.IsTrue(hs!.Ctrl);
        Assert.IsFalse(hs!.Alt);
        Assert.IsFalse(hs!.Shift);
        Assert.AreEqual(0x78, hs!.Code);
    }

    // ── Round-trip ────────────────────────────────────────────────────────────
    [TestMethod]
    public void RoundTrip_CtrlShiftF5()
    {
        // HotkeySettings → MacroHotkeySettings → HotkeySettings
        var original = new HotkeySettings(false, true, false, true, 0x74); // Ctrl+Shift+F5
        var macroHotkey = MacroHotkeyConverter.ToMacroHotkeySettings(original)!;
        var restored = MacroHotkeyConverter.ToHotkeySettings(macroHotkey)!;

        Assert.AreEqual(original.Win,   restored.Win);
        Assert.AreEqual(original.Ctrl,  restored.Ctrl);
        Assert.AreEqual(original.Alt,   restored.Alt);
        Assert.AreEqual(original.Shift, restored.Shift);
        Assert.AreEqual(original.Code,  restored.Code);
    }

    [TestMethod]
    public void RoundTrip_WinAltA()
    {
        var original = new HotkeySettings(true, false, true, false, 0x41); // Win+Alt+A
        var macroHotkey = MacroHotkeyConverter.ToMacroHotkeySettings(original)!;
        var restored = MacroHotkeyConverter.ToHotkeySettings(macroHotkey)!;

        Assert.AreEqual(original.Win,   restored.Win);
        Assert.AreEqual(original.Ctrl,  restored.Ctrl);
        Assert.AreEqual(original.Alt,   restored.Alt);
        Assert.AreEqual(original.Shift, restored.Shift);
        Assert.AreEqual(original.Code,  restored.Code);
    }
}
