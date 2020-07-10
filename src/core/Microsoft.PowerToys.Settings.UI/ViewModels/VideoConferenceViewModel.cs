// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Views;
using Windows.Devices.Enumeration;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class VideoConferenceViewModel : Observable
    {
        private VideoConferenceSettings Settings { get; set; }

        private const string ModuleName = "Video Conference";
        private const string ProxyCameraName = "PowerToys VideoConference";

        public VideoConferenceViewModel()
        {
            try
            {
                Settings = SettingsUtils.GetSettings<VideoConferenceSettings>(ModuleName);
            }
            catch
            {
                Settings = new VideoConferenceSettings();
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

            string overlayPosition = Settings.Properties.OverlayPosition.Value;

            switch (overlayPosition)
            {
                case "Center":
                    _overlayPositionIndex = 0;
                    break;
                case "Top left corner":
                    _overlayPositionIndex = 1;
                    break;
                case "Top right corner":
                    _overlayPositionIndex = 2;
                    break;
                case "Bottom left corner":
                    _overlayPositionIndex = 3;
                    break;
                case "Bottom right corner":
                    _overlayPositionIndex = 4;
                    break;
            }

            string overlayMonitor = Settings.Properties.OverlayMonitor.Value;
            switch (overlayMonitor)
            {
                case "Main monitor":
                    _overlayMonitorIndex = 0;
                    break;

                case "All monitors":
                    _overlayMonitorIndex = 1;
                    break;
            }

            var devicesInformation = Task.Run(() => GetAllCameras()).Result;

            _selectedCameraList = new List<string> { };

            int i = 0;
            foreach (DeviceInformation deviceInformation in devicesInformation)
            {
                if (deviceInformation.Name == Settings.Properties.SelectedCamera.Value)
                {
                    _selectedCameraIndex = i;
                }

                _selectedCameraList.Add(deviceInformation.Name);

                i++;
            }
        }

        private static async Task<List<DeviceInformation>> GetAllCameras()
        {
            var allCameras = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            return allCameras.Where(cameraInfo => cameraInfo.Name != ProxyCameraName).ToList();
        }

        private bool _isEnabled = false;
        private int _overlayPositionIndex;
        private int _overlayMonitorIndex;
        private HotkeySettings _cameraAndMicrophoneMuteHotkey;
        private HotkeySettings _mirophoneMuteHotkey;
        private HotkeySettings _cameraMuteHotkey;
        private List<string> _selectedCameraList;
        private int _selectedCameraIndex = 0;

        public List<string> SelectedCameraList
        {
            get
            {
                return _selectedCameraList;
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
                    if (_selectedCameraIndex >= 0 && _selectedCameraIndex < _selectedCameraList.Count())
                    {
                        Settings.Properties.SelectedCamera.Value = _selectedCameraList[_selectedCameraIndex];
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
                    ShellPage.DefaultSndMSGCallback(snd.ToString());
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

        public int OverlayPostionIndex
        {
            get
            {
                return _overlayPositionIndex;
            }

            set
            {
                if (_overlayPositionIndex != value)
                {
                    _overlayPositionIndex = value;
                    switch (_overlayPositionIndex)
                    {
                        case 0:
                            Settings.Properties.OverlayPosition.Value = "Center";
                            RaisePropertyChanged();
                            break;

                        case 1:
                            Settings.Properties.OverlayPosition.Value = "Top left corner";
                            RaisePropertyChanged();
                            break;

                        case 2:
                            Settings.Properties.OverlayPosition.Value = "Top right corner";
                            RaisePropertyChanged();
                            break;

                        case 3:
                            Settings.Properties.OverlayPosition.Value = "Bottom left corner";
                            RaisePropertyChanged();
                            break;

                        case 4:
                            Settings.Properties.OverlayPosition.Value = "Bottom right corner";
                            RaisePropertyChanged();
                            break;

                    }
                }

            }
        }

        public int OverlayMonitorIndex
        {
            get
            {
                return _overlayMonitorIndex;
            }

            set
            {
                if (_overlayMonitorIndex != value)
                {
                    _overlayMonitorIndex = value;
                    switch (_overlayMonitorIndex)
                    {
                        case 0:
                            Settings.Properties.OverlayMonitor.Value = "Main monitor";
                            RaisePropertyChanged();
                            break;

                        case 1:
                            Settings.Properties.OverlayMonitor.Value = "All monitors";
                            RaisePropertyChanged();
                            break;
                    }
                }

            }
        }

        public void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);
            SndVideoConferenceSettings outsettings = new SndVideoConferenceSettings(Settings);
            SndModuleSettings<SndVideoConferenceSettings> ipcMessage = new SndModuleSettings<SndVideoConferenceSettings>(outsettings);
            ShellPage.DefaultSndMSGCallback(ipcMessage.ToJsonString());
        }
    }
}
