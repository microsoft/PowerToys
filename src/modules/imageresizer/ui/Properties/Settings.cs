#pragma warning disable IDE0073, SA1636
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073, SA1636

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using ImageResizer.Helpers;
using ImageResizer.Models;
using ManagedCommon;
using Microsoft.UI.Dispatching;

namespace ImageResizer.Properties
{
    /// <summary>
    /// Represents the availability state of AI Super Resolution feature.
    /// </summary>
    public enum AiAvailabilityState
    {
        NotSupported,      // System doesn't support AI (architecture issue or policy disabled)
        ModelNotReady,     // AI supported but model not downloaded
        Ready,             // AI fully ready to use
    }

    public sealed partial class Settings : IDataErrorInfo, INotifyPropertyChanged
    {
        private const int WatcherDebounceDelayMs = 500;
        private static readonly IFileSystem _fileSystem = new FileSystem();
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
        {
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };

        // Used to synchronize access to the settings.json file (in-process only)
        private static readonly System.Threading.Lock _jsonSyncLock = new();

        // Lock for debouncing watcher events.
        private static readonly Lock _debounceLock = new();

        // Cached UI thread DispatcherQueue for cross-thread property change notifications
        private static DispatcherQueue _uiDispatcherQueue;

        private static CompositeFormat _valueMustBeBetween;

        private static CompositeFormat ValueMustBeBetween =>
            _valueMustBeBetween ??= System.Text.CompositeFormat.Parse(ResourceLoaderInstance.GetString("ValueMustBeBetween"));

        private static string _settingsPath = _fileSystem.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys", "Image Resizer", "settings.json");

        // Watches for external changes to settings file.
        private static IFileSystemWatcher _watcher;
        private static bool _isWatcherInitialized;

        // Executed when the watcher detects a file update.
        private static Action _reloadAction;

        // Debounce token for watcher events.
        private static CancellationTokenSource _debounceCts;

        private bool _isFirstLoad = true;

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
        private AiSize _aiSize;

        public Settings()
        {
            SelectedSizeIndex = 0;
            ShrinkOnly = false;
            Replace = false;
            IgnoreOrientation = true;
            RemoveMetadata = false;
            JpegQualityLevel = 90;
            PngInterlaceOption = Models.PngInterlaceOption.Default;
            TiffCompressOption = Models.TiffCompressOption.Default;
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
            AiSize = new AiSize(2);
            AllSizes = new AllSizesCollection(this);

            InitializeWatcher(Reload);
        }

        public static string SettingsPath { get => _settingsPath; set => _settingsPath = value; }

        /// <summary>
        /// Initializes the UI DispatcherQueue for cross-thread property change notifications.
        /// Must be called from the UI thread during app startup.
        /// </summary>
        public static void InitializeDispatcher()
        {
            _uiDispatcherQueue = DispatcherQueue.GetForCurrentThread();
        }

        private static void InitializeWatcher(Action reloadAction)
        {
            if (_isWatcherInitialized)
            {
                return;
            }

            _reloadAction = reloadAction;

            try
            {
                string settingsDirectory = _fileSystem.Path.GetDirectoryName(SettingsPath);
                string settingsFileName = _fileSystem.Path.GetFileName(SettingsPath);

                if (_fileSystem.Directory.Exists(settingsDirectory))
                {
                    _watcher = _fileSystem.FileSystemWatcher.New();
                    _watcher.Path = settingsDirectory;
                    _watcher.Filter = settingsFileName;
                    _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime;

                    _watcher.Changed += OnSettingsFileChanged;
                    _watcher.Created += OnSettingsFileChanged;

                    _watcher.EnableRaisingEvents = true;

                    _isWatcherInitialized = true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    $"Failed to initialize settings file watcher.", ex);
            }
        }

