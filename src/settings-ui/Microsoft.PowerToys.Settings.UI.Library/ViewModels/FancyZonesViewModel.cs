// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;

namespace Microsoft.PowerToys.Settings.UI.Library.ViewModels
{
    public class FancyZonesViewModel : Observable
    {
        private ISettingsUtils SettingsUtils { get; set; }

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private const string ModuleName = FancyZonesSettings.ModuleName;

        public ButtonClickCommand LaunchEditorEventHandler { get; set; }

        private FancyZonesSettings Settings { get; set; }

        private Func<string, int> SendConfigMSG { get; }

        private string settingsConfigFileFolder = string.Empty;

        private enum MoveWindowBehaviour
        {
            MoveWindowBasedOnZoneIndex = 0,
            MoveWindowBasedOnPosition,
        }

        private enum OverlappingZonesAlgorithm
        {
            Smallest = 0,
            Largest = 1,
            Positional = 2,
        }

        public FancyZonesViewModel(SettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, ISettingsRepository<FancyZonesSettings> moduleSettingsRepository, Func<string, int> ipcMSGCallBackFunc, string configFileSubfolder = "")
        {
            if (settingsUtils == null)
            {
                throw new ArgumentNullException(nameof(settingsUtils));
            }

            SettingsUtils = settingsUtils;

            // To obtain the general settings configurations of PowerToys Settings.
            if (settingsRepository == null)
            {
                throw new ArgumentNullException(nameof(settingsRepository));
            }

            GeneralSettingsConfig = settingsRepository.SettingsConfig;
            settingsConfigFileFolder = configFileSubfolder;

            // To obtain the settings configurations of Fancy zones.
            if (moduleSettingsRepository == null)
            {
                throw new ArgumentNullException(nameof(moduleSettingsRepository));
            }

            Settings = moduleSettingsRepository.SettingsConfig;

            LaunchEditorEventHandler = new ButtonClickCommand(LaunchEditor);

            _shiftDrag = Settings.Properties.FancyzonesShiftDrag.Value;
            _mouseSwitch = Settings.Properties.FancyzonesMouseSwitch.Value;
            _overrideSnapHotkeys = Settings.Properties.FancyzonesOverrideSnapHotkeys.Value;
            _moveWindowsAcrossMonitors = Settings.Properties.FancyzonesMoveWindowsAcrossMonitors.Value;
            _moveWindowBehaviour = Settings.Properties.FancyzonesMoveWindowsBasedOnPosition.Value ? MoveWindowBehaviour.MoveWindowBasedOnPosition : MoveWindowBehaviour.MoveWindowBasedOnZoneIndex;
            _overlappingZonesAlgorithm = (OverlappingZonesAlgorithm)Settings.Properties.FancyzonesOverlappingZonesAlgorithm.Value;
            _displayChangemoveWindows = Settings.Properties.FancyzonesDisplayChangeMoveWindows.Value;
            _zoneSetChangeMoveWindows = Settings.Properties.FancyzonesZoneSetChangeMoveWindows.Value;
            _appLastZoneMoveWindows = Settings.Properties.FancyzonesAppLastZoneMoveWindows.Value;
            _openWindowOnActiveMonitor = Settings.Properties.FancyzonesOpenWindowOnActiveMonitor.Value;
            _restoreSize = Settings.Properties.FancyzonesRestoreSize.Value;
            _quickLayoutSwitch = Settings.Properties.FancyzonesQuickLayoutSwitch.Value;
            _flashZonesOnQuickLayoutSwitch = Settings.Properties.FancyzonesFlashZonesOnQuickSwitch.Value;
            _useCursorPosEditorStartupScreen = Settings.Properties.UseCursorposEditorStartupscreen.Value;
            _showOnAllMonitors = Settings.Properties.FancyzonesShowOnAllMonitors.Value;
            _spanZonesAcrossMonitors = Settings.Properties.FancyzonesSpanZonesAcrossMonitors.Value;
            _makeDraggedWindowTransparent = Settings.Properties.FancyzonesMakeDraggedWindowTransparent.Value;
            _highlightOpacity = Settings.Properties.FancyzonesHighlightOpacity.Value;
            _excludedApps = Settings.Properties.FancyzonesExcludedApps.Value;
            EditorHotkey = Settings.Properties.FancyzonesEditorHotkey.Value;

            // set the callback functions value to hangle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;

            string inactiveColor = Settings.Properties.FancyzonesInActiveColor.Value;
            _zoneInActiveColor = !string.IsNullOrEmpty(inactiveColor) ? inactiveColor : "#F5FCFF";

            string borderColor = Settings.Properties.FancyzonesBorderColor.Value;
            _zoneBorderColor = !string.IsNullOrEmpty(borderColor) ? borderColor : "#FFFFFF";

            string highlightColor = Settings.Properties.FancyzonesZoneHighlightColor.Value;
            _zoneHighlightColor = !string.IsNullOrEmpty(highlightColor) ? highlightColor : "#0078D7";

            _isEnabled = GeneralSettingsConfig.Enabled.FancyZones;
        }

