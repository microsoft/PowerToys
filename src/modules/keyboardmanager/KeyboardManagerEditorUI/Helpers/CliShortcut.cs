// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel;

namespace KeyboardManagerEditorUI.Helpers
{
    /// <summary>A hotkey referencing a shared built-in CLI definition and its own parameter values.</summary>
    public sealed class CliShortcut : IToggleableShortcut, INotifyPropertyChanged
    {
        private string _commandName = string.Empty;
        private bool _isActive;

        public event PropertyChangedEventHandler? PropertyChanged;

        public List<string> Shortcut { get; set; } = new();

        public string CommandId { get; set; } = string.Empty;

        public string CommandName
        {
            get => _commandName;
            set
            {
                if (_commandName != value)
                {
                    _commandName = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CommandName)));
                }
            }
        }

        public Dictionary<string, string?> Parameters { get; set; } = new();

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
                }
            }
        }

        public string Id { get; set; } = string.Empty;

        public string AppName { get; set; } = string.Empty;
    }
}
