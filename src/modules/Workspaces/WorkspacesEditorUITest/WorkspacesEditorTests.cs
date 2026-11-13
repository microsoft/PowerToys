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
        var create = Find<Button>(By.AccessibilityId("NewProjectButton"));
        Assert.IsTrue(create.Displayed && create.Enabled, "Should have an enabled Create Workspace button.");
    }

    // These external-application scenarios were disabled before the WinUI migration.
    // The Next suite provides the active, controlled-fixture behavior coverage.
    /*
    [TestMethod("WorkspacesEditor.Editor.NewWorkspaceAppearsInList")]
    [TestCategory("Workspaces UI")]
    public void TestNewWorkspaceAppearsInListAfterCapture()
    {
        string name = "LegacyCapture_" + Guid.NewGuid().ToString("N");
        CreateTestWorkspace(name);
        Assert.AreEqual(name, GetWorkspaceCard(name).Name, "The captured workspace should appear in the list.");
        DeleteWorkspace(name);
    }

    [TestMethod("WorkspacesEditor.Editor.CancelCaptureDoesNotAddWorkspace")]
    [TestCategory("Workspaces UI")]
    public void TestCancelCaptureDoesNotAddWorkspace()
    {
        var initialIds = GetWorkspaceCards().Select(card => card.AutomationId).ToArray();
        StartCapture();
        CancelSnapshot();
        CollectionAssert.AreEquivalent(initialIds, GetWorkspaceCards().Select(card => card.AutomationId).ToArray(), "Canceling capture should preserve the workspace list.");
    }

    [TestMethod("WorkspacesEditor.Editor.SearchFiltersWorkspaces")]
    [TestCategory("Workspaces UI")]
    public void TestSearchFiltersWorkspaces()
    {
        string prefix = "LegacySearch_" + Guid.NewGuid().ToString("N");
        CreateTestWorkspace(prefix + "_1");
        CreateTestWorkspace(prefix + "_2");
        string other = "DifferentName_" + Guid.NewGuid().ToString("N");
        CreateTestWorkspace(other);

        var search = GetSearchBox();
        search.SetText(prefix);
        CollectionAssert.AreEquivalent(new[] { prefix + "_1", prefix + "_2" }, GetWorkspaceCards().Select(card => card.Name).ToArray(), "Search should display exactly the matching workspace cards.");
        search.SetText(string.Empty);
        DeleteWorkspace(prefix + "_1");
        DeleteWorkspace(prefix + "_2");
        DeleteWorkspace(other);
    }

    [TestMethod("WorkspacesEditor.Editor.SortByWorks")]
    [TestCategory("Workspaces UI")]
    public void TestSortByFunctionality()
    {
        string prefix = "LegacySort_" + Guid.NewGuid().ToString("N");
        CreateTestWorkspace(prefix + "_Z");
        CreateTestWorkspace(prefix + "_A");
        GetSearchBox().SetText(prefix);
        SelectSortOrder("SortNameItem");
        CollectionAssert.AreEqual(new[] { prefix + "_A", prefix + "_Z" }, GetWorkspaceCards().Select(card => card.Name).ToArray(), "Name sorting should order workspace cards alphabetically.");
        AssertSortOrderSelected("SortNameItem");
        GetSearchBox().SetText(string.Empty);
        DeleteWorkspace(prefix + "_Z");
        DeleteWorkspace(prefix + "_A");
    }

    [TestMethod("WorkspacesEditor.Editor.SortByPersists")]
    [TestCategory("Workspaces UI")]
    public void TestSortByPersistsAfterRestart()
    {
        SelectSortOrder("SortCreatedItem");
        AssertSortOrderSelected("SortCreatedItem");
        RestartScopeExe();
        AssertMainPage();
        AssertSortOrderSelected("SortCreatedItem");
    }

    [TestMethod("WorkspacesEditor.Editor.RemoveWorkspace")]
    [TestCategory("Workspaces UI")]
    public void TestRemoveWorkspace()
    {
        string name = "LegacyRemove_" + Guid.NewGuid().ToString("N");
        CreateTestWorkspace(name);
        DeleteWorkspace(name);
        Assert.IsFalse(GetWorkspaceCards().Any(card => card.Name == name), "The deleted workspace should be absent from the list.");
    }

    [TestMethod("WorkspacesEditor.Editor.EditOpensEditingPage")]
    [TestCategory("Workspaces UI")]
    public void TestEditOpensEditingPage()
    {
        string name = "LegacyEdit_" + Guid.NewGuid().ToString("N");
        CreateTestWorkspace(name);
        OpenWorkspace(name);
        Assert.IsTrue(Find<Button>(By.AccessibilityId("SaveButton")).Displayed, "Editing should expose Save.");
        Assert.IsTrue(Find<Button>(By.AccessibilityId("PART_BackButton")).Displayed, "Editing should expose title-bar back navigation.");
        ReturnToMainPage();
        DeleteWorkspace(name);
    }

    [TestMethod("WorkspacesEditor.Editor.ClickWorkspaceOpensEditingPage")]
    [TestCategory("Workspaces UI")]
    public void TestClickWorkspaceOpensEditingPage()
    {
        string name = "LegacyCard_" + Guid.NewGuid().ToString("N");
        CreateTestWorkspace(name);
        GetWorkspaceCard(name).Click();
        Assert.AreEqual(name, Find<TextBox>(By.AccessibilityId("EditNameTextBox")).Text, "Clicking the card should edit that workspace.");
        Find<TextBox>(By.AccessibilityId("EditNameTextBox")).SetText(name + "_unsaved");
        ReturnToMainPage();
        Assert.IsFalse(GetWorkspaceCards().Any(card => card.Name == name + "_unsaved"), "Back navigation should discard unsaved changes.");
        DeleteWorkspace(name);
    }
    */
}
