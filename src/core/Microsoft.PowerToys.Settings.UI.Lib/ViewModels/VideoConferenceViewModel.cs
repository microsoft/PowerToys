// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Lib.Helpers;
using Microsoft.PowerToys.Settings.UI.Lib.ViewModels.Commands;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class VideoConferenceViewModel : Observable
    {
        private VideoConferenceSettings Settings { get; set; }

        private const string ModuleName = "Video Conference";
        private const string ProxyCameraName = "PowerToys VideoConference";

        private Func<string, int> SendConfigMSG { get; }

        private string _settingsConfigFileFolder = string.Empty;

        public VideoConferenceViewModel(Func<string, int> ipcMSGCallBackFunc, string configFileSubfolder = "")
        {
            SendConfigMSG = ipcMSGCallBackFunc;

            _settingsConfigFileFolder = configFileSubfolder;

            try
            {
                Settings = SettingsUtils.GetSettings<VideoConferenceSettings>(GetSettingsSubPath());
            }
            catch
            {
                Settings = new VideoConferenceSettings();
                SettingsUtils.SaveSettings(Settings.ToJsonString(), GetSettingsSubPath());
            }

            CameraNames = interop.CommonManaged.GetAllVideoCaptureDeviceNames();
            MicrophoneNames = interop.CommonManaged.GetAllActiveMicrophoneDeviceNames();
            MicrophoneNames.Insert(0, "[All]");

            var shouldSaveSettings = false;

            if (Settings.Properties.SelectedCamera.Value == string.Empty && CameraNames.Count != 0)
            {
                _selectedCameraIndex = 0;
                Settings.Properties.SelectedCamera.Value = CameraNames[0];
                shouldSaveSettings = true;
            }
            else
            {
                _selectedCameraIndex = CameraNames.FindIndex(name => name == Settings.Properties.SelectedCamera.Value);
            }

            if (Settings.Properties.SelectedMicrophone.Value == string.Empty)
            {
                _selectedMicrophoneIndex = 0;
                Settings.Properties.SelectedMicrophone.Value = MicrophoneNames[0];
                shouldSaveSettings = true;
            }
            else
            {
                _selectedMicrophoneIndex = MicrophoneNames.FindIndex(name => name == Settings.Properties.SelectedMicrophone.Value);
            }

            if (shouldSaveSettings)
            {
                SettingsUtils.SaveSettings(Settings.ToJsonString(), ModuleName);
            }

            GeneralSettings generalSettings;
            try
            {
                generalSettings = SettingsUtils.GetSettings<GeneralSettings>(string.Empty);
            }
            catch
            {
                generalSettings = new GeneralSettings();
                SettingsUtils.SaveSettings(generalSettings.ToJsonString(), string.Empty);
            }

            this._isEnabled = generalSettings.Enabled.VideoConference;
            this._cameraAndMicrophoneMuteHotkey = Settings.Properties.MuteCameraAndMicrophoneHotkey.Value;
            this._mirophoneMuteHotkey = Settings.Properties.MuteMicrophoneHotkey.Value;
            this._cameraMuteHotkey = Settings.Properties.MuteCameraHotkey.Value;
            this.CameraImageOverlayPath = Settings.Properties.CameraOverlayImagePath.Value;
            this.SelectOverlayImage = new ButtonClickCommand(SelectOverlayImageAction);
            this.ClearOverlayImage = new ButtonClickCommand(ClearOverlayImageAction);

            this._hideToolbarWhenUnmuted = Settings.Properties.HideToolbarWhenUnmuted.Value;

            switch (Settings.Properties.ToolbarPosition.Value)
            {
                case "Top left corner":
                    _toolbarPositionIndex = 0;
                    break;
                case "Top center":
                    _toolbarPositionIndex = 1;
                    break;
                case "Top right corner":
                    _toolbarPositionIndex = 2;
                    break;
                case "Bottom left corner":
                    _toolbarPositionIndex = 3;
                    break;
                case "Bottom center":
                    _toolbarPositionIndex = 4;
                    break;
                case "Bottom right corner":
                    _toolbarPositionIndex = 5;
                    break;
            }

            switch (Settings.Properties.ToolbarMonitor.Value)
            {
                case "Main monitor":
                    _toolbarMonitorIndex = 0;
                    break;

                case "All monitors":
                    _toolbarMonitorIndex = 1;
                    break;
            }
        }

        private bool _isEnabled = false;
        private int _toolbarPositionIndex;
        private int _toolbarMonitorIndex;
        private HotkeySettings _cameraAndMicrophoneMuteHotkey;
        private HotkeySettings _mirophoneMuteHotkey;
        private HotkeySettings _cameraMuteHotkey;
        private int _selectedCameraIndex = -1;
        private int _selectedMicrophoneIndex = 0;
        private bool _hideToolbarWhenUnmuted;

        public List<string> CameraNames { get; }

        public List<string> MicrophoneNames { get; }

        public string CameraImageOverlayPath { get; set; }

        public ButtonClickCommand SelectOverlayImage { get; set; }

        public ButtonClickCommand ClearOverlayImage { get; set; }

        private void ClearOverlayImageAction()
        {
            CameraImageOverlayPath = string.Empty;
            Settings.Properties.CameraOverlayImagePath = string.Empty;
            RaisePropertyChanged("CameraImageOverlayPath");
        }

        private void SelectOverlayImageAction()
        {
            try
            {
                string pickedImage = null;
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Filter = "Image Files (*.jpeg;*.jpg;*.png)|*.jpeg;*.jpg;*.png";
                    openFileDialog.RestoreDirectory = true;
                    if (openFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        pickedImage = openFileDialog.FileName;
                    }
                }

                if (pickedImage != null)
                {
                    CameraImageOverlayPath = pickedImage;
                    Settings.Properties.CameraOverlayImagePath = pickedImage;
                    RaisePropertyChanged("CameraImageOverlayPath");
                }
            }
            catch
            {
            }
        }

        public int SelectedCameraIndex
        {
            get
            {
                return _selectedCameraIndex;
            }

            set
            {
                if (_selectedCameraIndex != value)
                {
                    _selectedCameraIndex = value;
                    if (_selectedCameraIndex >= 0 && _selectedCameraIndex < CameraNames.Count())
                    {
                        Settings.Properties.SelectedCamera.Value = CameraNames[_selectedCameraIndex];
                        RaisePropertyChanged();
                    }
                }
            }
        }

        public int SelectedMicrophoneIndex
        {
            get
            {
                return _selectedMicrophoneIndex;
            }

            set
            {
                if (_selectedMicrophoneIndex != value)
                {
                    _selectedMicrophoneIndex = value;
                    if (_selectedMicrophoneIndex >= 0 && _selectedMicrophoneIndex < MicrophoneNames.Count())
                    {
                        Settings.Properties.SelectedMicrophone.Value = MicrophoneNames[_selectedMicrophoneIndex];
                        RaisePropertyChanged();
                    }
                }
            }
        }

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
                    GeneralSettings generalSettings = SettingsUtils.GetSettings<GeneralSettings>(string.Empty);
                    generalSettings.Enabled.VideoConference = value;
                    OutGoingGeneralSettings snd = new OutGoingGeneralSettings(generalSettings);

                    SendConfigMSG(snd.ToString());
                    OnPropertyChanged("IsEnabled");
                }
            }
        }

        public HotkeySettings CameraAndMicrophoneMuteHotkey
        {
            get
            {
                return _cameraAndMicrophoneMuteHotkey;
            }

            set
            {
                if (value != _cameraAndMicrophoneMuteHotkey)
                {
                    _cameraAndMicrophoneMuteHotkey = value;
                    Settings.Properties.MuteCameraAndMicrophoneHotkey.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        public HotkeySettings MicrophoneMuteHotkey
        {
            get
            {
                return _mirophoneMuteHotkey;
            }

            set
            {
                if (value != _mirophoneMuteHotkey)
                {
                    _mirophoneMuteHotkey = value;
                    Settings.Properties.MuteMicrophoneHotkey.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        public HotkeySettings CameraMuteHotkey
        {
            get
            {
                return _cameraMuteHotkey;
            }

            set
            {
                if (value != _cameraMuteHotkey)
                {
                    _cameraMuteHotkey = value;
                    Settings.Properties.MuteCameraHotkey.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        public int ToolbarPostionIndex
        {
            get
            {
                return _toolbarPositionIndex;
            }

            set
            {
                if (_toolbarPositionIndex != value)
                {
                    _toolbarPositionIndex = value;
                    switch (_toolbarPositionIndex)
                    {
                        case 0:
                            Settings.Properties.ToolbarPosition.Value = "Top left corner";
                            RaisePropertyChanged();
                            break;

                        case 1:
                            Settings.Properties.ToolbarPosition.Value = "Top center";
                            RaisePropertyChanged();
                            break;

                        case 2:
                            Settings.Properties.ToolbarPosition.Value = "Top right corner";
                            RaisePropertyChanged();
                            break;

                        case 3:
                            Settings.Properties.ToolbarPosition.Value = "Bottom left corner";
                            RaisePropertyChanged();
                            break;

                        case 4:
                            Settings.Properties.ToolbarPosition.Value = "Bottom center";
                            RaisePropertyChanged();
                            break;

                        case 5:
                            Settings.Properties.ToolbarPosition.Value = "Bottom right corner";
                            RaisePropertyChanged();
                            break;
                    }
                }
            }
        }

        public int ToolbarMonitorIndex
        {
            get
            {
                return _toolbarMonitorIndex;
            }

            set
            {
                if (_toolbarMonitorIndex != value)
                {
                    _toolbarMonitorIndex = value;
                    switch (_toolbarMonitorIndex)
                    {
                        case 0:
                            Settings.Properties.ToolbarMonitor.Value = "Main monitor";
                            RaisePropertyChanged();
                            break;

                        case 1:
                            Settings.Properties.ToolbarMonitor.Value = "All monitors";
                            RaisePropertyChanged();
                            break;
                    }
                }
            }
        }

        public bool HideToolbarWhenUnmuted
        {
            get
            {
                return _hideToolbarWhenUnmuted;
            }

            set
            {
                if (value != _hideToolbarWhenUnmuted)
                {
                    _hideToolbarWhenUnmuted = value;
                    Settings.Properties.HideToolbarWhenUnmuted.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string GetSettingsSubPath()
        {
            return _settingsConfigFileFolder + "\\" + ModuleName;
        }

        public void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);
            SndVideoConferenceSettings outsettings = new SndVideoConferenceSettings(Settings);
            SndModuleSettings<SndVideoConferenceSettings> ipcMessage = new SndModuleSettings<SndVideoConferenceSettings>(outsettings);

            SendConfigMSG(ipcMessage.ToJsonString());
        }
    }

    [ComImport]
    [Guid("3E68D4BD-7135-4D10-8018-9FB6D9F33FA1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IInitializeWithWindow
    {
        void Initialize(IntPtr hwnd);
    }
}
