// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UITests;

[TestClass]
public class CommandWorkflowTests : CommandPaletteTestBase
{
    private string fixtureName = string.Empty;
    private string fixtureDirectory = string.Empty;
    private string shortcutPath = string.Empty;
    private Process? ownedConsole;
    private long ownedExplorerWindow;
    private HashSet<int> initialConsoleIds = [];

    protected override void PrepareTestState()
    {
        fixtureName = $"CmdPalUITest-{Guid.NewGuid():N}";
        foreach (var process in Process.GetProcessesByName("cmd"))
        {
            using (process)
            {
                initialConsoleIds.Add(process.Id);
            }
        }

        fixtureDirectory = Path.Combine(Path.GetTempPath(), fixtureName);
        Directory.CreateDirectory(fixtureDirectory);
        shortcutPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            fixtureName + ".lnk");

        var shellType = Type.GetTypeFromProgID("WScript.Shell", throwOnError: true)!;
        dynamic shell = Activator.CreateInstance(shellType)!;
        object? shortcutObject = null;
        try
        {
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcutObject = shortcut;
            shortcut.TargetPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            shortcut.Arguments = $"/d /k title {fixtureName}";

            // A console host can outlive cmd.exe and retain its working directory during cleanup.
            shortcut.WorkingDirectory = Environment.SystemDirectory;
            shortcut.Save();
        }
        finally
        {
            if (shortcutObject is not null)
            {
                Marshal.FinalReleaseComObject(shortcutObject);
            }

            Marshal.FinalReleaseComObject(shell);
        }

