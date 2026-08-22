// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WorkspacesEditorUITest;

/// <summary>
/// Design validation tests for the Snapshot/Capture window.
/// This window appears when creating a new workspace and shows
/// a "Capture" button overlay on the desktop.
///
/// These tests validate the capture flow UI elements exist
/// and are accessible for the WinUI migration.
/// </summary>
[TestClass]
public class SnapshotWindowDesignTests : WorkspacesUiAutomationBase
{
    public SnapshotWindowDesignTests()
        : base()
    {
    }

    [TestMethod("SnapshotWindow.HasCaptureButton")]
    [TestCategory("Design.SnapshotWindow")]
    public void SnapshotWindow_HasCaptureButton()
    {
        AttachWorkspacesEditor();

        var createButton = Find<Button>("Create Workspace");
        createButton.Click();
        Task.Delay(1000).Wait();

        AttachSnapshotWindow();

        Assert.IsTrue(Has<Button>("Capture"), "Snapshot window should have a Capture button");

        // Cancel to clean up
        var cancelButton = Find<Button>("Cancel");
        cancelButton.Click();
        Task.Delay(500).Wait();
    }

    [TestMethod("SnapshotWindow.HasCancelButton")]
    [TestCategory("Design.SnapshotWindow")]
    public void SnapshotWindow_HasCancelButton()
    {
        AttachWorkspacesEditor();

        var createButton = Find<Button>("Create Workspace");
        createButton.Click();
        Task.Delay(1000).Wait();

        AttachSnapshotWindow();

        Assert.IsTrue(Has<Button>("Cancel"), "Snapshot window should have a Cancel button");

        // Clean up
        Find<Button>("Cancel").Click();
        Task.Delay(500).Wait();
    }

    [TestMethod("SnapshotWindow.CancelReturnsToEditor")]
    [TestCategory("Design.SnapshotWindow")]
    public void SnapshotWindow_CancelButton_ReturnsToEditor()
    {
        AttachWorkspacesEditor();

        var createButton = Find<Button>("Create Workspace");
        createButton.Click();
        Task.Delay(1000).Wait();

        AttachSnapshotWindow();

        Find<Button>("Cancel").Click();
        Task.Delay(1000).Wait();

        // Should be back in the editor
        AttachWorkspacesEditor();
        Assert.IsTrue(Has<Button>("Create Workspace"), "After cancel, should return to editor with Create button visible");
    }

    [TestMethod("SnapshotWindow.Accessibility.ButtonsHaveNames")]
    [TestCategory("Design.SnapshotWindow")]
    public void SnapshotWindow_Buttons_HaveAccessibleNames()
    {
        AttachWorkspacesEditor();

        var createButton = Find<Button>("Create Workspace");
        createButton.Click();
        Task.Delay(1000).Wait();

        AttachSnapshotWindow();

        // Both buttons should be findable by name (meaning they have accessible names)
        var capture = Find<Button>("Capture");
        var cancel = Find<Button>("Cancel");

        Assert.IsNotNull(capture, "Capture button should have an accessible name");
        Assert.IsNotNull(cancel, "Cancel button should have an accessible name");

        // Clean up
        cancel.Click();
        Task.Delay(500).Wait();
    }
}
