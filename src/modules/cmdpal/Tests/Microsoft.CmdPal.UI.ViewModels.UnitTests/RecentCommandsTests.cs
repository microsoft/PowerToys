// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CmdPal.Ext.UnitTestBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class RecentCommandsTests : CommandPaletteUnitTestBase
{
    private static RecentCommandsManager CreateHistory(IList<string> commandIds = null)
    {
        var history = new RecentCommandsManager();
        if (commandIds != null)
        {
            foreach (var item in commandIds)
            {
                history.AddHistoryItem(item);
            }
        }

        return history;
    }

    private static RecentCommandsManager CreateMockHistoryServiceWithCommonCommands()
    {
        var commonCommands = new List<string>
        {
            "com.microsoft.cmdpal.shell",
            "com.microsoft.cmdpal.windowwalker",
            "Visual Studio 2022 Preview_6533433915015224980",
            "com.microsoft.cmdpal.reload",
            "com.microsoft.cmdpal.shell",
        };

        return CreateHistory(commonCommands);
    }

    [TestMethod]
    public void ValidateHistoryFunctionality()
    {
        // Setup
        var history = CreateHistory();

        // Act
        history.AddHistoryItem("com.microsoft.cmdpal.shell");

        // Assert
        Assert.IsTrue(history.GetCommandHistoryWeight("com.microsoft.cmdpal.shell") > 0);
    }
}
