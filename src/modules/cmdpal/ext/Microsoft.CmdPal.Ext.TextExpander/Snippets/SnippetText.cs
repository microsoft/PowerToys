// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.PowerToys.TextExpander;

namespace Microsoft.CmdPal.Ext.TextExpander.Snippets;

/// <summary>
/// Expands snippets with Text Expander's own rules (<c>Variables.ExpandWithPromptRequests</c> in TextExpander.Core),
/// so variables Text Expander learns later work here too. The first <c>$|$</c> is dropped and the caret stays at the
/// end of the text. Previews read the snippet's parts the way Text Expander's snippet editor does
/// (<c>SnippetTemplate.Parse</c>).
/// </summary>
internal static class SnippetText
{
    /// <summary>The longest one-line preview, in characters, before it's cut short.</summary>
    internal const int OneLineLength = 160;

    private static readonly CompositeFormat PlaceholderFormat = CompositeFormat.Parse(Properties.Resources.preview_placeholder_format);

    /// <summary>
    /// Gets the questions a snippet asks, in the order Text Expander asks them. A question that appears twice is asked
    /// twice, as in Text Expander.
    /// </summary>
    public static IReadOnlyList<PromptRequest> FindQuestions(string replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        var questions = new List<PromptRequest>();
        _ = Variables.ExpandWithPromptRequests(
            replacement,
            request =>
            {
                questions.Add(request);
                return string.Empty;
            });
        return questions;
    }

    /// <summary>
    /// Expands a snippet for inserting: dates and times as of now, <paramref name="answers"/> in the order of
    /// <see cref="FindQuestions"/>, and clipboard variables from <paramref name="clipboard"/>, read once and only if one
    /// is used. A part with several matches would take the first, but a snippet that takes part of the copy is
    /// inserted from its pick pages instead (<see cref="Expand(string, SnippetFill)"/>).
    /// </summary>
    public static string Expand(string replacement, IReadOnlyList<string> answers, Func<string?>? clipboard)
        => ExpandWithGaps(replacement, answers, clipboard).Text;

    internal static string Expand(string replacement, IReadOnlyList<string> answers, Func<string?>? clipboard, DateTime now, CultureInfo culture)
        => ExpandChoosing(replacement, answers, clipboard, now, culture, static choice => choice.Options[0]).Text;

    /// <summary>
    /// Expands a snippet with what its pick pages gathered: the answers, the copy they read, and the matches picked, in
    /// the order Text Expander asks for them.
    /// </summary>
    public static string Expand(string replacement, SnippetFill fill)
        => ExpandWithGaps(replacement, fill).Text;

    internal static string Expand(string replacement, SnippetFill fill, DateTime now, CultureInfo culture)
        => ExpandWithGaps(replacement, fill, now, culture).Text;

    /// <summary>
    /// Expands a snippet for pasting, as <see cref="Expand(string, IReadOnlyList{string}, Func{string?})"/> does, with the
    /// spots its clipboard variables left empty (<see cref="ExpandedSnippet.Gaps"/>), so the user can be told.
    /// </summary>
    public static ExpandedSnippet ExpandWithGaps(string replacement, IReadOnlyList<string> answers, Func<string?>? clipboard)
        => ExpandChoosing(replacement, answers, clipboard, DateTime.Now, CultureInfo.CurrentCulture, static choice => choice.Options[0]);

    /// <summary>
    /// Expands a snippet for pasting, as <see cref="Expand(string, SnippetFill)"/> does, with the spots its clipboard
    /// variables left empty (<see cref="ExpandedSnippet.Gaps"/>), so the user can be told.
    /// </summary>
    public static ExpandedSnippet ExpandWithGaps(string replacement, SnippetFill fill)
        => ExpandWithGaps(replacement, fill, DateTime.Now, CultureInfo.CurrentCulture);

    internal static ExpandedSnippet ExpandWithGaps(string replacement, SnippetFill fill, DateTime now, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(fill);

        var next = 0;
        return ExpandChoosing(replacement, fill.Answers, () => fill.Plan.Copy, now, culture, choice => next < fill.Picks.Count ? fill.Picks[next++] : choice.Options[0], fill.Plan.FindParts);
    }

    /// <summary>
    /// Gets a value indicating whether a snippet takes part of what's on the clipboard now, such as
    /// <c>{{clipboard:email}}</c>, which can have several matches to pick from. A copy picked from clipboard history
    /// doesn't count: only Text Expander can insert those (<see cref="TextExpanderOnlyVariables"/>).
    /// </summary>
    public static bool UsesClipboardParts(string replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        return replacement.Contains("{{clipboard:", StringComparison.Ordinal) && SnippetTemplate.Parse(replacement).Any(IsPartOfCopy);
    }

    /// <summary>
    /// Works out what a snippet takes from <paramref name="copy"/> with a dry run of Core's expansion: the choices Text
    /// Expander would ask for, in its order, and the spots Core leaves empty.
    /// </summary>
    public static ClipboardPlan PlanClipboard(string replacement, IReadOnlyList<string> answers, string? copy)
        => PlanClipboard(replacement, answers, copy, DateTime.Now, CultureInfo.CurrentCulture);

