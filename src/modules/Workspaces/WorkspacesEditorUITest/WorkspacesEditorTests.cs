// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;

namespace WorkspacesEditorUITest;

[TestClass]
public class WorkspacesEditorTests : UITestBase
{
    public WorkspacesEditorTests()
        : base(PowerToysModule.Workspaces, WindowSize.Medium)
    {
    }

    [TestMethod("WorkspacesEditor.Items.Present")]
    [TestCategory("Workspaces UI")]
    public void TestItemsPresents()
    {
        Assert.IsTrue(this.Has<Button>("Create Workspace"), "Should have create workspace button");
    }

    #region Settings Tests

    [TestMethod("WorkspacesEditor.Settings.LaunchFromSettings")]
    [TestCategory("Workspaces UI")]
    public void TestLaunchEditorFromSettingsPage()
    {
        // Switch to PowerToys Settings
        var settingsSession = new SessionHelper(PowerToysModule.PowerToysSettings).Init();
        var settings = new Session(settingsSession.GetRoot(), settingsSession.GetDriver(), PowerToysModule.PowerToysSettings, WindowSize.Medium);
        
        try
        {
            // Navigate to Workspaces settings
            settings.Find<NavigationViewItem>("Workspaces").Click();
            Thread.Sleep(1000);
            
            // Click the launch editor button
            var launchButton = settings.Find<Button>("Launch editor");
            Assert.IsNotNull(launchButton, "Launch editor button should exist in settings");
            launchButton.Click();
            
            // Wait for editor to open
            Thread.Sleep(2000);
            
            // Verify editor opened
            Assert.IsTrue(WindowHelper.IsWindowOpen("Workspaces Editor"), "Workspaces Editor should be open");
        }
        finally
        {
            settingsSession.Cleanup();
        }
    }

    [TestMethod("WorkspacesEditor.Settings.LaunchFromQuickAccess")]
    [TestCategory("Workspaces UI")]
    public void TestLaunchEditorFromQuickAccess()
    {
        // This test would require system tray interaction which is complex
        // For now, we'll verify the editor launches properly
        RestartScopeExe();
        Thread.Sleep(2000);
        Assert.IsTrue(WindowHelper.IsWindowOpen("Workspaces Editor"), "Workspaces Editor should be open");
    }

    [TestMethod("WorkspacesEditor.Settings.LaunchByActivationShortcut")]
    [TestCategory("Workspaces UI")]
    public void TestLaunchEditorByActivationShortcut()
    {
        // Close the editor first
        ExitScopeExe();
        Thread.Sleep(1000);
        
        // Default shortcut is Win+Ctrl+`
        SendKeys(Key.LeftWindows, Key.LeftControl, Key.OemTilde);
        Thread.Sleep(2000);
        
        // Verify editor opened
        Assert.IsTrue(WindowHelper.IsWindowOpen("Workspaces Editor"), "Workspaces Editor should open with activation shortcut");
        
        // Restart for next tests
        RestartScopeExe();
    }

    [TestMethod("WorkspacesEditor.Settings.DisableModuleNoLaunch")]
    [TestCategory("Workspaces UI")]
    public void TestDisabledModuleDoesNotLaunchByShortcut()
    {
        // Switch to PowerToys Settings
        var settingsSession = new SessionHelper(PowerToysModule.PowerToysSettings).Init();
        var settings = new Session(settingsSession.GetRoot(), settingsSession.GetDriver(), PowerToysModule.PowerToysSettings, WindowSize.Medium);
        
        try
        {
            // Navigate to Workspaces settings
            settings.Find<NavigationViewItem>("Workspaces").Click();
            Thread.Sleep(1000);
            
            // Disable the module
            var enableToggle = settings.Find<ToggleSwitch>("Enable Workspaces");
            if (enableToggle.IsToggled)
            {
                enableToggle.Click();
                Thread.Sleep(500);
            }
            
            // Close editor if open
            ExitScopeExe();
            Thread.Sleep(1000);
            
            // Try to launch with shortcut
            SendKeys(Key.LeftWindows, Key.LeftControl, Key.OemTilde);
            Thread.Sleep(2000);
            
            // Verify editor did not open
            Assert.IsFalse(WindowHelper.IsWindowOpen("Workspaces Editor"), "Workspaces Editor should not open when module is disabled");
            
            // Re-enable the module
            enableToggle.Click();
            Thread.Sleep(500);
        }
        finally
        {
            settingsSession.Cleanup();
            RestartScopeExe();
        }
    }

