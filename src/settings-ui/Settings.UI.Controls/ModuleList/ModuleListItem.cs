// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public class ModuleListItem : INotifyPropertyChanged
    {
        private bool _isEnabled;
        private string _label = string.Empty;
        private string _icon = string.Empty;
        private bool _isNew;
        private bool _isLocked;
        private object? _tag;
        private ICommand? _clickCommand;

        public virtual string Label
        {
            get => _label;
            set
            {
                if (_label != value)
                {
                    _label = value;
                    OnPropertyChanged();
                }
            }
        }

        public virtual string Icon
        {
            get => _icon;
            set
            {
                if (_icon != value)
                {
                    _icon = value;
                    OnPropertyChanged();
                }
            }
        }

        public virtual bool IsNew
        {
            get => _isNew;
            set
            {
                if (_isNew != value)
                {
                    _isNew = value;
                    OnPropertyChanged();
                }
            }
        }

        public virtual bool IsLocked
        {
            get => _isLocked;
            set
            {
                if (_isLocked != value)
                {
                    _isLocked = value;
                    OnPropertyChanged();
                }
            }
        }

        public virtual bool IsEnabled
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

        public virtual object? Tag
        {
            get => _tag;
            set
            {
                if (_tag != value)
                {
                    _tag = value;
                    OnPropertyChanged();
                }
            }
        }

        public virtual ICommand? ClickCommand
        {
            get => _clickCommand;
            set
            {
                if (_clickCommand != value)
                {
                    _clickCommand = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
