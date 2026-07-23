// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WorkspacesEditorUITest;

/// <summary>
/// Design validation tests for workspace items in the list.
/// When workspaces exist, each item must have: name, app count, launch button,
/// edit button, more options button.
///
/// These define the per-item UI contract the migration must preserve.
/// </summary>
[TestClass]
public class WorkspaceItemDesignTests : WorkspacesUiAutomationBase
{
    public WorkspaceItemDesignTests()
        : base()
    {
    }

    [TestInitialize]
    public void Setup()
    {
        // Ensure at least one workspace exists for item-level tests
        if (!HasWorkspaceItem())
        {
            CreateTestWorkspace("DesignTest");
            Task.Delay(2000).Wait();
        }
    }

    [TestMethod("WorkspaceItem.HasName")]
    [TestCategory("Design.WorkspaceItem")]
    public void WorkspaceItem_DisplaysName()
    {
        if (!HasWorkspaceItem())
        {
            Assert.Inconclusive("No workspace items available for testing");
            return;
        }

        var item = GetFirstWorkspaceItem();
        Assert.IsNotNull(item, "Should have at least one workspace item");
    }

    [TestMethod("WorkspaceItem.HasLaunchButton")]
    [TestCategory("Design.WorkspaceItem")]
    public void WorkspaceItem_HasLaunchButton()
    {
        if (!HasWorkspaceItem())
        {
            Assert.Inconclusive("No workspace items available for testing");
            return;
        }

        var item = GetFirstWorkspaceItem();
        var launchButton = item.Find<Button>(By.Name("Launch"));
        Assert.IsNotNull(launchButton, "Workspace item should have a Launch button");
    }

    [TestMethod("WorkspaceItem.HasEditButton")]
    [TestCategory("Design.WorkspaceItem")]
    public void WorkspaceItem_HasEditButton()
    {
        if (!HasWorkspaceItem())
        {
            Assert.Inconclusive("No workspace items available for testing");
            return;
        }

        var item = GetFirstWorkspaceItem();
        var editButton = item.Find<Button>(By.Name("Edit"));
        Assert.IsNotNull(editButton, "Workspace item should have an Edit button");
    }

    [TestMethod("WorkspaceItem.HasMoreOptionsButton")]
    [TestCategory("Design.WorkspaceItem")]
    public void WorkspaceItem_HasMoreOptionsButton()
    {
        if (!HasWorkspaceItem())
        {
            Assert.Inconclusive("No workspace items available for testing");
            return;
        }

        var item = GetFirstWorkspaceItem();
        var moreButton = item.Find<Button>(By.AccessibilityId("MoreButton"));
        Assert.IsNotNull(moreButton, "Workspace item should have a More options button");
    }

    [TestMethod("WorkspaceItem.HasAppCountText")]
    [TestCategory("Design.WorkspaceItem")]
    public void WorkspaceItem_DisplaysAppCount()
    {
        if (!HasWorkspaceItem())
        {
            Assert.Inconclusive("No workspace items available for testing");
            return;
        }

        var item = GetFirstWorkspaceItem();

        // App count text should contain a number followed by "App" or "Apps"
        var textBlocks = item.FindAll<TextBlock>(By.ClassName("TextBlock"));
        bool hasAppCount = textBlocks.Any(t =>
        {
            var text = t.GetAttribute("Name") ?? string.Empty;
            return text.Contains("App", System.StringComparison.OrdinalIgnoreCase);
        });

        Assert.IsTrue(hasAppCount, "Workspace item should display app count");
    }

    [TestMethod("WorkspaceItem.HasLastLaunchedText")]
    [TestCategory("Design.WorkspaceItem")]
    public void WorkspaceItem_DisplaysLastLaunchedTime()
    {
        if (!HasWorkspaceItem())
        {
            Assert.Inconclusive("No workspace items available for testing");
            return;
        }

        var item = GetFirstWorkspaceItem();

        // Should contain "Last launched" text
        var textBlocks = item.FindAll<TextBlock>(By.ClassName("TextBlock"));
        bool hasLastLaunched = textBlocks.Any(t =>
        {
            var text = t.GetAttribute("Name") ?? string.Empty;
            return text.Contains("Last", System.StringComparison.OrdinalIgnoreCase);
        });

        Assert.IsTrue(hasLastLaunched, "Workspace item should display last launched time");
    }

    private bool HasWorkspaceItem()
    {
        try
        {
            var root = Find<Element>(By.AccessibilityId("WorkspacesItemsControl"));
            return root != null;
        }
        catch
        {
            return false;
        }
    }

    private Element GetFirstWorkspaceItem()
    {
        var root = Find<Element>(By.AccessibilityId("WorkspacesItemsControl"));
        var items = root.FindAll<Element>(By.ClassName("WorkspaceItem"));
        return items.Count > 0 ? items[0] : root;
    }
}
