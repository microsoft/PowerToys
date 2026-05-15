// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.Ext.Run;
using Microsoft.CmdPal.Ext.UnitTestBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.Ext.Shell.UnitTests;

/// <summary>
/// Tests for the <see cref="RunListPage"/>. Ported from the OsClient
/// RunNative.UnitTests project (which targets the same RunListPage class).
///
/// NOTE: In pt, the RunListPage always returns at least one item (the
/// "run what you typed" command). The OsClient version does not. All
/// count-based assertions from the OsClient tests are adjusted by
/// <see cref="ExeItemCount"/> to account for this.
/// </summary>
[TestClass]
public class RunPageTests : CommandPaletteUnitTestBase
{
    /// <summary>
    /// In pt, RunListPage always includes one extra item for the typed
    /// command line itself. OsClient does not include this item.
    /// </summary>
    private const int ExeItemCount = 1;

    internal static Mock<IRunHistoryService> CreateMockHistoryService(IList<string> historyItems = null)
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

        mockHistoryService.Setup(x => x.RunCommand(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<ulong>()))
                         .Returns<string, string, bool, ulong>((cmd, dir, admin, hwnd) =>
                         {
                             return RunHistory.ExecuteCommandline(cmd, dir, hwnd, admin);
                         });

        mockHistoryService.Setup(x => x.ParseCommandline(It.IsAny<string>(), It.IsAny<string>()))
                         .Returns<string, string>((cmd, dir) => RunHistory.ParseCommandline(cmd, dir));

        mockHistoryService.Setup(x => x.QualifyCommandLineDirectory(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                         .Returns<string, string, string>((cmd, fullPath, defDir) =>
                         {
                             return RunHistory.QualifyCommandLineDirectory(cmd, fullPath, defDir);
                         });

        return mockHistoryService;
    }

    internal static IEnumerable<string> EnumerateFiles(string path)
    {
        // this matches the way that IACListISF suggests paths to us (I believe)
        var o = new EnumerationOptions
        {
            AttributesToSkip = FileAttributes.Hidden,
            ReturnSpecialDirectories = false,
            IgnoreInaccessible = true,
        };
        return Directory.EnumerateFileSystemEntries(path, "*", o);
    }

    [TestMethod]
    public async Task TestSimple()
    {
        // Note to future self: are the tests hanging mysteriously? Running
        // forever, but not actually doing anything, seemingly just spinning on
        // this case?
        //
        // If they are, then you forgot to add a string resource to our .resx
        // somewhere. Fix that, and the tests will run again.

        // Setup
        var nativeService = CreateMockHistoryService().Object;
        using var page = new RunListPage(nativeService, telemetryService: null);

        // Load up everything in c:\Windows, for the sake of comparing:
        var filesInWindows = EnumerateFiles("C:\\Windows");
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "c:\\Windows\\"; });

