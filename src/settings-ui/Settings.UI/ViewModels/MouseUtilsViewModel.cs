// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.Utilities;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class MouseUtilsViewModel : PageViewModelBase
    {
        protected override string ModuleName => "MouseUtils";

        private ISettingsUtils SettingsUtils { get; set; }

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private FindMyMouseSettings FindMyMouseSettingsConfig { get; set; }

        private MouseHighlighterSettings MouseHighlighterSettingsConfig { get; set; }

        private MousePointerCrosshairsSettings MousePointerCrosshairsSettingsConfig { get; set; }

        public MouseUtilsViewModel(ISettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, ISettingsRepository<FindMyMouseSettings> findMyMouseSettingsRepository, ISettingsRepository<MouseHighlighterSettings> mouseHighlighterSettingsRepository, ISettingsRepository<MouseJumpSettings> mouseJumpSettingsRepository, ISettingsRepository<MousePointerCrosshairsSettings> mousePointerCrosshairsSettingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            SettingsUtils = settingsUtils;

            // To obtain the general settings configurations of PowerToys Settings.
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            InitializeEnabledValues();

            // To obtain the find my mouse settings, if the file exists.
            // If not, to create a file with the default settings and to return the default configurations.
            ArgumentNullException.ThrowIfNull(findMyMouseSettingsRepository);

            FindMyMouseSettingsConfig = findMyMouseSettingsRepository.SettingsConfig;
            _findMyMouseActivationMethod = FindMyMouseSettingsConfig.Properties.ActivationMethod.Value < 4 ? FindMyMouseSettingsConfig.Properties.ActivationMethod.Value : 0;
            _findMyMouseIncludeWinKey = FindMyMouseSettingsConfig.Properties.IncludeWinKey.Value;
            _findMyMouseDoNotActivateOnGameMode = FindMyMouseSettingsConfig.Properties.DoNotActivateOnGameMode.Value;

            string backgroundColor = FindMyMouseSettingsConfig.Properties.BackgroundColor.Value;
            _findMyMouseBackgroundColor = !string.IsNullOrEmpty(backgroundColor) ? backgroundColor : "#80000000";

            string spotlightColor = FindMyMouseSettingsConfig.Properties.SpotlightColor.Value;
            _findMyMouseSpotlightColor = !string.IsNullOrEmpty(spotlightColor) ? spotlightColor : "#80FFFFFF";

            _findMyMouseSpotlightRadius = FindMyMouseSettingsConfig.Properties.SpotlightRadius.Value;
            _findMyMouseAnimationDurationMs = FindMyMouseSettingsConfig.Properties.AnimationDurationMs.Value;
            _findMyMouseSpotlightInitialZoom = FindMyMouseSettingsConfig.Properties.SpotlightInitialZoom.Value;
            _findMyMouseExcludedApps = FindMyMouseSettingsConfig.Properties.ExcludedApps.Value;
            _findMyMouseShakingMinimumDistance = FindMyMouseSettingsConfig.Properties.ShakingMinimumDistance.Value;
            _findMyMouseShakingIntervalMs = FindMyMouseSettingsConfig.Properties.ShakingIntervalMs.Value;
            _findMyMouseShakingFactor = FindMyMouseSettingsConfig.Properties.ShakingFactor.Value;

            ArgumentNullException.ThrowIfNull(mouseHighlighterSettingsRepository);

            MouseHighlighterSettingsConfig = mouseHighlighterSettingsRepository.SettingsConfig;
            string leftClickColor = MouseHighlighterSettingsConfig.Properties.LeftButtonClickColor.Value;
            _highlighterLeftButtonClickColor = !string.IsNullOrEmpty(leftClickColor) ? leftClickColor : "#a6FFFF00";

            string rightClickColor = MouseHighlighterSettingsConfig.Properties.RightButtonClickColor.Value;
            _highlighterRightButtonClickColor = !string.IsNullOrEmpty(rightClickColor) ? rightClickColor : "#a60000FF";

            string alwaysColor = MouseHighlighterSettingsConfig.Properties.AlwaysColor.Value;
            _highlighterAlwaysColor = !string.IsNullOrEmpty(alwaysColor) ? alwaysColor : "#00FF0000";
            _isSpotlightModeEnabled = MouseHighlighterSettingsConfig.Properties.SpotlightMode.Value;

            _highlighterRadius = MouseHighlighterSettingsConfig.Properties.HighlightRadius.Value;
            _highlightFadeDelayMs = MouseHighlighterSettingsConfig.Properties.HighlightFadeDelayMs.Value;
            _highlightFadeDurationMs = MouseHighlighterSettingsConfig.Properties.HighlightFadeDurationMs.Value;
            _highlighterAutoActivate = MouseHighlighterSettingsConfig.Properties.AutoActivate.Value;

            this.InitializeMouseJumpSettings(mouseJumpSettingsRepository);

            ArgumentNullException.ThrowIfNull(mousePointerCrosshairsSettingsRepository);

            MousePointerCrosshairsSettingsConfig = mousePointerCrosshairsSettingsRepository.SettingsConfig;

            string crosshairsColor = MousePointerCrosshairsSettingsConfig.Properties.CrosshairsColor.Value;
            _mousePointerCrosshairsColor = !string.IsNullOrEmpty(crosshairsColor) ? crosshairsColor : "#FF0000";

            string crosshairsBorderColor = MousePointerCrosshairsSettingsConfig.Properties.CrosshairsBorderColor.Value;
            _mousePointerCrosshairsBorderColor = !string.IsNullOrEmpty(crosshairsBorderColor) ? crosshairsBorderColor : "#FFFFFF";

            _mousePointerCrosshairsOpacity = MousePointerCrosshairsSettingsConfig.Properties.CrosshairsOpacity.Value;
            _mousePointerCrosshairsRadius = MousePointerCrosshairsSettingsConfig.Properties.CrosshairsRadius.Value;
            _mousePointerCrosshairsThickness = MousePointerCrosshairsSettingsConfig.Properties.CrosshairsThickness.Value;
            _mousePointerCrosshairsBorderSize = MousePointerCrosshairsSettingsConfig.Properties.CrosshairsBorderSize.Value;
            _mousePointerCrosshairsAutoHide = MousePointerCrosshairsSettingsConfig.Properties.CrosshairsAutoHide.Value;
            _mousePointerCrosshairsIsFixedLengthEnabled = MousePointerCrosshairsSettingsConfig.Properties.CrosshairsIsFixedLengthEnabled.Value;
            _mousePointerCrosshairsFixedLength = MousePointerCrosshairsSettingsConfig.Properties.CrosshairsFixedLength.Value;
            _mousePointerCrosshairsOrientation = MousePointerCrosshairsSettingsConfig.Properties.CrosshairsOrientation.Value;
            _mousePointerCrosshairsAutoActivate = MousePointerCrosshairsSettingsConfig.Properties.AutoActivate.Value;

            int isEnabled = 0;

            Utilities.NativeMethods.SystemParametersInfo(Utilities.NativeMethods.SPI_GETCLIENTAREAANIMATION, 0, ref isEnabled, 0);
            _isAnimationEnabledBySystem = isEnabled != 0;

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;
        }

        private void InitializeEnabledValues()
        {
            _findMyMouseEnabledGpoRuleConfiguration = GPOWrapper.GetConfiguredFindMyMouseEnabledValue();
            if (_findMyMouseEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _findMyMouseEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _findMyMouseEnabledStateIsGPOConfigured = true;
                _isFindMyMouseEnabled = _findMyMouseEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isFindMyMouseEnabled = GeneralSettingsConfig.Enabled.FindMyMouse;
            }

            _highlighterEnabledGpoRuleConfiguration = GPOWrapper.GetConfiguredMouseHighlighterEnabledValue();
            if (_highlighterEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _highlighterEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _highlighterEnabledStateIsGPOConfigured = true;
                _isMouseHighlighterEnabled = _highlighterEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isMouseHighlighterEnabled = GeneralSettingsConfig.Enabled.MouseHighlighter;
            }

            this.InitializeMouseJumpEnabledValues();

            _mousePointerCrosshairsEnabledGpoRuleConfiguration = GPOWrapper.GetConfiguredMousePointerCrosshairsEnabledValue();
            if (_mousePointerCrosshairsEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _mousePointerCrosshairsEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _mousePointerCrosshairsEnabledStateIsGPOConfigured = true;
                _isMousePointerCrosshairsEnabled = _mousePointerCrosshairsEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isMousePointerCrosshairsEnabled = GeneralSettingsConfig.Enabled.MousePointerCrosshairs;
            }
        }

        public override Dictionary<string, HotkeySettings[]> GetAllHotkeySettings()
        {
            var hotkeysDict = new Dictionary<string, HotkeySettings[]>
            {
                [FindMyMouseSettings.ModuleName] = [FindMyMouseActivationShortcut],
                [MouseHighlighterSettings.ModuleName] = [MouseHighlighterActivationShortcut],
                [MousePointerCrosshairsSettings.ModuleName] = [
                    MousePointerCrosshairsActivationShortcut,
                    GlidingCursorActivationShortcut],
                [MouseJumpSettings.ModuleName] = [MouseJumpActivationShortcut],
            };

            return hotkeysDict;
        }

        public bool IsFindMyMouseEnabled
        {
            get => _isFindMyMouseEnabled;
            set
            {
                if (_findMyMouseEnabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

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

        public bool IsFindMyMouseEnabledGpoConfigured
        {
            get => _findMyMouseEnabledStateIsGPOConfigured;
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

        public bool FindMyMouseIncludeWinKey
        {
            get
            {
                return _findMyMouseIncludeWinKey;
            }

            set
            {
                if (_findMyMouseIncludeWinKey != value)
                {
                    _findMyMouseIncludeWinKey = value;
                    FindMyMouseSettingsConfig.Properties.IncludeWinKey.Value = value;
                    NotifyFindMyMousePropertyChanged();
                }
            }
        }

        public HotkeySettings FindMyMouseActivationShortcut
        {
            get
            {
                return FindMyMouseSettingsConfig.Properties.ActivationShortcut;
            }

            set
            {
                if (FindMyMouseSettingsConfig.Properties.ActivationShortcut != value)
                {
                    FindMyMouseSettingsConfig.Properties.ActivationShortcut = value ?? FindMyMouseSettingsConfig.Properties.DefaultActivationShortcut;
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
                value = (value != null) ? SettingsUtilities.ToARGBHex(value) : "#FF000000";
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
                value = (value != null) ? SettingsUtilities.ToARGBHex(value) : "#FFFFFFFF";
                if (!value.Equals(_findMyMouseSpotlightColor, StringComparison.OrdinalIgnoreCase))
                {
                    _findMyMouseSpotlightColor = value;
                    FindMyMouseSettingsConfig.Properties.SpotlightColor.Value = value;
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

        public bool IsAnimationEnabledBySystem
        {
            get
            {
                return _isAnimationEnabledBySystem;
            }

            set
            {
                _isAnimationEnabledBySystem = value;
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

        public int FindMyMouseShakingIntervalMs
        {
            get
            {
                return _findMyMouseShakingIntervalMs;
            }

            set
            {
                if (value != _findMyMouseShakingIntervalMs)
                {
                    _findMyMouseShakingIntervalMs = value;
                    FindMyMouseSettingsConfig.Properties.ShakingIntervalMs.Value = value;
                    NotifyFindMyMousePropertyChanged();
                }
            }
        }

        public int FindMyMouseShakingFactor
        {
            get
            {
                return _findMyMouseShakingFactor;
            }

            set
            {
                if (value != _findMyMouseShakingFactor)
                {
                    _findMyMouseShakingFactor = value;
                    FindMyMouseSettingsConfig.Properties.ShakingFactor.Value = value;
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
                if (_highlighterEnabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

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

        public bool IsHighlighterEnabledGpoConfigured
        {
            get => _highlighterEnabledStateIsGPOConfigured;
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
                    MouseHighlighterSettingsConfig.Properties.ActivationShortcut = value ?? MouseHighlighterSettingsConfig.Properties.DefaultActivationShortcut;
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
                value = SettingsUtilities.ToARGBHex(value);
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
                value = SettingsUtilities.ToARGBHex(value);
                if (!value.Equals(_highlighterRightButtonClickColor, StringComparison.OrdinalIgnoreCase))
                {
                    _highlighterRightButtonClickColor = value;
                    MouseHighlighterSettingsConfig.Properties.RightButtonClickColor.Value = value;
                    NotifyMouseHighlighterPropertyChanged();
                }
            }
        }

        public string MouseHighlighterAlwaysColor
        {
            get
            {
                return _highlighterAlwaysColor;
            }

            set
            {
                value = SettingsUtilities.ToARGBHex(value);
                if (!value.Equals(_highlighterAlwaysColor, StringComparison.OrdinalIgnoreCase))
                {
                    _highlighterAlwaysColor = value;
                    MouseHighlighterSettingsConfig.Properties.AlwaysColor.Value = value;
                    NotifyMouseHighlighterPropertyChanged();
                }
            }
        }

        public bool IsSpotlightModeEnabled
        {
            get => _isSpotlightModeEnabled;
            set
            {
                if (_isSpotlightModeEnabled != value)
                {
                    _isSpotlightModeEnabled = value;
                    MouseHighlighterSettingsConfig.Properties.SpotlightMode.Value = value;
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

        public bool MouseHighlighterAutoActivate
        {
            get
            {
                return _highlighterAutoActivate;
            }

            set
            {
                if (value != _highlighterAutoActivate)
                {
                    _highlighterAutoActivate = value;
                    MouseHighlighterSettingsConfig.Properties.AutoActivate.Value = value;
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
                if (_mousePointerCrosshairsEnabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

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

        public bool IsMousePointerCrosshairsEnabledGpoConfigured
        {
            get => _mousePointerCrosshairsEnabledStateIsGPOConfigured;
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
                    MousePointerCrosshairsSettingsConfig.Properties.ActivationShortcut = value ?? MousePointerCrosshairsSettingsConfig.Properties.DefaultActivationShortcut;
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

        public bool MousePointerCrosshairsAutoHide
        {
            get
            {
                return _mousePointerCrosshairsAutoHide;
            }

            set
            {
                if (value != _mousePointerCrosshairsAutoHide)
                {
                    _mousePointerCrosshairsAutoHide = value;
                    MousePointerCrosshairsSettingsConfig.Properties.CrosshairsAutoHide.Value = value;
                    NotifyMousePointerCrosshairsPropertyChanged();
                }
            }
        }

        public bool MousePointerCrosshairsIsFixedLengthEnabled
        {
            get
            {
                return _mousePointerCrosshairsIsFixedLengthEnabled;
            }

            set
            {
                if (value != _mousePointerCrosshairsIsFixedLengthEnabled)
                {
                    _mousePointerCrosshairsIsFixedLengthEnabled = value;
                    MousePointerCrosshairsSettingsConfig.Properties.CrosshairsIsFixedLengthEnabled.Value = value;
                    NotifyMousePointerCrosshairsPropertyChanged();
                }
            }
        }

        public int MousePointerCrosshairsFixedLength
        {
            get
            {
                return _mousePointerCrosshairsFixedLength;
            }

            set
            {
                if (value != _mousePointerCrosshairsFixedLength)
                {
                    _mousePointerCrosshairsFixedLength = value;
                    MousePointerCrosshairsSettingsConfig.Properties.CrosshairsFixedLength.Value = value;
                    NotifyMousePointerCrosshairsPropertyChanged();
                }
            }
        }

        public int MousePointerCrosshairsOrientation
        {
            get
            {
                return _mousePointerCrosshairsOrientation;
            }

            set
            {
                if (value != _mousePointerCrosshairsOrientation)
                {
                    _mousePointerCrosshairsOrientation = value;
                    MousePointerCrosshairsSettingsConfig.Properties.CrosshairsOrientation.Value = value;
                    NotifyMousePointerCrosshairsPropertyChanged();
                }
            }
        }

        public bool MousePointerCrosshairsAutoActivate
        {
            get
            {
                return _mousePointerCrosshairsAutoActivate;
            }

            set
            {
                if (value != _mousePointerCrosshairsAutoActivate)
                {
                    _mousePointerCrosshairsAutoActivate = value;
                    MousePointerCrosshairsSettingsConfig.Properties.AutoActivate.Value = value;
                    NotifyMousePointerCrosshairsPropertyChanged();
                }
            }
        }

        public int GlidingCursorTravelSpeed
        {
            get => MousePointerCrosshairsSettingsConfig.Properties.GlidingTravelSpeed.Value;
            set
            {
                if (MousePointerCrosshairsSettingsConfig.Properties.GlidingTravelSpeed.Value != value)
                {
                    MousePointerCrosshairsSettingsConfig.Properties.GlidingTravelSpeed.Value = value;
                    NotifyMousePointerCrosshairsPropertyChanged();
                }
            }
        }

        public int GlidingCursorDelaySpeed
        {
            get => MousePointerCrosshairsSettingsConfig.Properties.GlidingDelaySpeed.Value;
            set
            {
                if (MousePointerCrosshairsSettingsConfig.Properties.GlidingDelaySpeed.Value != value)
                {
                    MousePointerCrosshairsSettingsConfig.Properties.GlidingDelaySpeed.Value = value;
                    NotifyMousePointerCrosshairsPropertyChanged();
                }
            }
        }

        public HotkeySettings GlidingCursorActivationShortcut
        {
            get
            {
                return MousePointerCrosshairsSettingsConfig.Properties.GlidingCursorActivationShortcut;
            }

            set
            {
                if (MousePointerCrosshairsSettingsConfig.Properties.GlidingCursorActivationShortcut != value)
                {
                    MousePointerCrosshairsSettingsConfig.Properties.GlidingCursorActivationShortcut = value ?? MousePointerCrosshairsSettingsConfig.Properties.DefaultGlidingCursorActivationShortcut;
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

        public void RefreshEnabledState()
        {
            InitializeEnabledValues();
            OnPropertyChanged(nameof(IsFindMyMouseEnabled));
            OnPropertyChanged(nameof(IsMouseHighlighterEnabled));
            OnPropertyChanged(nameof(IsMouseJumpEnabled));
            OnPropertyChanged(nameof(IsMousePointerCrosshairsEnabled));
        }

        private Func<string, int> SendConfigMSG { get; }

        private GpoRuleConfigured _findMyMouseEnabledGpoRuleConfiguration;
        private bool _findMyMouseEnabledStateIsGPOConfigured;
        private bool _isFindMyMouseEnabled;
        private int _findMyMouseActivationMethod;
        private bool _findMyMouseIncludeWinKey;
        private bool _findMyMouseDoNotActivateOnGameMode;
        private string _findMyMouseBackgroundColor;
        private string _findMyMouseSpotlightColor;
        private int _findMyMouseSpotlightRadius;
        private int _findMyMouseAnimationDurationMs;
        private int _findMyMouseSpotlightInitialZoom;
        private string _findMyMouseExcludedApps;
        private int _findMyMouseShakingMinimumDistance;
        private int _findMyMouseShakingIntervalMs;
        private int _findMyMouseShakingFactor;

        private GpoRuleConfigured _highlighterEnabledGpoRuleConfiguration;
        private bool _highlighterEnabledStateIsGPOConfigured;
        private bool _isMouseHighlighterEnabled;
        private string _highlighterLeftButtonClickColor;
        private string _highlighterRightButtonClickColor;
        private string _highlighterAlwaysColor;
        private bool _isSpotlightModeEnabled;
        private int _highlighterRadius;
        private int _highlightFadeDelayMs;
        private int _highlightFadeDurationMs;
        private bool _highlighterAutoActivate;

        private GpoRuleConfigured _mousePointerCrosshairsEnabledGpoRuleConfiguration;
        private bool _mousePointerCrosshairsEnabledStateIsGPOConfigured;
        private bool _isMousePointerCrosshairsEnabled;
        private string _mousePointerCrosshairsColor;
        private int _mousePointerCrosshairsOpacity;
        private int _mousePointerCrosshairsRadius;
        private int _mousePointerCrosshairsThickness;
        private string _mousePointerCrosshairsBorderColor;
        private int _mousePointerCrosshairsBorderSize;
        private bool _mousePointerCrosshairsAutoHide;
        private bool _mousePointerCrosshairsIsFixedLengthEnabled;
        private int _mousePointerCrosshairsFixedLength;
        private int _mousePointerCrosshairsOrientation;
        private bool _mousePointerCrosshairsAutoActivate;
        private bool _isAnimationEnabledBySystem;
    }
}
