// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WorkspacesEditorUITest;

/// <summary>
/// Design validation tests for the Workspace Editing page.
/// This page appears when a user clicks "Edit" on a workspace
/// and shows the app list with positioning controls.
///
/// UI elements that must be preserved:
/// - Workspace name text box
/// - App list with per-app controls
/// - Save/Cancel buttons
/// - Position controls (X, Y, Width, Height or Maximized/Minimized dropdown)
/// </summary>
[TestClass]
public class EditingPageDesignTests : WorkspacesUiAutomationBase
{
    public EditingPageDesignTests()
        : base()
    {
    }

    [TestInitialize]
    public void Setup()
    {
        // Ensure at least one workspace exists
        AttachWorkspacesEditor();
        if (!Has<Element>(By.AccessibilityId("WorkspacesItemsControl")))
        {
            CreateTestWorkspace("EditDesignTest");
            Task.Delay(2000).Wait();
        }
    }

    [TestMethod("EditingPage.HasNameTextBox")]
    [TestCategory("Design.EditingPage")]
    public void EditingPage_HasWorkspaceNameInput()
    {
        NavigateToEditPage();

        Assert.IsTrue(
            Has<TextBox>(By.AccessibilityId("EditNameTextBox")) || Has<TextBox>(By.Name("Workspace name")),
            "Editing page should have a workspace name text box");

        CancelAndReturn();
    }

    [TestMethod("EditingPage.HasSaveButton")]
    [TestCategory("Design.EditingPage")]
    public void EditingPage_HasSaveButton()
    {
        NavigateToEditPage();

        Assert.IsTrue(
            Has<Button>("Save Workspace") || Has<Button>("Save"),
            "Editing page should have a Save button");

        CancelAndReturn();
    }

    [TestMethod("EditingPage.HasCancelButton")]
    [TestCategory("Design.EditingPage")]
    public void EditingPage_HasCancelButton()
    {
        NavigateToEditPage();

        Assert.IsTrue(Has<Button>("Cancel"), "Editing page should have a Cancel button");

        CancelAndReturn();
    }

    [TestMethod("EditingPage.HasLaunchAndEditButton")]
    [TestCategory("Design.EditingPage")]
    public void EditingPage_HasLaunchAndEditButton()
    {
        NavigateToEditPage();

        Assert.IsTrue(
            Has<Button>("Launch & Edit") || Has<Button>("Launch and Edit"),
            "Editing page should have a 'Launch & Edit' button");

        CancelAndReturn();
    }

    [TestMethod("EditingPage.HasAppList")]
    [TestCategory("Design.EditingPage")]
    public void EditingPage_HasApplicationsList()
    {
        NavigateToEditPage();

        // Should have some app items visible
        Assert.IsTrue(
            Has<Element>(By.AccessibilityId("AppList")) || Has<Custom>("AppList"),
            "Editing page should have an application list");

        CancelAndReturn();
    }

    [TestMethod("EditingPage.Cancel_ReturnsToMainPage")]
    [TestCategory("Design.EditingPage")]
    public void EditingPage_Cancel_ReturnsToMainList()
    {
        NavigateToEditPage();

        Find<Button>("Cancel").Click();
        Task.Delay(1000).Wait();

        Assert.IsTrue(Has<Button>("Create Workspace"), "After cancel, should return to main page");
    }

    private void NavigateToEditPage()
    {
        AttachWorkspacesEditor();
        try
        {
            var root = Find<Element>(By.AccessibilityId("WorkspacesItemsControl"));
            var moreButton = root.Find<Button>(By.AccessibilityId("MoreButton"));
            moreButton.Click();
            Task.Delay(500).Wait();

            var editButton = Find<Button>(By.Name("Edit"));
            editButton.Click();
            Task.Delay(1000).Wait();
        }
        catch
        {
            // If edit via more menu doesn't work, try direct edit button
            var editButton = Find<Button>(By.Name("Edit"));
            editButton?.Click();
            Task.Delay(1000).Wait();
        }
    }

    private void CancelAndReturn()
    {
        try
        {
            Find<Button>("Cancel").Click();
            Task.Delay(500).Wait();
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
