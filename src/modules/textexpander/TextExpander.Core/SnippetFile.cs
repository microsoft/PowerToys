// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.ObjectModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.TextExpander
{
    /// <summary>
    /// The snippets file: a JSON object whose "snippets" list has a "trigger" and a "replacement" for each snippet, in
    /// order. A replacement is the exact text, with real line breaks and tabs. The engine builds its runtime map from
    /// these same entries, so editing and expansion read the file the same way.
    /// </summary>
    public sealed partial class SnippetFile
    {
        /// <summary>
        /// The newest format version this build understands, and the one it writes. A file with a later version still reads,
        /// but the editor doesn't save over it: see <see cref="Version"/>.
        /// </summary>
        public const int FormatVersion = 1;

        // People read and edit the file, so reading allows the comments and trailing commas a hand edit may add, and
        // writing keeps letters, symbols and HTML characters as they are rather than as \u escapes.
        private static readonly SnippetFileJsonContext Json = new(new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });

        private readonly List<Entry> _entries = new();
        private ReadOnlyCollection<Entry>? _snippets;
        private int _nextId;

        // The file's properties that this version doesn't know, written back as they were.
        private Dictionary<string, JsonElement>? _extra;

        /// <summary>One snippet.</summary>
        public sealed class Entry
        {
            /// <summary>
            /// Gets the stable handle for this entry, unique within its <see cref="SnippetFile"/>. Entries aren't addressed
            /// by trigger because the engine's map is last-one-wins and a file may hold duplicate triggers.
            /// </summary>
            public int Id { get; internal init; }

            public required string Trigger { get; set; }

            public required string Replacement { get; set; }

            /// <summary>Gets the snippet's properties that this version doesn't know, written back as they were.</summary>
            internal Dictionary<string, JsonElement>? Extra { get; init; }
        }

        /// <summary>
        /// Gets the file's "version": <see cref="FormatVersion"/> for a new file or one without it. A later one is from a newer
        /// PowerToys, which may keep what this version can't, so the editor doesn't save over it.
        /// </summary>
        public int Version { get; private init; } = FormatVersion;

        /// <summary>
        /// Gets the snippets in file order, as they are now: the list follows later changes.
        /// Duplicate triggers are kept; see <see cref="FindDuplicate"/>.
        /// </summary>
        public IReadOnlyList<Entry> Snippets => _snippets ??= _entries.AsReadOnly();

        /// <summary>
        /// Reads a snippets file. A UTF-8 byte order mark, comments and trailing commas are allowed. The version and the
        /// properties this version doesn't know are kept for <see cref="ToBytes"/>, and a snippet without a trigger or a
        /// replacement gets an empty one. The version must be a whole number: "1" or 1.0 is a mistake, like any other.
        /// </summary>
        /// <exception cref="SnippetFileFormatException">The bytes aren't JSON, or have no "snippets" list.</exception>
        public static SnippetFile Read(ReadOnlySpan<byte> utf8Json)
        {
            // Notepad and other editors may save one.
            if (utf8Json.StartsWith("\uFEFF"u8))
            {
                utf8Json = utf8Json[3..];
            }

            FileJson? json;
            try
            {
                json = JsonSerializer.Deserialize(utf8Json, Json.FileJson);
            }
            catch (JsonException ex)
            {
                throw new SnippetFileFormatException(ex.LineNumber + 1, ex);
            }

            if (json?.Snippets is null)
            {
                throw new SnippetFileFormatException(lineNumber: null, innerException: null);
            }

            var file = new SnippetFile { Version = json.Version ?? FormatVersion, _extra = json.Extra };
            foreach (EntryJson? entry in json.Snippets)
            {
                if (entry is not null)
                {
                    file.Add(entry.Trigger ?? string.Empty, entry.Replacement ?? string.Empty, entry.Extra);
                }
            }

            return file;
        }

        /// <summary>
        /// Gets the file's bytes: indented JSON in UTF-8, without a byte order mark. What this version doesn't know is
        /// written back as it was read, after what it knows; comments and the file's own layout aren't kept.
        /// </summary>
        /// <exception cref="SnippetFileWriteException">
        /// What this version kept without reading can't be written back, such as half of an emoji.
        /// </exception>
        public byte[] ToBytes()
        {
            var snippets = new List<EntryJson?>(_entries.Count);
            foreach (Entry entry in _entries)
            {
                snippets.Add(new EntryJson { Trigger = entry.Trigger, Replacement = entry.Replacement, Extra = entry.Extra });
            }

            var json = new FileJson { Version = Math.Max(Version, FormatVersion), Snippets = snippets, Extra = _extra };
            try
            {
                return JsonSerializer.SerializeToUtf8Bytes(json, Json.FileJson);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                throw new SnippetFileWriteException(ex);
            }
        }

        public SnippetFile Clone()
        {
            var clone = new SnippetFile { Version = Version, _extra = _extra, _nextId = _nextId };
            foreach (Entry entry in _entries)
            {
                clone._entries.Add(new Entry { Id = entry.Id, Trigger = entry.Trigger, Replacement = entry.Replacement, Extra = entry.Extra });
            }

            return clone;
        }

        /// <summary>
        /// Adds a snippet, or updates the entry with <paramref name="id"/> (null adds). An id that is no longer
        /// present throws: the caller holds a stale row, and appending would create a second snippet.
        /// </summary>
        public Entry Upsert(int? id, string trigger, string replacement, bool caseSensitive = true)
        {
            ArgumentNullException.ThrowIfNull(replacement);

            if (!IsValidTrigger(trigger, out SnippetProblem problem))
            {
                throw new ArgumentException(problem.ToString(), nameof(trigger));
            }

            Entry? existing = null;
            if (id is not null)
            {
                existing = _entries.Find(entry => entry.Id == id.Value)
                    ?? throw new InvalidOperationException($"Snippet {id.Value} is no longer in this file.");
            }

            if (FindDuplicate(trigger, id, caseSensitive) is not null)
            {
                throw new InvalidOperationException($"Another snippet already uses '{trigger}'.");
            }

            // Kept with \n line breaks, the ones the editor reports.
            replacement = NormaliseNewlines(replacement);
            if (existing is null)
            {
                return Add(trigger, replacement);
            }

            existing.Trigger = trigger;
            existing.Replacement = replacement;
            return existing;
        }

        /// <summary>Removes the entry with <paramref name="id"/>. Returns true when one went.</summary>
        public bool Remove(int id) => _entries.RemoveAll(entry => entry.Id == id) > 0;

        /// <summary>
        /// The entry that would shadow <paramref name="trigger"/> (the engine's map is last-one-wins), excluding
        /// the entry being edited by id, not by trigger, so existing duplicates are still reported.
        /// </summary>
        public Entry? FindDuplicate(string trigger, int? ignoringId = null, bool caseSensitive = true)
        {
            StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            return _entries.Find(entry => entry.Id != ignoringId && string.Equals(entry.Trigger, trigger, comparison));
        }

        /// <summary>
        /// Gets a value indicating whether <paramref name="trigger"/> can be typed: it isn't empty, and has no line break,
        /// no hidden first character and no space at either end.
        /// </summary>
        public static bool IsValidTrigger(string trigger, out SnippetProblem problem)
        {
            if (string.IsNullOrWhiteSpace(trigger))
            {
                problem = SnippetProblem.EmptyTrigger;
                return false;
            }

            if (trigger.AsSpan().ContainsAny('\r', '\n'))
            {
                problem = SnippetProblem.TriggerHasLineBreak;
                return false;
            }

            // A U+FEFF doesn't show, so the trigger looks like one that can be typed. Usually arrives by pasting from another editor.
            if (trigger[0] == '\uFEFF')
            {
                problem = SnippetProblem.TriggerStartsWithHiddenCharacter;
                return false;
            }

            if (!string.Equals(trigger, trigger.Trim(), StringComparison.Ordinal))
            {
                problem = SnippetProblem.TriggerHasOuterSpace;
                return false;
            }

            problem = SnippetProblem.None;
            return true;
        }

        /// <summary>Collapses CRLF and lone CR to <c>\n</c>.</summary>
        internal static string NormaliseNewlines(string text)
            => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

        private Entry Add(string trigger, string replacement, Dictionary<string, JsonElement>? extra = null)
        {
            var entry = new Entry { Id = _nextId++, Trigger = trigger, Replacement = replacement, Extra = extra };
            _entries.Add(entry);
            return entry;
        }

        internal sealed class FileJson
        {
            // A whole number. Anything else is a mistake in the file, reported with its line like any other.
            [JsonPropertyName("version")]
            public int? Version { get; set; }

            [JsonPropertyName("snippets")]
            public List<EntryJson?>? Snippets { get; set; }

            [JsonExtensionData]
            public Dictionary<string, JsonElement>? Extra { get; set; }
        }

        internal sealed class EntryJson
        {
            [JsonPropertyName("trigger")]
            public string? Trigger { get; set; }

            [JsonPropertyName("replacement")]
            public string? Replacement { get; set; }

            [JsonExtensionData]
            public Dictionary<string, JsonElement>? Extra { get; set; }
        }

        [JsonSerializable(typeof(FileJson))]
        internal sealed partial class SnippetFileJsonContext : JsonSerializerContext
        {
        }
    }
}
