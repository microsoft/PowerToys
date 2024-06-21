// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Settings.UI.UnitTests.Settings;

[TestClass]
public class ICmdReprParsableTests
{
    [TestMethod]
    public void KeyboardKeysPropertyParsing()
    {
        {
            Assert.IsTrue(KeyboardKeysProperty.TryParseFromCmd("win+ctrl+Alt+sHifT+Q", out var hotkey));

            Assert.AreEqual(new KeyboardKeysProperty { Value = new HotkeySettings(true, true, true, true, 0x51) }, hotkey);
        }

        {
            Assert.IsTrue(KeyboardKeysProperty.TryParseFromCmd("CTRL+z", out var hotkey));
            Assert.AreEqual(new KeyboardKeysProperty { Value = new HotkeySettings(false, true, false, false, 0x5A) }, hotkey);
        }

        {
            Assert.IsTrue(KeyboardKeysProperty.TryParseFromCmd("shift+ALT+0x59", out var hotkey));
            Assert.AreEqual(new KeyboardKeysProperty { Value = new HotkeySettings(false, false, true, true, 0x59) }, hotkey);
        }

        {
            Assert.IsTrue(KeyboardKeysProperty.TryParseFromCmd("alt+Space", out var hotkey));
            Assert.AreEqual(new KeyboardKeysProperty { Value = new HotkeySettings(false, false, true, false, 0x20) }, hotkey);
        }
    }

    [TestMethod]
    public void BoolPropertyParsing()
    {
        {
            Assert.IsTrue(BoolProperty.TryParseFromCmd("True", out var result));
            Assert.AreEqual(new BoolProperty(true), result);
        }

        {
            Assert.IsTrue(BoolProperty.TryParseFromCmd("false", out var result));
            Assert.AreEqual(new BoolProperty(false), result);
        }
    }

    [TestMethod]
    public void IntPropertyParsing()
    {
        {
            Assert.IsTrue(IntProperty.TryParseFromCmd("123", out var result));
            Assert.AreEqual(new IntProperty(123), result);
        }

        {
            Assert.IsTrue(IntProperty.TryParseFromCmd("1500", out var result));
            Assert.AreEqual(new IntProperty(1500), result);
            Assert.AreNotEqual(new IntProperty(15), result);
        }
    }

    [TestMethod]
    public void MouseJumpThumbnailSizeParsing()
    {
        {
            Assert.IsTrue(MouseJumpThumbnailSize.TryParseFromCmd("1920x1080", out var result));
            Assert.AreEqual(new MouseJumpThumbnailSize { Width = 1920, Height = 1080 }, result);
        }
    }
}
