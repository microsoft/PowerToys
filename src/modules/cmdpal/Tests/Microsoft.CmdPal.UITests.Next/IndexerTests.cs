// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Storage;
using Windows.Storage.Search;

namespace Microsoft.CmdPal.UITests;

[TestClass]
public class IndexerTests : CommandPaletteTestBase
{
    private const string TestFileContent = "This is Indexer UI test sample";
    private const string TestFolderName = "Downloads";

    private static string testFileName = string.Empty;
    private static string testFileBaseName = string.Empty;
    private static string testFilePath = string.Empty;
    private static string browseFolderPath = string.Empty;
    private static string browseChildFileName = string.Empty;
    private long containingFolderWindowHandle;
    private long openedFolderWindowHandle;
    private long propertiesWindowHandle;
    private long consoleWindowHandle;

    internal static void CreateIndexedFixture(TestContext context)
    {
        testFileBaseName = $"indexer_test_item_{Guid.NewGuid():N}";
        testFileName = testFileBaseName + ".txt";
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        Directory.CreateDirectory(documentsPath);
        testFilePath = Path.Combine(documentsPath, testFileName);
        browseFolderPath = Path.Combine(documentsPath, $"indexer_test_folder_{Guid.NewGuid():N}");
        browseChildFileName = $"indexer_browse_item_{Guid.NewGuid():N}.txt";
        var browseChildPath = Path.Combine(browseFolderPath, browseChildFileName);
        var created = false;
        try
        {
            File.WriteAllText(testFilePath, TestFileContent, Encoding.UTF8);
            Directory.CreateDirectory(browseFolderPath);
            File.WriteAllText(browseChildPath, TestFileContent, Encoding.UTF8);
            created = true;
            context.WriteLine($"Created indexed fixtures '{testFilePath}' and '{browseChildPath}' before any UI-test workload.");
        }
        finally
        {
            if (!created)
            {
                RemoveIndexedFixture();
            }
        }
    }

    [ClassInitialize]
    public static void PrepareIndexedFixture(TestContext context)
    {
        var documentsPath = Path.GetDirectoryName(testFilePath)!;
        var browseChildPath = Path.Combine(browseFolderPath, browseChildFileName);
        var indexed = false;
        try
        {
            context.WriteLine("Waiting for Windows Search to index the shared file and browse-folder fixtures before launching Command Palette");
            var folder = StorageFolder.GetFolderFromPathAsync(documentsPath).AsTask().GetAwaiter().GetResult();
            var options = new QueryOptions(CommonFileQuery.OrderByName, [".txt"])
            {
                IndexerOption = IndexerOption.OnlyUseIndexer,
                FolderDepth = FolderDepth.Deep,
                ApplicationSearchFilter = $"System.FileName:=\"{testFileName}\" OR System.FileName:=\"{browseChildFileName}\"",
            };
            var readiness = WaitHelper.WaitForStable(
                observe: () => folder.CreateFileQueryWithOptions(options).GetFilesAsync()
                    .AsTask().GetAwaiter().GetResult()
                    .Select(file => file.Path).ToHashSet(StringComparer.OrdinalIgnoreCase),
                isMatch: paths => paths is not null && paths.Contains(testFilePath) && paths.Contains(browseChildPath),
                timeoutMS: 180_000,
                requiredConsecutiveMatches: 1,
                pollIntervalMS: 1_000);
            indexed = readiness.Succeeded;
            Assert.IsTrue(
                indexed,
                $"Windows Search did not index fixtures '{testFilePath}' and '{browseChildPath}' before UI-test startup. Observed {readiness.LastObservation?.Count ?? 0} indexed text files under '{documentsPath}'.");
        }
        finally
        {
            if (!indexed)
            {
                RemoveIndexedFixture();
            }
        }
    }

    internal static void RemoveIndexedFixture()
    {
        if (!string.IsNullOrEmpty(testFilePath))
        {
            File.Delete(testFilePath);
        }

        if (Directory.Exists(browseFolderPath))
        {
            Directory.Delete(browseFolderPath, recursive: true);
        }
    }

