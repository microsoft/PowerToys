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
    private string consoleProcessPath = string.Empty;
    private Process? ownedConsole;
    private Process? decoyConsole;
    private long decoyWindow;
    private long ownedExplorerWindow;

    protected override void PrepareTestState()
    {
        fixtureName = $"CmdPalUITest-{Guid.NewGuid():N}";
        fixtureDirectory = Path.Combine(Path.GetTempPath(), fixtureName);
        Directory.CreateDirectory(fixtureDirectory);
        consoleProcessPath = Path.Combine(fixtureDirectory, "console.pid");
        var launcherPath = Path.Combine(fixtureDirectory, "launch-console.ps1");
        var launcherScript = $$"""
            $console = Start-Process -FilePath "$env:SystemRoot\System32\cmd.exe" -ArgumentList "/d /k title {{fixtureName}}" -WorkingDirectory "$env:SystemRoot\System32" -WindowStyle Normal -PassThru
            [IO.File]::WriteAllText('{{consoleProcessPath.Replace("'", "''", StringComparison.Ordinal)}}', [string]$console.Id)
            """;
        File.WriteAllText(launcherPath, launcherScript);
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
            shortcut.TargetPath = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
            shortcut.Arguments = $"-NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File \"{launcherPath}\"";

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

            WindowControl.TryCloseByApp(
                "conhost",
                window => window.Title.Contains(fixtureName, StringComparison.Ordinal));
            WindowControl.TryCloseByApp(
                "WindowsTerminal",
                window => window.Title.Contains(fixtureName, StringComparison.Ordinal));
            StopOwnedConsole(ownedConsole);
        }
        finally
        {
            try
            {
                StopOwnedConsole(decoyConsole);
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
        OpenExtension("Search apps", "Search apps");
        SetSearchBox(fixtureName);
        InvokeResultWithEnter(fixtureName);
        var console = WindowsFinder.WaitForWindow(
            window => window.Title.Contains(fixtureName, StringComparison.Ordinal) &&
                      window.ProcessName is "cmd" or "conhost" or "WindowsTerminal",
            timeoutMS: 30_000);
        Assert.IsNotNull(console, "The Installed apps extension did not launch the Win32 shortcut.");
        var processId = 0;
        var recorded = WaitHelper.WaitForStable(
            observe: () => File.ReadAllText(consoleProcessPath),
            isMatch: text => int.TryParse(text, out processId) && processId > 0,
            timeoutMS: 10_000,
            requiredConsecutiveMatches: 1,
            shouldRetryException: exception => exception is IOException);
        Assert.IsTrue(recorded.Succeeded, "The shortcut launcher did not record its owned console process.");
        ownedConsole = Process.GetProcessById(processId);
        Assert.AreEqual("cmd", ownedConsole.ProcessName, "The shortcut launcher did not start cmd.exe.");
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
        WindowHelper.MinimizeWindow(new IntPtr(target.WindowHandle));
        var decoyTitle = $"CmdPalDecoy-{Guid.NewGuid():N}";
        decoyConsole = Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            Arguments = $"/d /k title {decoyTitle}",
            UseShellExecute = true,
        });
        Assert.IsNotNull(decoyConsole);
        var decoy = WindowsFinder.WaitForWindow(
            window => window.Title.Contains(decoyTitle, StringComparison.Ordinal),
            timeoutMS: 20_000);
        Assert.IsNotNull(decoy, "The Window Walker decoy did not create a window.");
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

    private static void StopOwnedConsole(Process? process)
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
                Assert.IsTrue(process.WaitForExit(10_000), "The owned console did not exit.");
            }
        }
    }
}
