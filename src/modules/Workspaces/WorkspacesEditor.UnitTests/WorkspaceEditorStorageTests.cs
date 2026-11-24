// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesEditor.Messages;
using WorkspacesEditor.Models;
using WorkspacesEditor.Services;
using WorkspacesEditor.Utils;
using WorkspacesEditor.ViewModels;

namespace WorkspacesEditor.UnitTests;

[TestClass]
public sealed class WorkspaceEditorStorageTests
{
    private DirectoryInfo _folder;
    private string _path;
    private WorkspacesEditorIO _io;

    [TestInitialize]
    public void Initialize()
    {
        _folder = Directory.CreateTempSubdirectory("PowerToys-EditorStore-");
        _path = Path.Combine(_folder.FullName, "workspaces.json");
        _io = new WorkspacesEditorIO(Path.Combine(_folder.FullName, "temp-workspaces.json"), (_, _) => Task.FromResult(false));
    }

    [TestCleanup]
    public void Cleanup()
    {
        StrongReferenceMessenger.Default.UnregisterAll(this);
        _folder.Delete(recursive: true);
    }

    [TestMethod]
    public void SavingThroughTheEditorDoesNotUndoNativeMetadataUpdates()
    {
        var first = TestHelpers.CreateProject("First", 1, 0, "One");
        var second = TestHelpers.CreateProject("Second", 2, 0, "Two");
        _io.SerializeWorkspaces([first, second]);
        using var vm = new MainViewModel(_io, new WorkspacesSettings(), _ => { }, new NoShortcuts(), null, _ => { }, _ => { });
        Project draft = null;
        StrongReferenceMessenger.Default.Register<NavigateToEditorMessage>(this, (_, message) => draft = message.Project);
        Assert.IsTrue(_io.ParseWorkspaces(vm).Result);
        vm.OrderByIndex = 0;
        vm.EditProject(vm.Workspaces.Single(project => project.Id == second.Id));
        Assert.IsNotNull(draft);

        var native = JsonNode.Parse(File.ReadAllText(_path));
        var other = native["workspaces"][0];
        other["last-launched-time"] = 100;
        other["applications"][0]["application-path"] = "new-install.exe";
        other["applications"][0]["package-full-name"] = "native-package";
        other["applications"][0]["id"] = Guid.NewGuid().ToString("B");
        native["workspaces"][1]["last-launched-time"] = 300;
        native["workspaces"][1]["applications"][0]["application-path"] = "second-install.exe";
        File.WriteAllText(_path, native.ToJsonString());

        draft.Name = "Edited";
        vm.SaveOrAddProject(draft);

        Assert.IsFalse(vm.SaveFailed);
        var saved = JsonNode.Parse(File.ReadAllText(_path))["workspaces"].AsArray();
        Assert.IsTrue(JsonNode.DeepEquals(other, saved[0]));
        Assert.AreEqual("Edited", saved[1]["name"].GetValue<string>());
        Assert.AreEqual("second-install.exe", vm.Workspaces.Single(project => project.Id == second.Id).Applications[0].AppPath);
        Assert.AreEqual(300L, vm.WorkspacesView[0].LastLaunchedTime);
        Assert.AreEqual(second.Id, vm.WorkspacesView[0].Id);
        Assert.AreEqual(300L, saved[1]["last-launched-time"].GetValue<long>());
    }

