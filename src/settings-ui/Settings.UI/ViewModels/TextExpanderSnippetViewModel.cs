// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>One row in the snippet list.</summary>
    public sealed class TextExpanderSnippetViewModel : Observable
    {
        private string _trigger;
        private string _replacement;

        public TextExpanderSnippetViewModel(int id, string trigger, string replacement)
        {
            Id = id;
            _trigger = trigger;
            _replacement = replacement;
        }

        /// <summary>
        /// Gets the stable handle for the underlying entry.
        ///
        /// <para>
        /// Rows are addressed by this, never by trigger. A file may legitimately contain two rows
        /// with the same trigger, and keying on the trigger meant editing or deleting the second
        /// row silently hit the first.
        /// </para>
        /// </summary>
        public int Id { get; }

        public string Trigger
        {
            get => _trigger;
            set
            {
                if (_trigger != value)
                {
                    _trigger = value;
                    OnPropertyChanged(nameof(Trigger));
                }
            }
        }

        public string Replacement
        {
            get => _replacement;
            set
            {
                if (_replacement != value)
                {
                    _replacement = value;
                    OnPropertyChanged(nameof(Replacement));
                    OnPropertyChanged(nameof(Summary));
                }
            }
        }

        /// <summary>
        /// Gets a one-line description of the replacement.
        ///
        /// <para>
        /// A multi-line snippet is shown as its first line plus a count rather than with the
        /// newlines flattened out, so a row cannot be mistaken for the whole snippet.
        /// </para>
        /// </summary>
        public string Summary
        {
            get
            {
                string text = _replacement ?? string.Empty;
                string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

                if (lines.Length <= 1)
                {
                    return text;
                }

                return string.Format(
                    CultureInfo.CurrentCulture,
                    "{0}  (+{1} more {2})",
                    lines[0],
                    lines.Length - 1,
                    lines.Length - 1 == 1 ? "line" : "lines");
            }
        }
    }
}
