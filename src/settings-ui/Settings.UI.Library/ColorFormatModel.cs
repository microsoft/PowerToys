// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using ManagedCommon;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class ColorFormatModel : INotifyPropertyChanged
    {
        private string _name;
        private string _format;
        private bool _isShown;
        private bool _canMoveUp = true;
        private bool _canMoveDown = true;
        private bool _canBeDeleted = true;
        private bool _isNew;
        private bool _isValid = true;

        public ColorFormatModel(string name, string format, bool isShown)
        {
            Name = name;
            Format = format;
            IsShown = isShown;
            IsNew = false;
        }

        public ColorFormatModel()
        {
            Format = "new Color (R = %Re, G = %Gr, B = %Bl)";
            IsShown = true;
            IsNew = true;
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
                OnPropertyChanged(nameof(Name));
            }
        }

        public string Format
        {
            get
            {
                return _format;
            }

            set
            {
                _format = value;
                OnPropertyChanged(nameof(Format));
                OnPropertyChanged(nameof(Example));
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
                OnPropertyChanged(nameof(IsShown));
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
                if (value != _canMoveUp)
                {
                    _canMoveUp = value;
                    OnPropertyChanged(nameof(CanMoveUp));
                }
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
                if (value != _canMoveDown)
                {
                    _canMoveDown = value;
                    OnPropertyChanged(nameof(CanMoveDown));
                }
            }
        }

        public bool CanBeDeleted
        {
            get
            {
                return _canBeDeleted;
            }

            set
            {
                if (value != _canBeDeleted)
                {
                    _canBeDeleted = value;
                    OnPropertyChanged(nameof(CanBeDeleted));
                }
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
                OnPropertyChanged(nameof(IsNew));
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
                OnPropertyChanged(nameof(IsValid));
            }
        }

        public string Example
        {
            get
            {
                // get string representation in 2 steps. First replace all color specific number values then in 2nd step replace color name with localisation
                return Helpers.ColorNameHelper.ReplaceName(ColorFormatHelper.GetStringRepresentation(null, _format), null);
            }

            set
            {
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
