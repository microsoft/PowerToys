// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Workspaces.UITests;

public sealed partial class WorkspacesTests
{
    [TestMethod]
    public void StartupReadFailureCanBeRetriedWithoutChangingTheWorkspaceFile()
    {
        var project = SeedOneWorkspace();
        var name = WorkspaceTestState.Text(project, "name");
        var original = File.ReadAllBytes(WorkspaceTestState.WorkspacesPath);
        Button retry;
        using (var locked = new FileStream(WorkspaceTestState.WorkspacesPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            OpenEditor();
            retry = VisibleStorageAction("ReloadWorkspacesAction");
        }

        retry.Invoke(msPostAction: 0);

        Assert.IsTrue(retry.WaitForGone(15_000), "The read error remained visible after the successful reload.");
        Assert.IsTrue(Editor.Find<Button>(By.Name(name), 15_000).Displayed, "Reload did not restore the saved workspace card.");
        CollectionAssert.AreEqual(original, File.ReadAllBytes(WorkspaceTestState.WorkspacesPath));
    }

    [TestMethod]
    public void ConflictingSaveKeepsTheDraftUntilAnExplicitReloadSucceeds()
    {
        var project = SeedOneWorkspace();
        string originalName = WorkspaceTestState.Text(project, "name");
        string draftName = State.Prefix + "-local-edit";
        string externalName = State.Prefix + "-external-edit";
        OpenDraftFromCard(originalName);
        var name = Editor.Find<TextBox>(By.AccessibilityId("EditNameTextBox"), 15_000);
        name.SetText(draftName);
        project["name"] = externalName;
        State.WriteProjects(project);
        byte[] external = File.ReadAllBytes(WorkspaceTestState.WorkspacesPath);

        Editor.Find<Button>(By.AccessibilityId("SaveButton")).Invoke(msPostAction: 0);
        var discard = VisibleStorageAction("DiscardAndReloadWorkspaceAction");
        var delete = Editor.Find<Button>(By.AccessibilityId("DeleteWorkspaceButton"));
        Assert.IsFalse(delete.IsEnabled, "Deletion must wait until the conflicting draft has been reloaded.");
        Assert.AreEqual(draftName, name.Value);
        CollectionAssert.AreEqual(external, File.ReadAllBytes(WorkspaceTestState.WorkspacesPath));

        using (var locked = new FileStream(WorkspaceTestState.WorkspacesPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            discard.Invoke(msPostAction: 0);
            _ = VisibleStorageAction("ReloadWorkspacesAction");
            Assert.AreEqual(draftName, name.Value, "An unsuccessful discard-and-reload must leave the draft available.");
        }

        var retry = VisibleStorageAction("ReloadWorkspacesAction");
        retry.Invoke(msPostAction: 0);
        Assert.IsTrue(retry.WaitForGone(15_000), "Retrying the read did not clear the read failure.");
        Assert.AreEqual(draftName, name.Value, "Retrying a read must not discard an edit.");
        VisibleStorageAction("DiscardAndReloadWorkspaceAction").Invoke(msPostAction: 0);

        Assert.IsTrue(
            WaitHelper.WaitForStable(
                () => name.Value,
                value => value == externalName,
                15_000,
                requiredConsecutiveMatches: 2).Succeeded,
            "Discard and reload did not reopen the externally saved workspace.");
        Assert.IsTrue(discard.WaitForGone(15_000), "The conflict remained visible after replacing the draft.");
        Assert.IsTrue(delete.WaitForProperty("IsEnabled", "True"), "The freshly reloaded workspace should allow deletion.");
        CollectionAssert.AreEqual(external, File.ReadAllBytes(WorkspaceTestState.WorkspacesPath));
    }

    private Button VisibleStorageAction(string automationId)
    {
        var button = Editor.Find<Button>(By.AccessibilityId(automationId), 15_000);
        Assert.IsTrue(button.Displayed && button.Width > 0 && button.Height > 0 && button.IsEnabled, $"Storage recovery action '{automationId}' is not available.");
        return button;
    }
}
