// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.ViewModels;

public partial class ImageResizerViewModel : Observable
{
    private static readonly string DefaultPresetNamePrefix =
        Helpers.ResourceLoaderInstance.ResourceLoader.GetString("ImageResizer_DefaultSize_NewSizePrefix");

    private static readonly List<string> EncoderGuids =
    [
        "1b7cfaf4-713f-473c-bbcd-6137425faeaf",  // PNG Encoder
        "0af1d87e-fcfe-4188-bdeb-a7906471cbe3",  // Bitmap Encoder
        "19e4a5aa-5662-4fc5-a0c0-1758028e1057",  // JPEG Encoder
        "163bcc30-e2e9-4f0b-961d-a3e9fdb788a3",  // TIFF Encoder
        "57a37caa-367a-4540-916b-f183c5093a4b",  // TIFF Encoder
        "1f8a5601-7d4d-4cbd-9c82-1bc8d4eeb9a5",  // GIF Encoder
    ];

    /// <summary>
    /// Used to skip saving settings to file during initialization.
    /// </summary>
    private readonly bool _isInitializing;

    /// <summary>
    /// Holds defaults for new presets.
    /// </summary>
    private readonly ImageSize _customSize;

    private GeneralSettings GeneralSettingsConfig { get; set; }

    private readonly SettingsUtils _settingsUtils;

    private ImageResizerSettings Settings { get; set; }

    private const string ModuleName = ImageResizerSettings.ModuleName;

    private Func<string, int> SendConfigMSG { get; }

