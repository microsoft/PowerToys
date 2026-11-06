// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed class HotkeySettingsTests
{
    [TestMethod]
    public void HasSameKeyCombination_IgnoresDisplayTextWithoutChangingRecordEquality()
    {
        var shortcut = new HotkeySettings { Ctrl = true, Code = 65, Key = "A" };
        var sameKeys = shortcut with { Key = string.Empty };

        Assert.IsTrue(shortcut.HasSameKeyCombination(sameKeys));
        Assert.IsTrue(sameKeys.HasSameKeyCombination(shortcut));
        Assert.AreNotEqual(shortcut, sameKeys);
    }

    [TestMethod]
    public void HasSameKeyCombination_DoesNotMatchNull()
    {
        Assert.IsFalse(new HotkeySettings().HasSameKeyCombination(null));
    }
}
