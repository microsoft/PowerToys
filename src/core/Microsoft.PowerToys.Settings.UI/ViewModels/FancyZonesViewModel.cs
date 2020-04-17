// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.ViewModels.Commands;
using Microsoft.PowerToys.Settings.UI.Views;
using Windows.UI;
using Windows.UI.Popups;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class FancyZonesViewModel : Observable
    {
        private const string ModuleName = "FancyZones";

        public ButtonClickCommand LaunchEditorEventHandler { get; set; }

        public ICommand SaveColorChoiceEventHandler
        {
            get
            {
                return new RelayCommand<Color>(SaveColorChoice);
            }
        }

        private FancyZonesSettings Settings { get; set; }

        public FancyZonesViewModel()
        {
            try
            {
                Settings = SettingsUtils.GetSettings<FancyZonesSettings>(ModuleName);
            }
            catch
            {
                Settings = new FancyZonesSettings();
                SettingsUtils.SaveSettings(Settings.ToJsonString(), ModuleName);
            }

            this.LaunchEditorEventHandler = new ButtonClickCommand(LaunchEditor);

            this._shiftDrag = Settings.Properties.FancyzonesShiftDrag.Value;
            this._overrideSnapHotkeys = Settings.Properties.FancyzonesOverrideSnapHotkeys.Value;
            this._flashZones = Settings.Properties.FancyzonesZoneSetChangeFlashZones.Value;
            this._displayChangemoveWindows = Settings.Properties.FancyzonesDisplayChangeMoveWindows.Value;
            this._zoneSetChangeMoveWindows = Settings.Properties.FancyzonesZoneSetChangeMoveWindows.Value;
            this._virtualDesktopChangeMoveWindows = Settings.Properties.FancyzonesVirtualDesktopChangeMoveWindows.Value;
            this._appLastZoneMoveWindows = Settings.Properties.FancyzonesAppLastZoneMoveWindows.Value;
            this._useCursorPosEditorStartupScreen = Settings.Properties.UseCursorposEditorStartupscreen.Value;
            this._showOnAllMonitors = Settings.Properties.FancyzonesShowOnAllMonitors.Value;
            this._zoneHighlightColor = Settings.Properties.FancyzonesZoneHighlightColor.Value;
            this._highlightOpacity = Settings.Properties.FancyzonesHighlightOpacity.Value;
            this._excludedApps = Settings.Properties.FancyzonesExcludedApps.Value;
            this._editorHotkey = Settings.Properties.FancyzonesEditorHotkey.Value;

            GeneralSettings generalSettings = SettingsUtils.GetSettings<GeneralSettings>(string.Empty);
            this._isEnabled = generalSettings.Enabled.FancyZones;
        }

        private bool _isEnabled;
        private bool _shiftDrag;
        private bool _overrideSnapHotkeys;
        private bool _flashZones;
        private bool _displayChangemoveWindows;
        private bool _zoneSetChangeMoveWindows;
        private bool _virtualDesktopChangeMoveWindows;
        private bool _appLastZoneMoveWindows;
        private bool _useCursorPosEditorStartupScreen;
        private bool _showOnAllMonitors;
        private string _zoneHighlightColor;
        private int _highlightOpacity;
        private string _excludedApps;
        private HotkeySettings _editorHotkey;

        public bool IsEnabled
        {
            get
            {
                return _isEnabled;
            }

            set
            {
                if (value != _isEnabled)
                {
                    _isEnabled = value;
                    GeneralSettings generalSettings = SettingsUtils.GetSettings<GeneralSettings>(string.Empty);
                    generalSettings.Enabled.FancyZones = value;
                    OutGoingGeneralSettings snd = new OutGoingGeneralSettings(generalSettings);

                    ShellPage.DefaultSndMSGCallback(snd.ToString());
                    RaisePropertyChanged();
                }
            }
        }

        public bool ShiftDrag
        {
            get
            {
                return _shiftDrag;
            }

            set
            {
                if (value != _shiftDrag)
                {
                    _shiftDrag = value;
                    Settings.Properties.FancyzonesShiftDrag.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool OverrideSnapHotkeys
        {
            get
            {
                return _overrideSnapHotkeys;
            }

            set
            {
                if (value != _overrideSnapHotkeys)
                {
                    _overrideSnapHotkeys = value;
                    Settings.Properties.FancyzonesOverrideSnapHotkeys.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool ZoneSetChangeFlashZones
        {
            get
            {
                return _flashZones;
            }

            set
            {
                if (value != _flashZones)
                {
                    _flashZones = value;
                    Settings.Properties.FancyzonesZoneSetChangeFlashZones.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool DisplayChangeMoveWindows
        {
            get
            {
                return _displayChangemoveWindows;
            }

            set
            {
                if (value != _displayChangemoveWindows)
                {
                    _displayChangemoveWindows = value;
                    Settings.Properties.FancyzonesDisplayChangeMoveWindows.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool ZoneSetChangeMoveWindows
        {
            get
            {
                return _zoneSetChangeMoveWindows;
            }

            set
            {
                if (value != _zoneSetChangeMoveWindows)
                {
                    _zoneSetChangeMoveWindows = value;
                    Settings.Properties.FancyzonesZoneSetChangeMoveWindows.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool VirtualDesktopChangeMoveWindows
        {
            get
            {
                return _virtualDesktopChangeMoveWindows;
            }

            set
            {
                if (value != _virtualDesktopChangeMoveWindows)
                {
                    _virtualDesktopChangeMoveWindows = value;
                    Settings.Properties.FancyzonesVirtualDesktopChangeMoveWindows.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool AppLastZoneMoveWindows
        {
            get
            {
                return _appLastZoneMoveWindows;
            }

            set
            {
                if (value != _appLastZoneMoveWindows)
                {
                    _appLastZoneMoveWindows = value;
                    Settings.Properties.FancyzonesAppLastZoneMoveWindows.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool UseCursorPosEditorStartupScreen
        {
            get
            {
                return _useCursorPosEditorStartupScreen;
            }

            set
            {
                if (value != _useCursorPosEditorStartupScreen)
                {
                    _useCursorPosEditorStartupScreen = value;
                    Settings.Properties.UseCursorposEditorStartupscreen.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool ShowOnAllMonitors
        {
            get
            {
                return _showOnAllMonitors;
            }

            set
            {
                if (value != _showOnAllMonitors)
                {
                    _showOnAllMonitors = value;
                    Settings.Properties.FancyzonesShowOnAllMonitors.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string ZoneHighlightColor
        {
            get
            {
                return _zoneHighlightColor;
            }

            set
            {
                if (value != _zoneHighlightColor)
                {
                    _zoneHighlightColor = value;
                    Settings.Properties.FancyzonesZoneHighlightColor.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        public int HighlightOpacity
        {
            get
            {
                return _highlightOpacity;
            }

            set
            {
                if (value != _highlightOpacity)
                {
                    _highlightOpacity = value;
                    Settings.Properties.FancyzonesHighlightOpacity.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        /*
        public int EditorHotkey
        {
            get
            {
                return _editorHotkey;
            }

            set
            {
                if (value != _editorHotkey)
                {
                    _editorHotkey = value;
                    Settings.Properties.FancyzonesHighlightOpacity.Value = value;
                    RaisePropertyChanged();
                }
            }
        }
        */

        public HotkeySettings EditorHotkey
        {
            get
            {
                return _editorHotkey;
            }

            set
            {
                if (value != _editorHotkey)
                {
                    _editorHotkey = value;
                    Settings.Properties.FancyzonesEditorHotkey.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string ExcludedApps
        {
            get
            {
                return _excludedApps;
            }

            set
            {
                if (value != _excludedApps)
                {
                    _excludedApps = value;
                    Settings.Properties.FancyzonesExcludedApps.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        private void LaunchEditor()
        {
            // send message to launch the zones editor;
            ShellPage.DefaultSndMSGCallback("{\"action\":{\"FancyZones\":{\"action_name\":\"ToggledFZEditor\", \"value\":\"\"}}}");
        }

        private void SaveColorChoice(Color color)
        {
            ZoneHighlightColor = color.ToString();
        }

        public void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);
            SndFancyZonesSettings outsettings = new SndFancyZonesSettings(Settings);
            SndModuleSettings<SndFancyZonesSettings> ipcMessage = new SndModuleSettings<SndFancyZonesSettings>(outsettings);
            ShellPage.DefaultSndMSGCallback(ipcMessage.ToJsonString());
        }
    }
}