    [TestMethod]
    public void FailedInitialAndSubsequentLoadsCanBeRetriedWithoutAnEmptyWorkspaceMessage()
    {
        using var vm = CreateViewModel();
        File.WriteAllText(_path, "{");
        vm.ReloadWorkspacesCommand.Execute(null);
        Assert.IsTrue(vm.WorkspaceReadFailed);
        Assert.IsFalse(vm.IsWorkspacesViewEmpty);
        Assert.IsFalse(vm.SaveFailed);

        File.Delete(_path);
        vm.ReloadWorkspacesCommand.Execute(null);
        Assert.IsFalse(vm.WorkspaceReadFailed);
        Assert.IsTrue(vm.IsWorkspacesViewEmpty);
        var first = TestHelpers.CreateProject("First", 0, 0, "App");
        _io.SerializeWorkspaces([first]);
        vm.ReloadWorkspacesCommand.Execute(null);
        var canonical = vm.Workspaces.Single();

        var damaged = JsonNode.Parse(File.ReadAllText(_path));
        damaged["workspaces"].AsArray().Add(new JsonObject { ["id"] = Guid.NewGuid().ToString("B") });
        File.WriteAllText(_path, damaged.ToJsonString());
        vm.ReloadWorkspacesCommand.Execute(null);
        Assert.IsTrue(vm.WorkspaceReadFailed);
        Assert.AreSame(canonical, vm.WorkspacesView.Single());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ReadRetryPreservesDraftAndExplicitDiscardReopensTheLatestVersion(bool deleting)
    {
        var first = TestHelpers.CreateProject("First", 0, 0, "App");
        _io.SerializeWorkspaces([first]);
        using var vm = CreateViewModel();
        Project draft = null;
        StrongReferenceMessenger.Default.Register<NavigateToEditorMessage>(this, (_, message) => draft = message.Project);
        vm.ReloadWorkspacesCommand.Execute(null);
        vm.EditProject(vm.Workspaces.Single());
        var originalDraft = draft;
        draft.Name = "Local draft";
        var external = JsonNode.Parse(File.ReadAllText(_path));
        external["workspaces"][0]["name"] = "External change";
        File.WriteAllText(_path, external.ToJsonString());
        if (deleting)
        {
            vm.DeleteProjectById(draft.Id);
        }
        else
        {
            vm.SaveOrAddProject(draft);
        }

        Assert.IsTrue(vm.WorkspaceSaveConflict);
        Assert.IsFalse(vm.SaveFailed);
        Assert.AreSame(originalDraft, draft);
        File.WriteAllText(_path, "{");
        vm.DiscardAndReloadWorkspaceCommand.Execute(null);
        Assert.IsTrue(vm.WorkspaceReadFailed);
        Assert.IsTrue(vm.WorkspaceSaveConflict);
        Assert.AreSame(originalDraft, draft);
        Assert.AreEqual("Local draft", draft.Name);
        Assert.AreEqual("First", vm.Workspaces.Single().Name);

        File.WriteAllText(_path, external.ToJsonString());
        vm.ReloadWorkspacesCommand.Execute(null);
        Assert.IsFalse(vm.WorkspaceReadFailed);
        Assert.IsTrue(vm.WorkspaceSaveConflict);
        Assert.AreSame(originalDraft, draft);
        Assert.AreEqual("First", vm.Workspaces.Single().Name);
        vm.DiscardAndReloadWorkspaceCommand.Execute(null);
        Assert.IsFalse(vm.WorkspaceSaveConflict);
        Assert.AreNotSame(originalDraft, draft);
        Assert.AreEqual("External change", draft.Name);
        draft.Name = "Saved after reload";
        vm.SaveOrAddProject(draft);
        Assert.IsFalse(vm.SaveFailed);
        Assert.IsFalse(vm.WorkspaceSaveConflict);
        Assert.AreEqual("Saved after reload", JsonNode.Parse(File.ReadAllText(_path))["workspaces"][0]["name"].GetValue<string>());
    }

    [TestMethod]
    public void DiscardAfterExternalDeletionReturnsToTheUpdatedOverview()
    {
        var first = TestHelpers.CreateProject("First", 0, 0, "App");
        _io.SerializeWorkspaces([first]);
        using var vm = CreateViewModel();
        Project draft = null;
        int goBack = 0;
        StrongReferenceMessenger.Default.Register<NavigateToEditorMessage>(this, (_, message) => draft = message.Project);
        StrongReferenceMessenger.Default.Register<GoBackMessage>(this, (_, _) => goBack++);
        vm.ReloadWorkspacesCommand.Execute(null);
        vm.EditProject(vm.Workspaces.Single());
        draft.Name = "My changes";
        File.Delete(_path);
        vm.SaveOrAddProject(draft);
        Assert.IsTrue(vm.WorkspaceSaveConflict);
        Assert.AreEqual(0, goBack);
        vm.DiscardAndReloadWorkspaceCommand.Execute(null);
        Assert.IsFalse(vm.WorkspaceSaveConflict);
        Assert.IsFalse(vm.WorkspaceReadFailed);
        Assert.IsEmpty(vm.Workspaces);
        Assert.AreEqual(1, goBack);
        Assert.IsFalse(File.Exists(_path));
    }

    [TestMethod]
    public void ReopeningAfterDiscardAndFailedRefreshUsesANewBaseline()
    {
        _io.SerializeWorkspaces([TestHelpers.CreateProject("Original", 0, 0, "App")]);
        using var vm = CreateViewModel();
        Project draft = null;
        StrongReferenceMessenger.Default.Register<NavigateToEditorMessage>(this, (_, message) => draft = message.Project);
        vm.ReloadWorkspacesCommand.Execute(null);
        var oldCard = vm.Workspaces.Single();
        vm.EditProject(oldCard);
        var latest = JsonNode.Parse(File.ReadAllText(_path));
        latest["workspaces"][0]["name"] = "Current";
        File.WriteAllText(_path, latest.ToJsonString());
        draft.Name = "Unsaved";
        vm.SaveProject(draft);
        Assert.IsTrue(vm.WorkspaceSaveConflict);
        File.WriteAllText(_path, "{");
        vm.SwitchToMainView();
        Assert.IsTrue(vm.WorkspaceReadFailed);
        Assert.AreSame(oldCard, vm.Workspaces.Single());

        File.WriteAllText(_path, latest.ToJsonString());
        vm.EditProject(oldCard);
        Assert.AreEqual("Current", draft.Name);
        Assert.IsFalse(vm.WorkspaceReadFailed);
        draft.Name = "Changed";
        vm.SaveProject(draft);
        Assert.IsFalse(vm.WorkspaceSaveConflict);
        Assert.AreEqual("Changed", vm.Workspaces.Single().Name);
    }

    [TestMethod]
    public void DiscardAfterAnAddCollisionLoadsTheExistingWorkspace()
    {
        using var vm = CreateViewModel();
        Project draft = null;
        StrongReferenceMessenger.Default.Register<NavigateToEditorMessage>(this, (_, message) => draft = message.Project);
        var newProject = TestHelpers.CreateProject("New", 0, 0, "App");
        vm.EditProject(newProject, true);
        draft.Name = "Local";
        _io.SerializeWorkspaces([new Project(newProject) { Name = "Other workspace" }]);
        vm.SaveOrAddProject(draft);
        Assert.IsTrue(vm.WorkspaceSaveConflict);
        Assert.AreEqual("Local", draft.Name);
        Assert.IsEmpty(vm.Workspaces);
        vm.DiscardAndReloadWorkspaceCommand.Execute(null);
        Assert.AreEqual("Other workspace", vm.Workspaces.Single().Name);
        Assert.AreEqual("Other workspace", draft.Name);
        Assert.IsFalse(vm.WorkspaceSaveConflict);
    }

    [TestMethod]
    public void ShortcutCleanupGetsIdsOnlyFromAnUnambiguousSavedList()
    {
        Assert.IsEmpty(_io.GetStoredWorkspaceIds());
        var id = Guid.NewGuid();
        var rows = new JsonArray(new JsonObject { ["id"] = id.ToString("D"), ["future-format"] = true });
        var document = new JsonObject { ["workspaces"] = rows };
        File.WriteAllText(_path, document.ToJsonString());
        CollectionAssert.AreEqual(new[] { id.ToString("B") }, _io.GetStoredWorkspaceIds().ToArray());

        rows.Add(new JsonObject { ["id"] = id.ToString("B") });
        File.WriteAllText(_path, document.ToJsonString());
        Assert.IsNull(_io.GetStoredWorkspaceIds());
        rows[1] = new JsonObject { ["name"] = "Missing ID" };
        File.WriteAllText(_path, document.ToJsonString());
        Assert.IsNull(_io.GetStoredWorkspaceIds());
    }

    private MainViewModel CreateViewModel() => new(_io, new WorkspacesSettings(), _ => { }, new NoShortcuts(), null, _ => { }, _ => { });

    private sealed class NoShortcuts : IWorkspaceShortcutService
    {
        public bool Exists(Project project) => false;

        public void Apply(Project project, string previousName = null)
        {
        }

        public void Remove(Project project)
        {
        }
    }
}
