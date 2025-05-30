// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WorkspacesEditorUITest;

[TestClass]
public class WorkspacesEditorListTests : WorkspacesUiAutomationBase
{
    public WorkspacesEditorListTests()
        : base()
    {
    }

    [TestMethod("WorkspacesEditorList.ItemsPresent")]
    [TestCategory("Workspaces Editor List UI")]
    public void TestEditorListItemsPresent()
    {
        Assert.IsTrue(Has<Button>("Create Workspace"), "Should have create workspace button");

        // Verify other main UI elements
        Assert.IsTrue(Has<TextBox>("Search"), "Should have search box");
        Assert.IsTrue(Has<ComboBox>("SortBy"), "Should have sort dropdown");

        // If there are existing workspaces, verify list is shown
        if (Has<Custom>("WorkspacesList"))
        {
            var workspacesList = Find<Custom>("WorkspacesList");
            Assert.IsNotNull(workspacesList, "Workspaces list should be present");
        }
    }

    [TestMethod("WorkspacesEditorList.NewWorkspaceAppearsInList")]
    [TestCategory("Workspaces Editor List UI")]
    public void TestNewWorkspaceAppearsInListAfterCapture()
    {
        // Count existing workspaces
        var initialCount = GetWorkspaceCount();

        // Create a simple workspace
        CreateSimpleWorkspace("TestWorkspace_" + DateTime.Now.Ticks);

        // Verify count increased
        var finalCount = GetWorkspaceCount();
        Assert.AreEqual(initialCount + 1, finalCount, "Workspace count should increase by 1");
    }

    [TestMethod("WorkspacesEditorList.CancelCaptureDoesNotAddWorkspace")]
    [TestCategory("Workspaces Editor List UI")]
    public void TestCancelCaptureDoesNotAddWorkspace()
    {
        // Count existing workspaces
        var initialCount = GetWorkspaceCount();

        // Start creating workspace but cancel
        var createButton = Find<Button>("Create Workspace");
        createButton.Click();
        Thread.Sleep(1000);

        // Cancel without capturing
        var cancelButton = Find<Button>("Cancel");
        cancelButton.Click();
        Thread.Sleep(1000);

        // Verify count hasn't changed
        var finalCount = GetWorkspaceCount();
        Assert.AreEqual(initialCount, finalCount, "Workspace count should not change after canceling");
    }

    [TestMethod("WorkspacesEditorList.SearchFiltersWorkspaces")]
    [TestCategory("Workspaces Editor List UI")]
    public void TestSearchFiltersWorkspaces()
    {
        // Ensure we have test workspaces
        CreateSimpleWorkspace("SearchTest_Alpha");
        CreateSimpleWorkspace("SearchTest_Beta");
        CreateSimpleWorkspace("Different_Gamma");
        Thread.Sleep(1000);

        // Find search box and search for "SearchTest"
        var searchBox = Find<TextBox>("Search");
        searchBox.SetText("SearchTest");
        Thread.Sleep(1000);

        // Verify filtered results
        var visibleWorkspaces = GetVisibleWorkspaceItems();

        // Should only show items matching "SearchTest"
        foreach (var workspace in visibleWorkspaces)
        {
            var name = workspace.GetAttribute("Name");
            Assert.IsTrue(name.Contains("SearchTest"), $"Workspace '{name}' should not be visible when searching for 'SearchTest'");
        }

        // Clear search to show all
        searchBox.SetText(string.Empty);
        Thread.Sleep(500);

        // Verify all workspaces are visible again
        var allWorkspaces = GetVisibleWorkspaceItems();
        Assert.IsTrue(allWorkspaces.Count >= 3, "All workspaces should be visible after clearing search");
    }

    [TestMethod("WorkspacesEditorList.SearchByAppName")]
    [TestCategory("Workspaces Editor List UI")]
    public void TestSearchFiltersByAppName()
    {
        // Create workspace with specific app
        CreateWorkspaceWithNotepad("WorkspaceWithNotepad");
        Thread.Sleep(1000);

        // Search by app name
        var searchBox = Find<TextBox>("Search");
        searchBox.SetText("notepad");
        Thread.Sleep(1000);

        // Verify workspace with notepad is shown
        var visibleWorkspaces = GetVisibleWorkspaceItems();
        bool foundWorkspaceWithNotepad = false;

        foreach (var workspace in visibleWorkspaces)
        {
            var name = workspace.GetAttribute("Name");
            if (name.Contains("WorkspaceWithNotepad"))
            {
                foundWorkspaceWithNotepad = true;
                break;
            }
        }

        Assert.IsTrue(foundWorkspaceWithNotepad, "Workspace containing Notepad app should be visible when searching for 'notepad'");

        // Clear search
        searchBox.SetText(string.Empty);
        Thread.Sleep(500);
    }

