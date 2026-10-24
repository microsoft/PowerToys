// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>
    /// Builds the snippet editor's preview: what a snippet types, with a bubble wherever a value is
    /// only known when it runs.
    ///
    /// <para>
    /// Reads the snippet the way the engine's <c>Variables.ExpandWithCursor</c> does, with the same
    /// pattern, in one pass from the left: each value (a date, a time, a question) is a bubble of
    /// its own that nothing reads again, and only the snippet's own text is read for <c>\n</c>,
    /// <c>\t</c>, {{clipboard}} and the first cursor marker. The Settings project cannot reference
    /// the engine, so this is a deliberate second implementation: a change to one has to be made
    /// to the other.
    /// </para>
    /// </summary>
    internal static partial class TextExpanderSnippetPreview
    {
        private const string CursorMarker = "$|$";

        private const string ClipboardPlaceholder = "{{clipboard}}";

        public static IReadOnlyList<TextExpanderPreviewSegment> Build(string replacement, DateTime now)
        {
            if (string.IsNullOrEmpty(replacement))
            {
                return Array.Empty<TextExpanderPreviewSegment>();
            }

            // A snippet file cannot hold a carriage return, so the engine only ever sees \n.
            string text = replacement.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            var builder = new SegmentBuilder();

            int last = 0;
            if (text.Contains("{{", StringComparison.Ordinal))
            {
                foreach (Match match in ValuePattern().Matches(text))
                {
                    builder.AddSnippetText(text.AsSpan(last, match.Index - last));
                    builder.AddValue(ValueOf(match, now));
                    last = match.Index + match.Length;
                }
            }

            builder.AddSnippetText(text.AsSpan(last));
            return builder.ToSegments();
        }

        /// <summary>Gets a value indicating whether any part of the preview is a bubble.</summary>
        public static bool HasBubbles(IEnumerable<TextExpanderPreviewSegment> segments)
            => segments.Any(segment => segment.IsBubble);

        /// <summary>
        /// Spells the preview out for screen readers, putting each bubble's description, in
        /// parentheses, where the bubble is.
        /// </summary>
        public static string Describe(IEnumerable<TextExpanderPreviewSegment> segments, Func<TextExpanderPreviewSegment, string> describe)
        {
            var result = new StringBuilder();
            bool afterDescription = false;
            foreach (TextExpanderPreviewSegment segment in segments)
            {
                bool isDescription = false;
                switch (segment.Kind)
                {
                    case TextExpanderPreviewSegmentKind.Text:
                        if (afterDescription && segment.Value.Length > 0 && char.IsLetterOrDigit(segment.Value[0]))
                        {
                            result.Append(' ');
                        }

                        result.Append(segment.Value);
                        break;

                    case TextExpanderPreviewSegmentKind.LineBreak:
                        result.Append('\n');
                        break;

                    case TextExpanderPreviewSegmentKind.Tab:
                        result.Append('\t');
                        break;

                    default:
                        if (result.Length > 0 && !char.IsWhiteSpace(result[^1]))
                        {
                            result.Append(' ');
                        }

                        result.Append('(').Append(describe(segment)).Append(')');
                        isDescription = true;
                        break;
                }

                afterDescription = isDescription;
            }

            return result.ToString();
        }

        /// <summary>
        /// Gets a value indicating whether a {{date:FORMAT}} shows only a time of day, such as
        /// <c>HH:mm</c> or <c>%I:%M %p</c>, so it can be called a time rather than a date. Quoted
        /// and escaped text is ignored, and anything unclear counts as a date.
        /// </summary>
        internal static bool IsTimeOnlyFormat(string format)
        {
            bool hasTime = false;
            if (format.Contains('%', StringComparison.Ordinal))
            {
                for (int index = 0; index + 1 < format.Length; index++)
                {
                    if (format[index] != '%')
                    {
                        continue;
                    }

                    char specifier = format[++index];
                    if ("HIMSpRTX".Contains(specifier, StringComparison.Ordinal))
                    {
                        hasTime = true;
                    }
                    else if (specifier != '%' && specifier != 'n' && specifier != 't')
                    {
                        return false;
                    }
                }

                return hasTime;
            }

            for (int index = 0; index < format.Length; index++)
            {
                char c = format[index];
                if (c == '\\')
                {
                    index++;
                }
                else if (c is '\'' or '"')
                {
                    int close = format.IndexOf(c, index + 1);
                    index = close < 0 ? format.Length : close;
                }
                else if ("hHmstf".Contains(c, StringComparison.Ordinal))
                {
                    hasTime = true;
                }
                else if (char.IsLetter(c))
                {
                    return false;
                }
            }

            return hasTime;
        }

        /// <summary>
        /// Formats a date for {{date:FORMAT}}. Mirrors the engine's rule that a one-character
        /// format is a field rather than a standard specifier, so {{date:d}} previews the day and
        /// not a whole short date.
        /// </summary>
        internal static string FormatDate(DateTime value, string format)
        {
            if (format.Length == 0)
            {
                return string.Empty;
            }

            // A '%' is unambiguous: it is not a .NET custom specifier on its own, and every
            // strftime format has one. Returning the format verbatim here used to make
            // {{date:%Y-%m-%d}} preview as literal "%Y-%m-%d", telling the user that a syntax
            // the engine fully supports does not work.
            if (format.Contains('%', StringComparison.Ordinal))
            {
                return FormatStrftime(value, format);
            }

            try
            {
                return value.ToString(format.Length == 1 ? "%" + format : format, CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                return format;
            }
        }

        /// <summary>
        /// Expands strftime specifiers for the preview.
        ///
        /// <para>
        /// A deliberate second implementation of <c>Variables.FormatStrftime</c> in the engine,
        /// because the Settings project cannot reference the engine assembly. Kept as a literal
        /// transcription for that reason -- if the engine's table changes, this one has to change
        /// with it, and a preview that disagrees with what gets typed is the bug this whole
        /// method exists to fix.
        /// </para>
        /// </summary>
        private static string FormatStrftime(DateTime value, string format)
        {
            var result = new StringBuilder(format.Length + 16);

            for (int index = 0; index < format.Length; index++)
            {
                if (format[index] != '%' || index + 1 >= format.Length)
                {
                    result.Append(format[index]);
                    continue;
                }

                char specifier = format[++index];
                result.Append(specifier switch
                {
                    '%' => "%",
                    'a' => value.ToString("ddd", CultureInfo.InvariantCulture),
                    'A' => value.ToString("dddd", CultureInfo.InvariantCulture),
                    'b' or 'h' => value.ToString("MMM", CultureInfo.InvariantCulture),
                    'B' => value.ToString("MMMM", CultureInfo.InvariantCulture),
                    'd' => value.ToString("dd", CultureInfo.InvariantCulture),
                    'e' => value.Day.ToString(CultureInfo.InvariantCulture).PadLeft(2, ' '),
                    'F' => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    'H' => value.ToString("HH", CultureInfo.InvariantCulture),
                    'I' => value.ToString("hh", CultureInfo.InvariantCulture),
                    'j' => value.DayOfYear.ToString("000", CultureInfo.InvariantCulture),
                    'm' => value.ToString("MM", CultureInfo.InvariantCulture),
                    'M' => value.ToString("mm", CultureInfo.InvariantCulture),
                    'p' => value.ToString("tt", CultureInfo.InvariantCulture),
                    'R' => value.ToString("HH:mm", CultureInfo.InvariantCulture),
                    'S' => value.ToString("ss", CultureInfo.InvariantCulture),
                    'T' => value.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                    'u' => ((((int)value.DayOfWeek + 6) % 7) + 1).ToString(CultureInfo.InvariantCulture),
                    'w' => ((int)value.DayOfWeek).ToString(CultureInfo.InvariantCulture),
                    'x' => value.ToString("MM/dd/yy", CultureInfo.InvariantCulture),
                    'X' => value.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                    'y' => value.ToString("yy", CultureInfo.InvariantCulture),
                    'Y' => value.ToString("yyyy", CultureInfo.InvariantCulture),
                    'n' => "\n",
                    't' => "\t",
                    _ => $"%{specifier}",
                });
            }

            return result.ToString();
        }

        /// <summary>The engine's <c>Variables.ValuePattern</c>: every value a snippet can hold, read in one pass from the left.</summary>
        [GeneratedRegex(@"\{\{(?:(?<fixed>date|time|datetime|isodate)|date:(?<format>[^}]*)|prompt\??:(?<label>[^}]*))\}\}")]
        private static partial Regex ValuePattern();

        /// <summary>The bubble one match of <see cref="ValuePattern"/> stands for.</summary>
        private static TextExpanderPreviewSegment ValueOf(Match match, DateTime now)
        {
            if (match.Groups["label"].Success)
            {
                return new TextExpanderPreviewSegment(TextExpanderPreviewSegmentKind.Prompt, match.Groups["label"].Value);
            }

            if (match.Groups["format"].Success)
            {
                string format = match.Groups["format"].Value;
                return new TextExpanderPreviewSegment(
                    IsTimeOnlyFormat(format) ? TextExpanderPreviewSegmentKind.Time : TextExpanderPreviewSegmentKind.Date,
                    FormatDate(now, format));
            }

            return match.Groups["fixed"].Value switch
            {
                "date" => new TextExpanderPreviewSegment(TextExpanderPreviewSegmentKind.Date, now.ToString("MM/dd/yy", CultureInfo.InvariantCulture)),
                "time" => new TextExpanderPreviewSegment(TextExpanderPreviewSegmentKind.Time, now.ToString("hh:mm tt", CultureInfo.InvariantCulture)),
                "datetime" => new TextExpanderPreviewSegment(TextExpanderPreviewSegmentKind.Date, now.ToString("MM/dd/yy hh:mm tt", CultureInfo.InvariantCulture)),
                _ => new TextExpanderPreviewSegment(TextExpanderPreviewSegmentKind.Date, now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            };
        }

        /// <summary>Collects the preview's segments as the snippet is read, joining typed text into one segment.</summary>
        private sealed class SegmentBuilder
        {
            private readonly List<TextExpanderPreviewSegment> _segments = new();
            private readonly StringBuilder _typed = new();
            private bool _cursorPlaced;

            /// <summary>
            /// Adds part of the snippet's own text, reading it as the engine does: <c>\n</c> and
            /// <c>\t</c> are a line break and a tab, {{clipboard}} is pasted, and the first
            /// cursor marker places the cursor; the engine types any later ones as they are.
            /// </summary>
            public void AddSnippetText(ReadOnlySpan<char> text)
            {
                for (int index = 0; index < text.Length;)
                {
                    char character = text[index];
                    if (character == '\\' && index + 1 < text.Length && text[index + 1] is 'n' or 't')
                    {
                        AddBreak(text[index + 1] == 'n' ? '\n' : '\t');
                        index += 2;
                    }
                    else if (character is '\n' or '\t')
                    {
                        AddBreak(character);
                        index++;
                    }
                    else if (character == '{' && text[index..].StartsWith(ClipboardPlaceholder, StringComparison.Ordinal))
                    {
                        AddValue(new TextExpanderPreviewSegment(TextExpanderPreviewSegmentKind.Clipboard));
                        index += ClipboardPlaceholder.Length;
                    }
                    else if (!_cursorPlaced && character == '$' && text[index..].StartsWith(CursorMarker, StringComparison.Ordinal))
                    {
                        AddValue(new TextExpanderPreviewSegment(TextExpanderPreviewSegmentKind.Cursor));
                        _cursorPlaced = true;
                        index += CursorMarker.Length;
                    }
                    else
                    {
                        _typed.Append(character);
                        index++;
                    }
                }
            }

            /// <summary>Adds a bubble or marker. A date that formats to nothing shows nothing, as nothing is typed.</summary>
            public void AddValue(TextExpanderPreviewSegment value)
            {
                if (value.Kind is TextExpanderPreviewSegmentKind.Date or TextExpanderPreviewSegmentKind.Time && string.IsNullOrEmpty(value.Value))
                {
                    return;
                }

                FlushTyped();
                _segments.Add(value);
            }

            public IReadOnlyList<TextExpanderPreviewSegment> ToSegments()
            {
                FlushTyped();
                return _segments;
            }

            private void AddBreak(char character)
            {
                FlushTyped();
                _segments.Add(new TextExpanderPreviewSegment(character == '\n' ? TextExpanderPreviewSegmentKind.LineBreak : TextExpanderPreviewSegmentKind.Tab));
            }

            private void FlushTyped()
            {
                if (_typed.Length > 0)
                {
                    _segments.Add(new TextExpanderPreviewSegment(TextExpanderPreviewSegmentKind.Text, _typed.ToString()));
                    _typed.Clear();
                }
            }
        }
    }
}
