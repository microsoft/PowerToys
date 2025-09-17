// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TopToolbar.Models
{
    public enum ToolbarGroupLayoutStyle
    {
        Capsule,
        Icon,
        Text,
        Mixed,
    }

    public enum ToolbarGroupOverflowMode
    {
        Menu,
        Wrap,
        Hidden,
    }

    public class ToolbarGroupLayout : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private ToolbarGroupLayoutStyle _style = ToolbarGroupLayoutStyle.Capsule;

        public ToolbarGroupLayoutStyle Style
        {
            get => _style;
            set => SetProperty(ref _style, value);
        }

        private ToolbarGroupOverflowMode _overflow = ToolbarGroupOverflowMode.Menu;

        public ToolbarGroupOverflowMode Overflow
        {
            get => _overflow;
            set => SetProperty(ref _overflow, value);
        }

        private int? _maxInline;

        public int? MaxInline
        {
            get => _maxInline;
            set => SetProperty(ref _maxInline, value);
        }

        private bool _showLabels = true;

        public bool ShowLabels
        {
            get => _showLabels;
            set => SetProperty(ref _showLabels, value);
        }

        private void SetProperty<T>(ref T storage, T value, [CallerMemberName] string name = null)
        {
            if (!Equals(storage, value))
            {
                storage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
