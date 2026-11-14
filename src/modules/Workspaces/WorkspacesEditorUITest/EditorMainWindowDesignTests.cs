// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesCsharpLibrary.Data;

namespace WorkspacesEditorUITest;

/// <summary>
/// Design validation tests for the Workspaces Editor main window.
/// These verify that all expected UI elements are present and accessible,
/// serving as a contract that the WinUI migration must satisfy.
///
/// Window: MainWindow / WorkspacesEditorPage
/// Tests cover: header elements, action buttons, workspace list, search, sort.
/// </summary>
[TestClass]
public class EditorMainWindowDesignTests : WorkspacesUiAutomationBase
{
    public EditorMainWindowDesignTests()
        : base()
    {
    }

    [TestMethod("MainWindow.Header.TitleTextPresent")]
    [TestCategory("Design.MainWindow")]
    public void MainWindow_HasWorkspacesTitleText()
    {
        var window = Find<Window>(By.Name("Workspaces Editor"), global: true);
        Assert.IsTrue(window.Displayed, "The editor window should display its localized title.");
    }

    [TestMethod("MainWindow.Header.CreateWorkspaceButtonPresent")]
    [TestCategory("Design.MainWindow")]
    public void MainWindow_HasCreateWorkspaceButton()
    {
        Assert.IsTrue(Find<Button>(By.AccessibilityId("NewProjectButton")).Displayed, "Should have a visible Create Workspace button.");
    }

    [TestMethod("MainWindow.Header.SearchBoxPresent")]
    [TestCategory("Design.MainWindow")]
    public void MainWindow_HasSearchBox()
    {
        string prefix = "LegacySuggestion_" + Guid.NewGuid().ToString("N");
        string name = prefix + "_workspace";
        CreateTestWorkspace(name);
        try
        {
            var search = GetSearchBox();
            Assert.IsTrue(search.Displayed && search.Enabled, "Should have an enabled search input.");
            var ids = GetWorkspaceCards().Select(card => card.AutomationId).ToArray();
            search.SetText(prefix);
            Assert.AreEqual(prefix, search.Text, "The search input should accept a workspace name prefix.");
            CollectionAssert.AreEquivalent(ids, GetWorkspaceCards().Select(card => card.AutomationId).ToArray(), "Typing a search query should preserve the workspace overview.");

            SendKeys(Key.Down);
            Assert.IsTrue(SpinWait.SpinUntil(() => search.Text == name, TimeSpan.FromSeconds(5)), "Selecting the matching suggestion should fill the saved workspace name.");
            SendKeys(Key.Enter);
            Assert.AreEqual(name, Find<TextBox>(By.AccessibilityId("EditNameTextBox")).Text, "Submitting the selected suggestion should edit that workspace.");
            ReturnToMainPage();
        }
        finally
        {
            AttachWorkspacesEditor();
            if (Has(By.AccessibilityId("EditNameTextBox"), timeoutMS: 500))
            {
                ReturnToMainPage();
            }

            GetSearchBox().SetText(string.Empty);
            SendKeys(Key.Esc);
            DeleteWorkspace(name);
        }
    }

    [TestMethod("MainWindow.Header.SortByPresent")]
    [TestCategory("Design.MainWindow")]
    public void MainWindow_HasSortByFlyout()
    {
        var sort = Find<Button>(By.AccessibilityId("WorkspaceSortButton"));
        Assert.IsTrue(sort.Displayed && sort.Enabled, "Should have an enabled Sort by button.");
        sort.Click();
        Assert.IsTrue(Find<Element>(By.AccessibilityId("SortLastLaunchedItem")).Displayed, "Should offer Last launched sorting.");
        Assert.IsTrue(Find<Element>(By.AccessibilityId("SortCreatedItem")).Displayed, "Should offer Created sorting.");
        Assert.IsTrue(Find<Element>(By.AccessibilityId("SortNameItem")).Displayed, "Should offer Name sorting.");
        SendKeys(Key.Esc);
    }

