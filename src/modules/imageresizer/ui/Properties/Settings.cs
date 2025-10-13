#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Windows.Media.Imaging;

using ImageResizer.Models;
using ManagedCommon;

namespace ImageResizer.Properties
{
    public sealed partial class Settings : IDataErrorInfo, INotifyPropertyChanged
    {
        private static readonly IFileSystem _fileSystem = new FileSystem();
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
        {
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
            WriteIndented = true,
        };

        private static readonly CompositeFormat ValueMustBeBetween = System.Text.CompositeFormat.Parse(Properties.Resources.ValueMustBeBetween);

        // Used to synchronize access to the settings.json file
        private static Mutex _jsonMutex = new Mutex();
        private static string _settingsPath = _fileSystem.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys", "Image Resizer", "settings.json");
        private string _fileNameFormat;
        private bool _shrinkOnly;
        private int _selectedSizeIndex;
        private bool _replace;
        private bool _ignoreOrientation;
        private bool _removeMetadata;
        private int _jpegQualityLevel;
        private PngInterlaceOption _pngInterlaceOption;
        private TiffCompressOption _tiffCompressOption;
        private string _fileName;
        private bool _keepDateModified;
        private System.Guid _fallbackEncoder;
        private CustomSize _customSize;
        private bool _useAiSuperResolution;
        private int _aiSuperResolutionScale;

        public Settings()
        {
            SelectedSizeIndex = 0;
            ShrinkOnly = false;
            Replace = false;
            IgnoreOrientation = true;
            RemoveMetadata = false;
            JpegQualityLevel = 90;
            PngInterlaceOption = System.Windows.Media.Imaging.PngInterlaceOption.Default;
            TiffCompressOption = System.Windows.Media.Imaging.TiffCompressOption.Default;
            FileName = "%1 (%2)";
            Sizes = new ObservableCollection<ResizeSize>
            {
                new ResizeSize(0, "$small$", ResizeFit.Fit, 854, 480, ResizeUnit.Pixel),
                new ResizeSize(1, "$medium$", ResizeFit.Fit, 1366, 768, ResizeUnit.Pixel),
                new ResizeSize(2, "$large$", ResizeFit.Fit, 1920, 1080, ResizeUnit.Pixel),
                new ResizeSize(3, "$phone$", ResizeFit.Fit, 320, 568, ResizeUnit.Pixel),
            };
            KeepDateModified = false;
            FallbackEncoder = new System.Guid("19e4a5aa-5662-4fc5-a0c0-1758028e1057");
            CustomSize = new CustomSize(ResizeFit.Fit, 1024, 640, ResizeUnit.Pixel);
            AllSizes = new AllSizesCollection(this);
            UseAiSuperResolution = false;
            AiSuperResolutionScale = 2;
        }

        [JsonIgnore]
        public IEnumerable<ResizeSize> AllSizes { get; set; }

        // Using OrdinalIgnoreCase since this is internal and used for comparison with symbols
        public string FileNameFormat
            => _fileNameFormat
                ?? (_fileNameFormat = FileName
                    .Replace("{", "{{", StringComparison.OrdinalIgnoreCase)
                    .Replace("}", "}}", StringComparison.OrdinalIgnoreCase)
                    .Replace("%1", "{0}", StringComparison.OrdinalIgnoreCase)
                    .Replace("%2", "{1}", StringComparison.OrdinalIgnoreCase)
                    .Replace("%3", "{2}", StringComparison.OrdinalIgnoreCase)
                    .Replace("%4", "{3}", StringComparison.OrdinalIgnoreCase)
                    .Replace("%5", "{4}", StringComparison.OrdinalIgnoreCase)
                    .Replace("%6", "{5}", StringComparison.OrdinalIgnoreCase));

