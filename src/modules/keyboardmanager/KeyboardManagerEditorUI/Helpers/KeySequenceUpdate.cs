// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace KeyboardManagerEditorUI.Helpers
{
    internal readonly struct KeySequenceUpdate
    {
        public KeySequenceUpdate(bool isValid, KeySequenceError error, int resultCount)
        {
            IsValid = isValid;
            Error = error;
            ResultCount = resultCount;
        }

        public bool IsValid { get; }

        public KeySequenceError Error { get; }

        public int ResultCount { get; }

        public static KeySequenceUpdate Valid(int resultCount) => new(true, KeySequenceError.None, resultCount);

        public static KeySequenceUpdate Invalid(KeySequenceError error) => new(false, error, 0);
    }
}
