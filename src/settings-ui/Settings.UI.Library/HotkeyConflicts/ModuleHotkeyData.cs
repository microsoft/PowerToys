// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Windows.Web.AtomPub;

namespace Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts
{
    public class ModuleHotkeyData : INotifyPropertyChanged
    {
        private string _moduleName;
        private int _hotkeyID;
        private HotkeySettings _hotkeySettings;
        private bool _isSystemConflict;

        public event PropertyChangedEventHandler PropertyChanged;

        public string IconPath { get; set; }

        public string DisplayName { get; set; }

        public string Header { get; set; }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string ModuleName
        {
            get => _moduleName;
            set
            {
                if (_moduleName != value)
                {
                    _moduleName = value;
                }
            }
        }

        public int HotkeyID
        {
            get => _hotkeyID;
            set
            {
                if (_hotkeyID != value)
                {
                    _hotkeyID = value;
                }
            }
        }

        public HotkeySettings HotkeySettings
        {
            get => _hotkeySettings;
            set
            {
                if (_hotkeySettings != value)
                {
                    _hotkeySettings = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsSystemConflict
        {
            get => _isSystemConflict;
            set
            {
                if (_isSystemConflict != value)
                {
                    _isSystemConflict = value;
                }
            }
        }

        public ModuleType ModuleType { get; set; }
    }
}
