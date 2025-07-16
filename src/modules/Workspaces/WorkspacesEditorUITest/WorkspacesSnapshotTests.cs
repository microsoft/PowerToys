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

        // OpenWindowsSettings();
        Task.Delay(2000).Wait();

        AttachWorkspacesEditor();
        var createButton = Find<Button>("Create Workspace");
        createButton.Click();
        Task.Delay(1000).Wait();

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

        // bool isSettingsFound = workspace.Applications.Any(app => app.AppPath.Contains("Settings", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(isCalculatorFound, "Calculator should be captured in the workspace data.");

        // Assert.IsTrue(isSettingsFound, "Settings should be captured in the workspace data.");

        // Cancel to clean up
        AttachWorkspacesEditor();
        Find<Button>("Cancel").Click();
        Task.Delay(1000).Wait();

        // Close test applications
        CloseCalculator();

        // CloseWindowsSettings();
    }
}
