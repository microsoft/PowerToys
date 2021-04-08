// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class ColorFormatModel : INotifyPropertyChanged
    {
        private string _name;
        private string _example;
        private bool _isShown;
        private bool _canMoveUp = true;
        private bool _canMoveDown = true;

        public ColorFormatModel(string name, string example, bool isShown)
        {
            Name = name;
            Example = example;
            IsShown = isShown;
        }

        public string Name
        {
            get
            {
                return _name;
            }

            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        public string Example
        {
            get
            {
                return _example;
            }

            set
            {
                _example = value;
                OnPropertyChanged();
            }
        }

        public bool IsShown
        {
            get
            {
                return _isShown;
            }

            set
            {
                _isShown = value;
                OnPropertyChanged();
            }
        }

        public bool CanMoveUp
        {
            get
            {
                return _canMoveUp;
            }

            set
            {
                _canMoveUp = value;
                OnPropertyChanged();
            }
        }

        public bool CanMoveDown
        {
            get
            {
                return _canMoveDown;
            }

            set
            {
                _canMoveDown = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
