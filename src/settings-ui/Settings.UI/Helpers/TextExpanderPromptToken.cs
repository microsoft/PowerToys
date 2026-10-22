// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>Builds and reads the prompt variables supported by the Text Expander engine.</summary>
    internal static class TextExpanderPromptToken
    {
        private const string RequiredPrefix = "{{prompt:";
        private const string OptionalPrefix = "{{prompt?:";
        private const string Closing = "}}";

        public const string ProvisionalQuestion = "Question";

        public static string ProvisionalToken => Build(ProvisionalQuestion, requireAnswer: true);

        public static TextExpanderPromptValidationError Validate(string question)
        {
            question = question?.Trim() ?? string.Empty;
            if (question.Length == 0)
            {
                return TextExpanderPromptValidationError.QuestionRequired;
            }

            return question.Contains('}')
                ? TextExpanderPromptValidationError.InvalidQuestion
                : TextExpanderPromptValidationError.None;
        }

        public static string Build(string question, bool requireAnswer)
        {
            question = question?.Trim() ?? string.Empty;
            TextExpanderPromptValidationError error = Validate(question);
            if (error != TextExpanderPromptValidationError.None)
            {
                throw new ArgumentException("A prompt question must be non-empty and cannot contain a closing brace.", nameof(question));
            }

            return (requireAnswer ? RequiredPrefix : OptionalPrefix) + question + Closing;
        }

        public static bool TryParse(string token, out string question, out bool requireAnswer)
        {
            token ??= string.Empty;
            string prefix;
            if (token.StartsWith(OptionalPrefix, StringComparison.Ordinal))
            {
                prefix = OptionalPrefix;
                requireAnswer = false;
            }
            else if (token.StartsWith(RequiredPrefix, StringComparison.Ordinal))
            {
                prefix = RequiredPrefix;
                requireAnswer = true;
            }
            else
            {
                question = string.Empty;
                requireAnswer = true;
                return false;
            }

            if (!token.EndsWith(Closing, StringComparison.Ordinal) || token.Length < prefix.Length + Closing.Length)
            {
                question = string.Empty;
                return false;
            }

            question = token.Substring(prefix.Length, token.Length - prefix.Length - Closing.Length);
            return !question.Contains('}');
        }

        public static bool TryFindAtSelection(string text, int selectionStart, int selectionEnd, out int tokenStart, out int tokenEnd)
        {
            text ??= string.Empty;
            int start = Math.Min(selectionStart, selectionEnd);
            int end = Math.Max(selectionStart, selectionEnd);
            foreach ((int variableStart, int variableEnd) in TextExpanderTokenText.FindVariables(text))
            {
                bool selected = start == variableStart && end == variableEnd;
                bool caretInside = start == end && variableStart < start && start < variableEnd;
                if ((selected || caretInside)
                    && TryParse(text[variableStart..variableEnd], out _, out _))
                {
                    tokenStart = variableStart;
                    tokenEnd = variableEnd;
                    return true;
                }
            }

            tokenStart = tokenEnd = 0;
            return false;
        }
    }
}
