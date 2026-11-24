// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
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
    public void EditingOneWorkspacePreservesOtherRowsAndNativeMetadata()
    {
        var document = JsonNode.Parse("""
            { "extension": { "version": 3 }, "workspaces": [
                { "id": "A", "name": "First", "applications": [ { "application-path": "old.exe" } ] },
                { "id": "B", "name": "Second", "last-launched-time": 5, "extra": true,
                  "applications": [ { "id": "app", "application-path": "before.exe", "command-line-arguments": "", "extension": "keep" } ] }
            ] }
            """)!.AsObject();
        var rows = document["workspaces"]!.AsArray();
        var expected = rows[1]!.DeepClone().AsObject();
        var originalView = expected.DeepClone().AsObject();
        originalView.Remove("extra");
        originalView["applications"]![0]!.AsObject().Remove("extension");
        var edited = originalView.DeepClone().AsObject();
        edited["name"] = "Second, edited";
        edited["applications"]![0]!["command-line-arguments"] = "--profile=work";

        // While B is open in the editor, native launchers update both rows, and another
        // writer adds an entry the editor could not deserialize.
        rows[0]!["applications"]![0]!["application-path"] = "first-updated.exe";
        rows[0]!["applications"]![0]!["id"] = Guid.NewGuid().ToString("B");
        rows[0]!["applications"]![0]!["package-full-name"] = "updated-package";
        rows[1]!["last-launched-time"] = 20;
        rows[1]!["applications"]![0]!["application-path"] = "second-updated.exe";
        rows.Insert(0, JsonNode.Parse("""{ "future-format": [1, 2] }"""));
        File.WriteAllText(file, document.ToJsonString());

        IOUtils.WriteWorkspaceChange(file, expected, originalView, edited);

        var saved = JsonNode.Parse(File.ReadAllText(file))!;
        Assert.IsTrue(JsonNode.DeepEquals(document["extension"], saved["extension"]));
        var savedRows = saved["workspaces"]!.AsArray();
        Assert.AreEqual(3, savedRows.Count);
        Assert.IsTrue(JsonNode.DeepEquals(rows[0], savedRows[0]));
        Assert.IsTrue(JsonNode.DeepEquals(rows[1], savedRows[1]));
        Assert.AreEqual("Second, edited", savedRows[2]!["name"]!.GetValue<string>());
        Assert.AreEqual(20L, savedRows[2]!["last-launched-time"]!.GetValue<long>());
        Assert.IsTrue(savedRows[2]!["extra"]!.GetValue<bool>());
        var app = savedRows[2]!["applications"]![0]!;
        Assert.AreEqual("second-updated.exe", app["application-path"]!.GetValue<string>());
        Assert.AreEqual("--profile=work", app["command-line-arguments"]!.GetValue<string>());
        Assert.AreEqual("keep", app["extension"]!.GetValue<string>());
    }

    [TestMethod]
    public void EditedLegacyApplicationKeepsTheIdAssignedByTheLauncher()
    {
        var expected = JsonNode.Parse("""{ "id": "A", "applications": [ { "application-path": "before.exe" } ] }""")!.AsObject();
        var originalView = expected.DeepClone().AsObject();
        originalView["applications"]![0]!["id"] = Guid.NewGuid().ToString("B");
        var edited = originalView.DeepClone().AsObject();
        edited["applications"]![0]!["command-line-arguments"] = "--edited";
        var initialized = expected.DeepClone();
        string nativeId = Guid.NewGuid().ToString("B");
        initialized["applications"]![0]!["id"] = nativeId;
        File.WriteAllText(file, new JsonObject { ["workspaces"] = new JsonArray(initialized) }.ToJsonString());

        var saved = IOUtils.WriteWorkspaceChange(file, expected, originalView, edited);

        Assert.AreEqual(nativeId, saved["applications"]![0]!["id"]!.GetValue<string>());
        Assert.AreEqual("--edited", saved["applications"]![0]!["command-line-arguments"]!.GetValue<string>());
    }

    [TestMethod]
    public void DisplaySortingAndRecaptureKeepExtensionsOnTheCorrectMonitor()
    {
        const string monitors = "monitor-configuration";
        var stored = JsonNode.Parse("""
            { "id": "A", "name": "Original", "applications": [], "monitor-configuration": [
                { "instance-id": "shared", "id": "model", "monitor-number": 2, "dpi": 96, "extension": "right" },
                { "instance-id": "shared", "id": "model", "monitor-number": 1, "dpi": 96, "extension": "left" },
                { "instance-id": "distinct", "id": "model", "monitor-number": 3, "dpi": 96, "extension": "projector" }
            ] }
            """)!.AsObject();
        IOUtils.WriteWorkspaceChange(file, null, null, stored);
        var originalView = stored.DeepClone().AsObject();
        foreach (var monitor in originalView[monitors]!.AsArray())
        {
            monitor!.AsObject().Remove("extension");
        }

        var sorted = originalView.DeepClone().AsObject();
        sorted["name"] = "Renamed";
        var ordered = sorted[monitors]!.AsArray();
        var right = ordered[0];
        ordered.RemoveAt(0);
        ordered.Insert(1, right);
        var renamed = IOUtils.WriteWorkspaceChange(file, stored, originalView, sorted);
        Assert.IsTrue(JsonNode.DeepEquals(stored[monitors], renamed[monitors]), "A name-only save must preserve the raw monitor array, including its order.");

        // The editor model is now sorted differently from the saved array. Recapture changes
        // both identical-model displays and Windows assigns the distinct projector another number.
        var recaptured = sorted.DeepClone().AsObject();
        recaptured[monitors]![0]!["dpi"] = 144;
        recaptured[monitors]![1]!["dpi"] = 192;
        recaptured[monitors]![2]!["monitor-number"] = 4;
        IOUtils.WriteWorkspaceChange(file, renamed, sorted, recaptured);
        var saved = JsonNode.Parse(File.ReadAllText(file))!["workspaces"]![0]![monitors]!.AsArray();
        Assert.AreEqual(1, saved[0]!["monitor-number"]!.GetValue<int>());
        Assert.AreEqual(144, saved[0]!["dpi"]!.GetValue<int>());
        Assert.AreEqual("left", saved[0]!["extension"]!.GetValue<string>());
        Assert.AreEqual(2, saved[1]!["monitor-number"]!.GetValue<int>());
        Assert.AreEqual(192, saved[1]!["dpi"]!.GetValue<int>());
        Assert.AreEqual("right", saved[1]!["extension"]!.GetValue<string>());
        Assert.AreEqual(4, saved[2]!["monitor-number"]!.GetValue<int>());
        Assert.AreEqual("projector", saved[2]!["extension"]!.GetValue<string>());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RecaptureRepairsDuplicateMonitorNumbersWithoutGuessingExtensionOwnership(bool identicalDevices)
    {
        const string monitors = "monitor-configuration";
        var stored = JsonNode.Parse("""
            { "id": "A", "applications": [], "monitor-configuration": [
                { "instance-id": "left", "id": "model", "monitor-number": 1, "dpi": 96, "extension": "one" },
                { "instance-id": "right", "id": "model", "monitor-number": 1, "dpi": 96, "extension": "two" }
            ] }
            """)!.AsObject();
        if (identicalDevices)
        {
            stored[monitors]![1]!["instance-id"] = "left";
        }

        IOUtils.WriteWorkspaceChange(file, null, null, stored);
        var original = stored.DeepClone().AsObject();
        foreach (var row in original[monitors]!.AsArray())
        {
            row!.AsObject().Remove("extension");
        }

        var captured = original.DeepClone().AsObject();
        var ordered = captured[monitors]!.AsArray();
        var left = ordered[0]!;
        ordered.RemoveAt(0);
        ordered.Add(left);
        ordered[0]!["monitor-number"] = 2;
        ordered[0]!["dpi"] = 144;

        IOUtils.WriteWorkspaceChange(file, stored, original, captured);

        var result = JsonNode.Parse(File.ReadAllText(file))!["workspaces"]![0]![monitors]!.AsArray();
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(2, result[0]!["monitor-number"]!.GetValue<int>());
        Assert.AreEqual(144, result[0]!["dpi"]!.GetValue<int>());
        Assert.AreEqual(1, result[1]!["monitor-number"]!.GetValue<int>());
        if (identicalDevices)
        {
            Assert.IsFalse(result[0]!.AsObject().ContainsKey("extension"));
            Assert.IsFalse(result[1]!.AsObject().ContainsKey("extension"));
        }
        else
        {
            Assert.AreEqual("two", result[0]!["extension"]!.GetValue<string>());
            Assert.AreEqual("one", result[1]!["extension"]!.GetValue<string>());
        }
    }

    [TestMethod]
    public void SavingAnOtherwiseUnchangedRowPersistsRepairedLegacyIds()
    {
        var duplicate = Guid.NewGuid().ToString("D");
        var apps = new JsonArray(new JsonObject { ["id"] = duplicate }, new JsonObject { ["id"] = duplicate });
        var expected = new JsonObject { ["id"] = "A", ["applications"] = apps };
        var repaired = expected.DeepClone().AsObject();
        repaired["applications"]![0]!["id"] = Guid.Parse(duplicate).ToString("B");
        repaired["applications"]![1]!["id"] = Guid.NewGuid().ToString("B");
        IOUtils.WriteWorkspaceChange(file, null, null, expected);

        IOUtils.WriteWorkspaceChange(file, expected, repaired, repaired);

        Assert.IsTrue(JsonNode.DeepEquals(repaired, JsonNode.Parse(File.ReadAllText(file))!["workspaces"]![0]));
    }

    [TestMethod]
    public void StaleUpdatesAndDeletesNeverOverwriteAnotherEditorsChanges()
    {
        var expected = JsonNode.Parse("""{ "id": "A", "name": "Original", "applications": [] }""")!.AsObject();
        var edited = expected.DeepClone().AsObject();
        edited["name"] = "My edit";
        var current = expected.DeepClone().AsObject();
        current["name"] = "Someone else's edit";
        IOUtils.WriteWorkspaceChange(file, null, null, current);
        var before = File.ReadAllBytes(file);

        Assert.ThrowsExactly<WorkspaceConflictException>(() => IOUtils.WriteWorkspaceChange(file, expected, expected, edited));
        Assert.ThrowsExactly<WorkspaceConflictException>(() => IOUtils.WriteWorkspaceChange(file, expected, expected, null));
        Assert.ThrowsExactly<WorkspaceConflictException>(() => IOUtils.WriteWorkspaceChange(file, null, null, edited));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(file));

        IOUtils.WriteWorkspaceChange(file, current, current, null);
        Assert.ThrowsExactly<WorkspaceConflictException>(() => IOUtils.WriteWorkspaceChange(file, expected, expected, edited));
        Assert.IsEmpty(JsonNode.Parse(File.ReadAllText(file))!["workspaces"]!.AsArray());
    }

    [TestMethod]
    public void IncompatibleNativeAndEditorApplicationPathChangesAreRecoverableConflicts()
    {
        var original = JsonNode.Parse("""{ "id":"A", "applications":[{ "id":"app", "application-path":"old.exe" }] }""")!.AsObject();
        var edited = original.DeepClone().AsObject();
        var updated = original.DeepClone().AsObject();
        updated["applications"]![0]!["application-path"] = "native.exe";
        edited["applications"]![0]!["application-path"] = "captured.exe";
        IOUtils.WriteWorkspaceChange(file, null, null, updated);
        var savedBytes = File.ReadAllBytes(file);

        Assert.ThrowsExactly<WorkspaceConflictException>(() => IOUtils.WriteWorkspaceChange(file, original, original, edited));
        CollectionAssert.AreEqual(savedBytes, File.ReadAllBytes(file));
    }

    [TestMethod]
    public void ConcurrentChangesToDifferentWorkspacesBothSurvive()
    {
        var first = JsonNode.Parse("""{ "id": "A", "name": "First", "applications": [] }""")!.AsObject();
        var second = JsonNode.Parse("""{ "id": "B", "name": "Second", "applications": [] }""")!.AsObject();
        IOUtils.WriteWorkspaceChange(file, null, null, first);
        IOUtils.WriteWorkspaceChange(file, null, null, second);
        var renamed = second.DeepClone().AsObject();
        renamed["name"] = "Renamed";
        Parallel.Invoke(
            () => IOUtils.WriteWorkspaceChange(file, first, first, null),
            () => IOUtils.WriteWorkspaceChange(file, second, second, renamed));

        var rows = JsonNode.Parse(File.ReadAllText(file))!["workspaces"]!.AsArray();
        Assert.AreEqual(1, rows.Count);
        Assert.IsTrue(JsonNode.DeepEquals(renamed, rows[0]));
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
            using var delayCts = new CancellationTokenSource();
            var completed = await Task.WhenAny(writer, Task.Delay(200, delayCts.Token));
            delayCts.Cancel();
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
    public void ReadRetriesOnlyAfterAnExclusiveHandleBlocksTheExistingFile()
    {
        File.WriteAllText(file, """{ "workspaces": [ { "id":"A" } ] }""");
        using var owner = File.Open(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        int waits = 0;
        var data = IOUtils.ReadWorkspaceFile(file, _ =>
        {
            waits++;
            owner.Dispose();
        });

        Assert.AreEqual(1, waits);
        Assert.AreEqual("A", data["workspaces"]![0]!["id"]!.GetValue<string>());
    }

    [TestMethod]
    public void ReadPropagatesPersistentContentionAfterTheBoundedRetry()
    {
        File.WriteAllText(file, """{ "workspaces":[] }""");
        using var owner = File.Open(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        int waits = 0;

        var error = Assert.ThrowsExactly<IOException>(() => IOUtils.ReadWorkspaceFile(file, _ => waits++));

        Assert.AreEqual(32, error.HResult & 0xFFFF);
        Assert.AreEqual(9, waits);
    }

    [TestMethod]
    public void ReadDistinguishesMissingFileAndMalformedJsonWithoutWaiting()
    {
        int waits = 0;
        Assert.IsNull(IOUtils.ReadWorkspaceFile(file, _ => waits++));
        File.WriteAllText(file, "{invalid");
        Assert.Throws<JsonException>(() => IOUtils.ReadWorkspaceFile(file, _ => waits++));
        Assert.AreEqual(0, waits);
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
