// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KeyboardManagerEditorUI.Helpers
{
    /// <summary>
    /// Represents a mouse button remapping configuration.
    /// </summary>
    public partial class MouseMapping : INotifyPropertyChanged
    {
        /// <summary>
        /// The original mouse button (Left, Right, Middle, X1, X2).
        /// </summary>
        public string OriginalButton { get; set; } = string.Empty;

        /// <summary>
        /// The target type: "Key", "Shortcut", "Text", "RunProgram", "OpenUri".
        /// </summary>
        public string TargetType { get; set; } = "Key";

        /// <summary>
        /// Target key code (for Key type).
        /// </summary>
        public int TargetKeyCode { get; set; }

        /// <summary>
        /// Target key display name (for Key type).
        /// </summary>
        public string TargetKeyName { get; set; } = string.Empty;

        /// <summary>
        /// Target shortcut keys (for Shortcut type), semicolon-separated.
        /// </summary>
        public string TargetShortcutKeys { get; set; } = string.Empty;

        /// <summary>
        /// Target text (for Text type).
        /// </summary>
        public string TargetText { get; set; } = string.Empty;

        /// <summary>
        /// Program path (for RunProgram type).
        /// </summary>
        public string ProgramPath { get; set; } = string.Empty;

        /// <summary>
        /// Program arguments (for RunProgram type).
        /// </summary>
        public string ProgramArgs { get; set; } = string.Empty;

        /// <summary>
        /// URI to open (for OpenUri type).
        /// </summary>
        public string UriToOpen { get; set; } = string.Empty;

        private bool _isEnabled = true;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets a display string for the target.
        /// </summary>
        public string TargetDisplayName
        {
            get
            {
                return TargetType switch
                {
                    "Key" => TargetKeyName,
                    "Shortcut" => TargetShortcutKeys,
                    "Text" => $"\"{TargetText}\"",
                    "RunProgram" => ProgramPath,
                    "OpenUri" => UriToOpen,
                    _ => string.Empty,
                };
            }
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
