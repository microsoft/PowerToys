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
        OpenDraftFromCard(name);

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
        OpenDraftFromCard(WorkspaceTestState.Text(project, "name"));

        var repair = Editor.Find<Button>(By.AccessibilityId("RecaptureCurrentLayoutButton"), 15_000);
        Assert.IsFalse(Editor.Find<Button>(By.AccessibilityId("LaunchEditButton")).IsEnabled);
        Assert.IsFalse(Editor.Find<Button>(By.AccessibilityId("SaveButton")).IsEnabled);
        Assert.IsTrue(repair.IsEnabled);
        repair.Invoke(msPostAction: 0);
        _ = WaitForCaptureWindow();
        CollectionAssert.AreEqual(stored, File.ReadAllBytes(WorkspaceTestState.WorkspacesPath), "Starting a repair must preserve the saved layout.");
    }

    private void OpenDraftFromCard(string name)
    {
        OpenEditor();
        Editor.EnsureForeground();
        var card = Editor.Find<Button>(By.Name(name), 15_000);
        card.Focus();
        Assert.IsTrue(card.WaitForProperty("HasKeyboardFocus", "True"), "The workspace card did not receive keyboard focus.");
        KeyboardHelper.SendKeys(Key.Enter);
        Assert.IsTrue(Editor.Has(By.AccessibilityId("EditNameTextBox"), 15_000), "The selected workspace did not open for editing.");
    }
}
