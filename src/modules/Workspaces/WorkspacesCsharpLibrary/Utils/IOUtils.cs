// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using WorkspacesCsharpLibrary.Data;

namespace WorkspacesCsharpLibrary.Utils;

public class IOUtils
{
    private static readonly string[] LauncherMetadataFields = ["application-path", "package-full-name"];
    private readonly IFileSystem _fileSystem = new FileSystem();

    public void WriteFile(string fileName, string data)
    {
        if (!string.Equals(Path.GetFileName(fileName), "workspaces.json", StringComparison.OrdinalIgnoreCase))
        {
            _fileSystem.File.WriteAllText(fileName, data);
            return;
        }

        using var transaction = AcquireWorkspaceLock(fileName + ".lock");
        var incoming = JsonNode.Parse(data)?.AsObject() ?? throw new InvalidDataException("Invalid workspace data.");
        var current = ReadWorkspaceFile(fileName);
        if (current != null)
        {
            PreserveLaunchHistory(current, incoming);
        }

        Commit(fileName, incoming);
    }

    public static JsonObject ReadWorkspaceFile(string fileName)
    {
        try
        {
            using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return JsonNode.Parse(stream)?.AsObject() ?? throw new InvalidDataException("Invalid saved workspace data.");
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    // expected is the raw row originally read from disk. originalView also contains defaults
    // and IDs generated for legacy apps by the editor. A null expected adds, a null edited deletes.
    public static JsonObject WriteWorkspaceChange(string fileName, JsonObject expected, JsonObject originalView, JsonObject edited)
    {
        var id = (expected ?? edited)?["id"]?.GetValue<string>();
        if (string.IsNullOrEmpty(id) || (edited != null && !SameId(id, edited["id"]?.GetValue<string>())))
        {
            throw new InvalidDataException("Invalid workspace change.");
        }

        if (expected == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(fileName)));
        }

        using var transaction = AcquireWorkspaceLock(fileName + ".lock");
        var document = ReadWorkspaceFile(fileName) ?? new JsonObject { ["workspaces"] = new JsonArray() };
        var rows = document["workspaces"]?.AsArray() ?? throw new InvalidDataException("Missing saved workspace list.");
        int index = -1;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i] is JsonObject row && row["id"] is JsonValue value &&
                value.TryGetValue<string>(out string rowId) && SameId(rowId, id))
            {
                if (index >= 0)
                {
                    throw new InvalidDataException("Workspace identity is ambiguous.");
                }

                index = i;
            }
        }

        if (expected == null && index >= 0)
        {
            throw new IOException("A workspace with this ID already exists.");
        }

        if (expected != null && index < 0)
        {
            throw new IOException("The workspace no longer exists.");
        }

        JsonObject saved = null;
        if (index < 0)
        {
            saved = edited.DeepClone().AsObject();
            rows.Add((JsonNode)saved);
        }
        else
        {
            var current = rows[index].AsObject();
            CheckForConcurrentEdits(expected, current);
            if (edited == null)
            {
                rows.RemoveAt(index);
            }
            else
            {
                ArgumentNullException.ThrowIfNull(originalView);
                saved = MergeWorkspace(expected, originalView, edited, current);
                rows[index] = saved;
            }
        }

        Commit(fileName, document);
        return saved;
    }

    private static bool SameId(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase) ||
        (Guid.TryParse(left, out var first) && Guid.TryParse(right, out var second) && first == second);

    private static void CheckForConcurrentEdits(JsonObject expected, JsonObject current)
    {
        // The native launcher can initialize missing IDs, resolve installation paths and record
        // launch history without changing the editable workspace. Any other change requires a reload.
        var comparable = current.DeepClone().AsObject();
        CopyProperty(expected, comparable, "id");
        CopyProperty(expected, comparable, "last-launched-time");
        if (expected["applications"] is JsonArray before && comparable["applications"] is JsonArray after && before.Count == after.Count)
        {
            for (int i = 0; i < before.Count; i++)
            {
                var previous = before[i].AsObject();
                var latest = after[i].AsObject();
                string previousId = previous["id"]?.GetValue<string>();
                string latestId = latest["id"]?.GetValue<string>();
                if (SameId(previousId, latestId) || (string.IsNullOrEmpty(previousId) && Guid.TryParse(latestId, out _)))
                {
                    CopyProperty(previous, latest, "id");
                }

                foreach (string field in LauncherMetadataFields)
                {
                    if (latest[field] is JsonNode metadata && (metadata is not JsonValue text || !text.TryGetValue<string>(out _)))
                    {
                        throw new InvalidDataException("Invalid stored application metadata.");
                    }

                    CopyProperty(previous, latest, field);
                }
            }
        }

        if (!JsonNode.DeepEquals(expected, comparable))
        {
            throw new IOException("The workspace changed since it was opened.");
        }
    }

    private static JsonObject MergeWorkspace(JsonObject expected, JsonObject originalView, JsonObject edited, JsonObject current)
    {
        var result = current.DeepClone().AsObject();
        foreach (var (key, value) in edited)
        {
            if (key is not ("applications" or "monitor-configuration" or "id" or "last-launched-time") && !JsonNode.DeepEquals(originalView[key], value))
            {
                CopyEditedProperty(originalView, edited, result, key);
            }
        }

        var apps = MergeRows("applications", expected, originalView, edited, current, CanonicalAppId);
        if (apps.Select(CanonicalAppId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != apps.Count)
        {
            throw new IOException("An application ID now belongs to more than one workspace entry.");
        }

        result["applications"] = apps;
        const string monitors = "monitor-configuration";
        if (originalView[monitors] is JsonArray before && edited[monitors] is JsonArray after &&
            !(before.Count == after.Count && before.OrderBy(MonitorNumber).Zip(after.OrderBy(MonitorNumber), JsonNode.DeepEquals).All(equal => equal)))
        {
            // The editor orders monitors by their desktop coordinates. Keep the original raw
            // array on ordinary saves; only a changed topology needs to merge monitor rows.
            var storedIndices = expected[monitors].AsArray().Select((row, index) => (Number: MonitorNumber(row), Index: index))
                .ToDictionary(item => item.Number, item => item.Index);
            result[monitors] = MergeRows(monitors, expected, originalView, edited, current, MonitorIdentity(before, after), storedIndices);
        }

        return result;
    }

    private static JsonArray MergeRows(string field, JsonObject expected, JsonObject originalView, JsonObject edited, JsonObject current, Func<JsonNode, string> identity, Dictionary<int, int> storedMonitorIndices = null)
    {
        var before = originalView[field].AsArray();
        var indices = before.Select((row, index) => (Id: identity(row), Index: index))
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => new Queue<int>(group.Select(item => item.Index)), StringComparer.OrdinalIgnoreCase);
        var rows = new JsonArray();
        foreach (var row in edited[field].AsArray())
        {
            if (!indices.TryGetValue(identity(row), out var matches) || !matches.TryDequeue(out int index))
            {
                rows.Add(row.DeepClone());
                continue;
            }

            var updated = row.AsObject();
            var original = before[index].AsObject();
            int storedIndex = storedMonitorIndices == null ? index : storedMonitorIndices[MonitorNumber(original)];
            var stored = expected[field][storedIndex];
            var latest = current[field][storedIndex].AsObject();
            var merged = latest.DeepClone().AsObject();
            foreach (var (key, value) in updated)
            {
                if (field == "applications" && key == "id")
                {
                    if (SameId(stored["id"]?.GetValue<string>(), latest["id"]?.GetValue<string>()))
                    {
                        // Persist a repaired legacy ID, unless the launcher assigned one first.
                        CopyProperty(updated, merged, key);
                    }
                }
                else if (!JsonNode.DeepEquals(original[key], value))
                {
                    if (!JsonNode.DeepEquals(stored[key], latest[key]) && !JsonNode.DeepEquals(value, latest[key]))
                    {
                        throw new IOException("Application metadata changed since the workspace was opened.");
                    }

                    CopyEditedProperty(original, updated, merged, key);
                }
            }

            rows.Add((JsonNode)merged);
        }

        return rows;
    }

    private static string CanonicalAppId(JsonNode app)
    {
        var id = app["id"]?.GetValue<string>();
        return Guid.TryParse(id, out var guid) ? guid.ToString("D") : id ?? string.Empty;
    }

    private static int MonitorNumber(JsonNode monitor) => monitor["monitor-number"]?.GetValue<int>() ?? 0;

    private static Func<JsonNode, string> MonitorIdentity(JsonArray before, JsonArray after)
    {
        static string Instance(JsonNode row) => row["instance-id"]?.GetValue<string>() ?? string.Empty;
        static string Device(JsonNode row) => Instance(row) + "\0" + row["id"]?.GetValue<string>();

        HashSet<string> AmbiguousKeys(Func<JsonNode, string> key) => new[] { before, after }
            .SelectMany(rows => rows.GroupBy(key, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var repeatedInstances = AmbiguousKeys(Instance);
        var repeatedDevices = AmbiguousKeys(Device);
        return row =>
        {
            string instance = Instance(row);
            if (instance.Length > 0 && !repeatedInstances.Contains(instance))
            {
                return instance;
            }

            string device = Device(row);

            // Identical devices without distinct stable IDs can be distinguished only by their numbers.
            return device.Length > 1 && !repeatedDevices.Contains(device) ? device : device + "\0" + MonitorNumber(row);
        };
    }

    private static void CopyEditedProperty(JsonObject before, JsonObject after, JsonObject destination, string key)
    {
        if (before[key] is JsonObject previous && after[key] is JsonObject edited && destination[key] is JsonObject nested)
        {
            foreach (var (name, value) in edited)
            {
                if (!JsonNode.DeepEquals(previous[name], value))
                {
                    CopyEditedProperty(previous, edited, nested, name);
                }
            }
        }
        else
        {
            CopyProperty(after, destination, key);
        }
    }

    private static void CopyProperty(JsonObject source, JsonObject destination, string key)
    {
        if (source.TryGetPropertyValue(key, out var value))
        {
            destination[key] = value?.DeepClone();
        }
        else
        {
            destination.Remove(key);
        }
    }

    private static void Commit(string fileName, JsonObject incoming)
    {
        var serialized = incoming.ToJsonString(WorkspacesJsonOptions.EditorOptions);
        var temporary = fileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(Encoding.UTF8.GetBytes(serialized));
                stream.Flush(flushToDisk: true);
            }

            ReplaceWorkspaceFile(temporary, fileName);
            using var verification = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (!JsonNode.DeepEquals(incoming, JsonNode.Parse(verification)))
            {
                throw new IOException("Workspace write could not be verified.");
            }
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static void ReplaceWorkspaceFile(string temporary, string fileName)
    {
        var timer = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                File.Move(temporary, fileName, overwrite: true);
                return;
            }
            catch (IOException ex) when ((ex.HResult & 0xFFFF) is 32 or 33 && timer.ElapsedMilliseconds < 2000)
            {
                Thread.Sleep(20);
            }
            catch (UnauthorizedAccessException) when (timer.ElapsedMilliseconds < 2000)
            {
                // Windows can report an open destination without delete sharing as access denied.
                Thread.Sleep(20);
            }
        }
    }

    private static FileStream AcquireWorkspaceLock(string fileName)
    {
        var timer = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                return new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException ex) when ((ex.HResult & 0xFFFF) == 32 && timer.ElapsedMilliseconds < 2000)
            {
                Thread.Sleep(20);
            }
        }
    }

    private static void PreserveLaunchHistory(JsonObject current, JsonObject incoming)
    {
        var existing = current["workspaces"]?.AsArray() ?? throw new InvalidDataException("Missing saved workspace list.");
        var updated = incoming["workspaces"]?.AsArray() ?? throw new InvalidDataException("Missing workspace list.");
        foreach (var item in updated)
        {
            var workspace = item?.AsObject() ?? throw new InvalidDataException("Invalid workspace entry.");
            var id = workspace["id"]?.GetValue<string>();
            if (string.IsNullOrEmpty(id))
            {
                throw new InvalidDataException("Workspace ID is missing.");
            }

            foreach (var previous in existing)
            {
                if (string.Equals(id, previous?["id"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase) &&
                    previous?["last-launched-time"] is JsonNode lastLaunched)
                {
                    var timestamp = lastLaunched.GetValue<long>();
                    if (workspace["last-launched-time"] is not JsonNode incomingTime || timestamp > incomingTime.GetValue<long>())
                    {
                        workspace["last-launched-time"] = lastLaunched.DeepClone();
                    }

                    break;
                }
            }
        }
    }

    public string ReadFile(string fileName)
    {
        if (_fileSystem.File.Exists(fileName))
        {
            int attempts = 0;
            while (attempts < 10)
            {
                try
                {
                    using FileSystemStream inputStream = _fileSystem.File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using StreamReader reader = new(inputStream);
                    string data = reader.ReadToEnd();
                    inputStream.Close();
                    return data;
                }
                catch (Exception)
                {
                    Task.Delay(10).Wait();
                }

                attempts++;
            }
        }

        return string.Empty;
    }
}
