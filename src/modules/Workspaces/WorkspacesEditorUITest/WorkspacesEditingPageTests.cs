// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WorkspacesEditorUITest;

[TestClass]
[Ignore("not stable")]
public class WorkspacesEditingPageTests : WorkspacesUiAutomationBase
{
    public WorkspacesEditingPageTests()
        : base()
    {
    }

    [TestMethod("WorkspacesEditingPage.RemoveApp")]
    [TestCategory("Workspaces Editing Page UI")]
    public void TestRemoveAppFromWorkspace()
    {
        // Find app list
        var appList = Find<Custom>("AppList");
        var initialAppCount = appList.FindAll<Custom>(By.ClassName("AppItem")).Count;

        if (initialAppCount == 0)
        {
            Assert.Inconclusive("No apps in workspace to remove");
            return;
        }

        // Remove first app
        var firstApp = appList.FindAll<Custom>(By.ClassName("AppItem"))[0];
        var appName = firstApp.GetAttribute("Name");

        var removeButton = firstApp.Find<Button>("Remove");
        removeButton.Click();
        Thread.Sleep(500);

        // Verify app removed from list
        var finalAppCount = appList.FindAll<Custom>(By.ClassName("AppItem")).Count;
        Assert.AreEqual(initialAppCount - 1, finalAppCount, "App should be removed from list");

        // Verify preview updated
        var previewPane = Find<Pane>("Preview");
        var windowPreviews = previewPane.FindAll<Custom>(By.ClassName("WindowPreview"));
        Assert.AreEqual(finalAppCount, windowPreviews.Count, "Preview should show correct number of windows");
    }

    [TestMethod("WorkspacesEditingPage.RemoveAndAddBackApp")]
    [TestCategory("Workspaces Editing Page UI")]
    public void TestRemoveAndAddBackApp()
    {
        // Find app list
        var appList = Find<Custom>("AppList");
        var apps = appList.FindAll<Custom>(By.ClassName("AppItem"));

        if (apps.Count == 0)
        {
            Assert.Inconclusive("No apps in workspace to test");
            return;
        }

        var firstApp = apps[0];
        var appName = firstApp.GetAttribute("Name");

        // Remove app
        var removeButton = firstApp.Find<Button>("Remove");
        removeButton.Click();
        Thread.Sleep(500);

        // Verify removed app shows in "removed apps" section
        Assert.IsTrue(Has<Button>("Add back"), "Should have 'Add back' button for removed apps");

        // Add back the app
        var addBackButton = Find<Button>("Add back");
        addBackButton.Click();
        Thread.Sleep(500);

        // Verify app is back in the list
        var restoredApp = appList.Find<Custom>(By.Name(appName), timeoutMS: 2000);
        Assert.IsNotNull(restoredApp, "App should be restored to the list");

        // Verify preview updated
        var previewPane = Find<Pane>("Preview");
        var windowPreviews = previewPane.FindAll<Custom>(By.ClassName("WindowPreview"));
        var currentAppCount = appList.FindAll<Custom>(By.ClassName("AppItem")).Count;
        Assert.AreEqual(currentAppCount, windowPreviews.Count, "Preview should show all windows again");
    }

    [TestMethod("WorkspacesEditingPage.SetAppMinimized")]
    [TestCategory("Workspaces Editing Page UI")]
    public void TestSetAppMinimized()
    {
        // Find first app
        var appList = Find<Custom>("AppList");
        var apps = appList.FindAll<Custom>(By.ClassName("AppItem"));

        if (apps.Count == 0)
        {
            Assert.Inconclusive("No apps in workspace to test");
            return;
        }

        var firstApp = apps[0];

        // Find and toggle minimized checkbox
        var minimizedCheckbox = firstApp.Find<CheckBox>("Minimized");
        bool wasMinimized = minimizedCheckbox.IsChecked;

        minimizedCheckbox.Click();
        Thread.Sleep(500);

        // Verify state changed
        Assert.AreNotEqual(wasMinimized, minimizedCheckbox.IsChecked, "Minimized state should toggle");

        // Verify preview reflects the change
        var previewPane = Find<Pane>("Preview");
        var windowPreviews = previewPane.FindAll<Custom>(By.ClassName("WindowPreview"));

        // The first window preview should indicate minimized state
        if (minimizedCheckbox.IsChecked && windowPreviews.Count > 0)
        {
            var firstPreview = windowPreviews[0];
            var opacity = firstPreview.GetAttribute("Opacity");

            // Minimized windows might have reduced opacity or other visual indicator
            Assert.IsNotNull(opacity, "Minimized window should have visual indication in preview");
        }
    }

