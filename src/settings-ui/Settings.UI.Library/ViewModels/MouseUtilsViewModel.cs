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

        private InclusiveMouseSettings InclusiveMouseSettingsConfig { get; set; }

        public MouseUtilsViewModel(ISettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, ISettingsRepository<FindMyMouseSettings> findMyMouseSettingsRepository, ISettingsRepository<MouseHighlighterSettings> mouseHighlighterSettingsRepository, ISettingsRepository<InclusiveMouseSettings> inclusiveMouseSettingsRepository, Func<string, int> ipcMSGCallBackFunc)
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

            _isInclusiveMouseEnabled = GeneralSettingsConfig.Enabled.InclusiveMouse;

            // To obtain the find my mouse settings, if the file exists.
            // If not, to create a file with the default settings and to return the default configurations.
            if (findMyMouseSettingsRepository == null)
            {
                throw new ArgumentNullException(nameof(findMyMouseSettingsRepository));
            }

            FindMyMouseSettingsConfig = findMyMouseSettingsRepository.SettingsConfig;
            _findMyMouseDoNotActivateOnGameMode = FindMyMouseSettingsConfig.Properties.DoNotActivateOnGameMode.Value;

            string backgroundColor = FindMyMouseSettingsConfig.Properties.BackgroundColor.Value;
            _findMyMouseBackgroundColor = !string.IsNullOrEmpty(backgroundColor) ? backgroundColor : "#000000";

            string spotlightColor = FindMyMouseSettingsConfig.Properties.SpotlightColor.Value;
            _findMyMouseSpotlightColor = !string.IsNullOrEmpty(spotlightColor) ? spotlightColor : "#FFFFFF";

            _findMyMouseOverlayOpacity = FindMyMouseSettingsConfig.Properties.OverlayOpacity.Value;
            _findMyMouseSpotlightRadius = FindMyMouseSettingsConfig.Properties.SpotlightRadius.Value;
            _findMyMouseAnimationDurationMs = FindMyMouseSettingsConfig.Properties.AnimationDurationMs.Value;
            _findMyMouseSpotlightInitialZoom = FindMyMouseSettingsConfig.Properties.SpotlightInitialZoom.Value;

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

            if (inclusiveMouseSettingsRepository == null)
            {
                throw new ArgumentNullException(nameof(inclusiveMouseSettingsRepository));
            }

            InclusiveMouseSettingsConfig = inclusiveMouseSettingsRepository.SettingsConfig;

            string crosshairColor = InclusiveMouseSettingsConfig.Properties.CrosshairColor.Value;
            _inclusiveMouseCrosshairColor = !string.IsNullOrEmpty(crosshairColor) ? crosshairColor : "#0000FF";

            _inclusiveMouseCrosshairOpacity = InclusiveMouseSettingsConfig.Properties.CrosshairOpacity.Value;
            _inclusiveMouseCrosshairRadius = InclusiveMouseSettingsConfig.Properties.CrosshairRadius.Value;
            _inclusiveMouseCrosshairThickness = InclusiveMouseSettingsConfig.Properties.CrosshairThickness.Value;

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
                // The fallback value is based on ToRGBHex's behavior, which returns
                // #FFFFFF if any exceptions are encountered, e.g. from passing in a null value.
                // This extra handling is added here to deal with FxCop warnings.
                value = (value != null) ? SettingsUtilities.ToRGBHex(value) : "#FFFFFF";
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
                // The fallback value is based on ToRGBHex's behavior, which returns
                // #FFFFFF if any exceptions are encountered, e.g. from passing in a null value.
                // This extra handling is added here to deal with FxCop warnings.
                value = (value != null) ? SettingsUtilities.ToRGBHex(value) : "#FFFFFF";
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

        public bool IsInclusiveMouseEnabled
        {
            get => _isInclusiveMouseEnabled;
            set
            {
                if (_isInclusiveMouseEnabled != value)
                {
                    _isInclusiveMouseEnabled = value;

                    GeneralSettingsConfig.Enabled.InclusiveMouse = value;
                    OnPropertyChanged(nameof(_isInclusiveMouseEnabled));

                    OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);
                    SendConfigMSG(outgoing.ToString());

                    NotifyInclusiveMousePropertyChanged();
                }
            }
        }

        public HotkeySettings InclusiveMouseActivationShortcut
        {
            get
            {
                return InclusiveMouseSettingsConfig.Properties.ActivationShortcut;
            }

            set
            {
                if (InclusiveMouseSettingsConfig.Properties.ActivationShortcut != value)
                {
                    InclusiveMouseSettingsConfig.Properties.ActivationShortcut = value;
                    NotifyInclusiveMousePropertyChanged();
                }
            }
        }

        public string InclusiveMouseCrosshairColor
        {
            get
            {
                return _inclusiveMouseCrosshairColor;
            }

            set
            {
                // The fallback value is based on ToRGBHex's behavior, which returns
                // #FFFFFF if any exceptions are encountered, e.g. from passing in a null value.
                // This extra handling is added here to deal with FxCop warnings.
                value = (value != null) ? SettingsUtilities.ToRGBHex(value) : "#FFFFFF";
                if (!value.Equals(_inclusiveMouseCrosshairColor, StringComparison.OrdinalIgnoreCase))
                {
                    _inclusiveMouseCrosshairColor = value;
                    InclusiveMouseSettingsConfig.Properties.CrosshairColor.Value = value;
                    NotifyInclusiveMousePropertyChanged();
                }
            }
        }

        public int InclusiveMouseCrosshairOpacity
        {
            get
            {
                return _inclusiveMouseCrosshairOpacity;
            }

            set
            {
                if (value != _inclusiveMouseCrosshairOpacity)
                {
                    _inclusiveMouseCrosshairOpacity = value;
                    InclusiveMouseSettingsConfig.Properties.CrosshairOpacity.Value = value;
                    NotifyInclusiveMousePropertyChanged();
                }
            }
        }

        public int InclusiveMouseCrosshairRadius
        {
            get
            {
                return _inclusiveMouseCrosshairRadius;
            }

            set
            {
                if (value != _inclusiveMouseCrosshairRadius)
                {
                    _inclusiveMouseCrosshairRadius = value;
                    InclusiveMouseSettingsConfig.Properties.CrosshairRadius.Value = value;
                    NotifyInclusiveMousePropertyChanged();
                }
            }
        }

        public int InclusiveMouseCrosshairThickness
        {
            get
            {
                return _inclusiveMouseCrosshairThickness;
            }

            set
            {
                if (value != _inclusiveMouseCrosshairThickness)
                {
                    _inclusiveMouseCrosshairThickness = value;
                    InclusiveMouseSettingsConfig.Properties.CrosshairThickness.Value = value;
                    NotifyInclusiveMousePropertyChanged();
                }
            }
        }

        public void NotifyInclusiveMousePropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);

            SndInclusiveMouseSettings outsettings = new SndInclusiveMouseSettings(InclusiveMouseSettingsConfig);
            SndModuleSettings<SndInclusiveMouseSettings> ipcMessage = new SndModuleSettings<SndInclusiveMouseSettings>(outsettings);
            SendConfigMSG(ipcMessage.ToJsonString());
            SettingsUtils.SaveSettings(InclusiveMouseSettingsConfig.ToJsonString(), InclusiveMouseSettings.ModuleName);
        }

        private Func<string, int> SendConfigMSG { get; }

        private bool _isFindMyMouseEnabled;
        private bool _findMyMouseDoNotActivateOnGameMode;
        private string _findMyMouseBackgroundColor;
        private string _findMyMouseSpotlightColor;
        private int _findMyMouseOverlayOpacity;
        private int _findMyMouseSpotlightRadius;
        private int _findMyMouseAnimationDurationMs;
        private int _findMyMouseSpotlightInitialZoom;

        private bool _isMouseHighlighterEnabled;
        private string _highlighterLeftButtonClickColor;
        private string _highlighterRightButtonClickColor;
        private int _highlighterOpacity;
        private int _highlighterRadius;
        private int _highlightFadeDelayMs;
        private int _highlightFadeDurationMs;

        private bool _isInclusiveMouseEnabled;
        private string _inclusiveMouseCrosshairColor;
        private int _inclusiveMouseCrosshairOpacity;
        private int _inclusiveMouseCrosshairRadius;
        private int _inclusiveMouseCrosshairThickness;
    }
}