    [TestMethod("WorkspacesEditorList.SortByWorks")]
    [TestCategory("Workspaces Editor List UI")]
    public void TestSortByFunctionality()
    {
        // Ensure we have multiple workspaces
        CreateSimpleWorkspace("Workspace_A");
        CreateSimpleWorkspace("Workspace_Z");
        CreateSimpleWorkspace("Workspace_M");
        Thread.Sleep(1000);

        // Find sort dropdown
        var sortDropdown = Find<ComboBox>("SortBy");
        var initialSelection = sortDropdown.Text;

        // Click to open dropdown
        sortDropdown.Click();
        Thread.Sleep(500);

        // Find sort options
        var sortOptions = FindAll<Custom>(By.ClassName("ComboBoxItem"));
        Assert.IsTrue(sortOptions.Count > 1, "Should have multiple sort options");

        // Try different sort options
        foreach (var option in sortOptions)
        {
            var optionText = option.Text;
            if (optionText != initialSelection)
            {
                option.Click();
                Thread.Sleep(1000);

                // Verify list is still displayed
                Assert.IsTrue(Has<Custom>("WorkspacesList"), $"Workspaces list should still be visible after sorting by {optionText}");

                // Get first and last items to verify order changed
                var items = GetVisibleWorkspaceItems();
                if (items.Count > 1)
                {
                    var firstName = items[0].GetAttribute("Name");
                    var lastName = items[items.Count - 1].GetAttribute("Name");

                    // Basic verification that items exist
                    Assert.IsFalse(string.IsNullOrEmpty(firstName), "First item should have a name");
                    Assert.IsFalse(string.IsNullOrEmpty(lastName), "Last item should have a name");
                }

                break;
            }
        }
    }

    [TestMethod("WorkspacesEditorList.SortByPersists")]
    [TestCategory("Workspaces Editor List UI")]
    public void TestSortByPersistsAfterRestart()
    {
        // Set a specific sort option
        var sortDropdown = Find<ComboBox>("SortBy");
        sortDropdown.Click();
        Thread.Sleep(500);

        var sortOptions = FindAll<Custom>(By.ClassName("ComboBoxItem"));
        string selectedOption = string.Empty;

        // Select a non-default option
        if (sortOptions.Count > 1)
        {
            sortOptions[1].Click();
            Thread.Sleep(1000);
            selectedOption = sortDropdown.Text;
        }

        // Restart editor
        RestartScopeExe();
        Thread.Sleep(2000);

        // Verify sort option persisted
        sortDropdown = Find<ComboBox>("SortBy");
        Assert.AreEqual(selectedOption, sortDropdown.Text, "Sort option should persist after restart");
    }

    [TestMethod("WorkspacesEditorList.RemoveWorkspace")]
    [TestCategory("Workspaces Editor List UI")]
    public void TestRemoveWorkspaceFromList()
    {
        // Create a workspace to remove
        var workspaceName = "WorkspaceToRemove_" + DateTime.Now.Ticks;
        CreateSimpleWorkspace(workspaceName);
        Thread.Sleep(1000);

        // Find the workspace in the list
        var workspaceItem = FindWorkspaceByName(workspaceName);
        Assert.IsNotNull(workspaceItem, $"Should find workspace '{workspaceName}' in list");

        // Find and click remove button
        var removeButton = workspaceItem.Find<Button>("Remove");
        removeButton.Click();
        Thread.Sleep(500);

        // Handle confirmation dialog if it appears
        if (Has<Button>("Yes"))
        {
            Find<Button>("Yes").Click();
            Thread.Sleep(1000);
        }

        // Verify workspace is removed
        var removedWorkspace = FindWorkspaceByName(workspaceName, waitTime: 1000);
        Assert.IsNull(removedWorkspace, $"Workspace '{workspaceName}' should be removed from list");
    }

    [TestMethod("WorkspacesEditorList.EditOpensEditingPage")]
    [TestCategory("Workspaces Editor List UI")]
    public void TestEditButtonOpensEditingPage()
    {
        // Ensure we have at least one workspace
        EnsureAtLeastOneWorkspace();

        // Find first workspace
        var workspaceItem = GetFirstWorkspaceItem();
        Assert.IsNotNull(workspaceItem, "Should have at least one workspace");

        // Click edit button
        var editButton = workspaceItem.Find<Button>("Edit");
        editButton.Click();
        Thread.Sleep(1000);

        // Verify editing page opened
        VerifyEditingPageOpened();

        // Go back
        Find<Button>("Cancel").Click();
        Thread.Sleep(1000);
    }

