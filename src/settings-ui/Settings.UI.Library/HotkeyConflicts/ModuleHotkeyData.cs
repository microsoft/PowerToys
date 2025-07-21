// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts
{
    public class ModuleHotkeyData : INotifyPropertyChanged
    {
        private string _moduleName;
        private string _hotkeyName;
        private HotkeySettings _hotkeySettings;
        private bool _isSystemConflict;

        public event PropertyChangedEventHandler PropertyChanged;

        public string IconPath => GetModuleIconPath(ModuleName);

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

        public string HotkeyName
        {
            get => _hotkeyName;
            set
            {
                if (_hotkeyName != value)
                {
                    _hotkeyName = value;
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

        private static string GetModuleIconPath(string moduleName)
        {
            return moduleName?.ToLowerInvariant() switch
            {
                "advancedpaste" => "ms-appx:///Assets/Settings/Icons/AdvancedPaste.png",
                "alwaysontop" => "ms-appx:///Assets/Settings/Icons/AlwaysOnTop.png",
                "colorpicker" => "ms-appx:///Assets/Settings/Icons/ColorPicker.png",
                "cropandlock" => "ms-appx:///Assets/Settings/Icons/CropAndLock.png",
                "fancyzones" => "ms-appx:///Assets/Settings/Icons/FancyZones.png",
                "mousehighlighter" => "ms-appx:///Assets/Settings/Icons/MouseHighlighter.png",
                "mousepointercrosshairs" => "ms-appx:///Assets/Settings/Icons/MouseCrosshairs.png",
                "findmymouse" => "ms-appx:///Assets/Settings/Icons/FindMyMouse.png",
                "mousejump" => "ms-appx:///Assets/Settings/Icons/MouseJump.png",
                "peek" => "ms-appx:///Assets/Settings/Icons/Peek.png",
                "powerlauncher" => "ms-appx:///Assets/Settings/Icons/PowerToysRun.png",
                "measuretool" => "ms-appx:///Assets/Settings/Icons/ScreenRuler.png",
                "shortcutguide" => "ms-appx:///Assets/Settings/Icons/ShortcutGuide.png",
                "powerocr" => "ms-appx:///Assets/Settings/Icons/TextExtractor.png",
                "workspaces" => "ms-appx:///Assets/Settings/Icons/Workspaces.png",
                "cmdpal" => "ms-appx:///Assets/Settings/Icons/CmdPal.png",
                "mousewithoutborders" => "ms-appx:///Assets/Settings/Icons/MouseWithoutBorders.png",
                "zoomit" => "ms-appx:///Assets/Settings/Icons/ZoomIt.png",
                "measure tool" => "ms-appx:///Assets/Settings/Icons/ScreenRuler.png",
                _ => "ms-appx:///Assets/Settings/Icons/PowerToys.png",
            };
        }
    }
}
