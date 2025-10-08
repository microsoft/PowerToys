// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using AllExperiments;
using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Devices.Enumeration;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class ZoomItViewModel : Observable
    {
        private ISettingsUtils SettingsUtils { get; set; }

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly ZoomItSettings _zoomItSettings;

        private Func<string, int> SendConfigMSG { get; }

        private Func<string, string, string, int, string> PickFileDialog { get; }

        private Func<LOGFONT, LOGFONT> PickFontDialog { get; }

        public ButtonClickCommand SelectDemoTypeFileCommand { get; set; }

        public ButtonClickCommand SelectBreakSoundFileCommand { get; set; }

        public ButtonClickCommand SelectBreakBackgroundFileCommand { get; set; }

        public ButtonClickCommand SelectTypeFontCommand { get; set; }

        // These values should track what's in DemoType.h
        public int DemoTypeMaxTypingSpeed { get; } = 10;

        public int DemoTypeMinTypingSpeed { get; } = 100;

        public ObservableCollection<Tuple<string, string>> MicrophoneList { get; set; } = new ObservableCollection<Tuple<string, string>>();

        private async void LoadMicrophoneList()
        {
            ResourceLoader resourceLoader = ResourceLoaderInstance.ResourceLoader;
            string recordDefaultMicrophone = resourceLoader.GetString("ZoomIt_Record_Microphones_Default_Name");
            MicrophoneList.Add(new Tuple<string, string>(string.Empty, recordDefaultMicrophone));
            var microphones = await DeviceInformation.FindAllAsync(DeviceClass.AudioCapture);
            foreach (var microphone in microphones)
            {
                MicrophoneList.Add(new Tuple<string, string>(microphone.Id, microphone.Name));
            }
        }

        private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            MaxDepth = 0,
            IncludeFields = true,
        };

        public ZoomItViewModel(ISettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc, Func<string, string, string, int, string> pickFileDialog, Func<LOGFONT, LOGFONT> pickFontDialog)
        {
            ArgumentNullException.ThrowIfNull(settingsUtils);

            SettingsUtils = settingsUtils;

            // To obtain the general settings configurations of PowerToys Settings.
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            var zoomItSettings = global::PowerToys.ZoomItSettingsInterop.ZoomItSettings.LoadSettingsJson();
            _zoomItSettings = JsonSerializer.Deserialize<ZoomItSettings>(zoomItSettings, _serializerOptions);

            InitializeEnabledValue();

            // set the callback functions value to handle outgoing IPC message for the enabled value.
            SendConfigMSG = ipcMSGCallBackFunc;

            // set the callback for when we need the user to pick a file.
            PickFileDialog = pickFileDialog;

            // set the callback for when we need the user to pick a font.
            PickFontDialog = pickFontDialog;

            _typeFont = TypeFont;

            SelectDemoTypeFileCommand = new ButtonClickCommand(SelectDemoTypeFileAction);
            SelectBreakSoundFileCommand = new ButtonClickCommand(SelectBreakSoundFileAction);
            SelectBreakBackgroundFileCommand = new ButtonClickCommand(SelectBreakBackgroundFileAction);
            SelectTypeFontCommand = new ButtonClickCommand(SelectTypeFontAction);

            LoadMicrophoneList();
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredZoomItEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.ZoomIt;
            }
        }

        private void SendCustomAction(string actionName)
        {
            SendConfigMSG("{\"action\":{\"ZoomIt\":{\"action_name\":\"" + actionName + "\", \"value\":\"\"}}}");
        }

        public bool IsEnabled
        {
            get => _isEnabled;

            set
            {
                if (_enabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

                if (value != _isEnabled)
                {
                    _isEnabled = value;

                    // Set the status in the general settings configuration
                    GeneralSettingsConfig.Enabled.ZoomIt = value;
                    OutGoingGeneralSettings snd = new OutGoingGeneralSettings(GeneralSettingsConfig);

                    SendConfigMSG(snd.ToString());
                    OnPropertyChanged(nameof(IsEnabled));
                }
            }
        }

        public bool IsEnabledGpoConfigured
        {
            get => _enabledStateIsGPOConfigured;
        }

        public bool ShowTrayIcon
        {
            get => _zoomItSettings.Properties.ShowTrayIcon.Value;
            set
            {
                if (_zoomItSettings.Properties.ShowTrayIcon.Value != value)
                {
                    _zoomItSettings.Properties.ShowTrayIcon.Value = value;
                    OnPropertyChanged(nameof(ShowTrayIcon));
                    NotifySettingsChanged();
                }
            }
        }

        public HotkeySettings ZoomToggleKey
        {
            get => _zoomItSettings.Properties.ToggleKey.Value;
            set
            {
                if (_zoomItSettings.Properties.ToggleKey.Value != value)
                {
                    _zoomItSettings.Properties.ToggleKey.Value = value ?? ZoomItProperties.DefaultToggleKey;
                    OnPropertyChanged(nameof(ZoomToggleKey));
                    NotifySettingsChanged();
                }
            }
        }

        public bool AnimateZoom
        {
            get => _zoomItSettings.Properties.AnimateZoom.Value;
            set
            {
                if (_zoomItSettings.Properties.AnimateZoom.Value != value)
                {
                    _zoomItSettings.Properties.AnimateZoom.Value = value;
                    OnPropertyChanged(nameof(AnimateZoom));
                    NotifySettingsChanged();
                }
            }
        }

        public bool SmoothImage
        {
            get => _zoomItSettings.Properties.SmoothImage.Value;
            set
            {
                if (_zoomItSettings.Properties.SmoothImage.Value != value)
                {
                    _zoomItSettings.Properties.SmoothImage.Value = value;
                    OnPropertyChanged(nameof(SmoothImage));
                    NotifySettingsChanged();
                }
            }
        }

        public int ZoominSliderLevel
        {
            get => _zoomItSettings.Properties.ZoominSliderLevel.Value;
            set
            {
                if (_zoomItSettings.Properties.ZoominSliderLevel.Value != value)
                {
                    _zoomItSettings.Properties.ZoominSliderLevel.Value = value;
                    OnPropertyChanged(nameof(ZoominSliderLevel));
                    NotifySettingsChanged();
                }
            }
        }

        public HotkeySettings LiveZoomToggleKey
        {
            get => _zoomItSettings.Properties.LiveZoomToggleKey.Value;
            set
            {
                if (_zoomItSettings.Properties.LiveZoomToggleKey.Value != value)
                {
                    _zoomItSettings.Properties.LiveZoomToggleKey.Value = value ?? ZoomItProperties.DefaultLiveZoomToggleKey;
                    OnPropertyChanged(nameof(LiveZoomToggleKey));
                    NotifySettingsChanged();
                }
            }
        }

        public HotkeySettings DrawToggleKey
        {
            get => _zoomItSettings.Properties.DrawToggleKey.Value;
            set
            {
                if (_zoomItSettings.Properties.DrawToggleKey.Value != value)
                {
                    _zoomItSettings.Properties.DrawToggleKey.Value = value ?? ZoomItProperties.DefaultDrawToggleKey;
                    OnPropertyChanged(nameof(DrawToggleKey));
                    NotifySettingsChanged();
                }
            }
        }

        public HotkeySettings RecordToggleKey
        {
            get => _zoomItSettings.Properties.RecordToggleKey.Value;
            set
            {
                if (_zoomItSettings.Properties.RecordToggleKey.Value != value)
                {
                    _zoomItSettings.Properties.RecordToggleKey.Value = value ?? ZoomItProperties.DefaultRecordToggleKey;
                    OnPropertyChanged(nameof(RecordToggleKey));
                    NotifySettingsChanged();
                }
            }
        }

        public HotkeySettings SnipToggleKey
        {
            get => _zoomItSettings.Properties.SnipToggleKey.Value;
            set
            {
                if (_zoomItSettings.Properties.SnipToggleKey.Value != value)
                {
                    _zoomItSettings.Properties.SnipToggleKey.Value = value ?? ZoomItProperties.DefaultSnipToggleKey;
                    OnPropertyChanged(nameof(SnipToggleKey));
                    NotifySettingsChanged();
                }
            }
        }

        public HotkeySettings BreakTimerKey
        {
            get => _zoomItSettings.Properties.BreakTimerKey.Value;
            set
            {
                if (_zoomItSettings.Properties.BreakTimerKey.Value != value)
                {
                    _zoomItSettings.Properties.BreakTimerKey.Value = value ?? ZoomItProperties.DefaultBreakTimerKey;
                    OnPropertyChanged(nameof(BreakTimerKey));
                    NotifySettingsChanged();
                }
            }
        }

        public HotkeySettings DemoTypeToggleKey
        {
            get => _zoomItSettings.Properties.DemoTypeToggleKey.Value;
            set
            {
                if (_zoomItSettings.Properties.DemoTypeToggleKey.Value != value)
                {
                    _zoomItSettings.Properties.DemoTypeToggleKey.Value = value ?? ZoomItProperties.DefaultDemoTypeToggleKey;
                    OnPropertyChanged(nameof(DemoTypeToggleKey));
                    NotifySettingsChanged();
                }
            }
        }

        private LOGFONT _typeFont;

        public LOGFONT TypeFont
        {
            get
            {
                var encodedFont = _zoomItSettings.Properties.Font.Value;
                byte[] decodedFont = Convert.FromBase64String(encodedFont);
                int size = Marshal.SizeOf(typeof(LOGFONT));
                if (size != decodedFont.Length)
                {
                    throw new InvalidOperationException("Expected byte array from saved Settings doesn't match the LOGFONT structure size");
                }

                // Allocate unmanaged memory to hold the byte array
                IntPtr ptr = Marshal.AllocHGlobal(size);
                try
                {
                    // Copy the byte array into unmanaged memory
                    Marshal.Copy(decodedFont, 0, ptr, size);

                    // Marshal the unmanaged memory back to a LOGFONT structure
                    return (LOGFONT)Marshal.PtrToStructure(ptr, typeof(LOGFONT));
                }
                finally
                {
                    // Free the unmanaged memory
                    Marshal.FreeHGlobal(ptr);
                }
            }

            set
            {
                _typeFont = value;
                int size = Marshal.SizeOf(typeof(LOGFONT));
                byte[] bytes = new byte[size];

                // Allocate unmanaged memory for the LOGFONT structure
                IntPtr ptr = Marshal.AllocHGlobal(size);
                try
                {
                    // Marshal the LOGFONT structure to the unmanaged memory
                    Marshal.StructureToPtr(value, ptr, false);

                    // Copy the unmanaged memory into the managed byte array
                    Marshal.Copy(ptr, bytes, 0, size);
                }
                finally
                {
                    // Free the unmanaged memory
                    Marshal.FreeHGlobal(ptr);
                }

                _zoomItSettings.Properties.Font.Value = Convert.ToBase64String(bytes);
                OnPropertyChanged(nameof(DemoSampleFontFamily));
                OnPropertyChanged(nameof(DemoSampleFontSize));
                OnPropertyChanged(nameof(DemoSampleFontWeight));
                OnPropertyChanged(nameof(DemoSampleFontStyle));
                OnPropertyChanged(nameof(DemoSampleTextDecoration));
                NotifySettingsChanged();
            }
        }

        public FontFamily DemoSampleFontFamily
        {
            get
            {
                return new FontFamily(_typeFont.lfFaceName);
            }
        }

        public double DemoSampleFontSize
        {
            get
            {
                return _typeFont.lfHeight <= 0 ? 16 : _typeFont.lfHeight; // 16 is always the height we expect?
            }
        }

        public global::Windows.UI.Text.FontWeight DemoSampleFontWeight
        {
            get
            {
                if (_typeFont.lfWeight <= (int)FontWeight.FW_DONT_CARE)
                {
                    return Microsoft.UI.Text.FontWeights.Normal;
                }
                else if (_typeFont.lfWeight <= (int)FontWeight.FW_THIN)
                {
                    return Microsoft.UI.Text.FontWeights.Thin;
                }
                else if (_typeFont.lfWeight <= (int)FontWeight.FW_EXTRALIGHT)
                {
                    return Microsoft.UI.Text.FontWeights.ExtraLight;
                }
                else if (_typeFont.lfWeight <= (int)FontWeight.FW_LIGHT)
                {
                    return Microsoft.UI.Text.FontWeights.Light;
                }
                else if (_typeFont.lfWeight <= (int)FontWeight.FW_NORMAL)
                {
                    return Microsoft.UI.Text.FontWeights.Normal;
                }
                else if (_typeFont.lfWeight <= (int)FontWeight.FW_MEDIUM)
                {
                    return Microsoft.UI.Text.FontWeights.Medium;
                }
                else if (_typeFont.lfWeight <= (int)FontWeight.FW_SEMIBOLD)
                {
                    return Microsoft.UI.Text.FontWeights.SemiBold;
                }
                else if (_typeFont.lfWeight <= (int)FontWeight.FW_BOLD)
                {
                    return Microsoft.UI.Text.FontWeights.Bold;
                }
                else if (_typeFont.lfWeight <= (int)FontWeight.FW_EXTRABOLD)
                {
                    return Microsoft.UI.Text.FontWeights.ExtraBold;
                }
                else
                {
                    return Microsoft.UI.Text.FontWeights.Black; // FW_HEAVY
                }
            }
        }

        public global::Windows.UI.Text.FontStyle DemoSampleFontStyle
        {
            get => _typeFont.lfItalic != 0 ? global::Windows.UI.Text.FontStyle.Italic : global::Windows.UI.Text.FontStyle.Normal;
        }

        public global::Windows.UI.Text.TextDecorations DemoSampleTextDecoration
        {
            get => _typeFont.lfUnderline != 0 ? global::Windows.UI.Text.TextDecorations.Underline : global::Windows.UI.Text.TextDecorations.None;
        }

        public string DemoTypeFile
        {
            get => _zoomItSettings.Properties.DemoTypeFile.Value;
            set
            {
                if (_zoomItSettings.Properties.DemoTypeFile.Value != value)
                {
                    _zoomItSettings.Properties.DemoTypeFile.Value = value;
                    OnPropertyChanged(nameof(DemoTypeFile));
                    NotifySettingsChanged();
                }
            }
        }

        public bool DemoTypeUserDrivenMode
        {
            get => _zoomItSettings.Properties.DemoTypeUserDrivenMode.Value;
            set
            {
                if (_zoomItSettings.Properties.DemoTypeUserDrivenMode.Value != value)
                {
                    _zoomItSettings.Properties.DemoTypeUserDrivenMode.Value = value;
                    OnPropertyChanged(nameof(DemoTypeUserDrivenMode));
                    NotifySettingsChanged();
                }
            }
        }

        public int DemoTypeSpeedSlider
        {
            get => _zoomItSettings.Properties.DemoTypeSpeedSlider.Value;
            set
            {
                if (_zoomItSettings.Properties.DemoTypeSpeedSlider.Value != value)
                {
                    _zoomItSettings.Properties.DemoTypeSpeedSlider.Value = value;
                    OnPropertyChanged(nameof(DemoTypeSpeedSlider));
                    NotifySettingsChanged();
                }
            }
        }

        public int BreakTimeout
        {
            get => _zoomItSettings.Properties.BreakTimeout.Value;
            set
            {
                if (_zoomItSettings.Properties.BreakTimeout.Value != value)
                {
                    _zoomItSettings.Properties.BreakTimeout.Value = value;
                    OnPropertyChanged(nameof(BreakTimeout));
                    NotifySettingsChanged();
                }
            }
        }

        public bool BreakShowExpiredTime
        {
            get => _zoomItSettings.Properties.ShowExpiredTime.Value;
            set
            {
                if (_zoomItSettings.Properties.ShowExpiredTime.Value != value)
                {
                    _zoomItSettings.Properties.ShowExpiredTime.Value = value;
                    OnPropertyChanged(nameof(BreakShowExpiredTime));
                    NotifySettingsChanged();
                }
            }
        }

        public bool BreakPlaySoundFile
        {
            get => _zoomItSettings.Properties.BreakPlaySoundFile.Value;
            set
            {
                if (_zoomItSettings.Properties.BreakPlaySoundFile.Value != value)
                {
                    _zoomItSettings.Properties.BreakPlaySoundFile.Value = value;
                    OnPropertyChanged(nameof(BreakPlaySoundFile));
                    NotifySettingsChanged();
                }
            }
        }

        public string BreakSoundFile
        {
            get => _zoomItSettings.Properties.BreakSoundFile.Value;
            set
            {
                if (_zoomItSettings.Properties.BreakSoundFile.Value != value)
                {
                    _zoomItSettings.Properties.BreakSoundFile.Value = value;
                    OnPropertyChanged(nameof(BreakSoundFile));
                    NotifySettingsChanged();
                }
            }
        }

        public int BreakTimerOpacityIndex
        {
            get
            {
                return Math.Clamp((_zoomItSettings.Properties.BreakOpacity.Value / 10) - 1, 0, 9);
            }

            set
            {
                int newValue = (value + 1) * 10;
                if (_zoomItSettings.Properties.BreakOpacity.Value != newValue)
                {
                    _zoomItSettings.Properties.BreakOpacity.Value = newValue;
                    OnPropertyChanged(nameof(BreakTimerOpacityIndex));
                    NotifySettingsChanged();
                }
            }
        }

        public int BreakTimerPosition
        {
            get => _zoomItSettings.Properties.BreakTimerPosition.Value;
            set
            {
                if (_zoomItSettings.Properties.BreakTimerPosition.Value != value)
                {
                    _zoomItSettings.Properties.BreakTimerPosition.Value = value;
                    OnPropertyChanged(nameof(BreakTimerPosition));
                    NotifySettingsChanged();
                }
            }
        }

        public bool BreakShowBackgroundFile
        {
            get => _zoomItSettings.Properties.BreakShowBackgroundFile.Value;
            set
            {
                if (_zoomItSettings.Properties.BreakShowBackgroundFile.Value != value)
                {
                    _zoomItSettings.Properties.BreakShowBackgroundFile.Value = value;
                    OnPropertyChanged(nameof(BreakShowBackgroundFile));
                    NotifySettingsChanged();
                }
            }
        }

        public int BreakShowDesktopOrImageFileIndex
        {
            get => _zoomItSettings.Properties.BreakShowDesktop.Value ? 0 : 1;
            set
            {
                bool newValue = value == 0;
                if (_zoomItSettings.Properties.BreakShowDesktop.Value != newValue)
                {
                    _zoomItSettings.Properties.BreakShowDesktop.Value = newValue;
                    OnPropertyChanged(nameof(BreakShowDesktopOrImageFileIndex));
                    NotifySettingsChanged();
                }
            }
        }

        public string BreakBackgroundFile
        {
            get => _zoomItSettings.Properties.BreakBackgroundFile.Value;
            set
            {
                if (_zoomItSettings.Properties.BreakBackgroundFile.Value != value)
                {
                    _zoomItSettings.Properties.BreakBackgroundFile.Value = value;
                    OnPropertyChanged(nameof(BreakBackgroundFile));
                    NotifySettingsChanged();
                }
            }
        }

        public bool BreakBackgroundStretch
        {
            get => _zoomItSettings.Properties.BreakBackgroundStretch.Value;
            set
            {
                if (_zoomItSettings.Properties.BreakBackgroundStretch.Value != value)
                {
                    _zoomItSettings.Properties.BreakBackgroundStretch.Value = value;
                    OnPropertyChanged(nameof(BreakBackgroundStretch));
                    NotifySettingsChanged();
                }
            }
        }

        public int RecordScalingIndex
        {
            get
            {
                return Math.Clamp((_zoomItSettings.Properties.RecordScaling.Value / 10) - 1, 0, 9);
            }

            set
            {
                int newValue = (value + 1) * 10;
                if (_zoomItSettings.Properties.RecordScaling.Value != newValue)
                {
                    _zoomItSettings.Properties.RecordScaling.Value = newValue;
                    OnPropertyChanged(nameof(RecordScalingIndex));
                    NotifySettingsChanged();
                }
            }
        }

        public bool RecordCaptureAudio
        {
            get => _zoomItSettings.Properties.CaptureAudio.Value;
            set
            {
                if (_zoomItSettings.Properties.CaptureAudio.Value != value)
                {
                    _zoomItSettings.Properties.CaptureAudio.Value = value;
                    OnPropertyChanged(nameof(RecordCaptureAudio));
                    NotifySettingsChanged();
                }
            }
        }

        public string RecordMicrophoneDeviceId
        {
            get => _zoomItSettings.Properties.MicrophoneDeviceId.Value;
            set
            {
                if (_zoomItSettings.Properties.MicrophoneDeviceId.Value != value)
                {
                    _zoomItSettings.Properties.MicrophoneDeviceId.Value = value ?? string.Empty; // If we're trying to save a null, just default to empty string, which means default microphone.
                    OnPropertyChanged(nameof(RecordMicrophoneDeviceId));
                    NotifySettingsChanged();
                }
            }
        }

        private void NotifySettingsChanged()
        {
            global::PowerToys.ZoomItSettingsInterop.ZoomItSettings.SaveSettingsJson(
                JsonSerializer.Serialize(_zoomItSettings));
            SendCustomAction("refresh_settings");
        }

        private void SelectDemoTypeFileAction()
        {
            try
            {
                ResourceLoader resourceLoader = ResourceLoaderInstance.ResourceLoader;
                string title = resourceLoader.GetString("ZoomIt_DemoType_File_Picker_Dialog_Title");
                string allFilesFilter = resourceLoader.GetString("FilePicker_AllFilesFilter");
                string pickedFile = PickFileDialog($"{allFilesFilter}\0*.*\0\0", title, null, 0);
                if (pickedFile != null)
                {
                    DemoTypeFile = pickedFile;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error picking Demo Type file.", ex);
            }
        }

        private void SelectBreakSoundFileAction()
        {
            try
            {
                ResourceLoader resourceLoader = ResourceLoaderInstance.ResourceLoader;
                string title = resourceLoader.GetString("ZoomIt_Break_SoundFile_Picker_Dialog_Title");
                string soundFilesFilter = resourceLoader.GetString("FilePicker_ZoomIt_SoundsFilter");
                string allFilesFilter = resourceLoader.GetString("FilePicker_AllFilesFilter");
                string initialDirectory = Environment.ExpandEnvironmentVariables("%WINDIR%\\Media");
                string pickedFile = PickFileDialog($"{soundFilesFilter}\0*.wav\0{allFilesFilter}\0*.*\0\0", title, initialDirectory, 0);
                if (pickedFile != null)
                {
                    BreakSoundFile = pickedFile;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error picking Break Sound file.", ex);
            }
        }

        private void SelectBreakBackgroundFileAction()
        {
            try
            {
                ResourceLoader resourceLoader = ResourceLoaderInstance.ResourceLoader;
                string title = resourceLoader.GetString("ZoomIt_Break_BackgroundFile_Picker_Dialog_Title");
                string bitmapFilesFilter = resourceLoader.GetString("FilePicker_ZoomIt_BitmapFilesFilter");
                string allPictureFilesFilter = resourceLoader.GetString("FilePicker_ZoomIt_AllPicturesFilter");
                string allFilesFilter = resourceLoader.GetString("FilePicker_AllFilesFilter");
                string initialDirectory = Environment.ExpandEnvironmentVariables("%USERPROFILE%\\Pictures");
                string pickedFile = PickFileDialog($"{bitmapFilesFilter} (*.bmp;*.dib)\0*.bmp;*.dib\0PNG (*.png)\0*.png\0JPEG (*.jpg;*.jpeg;*.jpe;*.jfif)\0*.jpg;*.jpeg;*.jpe;*.jfif\0GIF (*.gif)\0*.gif\0{allPictureFilesFilter}\0*.bmp;*.dib;*.png;*.jpg;*.jpeg;*.jpe;*.jfif;*.gif\0{allFilesFilter}\0*.*\0\0", title, initialDirectory, 5);
                if (pickedFile != null)
                {
                    BreakBackgroundFile = pickedFile;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error picking Break Background file.", ex);
            }
        }

        private void SelectTypeFontAction()
        {
            try
            {
                LOGFONT result = PickFontDialog(_typeFont);
                if (result != null)
                {
                    TypeFont = result;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error picking Type font.", ex);
            }
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
        }

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private bool _isEnabled;
    }
}
