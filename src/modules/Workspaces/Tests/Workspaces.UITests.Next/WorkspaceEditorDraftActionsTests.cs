// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Workspaces.UITests;

public sealed partial class WorkspacesTests
{
    [TestMethod]
    public void LaunchAndEditFollowsDraftValidity()
    {
        var project = SeedOneWorkspace();
        var name = WorkspaceTestState.Text(project, "name");
        OpenEditor();
        Editor.Find<Button>(By.Name(name), 15_000).Invoke(msPostAction: 0);

        var field = Editor.Find<TextBox>(By.AccessibilityId("EditNameTextBox"), 15_000);
        var launch = Editor.Find<Button>(By.AccessibilityId("LaunchEditButton"));
        Assert.IsTrue(launch.IsEnabled);
        field.SetText(string.Empty);
        Assert.IsTrue(launch.WaitForProperty("IsEnabled", "False"), "An unnamed workspace cannot be launched for editing.");
        field.SetText(name);
        Assert.IsTrue(launch.WaitForProperty("IsEnabled", "True"), "A valid draft should be launchable again.");
    }

    [TestMethod]
    public void MissingMonitorOffersRecaptureOfCurrentLayout()
    {
        var app = State.Application("Fixture on a missing display", State.Prefix + "-missing-monitor");
        app["monitor"] = int.MaxValue;
        var project = State.Project("repair-layout", app);
        State.WriteProjects(project);
        var stored = File.ReadAllBytes(WorkspaceTestState.WorkspacesPath);
        OpenEditor();
        Editor.Find<Button>(By.Name(WorkspaceTestState.Text(project, "name")), 15_000).Invoke(msPostAction: 0);

        var repair = Editor.Find<Button>(By.AccessibilityId("RecaptureCurrentLayoutButton"), 15_000);
        Assert.IsFalse(Editor.Find<Button>(By.AccessibilityId("LaunchEditButton")).IsEnabled);
        Assert.IsFalse(Editor.Find<Button>(By.AccessibilityId("SaveButton")).IsEnabled);
        Assert.IsTrue(repair.IsEnabled);
        repair.Invoke(msPostAction: 0);
        _ = WaitForCaptureWindow();
        CollectionAssert.AreEqual(stored, File.ReadAllBytes(WorkspaceTestState.WorkspacesPath), "Starting a repair must preserve the saved layout.");
    }
}
