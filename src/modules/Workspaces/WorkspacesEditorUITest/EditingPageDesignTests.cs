// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesCsharpLibrary.Data;

namespace WorkspacesEditorUITest;

/// <summary>
/// Design validation tests for the Workspace Editing page.
/// This page appears when a user clicks a workspace card
/// and shows the app list with positioning controls.
///
/// UI elements that must be preserved:
/// - Workspace name text box
/// - App list with per-app controls
/// - Save button and title-bar back navigation
/// - Position controls (X, Y, Width, Height or Maximized/Minimized dropdown)
/// </summary>
[TestClass]
public class EditingPageDesignTests : WorkspacesUiAutomationBase
{
    private string workspaceName = string.Empty;

    public EditingPageDesignTests()
        : base()
    {
    }

    [TestInitialize]
    public void Setup()
    {
        workspaceName = "LegacyEditDesign_" + Guid.NewGuid().ToString("N");
        CreateTestWorkspace(workspaceName);
    }

    [TestCleanup]
    public void CleanupWorkspace()
    {
        AttachWorkspacesEditor();
        if (Has(By.AccessibilityId("EditNameTextBox"), timeoutMS: 500))
        {
            ReturnToMainPage();
        }

        if (GetWorkspaceCards().Any(card => card.Name == workspaceName))
        {
            DeleteWorkspace(workspaceName);
        }
    }

    [TestMethod("EditingPage.HasNameTextBox")]
    [TestCategory("Design.EditingPage")]
    public void EditingPage_HasWorkspaceNameInput()
    {
        NavigateToEditPage();

        var name = Find<TextBox>(By.AccessibilityId("EditNameTextBox"));
        Assert.IsTrue(name.Displayed && name.Enabled, "Editing page should have an enabled workspace name input.");
        Assert.AreEqual(workspaceName, name.Text, "The name input should show the selected workspace.");

        CancelAndReturn();
    }

    [TestMethod("EditingPage.HasSaveButton")]
    [TestCategory("Design.EditingPage")]
    public void EditingPage_HasSaveButton()
    {
        NavigateToEditPage();

        var save = Find<Button>(By.AccessibilityId("SaveButton"));
        Assert.IsTrue(save.Displayed && save.Enabled, "Editing page should have an enabled Save button.");

        CancelAndReturn();
    }

    [TestMethod("EditingPage.HasBackButton")]
    [TestCategory("Design.EditingPage")]
    public void EditingPage_HasBackButton()
    {
        NavigateToEditPage();

        var back = Find<Button>(By.AccessibilityId("PART_BackButton"));
        Assert.IsTrue(back.Displayed && back.Enabled, "Editing page should have an enabled title-bar back button.");

        CancelAndReturn();
    }

    [TestMethod("EditingPage.HasLaunchAndEditButton")]
    [TestCategory("Design.EditingPage")]
    public void EditingPage_HasLaunchAndEditButton()
    {
        NavigateToEditPage();

        var launch = Find<Button>(By.AccessibilityId("LaunchEditButton"));
        Assert.IsTrue(launch.Displayed && launch.Enabled, "Editing page should have an enabled Launch and Edit button.");

        CancelAndReturn();
    }

    [TestMethod("EditingPage.HasAppList")]
    [TestCategory("Design.EditingPage")]
    public void EditingPage_HasApplicationsList()
    {
        NavigateToEditPage();

        var storage = new WorkspacesData();
        var workspace = storage.Read(storage.File).Workspaces.Single(project => project.Name == workspaceName);
        Assert.IsTrue(workspace.Applications.Count > 0, "The captured fixture should contain applications.");

        // Verify rendered application rows without requiring an ItemsRepeater UIA container peer.
        var buttons = FindAll<Button>(By.AccessibilityId("ToggleApplicationInclusionButton"))
            .Where(button => button.Displayed)
            .ToList();
        Assert.IsTrue(buttons.Count > 0, "The application list should expose visible inclusion controls.");
        foreach (var button in buttons)
        {
            Assert.IsTrue(button.Enabled, "An included application should have an enabled Remove button.");
            var applications = workspace.Applications.Where(app => button.Name == $"Remove {app.Application}").ToList();
            Assert.IsTrue(applications.Count > 0, "Each inclusion control should identify a captured application.");
            Assert.IsTrue(FindAll<Element>(By.AccessibilityId(applications[0].Application)).Any(row => row.Displayed && row.Enabled), "Each visible inclusion control should have a visible application row.");
        }

        GetPreviewImage();

        CancelAndReturn();
    }

    [TestMethod("EditingPage.Back_ReturnsToMainPage")]
    [TestCategory("Design.EditingPage")]
    public void EditingPage_Back_ReturnsToMainList()
    {
        NavigateToEditPage();

        Find<TextBox>(By.AccessibilityId("EditNameTextBox")).SetText(workspaceName + "_unsaved");
        ReturnToMainPage();
        Assert.AreEqual(workspaceName, GetWorkspaceCard(workspaceName).Name, "Back navigation should preserve the saved workspace name.");
        Assert.IsFalse(GetWorkspaceCards().Any(card => card.Name == workspaceName + "_unsaved"), "Back navigation should discard the unsaved name.");
    }

    private void NavigateToEditPage()
    {
        OpenWorkspace(workspaceName);
    }

    private void CancelAndReturn()
    {
        ReturnToMainPage();
    }
}
