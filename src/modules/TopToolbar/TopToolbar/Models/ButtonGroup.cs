// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TopToolbar.Models
{
    public class ButtonGroup : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private string _id = System.Guid.NewGuid().ToString();

        public string Id
        {
            get => _id;
            set
            {
                if (_id != value)
                {
                    _id = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _name = string.Empty;

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _description = string.Empty;

        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isEnabled = true;

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

        private ToolbarGroupLayout _layout = new ToolbarGroupLayout();

        public ToolbarGroupLayout Layout
        {
            get => _layout;
            set
            {
                if (!Equals(_layout, value))
                {
                    _layout = value ?? new ToolbarGroupLayout();
                    OnPropertyChanged();
                }
            }
        }

        private ObservableCollection<string> _providers = new ObservableCollection<string>();

        public ObservableCollection<string> Providers
        {
            get => _providers;
            set
            {
                if (!ReferenceEquals(_providers, value))
                {
                    _providers = value ?? new ObservableCollection<string>();
                    OnPropertyChanged();
                }
            }
        }

        private ObservableCollection<string> _staticActions = new ObservableCollection<string>();

        public ObservableCollection<string> StaticActions
        {
            get => _staticActions;
            set
            {
                if (!ReferenceEquals(_staticActions, value))
                {
                    _staticActions = value ?? new ObservableCollection<string>();
                    OnPropertyChanged();
                }
            }
        }

        private string _filter = string.Empty;

        public string Filter
        {
            get => _filter;
            set
            {
                if (_filter != value)
                {
                    _filter = value ?? string.Empty;
                    OnPropertyChanged();
                }
            }
        }

        private bool _autoRefresh;

        public bool AutoRefresh
        {
            get => _autoRefresh;
            set
            {
                if (_autoRefresh != value)
                {
                    _autoRefresh = value;
                    OnPropertyChanged();
                }
            }
        }

        private ObservableCollection<ToolbarButton> _buttons = new ObservableCollection<ToolbarButton>();

        public ObservableCollection<ToolbarButton> Buttons
        {
            get => _buttons;
            set
            {
                if (!ReferenceEquals(_buttons, value))
                {
                    _buttons = value ?? new ObservableCollection<ToolbarButton>();
                    OnPropertyChanged();
                }
            }
        }
    }
}
