// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CmdPal.Core.Common.Services;
using Microsoft.CmdPal.Ext.Shell.Pages;
using Microsoft.CmdPal.Ext.UnitTestBase;
using Microsoft.CommandPalette.Extensions;
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
    [DataRow("\"C:\\Program Files\\Windows Defender\\MsMpEng.exe\"", "MsMpEng.exe")]
    [DataRow("C:\\Program Files\\Windows Defender\\MsMpEng.exe", "MsMpEng.exe")]
    public async Task QueryWithoutHistoryCommand(string command, string exeName)
    {
        // Setup
        var settings = Settings.CreateDefaultSettings();
        var mockHistory = CreateMockHistoryService();

        var pages = new ShellListPage(settings, mockHistory.Object, telemetryService: null);

        await UpdatePageAndWaitForItems(pages, () =>
        {
            // Test: Search for a command that exists in history
            pages.UpdateSearchText(string.Empty, command);
        });

        var commandList = pages.GetItems();

        Assert.AreEqual(1, commandList.Length);

        var listItem = commandList.FirstOrDefault();
        Assert.IsNotNull(listItem);

        var runExeListItem = listItem as RunExeItem;
        Assert.IsNotNull(runExeListItem);
        Assert.AreEqual(exeName, runExeListItem.Exe);
        Assert.IsTrue(listItem.Title.Contains(exeName), $"expect ${exeName} but got ${listItem.Title}");
        Assert.IsNotNull(listItem.Icon);
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

        var pages = new ShellListPage(settings, mockHistoryService.Object, telemetryService: null);

        await UpdatePageAndWaitForItems(pages, () =>
        {
            // Test: Search for a command that exists in history
            pages.UpdateSearchText(string.Empty, command);
        });

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

        var pages = new ShellListPage(settings, mockHistoryService.Object, telemetryService: null);

        await UpdatePageAndWaitForItems(pages, () =>
        {
            // Test: Search for a command that exists in history
            pages.UpdateSearchText("abcdefg", string.Empty);
        });

        var commandList = pages.GetItems();

        // Should find at least the ping command from history
        Assert.IsTrue(commandList.Length > 1);
    }

    [TestMethod]
    public async Task TestCacheBackToSameDirectory()
    {
        // Setup
        var settings = Settings.CreateDefaultSettings();
        var mockHistoryService = CreateMockHistoryService();

        var page = new ShellListPage(settings, mockHistoryService.Object, telemetryService: null);

        // Load up everything in c:\, for the sake of comparing:
        var filesInC = Directory.EnumerateFileSystemEntries("C:\\");

        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "c:\\"; });

        var commandList = page.GetItems();

        // Should find only items for what's in c:\
        Assert.IsTrue(commandList.Length == filesInC.Count());

        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "c:\\Win"; });
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "c:\\Windows"; });
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "c:\\"; });

        commandList = page.GetItems();

        // Should still find everything
        Assert.IsTrue(commandList.Length == filesInC.Count());

        await TypeStringIntoPage(page, "c:\\Windows\\Pro");
        await BackspaceSearchText(page, "c:\\Windows\\Pro", 3); // 3 characters for c:\

        commandList = page.GetItems();

        // Should still find everything
        Assert.IsTrue(commandList.Length == filesInC.Count());
    }

    private async Task TypeStringIntoPage(IDynamicListPage page, string searchText)
    {
        // type the string one character at a time
        for (var i = 0; i < searchText.Length; i++)
        {
            var substr = searchText[..i];
            await UpdatePageAndWaitForItems(page, () => { page.SearchText = substr; });
        }
    }

    private async Task BackspaceSearchText(IDynamicListPage page, string originalSearchText, int finalStringLength)
    {
        var originalLength = originalSearchText.Length;
        for (var i = originalLength; i >= finalStringLength; i--)
        {
            var substr = originalSearchText[..i];
            await UpdatePageAndWaitForItems(page, () => { page.SearchText = substr; });
        }
    }

    [TestMethod]
    public async Task TestCacheSameDirectorySlashy()
    {
        // Setup
        var settings = Settings.CreateDefaultSettings();
        var mockHistoryService = CreateMockHistoryService();

        var page = new ShellListPage(settings, mockHistoryService.Object, telemetryService: null);

        // Load up everything in c:\, for the sake of comparing:
        var filesInC = Directory.EnumerateFileSystemEntries("C:\\");
        var filesInWindows = Directory.EnumerateFileSystemEntries("C:\\Windows");
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "c:\\"; });

        var commandList = page.GetItems();
        Assert.IsTrue(commandList.Length == filesInC.Count());

        // First navigate to c:\Windows. This should match everything that matches "windows" inside of C:\
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "c:\\Windows"; });
        var cWindowsCommandsPre = page.GetItems();

        // Then go into c:\windows\. This will only have the results in c:\windows\
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "c:\\Windows\\"; });
        var windowsCommands = page.GetItems();
        Assert.IsTrue(windowsCommands.Length != cWindowsCommandsPre.Length);

        // now go back to c:\windows. This should match the results from the last time we entered this string
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "c:\\Windows"; });
        var cWindowsCommandsPost = page.GetItems();
        Assert.IsTrue(cWindowsCommandsPre.Length == cWindowsCommandsPost.Length);
    }

    [TestMethod]
    public async Task TestPathWithSpaces()
    {
        // Setup
        var settings = Settings.CreateDefaultSettings();
        var mockHistoryService = CreateMockHistoryService();

        var page = new ShellListPage(settings, mockHistoryService.Object, telemetryService: null);

        // Load up everything in c:\, for the sake of comparing:
        var filesInC = Directory.EnumerateFileSystemEntries("C:\\");
        var filesInProgramFiles = Directory.EnumerateFileSystemEntries("C:\\Program Files");
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "c:\\Program Files\\"; });

        var commandList = page.GetItems();
        Assert.IsTrue(commandList.Length == filesInProgramFiles.Count());
    }

    [TestMethod]
    public async Task TestNoWrapSuggestionsWithSpaces()
    {
        // Setup
        var settings = Settings.CreateDefaultSettings();
        var mockHistoryService = CreateMockHistoryService();

        var page = new ShellListPage(settings, mockHistoryService.Object, telemetryService: null);

        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "c:\\Program Files\\"; });

        var commandList = page.GetItems();

        foreach (var item in commandList)
        {
            Assert.IsTrue(!string.IsNullOrEmpty(item.TextToSuggest));
            Assert.IsFalse(item.TextToSuggest.StartsWith('"'));
        }
    }
}
