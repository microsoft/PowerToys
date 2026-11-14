// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WorkspacesEditorUITest;

[TestClass]
[Ignore("NOT STABLE")]
public class WorkspacesLauncherTest : WorkspacesUiAutomationBase
{
    private const string LauncherWindowTitle = "Your workspace is launching. Waiting on ...";

    public WorkspacesLauncherTest()
        : base()
    {
    }

    [TestMethod("WorkspacesEditor.Launcher.LaunchFromEditor")]
    [TestCategory("Workspaces UI")]
    public void TestLaunchWorkspaceFromEditor()
    {
        string name = "LegacyLaunch_" + Guid.NewGuid().ToString("N");
        CreateTestWorkspace(name);
        try
        {
            GetWorkspaceCard(name).Find<Button>(By.AccessibilityId("LaunchButton")).Click();
            Task.Delay(2000).Wait();
            Assert.IsTrue(Process.GetProcessesByName("notepad").Length > 0, "Launching the workspace should start its captured Notepad.");
            AssertMainPage();
        }
        finally
        {
            CloseTestApplications();
            AttachWorkspacesEditor();
            DeleteWorkspace(name);
        }
    }

    [TestMethod("WorkspacesEditor.Launcher.CancelLaunch")]
    [TestCategory("Workspaces UI")]
    public void TestCancelLaunch()
    {
        string name = CreateWorkspaceWithApps();
        try
        {
            GetWorkspaceCard(name).Find<Button>(By.AccessibilityId("LaunchButton")).Click();
            Session.Attach(LauncherWindowTitle);
            var cancel = Find<Button>(By.AccessibilityId("CancelButton"));
            Assert.AreEqual("Cancel launch", cancel.Name, "The launcher Cancel button should have its localized accessible name.");
            cancel.Click();
            Task.Delay(1000).Wait();
            Assert.IsFalse(WindowHelper.IsWindowOpen(LauncherWindowTitle), "Canceling launch should close the launcher status window.");
        }
        finally
        {
            CloseTestApplications();
            AttachWorkspacesEditor();
            DeleteWorkspace(name);
        }
    }

    [TestMethod("WorkspacesEditor.Launcher.DismissKeepsLaunching")]
    [TestCategory("Workspaces UI")]
    public void TestDismissKeepsAppsLaunching()
    {
        string name = CreateWorkspaceWithApps();
        try
        {
            GetWorkspaceCard(name).Find<Button>(By.AccessibilityId("LaunchButton")).Click();
            Session.Attach(LauncherWindowTitle);
            var dismiss = Find<Button>(By.AccessibilityId("DismissButton"));
            Assert.AreEqual("Dismiss", dismiss.Name, "The launcher Dismiss button should have its localized accessible name.");
            dismiss.Click();
            Task.Delay(1000).Wait();
            Assert.IsFalse(WindowHelper.IsWindowOpen(LauncherWindowTitle), "Dismissing launch should close the launcher status window.");
            Task.Delay(3000).Wait();
            Assert.IsTrue(Process.GetProcessesByName("notepad").Length > 0, "The captured Notepad should keep launching after dismiss.");
        }
        finally
        {
            CloseTestApplications();
            AttachWorkspacesEditor();
            DeleteWorkspace(name);
        }
    }
}
