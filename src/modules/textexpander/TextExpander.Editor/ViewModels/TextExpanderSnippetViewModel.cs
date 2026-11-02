// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.TextExpander.Editor.Helpers;

namespace Microsoft.PowerToys.TextExpander.Editor.ViewModels
{
    /// <summary>One row in the snippet list.</summary>
    public sealed class TextExpanderSnippetViewModel : Observable
    {
        private string _trigger;
        private string _replacement;
        private string _summary;
        private TextExpanderRowDescription _description;
        private bool _caseSensitive = true;
        private string _caseVariantAutomationName = string.Empty;

        public TextExpanderSnippetViewModel(int id, string trigger, string replacement)
        {
            Id = id;
            _trigger = trigger;
            _replacement = replacement;
        }

        /// <summary>
        /// Gets or sets what the row shows besides its trigger. Set by the page's view model, which knows the other
        /// triggers and the matching mode.
        /// </summary>
        public TextExpanderRowDescription Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged(nameof(Description));
                OnPropertyChanged(nameof(KindGlyph));
                OnPropertyChanged(nameof(LeadSegments));
                OnPropertyChanged(nameof(ChipSegments));
                OnPropertyChanged(nameof(Warning));
                OnPropertyChanged(nameof(HasWarning));
                OnPropertyChanged(nameof(AutomationName));
            }
        }

        /// <summary>Gets the icon of the chip that shows what the snippet inserts.</summary>
        public string KindGlyph => _description?.KindGlyph ?? TextExpanderRowDescriber.TextGlyph;

        /// <summary>Gets the words before the chip: "inserts", or the questions the snippet asks first, as bubbles.</summary>
        public IReadOnlyList<TextExpanderPreviewSegment> LeadSegments => _description?.Lead ?? Array.Empty<TextExpanderPreviewSegment>();

        /// <summary>Gets what the snippet inserts, as its real lines with bubbles: at most three, then "…".</summary>
        public IReadOnlyList<TextExpanderPreviewSegment> ChipSegments => _description?.Chip ?? Array.Empty<TextExpanderPreviewSegment>();

        /// <summary>Gets why the snippet never expands, or empty.</summary>
        public string Warning => _description?.Warning ?? string.Empty;

        public bool HasWarning => _description?.Warning != null;

        /// <summary>Gets what a screen reader says for the row: the trigger, its text, and any warning.</summary>
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
        /// Gets the stable handle for the underlying entry. Rows are addressed by this, never by trigger, because a
        /// file may contain duplicate triggers.
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
        /// Gets the replacement with \n line breaks, for the search. Kept until the replacement changes, because the
        /// search reads it for every row on every keystroke.
        /// </summary>
        public string Summary => _summary ??= NormaliseNewlines(_replacement);

        /// <summary>Gets <see cref="AutomationName"/>, which the list reads as the row's name.</summary>
        public override string ToString() => AutomationName;

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
