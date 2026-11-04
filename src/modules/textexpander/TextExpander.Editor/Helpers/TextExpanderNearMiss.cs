// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Microsoft.PowerToys.TextExpander.Editor.Helpers
{
    /// <summary>
    /// Text in a replacement that looks meant as a variable but isn't one, so it's typed as written, such as
    /// <c>{{Date}}</c>, <c>{{ date }}</c>, <c>{date}</c> or <c>{{Prompt:Name}}</c>, and the variable it most likely means.
    /// Variable names are exact and case-sensitive, so these never expand.
    /// </summary>
    /// <param name="Start">Where it starts in the replacement.</param>
    /// <param name="End">Where it ends.</param>
    /// <param name="Written">The text as written.</param>
    /// <param name="Suggestion">The variable it most likely means, or null when there's no sure one.</param>
    internal sealed record TextExpanderNearMiss(int Start, int End, string Written, string Suggestion)
    {
        // Longer than any variable a near miss could be for, so a stray brace doesn't reach across a paragraph.
        private const int MaxLength = 120;

        // The names a variable starts with, and words people use for one.
        private static readonly string[] Names = ["date", "time", "datetime", "isodate", "clipboard", "prompt", "prompt?"];

        private static readonly (string Word, string Variable)[] Synonyms =
        [
            ("cursor", Variables.CursorMarker),
            ("caret", Variables.CursorMarker),
            ("today", "{{date}}"),
            ("now", "{{datetime}}"),
            ("paste", "{{clipboard}}"),
        ];

        private static readonly string[] ClipboardWords = ["pick", "email", "link", "phone", "number", "last", "all"];

        /// <summary>Finds the near misses in <paramref name="replacement"/>, in order, outside its real variables.</summary>
        public static IReadOnlyList<TextExpanderNearMiss> Find(string replacement)
        {
            var found = new List<TextExpanderNearMiss>();
            if (string.IsNullOrEmpty(replacement) || !replacement.Contains('{', StringComparison.Ordinal))
            {
                return found;
            }

            IReadOnlyList<(int Start, int End)> variables = TextExpanderTokenText.FindVariables(replacement);

            // Only the first $|$ places the cursor, so with one there already, another would be typed as written.
            bool cursorTaken = false;
            foreach ((int start, int end) in variables)
            {
                cursorTaken |= end - start == Variables.CursorMarker.Length
                    && string.CompareOrdinal(replacement, start, Variables.CursorMarker, 0, Variables.CursorMarker.Length) == 0;
            }

            int next = 0;
            int index = 0;
            while (index < replacement.Length)
            {
                int open = replacement.IndexOf('{', index);
                if (open < 0)
                {
                    break;
                }

                // Inside a real variable, such as a date format with braces in it, nothing is a near miss.
                while (next < variables.Count && variables[next].End <= open)
                {
                    next++;
                }

                if (next < variables.Count && variables[next].Start <= open)
                {
                    index = variables[next].End;
                    continue;
                }

                if (Candidate(replacement, open, next < variables.Count ? variables[next].Start : replacement.Length, cursorTaken) is { } miss)
                {
                    found.Add(miss);
                    cursorTaken |= miss.Suggestion == Variables.CursorMarker;
                    index = miss.End;
                }
                else
                {
                    index = open + 1;
                }
            }

            return found;
        }

        /// <summary>
        /// Reads a near miss starting at <paramref name="open"/>, with one or two braces each side, that ends before
        /// <paramref name="limit"/>, the next real variable. With <paramref name="cursorTaken"/>, $|$ isn't suggested.
        /// </summary>
        private static TextExpanderNearMiss Candidate(string text, int open, int limit, bool cursorTaken)
        {
            int braces = open + 1 < text.Length && text[open + 1] == '{' ? 2 : 1;
            int innerStart = open + braces;

            // Reading the second of two braces as one brace would find what the two were already read as.
            if (braces == 1 && open > 0 && text[open - 1] == '{')
            {
                return null;
            }

            // Bounded, so text full of unclosed braces takes linear time.
            int searchEnd = Math.Min(limit, Math.Min(text.Length, innerStart + MaxLength));
            int close = searchEnd > innerStart ? text.IndexOf('}', innerStart, searchEnd - innerStart) : -1;
            if (close < 0)
            {
                return null;
            }

            string inner = text[innerStart..close];
            if (inner.Contains('\n', StringComparison.Ordinal) || inner.Contains('\r', StringComparison.Ordinal) || inner.Contains('{', StringComparison.Ordinal))
            {
                return null;
            }

            int end = close + 1;
            if (end < text.Length && text[end] == '}' && end < limit)
            {
                end++;
            }

            // One brace each side is a near miss only for a variable's name itself, such as {date}, not for any word in braces.
            string suggestion = Suggest(inner, strictName: braces == 1 || end - close == 1);
            if (cursorTaken && suggestion == Variables.CursorMarker)
            {
                suggestion = null;
            }

            if (suggestion == null && (braces == 1 || end - close == 1 || !NamesAVariable(inner)))
            {
                return null;
            }

            return new TextExpanderNearMiss(open, end, text[open..end], suggestion);
        }

        /// <summary>Gets whether <paramref name="inner"/> starts with a variable's name, as {{clipboard:xyz}} does.</summary>
        private static bool NamesAVariable(string inner)
        {
            (string name, _) = SplitName(inner);
            return Array.IndexOf(Names, name) >= 0;
        }

        /// <summary>
        /// Gets the variable <paramref name="inner"/>, the text between the braces, most likely means: its name in lowercase,
        /// without spaces, or with one letter wrong, missing, extra or swapped, then what follows the name as the variable
        /// needs it. With <paramref name="strictName"/>, as for {date}, only an exact name in any case counts.
        /// </summary>
        private static string Suggest(string inner, bool strictName)
        {
            (string name, string after) = SplitName(inner);
            if (after == null && !strictName)
            {
                // Only in double braces, as one brace around a word is common in code.
                foreach ((string word, string variable) in Synonyms)
                {
                    if (name == word)
                    {
                        return variable;
                    }
                }
            }

            string known = Array.IndexOf(Names, name) >= 0 ? name : strictName ? null : Closest(name, Names);
            if (known == null)
            {
                return null;
            }

            string suggestion = known switch
            {
                // Without a label, there's no telling what to ask.
                "prompt" or "prompt?" => string.IsNullOrWhiteSpace(after) ? null : "{{" + known + ":" + after.Trim() + "}}",

                // A format, for a time too, goes in {{date:…}}.
                "date" or "time" or "datetime" or "isodate" when !string.IsNullOrWhiteSpace(after) => "{{date:" + after.Trim() + "}}",
                "clipboard" => ClipboardSuggestion(after),
                _ => string.IsNullOrWhiteSpace(after) ? "{{" + known + "}}" : null,
            };

            return suggestion != null && IsOneVariable(suggestion) ? suggestion : null;
        }

        /// <summary>Gets a clipboard variable for what follows its name, each word in lowercase or one letter off, or null.</summary>
        private static string ClipboardSuggestion(string after)
        {
            if (string.IsNullOrWhiteSpace(after))
            {
                return "{{clipboard}}";
            }

            string[] words = after.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant().Split(':');
            if (words is ["all"])
            {
                // All of it is the whole copy.
                return "{{clipboard}}";
            }

            for (int index = 0; index < words.Length; index++)
            {
                if (!int.TryParse(words[index], NumberStyles.None, CultureInfo.InvariantCulture, out _))
                {
                    words[index] = Array.IndexOf(ClipboardWords, words[index]) >= 0 ? words[index] : Closest(words[index], ClipboardWords);
                    if (words[index] == null)
                    {
                        return null;
                    }
                }
            }

            string token = "{{clipboard:" + string.Join(':', words) + "}}";
            return ClipboardVariable.TryParse(token, out ClipboardVariable variable) ? variable.ToToken() : null;
        }

        /// <summary>Splits off the name, in lowercase and without spaces, before the first colon; what's after is null without one.</summary>
        private static (string Name, string After) SplitName(string inner)
        {
            int colon = inner.IndexOf(':', StringComparison.Ordinal);
            string name = (colon < 0 ? inner : inner[..colon]).Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
            return (name, colon < 0 ? null : inner[(colon + 1)..]);
        }

        private static bool IsOneVariable(string text)
        {
            if (text == Variables.CursorMarker)
            {
                return true;
            }

            IReadOnlyList<(int Start, int End)> variables = TextExpanderTokenText.FindVariables(text);
            return variables.Count == 1 && variables[0] == (0, text.Length);
        }

        /// <summary>Gets the one word of <paramref name="words"/> that <paramref name="word"/> is a letter away from, or null.</summary>
        private static string Closest(string word, string[] words)
        {
            if (word.Length < 3)
            {
                return null;
            }

            string closest = null;
            foreach (string candidate in words)
            {
                if (OneEditApart(word, candidate))
                {
                    if (closest != null)
                    {
                        return null;
                    }

                    closest = candidate;
                }
            }

            return closest;
        }

        /// <summary>Gets whether one letter changed, added, removed or two neighbors swapped turns one word into the other.</summary>
        private static bool OneEditApart(string a, string b)
        {
            if (a == b || Math.Abs(a.Length - b.Length) > 1)
            {
                return false;
            }

            int start = 0;
            while (start < a.Length && start < b.Length && a[start] == b[start])
            {
                start++;
            }

            int endA = a.Length;
            int endB = b.Length;
            while (endA > start && endB > start && a[endA - 1] == b[endB - 1])
            {
                endA--;
                endB--;
            }

            int differA = endA - start;
            int differB = endB - start;
            return (differA <= 1 && differB <= 1)
                || (differA == 2 && differB == 2 && a[start] == b[start + 1] && a[start + 1] == b[start]);
        }
    }
}
