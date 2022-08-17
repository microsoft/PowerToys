// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library.ViewModels
{
    public class MouseUtilsViewModel : Observable
    {
        private ISettingsUtils SettingsUtils { get; set; }

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private FindMyMouseSettings FindMyMouseSettingsConfig { get; set; }

        private MouseHighlighterSettings MouseHighlighterSettingsConfig { get; set; }

        private MousePointerCrosshairsSettings MousePointerCrosshairsSettingsConfig { get; set; }

        public MouseUtilsViewModel(ISettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, ISettingsRepository<FindMyMouseSettings> findMyMouseSettingsRepository, ISettingsRepository<MouseHighlighterSettings> mouseHighlighterSettingsRepository, ISettingsRepository<MousePointerCrosshairsSettings> mousePointerCrosshairsSettingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            SettingsUtils = settingsUtils;

            // To obtain the general settings configurations of PowerToys Settings.
            if (settingsRepository == null)
            {
                throw new ArgumentNullException(nameof(settingsRepository));
            }

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            _isFindMyMouseEnabled = GeneralSettingsConfig.Enabled.FindMyMouse;

            _isMouseHighlighterEnabled = GeneralSettingsConfig.Enabled.MouseHighlighter;

            _isMousePointerCrosshairsEnabled = GeneralSettingsConfig.Enabled.MousePointerCrosshairs;

            // To obtain the find my mouse settings, if the file exists.
            // If not, to create a file with the default settings and to return the default configurations.
            if (findMyMouseSettingsRepository == null)
            {
                throw new ArgumentNullException(nameof(findMyMouseSettingsRepository));
            }

            FindMyMouseSettingsConfig = findMyMouseSettingsRepository.SettingsConfig;
            _findMyMouseActivationMethod = FindMyMouseSettingsConfig.Properties.ActivationMethod.Value;
            _findMyMouseDoNotActivateOnGameMode = FindMyMouseSettingsConfig.Properties.DoNotActivateOnGameMode.Value;

            string backgroundColor = FindMyMouseSettingsConfig.Properties.BackgroundColor.Value;
            _findMyMouseBackgroundColor = !string.IsNullOrEmpty(backgroundColor) ? backgroundColor : "#000000";

            string spotlightColor = FindMyMouseSettingsConfig.Properties.SpotlightColor.Value;
            _findMyMouseSpotlightColor = !string.IsNullOrEmpty(spotlightColor) ? spotlightColor : "#FFFFFF";

            _findMyMouseOverlayOpacity = FindMyMouseSettingsConfig.Properties.OverlayOpacity.Value;
            _findMyMouseSpotlightRadius = FindMyMouseSettingsConfig.Properties.SpotlightRadius.Value;
            _findMyMouseAnimationDurationMs = FindMyMouseSettingsConfig.Properties.AnimationDurationMs.Value;
            _findMyMouseSpotlightInitialZoom = FindMyMouseSettingsConfig.Properties.SpotlightInitialZoom.Value;
            _findMyMouseExcludedApps = FindMyMouseSettingsConfig.Properties.ExcludedApps.Value;
            _findMyMouseShakingMinimumDistance = FindMyMouseSettingsConfig.Properties.ShakingMinimumDistance.Value;

            if (mouseHighlighterSettingsRepository == null)
            {
                throw new ArgumentNullException(nameof(mouseHighlighterSettingsRepository));
            }

            MouseHighlighterSettingsConfig = mouseHighlighterSettingsRepository.SettingsConfig;
            string leftClickColor = MouseHighlighterSettingsConfig.Properties.LeftButtonClickColor.Value;
            _highlighterLeftButtonClickColor = !string.IsNullOrEmpty(leftClickColor) ? leftClickColor : "#FFFF00";

            string rightClickColor = MouseHighlighterSettingsConfig.Properties.RightButtonClickColor.Value;
            _highlighterRightButtonClickColor = !string.IsNullOrEmpty(rightClickColor) ? rightClickColor : "#0000FF";

            _highlighterOpacity = MouseHighlighterSettingsConfig.Properties.HighlightOpacity.Value;
            _highlighterRadius = MouseHighlighterSettingsConfig.Properties.HighlightRadius.Value;
            _highlightFadeDelayMs = MouseHighlighterSettingsConfig.Properties.HighlightFadeDelayMs.Value;
            _highlightFadeDurationMs = MouseHighlighterSettingsConfig.Properties.HighlightFadeDurationMs.Value;

            if (mousePointerCrosshairsSettingsRepository == null)
            {
                throw new ArgumentNullException(nameof(mousePointerCrosshairsSettingsRepository));
            }

            MousePointerCrosshairsSettingsConfig = mousePointerCrosshairsSettingsRepository.SettingsConfig;

            string crosshairsColor = MousePointerCrosshairsSettingsConfig.Properties.CrosshairsColor.Value;
            _mousePointerCrosshairsColor = !string.IsNullOrEmpty(crosshairsColor) ? crosshairsColor : "#FF0000";

            string crosshairsBorderColor = MousePointerCrosshairsSettingsConfig.Properties.CrosshairsBorderColor.Value;
            _mousePointerCrosshairsBorderColor = !string.IsNullOrEmpty(crosshairsBorderColor) ? crosshairsBorderColor : "#FFFFFF";

            _mousePointerCrosshairsOpacity = MousePointerCrosshairsSettingsConfig.Properties.CrosshairsOpacity.Value;
            _mousePointerCrosshairsRadius = MousePointerCrosshairsSettingsConfig.Properties.CrosshairsRadius.Value;
            _mousePointerCrosshairsThickness = MousePointerCrosshairsSettingsConfig.Properties.CrosshairsThickness.Value;
            _mousePointerCrosshairsBorderSize = MousePointerCrosshairsSettingsConfig.Properties.CrosshairsBorderSize.Value;

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;
        }

        public bool IsFindMyMouseEnabled
        {
            get => _isFindMyMouseEnabled;
            set
            {
                if (_isFindMyMouseEnabled != value)
                {
                    _isFindMyMouseEnabled = value;

                    GeneralSettingsConfig.Enabled.FindMyMouse = value;
                    OnPropertyChanged(nameof(IsFindMyMouseEnabled));

                    OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);
                    SendConfigMSG(outgoing.ToString());

                    NotifyFindMyMousePropertyChanged();
                }
            }
        }

        public int FindMyMouseActivationMethod
        {
            get
            {
                return _findMyMouseActivationMethod;
            }

            set
            {
                if (value != _findMyMouseActivationMethod)
                {
                    _findMyMouseActivationMethod = value;
                    FindMyMouseSettingsConfig.Properties.ActivationMethod.Value = value;
                    NotifyFindMyMousePropertyChanged();
                }
            }
        }

        public bool FindMyMouseDoNotActivateOnGameMode
        {
            get
            {
                return _findMyMouseDoNotActivateOnGameMode;
            }

            set
            {
                if (_findMyMouseDoNotActivateOnGameMode != value)
                {
                    _findMyMouseDoNotActivateOnGameMode = value;
                    FindMyMouseSettingsConfig.Properties.DoNotActivateOnGameMode.Value = value;
                    NotifyFindMyMousePropertyChanged();
                }
            }
        }

        public string FindMyMouseBackgroundColor
        {
            get
            {
                return _findMyMouseBackgroundColor;
            }

            set
            {
                value = (value != null) ? SettingsUtilities.ToRGBHex(value) : "#000000";
                if (!value.Equals(_findMyMouseBackgroundColor, StringComparison.OrdinalIgnoreCase))
                {
                    _findMyMouseBackgroundColor = value;
                    FindMyMouseSettingsConfig.Properties.BackgroundColor.Value = value;
                    NotifyFindMyMousePropertyChanged();
                }
            }
        }

        public string FindMyMouseSpotlightColor
        {
            get
            {
                return _findMyMouseSpotlightColor;
            }

            set
            {
                value = (value != null) ? SettingsUtilities.ToRGBHex(value) : "#FFFFFF";
                if (!value.Equals(_findMyMouseSpotlightColor, StringComparison.OrdinalIgnoreCase))
                {
                    _findMyMouseSpotlightColor = value;
                    FindMyMouseSettingsConfig.Properties.SpotlightColor.Value = value;
                    NotifyFindMyMousePropertyChanged();
                }
            }
        }

        public int FindMyMouseOverlayOpacity
        {
            get
            {
                return _findMyMouseOverlayOpacity;
            }

            set
            {
                if (value != _findMyMouseOverlayOpacity)
                {
                    _findMyMouseOverlayOpacity = value;
                    FindMyMouseSettingsConfig.Properties.OverlayOpacity.Value = value;
                    NotifyFindMyMousePropertyChanged();
                }
            }
        }

        public int FindMyMouseSpotlightRadius
        {
            get
            {
                return _findMyMouseSpotlightRadius;
            }

            set
            {
                if (value != _findMyMouseSpotlightRadius)
                {
                    _findMyMouseSpotlightRadius = value;
                    FindMyMouseSettingsConfig.Properties.SpotlightRadius.Value = value;
                    NotifyFindMyMousePropertyChanged();
                }
            }
        }

        public int FindMyMouseAnimationDurationMs
        {
            get
            {
                return _findMyMouseAnimationDurationMs;
            }

            set
            {
                if (value != _findMyMouseAnimationDurationMs)
                {
                    _findMyMouseAnimationDurationMs = value;
                    FindMyMouseSettingsConfig.Properties.AnimationDurationMs.Value = value;
                    NotifyFindMyMousePropertyChanged();
                }
            }
        }

        public int FindMyMouseSpotlightInitialZoom
        {
            get
            {
                return _findMyMouseSpotlightInitialZoom;
            }

            set
            {
                if (value != _findMyMouseSpotlightInitialZoom)
                {
                    _findMyMouseSpotlightInitialZoom = value;
                    FindMyMouseSettingsConfig.Properties.SpotlightInitialZoom.Value = value;
                    NotifyFindMyMousePropertyChanged();
                }
            }
        }

        public string FindMyMouseExcludedApps
        {
            get
            {
                return _findMyMouseExcludedApps;
            }

            set
            {
                if (value != _findMyMouseExcludedApps)
                {
                    _findMyMouseExcludedApps = value;
                    FindMyMouseSettingsConfig.Properties.ExcludedApps.Value = value;
                    NotifyFindMyMousePropertyChanged();
                }
            }
        }

        public int FindMyMouseShakingMinimumDistance
        {
            get
            {
                return _findMyMouseShakingMinimumDistance;
            }

            set
            {
                if (value != _findMyMouseShakingMinimumDistance)
                {
                    _findMyMouseShakingMinimumDistance = value;
                    FindMyMouseSettingsConfig.Properties.ShakingMinimumDistance.Value = value;
                    NotifyFindMyMousePropertyChanged();
                }
            }
        }

        public void NotifyFindMyMousePropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);

            SndFindMyMouseSettings outsettings = new SndFindMyMouseSettings(FindMyMouseSettingsConfig);
            SndModuleSettings<SndFindMyMouseSettings> ipcMessage = new SndModuleSettings<SndFindMyMouseSettings>(outsettings);
            SendConfigMSG(ipcMessage.ToJsonString());
            SettingsUtils.SaveSettings(FindMyMouseSettingsConfig.ToJsonString(), FindMyMouseSettings.ModuleName);
        }

        public bool IsMouseHighlighterEnabled
        {
            get => _isMouseHighlighterEnabled;
            set
            {
                if (_isMouseHighlighterEnabled != value)
                {
                    _isMouseHighlighterEnabled = value;

                    GeneralSettingsConfig.Enabled.MouseHighlighter = value;
                    OnPropertyChanged(nameof(_isMouseHighlighterEnabled));

                    OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);
                    SendConfigMSG(outgoing.ToString());

                    NotifyMouseHighlighterPropertyChanged();
                }
            }
        }

        public HotkeySettings MouseHighlighterActivationShortcut
        {
            get
            {
                return MouseHighlighterSettingsConfig.Properties.ActivationShortcut;
            }

            set
            {
                if (MouseHighlighterSettingsConfig.Properties.ActivationShortcut != value)
                {
                    MouseHighlighterSettingsConfig.Properties.ActivationShortcut = value;
                    NotifyMouseHighlighterPropertyChanged();
                }
            }
        }

        public string MouseHighlighterLeftButtonClickColor
        {
            get
            {
                return _highlighterLeftButtonClickColor;
            }

            set
            {
                value = SettingsUtilities.ToRGBHex(value);
                if (!value.Equals(_highlighterLeftButtonClickColor, StringComparison.OrdinalIgnoreCase))
                {
                    _highlighterLeftButtonClickColor = value;
                    MouseHighlighterSettingsConfig.Properties.LeftButtonClickColor.Value = value;
                    NotifyMouseHighlighterPropertyChanged();
                }
            }
        }

        public string MouseHighlighterRightButtonClickColor
        {
            get
            {
                return _highlighterRightButtonClickColor;
            }

            set
            {
                value = SettingsUtilities.ToRGBHex(value);
                if (!value.Equals(_highlighterRightButtonClickColor, StringComparison.OrdinalIgnoreCase))
                {
                    _highlighterRightButtonClickColor = value;
                    MouseHighlighterSettingsConfig.Properties.RightButtonClickColor.Value = value;
                    NotifyMouseHighlighterPropertyChanged();
                }
            }
        }

        public int MouseHighlighterOpacity
        {
            get
            {
                return _highlighterOpacity;
            }

            set
            {
                if (value != _highlighterOpacity)
                {
                    _highlighterOpacity = value;
                    MouseHighlighterSettingsConfig.Properties.HighlightOpacity.Value = value;
                    NotifyMouseHighlighterPropertyChanged();
                }
            }
        }

        public int MouseHighlighterRadius
        {
            get
            {
                return _highlighterRadius;
            }

            set
            {
                if (value != _highlighterRadius)
                {
                    _highlighterRadius = value;
                    MouseHighlighterSettingsConfig.Properties.HighlightRadius.Value = value;
                    NotifyMouseHighlighterPropertyChanged();
                }
            }
        }

        public int MouseHighlighterFadeDelayMs
        {
            get
            {
                return _highlightFadeDelayMs;
            }

            set
            {
                if (value != _highlightFadeDelayMs)
                {
                    _highlightFadeDelayMs = value;
                    MouseHighlighterSettingsConfig.Properties.HighlightFadeDelayMs.Value = value;
                    NotifyMouseHighlighterPropertyChanged();
                }
            }
        }

        public int MouseHighlighterFadeDurationMs
        {
            get
            {
                return _highlightFadeDurationMs;
            }

            set
            {
                if (value != _highlightFadeDurationMs)
                {
                    _highlightFadeDurationMs = value;
                    MouseHighlighterSettingsConfig.Properties.HighlightFadeDurationMs.Value = value;
                    NotifyMouseHighlighterPropertyChanged();
                }
            }
        }

        public void NotifyMouseHighlighterPropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);

            SndMouseHighlighterSettings outsettings = new SndMouseHighlighterSettings(MouseHighlighterSettingsConfig);
            SndModuleSettings<SndMouseHighlighterSettings> ipcMessage = new SndModuleSettings<SndMouseHighlighterSettings>(outsettings);
            SendConfigMSG(ipcMessage.ToJsonString());
            SettingsUtils.SaveSettings(MouseHighlighterSettingsConfig.ToJsonString(), MouseHighlighterSettings.ModuleName);
        }

        public bool IsMousePointerCrosshairsEnabled
        {
            get => _isMousePointerCrosshairsEnabled;
            set
            {
                if (_isMousePointerCrosshairsEnabled != value)
                {
                    _isMousePointerCrosshairsEnabled = value;

                    GeneralSettingsConfig.Enabled.MousePointerCrosshairs = value;
                    OnPropertyChanged(nameof(_isMousePointerCrosshairsEnabled));

                    OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);
                    SendConfigMSG(outgoing.ToString());

                    NotifyMousePointerCrosshairsPropertyChanged();
                }
            }
        }

        public HotkeySettings MousePointerCrosshairsActivationShortcut
        {
            get
            {
                return MousePointerCrosshairsSettingsConfig.Properties.ActivationShortcut;
            }

            set
            {
                if (MousePointerCrosshairsSettingsConfig.Properties.ActivationShortcut != value)
                {
                    MousePointerCrosshairsSettingsConfig.Properties.ActivationShortcut = value;
                    NotifyMousePointerCrosshairsPropertyChanged();
                }
            }
        }

        public string MousePointerCrosshairsColor
        {
            get
            {
                return _mousePointerCrosshairsColor;
            }

            set
            {
                value = SettingsUtilities.ToRGBHex(value);
                if (!value.Equals(_mousePointerCrosshairsColor, StringComparison.OrdinalIgnoreCase))
                {
                    _mousePointerCrosshairsColor = value;
                    MousePointerCrosshairsSettingsConfig.Properties.CrosshairsColor.Value = value;
                    NotifyMousePointerCrosshairsPropertyChanged();
                }
            }
        }

        public int MousePointerCrosshairsOpacity
        {
            get
            {
                return _mousePointerCrosshairsOpacity;
            }

            set
            {
                if (value != _mousePointerCrosshairsOpacity)
                {
                    _mousePointerCrosshairsOpacity = value;
                    MousePointerCrosshairsSettingsConfig.Properties.CrosshairsOpacity.Value = value;
                    NotifyMousePointerCrosshairsPropertyChanged();
                }
            }
        }

        public int MousePointerCrosshairsRadius
        {
            get
            {
                return _mousePointerCrosshairsRadius;
            }

            set
            {
                if (value != _mousePointerCrosshairsRadius)
                {
                    _mousePointerCrosshairsRadius = value;
                    MousePointerCrosshairsSettingsConfig.Properties.CrosshairsRadius.Value = value;
                    NotifyMousePointerCrosshairsPropertyChanged();
                }
            }
        }

        public int MousePointerCrosshairsThickness
        {
            get
            {
                return _mousePointerCrosshairsThickness;
            }

            set
            {
                if (value != _mousePointerCrosshairsThickness)
                {
                    _mousePointerCrosshairsThickness = value;
                    MousePointerCrosshairsSettingsConfig.Properties.CrosshairsThickness.Value = value;
                    NotifyMousePointerCrosshairsPropertyChanged();
                }
            }
        }

        public string MousePointerCrosshairsBorderColor
        {
            get
            {
                return _mousePointerCrosshairsBorderColor;
            }

            set
            {
                value = SettingsUtilities.ToRGBHex(value);
                if (!value.Equals(_mousePointerCrosshairsBorderColor, StringComparison.OrdinalIgnoreCase))
                {
                    _mousePointerCrosshairsBorderColor = value;
                    MousePointerCrosshairsSettingsConfig.Properties.CrosshairsBorderColor.Value = value;
                    NotifyMousePointerCrosshairsPropertyChanged();
                }
            }
        }

        public int MousePointerCrosshairsBorderSize
        {
            get
            {
                return _mousePointerCrosshairsBorderSize;
            }

            set
            {
                if (value != _mousePointerCrosshairsBorderSize)
                {
                    _mousePointerCrosshairsBorderSize = value;
                    MousePointerCrosshairsSettingsConfig.Properties.CrosshairsBorderSize.Value = value;
                    NotifyMousePointerCrosshairsPropertyChanged();
                }
            }
        }

        public void NotifyMousePointerCrosshairsPropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);

            SndMousePointerCrosshairsSettings outsettings = new SndMousePointerCrosshairsSettings(MousePointerCrosshairsSettingsConfig);
            SndModuleSettings<SndMousePointerCrosshairsSettings> ipcMessage = new SndModuleSettings<SndMousePointerCrosshairsSettings>(outsettings);
            SendConfigMSG(ipcMessage.ToJsonString());
            SettingsUtils.SaveSettings(MousePointerCrosshairsSettingsConfig.ToJsonString(), MousePointerCrosshairsSettings.ModuleName);
        }

        private Func<string, int> SendConfigMSG { get; }

        private bool _isFindMyMouseEnabled;
        private int _findMyMouseActivationMethod;
        private bool _findMyMouseDoNotActivateOnGameMode;
        private string _findMyMouseBackgroundColor;
        private string _findMyMouseSpotlightColor;
        private int _findMyMouseOverlayOpacity;
        private int _findMyMouseSpotlightRadius;
        private int _findMyMouseAnimationDurationMs;
        private int _findMyMouseSpotlightInitialZoom;
        private string _findMyMouseExcludedApps;
        private int _findMyMouseShakingMinimumDistance;

        private bool _isMouseHighlighterEnabled;
        private string _highlighterLeftButtonClickColor;
        private string _highlighterRightButtonClickColor;
        private int _highlighterOpacity;
        private int _highlighterRadius;
        private int _highlightFadeDelayMs;
        private int _highlightFadeDurationMs;

        private bool _isMousePointerCrosshairsEnabled;
        private string _mousePointerCrosshairsColor;
        private int _mousePointerCrosshairsOpacity;
        private int _mousePointerCrosshairsRadius;
        private int _mousePointerCrosshairsThickness;
        private string _mousePointerCrosshairsBorderColor;
        private int _mousePointerCrosshairsBorderSize;
    }
}