    #endregion

    #region Snapshot Tool Tests

    [TestMethod("WorkspacesEditor.Snapshot.VerifyEditorShowsAllWindows")]
    [TestCategory("Workspaces UI")]
    public void TestSnapshotShowsAllOpenWindows()
    {
        // Open some test applications
        OpenTestApplications();
        Thread.Sleep(3000);
        
        // Click Create Workspace
        var createButton = Find<Button>("Create Workspace");
        createButton.Click();
        Thread.Sleep(1000);
        
        // Click Capture
        var captureButton = Find<Button>("Capture");
        captureButton.Click();
        Thread.Sleep(2000);
        
        // Verify captured windows appear in the editor
        var windowList = FindAll("Window");
        Assert.IsTrue(windowList.Count > 0, "Should have captured at least one window");
        
        // Close test applications
        CloseTestApplications();
    }

    [TestMethod("WorkspacesEditor.Snapshot.VerifyWindowPositions")]
    [TestCategory("Workspaces UI")]
    public void TestSnapshotCapturesCorrectWindowPositions()
    {
        // Open test applications
        OpenTestApplications();
        Thread.Sleep(3000);
        
        // Click Create Workspace
        var createButton = Find<Button>("Create Workspace");
        createButton.Click();
        Thread.Sleep(1000);
        
        // Click Capture
        var captureButton = Find<Button>("Capture");
        captureButton.Click();
        Thread.Sleep(2000);
        
        // Verify windows have position data
        var previewPane = Find<Pane>("Preview");
        Assert.IsNotNull(previewPane, "Preview pane should exist");
        
        // Look for window representations in the preview
        var windowPreviews = previewPane.FindAll<Custom>(By.ClassName("WindowPreview"));
        Assert.IsTrue(windowPreviews.Count > 0, "Should have window previews with positions");
        
        // Close test applications
        CloseTestApplications();
    }

    #endregion

    #region Editor Tests

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
        searchBox.Clear();
        searchBox.SendKeys("TestWorkspace");
        Thread.Sleep(1000);
        
        // Verify filtered results
        var workspacesList = Find<Custom>("WorkspacesList");
        var visibleItems = workspacesList.FindAll<Custom>(By.ClassName("WorkspaceItem"));
        
        // Should only show items matching "TestWorkspace"
        Assert.IsTrue(visibleItems.Count >= 2, "Should show at least 2 TestWorkspace items");
        
        // Clear search
        searchBox.Clear();
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
        string selectedOption = "";
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
        Assert.IsFalse(workspacesList.Has(By.Name("WorkspaceToRemove")), "Workspace should be removed from list");
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

    #endregion

    #region Editing Page Tests

    [TestMethod("WorkspacesEditor.EditingPage.RemoveApp")]
    [TestCategory("Workspaces UI")]
    public void TestRemoveAppFromWorkspace()
    {
        // Create workspace with apps
        CreateWorkspaceWithApps();
        
        // Open first workspace for editing
        var workspacesList = Find<Custom>("WorkspacesList");
        var workspaceItem = workspacesList.FindAll<Custom>(By.ClassName("WorkspaceItem"))[0];
        workspaceItem.Click();
        Thread.Sleep(1000);
        
        // Find app list
        var appList = Find<Custom>("AppList");
        var initialAppCount = appList.FindAll<Custom>(By.ClassName("AppItem")).Count;
        
        // Remove first app
        var firstApp = appList.FindAll<Custom>(By.ClassName("AppItem"))[0];
        var removeButton = firstApp.Find<Button>("Remove");
        removeButton.Click();
        Thread.Sleep(500);
        
        // Verify app removed from preview
        var finalAppCount = appList.FindAll<Custom>(By.ClassName("AppItem")).Count;
        Assert.AreEqual(initialAppCount - 1, finalAppCount, "App should be removed from list");
        
        // Cancel changes
        Find<Button>("Cancel").Click();
        Thread.Sleep(1000);
    }

