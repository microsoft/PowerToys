// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.PowerToys.TextExpander;

namespace Microsoft.PowerToys.TextExpander.Editor.Helpers
{
    /// <summary>Renders the shared snippet tokens without accessing runtime clipboard or prompt values.</summary>
    internal static class TextExpanderSnippetPreview
    {
        public static IReadOnlyList<TextExpanderPreviewSegment> Build(string replacement, DateTime now)
        {
            if (string.IsNullOrEmpty(replacement))
            {
                return Array.Empty<TextExpanderPreviewSegment>();
            }

            string text = replacement.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            var segments = new List<TextExpanderPreviewSegment>();
            foreach (SnippetToken token in SnippetTemplate.Parse(text))
            {
                TextExpanderPreviewSegmentKind kind = token.Kind switch
                {
                    SnippetTokenKind.Date => IsTimeOnlyFormat(token.Value) ? TextExpanderPreviewSegmentKind.Time : TextExpanderPreviewSegmentKind.Date,
                    SnippetTokenKind.Time => TextExpanderPreviewSegmentKind.Time,
                    SnippetTokenKind.Prompt => TextExpanderPreviewSegmentKind.Prompt,
                    SnippetTokenKind.Clipboard => TextExpanderPreviewSegmentKind.Clipboard,
                    SnippetTokenKind.Cursor => TextExpanderPreviewSegmentKind.Cursor,
                    SnippetTokenKind.LineBreak => TextExpanderPreviewSegmentKind.LineBreak,
                    SnippetTokenKind.Tab => TextExpanderPreviewSegmentKind.Tab,
                    _ => TextExpanderPreviewSegmentKind.Text,
                };
                string value = token.Kind is SnippetTokenKind.Date or SnippetTokenKind.Time
                    ? FormatDate(now, token.Value)
                    : token.Value;
                if (kind is TextExpanderPreviewSegmentKind.Date or TextExpanderPreviewSegmentKind.Time && value.Length == 0)
                {
                    continue;
                }

                if (kind == TextExpanderPreviewSegmentKind.Text && segments.Count > 0
                    && segments[^1].Kind == TextExpanderPreviewSegmentKind.Text)
                {
                    segments[^1] = segments[^1] with { Value = segments[^1].Value + value };
                }
                else
                {
                    segments.Add(new TextExpanderPreviewSegment(kind, value));
                }
            }

            return segments;
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

        internal static string FormatDate(DateTime value, string format) => Variables.FormatDate(value, format);
    }
}