    [TestMethod("MainWindow.Content.WorkspacesListPresent")]
    [TestCategory("Design.MainWindow")]
    public void MainWindow_HasWorkspacesList()
    {
        string name = "LegacyListDesign_" + Guid.NewGuid().ToString("N");
        CreateTestWorkspace(name);
        try
        {
            var storage = new WorkspacesData();
            var saved = storage.Read(storage.File).Workspaces.Single(project => project.Name == name);
            var cards = GetWorkspaceCards();
            var fixtureCards = cards.Where(card => Guid.Parse(card.AutomationId) == Guid.Parse(saved.Id)).ToList();
            Assert.AreEqual(1, fixtureCards.Count, "The saved workspace ID should expose exactly one card.");
            Assert.IsTrue(fixtureCards[0].Displayed && fixtureCards[0].Enabled, "The saved workspace card should be visible and enabled.");
            Assert.AreEqual(name, fixtureCards[0].Name, "The card should expose its saved workspace name.");

            var ids = cards.Select(card => card.AutomationId).ToArray();
            OpenWorkspace(name);
            ReturnToMainPage();
            CollectionAssert.AreEquivalent(ids, GetWorkspaceCards().Select(card => card.AutomationId).ToArray(), "Editing and returning should preserve the workspace overview cards.");
        }
        finally
        {
            DeleteWorkspace(name);
        }
    }

    [TestMethod("MainWindow.Content.EmptyStateMessagePresent")]
    [TestCategory("Design.MainWindow")]
    public void MainWindow_EmptyState_ShowsMessage()
    {
        var storage = new WorkspacesData();
        byte[]? original = File.Exists(storage.File) ? File.ReadAllBytes(storage.File) : null;
        ExitScopeExe();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(storage.File)!);
            File.WriteAllText(storage.File, storage.Serialize(new WorkspacesData.WorkspacesListWrapper { Workspaces = [] }));
            RestartScopeExe();
            AssertMainPage();
            Assert.AreEqual(0, GetWorkspaceCards().Count, "An empty saved-workspace store should expose no workspace cards.");
            var message = Find<TextBlock>(By.Name("There are no saved Workspaces."));
            Assert.IsTrue(message.Displayed, "An empty saved-workspace store should display the No_Workspaces_Message resource.");
        }
        finally
        {
            ExitScopeExe();
            if (original is null)
            {
                File.Delete(storage.File);
            }
            else
            {
                File.WriteAllBytes(storage.File, original);
            }

            RestartScopeExe();
        }
    }

    [TestMethod("MainWindow.Keyboard.TabNavigationWorks")]
    [TestCategory("Design.MainWindow")]
    public void MainWindow_TabNavigation_MovesForwardThroughControls()
    {
        var search = GetSearchBox();
        search.Click();
        Assert.IsTrue(bool.Parse(search.GetAttribute("HasKeyboardFocus")), "The search input should receive keyboard focus.");
        SendKeys(Key.Tab);
        Task.Delay(500).Wait();
        var focused = FindAll<Element>(By.XPath("//*[@HasKeyboardFocus='True' or @HasKeyboardFocus='true']"), timeoutMS: 1000);
        Assert.AreEqual(1, focused.Count, "Tab should place keyboard focus on one control.");
        Assert.IsTrue(focused[0].Enabled && focused[0].Displayed, "The focused control should be enabled and visible.");
        Assert.IsFalse(bool.Parse(search.GetAttribute("HasKeyboardFocus")), "Tab should move focus forward from the search input.");
    }

    [TestMethod("MainWindow.Accessibility.CreateButtonHasAutomationName")]
    [TestCategory("Design.MainWindow")]
    public void MainWindow_CreateButton_HasAccessibleName()
    {
        var button = Find<Button>(By.AccessibilityId("NewProjectButton"));
        Assert.AreEqual("Create Workspace", button.Name, "The Create Workspace button should have its localized accessible name.");
    }
}
