// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts
{
    public partial class HotkeyConflictGroupData : INotifyPropertyChanged
    {
        private bool _conflictIgnored;
        private bool _isSystemConflict;

        public HotkeyData Hotkey { get; set; }

        public bool IsSystemConflict
        {
            get => _isSystemConflict;
            set
            {
                if (_isSystemConflict != value)
                {
                    _isSystemConflict = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ShouldShowSysConflict));
                }
            }
        }

        public bool ConflictIgnored
        {
            get => _conflictIgnored;
            set
            {
                if (_conflictIgnored != value)
                {
                    _conflictIgnored = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ConflictVisible));
                    OnPropertyChanged(nameof(ShouldShowSysConflict));
                }
            }
        }

        public bool ConflictVisible => !ConflictIgnored;

        public bool ShouldShowSysConflict => !ConflictIgnored && IsSystemConflict;

        public List<ModuleHotkeyData> Modules { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
