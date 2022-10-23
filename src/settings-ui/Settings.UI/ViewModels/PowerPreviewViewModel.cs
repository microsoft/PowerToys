// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class PowerPreviewViewModel : Observable
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
            if (generalSettingsRepository == null)
            {
                throw new ArgumentNullException(nameof(generalSettingsRepository));
            }

            GeneralSettingsConfig = generalSettingsRepository.SettingsConfig;

            // To obtain the PowerPreview settings if it exists.
            // If the file does not exist, to create a new one and return the default settings configurations.
            if (moduleSettingsRepository == null)
            {
                throw new ArgumentNullException(nameof(moduleSettingsRepository));
            }

            Settings = moduleSettingsRepository.SettingsConfig;

            // set the callback functions value to hangle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;

            _svgRenderEnabledGpoRuleConfiguration = GPOWrapper.GetConfiguredSvgPreviewEnabledValue();
            if (_svgRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _svgRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _svgRenderEnabledStateIsGPOConfigured = true;
                _svgRenderIsEnabled = _svgRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _svgRenderIsEnabled = Settings.Properties.EnableSvgPreview;
            }

            _svgThumbnailEnabledGpoRuleConfiguration = GPOWrapper.GetConfiguredSvgThumbnailsEnabledValue();
            if (_svgThumbnailEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _svgThumbnailEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _svgThumbnailEnabledStateIsGPOConfigured = true;
                _svgThumbnailIsEnabled = _svgThumbnailEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _svgThumbnailIsEnabled = Settings.Properties.EnableSvgThumbnail;
            }

            _mdRenderEnabledGpoRuleConfiguration = GPOWrapper.GetConfiguredMarkdownPreviewEnabledValue();
            if (_mdRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _mdRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _mdRenderEnabledStateIsGPOConfigured = true;
                _mdRenderIsEnabled = _mdRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
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
            }
            else
            {
                _monacoRenderIsEnabled = Settings.Properties.EnableMonacoPreview;
            }

            _monacoWrapText = Settings.Properties.EnableMonacoPreviewWordWrap;
            _monacoPreviewTryFormat = Settings.Properties.MonacoPreviewTryFormat;

            _pdfRenderEnabledGpoRuleConfiguration = GPOWrapper.GetConfiguredPdfPreviewEnabledValue();
            if (_pdfRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _pdfRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _pdfRenderEnabledStateIsGPOConfigured = true;
                _pdfRenderIsEnabled = _pdfRenderEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
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
            }
            else
            {
                _gcodeRenderIsEnabled = Settings.Properties.EnableGcodePreview;
            }

            _pdfThumbnailEnabledGpoRuleConfiguration = GPOWrapper.GetConfiguredPdfThumbnailsEnabledValue();
            if (_pdfThumbnailEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _pdfThumbnailEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _pdfThumbnailEnabledStateIsGPOConfigured = true;
                _pdfThumbnailIsEnabled = _pdfThumbnailEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
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
            }
            else
            {
                _stlThumbnailIsEnabled = Settings.Properties.EnableStlThumbnail;
            }

            _stlThumbnailColor = Settings.Properties.StlThumbnailColor.Value;
        }

        private GpoRuleConfigured _svgRenderEnabledGpoRuleConfiguration;
        private bool _svgRenderEnabledStateIsGPOConfigured;
        private bool _svgRenderIsEnabled;

        private GpoRuleConfigured _mdRenderEnabledGpoRuleConfiguration;
        private bool _mdRenderEnabledStateIsGPOConfigured;
        private bool _mdRenderIsEnabled;

        private GpoRuleConfigured _monacoRenderEnabledGpoRuleConfiguration;
        private bool _monacoRenderEnabledStateIsGPOConfigured;
        private bool _monacoRenderIsEnabled;
        private bool _monacoWrapText;
        private bool _monacoPreviewTryFormat;

        private GpoRuleConfigured _pdfRenderEnabledGpoRuleConfiguration;
        private bool _pdfRenderEnabledStateIsGPOConfigured;
        private bool _pdfRenderIsEnabled;

        private GpoRuleConfigured _gcodeRenderEnabledGpoRuleConfiguration;
        private bool _gcodeRenderEnabledStateIsGPOConfigured;
        private bool _gcodeRenderIsEnabled;

        private GpoRuleConfigured _svgThumbnailEnabledGpoRuleConfiguration;
        private bool _svgThumbnailEnabledStateIsGPOConfigured;
        private bool _svgThumbnailIsEnabled;

        private GpoRuleConfigured _pdfThumbnailEnabledGpoRuleConfiguration;
        private bool _pdfThumbnailEnabledStateIsGPOConfigured;
        private bool _pdfThumbnailIsEnabled;

        private GpoRuleConfigured _gcodeThumbnailEnabledGpoRuleConfiguration;
        private bool _gcodeThumbnailEnabledStateIsGPOConfigured;
        private bool _gcodeThumbnailIsEnabled;

        private GpoRuleConfigured _stlThumbnailEnabledGpoRuleConfiguration;
        private bool _stlThumbnailEnabledStateIsGPOConfigured;
        private bool _stlThumbnailIsEnabled;
        private string _stlThumbnailColor;

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

        public bool IsSVGRenderEnabledGpoConfigured
        {
            get => _svgRenderEnabledStateIsGPOConfigured;
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

        public bool IsSVGThumbnailEnabledGpoConfigured
        {
            get => _svgThumbnailEnabledStateIsGPOConfigured;
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

        public bool IsMDRenderEnabledGpoConfigured
        {
            get => _mdRenderEnabledStateIsGPOConfigured;
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

        public bool IsMonacoRenderEnabledGpoConfigured
        {
            get => _monacoRenderEnabledStateIsGPOConfigured;
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

        public bool IsPDFRenderEnabledGpoConfigured
        {
            get => _pdfRenderEnabledStateIsGPOConfigured;
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

        public bool IsPDFThumbnailEnabledGpoConfigured
        {
            get => _pdfThumbnailEnabledStateIsGPOConfigured;
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

        public bool IsGCODERenderEnabledGpoConfigured
        {
            get => _gcodeRenderEnabledStateIsGPOConfigured;
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

        public bool IsGCODEThumbnailEnabledGpoConfigured
        {
            get => _gcodeThumbnailEnabledStateIsGPOConfigured;
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

        public bool IsSTLThumbnailEnabledGpoConfigured
        {
            get => _stlThumbnailEnabledStateIsGPOConfigured;
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
