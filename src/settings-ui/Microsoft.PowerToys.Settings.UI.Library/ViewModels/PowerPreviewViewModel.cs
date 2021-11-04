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
            _pdfRenderIsEnabled = Settings.Properties.EnablePdfPreview;
            _pdfThumbnailIsEnabled = Settings.Properties.EnablePdfThumbnail;
        }

        private bool _svgRenderIsEnabled;
        private bool _mdRenderIsEnabled;
        private bool _pdfRenderIsEnabled;
        private bool _svgThumbnailIsEnabled;
        private bool _pdfThumbnailIsEnabled;

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
