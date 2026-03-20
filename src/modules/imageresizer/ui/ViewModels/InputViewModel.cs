#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Common.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageResizer.Models;
using ImageResizer.Properties;
using ImageResizer.Services;
using ImageResizer.Views;
using Windows.Graphics.Imaging;

namespace ImageResizer.ViewModels
{
    public class InputViewModel : ObservableObject
    {
        public const int DefaultAiScale = 2;
        private const int MinAiScale = 1;
        private const int MaxAiScale = 8;

        private readonly ResizeBatch _batch;
        private readonly MainViewModel _mainViewModel;
        private readonly IMainView _mainView;
        private readonly bool _hasMultipleFiles;
        private bool _originalDimensionsLoaded;
        private int? _originalWidth;
        private int? _originalHeight;
        private string _currentResolutionDescription;
        private string _newResolutionDescription;
        private bool _isDownloadingModel;
        private string _modelStatusMessage;
        private double _modelDownloadProgress;

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

            ResizeCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(Resize, () => CanResize);
            CancelCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(Cancel);
            OpenSettingsCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(OpenSettings);
            EnterKeyPressedCommand = new RelayCommand<KeyPressParams>(HandleEnterKeyPress);
            DownloadModelCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(async () => await DownloadModelAsync());

            // Initialize AI UI state based on Settings availability
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

        public string CurrentResolutionDescription
        {
            get => _currentResolutionDescription;
            private set => SetProperty(ref _currentResolutionDescription, value);
        }

        public string NewResolutionDescription
        {
            get => _newResolutionDescription;
            private set => SetProperty(ref _newResolutionDescription, value);
        }

        public bool ShowAiSizeDescriptions => Settings?.SelectedSize is AiSize && !_hasMultipleFiles;

        public bool IsModelDownloading => _isDownloadingModel;

        public string ModelStatusMessage
        {
            get => _modelStatusMessage;
            private set => SetProperty(ref _modelStatusMessage, value);
        }

        public double ModelDownloadProgress
        {
            get => _modelDownloadProgress;
            private set => SetProperty(ref _modelDownloadProgress, value);
        }

        public bool ShowModelDownloadPrompt =>
            Settings?.SelectedSize is AiSize &&
            (App.AiAvailabilityState == AiAvailabilityState.ModelNotReady || _isDownloadingModel);

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

        public ICommand ResizeCommand { get; }

        public ICommand CancelCommand { get; }

        public ICommand OpenSettingsCommand { get; }

        public ICommand EnterKeyPressedCommand { get; private set; }

        public ICommand DownloadModelCommand { get; private set; }

        public bool TryingToResizeGifFiles =>
                _batch?.Files.Any(filename => filename.EndsWith(".gif", System.StringComparison.InvariantCultureIgnoreCase)) == true;

        public void Resize()
        {
            Settings.Save();
            var progressViewModel = new ProgressViewModel(_batch, _mainViewModel, _mainView);
            _mainViewModel.CurrentPage = new Views.ProgressPage { DataContext = progressViewModel };
        }

        public static void OpenSettings()
        {
            SettingsDeepLink.OpenSettings(SettingsDeepLink.SettingsWindow.ImageResizer);
        }

        private void HandleEnterKeyPress(KeyPressParams parameters)
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

        public void Cancel()
            => _mainView.Close();

        private void HandleSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Settings.SelectedSizeIndex):
                case nameof(Settings.SelectedSize):
                    NotifyAiStateChanged();
                    UpdateAiDetails();

                    if (ResizeCommand is CommunityToolkit.Mvvm.Input.RelayCommand cmd)
                    {
                        cmd.NotifyCanExecuteChanged();
                    }

                    break;
            }
        }

        private void EnsureAiScaleWithinRange()
        {
            if (Settings?.AiSize != null)
            {
                Settings.AiSize.Scale = Math.Clamp(
                    Settings.AiSize.Scale,
                    MinAiScale,
                    MaxAiScale);
            }
        }

        private void UpdateAiDetails()
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

            EnsureOriginalDimensionsLoaded();

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
            return string.Format(CultureInfo.CurrentCulture, "{0} × {1}", width, height);
        }

        private void EnsureOriginalDimensionsLoaded()
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
                using var stream = File.OpenRead(file);
                var winrtStream = stream.AsRandomAccessStream();
                var decoder = BitmapDecoder.CreateAsync(winrtStream).AsTask().GetAwaiter().GetResult();
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
            OnPropertyChanged(nameof(IsModelDownloading));
            OnPropertyChanged(nameof(ShowModelDownloadPrompt));
            OnPropertyChanged(nameof(ShowAiControls));
            OnPropertyChanged(nameof(ShowAiSizeDescriptions));
            OnPropertyChanged(nameof(CanResize));

            if (ResizeCommand is CommunityToolkit.Mvvm.Input.RelayCommand resizeCommand)
            {
                resizeCommand.NotifyCanExecuteChanged();
            }
        }

        private void NotifyAiScaleChanged()
        {
            OnPropertyChanged(nameof(AiSuperResolutionScale));
            OnPropertyChanged(nameof(AiScaleDisplay));
            UpdateAiDetails();
        }

        private async Task DownloadModelAsync()
        {
            try
            {
                _isDownloadingModel = true;
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
                    ResizeBatch.SetAiSuperResolutionService(aiService ?? (Services.IAISuperResolutionService)NoOpAiSuperResolutionService.Instance);
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
                _isDownloadingModel = false;

                if (App.AiAvailabilityState != AiAvailabilityState.Ready)
                {
                    ModelDownloadProgress = 0;
                }

                NotifyAiStateChanged();
            }
        }
    }
}