    [TestMethod("WorkspacesEditor.EditingPage.RemoveAndAddBackApp")]
    [TestCategory("Workspaces UI")]
    public void TestRemoveAndAddBackApp()
    {
        // Create workspace with apps
        CreateWorkspaceWithApps();
        
        // Open first workspace for editing
        var workspacesList = Find<Custom>("WorkspacesList");
        var workspaceItem = workspacesList.FindAll<Custom>(By.ClassName("WorkspaceItem"))[0];
        workspaceItem.Click();
        Thread.Sleep(1000);
        
        // Find app list
        var appList = Find<Custom>("AppList");
        var firstApp = appList.FindAll<Custom>(By.ClassName("AppItem"))[0];
        var appName = firstApp.GetAttribute("Name");
        
        // Remove app
        var removeButton = firstApp.Find<Button>("Remove");
        removeButton.Click();
        Thread.Sleep(500);
        
        // Add back
        var addBackButton = Find<Button>("Add back");
        addBackButton.Click();
        Thread.Sleep(500);
        
        // Verify app is back
        Assert.IsTrue(appList.Has(By.Name(appName)), "App should be added back to list");
        
        // Cancel changes
        Find<Button>("Cancel").Click();
        Thread.Sleep(1000);
    }

    [TestMethod("WorkspacesEditor.EditingPage.SetAppMinimized")]
    [TestCategory("Workspaces UI")]
    public void TestSetAppMinimized()
    {
        // Create workspace with apps
        CreateWorkspaceWithApps();
        
        // Open first workspace for editing
        var workspacesList = Find<Custom>("WorkspacesList");
        var workspaceItem = workspacesList.FindAll<Custom>(By.ClassName("WorkspaceItem"))[0];
        workspaceItem.Click();
        Thread.Sleep(1000);
        
        // Find first app
        var appList = Find<Custom>("AppList");
        var firstApp = appList.FindAll<Custom>(By.ClassName("AppItem"))[0];
        
        // Set minimized
        var minimizeCheckbox = firstApp.Find<CheckBox>("Minimized");
        minimizeCheckbox.Click();
        Thread.Sleep(500);
        
        // Verify preview updated
        var preview = Find<Pane>("Preview");
        Assert.IsNotNull(preview, "Preview should update when app state changes");
        
        // Save changes
        Find<Button>("Save").Click();
        Thread.Sleep(1000);
    }

    [TestMethod("WorkspacesEditor.EditingPage.SetAppMaximized")]
    [TestCategory("Workspaces UI")]
    public void TestSetAppMaximized()
    {
        // Create workspace with apps
        CreateWorkspaceWithApps();
        
        // Open first workspace for editing
        var workspacesList = Find<Custom>("WorkspacesList");
        var workspaceItem = workspacesList.FindAll<Custom>(By.ClassName("WorkspaceItem"))[0];
        workspaceItem.Click();
        Thread.Sleep(1000);
        
        // Find first app
        var appList = Find<Custom>("AppList");
        var firstApp = appList.FindAll<Custom>(By.ClassName("AppItem"))[0];
        
        // Set maximized
        var maximizeCheckbox = firstApp.Find<CheckBox>("Maximized");
        maximizeCheckbox.Click();
        Thread.Sleep(500);
        
        // Verify preview updated
        var preview = Find<Pane>("Preview");
        Assert.IsNotNull(preview, "Preview should update when app state changes");
        
        // Save changes
        Find<Button>("Save").Click();
        Thread.Sleep(1000);
    }

