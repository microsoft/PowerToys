// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.PowerToys.TextExpander.Editor.Helpers
{
    /// <summary>
    /// Holds a prompt edit apart from the flyout. A new prompt is shown as a syntactically safe
    /// provisional token, but that token is never a valid commit until the user enters a question.
    /// </summary>
    internal sealed class TextExpanderPromptEditSession
    {
        private readonly string _originalText;
        private readonly int _replaceStart;
        private readonly int _replaceEnd;
        private readonly int _originalSelectionStart;
        private readonly int _originalSelectionEnd;

        private TextExpanderPromptEditSession(
            string originalText,
            int replaceStart,
            int replaceEnd,
            int originalSelectionStart,
            int originalSelectionEnd,
            string initialText,
            int tokenStart,
            int tokenEnd,
            string question,
            bool requireAnswer,
            bool isNew)
        {
            _originalText = originalText;
            _replaceStart = replaceStart;
            _replaceEnd = replaceEnd;
            _originalSelectionStart = originalSelectionStart;
            _originalSelectionEnd = originalSelectionEnd;
            InitialText = initialText;
            TokenStart = tokenStart;
            TokenEnd = tokenEnd;
            Question = question;
            RequireAnswer = requireAnswer;
            IsNew = isNew;
        }

        public string InitialText { get; }

        public int TokenStart { get; }

        public int TokenEnd { get; }

        public string Question { get; }

        public bool RequireAnswer { get; }

        public bool IsNew { get; }

        /// <summary>Begins a new prompt at the selection, shown as a token that asks <paramref name="example"/> until a question is typed.</summary>
        public static TextExpanderPromptEditSession BeginNew(string text, int selectionStart, int selectionEnd, string example)
        {
            text ??= string.Empty;
            (int start, int end) = TextExpanderTokenText.InsertionRange(text, selectionStart, selectionEnd);
            string provisional = TextExpanderPromptToken.Provisional(example, requireAnswer: true);
            return new TextExpanderPromptEditSession(
                text,
                start,
                end,
                Math.Min(selectionStart, selectionEnd),
                Math.Max(selectionStart, selectionEnd),
                Replace(text, start, end, provisional),
                start,
                start + provisional.Length,
                string.Empty,
                requireAnswer: true,
                isNew: true);
        }

        public static bool TryBeginExisting(string text, int selectionStart, int selectionEnd, out TextExpanderPromptEditSession session)
        {
            text ??= string.Empty;
            if (!TextExpanderPromptToken.TryFindAtSelection(text, selectionStart, selectionEnd, out int start, out int end)
                || !TextExpanderPromptToken.TryParse(text[start..end], out string question, out bool requireAnswer))
            {
                session = null;
                return false;
            }

            session = new TextExpanderPromptEditSession(
                text,
                start,
                end,
                start,
                end,
                text,
                start,
                end,
                question,
                requireAnswer,
                isNew: false);
            return true;
        }

        public TextExpanderPromptEditDecision Resolve(TextExpanderPromptEditCloseReason reason, string question, bool requireAnswer)
        {
            if (reason == TextExpanderPromptEditCloseReason.Cancel)
            {
                int cancelSelectionStart = IsNew ? _originalSelectionStart : _replaceStart;
                int cancelSelectionEnd = IsNew ? _originalSelectionEnd : _replaceEnd;
                return new TextExpanderPromptEditDecision(
                    TextExpanderPromptEditDecisionKind.Cancel,
                    TextExpanderPromptValidationError.None,
                    _originalText,
                    cancelSelectionStart,
                    cancelSelectionEnd,
                    _replaceStart,
                    _replaceEnd);
            }

            TextExpanderPromptValidationError error = TextExpanderPromptToken.Validate(question);
            if (error != TextExpanderPromptValidationError.None)
            {
                return new TextExpanderPromptEditDecision(
                    TextExpanderPromptEditDecisionKind.KeepOpen,
                    error,
                    InitialText,
                    TokenStart,
                    TokenEnd,
                    TokenStart,
                    TokenEnd);
            }

            if (!IsNew
                && string.Equals(question, Question, StringComparison.Ordinal)
                && requireAnswer == RequireAnswer)
            {
                return new TextExpanderPromptEditDecision(
                    TextExpanderPromptEditDecisionKind.Commit,
                    TextExpanderPromptValidationError.None,
                    _originalText,
                    _replaceStart,
                    _replaceEnd,
                    _replaceStart,
                    _replaceEnd);
            }

            string token = TextExpanderPromptToken.Build(question, requireAnswer);
            string text = Replace(_originalText, _replaceStart, _replaceEnd, token);
            int selectionStart = IsNew ? _replaceStart + token.Length : _replaceStart;
            int selectionEnd = IsNew ? selectionStart : _replaceStart + token.Length;
            return new TextExpanderPromptEditDecision(
                TextExpanderPromptEditDecisionKind.Commit,
                TextExpanderPromptValidationError.None,
                text,
                selectionStart,
                selectionEnd,
                _replaceStart,
                _replaceStart + token.Length);
        }

        /// <summary>
        /// Gets the replacement as it reads while the question is typed: with the token as Add would write it, or
        /// with the provisional token, asking <paramref name="example"/>, while the question can't be written.
        /// Drafts never reach the undo history.
        /// </summary>
        public (string Text, int TokenStart, int TokenEnd) Draft(string question, bool requireAnswer, string example)
        {
            TextExpanderPromptEditDecision decision = Resolve(TextExpanderPromptEditCloseReason.Enter, question, requireAnswer);
            if (decision.Kind == TextExpanderPromptEditDecisionKind.Commit)
            {
                return (decision.Text, decision.TokenStart, decision.TokenEnd);
            }

            string token = TextExpanderPromptToken.Provisional(example, requireAnswer);
            return (Replace(_originalText, _replaceStart, _replaceEnd, token), _replaceStart, _replaceStart + token.Length);
        }

        private static string Replace(string text, int start, int end, string replacement)
            => text[..start] + replacement + text[end..];
    }
}
