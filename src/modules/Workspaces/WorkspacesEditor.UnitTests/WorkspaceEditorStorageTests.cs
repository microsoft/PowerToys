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
        vm.EditProject(vm.Workspaces.Single(project => project.Id == second.Id));
        Assert.IsNotNull(draft);

        var native = JsonNode.Parse(File.ReadAllText(_path));
        var other = native["workspaces"][0];
        other["last-launched-time"] = 100;
        other["applications"][0]["application-path"] = "new-install.exe";
        other["applications"][0]["package-full-name"] = "native-package";
        other["applications"][0]["id"] = Guid.NewGuid().ToString("B");
        native["workspaces"][1]["applications"][0]["application-path"] = "second-install.exe";
        File.WriteAllText(_path, native.ToJsonString());

        draft.Name = "Edited";
        vm.SaveOrAddProject(draft);

        Assert.IsFalse(vm.SaveFailed);
        var saved = JsonNode.Parse(File.ReadAllText(_path))["workspaces"].AsArray();
        Assert.IsTrue(JsonNode.DeepEquals(other, saved[0]));
        Assert.AreEqual("Edited", saved[1]["name"].GetValue<string>());
        Assert.AreEqual("second-install.exe", vm.Workspaces.Single(project => project.Id == second.Id).Applications[0].AppPath);
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
