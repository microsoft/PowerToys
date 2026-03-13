// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Common.UI;
using ImageResizer.Helpers;
using ImageResizer.Models;
using ImageResizer.Properties;
using ImageResizer.Services;
using ImageResizer.Views;

namespace ImageResizer.ViewModels
{
    public partial class InputViewModel : ObservableObject
    {
        public const int DefaultAiScale = 2;
        private const int MinAiScale = 1;
        private const int MaxAiScale = 8;

        public enum Dimension
        {
            Width,
            Height,
        }

        public class KeyPressParams
        {
            public double Value { get; set; }

            public Dimension Dimension { get; set; }
        }

        private readonly ResizeBatch _batch;
        private readonly MainViewModel _mainViewModel;
        private readonly IMainView _mainView;
        private readonly bool _hasMultipleFiles;
        private bool _originalDimensionsLoaded;
        private int? _originalWidth;
        private int? _originalHeight;

        [ObservableProperty]
        private string _currentResolutionDescription;

        [ObservableProperty]
        private string _newResolutionDescription;

        [ObservableProperty]
        private bool _isDownloadingModel;

        [ObservableProperty]
        private string _modelStatusMessage;

        [ObservableProperty]
        private double _modelDownloadProgress;

        public InputViewModel(
            Settings settings,
            MainViewModel mainViewModel,
            IMainView mainView,
            ResizeBatch batch)
        {
            _batch = batch;
            _mainViewModel = mainViewModel;
            _mainView = mainView;
            _hasMultipleFiles = _batch?.Files.Count > 1;

            Settings = settings;
            if (settings != null)
            {
                settings.CustomSize.PropertyChanged += (sender, e) => settings.SelectedSize = (CustomSize)sender;
                settings.AiSize.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == nameof(AiSize.Scale))
                    {
                        NotifyAiScaleChanged();
                    }
                };
                settings.PropertyChanged += HandleSettingsPropertyChanged;
            }