        [JsonIgnore]
        public ResizeSize SelectedSize
        {
            get => SelectedSizeIndex >= 0 && SelectedSizeIndex < Sizes.Count
                    ? Sizes[SelectedSizeIndex]
                    : CustomSize;
            set
            {
                var index = Sizes.IndexOf(value);
                if (index == -1)
                {
                    index = Sizes.Count;
                }

                SelectedSizeIndex = index;
            }
        }

        string IDataErrorInfo.Error
        {
            get
            {
                return string.Empty;
            }
        }

        string IDataErrorInfo.this[string columnName]
        {
            get
            {
                if (columnName != nameof(JpegQualityLevel))
                {
                    return string.Empty;
                }

                if (JpegQualityLevel < 1 || JpegQualityLevel > 100)
                {
                    // Using CurrentCulture since this is user facing
                    return string.Format(CultureInfo.CurrentCulture, ValueMustBeBetween, 1, 100);
                }

                return string.Empty;
            }
        }

        private class AllSizesCollection : IEnumerable<ResizeSize>, INotifyCollectionChanged, INotifyPropertyChanged
        {
            private ObservableCollection<ResizeSize> _sizes;
            private CustomSize _customSize;

            public AllSizesCollection(Settings settings)
            {
                _sizes = settings.Sizes;
                _customSize = settings.CustomSize;

                _sizes.CollectionChanged += HandleCollectionChanged;
                ((INotifyPropertyChanged)_sizes).PropertyChanged += HandlePropertyChanged;

                settings.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == nameof(Models.CustomSize))
                    {
                        var oldCustomSize = _customSize;
                        _customSize = settings.CustomSize;

                        OnCollectionChanged(
                            new NotifyCollectionChangedEventArgs(
                                NotifyCollectionChangedAction.Replace,
                                _customSize,
                                oldCustomSize,
                                _sizes.Count));
                    }
                    else if (e.PropertyName == nameof(Sizes))
                    {
                        var oldSizes = _sizes;

                        oldSizes.CollectionChanged -= HandleCollectionChanged;
                        ((INotifyPropertyChanged)oldSizes).PropertyChanged -= HandlePropertyChanged;

                        _sizes = settings.Sizes;

                        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

                        _sizes.CollectionChanged += HandleCollectionChanged;
                        ((INotifyPropertyChanged)_sizes).PropertyChanged += HandlePropertyChanged;
                    }
                };
            }

            public event NotifyCollectionChangedEventHandler CollectionChanged;

            public event PropertyChangedEventHandler PropertyChanged;

            public int Count
                => _sizes.Count + 1;

            public ResizeSize this[int index]
                => index == _sizes.Count
                    ? _customSize
                    : _sizes[index];

            public IEnumerator<ResizeSize> GetEnumerator()
                => new AllSizesEnumerator(this);

            private void HandleCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
                => OnCollectionChanged(e);

            private void HandlePropertyChanged(object sender, PropertyChangedEventArgs e)
                => PropertyChanged?.Invoke(this, e);

            private void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
                => CollectionChanged?.Invoke(this, e);

            IEnumerator IEnumerable.GetEnumerator()
                => GetEnumerator();

            private class AllSizesEnumerator : IEnumerator<ResizeSize>
            {
                private readonly AllSizesCollection _list;

                private int _index = -1;

                public AllSizesEnumerator(AllSizesCollection list)
                    => _list = list;

                public ResizeSize Current
                    => _list[_index];

                object IEnumerator.Current
                    => Current;

                public void Dispose()
                {
                }

                public bool MoveNext()
                    => ++_index < _list.Count;

