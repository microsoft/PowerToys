// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesEditor.Utils;

namespace WorkspacesEditorUITest;

[TestClass]
public class WorkspacesSnapshotTests : WorkspacesUiAutomationBase
{
    public WorkspacesSnapshotTests()
        : base()
    {
    }

    [TestMethod("WorkspacesSnapshot.CancelCapture")]
    [TestCategory("Workspaces Snapshot UI")]
    public void TestCaptureCancel()
    {
        AttachWorkspacesEditor();

        var createButton = Find<Button>("Create Workspace");
        createButton.Click();

        Task.Delay(1000).Wait();

        AttachSnapshotWindow();

        var cancelButton = Find<Button>("Cancel");

        Assert.IsNotNull(cancelButton, "Capture button should exist");

        cancelButton.Click();
    }

    [TestMethod("WorkspacesSnapshot.CapturePackagedApps")]
    [TestCategory("Workspaces Snapshot UI")]
    public void TestCapturePackagedApplications()
    {
        OpenCalculator();
        OpenWindowsSettings();
        Task.Delay(2000).Wait();

        AttachWorkspacesEditor();
        var createButton = Find<Button>("Create Workspace");
        createButton.Click();
        Task.Delay(500).Wait();

        AttachSnapshotWindow();
        var captureButton = Find<Button>("Capture");
        captureButton.Click();
        Task.Delay(3000).Wait();

        // Verify captured windows by reading the temporary workspaces file as the ground truth.
        var editorIO = new WorkspacesEditorIO();
        var workspace = editorIO.ParseTempProject();

        Assert.IsNotNull(workspace, "Workspace data should be deserialized.");
        Assert.IsNotNull(workspace.Applications, "Workspace should contain a list of apps.");

        bool isCalculatorFound = workspace.Applications.Any(app => app.AppPath.Contains("Calculator", StringComparison.OrdinalIgnoreCase));
        bool isSettingsFound = workspace.Applications.Any(app => app.AppPath.Contains("Settings", StringComparison.OrdinalIgnoreCase));

        Assert.IsTrue(isCalculatorFound, "Calculator should be captured in the workspace data.");
        Assert.IsTrue(isSettingsFound, "Settings should be captured in the workspace data.");

        // Cancel to clean up
        AttachWorkspacesEditor();
        Find<Button>("Cancel").Click();
        Task.Delay(1000).Wait();

        // Close test applications
        CloseCalculator();
        CloseWindowsSettings();
    }

