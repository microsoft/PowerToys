// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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

        // The automation host has no protected-store role. Inspect the rendered capture.
        AttachWorkspacesEditor();
        Assert.IsNotNull(Find<Element>("Calculator"), "Calculator should be visible in the protected capture.");

        // Cancel to clean up
        AttachWorkspacesEditor();
        Find<Button>("Cancel").Click();
        Task.Delay(1000).Wait();

        // Close test applications
        CloseCalculator();

        // CloseWindowsSettings();
    }
}
