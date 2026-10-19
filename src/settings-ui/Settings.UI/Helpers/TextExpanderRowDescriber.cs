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
    /// Describes a snippet for its row in the list: a plain sentence for a dynamic snippet, with
    /// its variables in the editor's bubbles, the saved text, and why it never expands.
    ///
    /// <para>
    /// Sentences come from whole localized templates rather than pieced-together fragments, and
    /// only for shapes that are recognized with certainty. A template's placeholders become slots
    /// for text or bubbles, so a translation can order them as its language needs. Anything else
    /// shows the saved text, because a wrong sentence is worse than none.
    /// </para>
    /// </summary>
    internal static partial class TextExpanderRowDescriber
    {
        /// <summary>The longest link shown in full. Longer ones keep the host and the end of the path.</summary>
        internal const int LinkBudget = 60;

        private const int SpokenLimit = 170;

        private static readonly string[] Schemes = { "https://", "http://" };

        private static readonly char[] SurroundingPunctuation = { ' ', '(', ')', '[', ']', '{', '}', '-', '\u2013', '\u2014', ',', '.', ':', ';', '/', '|' };

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

            var labels = new List<string>();
            bool unnamedQuestion = false;
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
                        if (string.IsNullOrWhiteSpace(segment.Value))
                        {
                            unnamedQuestion = true;
                        }
                        else if (!labels.Contains(segment.Value, StringComparer.Ordinal))
                        {
                            labels.Add(segment.Value);
                        }

                        break;
                }
            }

            string host = segments.Count > 0 && segments[0].Kind == TextExpanderPreviewSegmentKind.Text && LinkStart().IsMatch(segments[0].Value)
                ? ExtractHost(segments[0].Value)
                : null;
            bool asks = labels.Count > 0 || unnamedQuestion;
            bool dateOnly = dates && !clipboard && !asks && lines == 1 && segments.All(IsDateOrPunctuation);

            List<TextExpanderPreviewSegment> sentenceParts = null;
            if (clipboard || asks || dates)
            {
                sentenceParts = host != null
                    ? LinkSentence(host, clipboard, labels, unnamedQuestion, resource)
                    : OtherSentence(segments, dateOnly, onlyTimes, clipboard, labels, unnamedQuestion, lines, resource);
            }

            string sentence = sentenceParts == null ? null : string.Concat(sentenceParts.Select(part => part.Value));
            IReadOnlyList<TextExpanderPreviewSegment> shown = sentence != null && host != null ? WithShortLink(segments) : segments;

            string warning = WarningFor(trigger, hasLaterDuplicate, triggers, wordMode, resource);

            // The saved text goes under a sentence only when it says more: words or numbers of its
            // own, beyond the variables the sentence already shows.
            bool showSegmentsBelow = sentence != null && segments.Any(HasWordCharacters);

            // Without a sentence, the saved text is the title.
            IReadOnlyList<TextExpanderPreviewSegment> title = sentence == null ? TitleLines(segments) : Array.Empty<TextExpanderPreviewSegment>();

            // A row shows two lines at most, so the name says how many there are, unless the sentence does.
            bool sentenceCountsLines = sentence != null && host == null && lines > 1;
            string spoken = sentence ?? Spoken(segments, resource);
            if (lines > 1 && !sentenceCountsLines && spoken.Length > 0)
            {
                spoken = Format(resource("TextExpander_Row_SpokenLines"), spoken, lines);
            }

            string automationName = spoken.Length == 0
                ? (warning == null ? trigger : Format(resource("TextExpander_Row_AutomationName"), trigger, warning))
                : warning == null
                    ? Format(resource("TextExpander_Row_AutomationName"), trigger, spoken)
                    : Format(resource("TextExpander_Row_AutomationNameWarning"), trigger, spoken, warning);

            return new TextExpanderRowDescription(
                sentence,
                (IReadOnlyList<TextExpanderPreviewSegment>)sentenceParts ?? Array.Empty<TextExpanderPreviewSegment>(),
                shown,
                title,
                showSegmentsBelow,
                warning,
                automationName);
        }

        /// <summary>
        /// The saved text as a row's title: its first two lines that have something on them, then
        /// "…" if more follow. A blank line, such as the one between a message and its sign-off,
        /// would otherwise use up the row.
        /// </summary>
        internal static IReadOnlyList<TextExpanderPreviewSegment> TitleLines(IReadOnlyList<TextExpanderPreviewSegment> segments)
        {
            var lines = new List<List<TextExpanderPreviewSegment>> { new() };
            foreach (TextExpanderPreviewSegment segment in segments)
            {
                if (segment.Kind == TextExpanderPreviewSegmentKind.LineBreak)
                {
                    lines.Add(new List<TextExpanderPreviewSegment>());
                }
                else
                {
                    lines[^1].Add(segment);
                }
            }

            List<List<TextExpanderPreviewSegment>> filled = lines.Where(line => !line.All(IsBlank)).ToList();
            var title = new List<TextExpanderPreviewSegment>();
            for (int i = 0; i < Math.Min(2, filled.Count); i++)
            {
                if (i > 0)
                {
                    title.Add(new TextExpanderPreviewSegment(TextExpanderPreviewSegmentKind.LineBreak));
                }

                title.AddRange(filled[i]);
            }

            if (filled.Count > 2)
            {
                title.Add(new TextExpanderPreviewSegment(TextExpanderPreviewSegmentKind.Text, "\u2026"));
            }

            return title;
        }

        /// <summary>Gets the host a link goes to, or null when there isn't a clear one.</summary>
        internal static string ExtractHost(string text)
        {
            Match match = HostPattern().Match(text ?? string.Empty);
            if (!match.Success)
            {
                return null;
            }

            string host = match.Groups[1].Value;
            int userInfo = host.LastIndexOf('@');
            if (userInfo >= 0)
            {
                host = host[(userInfo + 1)..];
            }

            return host.Length == 0 ? null : host;
        }

        /// <summary>
        /// Drops a link's http:// or https:// and, when it is still longer than
        /// <paramref name="budget"/>, keeps the host and the end of the path, which is the part that
        /// tells links apart: <c>contoso.com/…/Forms/report</c>. Text after the link is kept.
        /// </summary>
        internal static string ShortenLink(string text, int budget)
        {
            text ??= string.Empty;
            int start = 0;
            while (start < text.Length && char.IsWhiteSpace(text[start]))
            {
                start++;
            }

            int scheme = SchemeLength(text, start);
            int end = start + scheme;
            while (end < text.Length && !char.IsWhiteSpace(text[end]))
            {
                end++;
            }

            string url = text[(start + scheme)..end];
            if (url.Length > budget)
            {
                int slash = url.IndexOf('/');
                if (slash > 0)
                {
                    string host = url[..slash];
                    string path = url[slash..];
                    int room = Math.Max(1, budget - host.Length - 2);
                    string tail = null;
                    for (int index = path.IndexOf('/', 1); index >= 0; index = path.IndexOf('/', index + 1))
                    {
                        if (path.Length - index <= room)
                        {
                            tail = path[index..];
                            break;
                        }
                    }

                    url = host + "/\u2026" + (tail ?? path[^Math.Min(room, path.Length)..]);
                }
            }

            return text[..start] + url + text[end..];
        }

        private static int SchemeLength(string text, int start)
        {
            foreach (string scheme in Schemes)
            {
                if (string.Compare(text, start, scheme, 0, scheme.Length, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return scheme.Length;
                }
            }

            return 0;
        }

        private static List<TextExpanderPreviewSegment> LinkSentence(string host, bool clipboard, List<string> labels, bool unnamedQuestion, Func<string, string> resource)
        {
            if (unnamedQuestion)
            {
                return null;
            }

            List<TextExpanderPreviewSegment> site = TextPart(host);
            return (clipboard, labels.Count > 0) switch
            {
                (true, true) => Fill(resource("TextExpander_Row_LinkClipboardPrompts"), site, ClipboardBubble(resource), LabelBubbles(labels, resource)),
                (true, false) => Fill(resource("TextExpander_Row_LinkClipboard"), site, ClipboardBubble(resource)),
                (false, true) => Fill(resource("TextExpander_Row_LinkPrompts"), site, LabelBubbles(labels, resource)),
                _ => Fill(resource("TextExpander_Row_Link"), site),
            };
        }

        private static List<TextExpanderPreviewSegment> OtherSentence(
            IReadOnlyList<TextExpanderPreviewSegment> segments,
            bool dateOnly,
            bool onlyTimes,
            bool clipboard,
            List<string> labels,
            bool unnamedQuestion,
            int lines,
            Func<string, string> resource)
        {
            if (dateOnly)
            {
                // One bubble for the value, without the brackets or punctuation around it.
                string value = string.Concat(segments.Select(s => s.Kind == TextExpanderPreviewSegmentKind.Tab ? " " : s.Value)).Trim().Trim(SurroundingPunctuation);
                if (value.Length == 0)
                {
                    return null;
                }

                var bubble = new List<TextExpanderPreviewSegment>
                {
                    new(onlyTimes ? TextExpanderPreviewSegmentKind.Time : TextExpanderPreviewSegmentKind.Date, value),
                };
                return Fill(resource(onlyTimes ? "TextExpander_Row_Time" : "TextExpander_Row_Date"), bubble);
            }

            string count = lines.ToString(CultureInfo.CurrentCulture);
            if (labels.Count > 0 || unnamedQuestion)
            {
                if (unnamedQuestion)
                {
                    return null;
                }

                return lines > 1
                    ? Fill(resource("TextExpander_Row_PromptsLines"), LabelBubbles(labels, resource), TextPart(count))
                    : Fill(resource("TextExpander_Row_Prompts"), LabelBubbles(labels, resource));
            }

            if (clipboard)
            {
                return lines > 1
                    ? Fill(resource("TextExpander_Row_ClipboardLines"), ClipboardBubble(resource), TextPart(count))
                    : Fill(resource("TextExpander_Row_Clipboard"), ClipboardBubble(resource));
            }

            // Dates mixed into other text: the saved text with its bubbles says it better.
            return null;
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

            // Say whether waiting for the whole word would let it through.
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

        private static bool HasWordCharacters(TextExpanderPreviewSegment segment)
            => segment.Kind == TextExpanderPreviewSegmentKind.Text && segment.Value.Any(char.IsLetterOrDigit);

        /// <summary>Gets a value indicating whether a piece of a line shows nothing worth a row's space.</summary>
        private static bool IsBlank(TextExpanderPreviewSegment segment) => segment.Kind switch
        {
            TextExpanderPreviewSegmentKind.Text => string.IsNullOrWhiteSpace(segment.Value),
            TextExpanderPreviewSegmentKind.Tab or TextExpanderPreviewSegmentKind.Cursor => true,
            _ => false,
        };

        private static IReadOnlyList<TextExpanderPreviewSegment> WithShortLink(IReadOnlyList<TextExpanderPreviewSegment> segments)
        {
            var shown = segments.ToList();
            shown[0] = shown[0] with { Value = ShortenLink(shown[0].Value, LinkBudget) };
            return shown;
        }

        /// <summary>Each label in a bubble, joined as the language joins a list: "A", "A and B", "A, B and C".</summary>
        private static List<TextExpanderPreviewSegment> LabelBubbles(List<string> labels, Func<string, string> resource)
        {
            var last = new List<TextExpanderPreviewSegment> { new(TextExpanderPreviewSegmentKind.Prompt, labels[^1]) };
            if (labels.Count == 1)
            {
                return last;
            }

            var head = new List<TextExpanderPreviewSegment>();
            for (int i = 0; i < labels.Count - 1; i++)
            {
                if (i > 0)
                {
                    AddText(head, resource("TextExpander_Row_JoinSeparator"));
                }

                head.Add(new TextExpanderPreviewSegment(TextExpanderPreviewSegmentKind.Prompt, labels[i]));
            }

            return Fill(resource("TextExpander_Row_JoinPair"), head, last);
        }

        /// <summary>A bubble for the clipboard that reads "what you copied".</summary>
        private static List<TextExpanderPreviewSegment> ClipboardBubble(Func<string, string> resource)
            => new() { new(TextExpanderPreviewSegmentKind.Clipboard, resource("TextExpander_Row_SpokenClipboard")) };

        private static List<TextExpanderPreviewSegment> TextPart(string text)
            => new() { new(TextExpanderPreviewSegmentKind.Text, text) };

        /// <summary>
        /// Fills a localized template's {0}, {1}… with text or bubbles. The template stays whole,
        /// so each language keeps its own word order around the slots.
        /// </summary>
        private static List<TextExpanderPreviewSegment> Fill(string template, params List<TextExpanderPreviewSegment>[] values)
        {
            template ??= string.Empty;
            var parts = new List<TextExpanderPreviewSegment>();
            int at = 0;
            foreach (Match slot in Slot().Matches(template))
            {
                AddText(parts, template[at..slot.Index]);
                int index = int.Parse(slot.Groups[1].Value, CultureInfo.InvariantCulture);
                if (index < values.Length && values[index] != null)
                {
                    foreach (TextExpanderPreviewSegment part in values[index])
                    {
                        if (part.Kind == TextExpanderPreviewSegmentKind.Text)
                        {
                            AddText(parts, part.Value);
                        }
                        else
                        {
                            parts.Add(part);
                        }
                    }
                }

                at = slot.Index + slot.Length;
            }

            AddText(parts, template[at..]);
            return parts;
        }

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

        /// <summary>Reads the saved text aloud, saying what each bubble, line break and tab is.</summary>
        private static string Spoken(IReadOnlyList<TextExpanderPreviewSegment> segments, Func<string, string> resource)
        {
            var text = new StringBuilder();
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
                text.Append(part);
            }

            string spoken = WhiteSpaceRun().Replace(text.ToString(), " ").Trim();
            return spoken.Length > SpokenLimit ? spoken[..(SpokenLimit - 1)] + "\u2026" : spoken;
        }

        private static string Format(string template, params object[] values)
            => string.Format(CultureInfo.CurrentCulture, template, values);

        [GeneratedRegex(@"^\s*(?:https?://|www\.)\S", RegexOptions.IgnoreCase)]
        private static partial Regex LinkStart();

        [GeneratedRegex(@"^\s*(?:https?://|(?=www\.))([^/\s?#]+)", RegexOptions.IgnoreCase)]
        private static partial Regex HostPattern();

        [GeneratedRegex(@"^[\s()\[\]{}\-\u2013\u2014,.:;/|]*$")]
        private static partial Regex PunctuationOnly();

        [GeneratedRegex(@"\s+")]
        private static partial Regex WhiteSpaceRun();

        [GeneratedRegex(@"\{(\d+)\}")]
        private static partial Regex Slot();
    }
}
