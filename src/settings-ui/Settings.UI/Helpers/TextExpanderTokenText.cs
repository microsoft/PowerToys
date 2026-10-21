// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>
    /// The plain-text rules behind the snippet editor's replacement box: where its variables are,
    /// what Backspace and Delete remove next to one, where Insert variable puts a new one, and how
    /// the box's text maps to the replacement that is saved.
    /// </summary>
    internal static partial class TextExpanderTokenText
    {
        private const string CursorMarker = "$|$";
        private const string Closing = "}}";

        /// <summary>Variables whose middle part Insert variable selects, ready to type over.</summary>
        private static readonly string[] EditablePrefixes = { "{{prompt:", "{{prompt?:", "{{date:" };

        /// <summary>Gets the variables in <paramref name="text"/>, in order, as start and end positions.</summary>
        public static IReadOnlyList<(int Start, int End)> FindVariables(string text)
        {
            var variables = new List<(int Start, int End)>();
            bool hasCursor = false;
            foreach (Match match in VariablePattern().Matches(text ?? string.Empty))
            {
                if (match.Value == CursorMarker)
                {
                    // Only the first marker moves the cursor; the engine types any others as they are.
                    if (hasCursor)
                    {
                        continue;
                    }

                    hasCursor = true;
                }

                variables.Add((match.Index, match.Index + match.Length));
            }

            return variables;
        }

        /// <summary>Gets the variable that <paramref name="position"/> falls strictly inside, if any.</summary>
        public static (int Start, int End)? VariableAround(string text, int position)
        {
            foreach ((int Start, int End) variable in FindVariables(text))
            {
                if (variable.Start < position && position < variable.End)
                {
                    return variable;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the variable that Backspace (just before the caret) or Delete (just after it)
        /// removes whole, so a variable is never left half deleted. Null when the key acts as usual.
        /// </summary>
        public static (int Start, int End)? VariableToDelete(string text, int caret, bool backward)
        {
            foreach ((int Start, int End) variable in FindVariables(text))
            {
                if (backward ? variable.End == caret : variable.Start == caret)
                {
                    return variable;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the range Insert variable replaces. That is normally the selection, but never part of
        /// another variable: then the new one goes straight after it and nothing is replaced.
        /// </summary>
        public static (int Start, int End) InsertionRange(string text, int selectionStart, int selectionEnd)
        {
            (int Start, int End)? first = VariableAround(text, selectionStart);
            (int Start, int End)? last = VariableAround(text, selectionEnd);
            if (first == null && last == null)
            {
                return (selectionStart, selectionEnd);
            }

            int after = Math.Max(first?.End ?? 0, last?.End ?? 0);
            return (after, after);
        }

        /// <summary>
        /// Gets the selection after <paramref name="variable"/> is inserted at <paramref name="insertedAt"/>:
        /// a question's label or a date's format, ready to type over, or otherwise the caret just after it.
        /// </summary>
        public static (int Start, int Length) SelectionAfterInsert(string variable, int insertedAt)
        {
            foreach (string prefix in EditablePrefixes)
            {
                if (variable.Length >= prefix.Length + Closing.Length
                    && variable.StartsWith(prefix, StringComparison.Ordinal)
                    && variable.EndsWith(Closing, StringComparison.Ordinal))
                {
                    return (insertedAt + prefix.Length, variable.Length - prefix.Length - Closing.Length);
                }
            }

            return (insertedAt + variable.Length, 0);
        }

        /// <summary>
        /// Gets where Insert variable puts <paramref name="variable"/> and what it selects then:
        /// it replaces the selection, though never part of another variable (see
        /// <see cref="InsertionRange"/>), and then selects a question's label or a date's format,
        /// ready to type over. A ready-made date format, with <paramref name="selectEditablePart"/>
        /// false, leaves the caret after it instead.
        /// </summary>
        public static (int Start, int End, int SelectionStart, int SelectionEnd) PlanInsert(string text, int selectionStart, int selectionEnd, string variable, bool selectEditablePart)
        {
            (int start, int end) = InsertionRange(text, selectionStart, selectionEnd);
            (int selectStart, int selectLength) = selectEditablePart ? SelectionAfterInsert(variable, start) : (start + variable.Length, 0);
            return (start, end, selectStart, selectStart + selectLength);
        }

        /// <summary>
        /// Converts the box's text, as RichEdit reports it, to the replacement.
        ///
        /// <para>
        /// RichEdit ends each paragraph with \r, and the whole text with one more that nobody typed.
        /// Snippets use \n, so the file gets exactly the bytes the plain text box used to save.
        /// Every other character maps one to one, so positions in the box stay valid.
        /// </para>
        /// </summary>
        public static string FromEditor(string raw, int storyLength)
        {
            raw ??= string.Empty;
            if (raw.Length > 0 && raw.Length >= storyLength && raw[^1] == '\r')
            {
                raw = raw[..^1];
            }

            return raw.Replace('\r', '\n');
        }

        /// <summary>Converts a replacement's line breaks, in any form, to the box's paragraph marks.</summary>
        public static string ToEditor(string text)
            => (text ?? string.Empty)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Replace('\n', '\r');

        /// <summary>
        /// Finds the one stretch that differs between two texts: where it starts, how many
        /// characters of <paramref name="before"/> it removed and what it put in their place.
        /// </summary>
        public static (int Start, int RemovedLength, string Inserted) Diff(string before, string after)
        {
            before ??= string.Empty;
            after ??= string.Empty;
            int shorter = Math.Min(before.Length, after.Length);

            int start = 0;
            while (start < shorter && before[start] == after[start])
            {
                start++;
            }

            int end = 0;
            while (end < shorter - start && before[before.Length - 1 - end] == after[after.Length - 1 - end])
            {
                end++;
            }

            return (start, before.Length - start - end, after.Substring(start, after.Length - start - end));
        }

        /// <summary>
        /// The variables the engine expands, and the cursor marker. Anything else in braces is typed
        /// as it is, so it is left looking like the rest of the text.
        /// </summary>
        [GeneratedRegex(@"\$\|\$|\{\{(?:date|time|datetime|isodate|clipboard)\}\}|\{\{date:[^}]*\}\}|\{\{prompt\??:[^}]*\}\}")]
        private static partial Regex VariablePattern();
    }
}
