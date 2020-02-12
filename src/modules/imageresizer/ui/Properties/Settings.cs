// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows.Media.Imaging;
using ImageResizer.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace ImageResizer.Properties
{
    [JsonObject(MemberSerialization.OptIn)]
    public partial class Settings : IDataErrorInfo, INotifyPropertyChanged
    {
        // Used to synchronize access to the settings.json file
        private static Mutex _jsonMutex = new Mutex();
        private static string _settingsPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys", "ImageResizer", "settings.json");
        private string _fileNameFormat;
        private bool _shrinkOnly;
        private int _selectedSizeIndex;
        private bool _replace;
        private bool _ignoreOrientation;
        private int _jpegQualityLevel;
        private PngInterlaceOption _pngInterlaceOption;
        private TiffCompressOption _tiffCompressOption;
        private string _fileName;
        private ObservableCollection<ImageResizer.Models.ResizeSize> _sizes;
        private bool _keepDateModified;
        private System.Guid _fallbackEncoder;
        private CustomSize _customSize;

        public Settings()
        {
            SelectedSizeIndex = 0;
            ShrinkOnly = false;
            Replace = false;
            IgnoreOrientation = true;
            JpegQualityLevel = 90;
            PngInterlaceOption = System.Windows.Media.Imaging.PngInterlaceOption.Default;
            TiffCompressOption = System.Windows.Media.Imaging.TiffCompressOption.Default;
            FileName = "%1 (%2)";
            Sizes = new ObservableCollection<ResizeSize>
            {
                new ResizeSize("$small$", ResizeFit.Fit, 854, 480, ResizeUnit.Pixel),
                new ResizeSize("$medium$", ResizeFit.Fit, 1366, 768, ResizeUnit.Pixel),
                new ResizeSize("$large$", ResizeFit.Fit, 1920, 1080, ResizeUnit.Pixel),
                new ResizeSize("$phone$", ResizeFit.Fit, 320, 568, ResizeUnit.Pixel),
            };
            KeepDateModified = false;
            FallbackEncoder = new System.Guid("19e4a5aa-5662-4fc5-a0c0-1758028e1057");
            CustomSize = new CustomSize(ResizeFit.Fit, 1024, 640, ResizeUnit.Pixel);
            AllSizes = new AllSizesCollection(this);
        }

        public IEnumerable<ResizeSize> AllSizes { get; set; }

        public string FileNameFormat
            => _fileNameFormat
                ?? (_fileNameFormat = FileName
                    .Replace("{", "{{")
                    .Replace("}", "}}")
                    .Replace("%1", "{0}")
                    .Replace("%2", "{1}")
                    .Replace("%3", "{2}")
                    .Replace("%4", "{3}")
                    .Replace("%5", "{4}")
                    .Replace("%6", "{5}"));

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
            => string.Empty;

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
                    return string.Format(Resources.ValueMustBeBetween, 1, 100);
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

        public static Settings Default
        {
            get
            {
                defaultInstance.Reload();
                return defaultInstance;
            }
        }

        [JsonProperty(PropertyName = "imageresizer_selectedSizeIndex")]
        public int SelectedSizeIndex
        {
            get => _selectedSizeIndex;
            set
            {
                _selectedSizeIndex = value;
                NotifyPropertyChanged();
            }
        }

        [JsonProperty(PropertyName = "imageresizer_shrinkOnly")]
        public bool ShrinkOnly
        {
            get => _shrinkOnly;
            set
            {
                _shrinkOnly = value;
                NotifyPropertyChanged();
            }
        }

        [JsonProperty(PropertyName = "imageresizer_replace")]
        public bool Replace
        {
            get => _replace;
            set
            {
                _replace = value;
                NotifyPropertyChanged();
            }
        }

        [JsonProperty(PropertyName = "imageresizer_ignoreOrientation")]
        public bool IgnoreOrientation
        {
            get => _ignoreOrientation;
            set
            {
                _ignoreOrientation = value;
                NotifyPropertyChanged();
            }
        }

        [JsonProperty(PropertyName = "imageresizer_jpegQualityLevel")]
        public int JpegQualityLevel
        {
            get => _jpegQualityLevel;
            set
            {
                _jpegQualityLevel = value;
                NotifyPropertyChanged();
            }
        }

        [JsonProperty(PropertyName = "imageresizer_pngInterlaceOption")]
        public PngInterlaceOption PngInterlaceOption
        {
            get => _pngInterlaceOption;
            set
            {
                _pngInterlaceOption = value;
                NotifyPropertyChanged();
            }
        }

        [JsonProperty(PropertyName = "imageresizer_tiffCompressOption")]
        public TiffCompressOption TiffCompressOption
        {
            get => _tiffCompressOption;
            set
            {
                _tiffCompressOption = value;
                NotifyPropertyChanged();
            }
        }

        [JsonProperty(PropertyName = "imageresizer_fileName")]
        public string FileName
        {
            get => _fileName;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new System.ArgumentNullException();
                }

                _fileName = value;
                NotifyPropertyChanged();
            }
        }

        [JsonProperty(PropertyName = "imageresizer_sizes")]
        public ObservableCollection<ResizeSize> Sizes
        {
            get => _sizes;
            set
            {
                _sizes = value;
                NotifyPropertyChanged();
            }
        }

        [JsonProperty(PropertyName = "imageresizer_keepDateModified")]
        public bool KeepDateModified
        {
            get => _keepDateModified;
            set
            {
                _keepDateModified = value;
                NotifyPropertyChanged();
            }
        }

        [JsonProperty(PropertyName = "imageresizer_fallbackEncoder")]
        public System.Guid FallbackEncoder
        {
            get => _fallbackEncoder;
            set
            {
                _fallbackEncoder = value;
                NotifyPropertyChanged();
            }
        }

        [JsonProperty(PropertyName = "imageresizer_customSize")]
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
            string jsonData = "{\"version\":\"1.0\",\"name\":\"ImageResizer\",\"properties\":";
            string tempJsonData = JsonConvert.SerializeObject(this);
            JObject tempSettings = JObject.Parse(tempJsonData);

            // Replace the <Value> of the property with { "value": <Value> } to be consistent with PowerToys
            foreach (var property in tempSettings)
            {
                tempSettings[property.Key] = new JObject { { "value", property.Value } };
            }

            jsonData += tempSettings.ToString(Formatting.None);
            jsonData += "}";

            // write string to file
            File.WriteAllText(SettingsPath, jsonData);
            _jsonMutex.ReleaseMutex();
        }

        public void Reload()
        {
            _jsonMutex.WaitOne();
            if (!File.Exists(SettingsPath))
            {
                _jsonMutex.ReleaseMutex();
                Save();
                return;
            }

            string jsonData = File.ReadAllText(SettingsPath);
            JObject powertoysSettings = JObject.Parse(jsonData);

            // Replace the { "value": <Value> } with <Value> to match the Settings object format
            foreach (var property in (JObject)powertoysSettings["properties"])
            {
                powertoysSettings["properties"][property.Key] = property.Value["value"];
            }

            Settings jsonSettings = JsonConvert.DeserializeObject<Settings>(powertoysSettings["properties"].ToString(), new JsonSerializerSettings() { ObjectCreationHandling = ObjectCreationHandling.Replace });
            App.Current.Dispatcher.Invoke(() =>
            {
                ShrinkOnly = jsonSettings.ShrinkOnly;
                Replace = jsonSettings.Replace;
                IgnoreOrientation = jsonSettings.IgnoreOrientation;
                JpegQualityLevel = jsonSettings.JpegQualityLevel;
                PngInterlaceOption = jsonSettings.PngInterlaceOption;
                TiffCompressOption = jsonSettings.TiffCompressOption;
                FileName = jsonSettings.FileName;
                Sizes = jsonSettings.Sizes;
                KeepDateModified = jsonSettings.KeepDateModified;
                FallbackEncoder = jsonSettings.FallbackEncoder;
                CustomSize = jsonSettings.CustomSize;
                SelectedSizeIndex = jsonSettings.SelectedSizeIndex;
            });
            _jsonMutex.ReleaseMutex();
        }
    }
}