            InitializeAiState();
        }

        public Settings Settings { get; }

        public IEnumerable<ResizeFit> ResizeFitValues => Enum.GetValues<ResizeFit>();

        public IEnumerable<ResizeUnit> ResizeUnitValues => Enum.GetValues<ResizeUnit>();

        public int AiSuperResolutionScale
        {
            get => Settings?.AiSize?.Scale ?? DefaultAiScale;
            set
            {
                if (Settings?.AiSize != null && Settings.AiSize.Scale != value)
                {
                    Settings.AiSize.Scale = value;
                    NotifyAiScaleChanged();
                }
            }
        }

        public string AiScaleDisplay => Settings?.AiSize?.ScaleDisplay ?? string.Empty;

        public bool IsCustomSizeSelected => Settings?.SelectedSize is CustomSize;

        public bool ShowAiSizeDescriptions => Settings?.SelectedSize is AiSize && !_hasMultipleFiles;

        public bool ShowModelDownloadPrompt =>
            Settings?.SelectedSize is AiSize &&
            (App.AiAvailabilityState == AiAvailabilityState.ModelNotReady || IsDownloadingModel);

        public bool ShowAiControls =>
            Settings?.SelectedSize is AiSize &&
            App.AiAvailabilityState == AiAvailabilityState.Ready;

        public bool CanResize
        {
            get
            {
                if (Settings?.SelectedSize is AiSize)
                {
                    return App.AiAvailabilityState == AiAvailabilityState.Ready;
                }

                return true;
            }
        }

        public bool TryingToResizeGifFiles =>
            _batch?.Files.Any(filename => filename.EndsWith(".gif", StringComparison.InvariantCultureIgnoreCase)) == true;

        [RelayCommand(CanExecute = nameof(CanResize))]
        public void Resize()
        {
            Settings.Save();
            _mainViewModel.CurrentPage = new ProgressViewModel(_batch, _mainViewModel, _mainView);
        }

        [RelayCommand]
        private void EnterKeyPressed(KeyPressParams parameters)
        {
            switch (parameters.Dimension)
            {
                case Dimension.Width:
                    Settings.CustomSize.Width = parameters.Value;
                    break;
                case Dimension.Height:
                    Settings.CustomSize.Height = parameters.Value;
                    break;
            }
        }

        [RelayCommand]
        public void Cancel()
            => _mainView.Close();

        [RelayCommand]
        public static void OpenSettings()
        {
            SettingsDeepLink.OpenSettings(SettingsDeepLink.SettingsWindow.ImageResizer);
        }

        [RelayCommand]
        public async Task DownloadModelAsync()
        {
            try
            {
                IsDownloadingModel = true;
                ModelStatusMessage = ResourceLoaderInstance.ResourceLoader.GetString("Input_AiModelDownloading");
                ModelDownloadProgress = 0;
                NotifyAiStateChanged();

                var progress = new Progress<double>(value =>
                {
                    ModelDownloadProgress = value > 1 ? value : value * 100;
                });

                var result = await WinAiSuperResolutionService.EnsureModelReadyAsync(progress);

                if (result?.Status == Microsoft.Windows.AI.AIFeatureReadyResultState.Success)
                {
                    ModelDownloadProgress = 100;
                    App.AiAvailabilityState = AiAvailabilityState.Ready;
                    UpdateStatusMessage();

                    var aiService = await WinAiSuperResolutionService.CreateAsync();
                    if (aiService != null)
                    {
                        ResizeBatch.SetAiSuperResolutionService(aiService);
                    }
                    else
                    {
                        ResizeBatch.SetAiSuperResolutionService(NoOpAiSuperResolutionService.Instance);
                    }
                }
                else
                {
                    ModelStatusMessage = ResourceLoaderInstance.ResourceLoader.GetString("Input_AiModelDownloadFailed");
                }
            }
            catch (Exception)
            {
                ModelStatusMessage = ResourceLoaderInstance.ResourceLoader.GetString("Input_AiModelDownloadFailed");
            }
            finally
            {
                IsDownloadingModel = false;
                if (App.AiAvailabilityState != AiAvailabilityState.Ready)
                {
                    ModelDownloadProgress = 0;
                }

                NotifyAiStateChanged();
            }
        }

        private void HandleSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Settings.SelectedSizeIndex):
                case nameof(Settings.SelectedSize):
                    NotifyAiStateChanged();
                    UpdateAiDetails();
                    ResizeCommand.NotifyCanExecuteChanged();
                    break;
            }
        }

        private void EnsureAiScaleWithinRange()
        {
            if (Settings?.AiSize != null)
            {
                Settings.AiSize.Scale = Math.Clamp(Settings.AiSize.Scale, MinAiScale, MaxAiScale);
            }
        }

        private async void UpdateAiDetails()
        {
            if (Settings == null || Settings.SelectedSize is not AiSize)
            {
                CurrentResolutionDescription = string.Empty;
                NewResolutionDescription = string.Empty;
                return;
            }

            EnsureAiScaleWithinRange();

            if (_hasMultipleFiles)
            {
                CurrentResolutionDescription = string.Empty;
                NewResolutionDescription = string.Empty;
                return;
            }

            await EnsureOriginalDimensionsLoadedAsync();

            var hasConcreteSize = _originalWidth.HasValue && _originalHeight.HasValue;
            CurrentResolutionDescription = hasConcreteSize
                ? FormatDimensions(_originalWidth!.Value, _originalHeight!.Value)
                : ResourceLoaderInstance.ResourceLoader.GetString("Input_AiUnknownSize");

            var scale = Settings.AiSize.Scale;
            NewResolutionDescription = hasConcreteSize
                ? FormatDimensions((long)_originalWidth!.Value * scale, (long)_originalHeight!.Value * scale)
                : ResourceLoaderInstance.ResourceLoader.GetString("Input_AiUnknownSize");
        }

        private static string FormatDimensions(long width, long height)
        {
            return string.Format(CultureInfo.CurrentCulture, "{0} x {1}", width, height);
        }

        private async Task EnsureOriginalDimensionsLoadedAsync()
        {
            if (_originalDimensionsLoaded)
            {
                return;
            }

            var file = _batch?.Files.FirstOrDefault();
            if (string.IsNullOrEmpty(file))
            {
                _originalDimensionsLoaded = true;
                return;
            }

            try
            {
                using var fileStream = File.OpenRead(file);
                using var stream = fileStream.AsRandomAccessStream();
                var decoder = await BitmapDecoder.CreateAsync(stream);
                _originalWidth = (int)decoder.PixelWidth;
                _originalHeight = (int)decoder.PixelHeight;
            }
            catch (Exception)
            {
                _originalWidth = null;
                _originalHeight = null;
            }
            finally
            {
                _originalDimensionsLoaded = true;
            }
        }

        private void InitializeAiState()
        {
            App.AiInitializationCompleted += OnAiInitializationCompleted;
            UpdateStatusMessage();
        }

        private void OnAiInitializationCompleted(object sender, AiAvailabilityState finalState)
        {
            UpdateStatusMessage();
            NotifyAiStateChanged();
        }

        private void UpdateStatusMessage()
        {
            ModelStatusMessage = App.AiAvailabilityState switch
            {
                AiAvailabilityState.Ready => string.Empty,
                AiAvailabilityState.ModelNotReady => ResourceLoaderInstance.ResourceLoader.GetString("Input_AiModelNotAvailable"),
                AiAvailabilityState.NotSupported => ResourceLoaderInstance.ResourceLoader.GetString("Input_AiModelNotSupported"),
                _ => string.Empty,
            };
        }

        private void NotifyAiStateChanged()
        {
            OnPropertyChanged(nameof(IsCustomSizeSelected));
            OnPropertyChanged(nameof(IsDownloadingModel));
            OnPropertyChanged(nameof(ShowModelDownloadPrompt));
            OnPropertyChanged(nameof(ShowAiControls));
            OnPropertyChanged(nameof(ShowAiSizeDescriptions));
            OnPropertyChanged(nameof(CanResize));
        }

        private void NotifyAiScaleChanged()
        {
            OnPropertyChanged(nameof(AiSuperResolutionScale));
            OnPropertyChanged(nameof(AiScaleDisplay));
            UpdateAiDetails();
        }
    }
}
