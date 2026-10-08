// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesCsharpLibrary.Data;
using WorkspacesCsharpLibrary.Utils;

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