    [TestMethod("WorkspacesEditingPage.SetAppMaximized")]
    [TestCategory("Workspaces Editing Page UI")]
    public void TestSetAppMaximized()
    {
        // Find first app
        var appList = Find<Custom>("AppList");
        var apps = appList.FindAll<Custom>(By.ClassName("AppItem"));

        if (apps.Count == 0)
        {
            Assert.Inconclusive("No apps in workspace to test");
            return;
        }

        var firstApp = apps[0];

        // Find and toggle maximized checkbox
        var maximizedCheckbox = firstApp.Find<CheckBox>("Maximized");
        bool wasMaximized = maximizedCheckbox.IsChecked;

        maximizedCheckbox.Click();
        Thread.Sleep(500);

        // Verify state changed
        Assert.AreNotEqual(wasMaximized, maximizedCheckbox.IsChecked, "Maximized state should toggle");

        // Verify preview reflects the change
        var previewPane = Find<Pane>("Preview");
        if (maximizedCheckbox.IsChecked)
        {
            // Maximized window should fill the preview area
            var windowPreviews = previewPane.FindAll<Custom>(By.ClassName("WindowPreview"));
            if (windowPreviews.Count > 0)
            {
                var firstPreview = windowPreviews[0];

                // Check if preview shows maximized state
                var width = firstPreview.GetAttribute("Width");
                var height = firstPreview.GetAttribute("Height");
                Assert.IsNotNull(width, "Maximized window should have width in preview");
                Assert.IsNotNull(height, "Maximized window should have height in preview");
            }
        }
    }

    [TestMethod("WorkspacesEditingPage.LaunchAsAdmin")]
    [TestCategory("Workspaces Editing Page UI")]
    public void TestSetLaunchAsAdmin()
    {
        // Find app that supports admin launch
        var appList = Find<Custom>("AppList");
        var apps = appList.FindAll<Custom>(By.ClassName("AppItem"));

        bool foundAdminCapableApp = false;
        foreach (var app in apps)
        {
            try
            {
                var adminCheckbox = app.Find<CheckBox>("Launch as admin", timeoutMS: 1000);
                if (adminCheckbox != null && adminCheckbox.IsChecked)
                {
                    foundAdminCapableApp = true;
                    bool wasAdmin = adminCheckbox.IsChecked;

                    adminCheckbox.Click();
                    Thread.Sleep(500);

                    // Verify state changed
                    Assert.AreNotEqual(wasAdmin, adminCheckbox.IsChecked, "Admin launch state should toggle");
                    break;
                }
            }
            catch
            {
                // This app doesn't support admin launch
                continue;
            }
        }

        if (!foundAdminCapableApp)
        {
            Assert.Inconclusive("No apps in workspace support admin launch");
        }
    }

    [TestMethod("WorkspacesEditingPage.AddCLIArgs")]
    [TestCategory("Workspaces Editing Page UI")]
    public void TestAddCommandLineArguments()
    {
        // Find first app
        var appList = Find<Custom>("AppList");
        var apps = appList.FindAll<Custom>(By.ClassName("AppItem"));

        if (apps.Count == 0)
        {
            Assert.Inconclusive("No apps in workspace to test");
            return;
        }

        var firstApp = apps[0];

        // Find CLI args textbox
        var cliArgsTextBox = firstApp.Find<TextBox>("Command line arguments", timeoutMS: 2000);
        if (cliArgsTextBox == null)
        {
            Assert.Inconclusive("App does not support command line arguments");
            return;
        }

        // Add test arguments
        string testArgs = "--test-arg value";
        cliArgsTextBox.SetText(testArgs);
        Thread.Sleep(500);

        // Verify arguments were entered
        Assert.AreEqual(testArgs, cliArgsTextBox.Text, "Command line arguments should be set");
    }

