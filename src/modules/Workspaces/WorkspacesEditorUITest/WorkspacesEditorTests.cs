// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WorkspacesEditorUITest;

[TestClass]
public class WorkspacesEditorTests : WorkspacesUiAutomationBase
{
    public WorkspacesEditorTests()
        : base()
    {
    }

    [TestMethod("WorkspacesEditor.Items.Present")]
    [TestCategory("Workspaces UI")]
    public void TestItemsPresents()
    {
        Assert.IsTrue(this.Has<Button>("Create Workspace"), "Should have create workspace button");
    }

    /*
    [TestMethod("WorkspacesEditor.Editor.NewWorkspaceAppearsInList")]
    [TestCategory("Workspaces UI")]
    public void TestNewWorkspaceAppearsInListAfterCapture()
    {
        // Open test application
        OpenNotepad();
        Thread.Sleep(2000);

        // Create workspace
        var createButton = Find<Button>("Create Workspace");
        createButton.Click();
        Thread.Sleep(1000);

        // Capture
        var captureButton = Find<Button>("Capture");
        captureButton.Click();
        Thread.Sleep(2000);

        // Save workspace
        var saveButton = Find<Button>("Save");
        saveButton.Click();
        Thread.Sleep(1000);

        // Verify workspace appears in list
        var workspacesList = Find<Custom>("WorkspacesList");
        var workspaceItems = workspacesList.FindAll<Custom>(By.ClassName("WorkspaceItem"));
        Assert.IsTrue(workspaceItems.Count > 0, "New workspace should appear in the list");

        CloseNotepad();
    }

    [TestMethod("WorkspacesEditor.Editor.CancelCaptureDoesNotAddWorkspace")]
    [TestCategory("Workspaces UI")]
    public void TestCancelCaptureDoesNotAddWorkspace()
    {
        // Count existing workspaces
        var workspacesList = Find<Custom>("WorkspacesList");
        var initialCount = workspacesList.FindAll<Custom>(By.ClassName("WorkspaceItem")).Count;

        // Create workspace
        var createButton = Find<Button>("Create Workspace");
        createButton.Click();
        Thread.Sleep(1000);

        // Cancel
        var cancelButton = Find<Button>("Cancel");
        cancelButton.Click();
        Thread.Sleep(1000);

        // Verify count hasn't changed
        var finalCount = workspacesList.FindAll<Custom>(By.ClassName("WorkspaceItem")).Count;
        Assert.AreEqual(initialCount, finalCount, "Workspace count should not change after canceling");
    }

    [TestMethod("WorkspacesEditor.Editor.SearchFiltersWorkspaces")]
    [TestCategory("Workspaces UI")]
    public void TestSearchFiltersWorkspaces()
    {
        // Create test workspaces first
        CreateTestWorkspace("TestWorkspace1");
        CreateTestWorkspace("TestWorkspace2");
        CreateTestWorkspace("DifferentName");

        // Find search box
        var searchBox = Find<TextBox>("Search");
        searchBox.SetText("TestWorkspace");
        Thread.Sleep(1000);

        // Verify filtered results
        var workspacesList = Find<Custom>("WorkspacesList");
        var visibleItems = workspacesList.FindAll<Custom>(By.ClassName("WorkspaceItem"));

        // Should only show items matching "TestWorkspace"
        Assert.IsTrue(visibleItems.Count >= 2, "Should show at least 2 TestWorkspace items");

        // Clear search
        searchBox.SetText(string.Empty);
        Thread.Sleep(500);
    }

    [TestMethod("WorkspacesEditor.Editor.SortByWorks")]
    [TestCategory("Workspaces UI")]
    public void TestSortByFunctionality()
    {
        // Find sort dropdown
        var sortDropdown = Find<ComboBox>("SortBy");
        sortDropdown.Click();
        Thread.Sleep(500);

        // Select different sort options
        var sortOptions = FindAll<Custom>(By.ClassName("ComboBoxItem"));
        if (sortOptions.Count > 1)
        {
            sortOptions[1].Click(); // Select second option
            Thread.Sleep(1000);

            // Verify list is updated (we can't easily verify sort order in UI tests)
            var workspacesList = Find<Custom>("WorkspacesList");
            Assert.IsNotNull(workspacesList, "Workspaces list should still be visible after sorting");
        }
    }

    [TestMethod("WorkspacesEditor.Editor.SortByPersists")]
    [TestCategory("Workspaces UI")]
    public void TestSortByPersistsAfterRestart()
    {
        // Set sort option
        var sortDropdown = Find<ComboBox>("SortBy");
        sortDropdown.Click();
        Thread.Sleep(500);

        var sortOptions = FindAll<Custom>(By.ClassName("ComboBoxItem"));
        string selectedOption = string.Empty;
        if (sortOptions.Count > 1)
        {
            sortOptions[1].Click(); // Select second option
            selectedOption = sortDropdown.Text;
            Thread.Sleep(1000);
        }

        // Restart editor
        RestartScopeExe();
        Thread.Sleep(2000);

        // Verify sort option persisted
        sortDropdown = Find<ComboBox>("SortBy");
        Assert.AreEqual(selectedOption, sortDropdown.Text, "Sort option should persist after restart");
    }

    [TestMethod("WorkspacesEditor.Editor.RemoveWorkspace")]
    [TestCategory("Workspaces UI")]
    public void TestRemoveWorkspace()
    {
        // Create a test workspace
        CreateTestWorkspace("WorkspaceToRemove");

        // Find the workspace in the list
        var workspacesList = Find<Custom>("WorkspacesList");
        var workspaceItem = workspacesList.Find<Custom>(By.Name("WorkspaceToRemove"));

        // Click remove button
        var removeButton = workspaceItem.Find<Button>("Remove");
        removeButton.Click();
        Thread.Sleep(1000);

        // Confirm removal if dialog appears
        if (Has<Button>("Yes"))
        {
            Find<Button>("Yes").Click();
            Thread.Sleep(1000);
        }

        // Verify workspace is removed
        Assert.IsFalse(Has(By.Name("WorkspaceToRemove")), "Workspace should be removed from list");
    }

    [TestMethod("WorkspacesEditor.Editor.EditOpensEditingPage")]
    [TestCategory("Workspaces UI")]
    public void TestEditOpensEditingPage()
    {
        // Create a test workspace if none exist
        if (!Has<Custom>("WorkspacesList"))
        {
            CreateTestWorkspace("TestWorkspace");
        }

        // Find first workspace
        var workspacesList = Find<Custom>("WorkspacesList");
        var workspaceItem = workspacesList.FindAll<Custom>(By.ClassName("WorkspaceItem"))[0];

        // Click edit button
        var editButton = workspaceItem.Find<Button>("Edit");
        editButton.Click();
        Thread.Sleep(1000);

        // Verify editing page opened
        Assert.IsTrue(Has<Button>("Save"), "Should have Save button on editing page");
        Assert.IsTrue(Has<Button>("Cancel"), "Should have Cancel button on editing page");

        // Go back
        Find<Button>("Cancel").Click();
        Thread.Sleep(1000);
    }

    [TestMethod("WorkspacesEditor.Editor.ClickWorkspaceOpensEditingPage")]
    [TestCategory("Workspaces UI")]
    public void TestClickWorkspaceOpensEditingPage()
    {
        // Create a test workspace if none exist
        if (!Has<Custom>("WorkspacesList"))
        {
            CreateTestWorkspace("TestWorkspace");
        }

        // Find first workspace
        var workspacesList = Find<Custom>("WorkspacesList");
        var workspaceItem = workspacesList.FindAll<Custom>(By.ClassName("WorkspaceItem"))[0];

        // Click on the workspace item itself
        workspaceItem.Click();
        Thread.Sleep(1000);

        // Verify editing page opened
        Assert.IsTrue(Has<Button>("Save"), "Should have Save button on editing page");
        Assert.IsTrue(Has<Button>("Cancel"), "Should have Cancel button on editing page");

        // Go back
        Find<Button>("Cancel").Click();
        Thread.Sleep(1000);
    }
    */
}