                public void Reset()
                    => _index = -1;
            }
        }

        private static Settings defaultInstance = new Settings();

        [JsonIgnore]
        public static Settings Default
        {
            get
            {
                defaultInstance.Reload();
                return defaultInstance;
            }
        }

        [JsonConverter(typeof(WrappedJsonValueConverter))]
        [JsonPropertyName("imageresizer_selectedSizeIndex")]
        public int SelectedSizeIndex
        {
            get => _selectedSizeIndex;
            set
            {
                _selectedSizeIndex = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(SelectedSize));
            }
        }

        [JsonConverter(typeof(WrappedJsonValueConverter))]
        [JsonPropertyName("imageresizer_shrinkOnly")]
        public bool ShrinkOnly
        {
            get => _shrinkOnly;
            set
            {
                _shrinkOnly = value;
                NotifyPropertyChanged();
            }
        }

        [JsonConverter(typeof(WrappedJsonValueConverter))]
        [JsonPropertyName("imageresizer_replace")]
        public bool Replace
        {
            get => _replace;
            set
            {
                _replace = value;
                NotifyPropertyChanged();
            }
        }

        [JsonConverter(typeof(WrappedJsonValueConverter))]
        [JsonPropertyName("imageresizer_ignoreOrientation")]
        public bool IgnoreOrientation
        {
            get => _ignoreOrientation;
            set
            {
                _ignoreOrientation = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether resizing images removes any metadata that doesn't affect rendering.
        /// Default is false.
        /// </summary>
        /// <remarks>
        /// Preserved Metadata:
        /// System.Photo.Orientation,
        /// System.Image.ColorSpace
        /// </remarks>
        [JsonConverter(typeof(WrappedJsonValueConverter))]
        [JsonPropertyName("imageresizer_removeMetadata")]
        public bool RemoveMetadata
        {
            get => _removeMetadata;
            set
            {
                _removeMetadata = value;
                NotifyPropertyChanged();
            }
        }

        [JsonConverter(typeof(WrappedJsonValueConverter))]
        [JsonPropertyName("imageresizer_jpegQualityLevel")]
        public int JpegQualityLevel
        {
            get => _jpegQualityLevel;
            set
            {
                _jpegQualityLevel = value;
                NotifyPropertyChanged();
            }
        }

        [JsonConverter(typeof(WrappedJsonValueConverter))]
        [JsonPropertyName("imageresizer_pngInterlaceOption")]
        public PngInterlaceOption PngInterlaceOption
        {
            get => _pngInterlaceOption;
            set
            {
                _pngInterlaceOption = value;
                NotifyPropertyChanged();
            }
        }

        [JsonConverter(typeof(WrappedJsonValueConverter))]
        [JsonPropertyName("imageresizer_tiffCompressOption")]
        public TiffCompressOption TiffCompressOption
        {
            get => _tiffCompressOption;
            set
            {
                _tiffCompressOption = value;
                NotifyPropertyChanged();
            }
        }

        [JsonConverter(typeof(WrappedJsonValueConverter))]
        [JsonPropertyName("imageresizer_fileName")]
        public string FileName
        {
            get => _fileName;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new System.ArgumentNullException(nameof(FileName));
                }

                _fileName = value;
                NotifyPropertyChanged();
            }
        }

        [JsonInclude]
        [JsonConverter(typeof(WrappedJsonValueConverter))]
        [JsonPropertyName("imageresizer_sizes")]
        public ObservableCollection<ResizeSize> Sizes { get; private set; }

        [JsonConverter(typeof(WrappedJsonValueConverter))]
        [JsonPropertyName("imageresizer_keepDateModified")]
        public bool KeepDateModified
        {
            get => _keepDateModified;
            set
            {
                _keepDateModified = value;
                NotifyPropertyChanged();
            }
        }

        [JsonConverter(typeof(WrappedJsonValueConverter))]
        [JsonPropertyName("imageresizer_fallbackEncoder")]
        public Guid FallbackEncoder
        {
            get => _fallbackEncoder;
            set
            {
                _fallbackEncoder = value;
                NotifyPropertyChanged();
            }
        }

        [JsonConverter(typeof(WrappedJsonValueConverter))]
        [JsonPropertyName("imageresizer_useAiSuperResolution")]
        public bool UseAiSuperResolution
        {
            get => _useAiSuperResolution;
            set
            {
                _useAiSuperResolution = value;
                NotifyPropertyChanged();
            }
        }

        [JsonConverter(typeof(WrappedJsonValueConverter))]
        [JsonPropertyName("imageresizer_aiSuperResolutionScale")]
        public int AiSuperResolutionScale
        {
            get => _aiSuperResolutionScale;
            set
            {
                if (value < 1 || value > 8)
                {
                    value = 2;
                }

                _aiSuperResolutionScale = value;
                NotifyPropertyChanged();
            }
        }

        [JsonConverter(typeof(WrappedJsonValueConverter))]
        [JsonPropertyName("imageresizer_customSize")]
        public CustomSize CustomSize
        {
            get => _customSize;
            set
            {
                _customSize = value;
                NotifyPropertyChanged();
            }
        }

        public static string SettingsPath { get => _settingsPath; set => _settingsPath = value; }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Save()
        {
            _jsonMutex.WaitOne();
            string jsonData = JsonSerializer.Serialize(new SettingsWrapper() { Properties = this }, _jsonSerializerOptions);

            // Create directory if it doesn't exist
            IFileInfo file = _fileSystem.FileInfo.New(SettingsPath);
            file.Directory.Create();

            // write string to file
            _fileSystem.File.WriteAllText(SettingsPath, jsonData);
            _jsonMutex.ReleaseMutex();
        }

        public void Reload()
        {
            string oldSettingsDir = _fileSystem.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys", "ImageResizer");
            string settingsDir = _fileSystem.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys", "Image Resizer");

            if (_fileSystem.Directory.Exists(oldSettingsDir) && !_fileSystem.Directory.Exists(settingsDir))
            {
                _fileSystem.Directory.Move(oldSettingsDir, settingsDir);
            }

            _jsonMutex.WaitOne();
            if (!_fileSystem.File.Exists(SettingsPath))
            {
                _jsonMutex.ReleaseMutex();
                Save();
                return;
            }

            string jsonData = _fileSystem.File.ReadAllText(SettingsPath);
            var jsonSettings = new Settings();
            try
            {
                jsonSettings = JsonSerializer.Deserialize<SettingsWrapper>(jsonData, _jsonSerializerOptions)?.Properties;
            }
            catch (JsonException)
            {
            }

            // Needs to be called on the App UI thread as the properties are bound to the UI.
            App.Current.Dispatcher.Invoke(() =>
            {
                ShrinkOnly = jsonSettings.ShrinkOnly;
                Replace = jsonSettings.Replace;
                IgnoreOrientation = jsonSettings.IgnoreOrientation;
                RemoveMetadata = jsonSettings.RemoveMetadata;
                JpegQualityLevel = jsonSettings.JpegQualityLevel;
                PngInterlaceOption = jsonSettings.PngInterlaceOption;
                TiffCompressOption = jsonSettings.TiffCompressOption;
                FileName = jsonSettings.FileName;
                KeepDateModified = jsonSettings.KeepDateModified;
                FallbackEncoder = jsonSettings.FallbackEncoder;
                CustomSize = jsonSettings.CustomSize;
                SelectedSizeIndex = jsonSettings.SelectedSizeIndex;
                UseAiSuperResolution = jsonSettings.UseAiSuperResolution;
                AiSuperResolutionScale = jsonSettings.AiSuperResolutionScale is >= 1 and <= 8
                    ? jsonSettings.AiSuperResolutionScale
                    : 2;

                if (jsonSettings.Sizes.Count > 0)
                {
                    Sizes.Clear();
                    Sizes.AddRange(jsonSettings.Sizes);

                    // Ensure Ids are unique and handle missing Ids
                    IdRecoveryHelper.RecoverInvalidIds(Sizes);
                }
            });

            _jsonMutex.ReleaseMutex();
        }
    }
}
