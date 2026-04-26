// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.MacroEngine;

namespace PowerToys.MacroEngine.Tests;

[TestClass]
public sealed class HotkeyManagerTests
{
    [TestMethod]
    public void KeyParser_IntegrationWithRegisterHotkey_ParsesBeforeRegistering()
    {
        Assert.ThrowsException<ArgumentException>(() => KeyParser.ParseHotkey("Ctrl+Shift"));
        var (mods, vk) = KeyParser.ParseHotkey("Ctrl+Shift+F9");
        Assert.AreNotEqual(0u, mods);
        Assert.AreEqual((ushort)0x78, vk); // F9 = 0x78
    }

    [TestMethod]
    public void HotkeyManager_Dispose_DoesNotThrow()
    {
        using var mgr = new HotkeyManager();

        // Dispose before Start should be safe (no STA thread running)
    }
}
