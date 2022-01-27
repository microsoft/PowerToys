// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library.ViewModels
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

            _svgRenderIsEnabled = Settings.Properties.EnableSvgPreview;
            _svgThumbnailIsEnabled = Settings.Properties.EnableSvgThumbnail;
            _mdRenderIsEnabled = Settings.Properties.EnableMdPreview;
            _monacoRenderIsEnabled = Settings.Properties.EnableMonacoPreview;
            _pdfRenderIsEnabled = Settings.Properties.EnablePdfPreview;
            _gcodeRenderIsEnabled = Settings.Properties.EnableGcodePreview;
            _pdfThumbnailIsEnabled = Settings.Properties.EnablePdfThumbnail;
            _gcodeThumbnailIsEnabled = Settings.Properties.EnableGcodeThumbnail;
            _stlThumbnailIsEnabled = Settings.Properties.EnableStlThumbnail;
        }

        private bool _svgRenderIsEnabled;
        private bool _mdRenderIsEnabled;
        private bool _monacoRenderIsEnabled;
        private bool _pdfRenderIsEnabled;
        private bool _gcodeRenderIsEnabled;
        private bool _svgThumbnailIsEnabled;
        private bool _pdfThumbnailIsEnabled;
        private bool _gcodeThumbnailIsEnabled;
        private bool _stlThumbnailIsEnabled;

        public bool SVGRenderIsEnabled
        {
            get
            {
                return _svgRenderIsEnabled;
            }

            set
            {
                if (value != _svgRenderIsEnabled)
                {
                    _svgRenderIsEnabled = value;
                    Settings.Properties.EnableSvgPreview = value;
                    RaisePropertyChanged();
                }
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
                if (value != _svgThumbnailIsEnabled)
                {
                    _svgThumbnailIsEnabled = value;
                    Settings.Properties.EnableSvgThumbnail = value;
                    RaisePropertyChanged();
                }
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
                if (value != _mdRenderIsEnabled)
                {
                    _mdRenderIsEnabled = value;
                    Settings.Properties.EnableMdPreview = value;
                    RaisePropertyChanged();
                }
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
                if (value != _monacoRenderIsEnabled)
                {
                    _monacoRenderIsEnabled = value;
                    Settings.Properties.EnableMonacoPreview = value;
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
                if (value != _pdfRenderIsEnabled)
                {
                    _pdfRenderIsEnabled = value;
                    Settings.Properties.EnablePdfPreview = value;
                    RaisePropertyChanged();
                }
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
                if (value != _pdfThumbnailIsEnabled)
                {
                    _pdfThumbnailIsEnabled = value;
                    Settings.Properties.EnablePdfThumbnail = value;
                    RaisePropertyChanged();
                }
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
                if (value != _gcodeRenderIsEnabled)
                {
                    _gcodeRenderIsEnabled = value;
                    Settings.Properties.EnableGcodePreview = value;
                    RaisePropertyChanged();
                }
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
                if (value != _gcodeThumbnailIsEnabled)
                {
                    _gcodeThumbnailIsEnabled = value;
                    Settings.Properties.EnableGcodeThumbnail = value;
                    RaisePropertyChanged();
                }
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
                if (value != _stlThumbnailIsEnabled)
                {
                    _stlThumbnailIsEnabled = value;
                    Settings.Properties.EnableStlThumbnail = value;
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
