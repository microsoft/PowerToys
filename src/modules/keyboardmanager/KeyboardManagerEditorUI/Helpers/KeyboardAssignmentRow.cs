// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel;

namespace KeyboardManagerEditorUI.Helpers
{
    /// <summary>One row in the auto-switch dialog: a detected keyboard and its assigned profile.</summary>
    public sealed class KeyboardAssignmentRow : INotifyPropertyChanged
    {
        private bool _isTyping;
        private string _displayName = string.Empty;

        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (_displayName != value)
                {
                    _displayName = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
                }
            }
        }

        public string DevicePath { get; set; } = string.Empty;

        public IReadOnlyList<ProfileChoice> Profiles { get; set; } = new List<ProfileChoice>();

        /// <summary>
        /// Stable id of the assigned profile, or <see langword="null"/> when unassigned. Bound to the
        /// ComboBox via SelectedValuePath="Id", so the unassigned sentinel is matched by its null id
        /// rather than by comparing against a localized label.
        /// </summary>
        public string? SelectedProfileId { get; set; }

        /// <summary>True while this keyboard is the one currently being typed on (for a highlight).</summary>
        public bool IsTyping
        {
            get => _isTyping;
            set
            {
                if (_isTyping != value)
                {
                    _isTyping = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsTyping)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