        private bool _isEnabled;
        private bool _shiftDrag;
        private bool _mouseSwitch;
        private bool _overrideSnapHotkeys;
        private bool _moveWindowsAcrossMonitors;
        private MoveWindowBehaviour _moveWindowBehaviour;
        private OverlappingZonesAlgorithm _overlappingZonesAlgorithm;
        private bool _displayChangemoveWindows;
        private bool _zoneSetChangeMoveWindows;
        private bool _appLastZoneMoveWindows;
        private bool _openWindowOnActiveMonitor;
        private bool _spanZonesAcrossMonitors;
        private bool _restoreSize;
        private bool _quickLayoutSwitch;
        private bool _flashZonesOnQuickLayoutSwitch;
        private bool _useCursorPosEditorStartupScreen;
        private bool _showOnAllMonitors;
        private bool _makeDraggedWindowTransparent;

        private int _highlightOpacity;
        private string _excludedApps;
        private HotkeySettings _editorHotkey;
        private string _zoneInActiveColor;
        private string _zoneBorderColor;
        private string _zoneHighlightColor;

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

                    // Set the status of FancyZones in the general settings configuration
                    GeneralSettingsConfig.Enabled.FancyZones = value;
                    OutGoingGeneralSettings snd = new OutGoingGeneralSettings(GeneralSettingsConfig);

