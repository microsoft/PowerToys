// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace KeyboardManagerEditorUI.Helpers
{
    public partial class Remapping : INotifyPropertyChanged, IToggleableShortcut
    {
        public List<string> Shortcut { get; set; } = new List<string>();

        public List<string> RemappedKeys { get; set; } = new List<string>();

        public bool IsAllApps { get; set; } = true;

        public string AppName { get; set; } = string.Empty;

        private bool IsEnabledValue { get; set; } = true;

        public string Id { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool IsEnabled
        {
            get => IsEnabledValue;
            set
            {
                if (IsEnabledValue != value)
                {
                    IsEnabledValue = value;
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
