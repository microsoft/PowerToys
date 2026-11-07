// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander.Editor.Helpers
{
    /// <summary>The result of trying to close a prompt-token editing session.</summary>
    internal sealed class TextExpanderPromptEditDecision
    {
        public TextExpanderPromptEditDecision(
            TextExpanderPromptEditDecisionKind kind,
            TextExpanderPromptValidationError error,
            string text,
            int selectionStart,
            int selectionEnd,
            int tokenStart,
            int tokenEnd)
        {
            Kind = kind;
            Error = error;
            Text = text;
            SelectionStart = selectionStart;
            SelectionEnd = selectionEnd;
            TokenStart = tokenStart;
            TokenEnd = tokenEnd;
        }

        public TextExpanderPromptEditDecisionKind Kind { get; }

        public TextExpanderPromptValidationError Error { get; }

        public string Text { get; }

        public int SelectionStart { get; }

        public int SelectionEnd { get; }

        public int TokenStart { get; }

        public int TokenEnd { get; }
    }
}
