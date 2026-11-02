// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.PowerToys.TextExpander.Editor.Helpers
{
    /// <summary>
    /// Plans how <c>TextExpanderBubbleText</c> draws a snippet's text: values as bubbles, or everything as text
    /// for a trimmed line (see <see cref="TextExpanderTrimFallback"/>).
    /// </summary>
    internal static class TextExpanderInlines
    {
        private const string ThinSpace = "\u2009";

        public static IReadOnlyList<TextExpanderInline> Plan(IReadOnlyList<TextExpanderPreviewSegment> segments, bool textOnly, Func<string, string> resource)
        {
            var plan = new List<TextExpanderInline>();
            foreach (TextExpanderPreviewSegment segment in segments ?? Array.Empty<TextExpanderPreviewSegment>())
            {
                switch (segment.Kind)
                {
                    case TextExpanderPreviewSegmentKind.Text:
                        plan.Add(new(TextExpanderInlineKind.Text, segment.Value, segment));
                        break;

                    case TextExpanderPreviewSegmentKind.LineBreak:
                        plan.Add(new(TextExpanderInlineKind.LineBreak, "\n", segment));
                        break;

                    case TextExpanderPreviewSegmentKind.Tab:
                        plan.Add(new(TextExpanderInlineKind.Text, "\t", segment));
                        break;

                    case TextExpanderPreviewSegmentKind.Cursor:
                        plan.Add(textOnly ? new(TextExpanderInlineKind.CursorText, "|", segment) : new(TextExpanderInlineKind.CursorBar, string.Empty, segment));
                        break;

                    default:
                        (string glyph, string label) = ValueText(segment, resource);
                        if (textOnly)
                        {
                            // The bubble's icon and label, without its box. Thin spaces stand in for its margins.
                            plan.Add(new(TextExpanderInlineKind.ValueIcon, ThinSpace + glyph, segment));
                            plan.Add(new(TextExpanderInlineKind.ValueLabel, "\u00A0" + label + ThinSpace, segment));
                        }
                        else
                        {
                            plan.Add(new(TextExpanderInlineKind.Bubble, label, segment));
                        }

                        break;
                }
            }

            return plan;
        }

        /// <summary>Gets a value's icon glyph and the label its bubble shows, such as "Clipboard" or a question's label.</summary>
        public static (string Glyph, string Label) ValueText(TextExpanderPreviewSegment segment, Func<string, string> resource) => segment.Kind switch
        {
            TextExpanderPreviewSegmentKind.Clipboard => ("\uE77F", string.IsNullOrEmpty(segment.Value) ? resource("TextExpander_PreviewClipboard") : segment.Value),
            TextExpanderPreviewSegmentKind.Prompt => ("\uE70F", string.IsNullOrWhiteSpace(segment.Value) ? resource("TextExpander_PreviewPromptUnnamed") : segment.Value),
            TextExpanderPreviewSegmentKind.Time => ("\uE823", segment.Value),
            _ => ("\uE787", segment.Value),
        };
    }
}
