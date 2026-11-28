// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Microsoft.PowerToys.TextExpander.Editor.Helpers
{
    /// <summary>
    /// The snippet editor's words for clipboard variables: what a bubble, a row and a screen reader call one, such as
    /// "Link from clipboard", and finding the one at the caret for the Clipboard panel.
    /// </summary>
    internal static class TextExpanderClipboardText
    {
        /// <summary>Gets the parts Insert variable and the Clipboard panel offer, in their order.</summary>
        public static IReadOnlyList<ClipboardPart> Parts { get; } = Array.AsReadOnly<ClipboardPart>([ClipboardPart.Email, ClipboardPart.Link, ClipboardPart.Phone, ClipboardPart.Number]);

        /// <summary>
        /// Gets what a bubble shows for <paramref name="variable"/>: "Clipboard" for the whole copy, or the part, from the
        /// clipboard or from clipboard history, such as "Link from clipboard".
        /// </summary>
        public static string Label(ClipboardVariable variable, Func<string, string> resource)
        {
            ArgumentNullException.ThrowIfNull(resource);
            variable = variable.Normalized();
            if (variable.Part == ClipboardPart.All)
            {
                return resource(variable.PickCopy ? "TextExpander_ClipboardWhole_History" : "TextExpander_PreviewClipboard");
            }

            string what = resource("TextExpander_ClipboardWhat_" + variable.Part);
            return string.Format(CultureInfo.CurrentCulture, resource(variable.PickCopy ? "TextExpander_ClipboardFrom_History" : "TextExpander_ClipboardFrom_Now"), what);
        }

        /// <summary>Describes a clipboard bubble for screen readers, such as "Link from clipboard, filled in when the snippet runs".</summary>
        public static string Description(ClipboardVariable variable, Func<string, string> resource)
            => variable.Normalized() == default
                ? resource("TextExpander_PreviewClipboard_Description")
                : string.Format(CultureInfo.CurrentCulture, resource("TextExpander_PreviewValue_Description"), Label(variable, resource));

        /// <summary>Gets what a screen reader says in a row for <paramref name="variable"/>, in place of its text.</summary>
        public static string Spoken(ClipboardVariable variable, Func<string, string> resource)
            => variable.Normalized() == default ? resource("TextExpander_Row_SpokenClipboard") : Label(variable, resource);

        /// <summary>
        /// Gets the hint for <paramref name="part"/> that Insert variable shows by it, with examples that the rules find and
        /// don't, written as <paramref name="culture"/> writes numbers, such as "7 to 15 digits in groups, such as
        /// (425) 555-0123, or all together after a +.".
        /// </summary>
        public static string Hint(ClipboardPart part, CultureInfo culture, Func<string, string> resource)
        {
            ArgumentNullException.ThrowIfNull(resource);
            IReadOnlyList<ClipboardPartExample> examples = ClipboardPartExamples.For(part, culture);
            string counts = examples.First(example => example.Counts).Text;
            string doesnt = examples.First(example => !example.Counts).Text;
            return string.Format(CultureInfo.CurrentCulture, resource("TextExpander_ClipboardHint_" + part), counts, doesnt);
        }

        /// <summary>Gets the heading over what counts as <paramref name="part"/> in the Clipboard panel, such as "Counts as a phone number".</summary>
        public static string CountsHeading(ClipboardPart part, Func<string, string> resource) => resource("TextExpander_ClipboardExamples_Counts_" + part);

        /// <summary>Gets why <paramref name="example"/> doesn't count, such as "add a + or spaces", or null for one that counts.</summary>
        public static string Why(ClipboardPartExample example, Func<string, string> resource)
            => example?.Why is { } why ? resource("TextExpander_ClipboardExample_" + why) : null;

        /// <summary>
        /// Gets what a screen reader says for <paramref name="example"/>: "Counts: (425) 555-0123", or "Doesn't count:
        /// 4255550123, add a + or spaces", since the check or cross beside it isn't read.
        /// </summary>
        public static string Spoken(ClipboardPartExample example, Func<string, string> resource)
        {
            ArgumentNullException.ThrowIfNull(example);
            return example.Counts
                ? string.Format(CultureInfo.CurrentCulture, resource("TextExpander_ClipboardExample_CountsName"), example.Text)
                : string.Format(CultureInfo.CurrentCulture, resource("TextExpander_ClipboardExample_DoesntCountName"), example.Text, Why(example, resource));
        }

        /// <summary>Gets whether <paramref name="text"/> has a variable that picks a copy from clipboard history when it runs.</summary>
        public static bool PicksFromHistory(string text)
        {
            text ??= string.Empty;
            if (!text.Contains("{{clipboard:pick", StringComparison.Ordinal))
            {
                return false;
            }

            foreach ((int start, int end) in TextExpanderTokenText.FindVariables(text))
            {
                if (ClipboardVariable.TryParse(text[start..end], out ClipboardVariable variable) && variable.PickCopy)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds the clipboard variable that the selection is, or that the caret is inside, as clicking or F2 on it does, to
        /// change it in the Clipboard panel.
        /// </summary>
        public static bool TryFindAtSelection(string text, int selectionStart, int selectionEnd, out int tokenStart, out int tokenEnd, out ClipboardVariable variable)
        {
            text ??= string.Empty;
            int start = Math.Min(selectionStart, selectionEnd);
            int end = Math.Max(selectionStart, selectionEnd);
            foreach ((int variableStart, int variableEnd) in TextExpanderTokenText.FindVariables(text))
            {
                bool selected = start == variableStart && end == variableEnd;
                bool caretInside = start == end && variableStart < start && start < variableEnd;
                if ((selected || caretInside) && ClipboardVariable.TryParse(text[variableStart..variableEnd], out variable))
                {
                    tokenStart = variableStart;
                    tokenEnd = variableEnd;
                    return true;
                }
            }

            tokenStart = tokenEnd = 0;
            variable = default;
            return false;
        }
    }
}
