// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public sealed class HotkeyWindowMessageHelperTests
{
    [TestMethod]
    [DataRow(false, false, true, false, 1)]
    [DataRow(false, true, false, false, 2)]
    [DataRow(false, false, false, true, 4)]
    [DataRow(true, false, false, false, 8)]
    [DataRow(true, true, true, true, 15)]
    public void MatchingKeyAndModifiers_ReturnCommand(bool win, bool ctrl, bool alt, bool shift, int modifiers)
    {
        var key = new HotkeySettings(win, ctrl, alt, shift, 65);
        Dictionary<int, TopLevelHotkey> registered = new() { [7] = new(key, string.Empty) };

        Assert.AreEqual(modifiers, (int)HotkeyWindowMessageHelper.GetModifiers(key));
        Assert.AreEqual(string.Empty, HotkeyWindowMessageHelper.GetCommand(registered, 7, (65 << 16) | modifiers));
    }

    [TestMethod]
    [DataRow(-1, 65, 2)]
    [DataRow(8, 65, 2)]
    [DataRow(7, 66, 2)]
    [DataRow(7, 65, 4)]
    public void UnknownIdOrMismatchedPayload_IsIgnored(int id, int code, int modifiers)
    {
        Dictionary<int, TopLevelHotkey> registered = new() { [7] = new(new HotkeySettings { Ctrl = true, Code = 65 }, "command") };

        Assert.IsNull(HotkeyWindowMessageHelper.GetCommand(registered, id, (code << 16) | modifiers));
    }

    [TestMethod]
    public void RemovedBindingId_DoesNotDispatchReplacementCommand()
    {
        var key = new HotkeySettings { Ctrl = true, Code = 65 };
        Dictionary<int, TopLevelHotkey> registered = new() { [1] = new(key, "original") };
        registered.Remove(1);
        registered.Add(2, new(key, "replacement"));

        Assert.IsNull(HotkeyWindowMessageHelper.GetCommand(registered, 1, (65 << 16) | 2));
        Assert.AreEqual("replacement", HotkeyWindowMessageHelper.GetCommand(registered, 2, (65 << 16) | 2));
    }
}
