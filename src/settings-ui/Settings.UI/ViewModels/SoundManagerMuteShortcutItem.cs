// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    // A row in the list of the mute shortcuts. The icon of the program cannot be saved with the settings, so the row holds it.
    public sealed class SoundManagerMuteShortcutItem : Observable
    {
        private readonly SoundManagerMuteShortcut _muteShortcut;

        private IconElement _icon;

        public SoundManagerMuteShortcutItem(SoundManagerMuteShortcut shortcut)
        {
            _muteShortcut = shortcut;
        }

        public string Name => _muteShortcut.Name;

        public string Program => _muteShortcut.Program;

        public string ProcessPath => _muteShortcut.ProcessPath;

        // The shortcut in the settings that this row belongs to.
        public SoundManagerMuteShortcut MuteShortcut => _muteShortcut;

        public HotkeySettings Hotkey
        {
            get => _muteShortcut.Shortcut;
            set
            {
                if (_muteShortcut.Shortcut != value)
                {
                    _muteShortcut.Shortcut = value;
                    OnPropertyChanged();
                }
            }
        }

        public IconElement Icon
        {
            get => _icon;
            set => Set(ref _icon, value);
        }
    }
}
