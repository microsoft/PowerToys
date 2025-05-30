// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WorkspacesEditorUITest;

[TestClass]
public class WorkspacesLauncherTest : WorkspacesUiAutomationBase
{
    public WorkspacesLauncherTest()
        : base()
    {
    }

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
}
