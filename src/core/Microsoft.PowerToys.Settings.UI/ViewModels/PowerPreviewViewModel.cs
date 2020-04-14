// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Views;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class PowerPreviewViewModel : Observable
    {
        private const string ModuleName = "File Explorer Preview";

        private PowerPreviewSettings Settings { get; set; }

        public PowerPreviewViewModel()
        {
            Settings = SettingsUtils.GetSettings<PowerPreviewSettings>(ModuleName);

            this._svgRenderIsEnabled = Settings.properties.IDS_PREVPANE_SVG_BOOL_TOGGLE_CONTROLL.value;
            this._mdRenderIsEnabled = Settings.properties.PREVPANE_MD_BOOL_TOGGLE_CONTROLL_ID.value;
        }

        private bool _svgRenderIsEnabled = false;
        private bool _mdRenderIsEnabled = false;

        public bool SVGRenderIsEnebled
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
                    Settings.properties.IDS_PREVPANE_SVG_BOOL_TOGGLE_CONTROLL.value = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool MDRenderIsEnebled
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
                    Settings.properties.PREVPANE_MD_BOOL_TOGGLE_CONTROLL_ID.value = value;
                    RaisePropertyChanged();
                }
            }
        }

        private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            // Notify UI of property change
            OnPropertyChanged(propertyName);

            if (ShellPage.DefaultSndMSGCallback != null)
            {
                SndPowerPreviewSettings snd = new SndPowerPreviewSettings(Settings);
                SndModuleSettings<SndPowerPreviewSettings> ipcMessage = new SndModuleSettings<SndPowerPreviewSettings>(snd);
                ShellPage.DefaultSndMSGCallback(ipcMessage.ToJsonString());
            }
        }
    }
}