        Assert.IsTrue(File.Exists(shortcutPath), "The Win32 application shortcut fixture was not created.");
    }

    [TestCleanup]
    public async Task CleanupCommandFixtures()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync();
        if (ownedExplorerWindow != 0)
        {
            WindowControl.TryCloseWindow(ownedExplorerWindow);
        }

        WindowControl.TryCloseByApp(
            "conhost",
            window => window.Title.Contains(fixtureName, StringComparison.Ordinal));
        WindowControl.TryCloseByApp(
            "WindowsTerminal",
            window => window.Title.Contains(fixtureName, StringComparison.Ordinal));
        if (ownedConsole is not null)
        {
            if (!ownedConsole.HasExited)
            {
                ownedConsole.Kill(entireProcessTree: true);
                Assert.IsTrue(ownedConsole.WaitForExit(10_000), "The owned console did not exit.");
            }

            ownedConsole.Dispose();
        }

        File.Delete(shortcutPath);
        Directory.Delete(fixtureDirectory, recursive: true);
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void InstalledAppsLaunchesWin32Application()
    {
        OpenExtension("Search apps", "Search apps");
        SetSearchBox(fixtureName);
        InvokeResultWithEnter(fixtureName);
        var console = WindowsFinder.WaitForWindow(
            window => window.Title.Contains(fixtureName, StringComparison.Ordinal) &&
                      window.ProcessName is "cmd" or "conhost" or "WindowsTerminal",
            timeoutMS: 30_000);
        Assert.IsNotNull(console, "The Installed apps extension did not launch the Win32 shortcut.");
        ownedConsole = FindOwnedConsole();
        Assert.IsNotNull(ownedConsole, "The Win32 application did not start cmd.exe.");
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void InstalledAppsLaunchesPackagedSettings()
    {
        OpenExtension("Search apps", "Search apps");
        SetSearchBox("Settings");
        try
        {
            InvokeResultWithEnter("Settings");
            AssertWindowsSettingsOpened();
        }
        finally
        {
            WindowControl.TryCloseByApp("SystemSettings");
        }
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void RunCommandsExecutesLocalCommand()
    {
        var marker = Path.Combine(fixtureDirectory, "run-result.txt");
        var command = $"cmd.exe /d /c echo {fixtureName}>\"{marker}\"";
        OpenExtension("Run commands", "Run commands");
        SetSearchBox(command);
        InvokeResultWithEnter("Run command");
        Step($"Waiting for the command to write '{marker}'");
        var execution = WaitHelper.WaitForStable(
            observe: () =>
            {
                using var stream = new FileStream(
                    marker, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd().Trim();
            },
            isMatch: content => content == fixtureName,
            timeoutMS: 20_000,
            requiredConsecutiveMatches: 2,
            shouldRetryException: exception => exception is IOException);
        Assert.IsTrue(execution.Succeeded, "The Run commands extension did not execute the fixture command.");
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void WindowWalkerActivatesSelectedWindow()
    {
        Step("Starting an owned console for Window Walker");
        ownedConsole = Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            Arguments = $"/d /k title {fixtureName}",
            UseShellExecute = true,
        });
        Assert.IsNotNull(ownedConsole);
        var target = WindowsFinder.WaitForWindow(
            window => window.Title.Contains(fixtureName, StringComparison.Ordinal),
            timeoutMS: 20_000);
        Assert.IsNotNull(target, "The Window Walker fixture did not create a window.");
        OpenExtension("Switch between open windows", "Switch between open windows");
        SetSearchBox(fixtureName);
        var result = CommandPaletteSession.Find<NavigationViewItem>(fixtureName, timeoutMS: 20_000);
        InvokeResultWithEnter(result.Name);
        Assert.IsTrue(
            WindowControl.WaitForForeground(
                new IntPtr(target.WindowHandle), timeoutMS: 15_000, requiredConsecutiveMatches: 3),
            "Window Walker did not activate the selected window.");
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void BookmarkCanBeAddedOpenedAndDeleted()
    {
        var added = false;
        var bookmarkName = fixtureName + "-bookmark";
        try
        {
            OpenExtension("Add bookmark", "Add bookmark");
            CommandPaletteSession.Find<TextBox>("URL or file path").SetText(fixtureDirectory);
            CommandPaletteSession.Find<TextBox>("Name (optional)").SetText(bookmarkName);
            Step("Saving a bookmark through its form");
            CommandPaletteSession.Find<Button>("Save").Invoke(msPostAction: 0);
            SetSearchBox(bookmarkName);
            FindExactResult(bookmarkName);
            added = true;
            InvokeResultWithEnter(bookmarkName);
            var explorer = WindowsFinder.WaitForWindowByApp(
                "explorer",
                window => window.Title.StartsWith(fixtureName, StringComparison.Ordinal),
                timeoutMS: 20_000);
            Assert.IsNotNull(explorer, "Opening the new bookmark did not open its target folder.");
            ownedExplorerWindow = explorer.WindowHandle;
        }
        finally
        {
            if (added)
            {
                Step("Deleting the test-owned bookmark");
                SetSearchBox(bookmarkName);
                FindExactResult(bookmarkName).MouseClick();
                OpenContextMenu();
                CommandPaletteSession.Find<NavigationViewItem>("Delete").Click();
                var confirmation = CommandPaletteSession.Find("Delete bookmark?");
                CommandPaletteSession.Find<Button>("Delete").Invoke(msPostAction: 0);
                Assert.IsTrue(confirmation.WaitForGone(15_000), "The bookmark deletion confirmation did not close.");
                SetSearchBox(bookmarkName);
                Assert.IsTrue(
                    CommandPaletteSession.WaitFor(
                        () => !CommandPaletteSession.FindAll<NavigationViewItem>(By.Name(bookmarkName), timeoutMS: 0)
                            .Any(item => string.Equals(item.Name, bookmarkName, StringComparison.Ordinal)),
                        timeoutMS: 10_000),
                    "The deleted bookmark is still offered by Command Palette.");
            }
        }
    }

    private Process? FindOwnedConsole()
    {
        foreach (var process in Process.GetProcessesByName("cmd"))
        {
            if (!initialConsoleIds.Contains(process.Id))
            {
                return process;
            }

            process.Dispose();
        }

        return null;
    }
}
