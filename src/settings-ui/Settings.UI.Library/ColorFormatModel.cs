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
        private bool _isNew;
        private bool _isValid = true;
        private bool _isUserDefined;

        public ColorFormatModel(string name, string example, bool isShown, bool isUserDefined)
        {
            Name = name;
            Example = example;
            IsShown = isShown;
            IsUserDefined = isUserDefined;
            IsNew = false;
        }

        public ColorFormatModel()
        {
            Example = "Color ( R =%Re, G = %Gr, B = %Bl)";
            IsShown = true;
            IsNew = true;
            IsUserDefined = true;
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

        public bool IsNew
        {
            get
            {
                return _isNew;
            }

            set
            {
                _isNew = value;
                OnPropertyChanged();
            }
        }

        public bool IsValid
        {
            get
            {
                return _isValid;
            }

            set
            {
                _isValid = value;
                OnPropertyChanged();
            }
        }

        public bool IsUserDefined
        {
            get
            {
                return _isUserDefined;
            }

            set
            {
                _isUserDefined = value;
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
