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
        StartCapture();
        var capture = Find<Button>(By.AccessibilityId("SnapshotButton"));
        Assert.IsTrue(capture.Displayed && capture.Enabled, "Snapshot window should have an enabled Capture button.");
        CancelSnapshot();
    }

    [TestMethod("SnapshotWindow.HasCancelButton")]
    [TestCategory("Design.SnapshotWindow")]
    public void SnapshotWindow_HasCancelButton()
    {
        StartCapture();
        var cancel = Find<Button>(By.AccessibilityId("CancelButton"));
        Assert.IsTrue(cancel.Displayed && cancel.Enabled, "Snapshot window should have an enabled Cancel button.");
        CancelSnapshot();
    }

    [TestMethod("SnapshotWindow.CancelReturnsToEditor")]
    [TestCategory("Design.SnapshotWindow")]
    public void SnapshotWindow_CancelButton_ReturnsToEditor()
    {
        var ids = GetWorkspaceCards().Select(card => card.AutomationId).ToArray();
        StartCapture();
        CancelSnapshot();
        CollectionAssert.AreEquivalent(ids, GetWorkspaceCards().Select(card => card.AutomationId).ToArray(), "Canceling capture should preserve the workspace list.");
    }

    [TestMethod("SnapshotWindow.Accessibility.ButtonsHaveNames")]
    [TestCategory("Design.SnapshotWindow")]
    public void SnapshotWindow_Buttons_HaveAccessibleNames()
    {
        StartCapture();
        var capture = Find<Button>(By.AccessibilityId("SnapshotButton"));
        var cancel = Find<Button>(By.AccessibilityId("CancelButton"));
        Assert.AreEqual("Capture", capture.Name, "Capture button should have its localized accessible name.");
        Assert.AreEqual("Cancel", cancel.Name, "Cancel button should have its localized accessible name.");
        CancelSnapshot();
    }
}