    [TestCleanup]
    public async Task CleanupIndexerTest()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync(TimeSpan.FromSeconds(1));

        if (containingFolderWindowHandle != 0)
        {
            WindowControl.TryCloseWindow(containingFolderWindowHandle);
        }

        WindowControl.TryCloseByApp(
            "Notepad",
            window => window.Title.Contains(testFileBaseName, StringComparison.OrdinalIgnoreCase));
        if (openedFolderWindowHandle != 0)
        {
            WindowControl.TryCloseWindow(openedFolderWindowHandle);
        }

        if (propertiesWindowHandle != 0)
        {
            WindowControl.TryCloseWindow(propertiesWindowHandle);
        }

        if (consoleWindowHandle != 0)
        {
            WindowControl.TryCloseWindow(consoleWindowHandle, timeoutMS: 2_000);
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
    private NavigationViewItem FindIndexedItem(string name, int timeoutMS = 90_000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMS);
        while (true)
        {
            SetFilesExtensionSearchBox(name);
            var item = CommandPaletteSession.FindAll<NavigationViewItem>(By.Name(name), timeoutMS: 3_000)
                .FirstOrDefault(result => string.Equals(result.Name, name, StringComparison.OrdinalIgnoreCase));
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
        InvokeResultWithEnter(searchFileItem.Name);
        Assert.IsNotNull(CommandPaletteSession.Find<Button>("Back", timeoutMS: 45_000));
    }

    private void AssertFixtureFolderOpened(IReadOnlySet<long> windowsBefore)
    {
        var explorer = WindowsFinder.WaitForWindow(
            window => !windowsBefore.Contains(window.Hwnd) &&
                      window.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase) &&
                      window.Title.Contains(Path.GetFileName(browseFolderPath), StringComparison.OrdinalIgnoreCase),
            timeoutMS: 20_000);
        Assert.IsNotNull(explorer, "File Explorer did not open the test-owned indexed folder.");
        openedFolderWindowHandle = explorer.WindowHandle;
    }

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
        var searchItem = FindIndexedItem(testFileName);
        Step($"Opening indexed file '{testFileName}' through the primary command");
        searchItem.MouseClick();
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
        FindIndexedItem(testFileName);

        Step($"Double-clicking indexed file '{testFileName}'");
        var windowsBefore = SnapshotWindowHandles();
        DoubleClickResult(testFileName);
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
        FindIndexedItem(Path.GetFileName(browseFolderPath)).MouseClick();
        var windowsBefore = SnapshotWindowHandles();
        Step("Opening the test-owned folder through the primary command");
        CommandPaletteSession.Find<Button>(By.AccessibilityId("PrimaryCommandButton")).Click();