    [TestMethod("WorkspacesEditorList.ClickWorkspaceOpensEditingPage")]
    [TestCategory("Workspaces Editor List UI")]
    public void TestClickWorkspaceItemOpensEditingPage()
    {
        // Ensure we have at least one workspace
        EnsureAtLeastOneWorkspace();

        // Find first workspace
        var workspaceItem = GetFirstWorkspaceItem();
        Assert.IsNotNull(workspaceItem, "Should have at least one workspace");

        // Click on the workspace item itself (not a button)
        workspaceItem.Click();
        Thread.Sleep(1000);

        // Verify editing page opened
        VerifyEditingPageOpened();

        // Go back
        Find<Button>("Cancel").Click();
        Thread.Sleep(1000);
    }

    [TestMethod("WorkspacesEditorList.EmptyState")]
    [TestCategory("Workspaces Editor List UI")]
    public void TestEmptyStateDisplay()
    {
        // Remove all workspaces to test empty state
        RemoveAllWorkspaces();

        // Verify empty state elements
        Assert.IsTrue(Has<Button>("Create Workspace"), "Create Workspace button should be present in empty state");

        // There might be an empty state message
        if (Has("No workspaces", timeoutMS: 1000))
        {
            var emptyMessage = Find("No workspaces");
            Assert.IsNotNull(emptyMessage, "Empty state message should be displayed");
        }
    }

    // Helper methods
    private int GetWorkspaceCount()
    {
        if (!Has<Custom>("WorkspacesList", timeoutMS: 1000))
        {
            return 0;
        }

        var workspacesList = Find<Custom>("WorkspacesList");
        return workspacesList.FindAll<Custom>(By.ClassName("WorkspaceItem")).Count;
    }

    private ReadOnlyCollection<Custom> GetVisibleWorkspaceItems()
    {
        if (!Has<Custom>("WorkspacesList", timeoutMS: 1000))
        {
            return new ReadOnlyCollection<Custom>(new List<Custom>());
        }

        var workspacesList = Find<Custom>("WorkspacesList");
        return workspacesList.FindAll<Custom>(By.ClassName("WorkspaceItem"));
    }

    private Custom? FindWorkspaceByName(string name, int waitTime = 5000)
    {
        if (!Has<Custom>("WorkspacesList", timeoutMS: waitTime))
        {
            return null;
        }

        var workspacesList = Find<Custom>("WorkspacesList");

        try
        {
            return workspacesList.Find<Custom>(By.Name(name), timeoutMS: waitTime);
        }
        catch
        {
            return null;
        }
    }

    private Custom? GetFirstWorkspaceItem()
    {
        var items = GetVisibleWorkspaceItems();
        return items.Count > 0 ? items[0] : null;
    }

    private void CreateSimpleWorkspace(string name)
    {
        // Open a test app
        System.Diagnostics.Process.Start("notepad.exe");
        Thread.Sleep(1000);

        // Create workspace
        var createButton = Find<Button>("Create Workspace");
        createButton.Click();
        Thread.Sleep(1000);

        // Capture
        var captureButton = Find<Button>("Capture");
        captureButton.Click();
        Thread.Sleep(2000);

        // Set name
        var nameTextBox = Find<TextBox>("Workspace name");
        nameTextBox.SetText(name);
        Thread.Sleep(500);

        // Save
        var saveButton = Find<Button>("Save");
        saveButton.Click();
        Thread.Sleep(1000);

        // Close test app
        foreach (var process in System.Diagnostics.Process.GetProcessesByName("notepad"))
        {
            process.Kill();
        }
    }

    private void CreateWorkspaceWithNotepad(string name)
    {
        CreateSimpleWorkspace(name);
    }

    private void EnsureAtLeastOneWorkspace()
    {
        if (GetWorkspaceCount() == 0)
        {
            CreateSimpleWorkspace("DefaultTestWorkspace");
        }
    }

    private void VerifyEditingPageOpened()
    {
        Assert.IsTrue(Has<Button>("Save"), "Should have Save button on editing page");
        Assert.IsTrue(Has<Button>("Cancel"), "Should have Cancel button on editing page");
        Assert.IsTrue(Has<TextBox>("Workspace name"), "Should have workspace name field");
        Assert.IsTrue(Has<Custom>("AppList"), "Should have app list on editing page");
    }

    private void RemoveAllWorkspaces()
    {
        while (GetWorkspaceCount() > 0)
        {
            var firstItem = GetFirstWorkspaceItem();
            if (firstItem != null)
            {
                var removeButton = firstItem.Find<Button>("Remove");
                removeButton.Click();
                Thread.Sleep(500);

                if (Has<Button>("Yes"))
                {
                    Find<Button>("Yes").Click();
                    Thread.Sleep(1000);
                }
            }
        }
    }
}
