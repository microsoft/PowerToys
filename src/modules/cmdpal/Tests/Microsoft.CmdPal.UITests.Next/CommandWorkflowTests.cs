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
    private const string FixtureProcessName = "winver";

    private string fixtureName = string.Empty;
    private string fixtureDirectory = string.Empty;
    private string shortcutPath = string.Empty;
    private Process? ownedApplication;
    private Process? decoyApplication;
    private long decoyWindow;
    private long ownedExplorerWindow;

    protected override void PrepareTestState()
    {
        fixtureName = $"CmdPalUITest-{Guid.NewGuid():N}";
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

            // A GUI fixture avoids depending on the user's Windows Terminal console broker.
            shortcut.TargetPath = Path.Combine(Environment.SystemDirectory, FixtureProcessName + ".exe");
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
        try
        {
            if (ownedExplorerWindow != 0)
            {
                WindowControl.TryCloseWindow(ownedExplorerWindow);
            }

            if (decoyWindow != 0)
            {
                WindowControl.TryCloseWindow(decoyWindow);
            }

            StopOwnedProcess(ownedApplication);
        }
        finally
        {
            try
            {
                StopOwnedProcess(decoyApplication);
            }
            finally
            {
                File.Delete(shortcutPath);
                Directory.Delete(fixtureDirectory, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void InstalledAppsLaunchesWin32Application()
    {
        var existingProcesses = SnapshotFixtureProcesses();
        OpenExtension("Search apps", "Search apps");
        SetSearchBox(fixtureName);
        InvokeResultWithEnter(fixtureName);
        var application = WindowsFinder.WaitForWindow(
            window => window.ProcessName == FixtureProcessName &&
                      !existingProcesses.Contains(window.ProcessId),
            timeoutMS: 30_000);
        Assert.IsNotNull(application, "The Installed apps extension did not launch the Win32 shortcut.");
        ownedApplication = Process.GetProcessById(application.ProcessId);
        Assert.AreEqual(FixtureProcessName, ownedApplication.ProcessName, "The shortcut did not start the Win32 fixture application.");
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void InstalledAppsLaunchesPackagedSettings()
    {
        using var diagnostics = BeginNavigationDiagnostics();
        OpenExtension("Search apps", "Search apps");
        SetSearchBox("Settings");
        var windowsBefore = SnapshotWindowsSettings();
        try
        {
            InvokeResultWithEnter("Settings");
            AssertWindowsSettingsOpened();
        }
        finally
        {
            CloseCreatedWindowsSettings(windowsBefore);
        }
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void RunCommandsExecutesLocalCommand()
    {
        var marker = Path.Combine(fixtureDirectory, "run-result.txt");
        var scriptPath = Path.Combine(fixtureDirectory, "write-result.js");
        var script = $"var file = new ActiveXObject('Scripting.FileSystemObject').CreateTextFile({System.Text.Json.JsonSerializer.Serialize(marker)}, true);" +
            $"file.WriteLine({System.Text.Json.JsonSerializer.Serialize(fixtureName)});file.Close();";
        File.WriteAllText(scriptPath, script);
        var command = $"wscript.exe //B //NoLogo \"{scriptPath}\"";
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
        Step("Starting an owned Win32 window for Window Walker");
        ownedApplication = StartFixtureApplication();
        var target = WaitForFixtureWindow(ownedApplication, fixtureName);
        WindowHelper.MinimizeWindow(new IntPtr(target.WindowHandle));
        var decoyTitle = $"CmdPalDecoy-{Guid.NewGuid():N}";
        decoyApplication = StartFixtureApplication();
        var decoy = WaitForFixtureWindow(decoyApplication, decoyTitle);
        decoyWindow = decoy.WindowHandle;
        Assert.IsTrue(WindowControl.WaitForForeground(new IntPtr(decoyWindow), timeoutMS: 10_000));
        Assert.AreNotEqual(new IntPtr(target.WindowHandle), WindowControl.GetForegroundWindowHandle());
        OpenExtension("Switch between open windows", "Switch between open windows");
        SetSearchBox(fixtureName);
        var result = CommandPaletteSession.Find<NavigationViewItem>(fixtureName, timeoutMS: 20_000);
        InvokeResultWithEnter(result.Name);
        var activated = WaitHelper.WaitForStable(
            observe: WindowControl.GetForegroundWindowHandle,
            isMatch: hwnd => hwnd == new IntPtr(target.WindowHandle),
            timeoutMS: 15_000,
            requiredConsecutiveMatches: 3);
        Assert.IsTrue(
            activated.Succeeded,
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
            added = true;
            SetSearchBox(bookmarkName);
            FindExactResult(bookmarkName);
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

    private static HashSet<int> SnapshotFixtureProcesses()
    {
        var processIds = new HashSet<int>();
        foreach (var process in Process.GetProcessesByName(FixtureProcessName))
        {
            using (process)
            {
                processIds.Add(process.Id);
            }
        }

        return processIds;
    }

    private static Process StartFixtureApplication()
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, FixtureProcessName + ".exe"),
            WorkingDirectory = Environment.SystemDirectory,
            UseShellExecute = true,
        });
        Assert.IsNotNull(process, "The Win32 fixture application did not start.");
        return process;
    }

    private static Session WaitForFixtureWindow(Process process, string title)
    {
        var window = WindowsFinder.WaitForWindow(
            candidate => candidate.ProcessId == process.Id,
            timeoutMS: 20_000);
        Assert.IsNotNull(window, "The Win32 fixture application did not create its own window.");
        Assert.IsTrue(
            NativeMethods.SetWindowTextW(new IntPtr(window.WindowHandle), title),
            $"The fixture window could not be named: Win32 error {Marshal.GetLastWin32Error()}.");
        Assert.IsTrue(
            WaitHelper.WaitForStable(
                observe: () => WindowsFinder.ListByApp(process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    .Any(candidate => candidate.Hwnd == window.WindowHandle && candidate.Title == title),
                isMatch: named => named,
                timeoutMS: 5_000,
                requiredConsecutiveMatches: 2).Succeeded,
            "The fixture window did not retain its unique title.");
        return window;
    }

    private static void StopOwnedProcess(Process? process)
    {
        if (process is null)
        {
            return;
        }

        using (process)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                Assert.IsTrue(process.WaitForExit(10_000), "The owned fixture application did not exit.");
            }
        }
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowTextW(IntPtr hwnd, string text);
    }
}
