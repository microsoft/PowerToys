// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;

namespace Microsoft.PowerToys.TextExpander.UITests;

/// <summary>
/// The Text Expander data folder, %LOCALAPPDATA%\Microsoft\PowerToys\TextExpander: the module's settings.json and the
/// default snippets database. The tests seed both before PowerToys starts and restore what was there afterwards.
/// </summary>
internal static class TextExpanderFiles
{
    public const string ModuleKey = "TextExpander";

    public const string SnippetsFileName = "PowerToysTextExpanderSnippets.json";

    // The editor writes the database with these options, so a seeded file is one it would write the same way.
    private static readonly JsonSerializerOptions DatabaseWriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonDocumentOptions LenientReadOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public static string Folder => Path.Combine(SettingsConfigHelper.PowerToysSettingsRoot, ModuleKey);

    public static string SettingsPath => Path.Combine(Folder, "settings.json");

    public static string SnippetsPath => Path.Combine(Folder, SnippetsFileName);

    /// <summary>Writes the module's settings.json in the shape Settings writes it.</summary>
    public static void WriteOptions(TextExpanderOptions options)
    {
        var settings = new JsonObject
        {
            ["properties"] = new JsonObject
            {
                ["snippets_path"] = new JsonObject { ["value"] = options.SnippetsPath },
                ["injection_backend"] = new JsonObject { ["value"] = options.InjectionBackend },
                ["clipboard_threshold_chars"] = new JsonObject { ["value"] = options.ClipboardThresholdChars },
                ["treat_remaps_as_typing"] = options.TreatRemapsAsTyping,
                ["case_sensitive"] = options.CaseSensitive,
                ["require_word_boundary"] = options.RequireWordBoundary,
                ["word_boundary_keeps_space"] = options.WordBoundaryKeepsSpace,
            },
            ["name"] = ModuleKey,
            ["version"] = "0.0.1",
        };

        WriteAtomically(SettingsPath, Encoding.UTF8.GetBytes(settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true })));
    }

    /// <summary>Reads a bool option, which settings.json stores bare.</summary>
    public static bool? ReadBoolOption(string name) =>
        ReadOption(name) is JsonValue value && value.TryGetValue(out bool result) ? result : null;

    /// <summary>Reads a string option, which settings.json stores as <c>{"value": "..."}</c>.</summary>
    public static string? ReadStringOption(string name) =>
        ReadOption(name) is JsonValue value && value.TryGetValue(out string? result) ? result : null;

    /// <summary>Writes the default snippets database the way the editor writes it: version 1, then the snippets in order.</summary>
    public static void WriteSnippets(IEnumerable<Snippet> snippets) => WriteSnippets(SnippetsPath, snippets);

    public static void WriteSnippets(string path, IEnumerable<Snippet> snippets)
    {
        var list = new JsonArray();
        foreach (var snippet in snippets)
        {
            list.Add(new JsonObject { ["trigger"] = snippet.Trigger, ["replacement"] = snippet.Replacement });
        }

        var file = new JsonObject { ["version"] = 1, ["snippets"] = list };
        WriteAtomically(path, Encoding.UTF8.GetBytes(file.ToJsonString(DatabaseWriteOptions)));
    }

    /// <summary>Reads the default snippets database, or null when there is none.</summary>
    public static IReadOnlyList<Snippet>? ReadSnippets()
    {
        var bytes = ReadAllBytesShared(SnippetsPath);
        if (bytes is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(WithoutByteOrderMark(bytes), LenientReadOptions);
        var snippets = new List<Snippet>();
        foreach (var entry in document.RootElement.GetProperty("snippets").EnumerateArray())
        {
            snippets.Add(new Snippet(
                entry.TryGetProperty("trigger", out var trigger) ? trigger.GetString() ?? string.Empty : string.Empty,
                entry.TryGetProperty("replacement", out var replacement) ? replacement.GetString() ?? string.Empty : string.Empty));
        }

        return snippets;
    }

    public static void DeleteSnippets()
    {
        if (File.Exists(SnippetsPath))
        {
            File.Delete(SnippetsPath);
        }
    }

    /// <summary>Formats snippets for a failure message.</summary>
    public static string Describe(IReadOnlyList<Snippet>? snippets) =>
        snippets is null
            ? "<no database>"
            : string.Join(", ", snippets.Select(snippet => $"{snippet.Trigger} => {Visible(snippet.Replacement)}"));

    /// <summary>Shows line breaks and tabs in a failure message.</summary>
    public static string Visible(string? text) =>
        text is null ? "<null>" : text.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal).Replace("\t", "\\t", StringComparison.Ordinal);

    /// <summary>Publishes the whole file at once, as PowerToys does, so no watcher reads it half-written.</summary>
    public static void WriteAtomically(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var staging = $"{path}.uitest-{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(staging, bytes);
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    File.Move(staging, path, overwrite: true);
                    return;
                }
                catch (Exception ex) when (attempt < 50 && ex is IOException or UnauthorizedAccessException)
                {
                    // A reader or a watcher can hold the destination for a moment.
                    Thread.Sleep(100);
                }
            }
        }
        finally
        {
            if (File.Exists(staging))
            {
                File.Delete(staging);
            }
        }
    }

    internal static byte[]? ReadAllBytesShared(string path)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                return copy.ToArray();
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (Exception ex) when (attempt < 50 && ex is IOException or UnauthorizedAccessException)
            {
                // Settings and the editor replace these files atomically; the destination can be briefly unavailable.
                Thread.Sleep(100);
            }
        }
    }

    private static JsonNode? ReadOption(string name)
    {
        var bytes = ReadAllBytesShared(SettingsPath);
        if (bytes is null)
        {
            return null;
        }

        var root = JsonNode.Parse(WithoutByteOrderMark(bytes).Span, documentOptions: LenientReadOptions);
        var option = root?["properties"]?[name];
        return option is JsonObject wrapped && wrapped.ContainsKey("value") ? wrapped["value"] : option;
    }

    private static ReadOnlyMemory<byte> WithoutByteOrderMark(byte[] bytes) =>
        bytes.AsSpan().StartsWith("\uFEFF"u8) ? bytes.AsMemory(3) : bytes.AsMemory();
}