    internal static ClipboardPlan PlanClipboard(string replacement, IReadOnlyList<string> answers, string? copy, DateTime now, CultureInfo culture)
    {
        // Filled in before anyone else sees the plan, so the parts found for the dry run are kept for the previews.
        var choices = new List<ClipboardChoice>();
        var gaps = new List<ClipboardGap>();
        var plan = new ClipboardPlan(copy, choices, gaps);
        var expanded = ExpandChoosing(
            replacement,
            answers,
            () => copy,
            now,
            culture,
            choice =>
            {
                choices.Add(choice);
                return choice.Options[0];
            },
            plan.FindParts);

        gaps.AddRange(expanded.Gaps);
        return plan;
    }

    /// <summary>
    /// Expands a snippet for showing: dates and times as they'd come out now, and in brackets, each clipboard variable's
    /// name, such as "Link from clipboard", and each question not answered yet. The clipboard isn't read: a list
    /// shouldn't show what's on it.
    /// </summary>
    public static string Preview(string replacement, IReadOnlyList<string>? answers = null)
        => Preview(replacement, answers, DateTime.Now, CultureInfo.CurrentCulture);

    internal static string Preview(string replacement, IReadOnlyList<string>? answers, DateTime now, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        var text = new StringBuilder(replacement.Length);
        var next = 0;
        foreach (var token in SnippetTemplate.Parse(replacement))
        {
            switch (token.Kind)
            {
                case SnippetTokenKind.Date:
                case SnippetTokenKind.Time:
                    text.Append(Variables.FormatToken(now, token, culture));
                    break;
                case SnippetTokenKind.Prompt:
                    var answer = answers is not null && next < answers.Count ? answers[next] : null;
                    next++;
                    text.Append(string.IsNullOrEmpty(answer) ? Placeholder(QuestionLabel(new PromptRequest(token.Value, token.Required))) : answer);
                    break;
                case SnippetTokenKind.Clipboard:
                    text.Append(Placeholder(ClipboardVariableText.Label(token.Clipboard)));
                    break;
                case SnippetTokenKind.Cursor:
                    break;
                case SnippetTokenKind.LineBreak:
                    text.Append('\n');
                    break;
                case SnippetTokenKind.Tab:
                    text.Append('\t');
                    break;
                default:
                    text.Append(token.Value);
                    break;
            }
        }

        return text.ToString();
    }

    /// <summary>
    /// Shows a snippet on its pick pages, as <see cref="Expand(string, SnippetFill)"/> would insert it, with each choice
    /// not made yet in brackets by name, such as "[Phone number from clipboard]".
    /// </summary>
    public static string Preview(string replacement, SnippetFill fill)
        => Preview(replacement, fill, DateTime.Now, CultureInfo.CurrentCulture);

    internal static string Preview(string replacement, SnippetFill fill, DateTime now, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(fill);

        var next = 0;
        return ExpandChoosing(
            replacement,
            fill.Answers,
            () => fill.Plan.Copy,
            now,
            culture,
            choice => next < fill.Picks.Count ? fill.Picks[next++] : Placeholder(ClipboardVariableText.Label(new ClipboardVariable(choice.FromHistory, choice.Part))),
            fill.Plan.FindParts).Text;
    }

    /// <summary>Gets the words to show for a question: its own, or "Your answer" when it has none, as in Text Expander.</summary>
    public static string QuestionLabel(PromptRequest request)
        => string.IsNullOrWhiteSpace(request.Label) ? Properties.Resources.answers_unnamed_label : request.Label.Trim();

    /// <summary>
    /// Puts <paramref name="text"/> on one line: line breaks, tabs and runs of spaces become one space, and anything
    /// past <paramref name="maxLength"/> characters is cut off with an ellipsis.
    /// </summary>
    public static string OneLine(string text, int maxLength = OneLineLength)
    {
        ArgumentNullException.ThrowIfNull(text);

        var builder = new StringBuilder(Math.Min(text.Length, maxLength) + 1);
        var pendingSpace = false;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(ch);
            if (builder.Length > maxLength)
            {
                break;
            }
        }

        if (builder.Length > maxLength)
        {
            builder.Length = maxLength;

            // Never leave half of a surrogate pair before the ellipsis.
            if (char.IsHighSurrogate(builder[^1]))
            {
                builder.Length--;
            }

            builder.Append('\u2026');
        }

        return builder.ToString();
    }

    private static string Placeholder(string label)
        => string.Format(CultureInfo.CurrentCulture, PlaceholderFormat, label);

    /// <summary>
    /// Expands with Core. Each question takes the next of <paramref name="answers"/>, and Core asks
    /// <paramref name="choose"/> only when a spot has two or more options. No clipboard history reader is given: only
    /// Text Expander inserts snippets that pick from history, so here Core would leave those spots empty.
    /// <paramref name="findParts"/>, when given, finds the parts of the copy in place of Core.
    /// </summary>
    private static ExpandedSnippet ExpandChoosing(string replacement, IReadOnlyList<string> answers, Func<string?>? clipboard, DateTime now, CultureInfo culture, Func<ClipboardChoice, string> choose, Func<string?, ClipboardPart, CultureInfo, IReadOnlyList<string>>? findParts = null)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        ArgumentNullException.ThrowIfNull(answers);

        var next = 0;
        return Variables.ExpandWithPromptRequests(
            replacement,
            _ => next < answers.Count ? answers[next++] : string.Empty,
            clipboard,
            now,
            culture,
            choose,
            findParts: findParts);
    }

    // A part of what's on the clipboard now, such as {{clipboard:email}}: not all of it, and not a copy from history.
    private static bool IsPartOfCopy(SnippetToken token)
        => token.Kind == SnippetTokenKind.Clipboard && !token.Clipboard.PickCopy && token.Clipboard.Part != ClipboardPart.All;
}