    public ImageResizerViewModel(SettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc, Func<string, string> resourceLoader)
    {
        _isInitializing = true;

        _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));

        // To obtain the general settings configurations of PowerToys.
        ArgumentNullException.ThrowIfNull(settingsRepository);

        GeneralSettingsConfig = settingsRepository.SettingsConfig;

        try
        {
            Settings = _settingsUtils.GetSettings<ImageResizerSettings>(ModuleName);
            IdRecoveryHelper.RecoverInvalidIds(Settings.Properties.ImageresizerSizes.Value);
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

        // set the callback functions value to handle outgoing IPC message.
        SendConfigMSG = ipcMSGCallBackFunc;

        InitializeEnabledValue();

        Sizes = new ObservableCollection<ImageSize>(Settings.Properties.ImageresizerSizes.Value);
        JPEGQualityLevel = Settings.Properties.ImageresizerJpegQualityLevel.Value;
        PngInterlaceOption = Settings.Properties.ImageresizerPngInterlaceOption.Value;
        TiffCompressOption = Settings.Properties.ImageresizerTiffCompressOption.Value;
        FileName = Settings.Properties.ImageresizerFileName.Value;
        KeepDateModified = Settings.Properties.ImageresizerKeepDateModified.Value;
        Encoder = GetEncoderIndex(Settings.Properties.ImageresizerFallbackEncoder.Value);

        _customSize = Settings.Properties.ImageresizerCustomSize.Value;

        _isInitializing = false;
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
    private ObservableCollection<ImageSize> _sizes = [];
    private int _jpegQualityLevel;
    private int _pngInterlaceOption;
    private int _tiffCompressOption;
    private string _fileName;
    private bool _keepDateModified;
    private int _encoderGuidId;

    public bool IsListViewFocusRequested { get; set; }

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
        get => _sizes;

        set
        {
            if (_sizes != null)
            {
                _sizes.CollectionChanged -= Sizes_CollectionChanged;
                UnsubscribeFromItemPropertyChanged(_sizes);
            }

            _sizes = value;

            if (_sizes != null)
            {
                _sizes.CollectionChanged += Sizes_CollectionChanged;
                SubscribeToItemPropertyChanged(_sizes);
            }

            OnPropertyChanged(nameof(Sizes));

            if (!_isInitializing)
            {
                SaveImageSizes();
            }
        }
    }

    private void Sizes_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        SubscribeToItemPropertyChanged(e.NewItems?.Cast<ImageSize>());
        UnsubscribeFromItemPropertyChanged(e.OldItems?.Cast<ImageSize>());
        SaveImageSizes();
    }

    private void SubscribeToItemPropertyChanged(IEnumerable<ImageSize> items)
    {
        if (items != null)
        {
            foreach (var item in items)
            {
                item.PropertyChanged += SizePropertyChanged;
            }
        }
    }

    private void UnsubscribeFromItemPropertyChanged(IEnumerable<ImageSize> items)
    {
        if (items != null)
        {
            foreach (var item in items)
            {
                item.PropertyChanged -= SizePropertyChanged;
            }
        }
    }

    private void SetProperty<T>(ref T backingField, T value, Action<T> updateSettingsAction, [CallerMemberName] string propertyName = null)
    {
        if (!EqualityComparer<T>.Default.Equals(backingField, value))
        {
            backingField = value;

            if (!_isInitializing)
            {
                updateSettingsAction(value);
                _settingsUtils.SaveSettings(Settings.ToJsonString(), ModuleName);
            }

            OnPropertyChanged(propertyName);
        }
    }

    public int JPEGQualityLevel
    {
        get => _jpegQualityLevel;

        set
        {
            SetProperty(ref _jpegQualityLevel, value, v => Settings.Properties.ImageresizerJpegQualityLevel.Value = v);
        }
    }

    public int PngInterlaceOption
    {
        get => _pngInterlaceOption;

        set
        {
            SetProperty(ref _pngInterlaceOption, value, v => Settings.Properties.ImageresizerPngInterlaceOption.Value = v);
        }
    }

    public int TiffCompressOption
    {
        get => _tiffCompressOption;

        set
        {
            SetProperty(ref _tiffCompressOption, value, v => Settings.Properties.ImageresizerTiffCompressOption.Value = v);
        }
    }

    public string FileName
    {
        get => _fileName;

        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                SetProperty(ref _fileName, value, v => Settings.Properties.ImageresizerFileName.Value = v);
            }
        }
    }

    public bool KeepDateModified
    {
        get => _keepDateModified;

        set
        {
            SetProperty(ref _keepDateModified, value, v => Settings.Properties.ImageresizerKeepDateModified.Value = v);
        }
    }

    public int Encoder
    {
        get => _encoderGuidId;

        set
        {
            SetProperty(ref _encoderGuidId, value, v => Settings.Properties.ImageresizerFallbackEncoder.Value = GetEncoderGuid(v));
        }
    }

    public string EncoderGuid => GetEncoderGuid(_encoderGuidId);

    public static string GetEncoderGuid(int index) =>
        index < 0 || index >= EncoderGuids.Count ? throw new ArgumentOutOfRangeException(nameof(index)) : EncoderGuids[index];

    public static int GetEncoderIndex(string encoderGuid)
    {
        int index = EncoderGuids.IndexOf(encoderGuid);
        return index == -1 ? throw new ArgumentException("Encoder GUID not found.", nameof(encoderGuid)) : index;
    }

    public void AddImageSize(string namePrefix = "")
    {
        if (string.IsNullOrEmpty(namePrefix))
        {
            namePrefix = DefaultPresetNamePrefix;
        }

        int maxId = Sizes.Count > 0 ? Sizes.Max(x => x.Id) : -1;
        string sizeName = GenerateNameForNewSize(namePrefix);

        Sizes.Add(new ImageSize(maxId + 1, GenerateNameForNewSize(namePrefix), _customSize.Fit, _customSize.Width, _customSize.Height, _customSize.Unit));

        // Set the focus requested flag to indicate that an add operation has occurred during the ContainerContentChanging event
        IsListViewFocusRequested = true;
    }

    public void DeleteImageSize(int id)
    {
        ImageSize size = _sizes.First(x => x.Id == id);
        Sizes.Remove(size);
    }

    public void SaveImageSizes()
    {
        Settings.Properties.ImageresizerSizes = new ImageResizerSizes(Sizes);
        _settingsUtils.SaveSettings(Settings.Properties.ImageresizerSizes.ToJsonString(), ModuleName, "sizes.json");
        _settingsUtils.SaveSettings(Settings.ToJsonString(), ModuleName);
    }

    public void SizePropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        SaveImageSizes();
    }

    private string GenerateNameForNewSize(string namePrefix)
    {
        int newSizeCounter = 0;

        foreach (var name in Sizes.Select(x => x.Name))
        {
            if (name.StartsWith(namePrefix, StringComparison.InvariantCulture) &&
                int.TryParse(name.AsSpan(namePrefix.Length), out int number) &&
                newSizeCounter < number)
            {
                newSizeCounter = number;
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