                    SendConfigMSG(snd.ToString());
                    OnPropertyChanged(nameof(IsEnabled));
                    OnPropertyChanged(nameof(SnapHotkeysCategoryEnabled));
                    OnPropertyChanged(nameof(QuickSwitchEnabled));
                }
            }
        }

        public bool SnapHotkeysCategoryEnabled
        {
            get
            {
                return _isEnabled && _overrideSnapHotkeys;
            }
        }

        public bool QuickSwitchEnabled
        {
            get
            {
                return _isEnabled && _quickLayoutSwitch;
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
                    NotifyPropertyChanged();
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
                    NotifyPropertyChanged();
                }
            }
        }

        public string GetSettingsSubPath()
        {
            return settingsConfigFileFolder + "\\" + ModuleName;
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
                    NotifyPropertyChanged();
                    OnPropertyChanged(nameof(SnapHotkeysCategoryEnabled));
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
                    NotifyPropertyChanged();
                }
            }
        }

        public bool MoveWindowsBasedOnPosition
        {
            get
            {
                return _moveWindowBehaviour == MoveWindowBehaviour.MoveWindowBasedOnPosition;
            }

            set
            {
                var settingsValue = Settings.Properties.FancyzonesMoveWindowsBasedOnPosition.Value;
                if (value != settingsValue)
                {
                    _moveWindowBehaviour = value ? MoveWindowBehaviour.MoveWindowBasedOnPosition : MoveWindowBehaviour.MoveWindowBasedOnZoneIndex;
                    Settings.Properties.FancyzonesMoveWindowsBasedOnPosition.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool MoveWindowsBasedOnZoneIndex
        {
            get
            {
                return _moveWindowBehaviour == MoveWindowBehaviour.MoveWindowBasedOnZoneIndex;
            }

            set
            {
                var settingsValue = Settings.Properties.FancyzonesMoveWindowsBasedOnPosition.Value;
                if (value == settingsValue)
                {
                    _moveWindowBehaviour = value ? MoveWindowBehaviour.MoveWindowBasedOnZoneIndex : MoveWindowBehaviour.MoveWindowBasedOnPosition;
                    Settings.Properties.FancyzonesMoveWindowsBasedOnPosition.Value = !value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int OverlappingZonesAlgorithmIndex
        {
            get
            {
                return (int)_overlappingZonesAlgorithm;
            }

            set
            {
                if (value != (int)_overlappingZonesAlgorithm)
                {
                    _overlappingZonesAlgorithm = (OverlappingZonesAlgorithm)value;
                    Settings.Properties.FancyzonesOverlappingZonesAlgorithm.Value = value;
                    NotifyPropertyChanged();
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
                    NotifyPropertyChanged();
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
                    NotifyPropertyChanged();
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
                    NotifyPropertyChanged();
                }
            }
        }

        public bool OpenWindowOnActiveMonitor
        {
            get
            {
                return _openWindowOnActiveMonitor;
            }

            set
            {
                if (value != _openWindowOnActiveMonitor)
                {
                    _openWindowOnActiveMonitor = value;
                    Settings.Properties.FancyzonesOpenWindowOnActiveMonitor.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool RestoreSize
        {
            get
            {
                return _restoreSize;
            }

            set
            {
                if (value != _restoreSize)
                {
                    _restoreSize = value;
                    Settings.Properties.FancyzonesRestoreSize.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool QuickLayoutSwitch
        {
            get
            {
                return _quickLayoutSwitch;
            }

            set
            {
                if (value != _quickLayoutSwitch)
                {
                    _quickLayoutSwitch = value;
                    Settings.Properties.FancyzonesQuickLayoutSwitch.Value = value;
                    NotifyPropertyChanged();
                    OnPropertyChanged(nameof(QuickSwitchEnabled));
                }
            }
        }

        public bool FlashZonesOnQuickSwitch
        {
            get
            {
                return _flashZonesOnQuickLayoutSwitch;
            }

            set
            {
                if (value != _flashZonesOnQuickLayoutSwitch)
                {
                    _flashZonesOnQuickLayoutSwitch = value;
                    Settings.Properties.FancyzonesFlashZonesOnQuickSwitch.Value = value;
                    NotifyPropertyChanged();
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
                    NotifyPropertyChanged();
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
                    NotifyPropertyChanged();
                }
            }
        }

        public bool SpanZonesAcrossMonitors
        {
            get
            {
                return _spanZonesAcrossMonitors;
            }

            set
            {
                if (value != _spanZonesAcrossMonitors)
                {
                    _spanZonesAcrossMonitors = value;
                    Settings.Properties.FancyzonesSpanZonesAcrossMonitors.Value = value;
                    NotifyPropertyChanged();
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
                    NotifyPropertyChanged();
                }
            }
        }

        // For the following setters we use OrdinalIgnoreCase string comparison since
        // we expect value to be a hex code.
        public string ZoneHighlightColor
        {
            get
            {
                return _zoneHighlightColor;
            }

            set
            {
                // The fallback value is based on ToRGBHex's behavior, which returns
                // #FFFFFF if any exceptions are encountered, e.g. from passing in a null value.
                // This extra handling is added here to deal with FxCop warnings.
                value = (value != null) ? ToRGBHex(value) : "#FFFFFF";
                if (!value.Equals(_zoneHighlightColor, StringComparison.OrdinalIgnoreCase))
                {
                    _zoneHighlightColor = value;
                    Settings.Properties.FancyzonesZoneHighlightColor.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string ZoneBorderColor
        {
            get
            {
                return _zoneBorderColor;
            }

            set
            {
                // The fallback value is based on ToRGBHex's behavior, which returns
                // #FFFFFF if any exceptions are encountered, e.g. from passing in a null value.
                // This extra handling is added here to deal with FxCop warnings.
                value = (value != null) ? ToRGBHex(value) : "#FFFFFF";
                if (!value.Equals(_zoneBorderColor, StringComparison.OrdinalIgnoreCase))
                {
                    _zoneBorderColor = value;
                    Settings.Properties.FancyzonesBorderColor.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string ZoneInActiveColor
        {
            get
            {
                return _zoneInActiveColor;
            }

            set
            {
                // The fallback value is based on ToRGBHex's behavior, which returns
                // #FFFFFF if any exceptions are encountered, e.g. from passing in a null value.
                // This extra handling is added here to deal with FxCop warnings.
                value = (value != null) ? ToRGBHex(value) : "#FFFFFF";
                if (!value.Equals(_zoneInActiveColor, StringComparison.OrdinalIgnoreCase))
                {
                    _zoneInActiveColor = value;
                    Settings.Properties.FancyzonesInActiveColor.Value = value;
                    NotifyPropertyChanged();
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
                    NotifyPropertyChanged();
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
                    if (value == null || value.IsEmpty())
                    {
                        _editorHotkey = FZConfigProperties.DefaultHotkeyValue;
                    }
                    else
                    {
                        _editorHotkey = value;
                    }

                    Settings.Properties.FancyzonesEditorHotkey.Value = _editorHotkey;
                    NotifyPropertyChanged();
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
                    NotifyPropertyChanged();
                }
            }
        }

        private void LaunchEditor()
        {
            // send message to launch the zones editor;
            SendConfigMSG("{\"action\":{\"FancyZones\":{\"action_name\":\"ToggledFZEditor\", \"value\":\"\"}}}");
        }

        public void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);
            SettingsUtils.SaveSettings(Settings.ToJsonString(), GetSettingsSubPath());
        }

        private static string ToRGBHex(string color)
        {
            // Using InvariantCulture as these are expected to be hex codes.
            bool success = int.TryParse(
                color.Replace("#", string.Empty),
                System.Globalization.NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out int argb);

            if (success)
            {
                Color clr = Color.FromArgb(argb);
                return "#" + clr.R.ToString("X2", CultureInfo.InvariantCulture) +
                    clr.G.ToString("X2", CultureInfo.InvariantCulture) +
                    clr.B.ToString("X2", CultureInfo.InvariantCulture);
            }
            else
            {
                return "#FFFFFF";
            }
        }
    }
}