        AssertFixtureFolderOpened(windowsBefore);
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void IndexerDoubleClickOpenFolderTest()
    {
        EnterIndexerExtension();
        var folderName = Path.GetFileName(browseFolderPath);
        FindIndexedItem(folderName);
        var windowsBefore = SnapshotWindowHandles();
        Step("Double-clicking the test-owned folder result");
        DoubleClickResult(folderName);
        AssertFixtureFolderOpened(windowsBefore);
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void IndexerBrowseFolderTest()
    {
        // Use a uniquely named folder: a plain "Downloads" query can also match shortcuts such as
        // %USERPROFILE%\Links\Downloads.lnk, which cannot be browsed.
        var folderName = Path.GetFileName(browseFolderPath);

        EnterIndexerExtension();
        FindIndexedItem(folderName).MouseClick();
        Step($"Browsing '{folderName}' with the secondary command");
        CommandPaletteSession.Find<Button>(By.AccessibilityId("SecondaryCommandButton")).Click();
        Assert.IsNotNull(
            CommandPaletteSession.Find<NavigationViewItem>(browseChildFileName, timeoutMS: 15_000),
            "Browsing the folder did not list the file it contains.");
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void IndexerCopyPathTest()
    {
        EnterIndexerExtension();
        FindIndexedItem(testFileName).MouseClick();
        var originalClipboard = ClipboardHelper.GetText();
        try
        {
            ClipboardHelper.Clear();
            OpenContextMenu();
            Step("Copying the selected file path");
            CommandPaletteSession.Find<NavigationViewItem>("Copy path").Click();
            var clipboardContent = ClipboardHelper.WaitForText(string.Empty, timeoutMS: 5_000);
            Assert.IsTrue(
                string.Equals(testFilePath, clipboardContent.Trim('"'), StringComparison.OrdinalIgnoreCase),
                $"Expected path '{testFilePath}', but the clipboard contained '{clipboardContent}'.");
        }
        finally
        {
            ClipboardHelper.SetText(originalClipboard);
        }
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void IndexerShowInFolderTest()
    {
        EnterIndexerExtension();
        FindIndexedItem(testFileName).MouseClick();
        OpenContextMenu();
        var windowsBefore = SnapshotWindowHandles();
        Step("Showing the selected file in File Explorer");
        CommandPaletteSession.Find<NavigationViewItem>("Show in folder").Click();

        var explorer = WindowsFinder.WaitForWindow(
            window => window.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase) &&
                      ExplorerShell.TryGetSelection(new IntPtr(window.Hwnd))?.SelectedPaths.SetEquals([testFilePath]) == true,
            timeoutMS: 20_000);
        Assert.IsNotNull(explorer, "File Explorer did not open the folder containing the indexed file.");
        if (!windowsBefore.Contains(explorer.WindowHandle))
        {
            containingFolderWindowHandle = explorer.WindowHandle;
        }

        var selection = WaitHelper.WaitForStable(
            observe: () => ExplorerShell.TryGetSelection(new IntPtr(explorer.WindowHandle)),
            isMatch: snapshot => snapshot is not null &&
                                 snapshot.SelectedPaths.SetEquals([testFilePath]) &&
                                 string.Equals(snapshot.FocusedPath, testFilePath, StringComparison.OrdinalIgnoreCase),
            timeoutMS: 20_000,
            requiredConsecutiveMatches: 3);
        Assert.IsTrue(selection.Succeeded, $"Show in folder did not select and focus '{testFilePath}'.");
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void IndexerOpenPathInConsoleTest()
    {
        EnterIndexerExtension();
        FindIndexedItem(testFileName).MouseClick();
        OpenContextMenu();
        var windowsBefore = SnapshotWindowHandles();
        var cmdProcessesBefore = GetProcessIds("cmd").ToHashSet();
        Step("Opening the selected file's path in a console");
        CommandPaletteSession.Find<NavigationViewItem>("Open path in console").Click();

        // cmd.exe is hosted by conhost on Windows 10 and by Windows Terminal when it is the default
        // terminal (Windows 11); the title casing of the cmd.exe path also differs between them.
        // A cold Terminal frame can appear before its console and title finish loading.
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
            timeoutMS: 45_000);
        var consoleProcessIds = GetProcessIds("cmd").Where(id => !cmdProcessesBefore.Contains(id)).ToArray();
        consoleWindowHandle = consoleWindow?.Hwnd ?? 0;

        Assert.IsNotNull(consoleSession, "No new console window titled with the cmd.exe path opened.");
        Assert.IsTrue(consoleProcessIds.Length > 0, "Opening the path in a console did not start cmd.exe.");
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void IndexerOpenPropertiesTest()
    {
        EnterIndexerExtension();
        FindIndexedItem(testFileName).MouseClick();
        OpenContextMenu();
        var windowsBefore = SnapshotWindowHandles();
        Step("Opening Properties for the selected file");
        CommandPaletteSession.Find<NavigationViewItem>("Properties").Click();

        var propertiesWindow = WindowsFinder.WaitForWindow(
            window => !windowsBefore.Contains(window.Hwnd) &&
                      window.Title.Contains(testFileBaseName, StringComparison.OrdinalIgnoreCase) &&
                      window.Title.Contains("Properties", StringComparison.OrdinalIgnoreCase),
            timeoutMS: 15_000);
        Assert.IsNotNull(propertiesWindow, "The properties window did not open for the selected file.");
        propertiesWindowHandle = propertiesWindow.WindowHandle;
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
