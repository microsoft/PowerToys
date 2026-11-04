// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Microsoft.PowerToys.TextExpander.Editor.Helpers
{
    /// <summary>
    /// The snippet editor's words for clipboard variables: what a bubble, a row and a screen reader call one, such as
    /// "Link 2 from clipboard", and finding the one at the caret for the Clipboard panel.
    /// </summary>
    internal static class TextExpanderClipboardText
    {
        /// <summary>Gets the parts Insert variable and the Clipboard panel offer, in their order.</summary>
        public static IReadOnlyList<ClipboardPart> Parts { get; } = [ClipboardPart.Email, ClipboardPart.Link, ClipboardPart.Phone, ClipboardPart.Number];

        /// <summary>
        /// Gets what a bubble shows for <paramref name="variable"/>: "Clipboard" for the whole copy, or the part and which
        /// of its matches, from the clipboard or a copy picked from history, such as "Link 2 from clipboard".
        /// </summary>
        public static string Label(ClipboardVariable variable, Func<string, string> resource)
        {
            ArgumentNullException.ThrowIfNull(resource);
            variable = variable.Normalized();
            if (variable.Part == ClipboardPart.All)
            {
                return resource(variable.PickCopy ? "TextExpander_ClipboardWhole_History" : "TextExpander_PreviewClipboard");
            }

            string what = resource("TextExpander_ClipboardWhat_" + variable.Part + variable.Match switch
            {
                ClipboardMatch.Numbered => "_Numbered",
                ClipboardMatch.Last => "_Last",
                ClipboardMatch.All => "_All",
                ClipboardMatch.Pick => "_Pick",
                _ => string.Empty,
            });
            if (variable.Match == ClipboardMatch.Numbered)
            {
                what = string.Format(CultureInfo.CurrentCulture, what, variable.Index);
            }

            return string.Format(CultureInfo.CurrentCulture, resource(variable.PickCopy ? "TextExpander_ClipboardFrom_History" : "TextExpander_ClipboardFrom_Now"), what);
        }

        /// <summary>Describes a clipboard bubble for screen readers, such as "Link 2 from clipboard, filled in when the snippet runs".</summary>
        public static string Description(ClipboardVariable variable, Func<string, string> resource)
            => variable.Normalized() == default
                ? resource("TextExpander_PreviewClipboard_Description")
                : string.Format(CultureInfo.CurrentCulture, resource("TextExpander_PreviewValue_Description"), Label(variable, resource));

        /// <summary>Gets what a screen reader says in a row for <paramref name="variable"/>, in place of its text.</summary>
        public static string Spoken(ClipboardVariable variable, Func<string, string> resource)
            => variable.Normalized() == default ? resource("TextExpander_Row_SpokenClipboard") : Label(variable, resource);

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
