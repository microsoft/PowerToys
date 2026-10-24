// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>
    /// Undo and redo for the snippet editor's replacement box, kept as whole texts.
    ///
    /// <para>
    /// The box colors its variables by formatting the text, and RichEdit's own undo would record
    /// every recoloring as a step, so Ctrl+Z would mostly undo colors. This history only sees the
    /// text. Typing and deleting are grouped about a word at a time, as in other Windows editors.
    /// </para>
    /// </summary>
    internal sealed class TextExpanderEditHistory
    {
        private const int Limit = 200;

        private readonly List<string> _states = new();
        private int _index;
        private Run _run;
        private int _runPosition;
        private bool _runEndsInWhiteSpace;

        public TextExpanderEditHistory(string text = "")
        {
            Reset(text);
        }

        private enum Run
        {
            None,
            Typing,
            Deleting,
        }

        /// <summary>Gets the text the history is at.</summary>
        public string Current => _states[_index];

        public bool CanUndo => _index > 0;

        public bool CanRedo => _index < _states.Count - 1;

        /// <summary>Starts again from <paramref name="text"/>, with nothing to undo or redo.</summary>
        public void Reset(string text)
        {
            _states.Clear();
            _states.Add(text ?? string.Empty);
            _index = 0;
            _run = Run.None;
        }

        /// <summary>Ends the current run of typing or deleting, so the next change is undone on its own.</summary>
        public void EndRun() => _run = Run.None;

        /// <summary>Records the box's text after a change.</summary>
        public void Record(string text)
        {
            text ??= string.Empty;
            if (string.Equals(text, Current, StringComparison.Ordinal))
            {
                return;
            }

            (int start, int removed, string inserted) = TextExpanderTokenText.Diff(Current, text);
            if (CanRedo)
            {
                _states.RemoveRange(_index + 1, _states.Count - _index - 1);
                _run = Run.None;
            }

            Run run = Run.None;
            bool whiteSpace = false;
            if (removed == 0 && inserted.Length == 1)
            {
                run = Run.Typing;
                whiteSpace = char.IsWhiteSpace(inserted[0]);
            }
            else if (removed == 1 && inserted.Length == 0)
            {
                run = Run.Deleting;
            }

            // Typing carries on a run while it continues from where the run stopped, until a new
            // word starts. Deleting carries on backwards (Backspace) or in place (Delete).
            bool continues = _index > 0 && run != Run.None && run == _run && run switch
            {
                Run.Typing => start == _runPosition && (whiteSpace || !_runEndsInWhiteSpace),
                Run.Deleting => start == _runPosition || start == _runPosition - 1,
                _ => false,
            };

            if (continues)
            {
                _states[_index] = text;
            }
            else
            {
                _states.Add(text);
                _index++;
                if (_states.Count > Limit)
                {
                    _states.RemoveAt(0);
                    _index--;
                }
            }

            _run = run;
            _runPosition = run == Run.Typing ? start + 1 : start;
            _runEndsInWhiteSpace = whiteSpace;
        }

        /// <summary>
        /// Steps back. The selection covers any text the undo put back, or is a caret where text
        /// was taken out.
        /// </summary>
        public bool TryUndo(out string text, out int selectionStart, out int selectionEnd)
        {
            if (!CanUndo)
            {
                text = Current;
                selectionStart = selectionEnd = 0;
                return false;
            }

            string undone = Current;
            _index--;
            text = Current;
            (int start, _, string restored) = TextExpanderTokenText.Diff(undone, text);
            selectionStart = start;
            selectionEnd = start + restored.Length;
            _run = Run.None;
            return true;
        }

        /// <summary>Steps forward again, leaving the caret after the change.</summary>
        public bool TryRedo(out string text, out int selectionStart, out int selectionEnd)
        {
            if (!CanRedo)
            {
                text = Current;
                selectionStart = selectionEnd = 0;
                return false;
            }

            string previous = Current;
            _index++;
            text = Current;
            (int start, _, string redone) = TextExpanderTokenText.Diff(previous, text);
            selectionStart = selectionEnd = start + redone.Length;
            _run = Run.None;
            return true;
        }
    }
}
