// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UITests;

[TestClass]
public class IndexerTests : CommandPaletteTestBase
{
    private const string TestFileContent = "This is Indexer UI test sample";
    private const string TestFolderName = "Downloads";

    private string testFileName = string.Empty;
    private string testFileBaseName = string.Empty;
    private string testFilePath = string.Empty;
    private string browseFolderPath = string.Empty;
    private long consoleWindowHandle;
    private int[] consoleProcessIds = [];

    protected override void PrepareTestState()
    {
        testFileBaseName = $"indexer_test_item_{Guid.NewGuid():N}";
        testFileName = testFileBaseName + ".txt";
        var downloadsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            TestFolderName);
        Directory.CreateDirectory(downloadsPath);
        testFilePath = Path.Combine(downloadsPath, testFileName);
        File.WriteAllText(testFilePath, TestFileContent, Encoding.UTF8);
    }

    [TestCleanup]
    public async Task CleanupIndexerTest()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync(TimeSpan.FromSeconds(1));

        WindowControl.TryCloseByApp(
            "Notepad",
            window => window.Title.Contains(testFileBaseName, StringComparison.OrdinalIgnoreCase));
        WindowControl.TryCloseByApp(
            "explorer",
            window => window.Title.Contains(TestFolderName, StringComparison.OrdinalIgnoreCase));
        WindowControl.TryCloseByApp(
            "explorer",
            window => window.Title.Contains("Properties", StringComparison.OrdinalIgnoreCase));
        if (consoleWindowHandle != 0)
        {
            WindowControl.TryCloseWindow(consoleWindowHandle, timeoutMS: 2_000);
        }

        foreach (var processId in consoleProcessIds)
        {
            try
            {
                using var process = System.Diagnostics.Process.GetProcessById(processId);
                process.Kill();
            }
            catch (ArgumentException)
            {
                // Already exited after its window closed.
            }
            catch (InvalidOperationException)
            {
            }
        }

        if (File.Exists(testFilePath))
        {
            File.Delete(testFilePath);
        }

        if (!string.IsNullOrEmpty(browseFolderPath) && Directory.Exists(browseFolderPath))
        {
            Directory.Delete(browseFolderPath, recursive: true);
        }
    }

    private static HashSet<long> SnapshotWindowHandles() =>
        WindowsFinder.ListAll().Select(window => window.Hwnd).ToHashSet();

    private static int[] GetProcessIds(string processName)
    {
        var processes = System.Diagnostics.Process.GetProcessesByName(processName);
        try
        {
            return processes.Select(process => process.Id).ToArray();
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    // A freshly created item may not be indexed yet; re-run the query until it appears.
    private NavigationViewItem FindIndexedItem(string name, int timeoutMS = 30_000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMS);
        while (true)
        {
            SetFilesExtensionSearchBox(name);
            var item = CommandPaletteSession.FindAll<NavigationViewItem>(By.Name(name), timeoutMS: 3_000).FirstOrDefault();
            if (item is not null)
            {
                return item;
            }

            Assert.IsTrue(DateTime.UtcNow < deadline, $"The indexer did not return '{name}' within {timeoutMS} ms.");
            SetFilesExtensionSearchBox(string.Empty);
            Thread.Sleep(1_000);
        }
    }

    private void EnterIndexerExtension()
    {
        SetSearchBox("files");

        var searchFileItem = CommandPaletteSession.Find<NavigationViewItem>("Search files");
        Assert.AreEqual("Search files", searchFileItem.Name);
        Step("Opening the Search files extension");
        DoubleClickResult(searchFileItem);
    }

    private Session? WaitForExplorerWindow(int timeoutMS = 10_000) =>
        WindowsFinder.WaitForWindow(
            window => window.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase) &&
                      window.Title.Contains(TestFolderName, StringComparison.OrdinalIgnoreCase),
            timeoutMS: timeoutMS);

    [TestMethod]
    [TestCategory("CmdPal")]
    public void BasicIndexerSearchTest()
    {
        EnterIndexerExtension();
        SetFilesExtensionSearchBox(TestFolderName);
        Assert.IsNotNull(CommandPaletteSession.Find<NavigationViewItem>(TestFolderName));
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void IndexerOpenFileTest()
    {
        EnterIndexerExtension();
        SetFilesExtensionSearchBox(testFileName);

        var searchItem = CommandPaletteSession.Find<NavigationViewItem>(testFileName);
        Step($"Opening indexed file '{testFileName}' through the primary command");
        searchItem.Click();
        var windowsBefore = SnapshotWindowHandles();
        CommandPaletteSession.Find<Button>(By.AccessibilityId("PrimaryCommandButton")).Click();
        ChooseDefaultAppIfPrompted(windowsBefore);

        // A cold Notepad start on a fresh profile can take well over 15 seconds.
        var notepadWindow = WindowsFinder.WaitForWindow(
            window => window.ProcessName.Contains("notepad", StringComparison.OrdinalIgnoreCase) &&
                      window.Title.Contains(testFileBaseName, StringComparison.OrdinalIgnoreCase),
            timeoutMS: 30_000);
        Assert.IsNotNull(notepadWindow, "The indexed text file did not open in Notepad.");
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void IndexerDoubleClickOpenFileTest()
    {
        EnterIndexerExtension();
        SetFilesExtensionSearchBox(testFileName);

        Step($"Double-clicking indexed file '{testFileName}'");
        var windowsBefore = SnapshotWindowHandles();
        DoubleClickResult(CommandPaletteSession.Find<NavigationViewItem>(testFileName));
        ChooseDefaultAppIfPrompted(windowsBefore);

        var notepadWindow = WindowsFinder.WaitForWindow(
            window => window.ProcessName.Contains("notepad", StringComparison.OrdinalIgnoreCase) &&
                      window.Title.Contains(testFileBaseName, StringComparison.OrdinalIgnoreCase),
            timeoutMS: 30_000);
        Assert.IsNotNull(notepadWindow, "The indexed text file did not open in Notepad.");
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void IndexerOpenFolderTest()
    {
        EnterIndexerExtension();
        SetFilesExtensionSearchBox(TestFolderName);

        CommandPaletteSession.Find<NavigationViewItem>(TestFolderName).Click();
        Step("Opening Downloads through the primary command");
        CommandPaletteSession.Find<Button>(By.AccessibilityId("PrimaryCommandButton")).Click();

        Assert.IsNotNull(WaitForExplorerWindow(), "File Explorer did not open Downloads.");
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void IndexerDoubleClickOpenFolderTest()
    {
        EnterIndexerExtension();
        SetFilesExtensionSearchBox(TestFolderName);

        Step("Double-clicking the Downloads result");
        DoubleClickResult(CommandPaletteSession.Find<NavigationViewItem>(TestFolderName));
        Assert.IsNotNull(WaitForExplorerWindow(), "File Explorer did not open Downloads.");
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void IndexerBrowseFolderTest()
    {
        // Use a uniquely named folder: a plain "Downloads" query can also match shortcuts such as
        // %USERPROFILE%\Links\Downloads.lnk, which cannot be browsed.
        var folderName = $"indexer_test_folder_{Guid.NewGuid():N}";
        var childFileName = $"indexer_browse_item_{Guid.NewGuid():N}.txt";
        browseFolderPath = Path.Combine(Path.GetDirectoryName(testFilePath)!, folderName);
        Directory.CreateDirectory(browseFolderPath);
        File.WriteAllText(Path.Combine(browseFolderPath, childFileName), TestFileContent, Encoding.UTF8);

        EnterIndexerExtension();
        FindIndexedItem(folderName).Click();
        Step($"Browsing '{folderName}' with the secondary command");
        CommandPaletteSession.Find<Button>(By.AccessibilityId("SecondaryCommandButton")).Click();
        Assert.IsNotNull(
            CommandPaletteSession.Find<NavigationViewItem>(childFileName, timeoutMS: 15_000),
            "Browsing the folder did not list the file it contains.");
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void IndexerCopyPathTest()
    {
        EnterIndexerExtension();
        SetFilesExtensionSearchBox(testFileName);

        CommandPaletteSession.Find<NavigationViewItem>(testFileName).Click();
        ClipboardHelper.Clear();
        OpenContextMenu();
        Step("Copying the selected file path");
        CommandPaletteSession.Find<NavigationViewItem>("Copy path").Click();

        var clipboardContent = ClipboardHelper.WaitForText(string.Empty, timeoutMS: 5_000);
        Assert.IsTrue(
            clipboardContent.Contains(testFileName, StringComparison.OrdinalIgnoreCase),
            $"Clipboard content does not contain the expected file name. Clipboard: {clipboardContent}");
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void IndexerShowInFolderTest()
    {
        EnterIndexerExtension();
        SetFilesExtensionSearchBox(testFileName);

        CommandPaletteSession.Find<NavigationViewItem>(testFileName).Click();
        OpenContextMenu();
        Step("Showing the selected file in File Explorer");
        CommandPaletteSession.Find<NavigationViewItem>("Show in folder").Click();

        Assert.IsNotNull(
            WaitForExplorerWindow(timeoutMS: 20_000),
            "File Explorer did not open the folder containing the indexed file.");
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void IndexerOpenPathInConsoleTest()
    {
        EnterIndexerExtension();
        SetFilesExtensionSearchBox(testFileName);

        CommandPaletteSession.Find<NavigationViewItem>(testFileName).Click();
        OpenContextMenu();
        var windowsBefore = SnapshotWindowHandles();
        var cmdProcessesBefore = GetProcessIds("cmd").ToHashSet();
        Step("Opening the selected file's path in a console");
        CommandPaletteSession.Find<NavigationViewItem>("Open path in console").Click();

        // cmd.exe is hosted by conhost on Windows 10 and by Windows Terminal when it is the default
        // terminal (Windows 11); the title casing of the cmd.exe path also differs between them.
        WindowsFinder.WindowInfo? consoleWindow = null;
        var consoleSession = WindowsFinder.WaitForWindow(
            window =>
            {
                var isConsole = !windowsBefore.Contains(window.Hwnd) &&
                                window.Title.Contains("cmd.exe", StringComparison.OrdinalIgnoreCase) &&
                                (window.ProcessName.Equals("cmd", StringComparison.OrdinalIgnoreCase) ||
                                 window.ProcessName.Equals("conhost", StringComparison.OrdinalIgnoreCase) ||
                                 window.ProcessName.Equals("WindowsTerminal", StringComparison.OrdinalIgnoreCase));
                if (isConsole)
                {
                    consoleWindow = window;
                }

                return isConsole;
            },
            timeoutMS: 15_000);
        consoleProcessIds = GetProcessIds("cmd").Where(id => !cmdProcessesBefore.Contains(id)).ToArray();
        consoleWindowHandle = consoleWindow?.Hwnd ?? 0;

        Assert.IsNotNull(consoleSession, "No new console window titled with the cmd.exe path opened.");
        Assert.IsTrue(consoleProcessIds.Length > 0, "Opening the path in a console did not start cmd.exe.");
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void IndexerOpenPropertiesTest()
    {
        EnterIndexerExtension();
        SetFilesExtensionSearchBox(testFileName);

        CommandPaletteSession.Find<NavigationViewItem>(testFileName).Click();
        OpenContextMenu();
        Step("Opening Properties for the selected file");
        CommandPaletteSession.Find<NavigationViewItem>("Properties").Click();

        var propertiesWindow = WindowsFinder.WaitForWindow(
            window => window.Title.Contains("Properties", StringComparison.OrdinalIgnoreCase),
            timeoutMS: 15_000);
        Assert.IsNotNull(propertiesWindow, "The properties window did not open for the selected file.");
    }

    // Only answer the shell's "How do you want to open this file?" picker, which is hosted by
    // OpenWith.exe and appears after the open request; never click buttons in unrelated windows.
    private void ChooseDefaultAppIfPrompted(IReadOnlySet<long> windowsBefore, int timeoutMS = 3_000)
    {
        var dialog = WindowsFinder.WaitForWindow(
            window => !windowsBefore.Contains(window.Hwnd) &&
                      window.ProcessName.Equals("OpenWith", StringComparison.OrdinalIgnoreCase),
            timeoutMS: timeoutMS);
        if (dialog is null)
        {
            return;
        }

        var justOnce = dialog.FindAll<Button>(By.Name("Just once"), timeoutMS: 2_000).FirstOrDefault();
        if (justOnce is not null)
        {
            Step("Confirming Notepad for this file-open request");
            justOnce.Click();
            return;
        }

        var ok = dialog.FindAll<Button>(By.Name("OK"), timeoutMS: 2_000).FirstOrDefault();
        if (ok is not null)
        {
            Step("Confirming the default app for this file-open request");
            ok.Click();
        }
    }
}