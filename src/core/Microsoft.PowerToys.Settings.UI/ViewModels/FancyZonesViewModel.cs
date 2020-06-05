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
using Microsoft.Toolkit.Uwp.Helpers;
using Windows.UI;
using Windows.UI.Popups;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class FancyZonesViewModel : Observable
    {
        private const string ModuleName = "FancyZones";

        public ButtonClickCommand LaunchEditorEventHandler { get; set; }

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
            this._mouseSwitch = Settings.Properties.FancyzonesMouseSwitch.Value;
            this._overrideSnapHotkeys = Settings.Properties.FancyzonesOverrideSnapHotkeys.Value;
            this._moveWindowsAcrossMonitors = Settings.Properties.FancyzonesMoveWindowsAcrossMonitors.Value;
            this._displayChangemoveWindows = Settings.Properties.FancyzonesDisplayChangeMoveWindows.Value;
            this._zoneSetChangeMoveWindows = Settings.Properties.FancyzonesZoneSetChangeMoveWindows.Value;
            this._appLastZoneMoveWindows = Settings.Properties.FancyzonesAppLastZoneMoveWindows.Value;
            this._useCursorPosEditorStartupScreen = Settings.Properties.UseCursorposEditorStartupscreen.Value;
            this._showOnAllMonitors = Settings.Properties.FancyzonesShowOnAllMonitors.Value;
            this._makeDraggedWindowTransparent = Settings.Properties.FancyzonesMakeDraggedWindowTransparent.Value;
            this._highlightOpacity = Settings.Properties.FancyzonesHighlightOpacity.Value;
            this._excludedApps = Settings.Properties.FancyzonesExcludedApps.Value;
            this.EditorHotkey = Settings.Properties.FancyzonesEditorHotkey.Value;

            string inactiveColor = Settings.Properties.FancyzonesInActiveColor.Value;
            this._zoneInActiveColor = inactiveColor != string.Empty ? inactiveColor.ToColor() : "#F5FCFF".ToColor();

            string borderColor = Settings.Properties.FancyzonesBorderColor.Value;
            this._zoneBorderColor = borderColor != string.Empty ? borderColor.ToColor() : "#FFFFFF".ToColor();

            string highlightColor = Settings.Properties.FancyzonesZoneHighlightColor.Value;
            this._zoneHighlightColor = highlightColor != string.Empty ? highlightColor.ToColor() : "#0078D7".ToColor();

            GeneralSettings generalSettings;
            try
            {
                generalSettings = SettingsUtils.GetSettings<GeneralSettings>(string.Empty);
            }
            catch
            {
                generalSettings = new GeneralSettings();
                SettingsUtils.SaveSettings(generalSettings.ToJsonString(), string.Empty);
            }

            this._isEnabled = generalSettings.Enabled.FancyZones;
        }

        private bool _isEnabled;
        private bool _shiftDrag;
        private bool _mouseSwitch;
        private bool _overrideSnapHotkeys;
        private bool _moveWindowsAcrossMonitors;
        private bool _displayChangemoveWindows;
        private bool _zoneSetChangeMoveWindows;
        private bool _appLastZoneMoveWindows;
        private bool _useCursorPosEditorStartupScreen;
        private bool _showOnAllMonitors;
        private bool _makeDraggedWindowTransparent;

        private int _highlightOpacity;
        private string _excludedApps;
        private HotkeySettings _editorHotkey;
        private Color _zoneInActiveColor;
        private Color _zoneBorderColor;
        private Color _zoneHighlightColor;

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
                    OnPropertyChanged("IsEnabled");
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

        public bool MouseSwitch
        {
            get
            {
                return _mouseSwitch;
            }

            set
            {
                if (value != _mouseSwitch)
                {
                    _mouseSwitch = value;
                    Settings.Properties.FancyzonesMouseSwitch.Value = value;
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

        public bool MoveWindowsAcrossMonitors
        {
            get
            {
                return _moveWindowsAcrossMonitors;
            }

            set
            {
                if (value != _moveWindowsAcrossMonitors)
                {
                    _moveWindowsAcrossMonitors = value;
                    Settings.Properties.FancyzonesMoveWindowsAcrossMonitors.Value = value;
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

        public bool MakeDraggedWindowsTransparent
        {
            get
            {
                return _makeDraggedWindowTransparent;
            }

            set
            {
                if (value != _makeDraggedWindowTransparent)
                {
                    _makeDraggedWindowTransparent = value;
                    Settings.Properties.FancyzonesMakeDraggedWindowTransparent.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        public Color ZoneHighlightColor
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
                    Settings.Properties.FancyzonesZoneHighlightColor.Value = ToRGBHex(value);
                    RaisePropertyChanged();
                }
            }
        }

        public Color ZoneBorderColor
        {
            get
            {
                return _zoneBorderColor;
            }

            set
            {
                if (value != _zoneBorderColor)
                {
                    _zoneBorderColor = value;
                    Settings.Properties.FancyzonesBorderColor.Value = ToRGBHex(value);
                    RaisePropertyChanged();
                }
            }
        }

        public Color ZoneInActiveColor
        {
            get
            {
                return _zoneInActiveColor;
            }

            set
            {
                if (value != _zoneInActiveColor)
                {
                    _zoneInActiveColor = value;
                    Settings.Properties.FancyzonesInActiveColor.Value = ToRGBHex(value);
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
                    if (value.IsEmpty())
                    {
                        _editorHotkey = new HotkeySettings(true, false, false, false, "'", 192);
                    }
                    else
                    {
                        _editorHotkey = value;
                    }

                    Settings.Properties.FancyzonesEditorHotkey.Value = _editorHotkey;
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

        private String ToRGBHex(Color color)
        {
            return "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
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
