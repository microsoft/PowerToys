// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesCsharpLibrary.Data;
using WorkspacesCsharpLibrary.Utils;
using WorkspacesEditor.Models;
using WorkspacesEditor.ViewModels;

namespace WorkspacesCsharpLibrary.UnitTests;

[TestClass]
public class WorkspaceWriteTests
{
    private string directory = string.Empty;
    private string file = string.Empty;

    [TestInitialize]
    public void Initialize()
    {
        directory = Path.Combine(Path.GetTempPath(), "WorkspacesStore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        file = Path.Combine(directory, "workspaces.json");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (File.Exists(file))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(directory, recursive: true);
    }

    [TestMethod]
    public void EditorSnapshotPreservesIdentityAndHistory()
    {
        var original = new WorkspacesEditor.Models.Project(new ProjectWrapper
        {
            Id = "{6CF910A2-D2E0-436D-A50E-41432A88452A}",
            Name = "Saved workspace",
            CreationTime = 100,
            LastLaunchedTime = 200,
            Applications = [],
            MonitorConfiguration = [],
        });
        var copy = new WorkspacesEditor.Models.Project(original);
        Assert.AreEqual(original.Id, copy.Id);
        Assert.AreEqual(100L, copy.CreationTime);
        Assert.AreEqual(200L, copy.LastLaunchedTime);
    }

    [TestMethod]
    public void FailedNewWorkspaceSavePreservesDraft()
    {
        var draft = CreateDraft();
        var applications = draft.Applications.ToArray();
        var existing = CreateDraft();
        existing.Name = "Existing workspace";
        List<Project> workspaces = [existing];
        int attempts = 0;

        Assert.IsFalse(MainViewModel.TryPersistNewProject(draft, workspaces, candidates =>
        {
            attempts++;
            CollectionAssert.AreEqual(new[] { existing, draft }, candidates);
            return false;
        }));

        Assert.AreEqual(1, attempts);
        CollectionAssert.AreEqual(applications, draft.Applications);
        Assert.IsTrue(draft.Applications[0].IsIncluded);
        Assert.IsFalse(draft.Applications[1].IsIncluded);
        Assert.AreEqual("--edited", draft.Applications[1].CommandLineArguments);
        Assert.AreEqual("New workspace", draft.Name);
        CollectionAssert.AreEqual(new[] { existing }, workspaces);
    }

    [TestMethod]
    public void FailedNewWorkspaceSaveCanReincludeApplicationAndRetry()
    {
        var draft = CreateDraft();
        var excluded = draft.Applications[1];
        List<Project> workspaces = [];
        Assert.IsFalse(MainViewModel.TryPersistNewProject(draft, workspaces, _ => false));
        Assert.IsTrue(draft.Applications.Contains(excluded));
        excluded.IsIncluded = true;
        excluded.CommandLineArguments = "--retry";
        int attempts = 0;

        Assert.IsTrue(MainViewModel.TryPersistNewProject(draft, workspaces, candidates =>
        {
            attempts++;
            Assert.AreEqual(1, candidates.Count);
            Assert.AreSame(draft, candidates[0]);
            Assert.AreEqual(2, candidates[0].Applications.Count);
            Assert.IsTrue(candidates[0].Applications[1].IsIncluded);
            Assert.AreEqual("--retry", candidates[0].Applications[1].CommandLineArguments);
            return true;
        }));

        Assert.AreEqual(1, attempts);
        Assert.AreEqual(2, draft.Applications.Count);
        Assert.AreSame(excluded, draft.Applications[1]);
        Assert.AreEqual(0, workspaces.Count);
    }

    [TestMethod]
    public void SuccessfulNewWorkspaceSaveRemovesExcludedApplicationsAfterPersistence()
    {
        var draft = CreateDraft();
        var applications = draft.Applications.ToArray();
        int attempts = 0;

        Assert.IsTrue(MainViewModel.TryPersistNewProject(draft, [], candidates =>
        {
            attempts++;
            Assert.AreEqual(1, candidates.Count);
            Assert.AreSame(draft, candidates[0]);
            CollectionAssert.AreEqual(applications, candidates[0].Applications);
            Assert.IsFalse(candidates[0].Applications[1].IsIncluded);
            return true;
        }));

        Assert.AreEqual(1, attempts);
        CollectionAssert.AreEqual(new[] { applications[0] }, draft.Applications);
    }

    private static Project CreateDraft()
    {
        var project = new Project(new ProjectWrapper
        {
            Id = Guid.NewGuid().ToString("B"),
            Name = "New workspace",
            Applications =
            [
                new ApplicationWrapper { Id = Guid.NewGuid().ToString("B"), Application = "Included", CommandLineArguments = string.Empty },
                new ApplicationWrapper { Id = Guid.NewGuid().ToString("B"), Application = "Excluded", CommandLineArguments = "--edited" },
            ],
            MonitorConfiguration = [],
        });
        project.Applications[1].IsIncluded = false;
        return project;
    }

    [TestMethod]
    public void CachedEditorSavePreservesLatestLaunchTime()
    {
        var io = new IOUtils();
        const string current = """{"workspaces":[{"id":"id","name":"Original","last-launched-time":100}]}""";
        const string incoming = """{"workspaces":[{"id":"id","name":"Edited","last-launched-time":10}]}""";
        io.WriteFile(file, current);
        io.WriteFile(file, incoming);
        var workspace = JsonNode.Parse(File.ReadAllText(file))!["workspaces"]![0]!;
        Assert.AreEqual("Edited", workspace["name"]!.GetValue<string>());
        Assert.AreEqual(100L, workspace["last-launched-time"]!.GetValue<long>());
        Assert.AreEqual(0, Directory.GetFiles(directory, "*.tmp").Length);
    }

    [TestMethod]
    public void InitialSaveUsesExistingWorkspaceShape()
    {
        const string data = """{"workspaces":[{"id":"id","name":"Test","applications":[{"application":"Notepad"}]}]}""";
        new IOUtils().WriteFile(file, data);
        Assert.IsTrue(JsonNode.DeepEquals(JsonNode.Parse(data), JsonNode.Parse(File.ReadAllText(file))));
    }

    [TestMethod]
    public void MalformedCurrentDataIsNotOverwritten()
    {
        File.WriteAllText(file, "{broken");
        try
        {
            new IOUtils().WriteFile(file, """{"workspaces":[]}""");
            Assert.Fail("Malformed existing data must fail the save.");
        }
        catch (System.Text.Json.JsonException)
        {
            Assert.AreEqual("{broken", File.ReadAllText(file));
        }
    }

    [TestMethod]
    public void ReadOnlyDestinationIsNotReportedAsSaved()
    {
        const string data = """{"workspaces":[{"id":"id","name":"Original"}]}""";
        File.WriteAllText(file, data);
        File.SetAttributes(file, FileAttributes.ReadOnly);
        try
        {
            new IOUtils().WriteFile(file, """{"workspaces":[]}""");
            Assert.Fail("A failed replacement must be observable.");
        }
        catch (UnauthorizedAccessException)
        {
            Assert.AreEqual(data, File.ReadAllText(file));
        }
    }
}
