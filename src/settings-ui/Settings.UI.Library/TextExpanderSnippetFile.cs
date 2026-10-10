// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// An editable view over snippets.txt that preserves everything it does not own.
    ///
    /// <para>
    /// <c>SnippetStore.Parse</c> in the engine answers "what does the engine expand?" and throws the rest
    /// away — comments, blank lines, ordering and the exact spelling of each entry. That is right for
    /// the hook and wrong for an editor: a user who has commented and grouped a hand-written file,
    /// then adds one snippet from the Settings page, must not get the file back stripped of every
    /// comment and reordered. So this keeps the original lines and rewrites only the entries that
    /// were actually edited.
    /// </para>
    ///
    /// <para>
    /// The grammar is deliberately not re-derived here. It mirrors <c>SnippetStore.Parse</c> in the engine
    /// exactly, and <c>AC42</c> asserts the two agree on the same input, because an editor that
    /// disagrees with the engine about what a file means would silently write snippets that never
    /// fire.
    /// </para>
    /// </summary>
    public sealed class TextExpanderSnippetFile
    {
        /// <summary>A line this file does not own: a comment, a blank, or unparseable text.</summary>
        private sealed class RawLine
        {
            public RawLine(string text) => Text = text;

            public string Text { get; }
        }

        /// <summary>
        /// One snippet, together with the lines it came from.
        ///
        /// <para>
        /// <see cref="SourceLines"/> is what lets an untouched entry re-emit its exact original
        /// lines. Regenerating every entry on save would normalise leading whitespace and collapse
        /// the block form onto one line — a diff full of changes nobody made, which is how a user
        /// learns not to trust the editor with their file.
        /// </para>
        ///
        /// <para>
        /// Note this is line-level preservation, not byte-level: the encoding, line-ending style and
        /// final newline are decided by whoever writes <see cref="ToLines"/> out to disk.
        /// </para>
        /// </summary>
        public sealed class Entry
        {
            /// <summary>
            /// Stable handle for this entry, unique within its <see cref="TextExpanderSnippetFile"/>.
            ///
            /// <para>
            /// Entries cannot be addressed by trigger. The engine's map is last-one-wins, so a file
            /// may legitimately contain two rows with the same trigger — and that is exactly the case
            /// the duplicate check exists to surface. Keying on the trigger meant editing the second
            /// row silently rewrote the first, and deleting the second deleted the first, while the
            /// text that actually expands never changed.
            /// </para>
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
        private int _nextId;

        private TextExpanderSnippetFile()
        {
        }

        /// <summary>Snippets in file order. Duplicate triggers are kept; see <see cref="FindDuplicateTrigger"/>.</summary>
        public IReadOnlyList<Entry> Snippets => _items.OfType<Entry>().ToList();

        // ── Reading ──────────────────────────────────────────────
        public static TextExpanderSnippetFile Parse(string[] lines)
        {
            ArgumentNullException.ThrowIfNull(lines);

            var file = new TextExpanderSnippetFile();
            int i = 0;

            while (i < lines.Length)
            {
                string line = lines[i];
                string stripped = line.Trim();
                int start = i;
                i++;

                if (stripped.Length == 0 || stripped.StartsWith("//", StringComparison.Ordinal))
                {
                    file._items.Add(new RawLine(line));
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

                    // An unterminated block still parses in the engine, which treats the rest of the
                    // file as the body. That reading is preserved rather than "repaired" on load, so
                    // an unedited file round-trips; ToLines closes the block only when it has to
                    // write something after it, which would otherwise land inside the body.
                    if (trigger.Length > 0)
                    {
                        file._items.Add(new Entry
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
                                file._items.Add(new RawLine(skipped));
                        }
                    }

                    continue;
                }

                int eq = stripped.IndexOf('=');
                if (eq > 0)
                {
                    int rawEq = line.IndexOf('=');
                    file._items.Add(new Entry
                    {
                        Id = file._nextId++,
                        Trigger = stripped.Substring(0, eq),
                        Replacement = line.Substring(rawEq + 1),
                        SourceLines = new[] { line },
                    });
                }
                else
                {
                    file._items.Add(new RawLine(line));
                }
            }

            return file;
        }

        // ── Editing ──────────────────────────────────────────────
        /// <summary>
        /// Adds a snippet, or updates the entry with <paramref name="id"/>. Pass null to add.
        ///
        /// <para>
        /// Addressed by id rather than by trigger: see <see cref="Entry.Id"/>. Passing an id that is
        /// no longer present throws rather than silently appending, because that means the caller is
        /// holding a stale row and appending would quietly create a second snippet instead of
        /// editing the one the user is looking at.
        /// </para>
        /// </summary>
        public Entry Upsert(int? id, string trigger, string replacement)
        {
            ArgumentException.ThrowIfNullOrEmpty(trigger);
            ArgumentNullException.ThrowIfNull(replacement);

            if (!IsValidTrigger(trigger, out string triggerProblem))
            {
                    throw new ArgumentException(triggerProblem, nameof(trigger));
            }

            if (!IsValidReplacement(replacement, out string replacementProblem))
            {
                    throw new ArgumentException(replacementProblem, nameof(replacement));
            }

            // Stored normalised so what the model holds is what a save/read cycle returns.
            replacement = NormaliseNewlines(replacement);

            if (id is null)
            {
                if (FindDuplicate(trigger, ignoringId: null) is not null)
                {
                        throw new InvalidOperationException($"Another snippet already uses '{trigger}'.");
                }

                var added = new Entry { Id = _nextId++, Trigger = trigger, Replacement = replacement };
                _items.Add(added);
                return added;
            }

            Entry existing = _items.OfType<Entry>().FirstOrDefault(e => e.Id == id.Value)
                ?? throw new InvalidOperationException($"Snippet {id.Value} is no longer in this file.");

            if (FindDuplicate(trigger, ignoringId: id.Value) is not null)
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
            Entry found = _items.OfType<Entry>().FirstOrDefault(e => e.Id == id);
            return found is not null && _items.Remove(found);
        }

        /// <summary>
        /// The entry that would shadow <paramref name="trigger"/>, ignoring the one being edited.
        ///
        /// <para>
        /// The engine's map is last-one-wins, so a duplicate does not error — it silently makes one
        /// of the two snippets dead. Surfacing it before the save is the only point at which the
        /// user can tell the difference.
        /// </para>
        ///
        /// <para>
        /// Excluding by id, not by trigger. Excluding by trigger skipped <em>every</em> row sharing
        /// it, so a file that already contained a duplicate reported no conflict at all — the one
        /// case this check exists for.
        /// </para>
        /// </summary>
        public Entry FindDuplicate(string trigger, int? ignoringId = null)
        {
            foreach (Entry entry in _items.OfType<Entry>())
            {
                if (ignoringId is not null && entry.Id == ignoringId.Value)
                {
                        continue;
                }

                if (string.Equals(entry.Trigger, trigger, StringComparison.Ordinal))
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

                    // An unterminated block swallows everything after it. Leaving it open is only
                    // safe when nothing follows — otherwise the next snippet is written *inside*
                    // this one's body and silently stops existing. Closing it here makes the file
                    // say what the editor has been showing all along.
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
        /// Renders one entry.
        ///
        /// <para>
        /// Line endings are normalised to <c>\n</c> <em>before</em> deciding on a representation. A
        /// lone <c>\r</c> contains no <c>\n</c>, so a naive check sends it out single-line — but
        /// <c>StreamReader.ReadLine</c> treats a bare CR as a line terminator when the file is
        /// read back, so the tail of the replacement silently becomes a second snippet and can
        /// overwrite a real one.
        /// </para>
        ///
        /// <para>
        /// The block form is also forced when the single-line output would end in <c>&lt;&lt;&lt;</c>.
        /// The engine tests for that suffix <em>before</em> it looks for an <c>=</c>, so
        /// <c>a=literal&lt;&lt;&lt;</c> is read as a block opening with the trigger
        /// <c>a=literal</c> — destroying this entry and swallowing every following line as its body.
        /// </para>
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
        /// True when a replacement can be written and read back unchanged.
        ///
        /// <para>
        /// A multi-line replacement is stored as a <c>&lt;&lt;&lt;</c>…<c>&gt;&gt;&gt;</c> block, and
        /// the grammar has no escape for its own terminator. A body line that trims to
        /// <c>&gt;&gt;&gt;</c> therefore closes the block early: the rest of the replacement lands in
        /// the file as top-level syntax and can silently redefine an unrelated snippet. Refusing is
        /// the only honest option — this really is unrepresentable, not merely awkward.
        /// </para>
        ///
        /// <para>
        /// A replacement that is <em>only</em> <c>&gt;&gt;&gt;</c> is fine, because it goes out in the
        /// single-line form where the terminator has no meaning.
        /// </para>
        /// </summary>
        public static bool IsValidReplacement(string replacement, out string problem)
        {
            string normalised = NormaliseNewlines(replacement ?? string.Empty);

            if (normalised.Contains('\n', StringComparison.Ordinal))
            {
                foreach (string line in normalised.Split('\n'))
                {
                    if (line.Trim() == ">>>")
                    {
                        problem = "A multi-line replacement cannot contain a line that is just '>>>'.";
                        return false;
                    }
                }
            }

            problem = null;
            return true;
        }

        public string ToText() => string.Join(Environment.NewLine, ToLines());

        /// <summary>
        /// True when a trigger is safe to write. Rejects the two spellings the grammar cannot
        /// express, rather than writing a line that would parse back as something else.
        /// </summary>
        public static bool IsValidTrigger(string trigger, out string problem)
        {
            if (string.IsNullOrWhiteSpace(trigger))
            {
                problem = "Enter a trigger.";
                return false;
            }

            // Checked before the whitespace rule: a trigger carrying a newline passes Trim()
            // untouched, is written as one line, and then comes back from StreamReader as two —
            // so the snippet silently ends up under a different trigger than the one just typed.
            if (trigger.Contains('\n', StringComparison.Ordinal)
                || trigger.Contains('\r', StringComparison.Ordinal))
            {
                problem = "A trigger cannot contain a line break.";
                return false;
            }

            // A leading U+FEFF is eaten by BOM detection on the next read, so the saved trigger is
            // not the one the user entered. Usually arrives by pasting from another editor.
            if (trigger[0] == '\uFEFF')
            {
                problem = "A trigger cannot start with a byte order mark.";
                return false;
            }

            if (!string.Equals(trigger, trigger.Trim(), StringComparison.Ordinal))
            {
                // A leading space is unreachable (the parser trims before reading the trigger) and a
                // trailing one silently becomes part of it.
                problem = "A trigger cannot start or end with a space.";
                return false;
            }

            if (trigger.Contains('=', StringComparison.Ordinal))
            {
                problem = "A trigger cannot contain '='.";
                return false;
            }

            if (trigger.EndsWith("<<<", StringComparison.Ordinal))
            {
                problem = "A trigger cannot end with '<<<'.";
                return false;
            }

            if (trigger.StartsWith("//", StringComparison.Ordinal))
            {
                problem = "A trigger cannot start with '//'.";
                return false;
            }

            problem = null;
            return true;
        }
    }
}
