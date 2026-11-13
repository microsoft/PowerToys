// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesCsharpLibrary.Data;

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
        int count = GetWorkspaceCards().Count;
        StartCapture();
        var cancelButton = Find<Button>(By.AccessibilityId("CancelButton"));
        Assert.AreEqual("Cancel", cancelButton.Name, "The snapshot Cancel button should have an accessible name.");
        CancelSnapshot();
        Assert.AreEqual(count, GetWorkspaceCards().Count, "Canceling capture should not add a workspace.");
    }

    [TestMethod("WorkspacesSnapshot.CapturePackagedApps")]
    [TestCategory("Workspaces Snapshot UI")]
    public void TestCapturePackagedApplications()
    {
        OpenCalculator();
        try
        {
            Task.Delay(2000).Wait();
            StartCapture();
            CaptureWorkspace();

            // Read the native capture DTO without constructing a WinUI Project or its images.
            Assert.IsTrue(File.Exists(TempProjectData.File), "Capture should write the temporary workspace data.");
            var workspace = new ProjectData().Read(TempProjectData.File);
            Assert.IsTrue(Guid.TryParse(workspace.Id, out _), "The captured workspace should have a valid ID.");
            Assert.IsNotNull(workspace.Applications, "Workspace should contain a list of apps.");
            var calculator = workspace.Applications.Where(app => app.ApplicationPath.Contains("Calculator", StringComparison.OrdinalIgnoreCase)).ToList();
            Assert.IsTrue(calculator.Count > 0, "Calculator should be captured in the workspace data.");
            Assert.IsTrue(calculator.Any(app => !string.IsNullOrEmpty(app.PackageFullName) && !string.IsNullOrEmpty(app.AppUserModelId)), "The captured Calculator should retain its packaged application identity.");

            ReturnToMainPage();
            Assert.IsFalse(File.Exists(TempProjectData.File), "Canceling the new workspace should discard its temporary capture data.");
        }
        finally
        {
            CloseCalculator();
        }
    }
}
