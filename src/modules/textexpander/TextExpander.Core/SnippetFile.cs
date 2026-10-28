// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Microsoft.PowerToys.TextExpander
{
    /// <summary>
    /// An editable view over the snippet file that keeps comments, blank lines, ordering and untouched entries'
    /// original lines, rewriting only the entries that were edited. The engine builds its runtime map from
    /// these same entries, so editing and expansion share one grammar.
    /// </summary>
    public sealed class SnippetFile
    {
        /// <summary>A line this file does not own: a comment, a blank, or unparseable text.</summary>
        private sealed class RawLine
        {
            public RawLine(string text) => Text = text;

            public string Text { get; }
        }

        /// <summary>
        /// One snippet, together with the lines it came from (<see cref="SourceLines"/>), so an untouched entry
        /// re-emits its exact original lines. Encoding and line endings are up to whoever writes <see cref="ToLines"/>.
        /// </summary>
        public sealed class Entry
        {
            /// <summary>
            /// Stable handle for this entry, unique within its <see cref="SnippetFile"/>. Entries aren't addressed
            /// by trigger because the engine's map is last-one-wins and a file may hold duplicate triggers.
            /// </summary>
            public int Id { get; internal init; }

            public required string Trigger { get; set; }

            public required string Replacement { get; set; }

            /// <summary>The exact lines this entry was read from, or null once it has been edited.</summary>
            internal string[] SourceLines { get; set; }

            /// <summary>True when this came from a block the file never closed.</summary>
            internal bool UnterminatedBlock { get; init; }

            internal Entry Clone() => new()
            {
                Id = Id,
                Trigger = Trigger,
                Replacement = Replacement,
                SourceLines = SourceLines,
                UnterminatedBlock = UnterminatedBlock,
            };
        }

        // Comments and entries interleaved, in file order, so a comment stays attached to whatever
        // it was written above.
        private readonly List<object> _items = new List<object>();

        // The entries alone, in file order, and by id, kept with _items, so listing or finding an
        // entry doesn't filter every line of the file again.
        private readonly List<Entry> _entries = new List<Entry>();
        private readonly Dictionary<int, Entry> _byId = new Dictionary<int, Entry>();
        private ReadOnlyCollection<Entry> _snippets;
        private int _nextId;

        private SnippetFile()
        {
        }

        /// <summary>
        /// Gets the snippets in file order, as they are now: the list follows later changes.
        /// Duplicate triggers are kept; see <see cref="FindDuplicate"/>.
        /// </summary>
        public IReadOnlyList<Entry> Snippets => _snippets ??= _entries.AsReadOnly();

        public SnippetFile Clone()
        {
            var clone = new SnippetFile { _nextId = _nextId };
            foreach (object item in _items)
            {
                clone.Add(item is Entry entry ? entry.Clone() : item);
            }

            return clone;
        }

        // ── Reading ──────────────────────────────────────────────
        public static SnippetFile Parse(string[] lines)
        {
            ArgumentNullException.ThrowIfNull(lines);

            var file = new SnippetFile();
            int i = 0;

            while (i < lines.Length)
            {
                string line = lines[i];
                string stripped = line.Trim();
                int start = i;
                i++;

                if (stripped.Length == 0 || stripped.StartsWith("//", StringComparison.Ordinal))
                {
                    file.Add(new RawLine(line));
                    continue;
                }

                if (stripped.EndsWith("<<<", StringComparison.Ordinal))
                {
                    string trigger = stripped.Substring(0, stripped.Length - 3);
                    var body = new List<string>();
                    bool closed = false;

                    while (i < lines.Length)
                    {
                        string bodyLine = lines[i];
                        i++;
                        if (bodyLine.Trim() == ">>>")
                        {
                            closed = true;
                            break;
                        }

                        body.Add(bodyLine);
                    }

                    // An unterminated block still parses, with the rest of the file as its body. It is kept so an
                    // unedited file round-trips; ToLines closes it only when something must be written after it.
                    if (trigger.Length > 0)
                    {
                        file.Add(new Entry
                        {
                            Id = file._nextId++,
                            Trigger = trigger,
                            Replacement = string.Join("\n", body),
                            SourceLines = lines[start..i],
                            UnterminatedBlock = !closed,
                        });
                    }
                    else
                    {
                        foreach (string skipped in lines[start..i])
                        {
                                file.Add(new RawLine(skipped));
                        }
                    }

                    continue;
                }

                int eq = stripped.IndexOf('=');
                if (eq > 0)
                {
                    int rawEq = line.IndexOf('=');
                    file.Add(new Entry
                    {
                        Id = file._nextId++,
                        Trigger = stripped.Substring(0, eq),
                        Replacement = line.Substring(rawEq + 1),
                        SourceLines = new[] { line },
                    });
                }
                else
                {
                    file.Add(new RawLine(line));
                }
            }

            return file;
        }

        // ── Editing ──────────────────────────────────────────────
        /// <summary>
        /// Adds a snippet, or updates the entry with <paramref name="id"/> (null adds). An id that is no longer
        /// present throws: the caller holds a stale row, and appending would create a second snippet.
        /// </summary>
        public Entry Upsert(int? id, string trigger, string replacement, bool caseSensitive = true)
        {
            ArgumentException.ThrowIfNullOrEmpty(trigger);
            ArgumentNullException.ThrowIfNull(replacement);

            if (!IsValidTrigger(trigger, out SnippetProblem triggerProblem))
            {
                    throw new ArgumentException(triggerProblem.ToString(), nameof(trigger));
            }

            if (!IsValidReplacement(replacement, out SnippetProblem replacementProblem))
            {
                    throw new ArgumentException(replacementProblem.ToString(), nameof(replacement));
            }

            // Stored normalised so what the model holds is what a save/read cycle returns.
            replacement = NormaliseNewlines(replacement);

            if (id is null)
            {
                if (FindDuplicate(trigger, ignoringId: null, caseSensitive) is not null)
                {
                        throw new InvalidOperationException($"Another snippet already uses '{trigger}'.");
                }

                var added = new Entry { Id = _nextId++, Trigger = trigger, Replacement = replacement };
                Add(added);
                return added;
            }

            if (!_byId.TryGetValue(id.Value, out Entry existing))
            {
                throw new InvalidOperationException($"Snippet {id.Value} is no longer in this file.");
            }

            if (FindDuplicate(trigger, ignoringId: id.Value, caseSensitive) is not null)
            {
                    throw new InvalidOperationException($"Another snippet already uses '{trigger}'.");
            }

            // Dropping SourceLines is what marks the entry dirty, so it is regenerated on save while
            // every untouched entry around it is still emitted verbatim.
            if (!string.Equals(existing.Trigger, trigger, StringComparison.Ordinal)
                || !string.Equals(existing.Replacement, replacement, StringComparison.Ordinal))
            {
                existing.Trigger = trigger;
                existing.Replacement = replacement;
                existing.SourceLines = null;
            }

            return existing;
        }

        /// <summary>Removes the entry with <paramref name="id"/>. Returns true when one went.</summary>
        public bool Remove(int id)
        {
            if (!_byId.Remove(id, out Entry found))
            {
                return false;
            }

            _entries.Remove(found);
            return _items.Remove(found);
        }

        /// <summary>
        /// The entry that would shadow <paramref name="trigger"/> (the engine's map is last-one-wins), excluding
        /// the entry being edited by id, not by trigger, so existing duplicates are still reported.
        /// </summary>
        public Entry FindDuplicate(string trigger, int? ignoringId = null, bool caseSensitive = true)
        {
            StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            foreach (Entry entry in _entries)
            {
                if (ignoringId is not null && entry.Id == ignoringId.Value)
                {
                        continue;
                }

                if (string.Equals(entry.Trigger, trigger, comparison))
                {
                        return entry;
                }
            }

            return null;
        }

        // ── Writing ──────────────────────────────────────────────
        public string[] ToLines()
        {
            var result = new List<string>();

            for (int index = 0; index < _items.Count; index++)
            {
                object item = _items[index];

                if (item is RawLine raw)
                {
                    result.Add(raw.Text);
                    continue;
                }

                var entry = (Entry)item;

                if (entry.SourceLines != null)
                {
                    result.AddRange(entry.SourceLines);

                    // An unterminated block is closed when anything follows it; otherwise the next snippet
                    // would be written inside its body.
                    if (entry.UnterminatedBlock && index < _items.Count - 1)
                    {
                            result.Add(">>>");
                    }

                    continue;
                }

                result.AddRange(Render(entry.Trigger, entry.Replacement));
            }

            return result.ToArray();
        }

        /// <summary>
        /// Renders one entry. Newlines are normalized first, because a lone CR would otherwise be written on one
        /// line and read back as two. The block form is also forced when a single line would end in
        /// <c>&lt;&lt;&lt;</c>, which the engine would read as a block opening.
        /// </summary>
        internal static string[] Render(string trigger, string replacement)
        {
            string normalised = NormaliseNewlines(replacement);

            // TrimEnd because the engine trims before testing the suffix, so trailing spaces do not
            // save a line that would otherwise open a block.
            bool needsBlock = normalised.Contains('\n', StringComparison.Ordinal)
                || normalised.TrimEnd().EndsWith("<<<", StringComparison.Ordinal);

            if (!needsBlock)
            {
                    return new[] { trigger + "=" + normalised };
            }

            var lines = new List<string> { trigger + "<<<" };
            lines.AddRange(normalised.Split('\n'));
            lines.Add(">>>");
            return lines.ToArray();
        }

        /// <summary>Collapses CRLF and lone CR to <c>\n</c>, the one form the grammar can carry.</summary>
        internal static string NormaliseNewlines(string text)
            => text.Replace("\r\n", "\n", StringComparison.Ordinal)
                   .Replace('\r', '\n');

        /// <summary>
        /// True when a replacement can be written and read back unchanged. The grammar has no escape for a block
        /// body line that trims to <c>&gt;&gt;&gt;</c>, which would close the block early; a replacement that is
        /// only <c>&gt;&gt;&gt;</c> is fine because it is written on a single line.
        /// </summary>
        public static bool IsValidReplacement(string replacement, out SnippetProblem problem)
        {
            string normalised = NormaliseNewlines(replacement ?? string.Empty);

            if (normalised.Contains('\n', StringComparison.Ordinal))
            {
                foreach (string line in normalised.Split('\n'))
                {
                    if (line.Trim() == ">>>")
                    {
                        problem = SnippetProblem.ReplacementHasBlockEnd;
                        return false;
                    }
                }
            }

            problem = SnippetProblem.None;
            return true;
        }

        public string ToText() => string.Join(Environment.NewLine, ToLines());

        /// <summary>
        /// True when a trigger is safe to write. Rejects the two spellings the grammar cannot
        /// express, rather than writing a line that would parse back as something else.
        /// </summary>
        public static bool IsValidTrigger(string trigger, out SnippetProblem problem)
        {
            if (string.IsNullOrWhiteSpace(trigger))
            {
                problem = SnippetProblem.EmptyTrigger;
                return false;
            }

            // Checked before the whitespace rule: a newline inside a trigger would be read back as two lines.
            if (trigger.Contains('\n', StringComparison.Ordinal)
                || trigger.Contains('\r', StringComparison.Ordinal))
            {
                problem = SnippetProblem.TriggerHasLineBreak;
                return false;
            }

            // A leading U+FEFF is eaten by BOM detection on the next read, so the saved trigger is
            // not the one the user entered. Usually arrives by pasting from another editor.
            if (trigger[0] == '\uFEFF')
            {
                problem = SnippetProblem.TriggerStartsWithHiddenCharacter;
                return false;
            }

            if (!string.Equals(trigger, trigger.Trim(), StringComparison.Ordinal))
            {
                // A leading space is unreachable (the parser trims before reading the trigger) and a
                // trailing one silently becomes part of it.
                problem = SnippetProblem.TriggerHasOuterSpace;
                return false;
            }

            if (trigger.Contains('=', StringComparison.Ordinal))
            {
                problem = SnippetProblem.TriggerHasEquals;
                return false;
            }

            if (trigger.EndsWith("<<<", StringComparison.Ordinal))
            {
                problem = SnippetProblem.TriggerEndsWithBlockStart;
                return false;
            }

            if (trigger.StartsWith("//", StringComparison.Ordinal))
            {
                problem = SnippetProblem.TriggerStartsWithComment;
                return false;
            }

            problem = SnippetProblem.None;
            return true;
        }

        /// <summary>Appends a line or an entry, keeping the entry lists in step.</summary>
        private void Add(object item)
        {
            _items.Add(item);
            if (item is Entry entry)
            {
                _entries.Add(entry);
                _byId[entry.Id] = entry;
            }
        }
    }
}
