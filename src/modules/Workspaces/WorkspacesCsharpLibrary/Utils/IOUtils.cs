// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using WorkspacesCsharpLibrary.Data;

namespace WorkspacesCsharpLibrary.Utils;

public class IOUtils
{
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
        if (File.Exists(fileName))
        {
            using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var current = JsonNode.Parse(stream)?.AsObject() ?? throw new InvalidDataException("Invalid saved workspace data.");
            PreserveLaunchHistory(current, incoming);
        }

        var serialized = incoming.ToJsonString(WorkspacesJsonOptions.EditorOptions);
        var temporary = fileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(Encoding.UTF8.GetBytes(serialized));
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, fileName, overwrite: true);
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