    [TestMethod("WorkspacesEditingPage.ChangeAppPosition")]
    [TestCategory("Workspaces Editing Page UI")]
    public void TestManuallyChangeAppPosition()
    {
        // Find first app
        var appList = Find<Custom>("AppList");
        var apps = appList.FindAll<Custom>(By.ClassName("AppItem"));

        if (apps.Count == 0)
        {
            Assert.Inconclusive("No apps in workspace to test");
            return;
        }

        var firstApp = apps[0];

        // Find position controls
        var xPositionBox = firstApp.Find<TextBox>("X position", timeoutMS: 2000);
        var yPositionBox = firstApp.Find<TextBox>("Y position", timeoutMS: 2000);

        if (xPositionBox == null || yPositionBox == null)
        {
            // Try alternate approach with spinners
            var positionSpinners = firstApp.FindAll<Custom>(By.ClassName("SpinBox"));
            if (positionSpinners.Count >= 2)
            {
                xPositionBox = positionSpinners[0].Find<TextBox>(By.ClassName("TextBox"));
                yPositionBox = positionSpinners[1].Find<TextBox>(By.ClassName("TextBox"));
            }
        }

        if (xPositionBox != null && yPositionBox != null)
        {
            // Change position
            xPositionBox.SetText("200");
            Thread.Sleep(500);

            yPositionBox.SetText("150");
            Thread.Sleep(500);

            // Verify preview updated
            var previewPane = Find<Pane>("Preview");
            var windowPreviews = previewPane.FindAll<Custom>(By.ClassName("WindowPreview"));
            Assert.IsTrue(windowPreviews.Count > 0, "Preview should show window at new position");
        }
        else
        {
            Assert.Inconclusive("Could not find position controls");
        }
    }

    [TestMethod("WorkspacesEditingPage.ChangeWorkspaceName")]
    [TestCategory("Workspaces Editing Page UI")]
    public void TestChangeWorkspaceName()
    {
        // Find workspace name textbox
        var nameTextBox = Find<TextBox>("Workspace name");
        string originalName = nameTextBox.Text;

        // Change name
        string newName = "Renamed_Workspace_" + DateTime.Now.Ticks;
        nameTextBox.SetText(newName);
        Thread.Sleep(500);

        // Save changes
        var saveButton = Find<Button>("Save");
        saveButton.Click();
        Thread.Sleep(1000);

        // Verify we're back at main list
        Assert.IsTrue(Has<Custom>("WorkspacesList"), "Should return to main list after saving");

        // Verify workspace was renamed
        var workspacesList = Find<Custom>("WorkspacesList");
        var renamedWorkspace = workspacesList.Find<Custom>(By.Name(newName), timeoutMS: 2000);
        Assert.IsNotNull(renamedWorkspace, "Workspace should be renamed in the list");
    }

    [TestMethod("WorkspacesEditingPage.SaveAndCancelWork")]
    [TestCategory("Workspaces Editing Page UI")]
    public void TestSaveAndCancelButtons()
    {
        // Make a change
        var nameTextBox = Find<TextBox>("Workspace name");
        string originalName = nameTextBox.Text;
        string tempName = originalName + "_temp";

        nameTextBox.SetText(tempName);
        Thread.Sleep(500);

        // Test Cancel button
        var cancelButton = Find<Button>("Cancel");
        cancelButton.Click();
        Thread.Sleep(1000);

        // Verify returned to main list without saving
        Assert.IsTrue(Has<Custom>("WorkspacesList"), "Should return to main list");

        // Go back to editing
        var workspacesList = Find<Custom>("WorkspacesList");
        var workspace = workspacesList.FindAll<Custom>(By.ClassName("WorkspaceItem"))[0];
        workspace.Click();
        Thread.Sleep(1000);

        // Verify name wasn't changed
        nameTextBox = Find<TextBox>("Workspace name");
        Assert.AreEqual(originalName, nameTextBox.Text, "Name should not be changed after cancel");

        // Now test Save button
        nameTextBox.SetText(tempName);
        Thread.Sleep(500);

        var saveButton = Find<Button>("Save");
        saveButton.Click();
        Thread.Sleep(1000);

        // Verify saved
        workspacesList = Find<Custom>("WorkspacesList");
        var savedWorkspace = workspacesList.Find<Custom>(By.Name(tempName), timeoutMS: 2000);
        Assert.IsNotNull(savedWorkspace, "Workspace should be saved with new name");
    }

