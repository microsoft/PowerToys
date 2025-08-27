// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.Ext.Shell.Pages;
using Microsoft.CmdPal.Ext.UnitTestBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.Ext.Shell.UnitTests;

[TestClass]
public class QueryTests : CommandPaletteUnitTestBase
{
    private static Mock<IRunHistoryService> CreateMockHistoryService(IList<string> historyItems = null)
    {
        var mockHistoryService = new Mock<IRunHistoryService>();
        var history = historyItems ?? new List<string>();

        mockHistoryService.Setup(x => x.GetRunHistory())
                         .Returns(() => history.ToList().AsReadOnly());

        mockHistoryService.Setup(x => x.AddRunHistoryItem(It.IsAny<string>()))
                         .Callback<string>(item =>
                         {
                             if (!string.IsNullOrWhiteSpace(item))
                             {
                                 history.Remove(item);
                                 history.Insert(0, item);
                             }
                         });

        mockHistoryService.Setup(x => x.ClearRunHistory())
                         .Callback(() => history.Clear());

        return mockHistoryService;
    }

    private static Mock<IRunHistoryService> CreateMockHistoryServiceWithCommonCommands()
    {
        var commonCommands = new List<string>
        {
            "ping google.com",
            "ipconfig /all",
            "curl https://api.github.com",
            "dir",
            "cd ..",
            "git status",
            "npm install",
            "python --version",
        };

        return CreateMockHistoryService(commonCommands);
    }

    [TestMethod]
    public void ValidateHistoryFunctionality()
    {
        // Setup
        var settings = Settings.CreateDefaultSettings();

        // Act
        settings.AddCmdHistory("test-command");

        // Assert
        Assert.AreEqual(1, settings.Count["test-command"]);
    }

    [TestMethod]
    [DataRow("ping bing.com", "ping.exe")]
    [DataRow("curl bing.com", "curl.exe")]
    [DataRow("ipconfig /all", "ipconfig.exe")]
    public async Task QueryWithoutHistoryCommand(string command, string exeName)
    {
        // Setup
        var settings = Settings.CreateDefaultSettings();
        var mockHistory = CreateMockHistoryService();

        var pages = new ShellListPage(settings, mockHistory.Object);

        pages.UpdateSearchText(string.Empty, command);

        // wait for about 1s.
        await Task.Delay(1000);

        var commandList = pages.GetItems();

        Assert.AreEqual(1, commandList.Length);

        var executeCommand = commandList.FirstOrDefault();
        Assert.IsNotNull(executeCommand);
        Assert.IsNotNull(executeCommand.Icon);
        Assert.IsTrue(executeCommand.Title.Contains(exeName), $"expect ${exeName} but got ${executeCommand.Title}");
    }

    [TestMethod]
    [DataRow("ping bing.com", "ping.exe")]
    [DataRow("curl bing.com", "curl.exe")]
    [DataRow("ipconfig /all", "ipconfig.exe")]
    public async Task QueryWithHistoryCommands(string command, string exeName)
    {
        // Setup
        var settings = Settings.CreateDefaultSettings();
        var mockHistoryService = CreateMockHistoryServiceWithCommonCommands();

        var pages = new ShellListPage(settings, mockHistoryService.Object);

        // Test: Search for a command that exists in history
        pages.UpdateSearchText(string.Empty, command);

        await Task.Delay(1000);

        var commandList = pages.GetItems();

        // Should find at least the ping command from history
        Assert.IsTrue(commandList.Length > 1);

        var expectedCommand = commandList.FirstOrDefault();
        Assert.IsNotNull(expectedCommand);
        Assert.IsNotNull(expectedCommand.Icon);
        Assert.IsTrue(expectedCommand.Title.Contains(exeName), $"expect ${exeName} but got ${expectedCommand.Title}");
    }

    [TestMethod]
    public async Task EmptyQueryWithHistoryCommands()
    {
        // Setup
        var settings = Settings.CreateDefaultSettings();
        var mockHistoryService = CreateMockHistoryServiceWithCommonCommands();

        var pages = new ShellListPage(settings, mockHistoryService.Object);

        pages.UpdateSearchText("abcdefg", string.Empty);

        await Task.Delay(1000);

        var commandList = pages.GetItems();

        // Should find at least the ping command from history
        Assert.IsTrue(commandList.Length > 1);
    }
}
