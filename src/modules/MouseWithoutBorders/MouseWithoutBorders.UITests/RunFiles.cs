// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.MouseWithoutBorders.UITests;

internal static class RunFiles
{
    internal static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static IEnumerable<string> EvidenceFiles(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetExtension(path) is ".json" or ".png"))
        {
            yield return file;
        }

        // Endpoint workers write only this run's filtered/redacted log excerpts here.
        // Do not recurse into recordings, requests, or private recovery directories.
        var logs = Path.Combine(directory, "logs");
        if (Directory.Exists(logs))
        {
            foreach (var file in Directory.EnumerateFiles(logs, "*", SearchOption.TopDirectoryOnly))
            {
                yield return file;
            }
        }
    }

    public static string PersistentResultsRoot(string? testRunDirectory)
    {
        if (string.IsNullOrWhiteSpace(testRunDirectory))
        {
            return Path.Combine(Environment.CurrentDirectory, "TestResults");
        }

        // MSTest removes successful deployment trees, including attached files.
        return Directory.GetParent(Path.GetFullPath(testRunDirectory))?.FullName
            ?? throw new InvalidOperationException("The test deployment directory has no parent for persistent evidence.");
    }

    public static void Write(string path, object value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
        {
            stream.Write(bytes);
            stream.Flush();
        }

        // Unique messages become visible before the writer has finished. A separate
        // marker prevents redirected readers from caching an incomplete first read.
        using var published = new FileStream(path + ".ready", FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
    }

    public static JsonObject Read(string path)
    {
        var timer = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                return JsonNode.Parse(stream) as JsonObject ?? throw new JsonException("Expected a JSON object.");
            }
            catch (Exception error) when ((error is IOException || error is JsonException) && timer.Elapsed < TimeSpan.FromSeconds(3))
            {
                Thread.Sleep(50);
            }
        }
    }

    public static void Wait(Func<bool> condition, TimeSpan timeout, string description, Action? tick = null)
    {
        var timer = Stopwatch.StartNew();
        do
        {
            tick?.Invoke();
            if (condition())
            {
                return;
            }

            Thread.Sleep(200);
        }
        while (timer.Elapsed < timeout);
        throw new TimeoutException(description);
    }
}
