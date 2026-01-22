// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KeyboardManagerEditorUI.Helpers
{
    /// <summary>
    /// Represents a key-to-mouse remapping configuration.
    /// </summary>
    public partial class KeyToMouseMapping : INotifyPropertyChanged
    {
        /// <summary>
        /// The original key code.
        /// </summary>
        public int OriginalKeyCode { get; set; }

        /// <summary>
        /// The original key display name.
        /// </summary>
        public string OriginalKeyName { get; set; } = string.Empty;

        /// <summary>
        /// The target mouse button (Left, Right, Middle, X1, X2).
        /// </summary>
        public string TargetMouseButton { get; set; } = string.Empty;

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

        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
