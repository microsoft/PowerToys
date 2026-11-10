// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
    public void ListWriterPreservesLatestLaunchTime()
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
    public async Task ReplacementWaitsForTransientReaderWithoutDeleteSharing()
    {
        const string original = """{"workspaces":[]}""";
        const string updated = """{"workspaces":[{"id":"new","name":"New"}]}""";
        new IOUtils().WriteFile(file, original);
        using var reader = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var started = new ManualResetEventSlim();
        var writer = Task.Run(() =>
        {
            started.Set();
            new IOUtils().WriteFile(file, updated);
        });
        try
        {
            Assert.IsTrue(started.Wait(TimeSpan.FromSeconds(5)));
            var completed = await Task.WhenAny(writer, Task.Delay(200));
            Assert.AreNotSame(writer, completed, "The writer should wait for the short-lived reader.");
        }
        finally
        {
            reader.Dispose();
            await writer.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.IsTrue(JsonNode.DeepEquals(JsonNode.Parse(updated), JsonNode.Parse(File.ReadAllText(file))));
    }

    [TestMethod]
    public void ReplacementWaitForReaderIsBounded()
    {
        const string original = """{"workspaces":[]}""";
        new IOUtils().WriteFile(file, original);
        using var reader = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var timer = Stopwatch.StartNew();
        try
        {
            new IOUtils().WriteFile(file, """{"workspaces":[{"id":"new","name":"New"}]}""");
            Assert.Fail("A persistent sharing conflict must not be reported as saved.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Assert.IsTrue(timer.ElapsedMilliseconds >= 1800, "The bounded replacement retry should be exercised.");
            Assert.IsTrue(timer.ElapsedMilliseconds < 5000);
            Assert.IsTrue(JsonNode.DeepEquals(JsonNode.Parse(original), JsonNode.Parse(File.ReadAllText(file))));
            Assert.AreEqual(0, Directory.GetFiles(directory, "*.tmp").Length);
        }
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