        private static async void OnSettingsFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                await HandleSettingsFileChangedAsync();
            }
            catch (Exception ex)
            {
                // All expected exceptions are handled in the async method, e.g.
                // TaskCanceledException.
                Logger.LogError(
                    $"Error reloading settings from watcher.", ex);
            }
        }

        private static async Task HandleSettingsFileChangedAsync()
        {
            CancellationToken token;

            // Cancel any pending reload request.
            lock (_debounceLock)
            {
                if (_debounceCts != null)
                {
                    _debounceCts.Cancel();
                    _debounceCts.Dispose();
                }

                // Create a new debounce token.
                _debounceCts = new();
                token = _debounceCts.Token;
            }

            try
            {
                await Task.Delay(WatcherDebounceDelayMs, token);

                if (!token.IsCancellationRequested)
                {
                    _reloadAction?.Invoke();
                }
            }
            catch (TaskCanceledException)
            {
                // Ignore cancellation. A new event superseded this one.
            }
        }

        private void ValidateSelectedSizeIndex()
        {
            var maxIndex = ImageResizer.App.AiAvailabilityState == AiAvailabilityState.NotSupported
                ? Sizes.Count
                : Sizes.Count + 1;

            if (_selectedSizeIndex > maxIndex)
            {
                _selectedSizeIndex = 0;
            }
        }

        [JsonIgnore]
        public IEnumerable<ResizeSize> AllSizes { get; set; }

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
            get
            {
                if (SelectedSizeIndex >= 0 && SelectedSizeIndex < Sizes.Count)
                {
                    return Sizes[SelectedSizeIndex];
                }
                else if (SelectedSizeIndex == Sizes.Count)
                {
                    return CustomSize;
                }
                else if (App.AiAvailabilityState != AiAvailabilityState.NotSupported &&
                    SelectedSizeIndex == Sizes.Count + 1)
                {
                    return AiSize;
                }
                else
                {
                    return CustomSize;
                }
            }

            set
            {
                var index = Sizes.IndexOf(value);
                if (index == -1)
                {
                    if (value is AiSize)
                    {
                        index = Sizes.Count + 1;
                    }
                    else
                    {
                        index = Sizes.Count;
                    }
                }

                SelectedSizeIndex = index;
            }
        }

        string IDataErrorInfo.Error => string.Empty;

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
                    return string.Format(CultureInfo.CurrentCulture, ValueMustBeBetween, 1, 100);
                }

                return string.Empty;
            }
        }

        private class AllSizesCollection : IEnumerable<ResizeSize>, INotifyCollectionChanged, INotifyPropertyChanged
        {
            private readonly Settings _settings;
            private ObservableCollection<ResizeSize> _sizes;
            private CustomSize _customSize;
            private AiSize _aiSize;

            public AllSizesCollection(Settings settings)
            {
                _settings = settings;
                _sizes = settings.Sizes;
                _customSize = settings.CustomSize;
                _aiSize = settings.AiSize;

                _sizes.CollectionChanged += HandleCollectionChanged;
                ((INotifyPropertyChanged)_sizes).PropertyChanged += HandlePropertyChanged;

                settings.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == nameof(Models.CustomSize))
                    {
                        _customSize = settings.CustomSize;
                        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                    }
                    else if (e.PropertyName == nameof(Models.AiSize))
                    {
                        _aiSize = settings.AiSize;
                        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
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
                => _sizes.Count + 1 + (ImageResizer.App.AiAvailabilityState != AiAvailabilityState.NotSupported ? 1 : 0);

            public ResizeSize this[int index]
            {
                get
                {
                    if (index < _sizes.Count)
                    {
                        return _sizes[index];
                    }
                    else if (index == _sizes.Count)
                    {
                        return _customSize;
                    }
                    else if (ImageResizer.App.AiAvailabilityState != AiAvailabilityState.NotSupported && index == _sizes.Count + 1)
                    {
                        return _aiSize;
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException(nameof(index), index, $"Index {index} is out of range for AllSizesCollection.");
                    }
                }
            }

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

        private static Settings defaultInstance;

        [JsonIgnore]
        public static Settings Default
        {
            get
            {
                if (defaultInstance == null)
                {
                    defaultInstance = new Settings();
                    defaultInstance.Reload();
                }

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
                if (_selectedSizeIndex == value)
                {
                    return;
                }

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

        [JsonConverter(typeof(WrappedJsonValueConverter))]
        [JsonPropertyName("imageresizer_aiSize")]
        public AiSize AiSize
        {
            get => _aiSize;
            set
            {
                _aiSize = value;
                NotifyPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Save()
        {
            lock (_jsonSyncLock)
            {
                SaveCore();
            }
        }

        /// <summary>
        /// Writes current settings to disk. Must be called under <see cref="_jsonSyncLock"/>.
        /// </summary>
        private void SaveCore()
        {
            string jsonData = JsonSerializer.Serialize(new SettingsWrapper() { Properties = this }, _jsonSerializerOptions);

            IFileInfo file = _fileSystem.FileInfo.New(SettingsPath);
            file.Directory.Create();

            _fileSystem.File.WriteAllText(SettingsPath, jsonData);
        }

        public void Reload()
        {
            string oldSettingsDir = _fileSystem.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys", "ImageResizer");
            string settingsDir = _fileSystem.Path.GetDirectoryName(SettingsPath);

            if (_fileSystem.Directory.Exists(oldSettingsDir) && !_fileSystem.Directory.Exists(settingsDir))
            {
                _fileSystem.Directory.Move(oldSettingsDir, settingsDir);
            }

            // Read and deserialize under lock; ReloadCore runs outside the lock
            // because jsonSettings is an in-memory snapshot with no file I/O.
            Settings jsonSettings;
            lock (_jsonSyncLock)
            {
                if (!_fileSystem.File.Exists(SettingsPath))
                {
                    SaveCore();
                    return;
                }

                string jsonData = _fileSystem.File.ReadAllText(SettingsPath);
                jsonSettings = new Settings();
                try
                {
                    jsonSettings = JsonSerializer.Deserialize<SettingsWrapper>(jsonData, _jsonSerializerOptions)?.Properties;
                }
                catch (JsonException ex)
                {
                    Logger.LogError($"Failed to parse settings JSON, using defaults: {ex.Message}");
                }
            }

            // Apply deserialized snapshot to live properties on the UI thread.
            if (_uiDispatcherQueue != null)
            {
                if (_uiDispatcherQueue.HasThreadAccess)
                {
                    ReloadCore(jsonSettings);
                }
                else
                {
                    _uiDispatcherQueue.TryEnqueue(() => ReloadCore(jsonSettings));
                }
            }
            else
            {
                // No UI context (unit tests or CLI mode) — call directly.
                ReloadCore(jsonSettings);
            }
        }

        private void ReloadCore(Settings jsonSettings)
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

            // Recover invalid IDs before any ID-based matching step. This handles corrupted or
            // user-edited settings files.
            if (jsonSettings.Sizes.Count > 0)
            {
                IdRecoveryHelper.RecoverInvalidIds(jsonSettings.Sizes);
            }

            // Remember previous selection to try to preserve it.
            bool wasCustomSize = SelectedSize is CustomSize;
            bool wasAiSize = SelectedSize is AiSize;
            int? selectedId = SelectedSize is IHasId userSize ? userSize.Id : null;

            // Replace Custom/AI instances from disk, and ensure the presets are created if absent.
            CustomSize = jsonSettings.CustomSize ?? new CustomSize(ResizeFit.Fit, 1024, 640, ResizeUnit.Pixel);
            AiSize = jsonSettings.AiSize ?? new AiSize(2);

            // Default to the index from the settings file on first load.
            int targetIndex = jsonSettings.SelectedSizeIndex;

            if (_isFirstLoad)
            {
                _isFirstLoad = false;
            }
            else
            {
                // The settings were updated externally. Try to preserve the selected size.
                if (wasCustomSize)
                {
                    targetIndex = jsonSettings.Sizes.Count;
                }
                else if (wasAiSize)
                {
                    targetIndex = jsonSettings.Sizes.Count + 1;
                }
                else if (selectedId is not null)
                {
                    // Match by Id for user-defined size presets, defaulting to CustomSize if not
                    // found.
                    targetIndex = jsonSettings.Sizes.Count;

                    for (int i = 0; i < jsonSettings.Sizes.Count; i++)
                    {
                        if (jsonSettings.Sizes[i].Id == selectedId)
                        {
                            targetIndex = i;
                            break;
                        }
                    }
                }
            }

            // Validate index. Ensures that if AI is not supported, we don't select an out-of-
            // range index. Also handles the file value being corrupt or out of range.
            int maxIndex = App.AiAvailabilityState == AiAvailabilityState.NotSupported
                ? jsonSettings.Sizes.Count // CustomSize only
                : jsonSettings.Sizes.Count + 1; // CustomSize + AiSize

            if (targetIndex > maxIndex || targetIndex < 0)
            {
                targetIndex = jsonSettings.Sizes.Count; // Fall back to CustomSize
            }

            Sizes.Clear();
            if (jsonSettings.Sizes.Count > 0)
            {
                Sizes.AddRange(jsonSettings.Sizes);
            }

            _selectedSizeIndex = targetIndex;
            NotifyPropertyChanged(nameof(SelectedSizeIndex));
            NotifyPropertyChanged(nameof(SelectedSize));
        }
    }
}