    /* Not finished yet
    [TestMethod("WorkspacesSnapshot.CaptureElevatedApps")]
    [TestCategory("Workspaces Snapshot UI")]
    public void TestCaptureElevatedApplications()
    {
        // This test only works if PowerToys is running elevated
        if (!IsRunningElevated())
        {
            Assert.Inconclusive("PowerToys is not running elevated, cannot test elevated app capture");
            return;
        }

        // Open an elevated application
        OpenElevatedNotepad();
        Thread.Sleep(3000);

        // Click Create Workspace
        var createButton = Find<Button>("Create Workspace");
        createButton.Click();
        Thread.Sleep(1000);

        // Click Capture
        var captureButton = Find<Button>("Capture");
        captureButton.Click();
        Thread.Sleep(2000);

        // Verify elevated app was captured with Admin flag
        var appList = Find<Custom>("AppList");
        var capturedApps = appList.FindAll<Custom>(By.ClassName("AppItem"));

        bool foundElevatedApp = false;
        foreach (var app in capturedApps)
        {
            var appName = app.GetAttribute("Name");
            if (appName.Contains("Notepad", StringComparison.OrdinalIgnoreCase))
            {
                // Check if it has the Admin checkbox checked
                var adminCheckbox = app.Find<CheckBox>("Admin");
                if (adminCheckbox != null && adminCheckbox.IsChecked)
                {
                    foundElevatedApp = true;
                    break;
                }
            }
        }

        Assert.IsTrue(foundElevatedApp, "Elevated Notepad should be captured with Admin flag");

        // Cancel to clean up
        Find<Button>("Cancel").Click();
        Thread.Sleep(1000);

        // Close elevated app
        CloseElevatedNotepad();
    }

    [TestMethod("WorkspacesSnapshot.CaptureMinimizedApps")]
    [TestCategory("Workspaces Snapshot UI")]
    public void TestCaptureMinimizedApplications()
    {
        // Open and minimize applications
        OpenNotepad();
        Thread.Sleep(1000);
        MinimizeActiveWindow();
        Thread.Sleep(1000);

        // Click Create Workspace
        var createButton = Find<Button>("Create Workspace");
        createButton.Click();
        Thread.Sleep(1000);

        // Click Capture
        var captureButton = Find<Button>("Capture");
        captureButton.Click();
        Thread.Sleep(2000);

        // Verify minimized app was captured
        var appList = Find<Custom>("AppList");
        var capturedApps = appList.FindAll<Custom>(By.ClassName("AppItem"));

        bool foundNotepad = false;
        foreach (var app in capturedApps)
        {
            var appName = app.GetAttribute("Name");
            if (appName.Contains("Notepad", StringComparison.OrdinalIgnoreCase))
            {
                foundNotepad = true;
                break;
            }
        }

        Assert.IsTrue(foundNotepad, "Minimized Notepad should still be captured");

        // Cancel to clean up
        Find<Button>("Cancel").Click();
        Thread.Sleep(1000);

        // Close test application
        CloseNotepad();
    }

    [TestMethod("WorkspacesSnapshot.VerifyWindowPositions")]
    [TestCategory("Workspaces Snapshot UI")]
    public void TestSnapshotCapturesCorrectWindowPositions()
    {
        // Open applications at specific positions
        OpenNotepadAtPosition(100, 100, 600, 400);
        OpenCalculatorAtPosition(700, 100, 400, 500);
        Thread.Sleep(3000);

        // Click Create Workspace
        var createButton = Find<Button>("Create Workspace");
        createButton.Click();
        Thread.Sleep(1000);

        // Click Capture
        var captureButton = Find<Button>("Capture");
        captureButton.Click();
        Thread.Sleep(2000);

        // Verify windows have position data in preview
        var previewPane = Find<Pane>("Preview");
        Assert.IsNotNull(previewPane, "Preview pane should exist");

        // Look for window representations in the preview
        var windowPreviews = previewPane.FindAll<Custom>(By.ClassName("WindowPreview"));
        Assert.AreEqual(2, windowPreviews.Count, "Should have 2 window previews");

        // Verify each preview has position attributes
        foreach (var preview in windowPreviews)
        {
            var left = preview.GetAttribute("Left");
            var top = preview.GetAttribute("Top");
            var width = preview.GetAttribute("Width");
            var height = preview.GetAttribute("Height");

            Assert.IsFalse(string.IsNullOrEmpty(left), "Window preview should have Left position");
            Assert.IsFalse(string.IsNullOrEmpty(top), "Window preview should have Top position");
            Assert.IsFalse(string.IsNullOrEmpty(width), "Window preview should have Width");
            Assert.IsFalse(string.IsNullOrEmpty(height), "Window preview should have Height");
        }

        // Cancel to clean up
        Find<Button>("Cancel").Click();
        Thread.Sleep(1000);

        // Close test applications
        CloseNotepad();
        CloseCalculator();
    }

    [TestMethod("WorkspacesSnapshot.CaptureAfterOpeningNewWindow")]
    [TestCategory("Workspaces Snapshot UI")]
    public void TestCaptureIncludesNewlyOpenedWindows()
    {
        // Open initial application
        OpenNotepad();
        Thread.Sleep(2000);

        // Click Create Workspace
        var createButton = Find<Button>("Create Workspace");
        createButton.Click();
        Thread.Sleep(1000);

        // Open another window after clicking Create
        OpenCalculator();
        Thread.Sleep(2000);

        // Click Capture
        var captureButton = Find<Button>("Capture");
        captureButton.Click();
        Thread.Sleep(2000);

        // Verify both windows were captured
        var appList = Find<Custom>("AppList");
        var capturedApps = appList.FindAll<Custom>(By.ClassName("AppItem"));

        bool foundNotepad = false;
        bool foundCalculator = false;

        foreach (var app in capturedApps)
        {
            var appName = app.GetAttribute("Name");
            if (appName.Contains("Notepad", StringComparison.OrdinalIgnoreCase))
            {
                foundNotepad = true;
            }

            if (appName.Contains("Calculator", StringComparison.OrdinalIgnoreCase))
            {
                foundCalculator = true;
            }
        }

        Assert.IsTrue(foundNotepad, "Notepad should be captured");
        Assert.IsTrue(foundCalculator, "Calculator opened after Create should also be captured");

        // Cancel to clean up
        Find<Button>("Cancel").Click();
        Thread.Sleep(1000);

        // Close test applications
        CloseNotepad();
        CloseCalculator();
    }

    private void OpenNotepadAtPosition(int x, int y, int width, int height)
    {
        var process = Process.Start("notepad.exe");
        Thread.Sleep(1000);

        // Use WindowHelper to position the window
        // WindowHelper.MoveAndResizeWindow("Notepad", x, y, width, height);
    }

    private void OpenCalculatorAtPosition(int x, int y, int width, int height)
    {
        Process.Start("calc.exe");
        Thread.Sleep(1000);

        // WindowHelper.MoveAndResizeWindow("Calculator", x, y, width, height);
    }

    private void OpenElevatedNotepad()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "notepad.exe",
            UseShellExecute = true,
            Verb = "runas",
        };

        try
        {
            Process.Start(startInfo);
            Thread.Sleep(2000);
        }
        catch
        {
            // User might have cancelled UAC prompt
        }
    }

    private void CloseElevatedNotepad()
    {
        CloseNotepad();
    }

    private bool IsRunningElevated()
    {
        using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
        {
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
    }

    private void MinimizeActiveWindow()
    {
        SendKeys(Key.Win, Key.Down);
        Thread.Sleep(500);
    }
    */
}