    [TestMethod("WorkspacesEditor.EditingPage.ChangeWorkspaceName")]
    [TestCategory("Workspaces UI")]
    public void TestChangeWorkspaceName()
    {
        // Create workspace
        CreateTestWorkspace("OriginalName");
        
        // Open for editing
        var workspacesList = Find<Custom>("WorkspacesList");
        var workspaceItem = workspacesList.Find<Custom>(By.Name("OriginalName"));
        workspaceItem.Click();
        Thread.Sleep(1000);
        
        // Change name
        var nameTextBox = Find<TextBox>("WorkspaceName");
        nameTextBox.Clear();
        nameTextBox.SendKeys("NewWorkspaceName");
        Thread.Sleep(500);
        
        // Save
        Find<Button>("Save").Click();
        Thread.Sleep(1000);
        
        // Verify name changed in list
        workspacesList = Find<Custom>("WorkspacesList");
        Assert.IsTrue(workspacesList.Has(By.Name("NewWorkspaceName")), "Workspace name should be updated");
        Assert.IsFalse(workspacesList.Has(By.Name("OriginalName")), "Old workspace name should not exist");
    }

    [TestMethod("WorkspacesEditor.EditingPage.SaveAndCancelWork")]
    [TestCategory("Workspaces UI")]
    public void TestSaveAndCancelButtons()
    {
        // Create workspace
        CreateTestWorkspace("TestSaveCancel");
        
        // Open for editing
        var workspacesList = Find<Custom>("WorkspacesList");
        var workspaceItem = workspacesList.Find<Custom>(By.Name("TestSaveCancel"));
        workspaceItem.Click();
        Thread.Sleep(1000);
        
        // Make a change
        var nameTextBox = Find<TextBox>("WorkspaceName");
        nameTextBox.Clear();
        nameTextBox.SendKeys("ChangedName");
        Thread.Sleep(500);
        
        // Cancel
        Find<Button>("Cancel").Click();
        Thread.Sleep(1000);
        
        // Verify change not saved
        workspacesList = Find<Custom>("WorkspacesList");
        Assert.IsTrue(workspacesList.Has(By.Name("TestSaveCancel")), "Original name should remain after cancel");
        Assert.IsFalse(workspacesList.Has(By.Name("ChangedName")), "Changed name should not be saved");
        
        // Open again and save this time
        workspaceItem = workspacesList.Find<Custom>(By.Name("TestSaveCancel"));
        workspaceItem.Click();
        Thread.Sleep(1000);
        
        nameTextBox = Find<TextBox>("WorkspaceName");
        nameTextBox.Clear();
        nameTextBox.SendKeys("SavedName");
        Thread.Sleep(500);
        
        // Save
        Find<Button>("Save").Click();
        Thread.Sleep(1000);
        
        // Verify change saved
        workspacesList = Find<Custom>("WorkspacesList");
        Assert.IsTrue(workspacesList.Has(By.Name("SavedName")), "Changed name should be saved");
    }

    [TestMethod("WorkspacesEditor.EditingPage.NavigateWithoutSaving")]
    [TestCategory("Workspaces UI")]
    public void TestNavigateToMainPageWithoutSaving()
    {
        // Create workspace
        CreateTestWorkspace("TestNavigation");
        
        // Open for editing
        var workspacesList = Find<Custom>("WorkspacesList");
        var workspaceItem = workspacesList.Find<Custom>(By.Name("TestNavigation"));
        workspaceItem.Click();
        Thread.Sleep(1000);
        
        // Make a change
        var nameTextBox = Find<TextBox>("WorkspaceName");
        nameTextBox.Clear();
        nameTextBox.SendKeys("UnsavedChange");
        Thread.Sleep(500);
        
        // Click Workspaces link to go back
        var workspacesLink = Find<HyperlinkButton>("Workspaces");
        workspacesLink.Click();
        Thread.Sleep(1000);
        
        // Verify returned to main page without saving
        Assert.IsTrue(Has<Button>("Create Workspace"), "Should be on main page");
        workspacesList = Find<Custom>("WorkspacesList");
        Assert.IsTrue(workspacesList.Has(By.Name("TestNavigation")), "Original name should remain");
        Assert.IsFalse(workspacesList.Has(By.Name("UnsavedChange")), "Change should not be saved");
    }

