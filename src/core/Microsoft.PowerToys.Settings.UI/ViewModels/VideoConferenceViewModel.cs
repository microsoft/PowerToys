// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.ViewModels.Commands;
using Microsoft.PowerToys.Settings.UI.Views;
using Windows.Devices.Enumeration;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    [ComImport]
    [Guid("3E68D4BD-7135-4D10-8018-9FB6D9F33FA1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IInitializeWithWindow
    {
        void Initialize(IntPtr hwnd);
    }

    public class VideoConferenceViewModel : Observable
    {
        private VideoConferenceSettings Settings { get; set; }

        private const string ModuleName = "Video Conference";
        private const string ProxyCameraName = "PowerToys VideoConference";

        public VideoConferenceViewModel()
        {
            Settings = SettingsUtils.GetOrCreateSettings<VideoConferenceSettings>(ModuleName);

            CameraNames = Task.Run(() => GetAllCameras()).Result.Select(di => di.Name).ToList();
            // Uncomment to have an additional webcam to debug with! (don't forget to install it)
            //CameraNames.Add("DroidCam Source 3");
            if (Settings.Properties.SelectedCamera.Value == string.Empty && CameraNames.Count != 0)
            {
                _selectedCameraIndex = 0;
                Settings.Properties.SelectedCamera.Value = CameraNames[0];
                SettingsUtils.SaveSettings(Settings.ToJsonString(), ModuleName);
            }
            else
            {
                _selectedCameraIndex = CameraNames.FindIndex(name => name == Settings.Properties.SelectedCamera.Value);
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

            this._hideOverlayWhenUnmuted = Settings.Properties.HideOverlayWhenUnmuted.Value;

            switch (Settings.Properties.OverlayPosition.Value)
            {
                case "Top left corner":
                    _overlayPositionIndex = 0;
                    break;
                case "Top center":
                    _overlayPositionIndex = 1;
                    break;
                case "Top right corner":
                    _overlayPositionIndex = 2;
                    break;
                case "Bottom left corner":
                    _overlayPositionIndex = 3;
                    break;
                case "Bottom center":
                    _overlayPositionIndex = 4;
                    break;
                case "Bottom right corner":
                    _overlayPositionIndex = 5;
                    break;
            }

            switch (Settings.Properties.OverlayMonitor.Value)
            {
                case "Main monitor":
                    _overlayMonitorIndex = 0;
                    break;

                case "All monitors":
                    _overlayMonitorIndex = 1;
                    break;
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
        private int _selectedCameraIndex = -1;
        private bool _hideOverlayWhenUnmuted;

        public List<string> CameraNames { get; }

        public string CameraImageOverlayPath { get; set; }

        public ButtonClickCommand SelectOverlayImage { get; set; }

        public ButtonClickCommand ClearOverlayImage { get; set; }

        private void ClearOverlayImageAction()
        {
            CameraImageOverlayPath = string.Empty;
            Settings.Properties.CameraOverlayImagePath = string.Empty;
            RaisePropertyChanged("CameraImageOverlayPath");
        }

        private async void SelectOverlayImageAction()
        {
            try
            {
                FileOpenPicker openPicker = new FileOpenPicker
                {
                    ViewMode = PickerViewMode.Thumbnail,
                    SuggestedStartLocation = PickerLocationId.ComputerFolder,
                };

                openPicker.FileTypeFilter.Add(".jpg");
                openPicker.FileTypeFilter.Add(".jpeg");
                openPicker.FileTypeFilter.Add(".png");
                ((IInitializeWithWindow)(object)openPicker).Initialize(System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle);
                var pickedImage = await openPicker.PickSingleFileAsync();

                if (pickedImage != null)
                {
                    CameraImageOverlayPath = pickedImage.Path;
                    Settings.Properties.CameraOverlayImagePath = pickedImage.Path;
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
                            Settings.Properties.OverlayPosition.Value = "Top left corner";
                            RaisePropertyChanged();
                            break;

                        case 1:
                            Settings.Properties.OverlayPosition.Value = "Top center";
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
                            Settings.Properties.OverlayPosition.Value = "Bottom center";
                            RaisePropertyChanged();
                            break;

                        case 5:
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

        public bool HideOverlayWhenUnmuted
        {
            get
            {
                return _hideOverlayWhenUnmuted;
            }

            set
            {
                if (value != _hideOverlayWhenUnmuted)
                {
                    _hideOverlayWhenUnmuted = value;
                    Settings.Properties.HideOverlayWhenUnmuted.Value = value;
                    RaisePropertyChanged();
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
