// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesCsharpLibrary.Data;

namespace WorkspacesEditorUITest;

[TestClass]
[Ignore("not stable")]
public class WorkspacesEditingPageTests : WorkspacesUiAutomationBase
{
    private string workspaceName = string.Empty;

    public WorkspacesEditingPageTests()
        : base()
    {
    }

    [TestInitialize]
    public void SetupWorkspace()
    {
        workspaceName = "LegacyEditing_" + Guid.NewGuid().ToString("N");
        CreateTestWorkspace(workspaceName);
        OpenWorkspace(workspaceName);
    }

    [TestCleanup]
    public void CleanupWorkspace()
    {
        CloseCalculator();
        AttachWorkspacesEditor();
        if (Has(By.AccessibilityId("EditNameTextBox"), timeoutMS: 500))
        {
            ReturnToMainPage();
        }

        if (GetWorkspaceCards().Any(card => card.Name == workspaceName))
        {
            DeleteWorkspace(workspaceName);
        }

        File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"{workspaceName}.lnk"));
    }

    [TestMethod("WorkspacesEditingPage.RemoveApp")]
    [TestCategory("Workspaces Editing Page UI")]
    public void TestRemoveAppFromWorkspace()
    {
        var app = FindCapturedNotepad();
        int initialCount = IncludedApplicationCount();
        using var before = CapturePreview();
        InclusionButton(app, "Remove").Click();

        Assert.IsFalse(app.Enabled, "The removed application should stay in the list as a disabled expander.");
        Assert.IsTrue(InclusionButton(app, "Add back").Enabled, "The removed application should have an enabled Add back button.");
        Assert.AreEqual(initialCount - 1, IncludedApplicationCount(), "One application should be excluded.");
        AssertPreviewPixels(before, changed: true);

        string appId = CapturedNotepad().Id;
        if (IncludedApplicationCount() == 0)
        {
            Assert.IsFalse(Find<Button>(By.AccessibilityId("SaveButton")).Enabled, "A workspace with no included applications should not be saveable.");
        }
        else
        {
            SaveWorkspace();
            Assert.IsFalse(StoredWorkspace().Applications.Any(application => application.Id == appId), "The removed application should be excluded from the saved workspace.");
        }
    }

    [TestMethod("WorkspacesEditingPage.RemoveAndAddBackApp")]
    [TestCategory("Workspaces Editing Page UI")]
    public void TestRemoveAndAddBackApp()
    {
        var app = FindCapturedNotepad();
        int initialCount = IncludedApplicationCount();
        using var before = CapturePreview();
        InclusionButton(app, "Remove").Click();
        Assert.IsFalse(app.Enabled, "The removed application should be disabled.");
        AssertPreviewPixels(before, changed: true);

        InclusionButton(app, "Add back").Click();
        Assert.IsTrue(app.Enabled, "Adding the application back should re-enable its expander.");
        Assert.AreEqual(initialCount, IncludedApplicationCount(), "Adding the application back should restore the included application count.");
        AssertPreviewPixels(before, changed: false);
    }

    [TestMethod("WorkspacesEditingPage.SetAppMinimized")]
    [TestCategory("Workspaces Editing Page UI")]
    public void TestSetAppMinimized()
    {
        var app = ExpandApplication(CapturedNotepad().Application);
        SelectPosition(app, "Custom");
        using var before = CapturePreview();
        SelectPosition(app, "Minimized");
        Assert.IsFalse(app.FindAll<TextBox>(By.AccessibilityId("LeftTextBox"), timeoutMS: 500).Any(input => input.Displayed), "Minimized windows should hide custom position inputs.");
        AssertPreviewPixels(before, changed: true);
        SaveWorkspace();

        var saved = CapturedNotepad();
        Assert.IsTrue(saved.Minimized, "The saved application should be minimized.");
        Assert.IsFalse(saved.Maximized, "The saved application should not also be maximized.");
    }

    [TestMethod("WorkspacesEditingPage.SetAppMaximized")]
    [TestCategory("Workspaces Editing Page UI")]
    public void TestSetAppMaximized()
    {
        var app = ExpandApplication(CapturedNotepad().Application);
        SelectPosition(app, "Custom");
        using var before = CapturePreview();
        SelectPosition(app, "Maximized");
        Assert.IsFalse(app.FindAll<TextBox>(By.AccessibilityId("LeftTextBox"), timeoutMS: 500).Any(input => input.Displayed), "Maximized windows should hide custom position inputs.");
        AssertPreviewPixels(before, changed: true);
        SaveWorkspace();

        var saved = CapturedNotepad();
        Assert.IsTrue(saved.Maximized, "The saved application should be maximized.");
        Assert.IsFalse(saved.Minimized, "The saved application should not also be minimized.");
    }

    [TestMethod("WorkspacesEditingPage.LaunchAsAdmin")]
    [TestCategory("Workspaces Editing Page UI")]
    public void TestSetLaunchAsAdmin()
    {
        var candidates = StoredWorkspace().Applications.Where(application => application.CanLaunchElevated).ToList();
        if (candidates.Count == 0)
        {
            Assert.Inconclusive("No apps in workspace support admin launch");
            return;
        }

        var application = candidates[0];
        var app = ExpandApplication(application.Application);
        var admin = app.Find<CheckBox>(By.AccessibilityId("LaunchAsAdminCheckBox"));
        Assert.IsTrue(admin.Enabled, "An admin-capable application should have an enabled checkbox.");
        bool original = admin.IsChecked;
        admin.Click();
        Assert.AreNotEqual(original, admin.IsChecked, "The admin launch state should toggle.");
        SaveWorkspace();
        Assert.AreEqual(!original, StoredWorkspace().Applications.Single(saved => saved.Id == application.Id).IsElevated, "The admin launch choice should be saved.");
    }

    [TestMethod("WorkspacesEditingPage.AddCLIArgs")]
    [TestCategory("Workspaces Editing Page UI")]
    public void TestAddCommandLineArguments()
    {
        var app = ExpandApplication(CapturedNotepad().Application);
        var arguments = app.Find<TextBox>(By.AccessibilityId("CommandLineTextBox"));
        const string expected = "--test-arg value";
        arguments.SetText(expected);
        Assert.AreEqual(expected, arguments.Text, "Command line arguments should be entered.");
        SaveWorkspace();
        Assert.AreEqual(expected, CapturedNotepad().CommandLineArguments, "Command line arguments should be saved.");
    }

    [TestMethod("WorkspacesEditingPage.ChangeAppPosition")]
    [TestCategory("Workspaces Editing Page UI")]
    public void TestManuallyChangeAppPosition()
    {
        var app = ExpandApplication(CapturedNotepad().Application);
        SelectPosition(app, "Custom");
        using var before = CapturePreview();
        app.Find<TextBox>(By.AccessibilityId("LeftTextBox")).SetText("200");
        app.Find<TextBox>(By.AccessibilityId("TopTextBox")).SetText("150");
        app.Find<TextBox>(By.AccessibilityId("WidthTextBox")).SetText("640");
        app.Find<TextBox>(By.AccessibilityId("HeightTextBox")).SetText("480");
        AssertPreviewPixels(before, changed: true);
        SaveWorkspace();

        var position = CapturedNotepad().Position;
        Assert.AreEqual(200, position.X, "The horizontal position should be saved.");
        Assert.AreEqual(150, position.Y, "The vertical position should be saved.");
        Assert.AreEqual(640, position.Width, "The width should be saved.");
        Assert.AreEqual(480, position.Height, "The height should be saved.");
    }

    [TestMethod("WorkspacesEditingPage.ChangeWorkspaceName")]
    [TestCategory("Workspaces Editing Page UI")]
    public void TestChangeWorkspaceName()
    {
        string original = workspaceName;
        workspaceName += "_renamed";
        Find<TextBox>(By.AccessibilityId("EditNameTextBox")).SetText(workspaceName);
        SaveWorkspace();
        Assert.AreEqual(workspaceName, GetWorkspaceCard(workspaceName).Name, "The workspace should be renamed in the list.");
        Assert.IsFalse(GetWorkspaceCards().Any(card => card.Name == original), "The old workspace name should no longer be shown.");
    }

    [TestMethod("WorkspacesEditingPage.SaveAndCancelWork")]
    [TestCategory("Workspaces Editing Page UI")]
    public void TestSaveAndCancelButtons()
    {
        string temporary = workspaceName + "_temp";
        Find<TextBox>(By.AccessibilityId("EditNameTextBox")).SetText(temporary);
        ReturnToMainPage();
        Assert.IsFalse(GetWorkspaceCards().Any(card => card.Name == temporary), "Back navigation should discard the unsaved name.");

        OpenWorkspace(workspaceName);
        Assert.AreEqual(workspaceName, Find<TextBox>(By.AccessibilityId("EditNameTextBox")).Text, "The saved name should be restored after canceling.");
        Find<TextBox>(By.AccessibilityId("EditNameTextBox")).SetText(temporary);
        workspaceName = temporary;
        SaveWorkspace();
        Assert.AreEqual(temporary, GetWorkspaceCard(temporary).Name, "Save should persist the new workspace name.");
    }

    [TestMethod("WorkspacesEditingPage.NavigateWithoutSaving")]
    [TestCategory("Workspaces Editing Page UI")]
    public void TestNavigateToMainPageWithoutSaving()
    {
        string unsaved = workspaceName + "_unsaved";
        Find<TextBox>(By.AccessibilityId("EditNameTextBox")).SetText(unsaved);
        SendKeys(Key.Tab);
        SendKeys(Key.Esc);
        AssertMainPage();
        Assert.IsFalse(GetWorkspaceCards().Any(card => card.Name == unsaved), "Escape navigation should discard unsaved changes.");
        Assert.AreEqual(workspaceName, GetWorkspaceCard(workspaceName).Name, "Escape navigation should preserve the saved workspace.");
    }

    [TestMethod("WorkspacesEditingPage.CreateDesktopShortcut")]
    [TestCategory("Workspaces Editing Page UI")]
    public void TestCreateDesktopShortcut()
    {
        string shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"{workspaceName}.lnk");
        Find<CheckBox>(By.AccessibilityId("CreateShortcutCheckBox")).SetCheck(true);
        SaveWorkspace();
        Assert.IsTrue(File.Exists(shortcutPath), "Saving with Create desktop shortcut checked should create the shortcut.");
        Assert.IsTrue(StoredWorkspace().IsShortcutNeeded, "The shortcut choice should be saved.");
    }

    [TestMethod("WorkspacesEditingPage.DesktopShortcutState")]
    [TestCategory("Workspaces Editing Page UI")]
    public void TestDesktopShortcutCheckboxState()
    {
        string shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"{workspaceName}.lnk");
        Find<CheckBox>(By.AccessibilityId("CreateShortcutCheckBox")).SetCheck(true);
        SaveWorkspace();
        Assert.IsTrue(File.Exists(shortcutPath), "The shortcut fixture should exist.");

        OpenWorkspace(workspaceName);
        Assert.IsTrue(Find<CheckBox>(By.AccessibilityId("CreateShortcutCheckBox")).IsChecked, "The shortcut checkbox should reflect the existing shortcut.");
        File.Delete(shortcutPath);
        ReturnToMainPage();
        OpenWorkspace(workspaceName);
        Assert.IsFalse(Find<CheckBox>(By.AccessibilityId("CreateShortcutCheckBox")).IsChecked, "The shortcut checkbox should refresh after its shortcut is removed.");
    }

    [TestMethod("WorkspacesEditingPage.LaunchAndEdit")]
    [TestCategory("Workspaces Editing Page UI")]
    public void TestLaunchAndEditCapture()
    {
        Find<Button>(By.AccessibilityId("LaunchEditButton")).Click();
        AttachSnapshotWindow();
        OpenCalculator();
        CaptureWorkspace();
        Assert.AreEqual(workspaceName, Find<TextBox>(By.AccessibilityId("EditNameTextBox")).Text, "Recapture should keep editing the selected workspace.");
        var captured = new ProjectData().Read(TempProjectData.File);
        Assert.IsTrue(captured.Applications.Any(application => application.ApplicationPath.Contains("Calculator", StringComparison.OrdinalIgnoreCase)), "The newly opened Calculator should be recaptured.");
        Assert.IsTrue(FindAll<Button>(By.AccessibilityId("ToggleApplicationInclusionButton")).Any(button => button.Name.Contains("Calculator", StringComparison.OrdinalIgnoreCase)), "The newly captured Calculator should appear in the application list.");
    }

    private ProjectWrapper StoredWorkspace()
    {
        var storage = new WorkspacesData();
        var workspaces = storage.Read(storage.File).Workspaces.Where(project => project.Name == workspaceName).ToList();
        Assert.AreEqual(1, workspaces.Count, "The test workspace should be saved exactly once.");
        return workspaces[0];
    }

    private ApplicationWrapper CapturedNotepad()
    {
        var notepads = StoredWorkspace().Applications.Where(application => application.ApplicationPath.Contains("notepad", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.IsTrue(notepads.Count > 0, "The Notepad fixture should have been captured.");
        return notepads[0];
    }

    private Element FindCapturedNotepad() => Find<Element>(By.AccessibilityId(CapturedNotepad().Application));

    private Element ExpandApplication(string name)
    {
        var app = Find<Element>(By.AccessibilityId(name));
        if (!app.FindAll<TextBox>(By.AccessibilityId("CommandLineTextBox"), timeoutMS: 500).Any(input => input.Displayed))
        {
            app.Click();
        }

        Assert.IsTrue(app.Find<TextBox>(By.AccessibilityId("CommandLineTextBox")).Displayed, "The application expander should show its edit inputs.");
        return app;
    }

    private Button InclusionButton(Element app, string action)
    {
        // The inclusion button is a sibling of the disabled expander, so scope by ID and app name.
        var buttons = FindAll<Button>(By.AccessibilityId("ToggleApplicationInclusionButton"))
            .Where(button => button.Name == $"{action} {app.Name}")
            .OrderBy(button => Math.Abs((button.Rect?.Top ?? int.MaxValue) - (app.Rect?.Top ?? 0)))
            .ToList();
        Assert.IsTrue(buttons.Count > 0, $"The application should have a '{action}' button.");
        return buttons[0];
    }

    private int IncludedApplicationCount() => FindAll<Button>(By.AccessibilityId("ToggleApplicationInclusionButton")).Count(button => button.Name.StartsWith("Remove ", StringComparison.Ordinal));

    private void SelectPosition(Element app, string state)
    {
        app.Find<ComboBox>(By.AccessibilityId("WindowPositionComboBox")).Click();
        Find<Element>(By.Name(state), global: true).Click();
    }

    private Bitmap CapturePreview()
    {
        var bounds = GetPreviewImage().Rect!.Value;
        Session.MoveMouseTo(0, 0);
        var image = new Bitmap(bounds.Width, bounds.Height);
        using var graphics = Graphics.FromImage(image);
        graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        return image;
    }

    private void AssertPreviewPixels(Bitmap before, bool changed)
    {
        using var after = CapturePreview();
        Assert.AreEqual(before.Size, after.Size, "Editing an application should preserve preview dimensions.");
        bool difference = false;
        for (int y = 0; y < before.Height && !difference; y++)
        {
            for (int x = 0; x < before.Width; x++)
            {
                if (before.GetPixel(x, y) != after.GetPixel(x, y))
                {
                    difference = true;
                    break;
                }
            }
        }

        Assert.AreEqual(changed, difference, changed ? "The application edit should change the preview pixels." : "Adding the application back should restore the preview pixels.");
    }
}
