// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.DSC.Models.KeyboardManager;

namespace PowerToys.DSC.UnitTests.KeyboardManager;

[TestClass]
public sealed class KbmKeyNamesTests
{
    private static readonly uint[] _numpadExtendedBaseCodes = [13, 33, 34, 35, 36, 37, 38, 39, 40, 45, 46, 111];

    [TestMethod]
    public void RoundTrip_AllStorableCodes()
    {
        // Every code Keyboard Manager can store must round-trip exactly:
        // ToVk(ToName(code)) == code, independent of layout or culture.
        var codes = new List<uint>();
        for (var code = 1u; code <= 255u; code++)
        {
            codes.Add(code);
        }

        codes.Add(KbmKeyNames.VkDisabled);
        codes.Add(KbmKeyNames.VkWinBoth);
        foreach (var baseCode in _numpadExtendedBaseCodes)
        {
            codes.Add(baseCode | KbmKeyNames.NumpadOriginBit);
        }

        foreach (var code in codes)
        {
            var name = KbmKeyNames.GetName(code);
            Assert.IsTrue(KbmKeyNames.TryGetCode(name, out var roundTripped), $"Name '{name}' for code {code} did not parse");
            Assert.AreEqual(code, roundTripped, $"Code {code} round-tripped to {roundTripped} via '{name}'");
        }
    }

    [TestMethod]
    public void GetName_CanonicalNames()
    {
        Assert.AreEqual("CapsLock", KbmKeyNames.GetName(20));
        Assert.AreEqual("Esc", KbmKeyNames.GetName(27));
        Assert.AreEqual("A", KbmKeyNames.GetName(65));
        Assert.AreEqual("F24", KbmKeyNames.GetName(135));
        Assert.AreEqual("Ctrl", KbmKeyNames.GetName(17));
        Assert.AreEqual("LShift", KbmKeyNames.GetName(160));
        Assert.AreEqual("Win", KbmKeyNames.GetName(KbmKeyNames.VkWinBoth));
        Assert.AreEqual("Disable", KbmKeyNames.GetName(KbmKeyNames.VkDisabled));
        Assert.AreEqual("NumPad0", KbmKeyNames.GetName(96));
        Assert.AreEqual("NumPadEnter", KbmKeyNames.GetName(13 | KbmKeyNames.NumpadOriginBit));
        Assert.AreEqual("Semicolon", KbmKeyNames.GetName(186));
    }

    [TestMethod]
    public void GetName_UnknownCode_FallsBackToVkLiteral()
    {
        Assert.AreEqual("VK1000", KbmKeyNames.GetName(1000));
    }

    [TestMethod]
    public void TryGetCode_Aliases()
    {
        var aliases = new Dictionary<string, uint>
        {
            ["Escape"] = 27,
            ["Return"] = 13,
            ["Control"] = 17,
            ["Windows"] = KbmKeyNames.VkWinBoth,
            ["PageUp"] = 33,
            ["PageDown"] = 34,
            [";"] = 186,
            ["="] = 187,
            ["/"] = 191,
            ["'"] = 222,
            ["VK44"] = 44,
            ["0x2C"] = 44,
            ["0x2c"] = 44,
        };

        foreach (var (alias, expected) in aliases)
        {
            Assert.IsTrue(KbmKeyNames.TryGetCode(alias, out var code), $"Alias '{alias}' did not parse");
            Assert.AreEqual(expected, code, $"Alias '{alias}'");
        }
    }

    [TestMethod]
    public void TryGetCode_IsCaseInsensitive()
    {
        Assert.IsTrue(KbmKeyNames.TryGetCode("capslock", out var code));
        Assert.AreEqual(20u, code);
        Assert.IsTrue(KbmKeyNames.TryGetCode("NUMPADENTER", out code));
        Assert.AreEqual(13 | KbmKeyNames.NumpadOriginBit, code);
    }

    [TestMethod]
    public void TryGetCode_TrimsWhitespace()
    {
        Assert.IsTrue(KbmKeyNames.TryGetCode(" Esc ", out var code));
        Assert.AreEqual(27u, code);
    }

    [TestMethod]
    public void TryGetCode_UnknownName_Fails()
    {
        Assert.IsFalse(KbmKeyNames.TryGetCode("CapsLok", out _));
        Assert.IsFalse(KbmKeyNames.TryGetCode(string.Empty, out _));
        Assert.IsFalse(KbmKeyNames.TryGetCode(null, out _));
        Assert.IsFalse(KbmKeyNames.TryGetCode("VK", out _));
        Assert.IsFalse(KbmKeyNames.TryGetCode("VK0", out _));
        Assert.IsFalse(KbmKeyNames.TryGetCode("0x", out _));
    }

    [TestMethod]
    public void GetModifierClass_ClassifiesAllModifiers()
    {
        Assert.AreEqual(KbmKeyNames.ModifierClass.Win, KbmKeyNames.GetModifierClass(KbmKeyNames.VkWinBoth));
        Assert.AreEqual(KbmKeyNames.ModifierClass.Win, KbmKeyNames.GetModifierClass(91));
        Assert.AreEqual(KbmKeyNames.ModifierClass.Win, KbmKeyNames.GetModifierClass(92));
        Assert.AreEqual(KbmKeyNames.ModifierClass.Ctrl, KbmKeyNames.GetModifierClass(17));
        Assert.AreEqual(KbmKeyNames.ModifierClass.Ctrl, KbmKeyNames.GetModifierClass(162));
        Assert.AreEqual(KbmKeyNames.ModifierClass.Ctrl, KbmKeyNames.GetModifierClass(163));
        Assert.AreEqual(KbmKeyNames.ModifierClass.Alt, KbmKeyNames.GetModifierClass(18));
        Assert.AreEqual(KbmKeyNames.ModifierClass.Alt, KbmKeyNames.GetModifierClass(164));
        Assert.AreEqual(KbmKeyNames.ModifierClass.Alt, KbmKeyNames.GetModifierClass(165));
        Assert.AreEqual(KbmKeyNames.ModifierClass.Shift, KbmKeyNames.GetModifierClass(16));
        Assert.AreEqual(KbmKeyNames.ModifierClass.Shift, KbmKeyNames.GetModifierClass(160));
        Assert.AreEqual(KbmKeyNames.ModifierClass.Shift, KbmKeyNames.GetModifierClass(161));
        Assert.AreEqual(KbmKeyNames.ModifierClass.None, KbmKeyNames.GetModifierClass(65));
    }
}