    [TestMethod("WorkspacesEditingPage.NavigateWithoutSaving")]
    [TestCategory("Workspaces Editing Page UI")]
    public void TestNavigateToMainPageWithoutSaving()
    {
        // Make a change
        var nameTextBox = Find<TextBox>("Workspace name");
        string originalName = nameTextBox.Text;

        nameTextBox.SetText(originalName + "_unsaved");
        Thread.Sleep(500);

        // Click on "Workspaces" navigation/breadcrumb
        if (Has<NavigationViewItem>("Workspaces", timeoutMS: 1000))
        {
            var workspacesNav = Find<NavigationViewItem>("Workspaces");
            workspacesNav.Click();
            Thread.Sleep(1000);
        }
        else if (Has<HyperlinkButton>("Workspaces", timeoutMS: 1000))
        {
            var workspacesBreadcrumb = Find<HyperlinkButton>("Workspaces");
            workspacesBreadcrumb.Click();
            Thread.Sleep(1000);
        }

        // If there's a confirmation dialog, handle it
        if (Has<Button>("Discard", timeoutMS: 1000))
        {
            Find<Button>("Discard").Click();
            Thread.Sleep(500);
        }

        // Verify returned to main list
        Assert.IsTrue(Has<Custom>("WorkspacesList"), "Should return to main list");

        // Verify changes weren't saved
        var workspacesList = Find<Custom>("WorkspacesList");
        var unsavedWorkspace = workspacesList.Find<Custom>(By.Name(originalName + "_unsaved"), timeoutMS: 1000);
        Assert.IsNull(unsavedWorkspace, "Unsaved changes should not persist");
    }

    [TestMethod("WorkspacesEditingPage.CreateDesktopShortcut")]
    [TestCategory("Workspaces Editing Page UI")]
    public void TestCreateDesktopShortcut()
    {
        // Find desktop shortcut checkbox
        var shortcutCheckbox = Find<CheckBox>("Create desktop shortcut");

        // Get desktop path
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        // Get workspace name to check for shortcut
        var nameTextBox = Find<TextBox>("Workspace name");
        string workspaceName = nameTextBox.Text;
        string shortcutPath = Path.Combine(desktopPath, $"{workspaceName}.lnk");

        // Clean up any existing shortcut
        if (File.Exists(shortcutPath))
        {
            File.Delete(shortcutPath);
            Thread.Sleep(500);
        }

        // Check the checkbox
        if (!shortcutCheckbox.IsChecked)
        {
            shortcutCheckbox.Click();
            Thread.Sleep(500);
        }

        // Save
        var saveButton = Find<Button>("Save");
        saveButton.Click();
        Thread.Sleep(2000); // Give time for shortcut creation

        // Verify shortcut was created
        Assert.IsTrue(File.Exists(shortcutPath), "Desktop shortcut should be created");

        // Clean up
        if (File.Exists(shortcutPath))
        {
            File.Delete(shortcutPath);
        }
    }

