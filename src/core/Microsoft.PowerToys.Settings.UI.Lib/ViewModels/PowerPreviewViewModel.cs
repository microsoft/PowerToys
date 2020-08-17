// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Lib.Helpers;

namespace Microsoft.PowerToys.Settings.UI.Lib.ViewModels
{
    public class PowerPreviewViewModel : Observable
    {
        private const string ModuleName = "File Explorer";

        private PowerPreviewSettings Settings { get; set; }

        private Func<string, int> SendConfigMSG { get; }

        public string SettingsConfigFileFolder = string.Empty;

        public PowerPreviewViewModel(Func<string, int> ipcMSGCallBackFunc, string configFileSubfolder = "")
        {
            // Update Settings file folder:
            SettingsConfigFileFolder = configFileSubfolder;

            try
            {
                Settings = SettingsUtils.GetSettings<PowerPreviewSettings>(GetSettingsSubPath());
            }
            catch
            {
                Settings = new PowerPreviewSettings();
                SettingsUtils.SaveSettings(Settings.ToJsonString(), GetSettingsSubPath());
            }

            // set the callback functions value to hangle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;

            this._svgRenderIsEnabled = Settings.Properties.EnableSvgPreview;
            this._svgThumbnailIsEnabled = Settings.Properties.EnableSvgThumbnail;
            this._mdRenderIsEnabled = Settings.Properties.EnableMdPreview;
        }

        private bool _svgRenderIsEnabled = false;
        private bool _mdRenderIsEnabled = false;
        private bool _svgThumbnailIsEnabled = false;

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

        public string GetSettingsSubPath()
        {
            return SettingsConfigFileFolder + "\\" + ModuleName;
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