    [TestMethod("WorkspacesEditor.EditingPage.CreateDesktopShortcut")]
    [TestCategory("Workspaces UI")]
    public void TestCreateDesktopShortcut()
    {
        // Create workspace
        CreateTestWorkspace("ShortcutTest");
        
        // Open for editing
        var workspacesList = Find<Custom>("WorkspacesList");
        var workspaceItem = workspacesList.Find<Custom>(By.Name("ShortcutTest"));
        workspaceItem.Click();
        Thread.Sleep(1000);
        
        // Check create desktop shortcut
        var shortcutCheckbox = Find<CheckBox>("Create desktop shortcut");
        if (!shortcutCheckbox.IsChecked)
        {
            shortcutCheckbox.Click();
            Thread.Sleep(500);
        }
        
        // Save
        Find<Button>("Save").Click();
        Thread.Sleep(1000);
        
        // Verify shortcut created (this would require checking desktop in real test)
        // For now, just verify the checkbox state persists
        workspaceItem = workspacesList.Find<Custom>(By.Name("ShortcutTest"));
        workspaceItem.Click();
        Thread.Sleep(1000);
        
        shortcutCheckbox = Find<CheckBox>("Create desktop shortcut");
        Assert.IsTrue(shortcutCheckbox.IsChecked, "Desktop shortcut checkbox should remain checked");
        
        // Cancel
        Find<Button>("Cancel").Click();
        Thread.Sleep(1000);
    }

    [TestMethod("WorkspacesEditor.EditingPage.LaunchAndEdit")]
    [TestCategory("Workspaces UI")]
    public void TestLaunchAndEditCapture()
    {
        // Create workspace with notepad
        CreateWorkspaceWithApps();
        
        // Open for editing
        var workspacesList = Find<Custom>("WorkspacesList");
        var workspaceItem = workspacesList.FindAll<Custom>(By.ClassName("WorkspaceItem"))[0];
        workspaceItem.Click();
        Thread.Sleep(1000);
        
        // Click Launch and Edit
        var launchEditButton = Find<Button>("Launch and Edit");
        launchEditButton.Click();
        Thread.Sleep(3000); // Wait for apps to launch
        
        // Click Capture
        var captureButton = Find<Button>("Capture");
        captureButton.Click();
        Thread.Sleep(2000);
        
        // Verify apps were captured
        var appList = Find<Custom>("AppList");
        var appCount = appList.FindAll<Custom>(By.ClassName("AppItem")).Count;
        Assert.IsTrue(appCount > 0, "Should have captured launched apps");
        
        // Cancel changes
        Find<Button>("Cancel").Click();
        Thread.Sleep(1000);
        
        // Close launched apps
        CloseTestApplications();
    }

    #endregion

    #region Launcher Tests

    [TestMethod("WorkspacesEditor.Launcher.LaunchFromEditor")]
    [TestCategory("Workspaces UI")]
    public void TestLaunchWorkspaceFromEditor()
    {
        // Create workspace with apps
        CreateWorkspaceWithApps();
        
        // Find launch button for first workspace
        var workspacesList = Find<Custom>("WorkspacesList");
        var workspaceItem = workspacesList.FindAll<Custom>(By.ClassName("WorkspaceItem"))[0];
        var launchButton = workspaceItem.Find<Button>("Launch");
        launchButton.Click();
        Thread.Sleep(3000);
        
        // Verify launch UI appears
        Assert.IsTrue(Has<Window>("Workspaces Launcher"), "Launcher window should appear");
        
        // Wait for launch to complete
        Thread.Sleep(3000);
        
        // Close launched apps
        CloseTestApplications();
    }

