// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class ImageResizerViewModel : Observable
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly ISettingsUtils _settingsUtils;

        private ImageResizerSettings Settings { get; set; }

        private const string ModuleName = ImageResizerSettings.ModuleName;

        private Func<string, int> SendConfigMSG { get; }

        public ImageResizerViewModel(ISettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc, Func<string, string> resourceLoader)
        {
            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));

            // To obtain the general settings configurations of PowerToys.
            if (settingsRepository == null)
            {
                throw new ArgumentNullException(nameof(settingsRepository));
            }

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            try
            {
                Settings = _settingsUtils.GetSettings<ImageResizerSettings>(ModuleName);
            }
            catch (Exception e)
            {
                Logger.LogError($"Exception encountered while reading {ModuleName} settings.", e);
#if DEBUG
                if (e is ArgumentException || e is ArgumentNullException || e is PathTooLongException)
                {
                    throw;
                }
#endif
                Settings = new ImageResizerSettings(resourceLoader);
                _settingsUtils.SaveSettings(Settings.ToJsonString(), ModuleName);
            }

            // set the callback functions value to hangle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;

            InitializeEnabledValue();

            _advancedSizes = Settings.Properties.ImageresizerSizes.Value;
            _jpegQualityLevel = Settings.Properties.ImageresizerJpegQualityLevel.Value;
            _pngInterlaceOption = Settings.Properties.ImageresizerPngInterlaceOption.Value;
            _tiffCompressOption = Settings.Properties.ImageresizerTiffCompressOption.Value;
            _fileName = Settings.Properties.ImageresizerFileName.Value;
            _keepDateModified = Settings.Properties.ImageresizerKeepDateModified.Value;
            _encoderGuidId = GetEncoderIndex(Settings.Properties.ImageresizerFallbackEncoder.Value);

            int i = 0;
            foreach (ImageSize size in _advancedSizes)
            {
                size.Id = i;
                i++;
                size.PropertyChanged += SizePropertyChanged;
            }
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredImageResizerEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.ImageResizer;
            }
        }

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private bool _isEnabled;
        private ObservableCollection<ImageSize> _advancedSizes = new ObservableCollection<ImageSize>();
        private int _jpegQualityLevel;
        private int _pngInterlaceOption;
        private int _tiffCompressOption;
        private string _fileName;
        private bool _keepDateModified;
        private int _encoderGuidId;

        public bool IsListViewFocusRequested { get; set; }

        public bool IsEnabled
        {
            get
            {
                return _isEnabled;
            }

            set
            {
                if (_enabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

                if (value != _isEnabled)
                {
                    // To set the status of ImageResizer in the General PowerToys settings.
                    _isEnabled = value;
                    GeneralSettingsConfig.Enabled.ImageResizer = value;
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

        public ObservableCollection<ImageSize> Sizes
        {
            get
            {
                return _advancedSizes;
            }

            set
            {
                SavesImageSizes(value);
                _advancedSizes = value;
                OnPropertyChanged(nameof(Sizes));
            }
        }

        public int JPEGQualityLevel
        {
            get
            {
                return _jpegQualityLevel;
            }

            set
            {
                if (_jpegQualityLevel != value)
                {
                    _jpegQualityLevel = value;
                    Settings.Properties.ImageresizerJpegQualityLevel.Value = value;
                    _settingsUtils.SaveSettings(Settings.ToJsonString(), ModuleName);
                    OnPropertyChanged(nameof(JPEGQualityLevel));
                }
            }
        }

        public int PngInterlaceOption
        {
            get
            {
                return _pngInterlaceOption;
            }

            set
            {
                if (_pngInterlaceOption != value)
                {
                    _pngInterlaceOption = value;
                    Settings.Properties.ImageresizerPngInterlaceOption.Value = value;
                    _settingsUtils.SaveSettings(Settings.ToJsonString(), ModuleName);
                    OnPropertyChanged(nameof(PngInterlaceOption));
                }
            }
        }

        public int TiffCompressOption
        {
            get
            {
                return _tiffCompressOption;
            }

            set
            {
                if (_tiffCompressOption != value)
                {
                    _tiffCompressOption = value;
                    Settings.Properties.ImageresizerTiffCompressOption.Value = value;
                    _settingsUtils.SaveSettings(Settings.ToJsonString(), ModuleName);
                    OnPropertyChanged(nameof(TiffCompressOption));
                }
            }
        }

        public string FileName
        {
            get
            {
                return _fileName;
            }

            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _fileName = value;
                    Settings.Properties.ImageresizerFileName.Value = value;
                    _settingsUtils.SaveSettings(Settings.ToJsonString(), ModuleName);
                    OnPropertyChanged(nameof(FileName));
                }
            }
        }

        public bool KeepDateModified
        {
            get
            {
                return _keepDateModified;
            }

            set
            {
                _keepDateModified = value;
                Settings.Properties.ImageresizerKeepDateModified.Value = value;
                _settingsUtils.SaveSettings(Settings.ToJsonString(), ModuleName);
                OnPropertyChanged(nameof(KeepDateModified));
            }
        }

        public int Encoder
        {
            get
            {
                return _encoderGuidId;
            }

            set
            {
                if (_encoderGuidId != value)
                {
                    _encoderGuidId = value;
                    _settingsUtils.SaveSettings(Settings.Properties.ImageresizerSizes.ToJsonString(), ModuleName, "sizes.json");
                    Settings.Properties.ImageresizerFallbackEncoder.Value = GetEncoderGuid(value);
                    _settingsUtils.SaveSettings(Settings.ToJsonString(), ModuleName);
                    OnPropertyChanged(nameof(Encoder));
                }
            }
        }

        public string EncoderGuid
        {
            get
            {
                return ImageResizerViewModel.GetEncoderGuid(_encoderGuidId);
            }
        }

        public void AddRow(string sizeNamePrefix)
        {
            /// This is a fallback validation to eliminate the warning "CA1062:Validate arguments of public methods" when using the parameter (variable) "sizeNamePrefix" in the code.
            /// If the parameter is unexpectedly empty or null, we fill the parameter with a non-localized string.
            /// Normally the parameter "sizeNamePrefix" can't be null or empty because it is filled with a localized string when we call this method from <see cref="UI.Views.ImageResizerPage.AddSizeButton_Click"/>.
            sizeNamePrefix = string.IsNullOrEmpty(sizeNamePrefix) ? "New Size" : sizeNamePrefix;

            ObservableCollection<ImageSize> imageSizes = Sizes;
            int maxId = imageSizes.Count > 0 ? imageSizes.OrderBy(x => x.Id).Last().Id : -1;
            string sizeName = GenerateNameForNewSize(imageSizes, sizeNamePrefix);

            ImageSize newSize = new ImageSize(maxId + 1, sizeName, ResizeFit.Fit, 854, 480, ResizeUnit.Pixel);
            newSize.PropertyChanged += SizePropertyChanged;
            imageSizes.Add(newSize);
            _advancedSizes = imageSizes;
            SavesImageSizes(imageSizes);

            // Set the focus requested flag to indicate that an add operation has occurred during the ContainerContentChanging event
            IsListViewFocusRequested = true;
        }

        public void DeleteImageSize(int id)
        {
            ImageSize size = _advancedSizes.First(x => x.Id == id);
            ObservableCollection<ImageSize> imageSizes = Sizes;
            imageSizes.Remove(size);

            _advancedSizes = imageSizes;
            SavesImageSizes(imageSizes);
        }

        public void SavesImageSizes(ObservableCollection<ImageSize> imageSizes)
        {
            _settingsUtils.SaveSettings(Settings.Properties.ImageresizerSizes.ToJsonString(), ModuleName, "sizes.json");
            Settings.Properties.ImageresizerSizes = new ImageResizerSizes(imageSizes);
            _settingsUtils.SaveSettings(Settings.ToJsonString(), ModuleName);
        }

        public static string GetEncoderGuid(int value)
        {
            // PNG Encoder guid
            if (value == 0)
            {
                return "1b7cfaf4-713f-473c-bbcd-6137425faeaf";
            }

            // Bitmap Encoder guid
            else if (value == 1)
            {
                return "0af1d87e-fcfe-4188-bdeb-a7906471cbe3";
            }

            // JPEG Encoder guid
            else if (value == 2)
            {
                return "19e4a5aa-5662-4fc5-a0c0-1758028e1057";
            }

            // Tiff encoder guid.
            else if (value == 3)
            {
                return "163bcc30-e2e9-4f0b-961d-a3e9fdb788a3";
            }

            // Tiff encoder guid.
            else if (value == 4)
            {
                return "57a37caa-367a-4540-916b-f183c5093a4b";
            }

            // Gif encoder guid.
            else if (value == 5)
            {
                return "1f8a5601-7d4d-4cbd-9c82-1bc8d4eeb9a5";
            }

            return null;
        }

        public static int GetEncoderIndex(string value)
        {
            // PNG Encoder guid
            if (value == "1b7cfaf4-713f-473c-bbcd-6137425faeaf")
            {
                return 0;
            }

            // Bitmap Encoder guid
            else if (value == "0af1d87e-fcfe-4188-bdeb-a7906471cbe3")
            {
                return 1;
            }

            // JPEG Encoder guid
            else if (value == "19e4a5aa-5662-4fc5-a0c0-1758028e1057")
            {
                return 2;
            }

            // Tiff encoder guid.
            else if (value == "163bcc30-e2e9-4f0b-961d-a3e9fdb788a3")
            {
                return 3;
            }

            // Tiff encoder guid.
            else if (value == "57a37caa-367a-4540-916b-f183c5093a4b")
            {
                return 4;
            }

            // Gif encoder guid.
            else if (value == "1f8a5601-7d4d-4cbd-9c82-1bc8d4eeb9a5")
            {
                return 5;
            }

            return -1;
        }

        public void SizePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            ImageSize modifiedSize = (ImageSize)sender;
            ObservableCollection<ImageSize> imageSizes = Sizes;
            imageSizes.First(x => x.Id == modifiedSize.Id).Update(modifiedSize);
            _advancedSizes = imageSizes;
            SavesImageSizes(imageSizes);
        }

        private static string GenerateNameForNewSize(in ObservableCollection<ImageSize> sizesList, in string namePrefix)
        {
            int newSizeCounter = 0;

            foreach (ImageSize imgSize in sizesList)
            {
                string name = imgSize.Name;

                if (name.StartsWith(namePrefix, StringComparison.InvariantCulture))
                {
                    if (int.TryParse(name.AsSpan(namePrefix.Length), out int number))
                    {
                        if (newSizeCounter < number)
                        {
                            newSizeCounter = number;
                        }
                    }
                }
            }

            return $"{namePrefix} {++newSizeCounter}";
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
        }
    }
}