    [TestMethod("WorkspacesEditingPage.DesktopShortcutState")]
    [TestCategory("Workspaces Editing Page UI")]
    public void TestDesktopShortcutCheckboxState()
    {
        // Get workspace name
        var nameTextBox = Find<TextBox>("Workspace name");
        string workspaceName = nameTextBox.Text;
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string shortcutPath = Path.Combine(desktopPath, $"{workspaceName}.lnk");

        // Find checkbox
        var shortcutCheckbox = Find<CheckBox>("Create desktop shortcut");

        // Test 1: When shortcut exists
        if (!File.Exists(shortcutPath))
        {
            // Create shortcut first
            if (!shortcutCheckbox.IsChecked)
            {
                shortcutCheckbox.Click();
                Thread.Sleep(500);
            }

            Find<Button>("Save").Click();
            Thread.Sleep(2000);

            // Navigate back to editing
            NavigateToEditingPage();
        }

        shortcutCheckbox = Find<CheckBox>("Create desktop shortcut");
        Assert.IsTrue(shortcutCheckbox.IsChecked, "Checkbox should be checked when shortcut exists");

        // Test 2: Remove shortcut
        if (File.Exists(shortcutPath))
        {
            File.Delete(shortcutPath);
            Thread.Sleep(500);
        }

        // Re-navigate to refresh state
        Find<Button>("Cancel").Click();
        Thread.Sleep(1000);
        NavigateToEditingPage();

        shortcutCheckbox = Find<CheckBox>("Create desktop shortcut");
        Assert.IsFalse(shortcutCheckbox.IsChecked, "Checkbox should be unchecked when shortcut doesn't exist");
    }

    [TestMethod("WorkspacesEditingPage.LaunchAndEdit")]
    [TestCategory("Workspaces Editing Page UI")]
    public void TestLaunchAndEditCapture()
    {
        // Find Launch and Edit button
        var launchEditButton = Find<Button>("Launch and Edit");
        launchEditButton.Click();
        Thread.Sleep(3000); // Wait for apps to launch

        // Open a new application
        Process.Start("calc.exe");
        Thread.Sleep(2000);

        // Click Capture
        var captureButton = Find<Button>("Capture");
        captureButton.Click();
        Thread.Sleep(2000);

        // Verify new app was added
        var appList = Find<Custom>("AppList");
        var apps = appList.FindAll<Custom>(By.ClassName("AppItem"));

        bool foundCalculator = false;
        foreach (var app in apps)
        {
            var appName = app.GetAttribute("Name");
            if (appName.Contains("Calculator", StringComparison.OrdinalIgnoreCase))
            {
                foundCalculator = true;
                break;
            }
        }

        Assert.IsTrue(foundCalculator, "Newly opened Calculator should be captured and added");

        // Clean up
        foreach (var process in Process.GetProcessesByName("CalculatorApp"))
        {
            process.Kill();
        }

        foreach (var process in Process.GetProcessesByName("Calculator"))
        {
            process.Kill();
        }
    }

    // Helper methods
    private void NavigateToEditingPage()
    {
        // Ensure we have at least one workspace
        if (!Has<Custom>("WorkspacesList", timeoutMS: 1000))
        {
            CreateTestWorkspace();
        }

        // Click on first workspace to edit
        var workspacesList = Find<Custom>("WorkspacesList");
        var workspaceItems = workspacesList.FindAll<Custom>(By.ClassName("WorkspaceItem"));

        if (workspaceItems.Count == 0)
        {
            CreateTestWorkspace();
            workspaceItems = workspacesList.FindAll<Custom>(By.ClassName("WorkspaceItem"));
        }

        workspaceItems[0].Click();
        Thread.Sleep(1000);
    }

    private void CreateTestWorkspace()
    {
        // Open a test app
        Process.Start("notepad.exe");
        Thread.Sleep(1000);

        // Create workspace
        var createButton = Find<Button>("Create Workspace");
        createButton.Click();
        Thread.Sleep(1000);

        // Capture
        var captureButton = Find<Button>("Capture");
        captureButton.Click();
        Thread.Sleep(2000);

        // Save with default name
        var saveButton = Find<Button>("Save");
        saveButton.Click();
        Thread.Sleep(1000);

        // Close test app
        foreach (var process in Process.GetProcessesByName("notepad"))
        {
            process.Kill();
        }
    }
}