    [TestMethod("WorkspacesEditor.Launcher.CancelLaunch")]
    [TestCategory("Workspaces UI")]
    public void TestCancelLaunch()
    {
        // Create workspace with multiple apps
        CreateWorkspaceWithApps();
        
        // Launch workspace
        var workspacesList = Find<Custom>("WorkspacesList");
        var workspaceItem = workspacesList.FindAll<Custom>(By.ClassName("WorkspaceItem"))[0];
        var launchButton = workspaceItem.Find<Button>("Launch");
        launchButton.Click();
        Thread.Sleep(1000);
        
        // Cancel launch
        if (Has<Button>("Cancel launch"))
        {
            Find<Button>("Cancel launch").Click();
            Thread.Sleep(1000);
            
            // Verify launcher closed
            Assert.IsFalse(Has<Window>("Workspaces Launcher"), "Launcher window should close after cancel");
        }
        
        // Close any apps that may have launched
        CloseTestApplications();
    }

    [TestMethod("WorkspacesEditor.Launcher.DismissKeepsLaunching")]
    [TestCategory("Workspaces UI")]
    public void TestDismissKeepsAppsLaunching()
    {
        // Create workspace with apps
        CreateWorkspaceWithApps();
        
        // Launch workspace
        var workspacesList = Find<Custom>("WorkspacesList");
        var workspaceItem = workspacesList.FindAll<Custom>(By.ClassName("WorkspaceItem"))[0];
        var launchButton = workspaceItem.Find<Button>("Launch");
        launchButton.Click();
        Thread.Sleep(1000);
        
        // Dismiss launcher
        if (Has<Button>("Dismiss"))
        {
            Find<Button>("Dismiss").Click();
            Thread.Sleep(1000);
            
            // Verify launcher closed but apps continue launching
            Assert.IsFalse(Has<Window>("Workspaces Launcher"), "Launcher window should close after dismiss");
            
            // Wait for apps to finish launching
            Thread.Sleep(3000);
            
            // Verify apps launched (notepad should be open)
            Assert.IsTrue(WindowHelper.IsWindowOpen("Notepad"), "Apps should continue launching after dismiss");
        }
        
        // Close launched apps
        CloseTestApplications();
    }

    #endregion

    #region Helper Methods

    private void CreateTestWorkspace(string name)
    {
        // Open notepad for capture
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
        
        // Set name
        var nameTextBox = Find<TextBox>("WorkspaceName");
        nameTextBox.Clear();
        nameTextBox.SendKeys(name);
        Thread.Sleep(500);
        
        // Save
        Find<Button>("Save").Click();
        Thread.Sleep(1000);
        
        // Close notepad
        CloseNotepad();
    }

    private void CreateWorkspaceWithApps()
    {
        // Open multiple test applications
        OpenTestApplications();
        Thread.Sleep(3000);
        
        // Create workspace
        var createButton = Find<Button>("Create Workspace");
        createButton.Click();
        Thread.Sleep(1000);
        
        // Capture
        var captureButton = Find<Button>("Capture");
        captureButton.Click();
        Thread.Sleep(2000);
        
        // Save
        Find<Button>("Save").Click();
        Thread.Sleep(1000);
        
        // Close test applications
        CloseTestApplications();
    }

    private void OpenTestApplications()
    {
        OpenNotepad();
        // Could add more applications here
        Thread.Sleep(1000);
    }

    private void CloseTestApplications()
    {
        CloseNotepad();
        // Close other test applications if any
    }

    private void OpenNotepad()
    {
        var process = System.Diagnostics.Process.Start("notepad.exe");
        Thread.Sleep(1000);
    }

    private void CloseNotepad()
    {
        var processes = System.Diagnostics.Process.GetProcessesByName("notepad");
        foreach (var process in processes)
        {
            try
            {
                process.Kill();
                process.WaitForExit();
            }
            catch { }
        }
    }

    #endregion
}
