// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;

using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Settings.UI.Library.Enumerations;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class PowerPreviewViewModel : Observable
    {
        private const string ModuleName = PowerPreviewSettings.ModuleName;

        private PowerPreviewSettings Settings { get; set; }

        private Func<string, int> SendConfigMSG { get; }

        private string _settingsConfigFileFolder = string.Empty;

        private GeneralSettings GeneralSettingsConfig { get; set; }

        public PowerPreviewViewModel(ISettingsRepository<PowerPreviewSettings> moduleSettingsRepository, ISettingsRepository<GeneralSettings> generalSettingsRepository, Func<string, int> ipcMSGCallBackFunc, string configFileSubfolder = "")
        {
            // Update Settings file folder:
            _settingsConfigFileFolder = configFileSubfolder;

            // To obtain the general Settings configurations of PowerToys
            ArgumentNullException.ThrowIfNull(generalSettingsRepository);

            GeneralSettingsConfig = generalSettingsRepository.SettingsConfig;

            // To obtain the PowerPreview settings if it exists.
            // If the file does not exist, to create a new one and return the default settings configurations.
            ArgumentNullException.ThrowIfNull(moduleSettingsRepository);

            Settings = moduleSettingsRepository.SettingsConfig;

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;

            _svgRenderEnabledGpoRuleConfiguration = GPOWrapper.GetConfiguredSvgPreviewEnabledValue();
            if (_svgRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _svgRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _svgRenderEnabledStateIsGPOConfigured = true;
                _svgRenderIsEnabled = _svgRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
                _svgRenderIsGpoEnabled = _svgRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
                _svgRenderIsGpoDisabled = _svgRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled;
            }
            else
            {
                _svgRenderIsEnabled = Settings.Properties.EnableSvgPreview;
            }

            _svgBackgroundColorMode = Settings.Properties.SvgBackgroundColorMode.Value;
            _svgBackgroundSolidColor = Settings.Properties.SvgBackgroundSolidColor.Value;
            _svgBackgroundCheckeredShade = Settings.Properties.SvgBackgroundCheckeredShade.Value;

            _mdRenderEnabledGpoRuleConfiguration = GPOWrapper.GetConfiguredMarkdownPreviewEnabledValue();
            if (_mdRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _mdRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _mdRenderEnabledStateIsGPOConfigured = true;
                _mdRenderIsEnabled = _mdRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
                _mdRenderIsGpoEnabled = _mdRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
                _mdRenderIsGpoDisabled = _mdRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled;
            }
            else
            {
                _mdRenderIsEnabled = Settings.Properties.EnableMdPreview;
            }

            _monacoRenderEnabledGpoRuleConfiguration = GPOWrapper.GetConfiguredMonacoPreviewEnabledValue();
            if (_monacoRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _monacoRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _monacoRenderEnabledStateIsGPOConfigured = true;
                _monacoRenderIsEnabled = _monacoRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
                _monacoRenderIsGpoEnabled = _monacoRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
                _monacoRenderIsGpoDisabled = _monacoRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled;
            }
            else
            {
                _monacoRenderIsEnabled = Settings.Properties.EnableMonacoPreview;
            }

            _monacoWrapText = Settings.Properties.EnableMonacoPreviewWordWrap;
            _monacoPreviewTryFormat = Settings.Properties.MonacoPreviewTryFormat;
            _monacoMaxFileSize = Settings.Properties.MonacoPreviewMaxFileSize.Value;
            _monacoFontSize = Settings.Properties.MonacoPreviewFontSize.Value;
            _monacoStickyScroll = Settings.Properties.MonacoPreviewStickyScroll;
            _monacoMinimap = Settings.Properties.MonacoPreviewMinimap;

            _pdfRenderEnabledGpoRuleConfiguration = GPOWrapper.GetConfiguredPdfPreviewEnabledValue();
            if (_pdfRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _pdfRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _pdfRenderEnabledStateIsGPOConfigured = true;
                _pdfRenderIsEnabled = _pdfRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
                _pdfRenderIsGpoEnabled = _pdfRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
                _pdfRenderIsGpoDisabled = _pdfRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled;
            }
            else
            {
                _pdfRenderIsEnabled = Settings.Properties.EnablePdfPreview;
            }

            _gcodeRenderEnabledGpoRuleConfiguration = GPOWrapper.GetConfiguredGcodePreviewEnabledValue();
            if (_gcodeRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _gcodeRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _gcodeRenderEnabledStateIsGPOConfigured = true;
                _gcodeRenderIsEnabled = _gcodeRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
                _gcodeRenderIsGpoEnabled = _gcodeRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
                _gcodeRenderIsGpoDisabled = _gcodeRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled;
            }
            else
            {
                _gcodeRenderIsEnabled = Settings.Properties.EnableGcodePreview;
            }

            _qoiRenderEnabledGpoRuleConfiguration = GPOWrapper.GetConfiguredQoiPreviewEnabledValue();
            if (_qoiRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _qoiRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _qoiRenderEnabledStateIsGPOConfigured = true;
                _qoiRenderIsEnabled = _qoiRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
                _qoiRenderIsGpoEnabled = _qoiRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
                _qoiRenderIsGpoDisabled = _qoiRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled;
            }
            else
            {
                _qoiRenderIsEnabled = Settings.Properties.EnableQoiPreview;
            }

            _svgThumbnailEnabledGpoRuleConfiguration = GPOWrapper.GetConfiguredSvgThumbnailsEnabledValue();
            if (_svgThumbnailEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _svgThumbnailEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _svgThumbnailEnabledStateIsGPOConfigured = true;
                _svgThumbnailIsEnabled = _svgThumbnailEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
                _svgThumbnailIsGpoEnabled = _svgThumbnailEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
                _svgThumbnailIsGpoDisabled = _svgThumbnailEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled;
            }
            else
            {
                _svgThumbnailIsEnabled = Settings.Properties.EnableSvgThumbnail;
            }

            _pdfThumbnailEnabledGpoRuleConfiguration = GPOWrapper.GetConfiguredPdfThumbnailsEnabledValue();
            if (_pdfThumbnailEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _pdfThumbnailEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _pdfThumbnailEnabledStateIsGPOConfigured = true;
                _pdfThumbnailIsEnabled = _pdfThumbnailEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
                _pdfThumbnailIsGpoEnabled = _pdfThumbnailEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
                _pdfThumbnailIsGpoDisabled = _pdfThumbnailEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled;
            }
            else
            {
                _pdfThumbnailIsEnabled = Settings.Properties.EnablePdfThumbnail;
            }

            _gcodeThumbnailEnabledGpoRuleConfiguration = GPOWrapper.GetConfiguredGcodeThumbnailsEnabledValue();
            if (_gcodeThumbnailEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _gcodeThumbnailEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _gcodeThumbnailEnabledStateIsGPOConfigured = true;
                _gcodeThumbnailIsEnabled = _gcodeThumbnailEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
                _gcodeThumbnailIsGpoEnabled = _gcodeThumbnailEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
                _gcodeThumbnailIsGpoDisabled = _gcodeThumbnailEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled;
            }
            else
            {
                _gcodeThumbnailIsEnabled = Settings.Properties.EnableGcodeThumbnail;
            }

            _stlThumbnailEnabledGpoRuleConfiguration = GPOWrapper.GetConfiguredStlThumbnailsEnabledValue();
            if (_stlThumbnailEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _stlThumbnailEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _stlThumbnailEnabledStateIsGPOConfigured = true;
                _stlThumbnailIsEnabled = _stlThumbnailEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
                _stlThumbnailIsGpoEnabled = _stlThumbnailEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
                _stlThumbnailIsGpoDisabled = _stlThumbnailEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled;
            }
            else
            {
                _stlThumbnailIsEnabled = Settings.Properties.EnableStlThumbnail;
            }

            _stlThumbnailColor = Settings.Properties.StlThumbnailColor.Value;

            _qoiThumbnailEnabledGpoRuleConfiguration = GPOWrapper.GetConfiguredQoiThumbnailsEnabledValue();
            if (_qoiThumbnailEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _qoiThumbnailEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _qoiThumbnailEnabledStateIsGPOConfigured = true;
                _qoiThumbnailIsEnabled = _qoiThumbnailEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
                _qoiThumbnailIsGpoEnabled = _qoiThumbnailEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
                _qoiThumbnailIsGpoDisabled = _qoiThumbnailEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled;
            }
            else
            {
                _qoiThumbnailIsEnabled = Settings.Properties.EnableQoiThumbnail;
            }
        }

        private GpoRuleConfigured _svgRenderEnabledGpoRuleConfiguration;
        private bool _svgRenderEnabledStateIsGPOConfigured;
        private bool _svgRenderIsGpoEnabled;
        private bool _svgRenderIsGpoDisabled;
        private bool _svgRenderIsEnabled;
        private int _svgBackgroundColorMode;
        private string _svgBackgroundSolidColor;
        private int _svgBackgroundCheckeredShade;

        private GpoRuleConfigured _mdRenderEnabledGpoRuleConfiguration;
        private bool _mdRenderEnabledStateIsGPOConfigured;
        private bool _mdRenderIsGpoEnabled;
        private bool _mdRenderIsGpoDisabled;
        private bool _mdRenderIsEnabled;

        private GpoRuleConfigured _monacoRenderEnabledGpoRuleConfiguration;
        private bool _monacoRenderEnabledStateIsGPOConfigured;
        private bool _monacoRenderIsGpoEnabled;
        private bool _monacoRenderIsGpoDisabled;
        private bool _monacoRenderIsEnabled;
        private bool _monacoWrapText;
        private bool _monacoPreviewTryFormat;
        private int _monacoMaxFileSize;
        private bool _monacoStickyScroll;
        private int _monacoFontSize;
        private bool _monacoMinimap;

        private GpoRuleConfigured _pdfRenderEnabledGpoRuleConfiguration;
        private bool _pdfRenderEnabledStateIsGPOConfigured;
        private bool _pdfRenderIsGpoEnabled;
        private bool _pdfRenderIsGpoDisabled;
        private bool _pdfRenderIsEnabled;

        private GpoRuleConfigured _gcodeRenderEnabledGpoRuleConfiguration;
        private bool _gcodeRenderEnabledStateIsGPOConfigured;
        private bool _gcodeRenderIsGpoEnabled;
        private bool _gcodeRenderIsGpoDisabled;
        private bool _gcodeRenderIsEnabled;

        private GpoRuleConfigured _qoiRenderEnabledGpoRuleConfiguration;
        private bool _qoiRenderEnabledStateIsGPOConfigured;
        private bool _qoiRenderIsGpoEnabled;
        private bool _qoiRenderIsGpoDisabled;
        private bool _qoiRenderIsEnabled;

        private GpoRuleConfigured _svgThumbnailEnabledGpoRuleConfiguration;
        private bool _svgThumbnailEnabledStateIsGPOConfigured;
        private bool _svgThumbnailIsGpoEnabled;
        private bool _svgThumbnailIsGpoDisabled;
        private bool _svgThumbnailIsEnabled;

        private GpoRuleConfigured _pdfThumbnailEnabledGpoRuleConfiguration;
        private bool _pdfThumbnailEnabledStateIsGPOConfigured;
        private bool _pdfThumbnailIsGpoEnabled;
        private bool _pdfThumbnailIsGpoDisabled;
        private bool _pdfThumbnailIsEnabled;

        private GpoRuleConfigured _gcodeThumbnailEnabledGpoRuleConfiguration;
        private bool _gcodeThumbnailEnabledStateIsGPOConfigured;
        private bool _gcodeThumbnailIsGpoEnabled;
        private bool _gcodeThumbnailIsGpoDisabled;
        private bool _gcodeThumbnailIsEnabled;

        private GpoRuleConfigured _stlThumbnailEnabledGpoRuleConfiguration;
        private bool _stlThumbnailEnabledStateIsGPOConfigured;
        private bool _stlThumbnailIsGpoEnabled;
        private bool _stlThumbnailIsGpoDisabled;
        private bool _stlThumbnailIsEnabled;
        private string _stlThumbnailColor;

        private GpoRuleConfigured _qoiThumbnailEnabledGpoRuleConfiguration;
        private bool _qoiThumbnailEnabledStateIsGPOConfigured;
        private bool _qoiThumbnailIsGpoEnabled;
        private bool _qoiThumbnailIsGpoDisabled;
        private bool _qoiThumbnailIsEnabled;

        public bool SomePreviewPaneEnabledGposConfigured
        {
            get
            {
                return _svgRenderEnabledStateIsGPOConfigured || _mdRenderEnabledStateIsGPOConfigured
                    || _monacoRenderEnabledStateIsGPOConfigured || _pdfRenderEnabledStateIsGPOConfigured
                    || _gcodeRenderEnabledStateIsGPOConfigured || _qoiRenderEnabledStateIsGPOConfigured;
            }
        }

        public bool SomeThumbnailEnabledGposConfigured
        {
            get
            {
                return _svgThumbnailEnabledStateIsGPOConfigured || _pdfThumbnailEnabledStateIsGPOConfigured
                    || _gcodeThumbnailEnabledStateIsGPOConfigured || _stlThumbnailEnabledStateIsGPOConfigured
                    || _qoiThumbnailEnabledStateIsGPOConfigured;
            }
        }

        public bool SVGRenderIsEnabled
        {
            get
            {
                return _svgRenderIsEnabled;
            }

            set
            {
                if (_svgRenderEnabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

                if (value != _svgRenderIsEnabled)
                {
                    _svgRenderIsEnabled = value;
                    Settings.Properties.EnableSvgPreview = value;
                    RaisePropertyChanged();
                }
            }
        }

        public int SVGRenderBackgroundColorMode
        {
            get
            {
                return _svgBackgroundColorMode;
            }

            set
            {
                if (value != _svgBackgroundColorMode)
                {
                    _svgBackgroundColorMode = value;
                    Settings.Properties.SvgBackgroundColorMode.Value = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(IsSvgBackgroundColorVisible));
                    RaisePropertyChanged(nameof(IsSvgCheckeredShadeVisible));
                }
            }
        }

        public bool IsSvgBackgroundColorVisible
        {
            get
            {
                return (SvgPreviewColorMode)SVGRenderBackgroundColorMode == SvgPreviewColorMode.SolidColor;
            }
        }

        public string SVGRenderBackgroundSolidColor
        {
            get
            {
                return _svgBackgroundSolidColor;
            }

            set
            {
                if (value != _svgBackgroundSolidColor)
                {
                    _svgBackgroundSolidColor = value;
                    Settings.Properties.SvgBackgroundSolidColor = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool IsSvgCheckeredShadeVisible
        {
            get
            {
                return (SvgPreviewColorMode)SVGRenderBackgroundColorMode == SvgPreviewColorMode.Checkered;
            }
        }

        public int SVGRenderBackgroundCheckeredShade
        {
            get
            {
                return _svgBackgroundCheckeredShade;
            }

            set
            {
                if (value != _svgBackgroundCheckeredShade)
                {
                    _svgBackgroundCheckeredShade = value;
                    Settings.Properties.SvgBackgroundCheckeredShade.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        // Used to only disable enabled button on forced enabled state. (With this users still able to change the utility properties.)
        public bool SVGRenderIsGpoEnabled
        {
            get
            {
                return _svgRenderIsGpoEnabled;
            }
        }

        // Used to disable the settings card on forced disabled state.
        public bool SVGRenderIsGpoDisabled
        {
            get
            {
                return _svgRenderIsGpoDisabled;
            }
        }

        public bool SVGThumbnailIsEnabled
        {
            get
            {
                return _svgThumbnailIsEnabled;
            }

            set
            {
                if (_svgThumbnailEnabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

                if (value != _svgThumbnailIsEnabled)
                {
                    _svgThumbnailIsEnabled = value;
                    Settings.Properties.EnableSvgThumbnail = value;
                    RaisePropertyChanged();
                }
            }
        }

        // Used to only disable enabled button on forced enabled state. (With this users still able to change the utility properties.)
        public bool SVGThumbnailIsGpoEnabled
        {
            get
            {
                return _svgThumbnailIsGpoEnabled;
            }
        }

        // Used to disable the settings card on forced disabled state.
        public bool SVGThumbnailIsGpoDisabled
        {
            get
            {
                return _svgThumbnailIsGpoDisabled;
            }
        }

        public bool MDRenderIsEnabled
        {
            get
            {
                return _mdRenderIsEnabled;
            }

            set
            {
                if (_mdRenderEnabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

                if (value != _mdRenderIsEnabled)
                {
                    _mdRenderIsEnabled = value;
                    Settings.Properties.EnableMdPreview = value;
                    RaisePropertyChanged();
                }
            }
        }

        // Used to only disable enabled button on forced enabled state. (With this users still able to change the utility properties.)
        public bool MDRenderIsGpoEnabled
        {
            get
            {
                return _mdRenderIsGpoEnabled;
            }
        }

        // Used to disable the settings card on forced disabled state.
        public bool MDRenderIsGpoDisabled
        {
            get
            {
                return _mdRenderIsGpoDisabled;
            }
        }

        public bool MonacoRenderIsEnabled
        {
            get
            {
                return _monacoRenderIsEnabled;
            }

            set
            {
                if (_monacoRenderEnabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

                if (value != _monacoRenderIsEnabled)
                {
                    _monacoRenderIsEnabled = value;
                    Settings.Properties.EnableMonacoPreview = value;
                    RaisePropertyChanged();
                }
            }
        }

        // Used to only disable enabled button on forced enabled state. (With this users still able to change the utility properties.)
        public bool MonacoRenderIsGpoEnabled
        {
            get
            {
                return _monacoRenderIsGpoEnabled;
            }
        }

        // Used to disable the settings card on forced disabled state.
        public bool MonacoRenderIsGpoDisabled
        {
            get
            {
                return _monacoRenderIsGpoDisabled;
            }
        }

        public bool MonacoWrapText
        {
            get
            {
                return _monacoWrapText;
            }

            set
            {
                if (_monacoWrapText != value)
                {
                    _monacoWrapText = value;
                    Settings.Properties.EnableMonacoPreviewWordWrap = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool MonacoPreviewTryFormat
        {
            get
            {
                return _monacoPreviewTryFormat;
            }

            set
            {
                if (_monacoPreviewTryFormat != value)
                {
                    _monacoPreviewTryFormat = value;
                    Settings.Properties.MonacoPreviewTryFormat = value;
                    RaisePropertyChanged();
                }
            }
        }

        public int MonacoPreviewMaxFileSize
        {
            get
            {
                return _monacoMaxFileSize;
            }

            set
            {
                if (_monacoMaxFileSize != value)
                {
                    _monacoMaxFileSize = value;
                    Settings.Properties.MonacoPreviewMaxFileSize.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool MonacoPreviewStickyScroll
        {
            get
            {
                return _monacoStickyScroll;
            }

            set
            {
                if (_monacoStickyScroll != value)
                {
                    _monacoStickyScroll = value;
                    Settings.Properties.MonacoPreviewStickyScroll = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool MonacoPreviewMinimap
        {
            get => _monacoMinimap;
            set
            {
                if (_monacoMinimap != value)
                {
                    _monacoMinimap = value;
                    Settings.Properties.MonacoPreviewMinimap = value;
                    RaisePropertyChanged();
                }
            }
        }

        public int MonacoPreviewFontSize
        {
            get
            {
                return _monacoFontSize;
            }

            set
            {
                if (_monacoFontSize != value)
                {
                    _monacoFontSize = value;
                    Settings.Properties.MonacoPreviewFontSize.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool PDFRenderIsEnabled
        {
            get
            {
                return _pdfRenderIsEnabled;
            }

            set
            {
                if (_pdfRenderEnabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

                if (value != _pdfRenderIsEnabled)
                {
                    _pdfRenderIsEnabled = value;
                    Settings.Properties.EnablePdfPreview = value;
                    RaisePropertyChanged();
                }
            }
        }

        // Used to only disable enabled button on forced enabled state. (With this users still able to change the utility properties.)
        public bool PDFRenderIsGpoEnabled
        {
            get
            {
                return _pdfRenderIsGpoEnabled;
            }
        }

        // Used to disable the settings card on forced disabled state.
        public bool PDFRenderIsGpoDisabled
        {
            get
            {
                return _pdfRenderIsGpoDisabled;
            }
        }

        public bool PDFThumbnailIsEnabled
        {
            get
            {
                return _pdfThumbnailIsEnabled;
            }

            set
            {
                if (_pdfThumbnailEnabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

                if (value != _pdfThumbnailIsEnabled)
                {
                    _pdfThumbnailIsEnabled = value;
                    Settings.Properties.EnablePdfThumbnail = value;
                    RaisePropertyChanged();
                }
            }
        }

        // Used to only disable enabled button on forced enabled state. (With this users still able to change the utility properties.)
        public bool PDFThumbnailIsGpoEnabled
        {
            get
            {
                return _pdfThumbnailIsGpoEnabled;
            }
        }

        // Used to disable the settings card on forced disabled state.
        public bool PDFThumbnailIsGpoDisabled
        {
            get
            {
                return _pdfThumbnailIsGpoDisabled;
            }
        }

        public bool GCODERenderIsEnabled
        {
            get
            {
                return _gcodeRenderIsEnabled;
            }

            set
            {
                if (_gcodeRenderEnabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

                if (value != _gcodeRenderIsEnabled)
                {
                    _gcodeRenderIsEnabled = value;
                    Settings.Properties.EnableGcodePreview = value;
                    RaisePropertyChanged();
                }
            }
        }

        // Used to only disable enabled button on forced enabled state. (With this users still able to change the utility properties.)
        public bool GCODERenderIsGpoEnabled
        {
            get
            {
                return _gcodeRenderIsGpoEnabled;
            }
        }

        // Used to disable the settings card on forced disabled state.
        public bool GCODERenderIsGpoDisabled
        {
            get
            {
                return _gcodeRenderIsGpoDisabled;
            }
        }

        public bool GCODEThumbnailIsEnabled
        {
            get
            {
                return _gcodeThumbnailIsEnabled;
            }

            set
            {
                if (_gcodeThumbnailEnabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

                if (value != _gcodeThumbnailIsEnabled)
                {
                    _gcodeThumbnailIsEnabled = value;
                    Settings.Properties.EnableGcodeThumbnail = value;
                    RaisePropertyChanged();
                }
            }
        }

        // Used to only disable enabled button on forced enabled state. (With this users still able to change the utility properties.)
        public bool GCODEThumbnailIsGpoEnabled
        {
            get
            {
                return _gcodeThumbnailIsGpoEnabled;
            }
        }

        // Used to disable the settings card on forced disabled state.
        public bool GCODEThumbnailIsGpoDisabled
        {
            get
            {
                return _gcodeThumbnailIsGpoDisabled;
            }
        }

        public bool STLThumbnailIsEnabled
        {
            get
            {
                return _stlThumbnailIsEnabled;
            }

            set
            {
                if (_stlThumbnailEnabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

                if (value != _stlThumbnailIsEnabled)
                {
                    _stlThumbnailIsEnabled = value;
                    Settings.Properties.EnableStlThumbnail = value;
                    RaisePropertyChanged();
                }
            }
        }

        // Used to only disable enabled button on forced enabled state. (With this users still able to change the utility properties.)
        public bool STLThumbnailIsGpoEnabled
        {
            get
            {
                return _stlThumbnailIsGpoEnabled;
            }
        }

        // Used to disable the settings card on forced disabled state.
        public bool STLThumbnailIsGpoDisabled
        {
            get
            {
                return _stlThumbnailIsGpoDisabled;
            }
        }

        public string STLThumbnailColor
        {
            get
            {
                return _stlThumbnailColor;
            }

            set
            {
                if (value != _stlThumbnailColor)
                {
                    _stlThumbnailColor = value;
                    Settings.Properties.StlThumbnailColor.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool QOIRenderIsEnabled
        {
            get
            {
                return _qoiRenderIsEnabled;
            }

            set
            {
                if (_qoiRenderEnabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

                if (value != _qoiRenderIsEnabled)
                {
                    _qoiRenderIsEnabled = value;
                    Settings.Properties.EnableQoiPreview = value;
                    RaisePropertyChanged();
                }
            }
        }

        // Used to only disable enabled button on forced enabled state. (With this users still able to change the utility properties.)
        public bool QOIRenderIsGpoEnabled
        {
            get
            {
                return _qoiRenderIsGpoEnabled;
            }
        }

        // Used to disable the settings card on forced disabled state.
        public bool QOIRenderIsGpoDisabled
        {
            get
            {
                return _qoiRenderIsGpoDisabled;
            }
        }

        public bool QOIThumbnailIsEnabled
        {
            get
            {
                return _qoiThumbnailIsEnabled;
            }

            set
            {
                if (_qoiThumbnailEnabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

                if (value != _qoiThumbnailIsEnabled)
                {
                    _qoiThumbnailIsEnabled = value;
                    Settings.Properties.EnableQoiThumbnail = value;
                    RaisePropertyChanged();
                }
            }
        }

        // Used to only disable enabled button on forced enabled state. (With this users still able to change the utility properties.)
        public bool QOIThumbnailIsGpoEnabled
        {
            get
            {
                return _qoiRenderIsGpoEnabled;
            }
        }

        // Used to disable the settings card on forced disabled state.
        public bool QOIThumbnailIsGpoDisabled
        {
            get
            {
                return _qoiThumbnailIsGpoDisabled;
            }
        }

        public string GetSettingsSubPath()
        {
            return _settingsConfigFileFolder + "\\" + ModuleName;
        }

        public bool IsElevated
        {
            get
            {
                return GeneralSettingsConfig.IsElevated;
            }
        }

        private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            // Notify UI of property change
            OnPropertyChanged(propertyName);

            if (SendConfigMSG != null)
            {
                SndPowerPreviewSettings snd = new SndPowerPreviewSettings(Settings);
                SndModuleSettings<SndPowerPreviewSettings> ipcMessage = new SndModuleSettings<SndPowerPreviewSettings>(snd);
                SendConfigMSG(ipcMessage.ToJsonString());
            }
        }
    }
}
