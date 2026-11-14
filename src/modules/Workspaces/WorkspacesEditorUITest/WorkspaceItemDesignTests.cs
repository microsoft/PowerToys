// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesCsharpLibrary.Data;

namespace WorkspacesEditorUITest;

/// <summary>
/// Design validation tests for workspace items in the list.
/// When workspaces exist, each item must have: name, app count, launch button.
/// Editing is initiated by clicking the card itself (no separate Edit button).
///
/// These define the per-item UI contract for the WinUI 3 editor.
/// </summary>
[TestClass]
public class WorkspaceItemDesignTests : WorkspacesUiAutomationBase
{
    private string workspaceName = string.Empty;

    public WorkspaceItemDesignTests()
        : base()
    {
    }

    [TestInitialize]
    public void Setup()
    {
        workspaceName = "LegacyItemDesign_" + Guid.NewGuid().ToString("N");
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

    [TestMethod("WorkspaceItem.HasName")]
    [TestCategory("Design.WorkspaceItem")]
    public void WorkspaceItem_DisplaysName()
    {
        var item = GetWorkspaceCard(workspaceName);
        Assert.AreEqual(workspaceName, item.Name, "The workspace card should expose its saved name.");
        Assert.IsTrue(Guid.TryParse(item.AutomationId, out _), "The workspace card should expose its stable GUID.");
    }

    [TestMethod("WorkspaceItem.HasLaunchButton")]
    [TestCategory("Design.WorkspaceItem")]
    public void WorkspaceItem_HasLaunchButton()
    {
        var item = GetWorkspaceCard(workspaceName);
        var launchButton = item.Find<Button>(By.AccessibilityId("LaunchButton"));
        Assert.IsTrue(launchButton.Displayed && launchButton.Enabled, "Workspace item should have an enabled Launch button.");
        Assert.AreEqual("Launch", launchButton.Name, "The Launch button should have its localized accessible name.");
    }

    [TestMethod("WorkspaceItem.CardIsClickable")]
    [TestCategory("Design.WorkspaceItem")]
    public void WorkspaceItem_CardIsClickableForEditing()
    {
        OpenWorkspace(workspaceName);
        Assert.IsTrue(Find<Button>(By.AccessibilityId("SaveButton")).Displayed, "Clicking the workspace card should open the editing page.");
        ReturnToMainPage();
    }

    [TestMethod("WorkspaceItem.HasSortButton")]
    [TestCategory("Design.WorkspaceItem")]
    public void WorkspaceItem_HasSortButton()
    {
        var sort = Find<Button>(By.AccessibilityId("WorkspaceSortButton"));
        Assert.IsTrue(sort.Displayed && sort.Enabled, "The workspace list should expose its Sort by button.");
        sort.Click();
        Assert.IsTrue(Find<Element>(By.AccessibilityId("SortNameItem")).Displayed, "The sort button should open its ordering flyout.");
        SendKeys(Key.Esc);
    }

    [TestMethod("WorkspaceItem.HasAppCountText")]
    [TestCategory("Design.WorkspaceItem")]
    public void WorkspaceItem_DisplaysAppCount()
    {
        var item = GetWorkspaceCard(workspaceName);
        var storage = new WorkspacesData();
        var workspace = storage.Read(storage.File).Workspaces.Single(project => project.Name == workspaceName);
        int count = workspace.Applications.Count;
        string expected = $"{count} {(count == 1 ? "app" : "apps")}";
        Assert.IsTrue(item.FindAll<TextBlock>(By.Name(expected)).Any(text => text.Displayed), "The workspace card should show the captured application count.");
    }

    [TestMethod("WorkspaceItem.HasLastLaunchedText")]
    [TestCategory("Design.WorkspaceItem")]
    public void WorkspaceItem_DisplaysLastLaunchedTime()
    {
        var item = GetWorkspaceCard(workspaceName);
        var lastLaunched = item.Find<TextBlock>(By.Name("Last launched: never"));
        Assert.IsTrue(lastLaunched.Displayed, "A new workspace card should show that it has never been launched.");
    }
}
