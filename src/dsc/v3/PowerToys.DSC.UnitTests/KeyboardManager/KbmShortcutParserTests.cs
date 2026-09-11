// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.DSC.Models.KeyboardManager;

namespace PowerToys.DSC.UnitTests.KeyboardManager;

[TestClass]
public sealed class KbmShortcutParserTests
{
    [TestMethod]
    [DataRow("Ctrl+Shift+A", "17;16;65")]
    [DataRow("LCtrl+RAlt+F5", "162;165;116")]
    [DataRow("Win+Ctrl+Alt+Shift+Delete", "260;17;18;16;46")]
    [DataRow("Esc", "27")]
    [DataRow("Ctrl+Alt+VK255", "17;18;255")]
    public void ParseKeyOrShortcut_ProducesStoredVkString(string input, string expected)
    {
        Assert.IsTrue(KbmShortcutParser.TryParseKeyOrShortcut(input, out var result, out var error), error);
        Assert.AreEqual(expected, result.ToVkString());
        Assert.AreEqual(0u, result.SecondKeyOfChord);
    }

    [TestMethod]
    [DataRow("Shift+Alt+Ctrl+Win+A", "260;17;18;16;65")]
    [DataRow("shift+ctrl+a", "17;16;65")]
    public void ParseKeyOrShortcut_ModifierOrderIsCanonical(string input, string expected)
    {
        // Any authored modifier order serializes as Win, Ctrl, Alt, Shift
        // (the order used by Shortcut::ToHstringVK)
        Assert.IsTrue(KbmShortcutParser.TryParseKeyOrShortcut(input, out var result, out var error), error);
        Assert.AreEqual(expected, result.ToVkString());
    }

    [TestMethod]
    public void ParseKeyOrShortcut_Chord()
    {
        Assert.IsTrue(KbmShortcutParser.TryParseKeyOrShortcut("Win+O, K", out var result, out var error), error);
        Assert.AreEqual("260;79;75", result.ToVkString());
        Assert.AreEqual(75u, result.SecondKeyOfChord);
    }

    [TestMethod]
    [DataRow("A+B", "more than one action key")]
    [DataRow("Ctrl+LCtrl+A", "repeats the Ctrl modifier")]
    [DataRow("Ctrl++A", "empty key part")]
    [DataRow("Win+O, K+L", "must be a single key")]
    [DataRow("Win+O, K, L", "more than one chord separator")]
    [DataRow("Ctrl+Shift", "no action key")]
    [DataRow("Ctrl+Foo", "Invalid key name 'Foo'")]
    [DataRow("O, K", "requires at least one modifier")]
    [DataRow("Win+O, Ctrl", "cannot be a modifier")]
    [DataRow("", "empty")]
    public void ParseKeyOrShortcut_InvalidInput_Fails(string input, string expectedErrorFragment)
    {
        Assert.IsFalse(KbmShortcutParser.TryParseKeyOrShortcut(input, out _, out var error));
        StringAssert.Contains(error, expectedErrorFragment);
    }

    [TestMethod]
    public void ParseKey_SingleKeyOnly()
    {
        Assert.IsTrue(KbmShortcutParser.TryParseKey("CapsLock", out var result, out var error), error);
        Assert.AreEqual("20", result.ToVkString());
        Assert.IsTrue(result.IsSingleKey);

        // A lone modifier is a valid single key (e.g. remapping CapsLock to LCtrl)
        Assert.IsTrue(KbmShortcutParser.TryParseKey("LCtrl", out result, out error), error);
        Assert.AreEqual("162", result.ToVkString());

        Assert.IsFalse(KbmShortcutParser.TryParseKey("Nope", out _, out _));
        Assert.IsFalse(KbmShortcutParser.TryParseKey(null, out _, out _));
    }

    [TestMethod]
    public void ParseVkString_RoundTrips()
    {
        Assert.IsTrue(KbmShortcutParser.TryParseVkString("162;65", 0, out var result));
        Assert.AreEqual("162;65", result.ToVkString());
        Assert.AreEqual(0u, result.SecondKeyOfChord);

        Assert.IsTrue(KbmShortcutParser.TryParseVkString("260;79;75", 75, out result));
        Assert.AreEqual(75u, result.SecondKeyOfChord);

        Assert.IsFalse(KbmShortcutParser.TryParseVkString("162;abc", 0, out _));
        Assert.IsFalse(KbmShortcutParser.TryParseVkString(string.Empty, 0, out _));
        Assert.IsFalse(KbmShortcutParser.TryParseVkString(null, 0, out _));
    }

    [TestMethod]
    public void Format_ProducesCanonicalFriendlyString()
    {
        Assert.IsTrue(KbmShortcutParser.TryParseKeyOrShortcut("shift+ctrl+a", out var result, out _));
        Assert.AreEqual("Ctrl+Shift+A", KbmShortcutParser.Format(result));

        Assert.IsTrue(KbmShortcutParser.TryParseKeyOrShortcut("Win+O, K", out result, out _));
        Assert.AreEqual("Win+O, K", KbmShortcutParser.Format(result));
    }

    [TestMethod]
    public void Canonicalize_ReordersStoredKeys()
    {
        // Hand-edited profiles may store modifiers in a non-canonical order
        Assert.IsTrue(KbmShortcutParser.TryParseVkString("16;17;65", 0, out var result));
        var canonical = KbmShortcutParser.Canonicalize(result);
        Assert.AreEqual("17;16;65", canonical.ToVkString());
        Assert.AreEqual("Ctrl+Shift+A", KbmShortcutParser.Format(canonical));
    }
}
