// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>One row in the snippet list.</summary>
    public sealed class TextExpanderSnippetViewModel : Observable
    {
        private string _trigger;
        private string _replacement;
        private string _summary;
        private TextExpanderRowDescription _description;
        private double _keycapColumnWidth = TextExpanderKeycapLayout.MinWidth;
        private bool _caseSensitive = true;
        private string _caseVariantAutomationName = string.Empty;

        public TextExpanderSnippetViewModel(int id, string trigger, string replacement)
        {
            Id = id;
            _trigger = trigger;
            _replacement = replacement;
        }

        /// <summary>Gets or sets the width of the list's keycap column, the same on every row so descriptions line up.</summary>
        public double KeycapColumnWidth
        {
            get => _keycapColumnWidth;
            set
            {
                if (_keycapColumnWidth != value)
                {
                    _keycapColumnWidth = value;
                    OnPropertyChanged(nameof(KeycapColumnWidth));
                }
            }
        }

        /// <summary>
        /// Gets or sets what the row shows besides its trigger. Set by the page's view model, which
        /// knows the other triggers and the matching mode.
        /// </summary>
        public TextExpanderRowDescription Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged(nameof(Description));
                OnPropertyChanged(nameof(Sentence));
                OnPropertyChanged(nameof(SentenceParts));
                OnPropertyChanged(nameof(HasSentence));
                OnPropertyChanged(nameof(ShowsSavedTextAsTitle));
                OnPropertyChanged(nameof(ShowsSavedTextBelow));
                OnPropertyChanged(nameof(Segments));
                OnPropertyChanged(nameof(TitleSegments));
                OnPropertyChanged(nameof(Warning));
                OnPropertyChanged(nameof(HasWarning));
                OnPropertyChanged(nameof(AutomationName));
            }
        }

        /// <summary>Gets the plain sentence shown for a dynamic snippet, or empty.</summary>
        public string Sentence => _description?.Sentence ?? string.Empty;

        public bool HasSentence => _description?.Sentence != null;

        /// <summary>Gets a value indicating whether the saved text itself is the row's title, as it is for plain text.</summary>
        public bool ShowsSavedTextAsTitle => !HasSentence;

        /// <summary>Gets a value indicating whether the saved text goes under the sentence, which it does only when it adds something.</summary>
        public bool ShowsSavedTextBelow => HasSentence && _description.ShowsSegmentsBelow;

        /// <summary>Gets the saved text, with the editor's bubbles.</summary>
        public IReadOnlyList<TextExpanderPreviewSegment> Segments => _description?.Segments ?? Array.Empty<TextExpanderPreviewSegment>();

        /// <summary>Gets the sentence with its variables as bubbles.</summary>
        public IReadOnlyList<TextExpanderPreviewSegment> SentenceParts => _description?.SentenceParts ?? Array.Empty<TextExpanderPreviewSegment>();

        /// <summary>Gets the saved text as the title, on up to two lines, for a snippet without a sentence.</summary>
        public IReadOnlyList<TextExpanderPreviewSegment> TitleSegments => _description?.TitleSegments ?? Array.Empty<TextExpanderPreviewSegment>();

        /// <summary>Gets why the snippet never expands, or empty.</summary>
        public string Warning => _description?.Warning ?? string.Empty;

        public bool HasWarning => _description?.Warning != null;

        /// <summary>Gets what a screen reader says for the row: the trigger, what it does, and any warning.</summary>
        public string AutomationName => string.IsNullOrEmpty(_caseVariantAutomationName)
            ? _description?.AutomationName ?? _trigger
            : string.Concat(_description?.AutomationName ?? _trigger, ". ", _caseVariantAutomationName);

        public bool CaseSensitive
        {
            get => _caseSensitive;
            set
            {
                if (_caseSensitive != value)
                {
                    _caseSensitive = value;
                    OnPropertyChanged(nameof(CaseSensitive));
                    OnPropertyChanged(nameof(TriggerDisplay));
                    OnPropertyChanged(nameof(ShowsCaseVariants));
                }
            }
        }

        public string CaseVariantAutomationName
        {
            get => _caseVariantAutomationName;
            set
            {
                value ??= string.Empty;
                if (_caseVariantAutomationName != value)
                {
                    _caseVariantAutomationName = value;
                    OnPropertyChanged(nameof(CaseVariantAutomationName));
                    OnPropertyChanged(nameof(AutomationName));
                }
            }
        }

        public bool ShowsCaseVariants => !CaseSensitive
            && !string.Equals(_trigger.ToLowerInvariant(), _trigger.ToUpperInvariant(), StringComparison.Ordinal);

        public string TriggerDisplay => ShowsCaseVariants
            ? string.Concat(_trigger.ToLowerInvariant(), " / ", _trigger.ToUpperInvariant())
            : _trigger;

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
                    OnPropertyChanged(nameof(TriggerDisplay));
                    OnPropertyChanged(nameof(ShowsCaseVariants));
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
                    _summary = null;
                    OnPropertyChanged(nameof(Replacement));
                    OnPropertyChanged(nameof(Summary));
                }
            }
        }

        /// <summary>
        /// Gets the replacement with line breaks visible; the row limits its visual preview to two
        /// lines. Kept until the replacement changes, because the search reads it for every row on
        /// every keystroke.
        /// </summary>
        public string Summary => _summary ??= (_replacement ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\t", "\t", StringComparison.Ordinal);

        public bool HasChanges(string trigger, string replacement)
            => !string.Equals(Trigger, trigger, StringComparison.Ordinal)
                || !string.Equals(
                    NormaliseNewlines(Replacement),
                    NormaliseNewlines(replacement),
                    StringComparison.Ordinal);

        private static string NormaliseNewlines(string text)
            => (text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }
}
