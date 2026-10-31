// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITestAutomationNext.UnitTests;

[TestClass]
public sealed class SessionHelperTests
{
    [TestMethod]
    [DataRow("Microsoft.CmdPal.UI", "Command Palette", true)]
    [DataRow("microsoft.cmdpal.ui", "Command Palette", true)]
    [DataRow("msedge", "Command Palette documentation", false)]
    [DataRow("Microsoft.CmdPal.UI", "PopupHost", false)]
    public void MainWindowRequiresBothOwnerAndTitle(string process, string title, bool expected)
    {
        Assert.AreEqual(
            expected,
            SessionHelper.MatchesMainWindow("Microsoft.CmdPal.UI", "Command Palette", process, title));
    }
}