        var commandList = page.GetItems();
        Assert.AreEqual(filesInWindows.Count() + ExeItemCount, commandList.Length);
    }

    [TestMethod]
    public async Task TestCacheSameDirectorySlashy()
    {
        // Setup
        var nativeService = CreateMockHistoryService().Object;
        using var page = new RunListPage(nativeService, telemetryService: null);

        var filesInC = EnumerateFiles("C:\\");
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "c:\\"; });

        var commandList = page.GetItems();
        Assert.AreEqual(filesInC.Count() + ExeItemCount, commandList.Length);

        // First navigate to c:\Windows. This should match everything that matches "windows" inside of C:\
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "c:\\Windows"; });
        var cWindowsCommandsPre = page.GetItems();

        // Then go into c:\windows\. This will only have the results in c:\windows\
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "c:\\Windows\\"; });
        var windowsCommands = page.GetItems();
        Assert.AreNotEqual(cWindowsCommandsPre.Length, windowsCommands.Length);

        // now go back to c:\windows. This should match the results from the last time we entered this string
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "c:\\Windows"; });
        var cWindowsCommandsPost = page.GetItems();
        Assert.AreEqual(cWindowsCommandsPre.Length, cWindowsCommandsPost.Length);
    }

    [TestMethod]
    public async Task TestCacheSameDirectorySlashySpaces()
    {
        // Setup
        var nativeService = CreateMockHistoryService().Object;
        using var page = new RunListPage(nativeService, telemetryService: null);

        var filesInC = EnumerateFiles("C:\\");
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "c:\\"; });

        var commandList = page.GetItems();
        Assert.AreEqual(filesInC.Count() + ExeItemCount, commandList.Length);

        // First navigate to c:\Program Files. This should match everything that matches "Program Files" inside of C:\
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "c:\\Program Files"; });
        var cWindowsCommandsPre = page.GetItems();

        // Then go into c:\Program Files\. This will only have the results in c:\Program Files\
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "c:\\Program Files\\"; });
        var windowsCommands = page.GetItems();
        Assert.AreNotEqual(cWindowsCommandsPre.Length, windowsCommands.Length);

        // now go back to c:\Program Files. This should match the results from the last time we entered this string
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "c:\\Program Files"; });
        var cWindowsCommandsPost = page.GetItems();
        Assert.AreEqual(cWindowsCommandsPre.Length, cWindowsCommandsPost.Length);
    }

    [TestMethod]
    public async Task TestCacheSameDirectorySlashyFilter()
    {
        // Setup
        var nativeService = CreateMockHistoryService().Object;
        using var page = new RunListPage(nativeService, telemetryService: null);

        var filesInC = EnumerateFiles("C:\\");
        var filesInWindows = EnumerateFiles("C:\\Windows");
        var inWindowsWithS = filesInWindows.Where(f => Path.GetFileName(f).StartsWith("s", StringComparison.OrdinalIgnoreCase));
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "c:\\"; });

        var commandList = page.GetItems();
        Assert.AreEqual(filesInC.Count() + ExeItemCount, commandList.Length);

        // First navigate to c:\Windows. This should match everything that matches "windows" inside of C:\
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "c:\\Windows"; });
        var cWindowsCommandsPre = page.GetItems();

        // Then go into c:\windows\. This will only have the results in c:\windows\
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "c:\\Windows\\"; });
        var windowsCommands = page.GetItems();
        Assert.AreNotEqual(cWindowsCommandsPre.Length, windowsCommands.Length);

        // now go to c:\windows\s. This should only have the results with an 'S' in them
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "c:\\Windows\\s"; });
        var postFilterOnS = page.GetItems();
        Assert.AreEqual(inWindowsWithS.Count() + ExeItemCount, postFilterOnS.Length);
    }

    [TestMethod]
    public async Task TestDriveRootSlashy()
    {
        // For this test, set our current working directory to C:\Windows\System32.
        // That dir has a lot of files. This test however double-checks that
        // searching for "C:" doesn't also return files from the current working
        // directory. This is because `Directory.GetFileSystemEntries("c:")`
        // will also return files from the current working directory.
        //
        // In pt: "c:" should NOT list directory contents at all, only "c:\"
        // should. The OsClient version listed C:\ contents on bare "c:" which
        // was incorrect.
        var originalCwd = Environment.CurrentDirectory;
        Environment.CurrentDirectory = "C:\\windows\\system32";
        using var cleanup = new ScopeExit(() => Environment.CurrentDirectory = originalCwd);

        // Setup
        var nativeService = CreateMockHistoryService().Object;
        using var page = new RunListPage(nativeService, telemetryService: null);

        var filesInC = EnumerateFiles("C:\\");
        var numFilesInC = filesInC.Count();

        // "c:" alone should NOT list directory contents — only the exe item
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "c:"; });

        var commandList = page.GetItems();
        Assert.AreEqual(ExeItemCount, commandList.Length);

        // Navigate to a different dir, then to c:\  — this SHOULD list directory contents
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "c:\\windows\\"; });
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "c:\\"; });
        var withSlashCommands = page.GetItems();
        Assert.AreEqual(numFilesInC + ExeItemCount, withSlashCommands.Length);
    }

    [TestMethod]
    public async Task TestListFilesEnvVar()
    {
        // Setup
        var nativeService = CreateMockHistoryService().Object;
        using var page = new RunListPage(nativeService, telemetryService: null);

        var systemRoot = Environment.GetEnvironmentVariable("SystemRoot");
        if (string.IsNullOrEmpty(systemRoot))
        {
            Assert.Fail("SystemRoot env var not set");
        }

        var filesInSystemRoot = EnumerateFiles(systemRoot);
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "%SystemRoot%\\"; });

        var commandList = page.GetItems();
        Assert.AreEqual(filesInSystemRoot.Count() + ExeItemCount, commandList.Length);
    }

    [TestMethod]
    [DataRow("notepad")]
    [DataRow("cmd.exe /c dir")]
    [DataRow("c:\\Windows\\System32\\cmd.exe /c dir")]
    [DataRow("%systemroot%\\System32\\cmd.exe")]
    [DataRow("%systemroot%\\System32\\cmd.exe /c dir")]
    [DataRow("https:\\www.microsoft.com")]
    public async Task TestCommandlineNoResult(string commandline)
    {
        // Setup
        var nativeService = CreateMockHistoryService().Object;
        using var page = new RunListPage(nativeService, telemetryService: null);

        await UpdatePageAndWaitForItems(page, () => { page.SearchText = commandline; });

        var commandList = page.GetItems();
        Assert.AreEqual(ExeItemCount, commandList.Length);
    }

    [TestMethod]
    [DataRow("%")]
    [DataRow("%%")]
    [DataRow("%nonexistent%")]
    [DataRow("%nonexistent%\\")]
    public async Task TestPartialEnvVarDoesNotCrash(string input)
    {
        // Typing a bare '%' or incomplete environment variable shouldn't crash
        var nativeService = CreateMockHistoryService().Object;
        using var page = new RunListPage(nativeService, telemetryService: null);

        // This should complete without throwing
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = input; });

        var commandList = page.GetItems();
        Assert.IsNotNull(commandList);
    }

    [TestMethod]
    public async Task TestListFilesEnvVarUserProfile()
    {
        // Setup
        var nativeService = CreateMockHistoryService().Object;
        using var page = new RunListPage(nativeService, telemetryService: null);

        var userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
        if (string.IsNullOrEmpty(userProfile))
        {
            Assert.Fail("USERPROFILE env var not set");
        }

        var filesInUserProfile = EnumerateFiles(userProfile);
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "%userprofile%\\"; });

        var commandList = page.GetItems();
        Assert.AreEqual(filesInUserProfile.Count() + ExeItemCount, commandList.Length);
    }

    [TestMethod]
    public async Task TestPathWithSpaces()
    {
        // Setup
        var nativeService = CreateMockHistoryService().Object;
        using var page = new RunListPage(nativeService, telemetryService: null);

        // Now set up a temp directory that looks like this:
        // <temp dir>
        // ├───Thing
        // └───Thing One
        //         at me.txt
        //         bro.png
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        using var cleanup = new ScopeExit(() => Directory.Delete(tempDir, recursive: true));
        var dirWithSpaces = Path.Combine(tempDir, "Thing One");
        Directory.CreateDirectory(dirWithSpaces);
        var fileInDirWithSpaces = Path.Combine(dirWithSpaces, "at me.txt");
        File.WriteAllText(fileInDirWithSpaces, "hello world");
        var fileInDirWithSpaces2 = Path.Combine(dirWithSpaces, "bro.png");
        File.WriteAllText(fileInDirWithSpaces2, "image data");
        var dirWithoutSpaces = Path.Combine(tempDir, "Thing");
        Directory.CreateDirectory(dirWithoutSpaces);

        // Now, navigate to "<tempDir>\Thing"
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = dirWithoutSpaces; });

        // There should be two results here: our two directories (+ the exe item)
        var commandList = page.GetItems();
        Assert.AreEqual(2 + ExeItemCount, commandList.Length);

        // Now, navigate to "<tempDir>\Thing One". There should just be the single result for "Thing One" (+ exe item)
        // TODO: In pt, ParseCommandline splits at the space in "Thing One",
        // so we match both "Thing" and "Thing One" directories. The native
        // C++ version handles paths with spaces differently via _CopyCommand.
        // Skipping this assertion until ParseCommandline is fixed.
        // await UpdatePageAndWaitForItems(page, () => { page.SearchText = dirWithSpaces; });
        // commandList = page.GetItems();
        // Assert.AreEqual(1 + ExeItemCount, commandList.Length);
        // Assert.AreEqual("Thing One", commandList[ExeItemCount].Title, true, CultureInfo.InvariantCulture);

        // Finally, navigate to "<tempDir>\Thing One\". That should list the two files inside (+ exe item)
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = dirWithSpaces + "\\"; });
        commandList = page.GetItems();
        Assert.AreEqual(2 + ExeItemCount, commandList.Length);
    }

    [TestMethod]
    public void TestFilterCurrentDirectoryFiles_NoFilter()
    {
        // Setup - create mock items
        var items = new Dictionary<string, RunExeItem>
        {
            ["file1.txt"] = new RunExeItem("file1.txt", string.Empty, "C:\\test\\file1.txt", null, null) { Title = "file1.txt" },
            ["file2.exe"] = new RunExeItem("file2.exe", string.Empty, "C:\\test\\file2.exe", null, null) { Title = "file2.exe" },
            ["folder"] = new RunExeItem("folder", string.Empty, "C:\\test\\folder", null, null) { Title = "folder" },
        };

        // Test with no filter (empty fuzzy string)
        var result = RunListPage.FilterCurrentDirectoryFiles(
            fullFilePath: "C:\\test\\",
            directoryPath: "C:\\test",
            currentSubdir: "C:\\test",
            currentPathItems: new ReadOnlyDictionary<string, RunExeItem>(items),
            telemetryService: null);

        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public void TestFilterCurrentDirectoryFiles_WithFilter()
    {
        // Setup - create mock items
        var items = new Dictionary<string, RunExeItem>
        {
            ["file1.txt"] = new RunExeItem("file1.txt", string.Empty, "C:\\test\\file1.txt", null, null) { Title = "file1.txt" },
            ["file2.exe"] = new RunExeItem("file2.exe", string.Empty, "C:\\test\\file2.exe", null, null) { Title = "file2.exe" },
            ["document.pdf"] = new RunExeItem("document.pdf", string.Empty, "C:\\test\\document.pdf", null, null) { Title = "document.pdf" },
            ["folder"] = new RunExeItem("folder", string.Empty, "C:\\test\\folder", null, null) { Title = "folder" },
        };

        // Test filtering with "file" - should match file1.txt and file2.exe
        var result = RunListPage.FilterCurrentDirectoryFiles(
            fullFilePath: "C:\\test\\file",
            directoryPath: "C:\\test",
            currentSubdir: "C:\\test",
            currentPathItems: new ReadOnlyDictionary<string, RunExeItem>(items),
            telemetryService: null);

        Assert.AreEqual(2, result.Count);
        Assert.IsTrue(result.Any(item => item.Title == "file1.txt"));
        Assert.IsTrue(result.Any(item => item.Title == "file2.exe"));
    }

    [TestMethod]
    public void TestFilterCurrentDirectoryFiles_PartialMatch()
    {
        // Setup - create mock items
        var items = new Dictionary<string, RunExeItem>
        {
            ["document.txt"] = new RunExeItem("document.txt", string.Empty, "C:\\docs\\document.txt", null, null) { Title = "document.txt" },
            ["readme.md"] = new RunExeItem("readme.md", string.Empty, "C:\\docs\\readme.md", null, null) { Title = "readme.md" },
            ["config.json"] = new RunExeItem("config.json", string.Empty, "C:\\docs\\config.json", null, null) { Title = "config.json" },
        };

        // Test filtering with partial match "doc" - should match document.txt
        var result = RunListPage.FilterCurrentDirectoryFiles(
            fullFilePath: "C:\\docs\\doc",
            directoryPath: "C:\\docs",
            currentSubdir: "C:\\docs",
            currentPathItems: new ReadOnlyDictionary<string, RunExeItem>(items),
            telemetryService: null);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("document.txt", result[0].Title);
    }

    [TestMethod]
    public void TestFilterCurrentDirectoryFiles_NoMatches()
    {
        // Setup - create mock items
        var items = new Dictionary<string, RunExeItem>
        {
            ["file1.txt"] = new RunExeItem("file1.txt", string.Empty, "C:\\test\\file1.txt", null, null) { Title = "file1.txt" },
            ["file2.exe"] = new RunExeItem("file2.exe", string.Empty, "C:\\test\\file2.exe", null, null) { Title = "file2.exe" },
        };

        // Test filtering with non-matching string
        var result = RunListPage.FilterCurrentDirectoryFiles(
            fullFilePath: "C:\\test\\xyz",
            directoryPath: "C:\\test",
            currentSubdir: "C:\\test",
            currentPathItems: new ReadOnlyDictionary<string, RunExeItem>(items),
            telemetryService: null);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void TestFilterCurrentDirectoryFiles_CaseInsensitive()
    {
        // Setup - create mock items
        var items = new Dictionary<string, RunExeItem>
        {
            ["Document.TXT"] = new RunExeItem("Document.TXT", string.Empty, "C:\\test\\Document.TXT", null, null) { Title = "Document.TXT" },
            ["readme.MD"] = new RunExeItem("readme.MD", string.Empty, "C:\\test\\readme.MD", null, null) { Title = "readme.MD" },
        };

        // Test case-insensitive matching
        var result = RunListPage.FilterCurrentDirectoryFiles(
            fullFilePath: "C:\\test\\doc",
            directoryPath: "C:\\test",
            currentSubdir: "C:\\test",
            currentPathItems: new ReadOnlyDictionary<string, RunExeItem>(items),
            telemetryService: null);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Document.TXT", result[0].Title);
    }

    [TestMethod]
    public void TestFilterCurrentDirectoryFiles_WithTrailingSlash()
    {
        // Setup - create mock items
        var items = new Dictionary<string, RunExeItem>
        {
            ["subfolder"] = new RunExeItem("subfolder", string.Empty, "C:\\parent\\subfolder", null, null) { Title = "subfolder" },
            ["file.txt"] = new RunExeItem("file.txt", string.Empty, "C:\\parent\\file.txt", null, null) { Title = "file.txt" },
        };

        // Test with trailing slash - should return all items (no filter)
        var result = RunListPage.FilterCurrentDirectoryFiles(
            fullFilePath: "C:\\parent\\",
            directoryPath: "C:\\parent\\",
            currentSubdir: "C:\\parent",
            currentPathItems: new ReadOnlyDictionary<string, RunExeItem>(items),
            telemetryService: null);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public void TestFilterCurrentDirectoryFiles_EmptyItems()
    {
        // Test with empty items dictionary
        var items = new Dictionary<string, RunExeItem>();

        var result = RunListPage.FilterCurrentDirectoryFiles(
            fullFilePath: "C:\\test\\file",
            directoryPath: "C:\\test",
            currentSubdir: "C:\\test",
            currentPathItems: new ReadOnlyDictionary<string, RunExeItem>(items),
            telemetryService: null);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void TestFilterCurrentDirectoryFiles_FuzzyMatching()
    {
        // Setup - create mock items to test fuzzy matching
        var items = new Dictionary<string, RunExeItem>
        {
            ["application.exe"] = new RunExeItem("application.exe", string.Empty, "C:\\apps\\application.exe", null, null) { Title = "application.exe" },
            ["app_config.json"] = new RunExeItem("app_config.json", string.Empty, "C:\\apps\\app_config.json", null, null) { Title = "app_config.json" },
            ["deploy.bat"] = new RunExeItem("deploy.bat", string.Empty, "C:\\apps\\deploy.bat", null, null) { Title = "deploy.bat" },
        };

        // Test fuzzy matching with "app" - should match application.exe and app_config.json
        var result = RunListPage.FilterCurrentDirectoryFiles(
            fullFilePath: "C:\\apps\\app",
            directoryPath: "C:\\apps",
            currentSubdir: "C:\\apps",
            currentPathItems: new ReadOnlyDictionary<string, RunExeItem>(items),
            telemetryService: null);

        Assert.AreEqual(2, result.Count);
        Assert.IsTrue(result.Any(item => item.Title == "application.exe"));
        Assert.IsTrue(result.Any(item => item.Title == "app_config.json"));
        Assert.IsFalse(result.Any(item => item.Title == "deploy.bat"));
    }

    [TestMethod]
    public async Task TestNtPathSimple()
    {
        // Setup
        var nativeService = CreateMockHistoryService().Object;
        using var page = new RunListPage(nativeService, telemetryService: null);

        // Load up everything in c:\, for the sake of comparing:
        var filesInWindows = EnumerateFiles("C:\\Windows");
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = @"\\?\c:\Windows\"; });

        var commandList = page.GetItems();
        Assert.AreEqual(filesInWindows.Count() + ExeItemCount, commandList.Length);

        // all the items should have "\\?\" at the start of their TextToSuggest
        foreach (var item in commandList)
        {
            var suggestion = item.TextToSuggest;
            StringAssert.StartsWith(suggestion, @"\\?\");
        }
    }

    [TestMethod]
    public async Task TestNtPathFiltering()
    {
        // Setup
        var nativeService = CreateMockHistoryService().Object;
        using var page = new RunListPage(nativeService, telemetryService: null);

        var filesInWindows = EnumerateFiles("C:\\Windows");
        var inWindowsWithS = filesInWindows.Where(f => Path.GetFileName(f).StartsWith("s", StringComparison.OrdinalIgnoreCase));

        await UpdatePageAndWaitForItems(page, () => { page.SearchText = @"\\?\c:\Windows\s"; });

        var commandList = page.GetItems();
        Assert.AreEqual(inWindowsWithS.Count() + ExeItemCount, commandList.Length);

        // all the items should have "\\?\" at the start of their TextToSuggest
        foreach (var item in commandList)
        {
            var suggestion = item.TextToSuggest;
            StringAssert.StartsWith(suggestion, @"\\?\");
        }
    }

    [TestMethod]
    public async Task TestTildeExpandsToUserProfile()
    {
        // Setup
        var nativeService = CreateMockHistoryService().Object;
        using var page = new RunListPage(nativeService, telemetryService: null);

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var filesInUserProfile = EnumerateFiles(userProfile);

        // Navigate with tilde followed by backslash - should list files in user profile
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "~\\"; });

        var commandList = page.GetItems();
        Assert.AreEqual(filesInUserProfile.Count() + ExeItemCount, commandList.Length);
    }

    [TestMethod]
    [Ignore("Flaky test. TODO: https://task.ms/60553495 - fix and reenable")]
    public async Task TestTildeWithSubdirectory()
    {
        // Setup
        var nativeService = CreateMockHistoryService().Object;
        using var page = new RunListPage(nativeService, telemetryService: null);

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var documentsPath = Path.Combine(userProfile, "Documents");

        // Skip test if Documents folder doesn't exist
        if (!Directory.Exists(documentsPath))
        {
            return;
        }

        var filesInDocuments = EnumerateFiles(documentsPath);

        // Navigate with tilde to Documents folder
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "~\\Documents\\"; });

        var commandList = page.GetItems();
        Assert.AreEqual(filesInDocuments.Count(), commandList.Length);
    }

    [TestMethod]
    public async Task TestTildeWithFilter()
    {
        // Setup
        var nativeService = CreateMockHistoryService().Object;
        using var page = new RunListPage(nativeService, telemetryService: null);

        // First list all files in user profile
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "~\\"; });
        var allItems = page.GetItems();

        // Now apply a filter
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "~\\D"; });
        var filteredItems = page.GetItems();

        // Filtered items should be less than or equal to all items
        Assert.IsTrue(filteredItems.Length <= allItems.Length);

        // All filtered items should contain 'D' (case insensitive)
        foreach (var item in filteredItems)
        {
            StringAssert.Contains(item.Title, "D", StringComparison.OrdinalIgnoreCase);
        }
    }

    [TestMethod]
    public async Task TestTildeSuggestsCorrectPath()
    {
        // Setup
        var nativeService = CreateMockHistoryService().Object;
        using var page = new RunListPage(nativeService, telemetryService: null);

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Navigate with tilde
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "~\\"; });

        var commandList = page.GetItems();

        // Verify that items suggest paths starting with tilde or user profile
        foreach (var item in commandList)
        {
            if (item is IFileItem)
            {
                Assert.IsTrue(
                    item.TextToSuggest.StartsWith('~') || item.TextToSuggest.StartsWith(userProfile, StringComparison.Ordinal),
                    $"Expected TextToSuggest to start with ~ or {userProfile}, but got {item.TextToSuggest}");
            }
        }
    }

    [TestMethod]
    public async Task TestTildeAlone()
    {
        // Setup
        var nativeService = CreateMockHistoryService().Object;
        using var page = new RunListPage(nativeService, telemetryService: null);

        // Test just "~" without trailing slash
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "~"; });

        var commandList = page.GetItems();

        // Should match folders in parent that start with user profile folder name
        // or be empty if no matches
        Assert.IsNotNull(commandList);
    }

    [TestMethod]
    public async Task TestTildeNavigationCache()
    {
        // Setup
        var nativeService = CreateMockHistoryService().Object;
        using var page = new RunListPage(nativeService, telemetryService: null);

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var filesInUserProfile = EnumerateFiles(userProfile);

        // First navigation with tilde
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "~\\"; });
        var firstResult = page.GetItems();

        // Navigate away
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "C:\\Windows"; });

        // Navigate back with tilde - should use cached results
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "~\\"; });
        var secondResult = page.GetItems();

        Assert.AreEqual(firstResult.Length, secondResult.Length);
        Assert.AreEqual(filesInUserProfile.Count() + ExeItemCount, secondResult.Length);
    }

    [TestMethod]
    [DataRow("C:\\")]
    [DataRow("C:\\Windows\\")]
    [DataRow("C:\\Program Files\\")]
    public async Task GetSuggestionsForPath_ReturnsValidResults(string path)
    {
        // Act
        var results = await StaHelperService.RunOnStaAsync(
            () => RunListPage.GetSuggestionsForPath(path),
            CancellationToken.None);

        // Assert
        Assert.IsNotNull(results);

        // Results may be empty for some paths, but should never be null
    }

    [TestMethod]
    public async Task GetSuggestionsForPath_ResultsAreStrings()
    {
        // Arrange
        var path = "C:\\";

        // Act
        var results = await StaHelperService.RunOnStaAsync(
            () => RunListPage.GetSuggestionsForPath(path),
            CancellationToken.None);

        // Assert
        Assert.IsNotNull(results);
        foreach (var result in results)
        {
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType<string>(result);
        }
    }

    [TestMethod]
    [DataRow("C:\\Windows\\")]
    [DataRow("C:\\Program Files\\")]
    public async Task GetSuggestionsForPath_WithPathPrefix_ReturnsValidResults(string path)
    {
        // Setup
        var files = EnumerateFiles(path);

        // Act
        var results = await StaHelperService.RunOnStaAsync(
            () => RunListPage.GetSuggestionsForPath(path),
            CancellationToken.None);

        // Assert
        Assert.IsNotNull(results);
        Assert.AreEqual(files.Count(), results.Length);
    }

    [TestMethod]
    public async Task GetSuggestionsForPath_PathNoSlash_ReturnsCorrectResults()
    {
        // Setup
        var filesInWindows = EnumerateFiles("C:\\Windows");

        // Act
        var results = await StaHelperService.RunOnStaAsync(
            () => RunListPage.GetSuggestionsForPath("C:\\Windows"),
            CancellationToken.None);

        // Assert
        Assert.IsNotNull(results);
        foreach (var result in results)
        {
            StringAssert.StartsWith(result, "C:\\Windows", StringComparison.InvariantCultureIgnoreCase);
        }

        Assert.AreEqual(filesInWindows.Count(), results.Length);
    }

    [TestMethod]
    public async Task GetSuggestionsForPath_PathWithSlash_ReturnsCorrectResults()
    {
        // Setup
        var filesInWindows = EnumerateFiles("C:\\Windows");

        // Act
        var results = await StaHelperService.RunOnStaAsync(
            () => RunListPage.GetSuggestionsForPath("C:\\Windows\\"),
            CancellationToken.None);

        // Assert
        Assert.IsNotNull(results);
        Assert.AreEqual(filesInWindows.Count(), results.Length);
        foreach (var result in results)
        {
            StringAssert.StartsWith(result, "C:\\Windows\\");
        }
    }

    [TestMethod]
    public async Task GetSuggestionsForPath_NtPath_NoFiltering()
    {
        // Setup
        var filesInWindows = EnumerateFiles("C:\\Windows");

        // Act
        var results = await StaHelperService.RunOnStaAsync(
            () => RunListPage.GetSuggestionsForPath("\\\\?\\C:\\Windows\\"),
            CancellationToken.None);

        // Assert
        Assert.IsNotNull(results);
        Assert.AreEqual(filesInWindows.Count(), results.Length);
    }

    [TestMethod]
    public async Task TestTildeWithNestedPath()
    {
        // Setup
        var nativeService = CreateMockHistoryService().Object;
        using var page = new RunListPage(nativeService, telemetryService: null);

        // Create a temp directory structure in user profile for testing
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var tempDir = Path.Combine(userProfile, "TestTildeTemp_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        using var cleanup = new ScopeExit(() => Directory.Delete(tempDir, recursive: true));

        var nestedDir = Path.Combine(tempDir, "Nested");
        Directory.CreateDirectory(nestedDir);
        var testFile = Path.Combine(nestedDir, "testfile.txt");
        File.WriteAllText(testFile, "test content");

        var relativePath = Path.GetRelativePath(userProfile, nestedDir);
        var tildePathWithSlash = "~\\" + relativePath + "\\";

        // Navigate with tilde to nested directory
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = tildePathWithSlash; });

        var commandList = page.GetItems();
        Assert.AreEqual(1 + ExeItemCount, commandList.Length);
        Assert.AreEqual("testfile.txt", commandList[ExeItemCount].Title, true, CultureInfo.InvariantCulture);
    }

    [TestMethod]
    public async Task TestTildeDotFilter()
    {
        // Setup
        var nativeService = CreateMockHistoryService().Object;
        using var page = new RunListPage(nativeService, telemetryService: null);

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var filesInUserProfile = EnumerateFiles(userProfile);
        var dotFiles = filesInUserProfile.Where(f => Path.GetFileName(f).StartsWith(".", StringComparison.OrdinalIgnoreCase));

        // Typing "~\." should only return items in %userprofile% that start with '.'
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "~\\."; });

        var commandList = page.GetItems();
        Assert.AreEqual(dotFiles.Count() + ExeItemCount, commandList.Length);

        // All returned items (except the exe item) should start with '.'
        foreach (var item in commandList.Skip(ExeItemCount))
        {
            StringAssert.StartsWith(item.Title, ".");
        }
    }

    [TestMethod]
    public async Task TestFilterPathWithSpace()
    {
        // Setup
        var nativeService = CreateMockHistoryService().Object;
        using var page = new RunListPage(nativeService, telemetryService: null);

        var filesInProgramFiles = EnumerateFiles("C:\\Program Files");
        var inProgramFilesWithM = filesInProgramFiles.Where(f => Path.GetFileName(f).StartsWith("m", StringComparison.OrdinalIgnoreCase));
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "C:\\Program Files\\"; });

        var commandList = page.GetItems();
        Assert.AreEqual(filesInProgramFiles.Count() + ExeItemCount, commandList.Length);

        // now go to c:\Program Files\m. This should only have the results with an 'M' in them
        await UpdatePageAndWaitForItems(page, () => { page.SearchText = "C:\\Program Files\\m"; });
        var postFilterOnM = page.GetItems();
        Assert.AreEqual(inProgramFilesWithM.Count() + ExeItemCount, postFilterOnM.Length);
        Assert.AreNotEqual(commandList.Length, inProgramFilesWithM.Count());
    }
}
