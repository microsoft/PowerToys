// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.PowerToys.TextExpander.Editor.Helpers
{
    /// <summary>
    /// Describes a snippet for its row in the list, in Keyboard Manager's style: the questions it asks, what it
    /// inserts as its real lines in an accent chip, why it never expands, and what a screen reader says.
    /// </summary>
    internal static partial class TextExpanderRowDescriber
    {
        /// <summary>The most real lines a row's chip shows; "…" after the last one says more follow.</summary>
        internal const int ChipLineLimit = 3;

        /// <summary>The chip's icon for text.</summary>
        internal const string TextGlyph = "\uE8D2";

        private const int SpokenLimit = 170;

        /// <summary>
        /// Describes a snippet for its row in Keyboard Manager's style: the questions it asks, what it inserts as its
        /// real lines in an accent chip with an icon, why it never expands, and what a screen reader says.
        /// </summary>
        public static TextExpanderRowDescription Describe(
            string trigger,
            string replacement,
            bool hasLaterDuplicate,
            TextExpanderTriggerSet triggers,
            bool wordMode,
            DateTime now,
            Func<string, string> resource)
        {
            trigger ??= string.Empty;
            IReadOnlyList<TextExpanderPreviewSegment> segments = TextExpanderSnippetPreview.Build(replacement ?? string.Empty, now);

            var questions = new List<string>();
            bool clipboard = false;
            bool dates = false;
            bool onlyTimes = true;
            int lines = 1;
            foreach (TextExpanderPreviewSegment segment in segments)
            {
                switch (segment.Kind)
                {
                    case TextExpanderPreviewSegmentKind.LineBreak:
                        lines++;
                        break;
                    case TextExpanderPreviewSegmentKind.Clipboard:
                        clipboard = true;
                        break;
                    case TextExpanderPreviewSegmentKind.Date:
                        dates = true;
                        onlyTimes = false;
                        break;
                    case TextExpanderPreviewSegmentKind.Time:
                        dates = true;
                        break;
                    case TextExpanderPreviewSegmentKind.Prompt:
                        string label = TextExpanderInlines.ValueText(segment, resource).Label;
                        if (!questions.Contains(label, StringComparer.Ordinal))
                        {
                            questions.Add(label);
                        }

                        break;
                }
            }

            // A snippet that is one value, or only dates and times with punctuation, shows them as its text, with their icon.
            bool oneValue = segments.Count == 1 && segments[0].IsBubble;
            bool onlyDates = dates && !clipboard && questions.Count == 0 && lines == 1 && segments.All(IsDateOrPunctuation);
            TextExpanderPreviewSegmentKind? kind = oneValue ? segments[0].Kind
                : onlyDates ? (onlyTimes ? TextExpanderPreviewSegmentKind.Time : TextExpanderPreviewSegmentKind.Date)
                : clipboard ? TextExpanderPreviewSegmentKind.Clipboard
                : (TextExpanderPreviewSegmentKind?)null;
            string glyph = kind is { } valueKind ? TextExpanderInlines.ValueText(new(valueKind), resource).Glyph : TextGlyph;

            string warning = WarningFor(trigger, hasLaterDuplicate, triggers, wordMode, resource);
            string spoken = Spoken(segments, resource);
            if (lines > 1 && spoken.Length > 0)
            {
                spoken = Format(resource("TextExpander_Row_SpokenLines"), spoken, lines);
            }

            string never = warning == null ? null : Format(resource("TextExpander_Row_NeverExpandsSpoken"), warning);
            string automationName = spoken.Length == 0
                ? (never == null ? trigger : Format(resource("TextExpander_Row_AutomationName"), trigger, never))
                : never == null
                    ? Format(resource("TextExpander_Row_AutomationName"), trigger, spoken)
                    : Format(resource("TextExpander_Row_AutomationNameWarning"), trigger, WithoutFinalPeriod(spoken), never);

            return new TextExpanderRowDescription(glyph, Lead(questions, resource), ChipLines(segments, oneValue || onlyDates, resource), warning, automationName);
        }

        /// <summary>
        /// Gets the words before the chip: "inserts", or the questions as bubbles in "asks {0} then inserts". The
        /// template stays whole, so each language keeps its own word order around the questions.
        /// </summary>
        private static IReadOnlyList<TextExpanderPreviewSegment> Lead(List<string> questions, Func<string, string> resource)
        {
            var lead = new List<TextExpanderPreviewSegment>();
            string template = resource(questions.Count == 0 ? "TextExpander_Row_Inserts" : "TextExpander_Row_AsksThenInserts") ?? string.Empty;
            int slot = template.IndexOf("{0}", StringComparison.Ordinal);
            AddText(lead, slot < 0 ? template : template[..slot]);
            for (int index = 0; index < questions.Count; index++)
            {
                AddText(lead, index > 0 ? " " : string.Empty);
                lead.Add(new TextExpanderPreviewSegment(TextExpanderPreviewSegmentKind.Prompt, questions[index]));
            }

            AddText(lead, slot < 0 ? string.Empty : template[(slot + 3)..]);
            return lead;
        }

        /// <summary>
        /// Gets the real lines a row's chip shows: without blank lines at either end, at most
        /// <see cref="ChipLineLimit"/>, then "…" if more follow. With <paramref name="valuesAsText"/>, values are
        /// text rather than bubbles.
        /// </summary>
        internal static IReadOnlyList<TextExpanderPreviewSegment> ChipLines(IReadOnlyList<TextExpanderPreviewSegment> segments, bool valuesAsText, Func<string, string> resource)
        {
            // Only the lines from the first that isn't blank, ChipLineLimit of them, are kept: past them, all that matters
            // is whether anything else shows.
            var lines = new List<List<TextExpanderPreviewSegment>> { new() };
            bool more = false;
            foreach (TextExpanderPreviewSegment segment in segments)
            {
                if (segment.Kind == TextExpanderPreviewSegmentKind.LineBreak)
                {
                    if (lines.Count == 1 && IsBlankLine(lines[0]))
                    {
                        lines[0].Clear();
                    }
                    else if (lines.Count <= ChipLineLimit)
                    {
                        lines.Add(new List<TextExpanderPreviewSegment>());
                    }

                    continue;
                }

                TextExpanderPreviewSegment shown = valuesAsText && segment.IsBubble
                    ? new TextExpanderPreviewSegment(TextExpanderPreviewSegmentKind.Text, TextExpanderInlines.ValueText(segment, resource).Label)
                    : segment;
                if (lines.Count > ChipLineLimit)
                {
                    if (!IsBlank(shown))
                    {
                        more = true;
                        break;
                    }
                }
                else if (shown.Kind == TextExpanderPreviewSegmentKind.Text)
                {
                    AddText(lines[^1], shown.Value);
                }
                else
                {
                    lines[^1].Add(shown);
                }
            }

            int count = more ? ChipLineLimit : lines.FindLastIndex(line => !IsBlankLine(line)) + 1;
            var chip = new List<TextExpanderPreviewSegment>();
            for (int index = 0; index < count; index++)
            {
                if (index > 0)
                {
                    chip.Add(new TextExpanderPreviewSegment(TextExpanderPreviewSegmentKind.LineBreak));
                }

                chip.AddRange(lines[index]);
            }

            if (more)
            {
                AddText(chip, " \u2026");
            }

            return chip;
        }

        private static string WarningFor(string trigger, bool hasLaterDuplicate, TextExpanderTriggerSet triggers, bool wordMode, Func<string, string> resource)
        {
            if (hasLaterDuplicate)
            {
                return Format(resource("TextExpander_Row_NeverExpandsDuplicate"), trigger);
            }

            if (TextExpanderTriggerReach.FindBlocker(trigger, triggers, wordMode) is not { } blocker)
            {
                return null;
            }

            if (wordMode)
            {
                return char.IsWhiteSpace(blocker.Next)
                    ? Format(resource("TextExpander_Row_NeverExpandsWhenYouTypeSpace"), blocker.Trigger)
                    : Format(resource("TextExpander_Row_NeverExpandsWhenYouType"), blocker.Trigger, blocker.Next);
            }

            // Say whether waiting for space or punctuation would let it through.
            return TextExpanderTriggerReach.FindBlocker(trigger, triggers, wordMode: true) == null
                ? Format(resource("TextExpander_Row_NeverExpandsFiresFirstWordWait"), blocker.Trigger)
                : Format(resource("TextExpander_Row_NeverExpandsFiresFirst"), blocker.Trigger);
        }

        private static bool IsDateOrPunctuation(TextExpanderPreviewSegment segment) => segment.Kind switch
        {
            TextExpanderPreviewSegmentKind.Text => PunctuationOnly().IsMatch(segment.Value),
            TextExpanderPreviewSegmentKind.Prompt or TextExpanderPreviewSegmentKind.Clipboard or TextExpanderPreviewSegmentKind.LineBreak => false,
            _ => true,
        };

        /// <summary>Gets a value indicating whether a line shows nothing at all: only spaces and tabs.</summary>
        private static bool IsBlankLine(List<TextExpanderPreviewSegment> line) => line.TrueForAll(IsBlank);

        private static bool IsBlank(TextExpanderPreviewSegment segment)
            => segment.Kind == TextExpanderPreviewSegmentKind.Tab
                || (segment.Kind == TextExpanderPreviewSegmentKind.Text && string.IsNullOrWhiteSpace(segment.Value));

        /// <summary>Drops a final period, which the warning's sentence after it would double.</summary>
        private static string WithoutFinalPeriod(string text)
            => text.EndsWith('.') && !text.EndsWith("..", StringComparison.Ordinal) ? text[..^1] : text;

        private static void AddText(List<TextExpanderPreviewSegment> parts, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (parts.Count > 0 && parts[^1].Kind == TextExpanderPreviewSegmentKind.Text)
            {
                parts[^1] = parts[^1] with { Value = parts[^1].Value + text };
            }
            else
            {
                parts.Add(new TextExpanderPreviewSegment(TextExpanderPreviewSegmentKind.Text, text));
            }
        }

        /// <summary>
        /// Reads the saved text aloud, saying what each bubble, line break and tab is. Spaces run together into one, and
        /// reading stops once there's more than a row says.
        /// </summary>
        private static string Spoken(IReadOnlyList<TextExpanderPreviewSegment> segments, Func<string, string> resource)
        {
            var text = new StringBuilder();
            bool space = false;
            foreach (TextExpanderPreviewSegment segment in segments)
            {
                string part = segment.Kind switch
                {
                    TextExpanderPreviewSegmentKind.Text => segment.Value,
                    TextExpanderPreviewSegmentKind.Date or TextExpanderPreviewSegmentKind.Time => " " + segment.Value + " ",
                    TextExpanderPreviewSegmentKind.LineBreak => " (" + resource("TextExpander_Row_SpokenNewLine") + ") ",
                    TextExpanderPreviewSegmentKind.Tab => " (" + resource("TextExpander_Row_SpokenTab") + ") ",
                    TextExpanderPreviewSegmentKind.Clipboard => " (" + resource("TextExpander_Row_SpokenClipboard") + ") ",
                    TextExpanderPreviewSegmentKind.Prompt => " (" + Format(resource("TextExpander_Row_SpokenPrompt"), string.IsNullOrWhiteSpace(segment.Value) ? resource("TextExpander_PreviewPromptUnnamed") : segment.Value) + ") ",
                    _ => " (" + resource("TextExpander_Row_SpokenCursor") + ") ",
                };
                foreach (char character in part ?? string.Empty)
                {
                    if (char.IsWhiteSpace(character))
                    {
                        space = text.Length > 0;
                        continue;
                    }

                    text.Append(space ? " " : string.Empty).Append(character);
                    space = false;
                    if (text.Length > SpokenLimit)
                    {
                        return text.ToString(0, SpokenLimit - 1) + "\u2026";
                    }
                }
            }

            return text.ToString();
        }

        private static string Format(string template, params object[] values)
            => string.Format(CultureInfo.CurrentCulture, template, values);

        [GeneratedRegex(@"^[\s()\[\]{}\-\u2013\u2014,.:;/|]*$")]
        private static partial Regex PunctuationOnly();
    }
}
