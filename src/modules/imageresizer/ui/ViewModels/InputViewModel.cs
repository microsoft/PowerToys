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
using System.Windows.Media.Imaging;
using Common.UI;
using ImageResizer.Helpers;
using ImageResizer.Models;
using ImageResizer.Properties;
using ImageResizer.Services;
using ImageResizer.Views;

namespace ImageResizer.ViewModels
{
    public class InputViewModel : Observable
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

            ResizeCommand = new RelayCommand(Resize, () => CanResize);
            CancelCommand = new RelayCommand(Cancel);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            EnterKeyPressedCommand = new RelayCommand<KeyPressParams>(HandleEnterKeyPress);
            DownloadModelCommand = new RelayCommand(async () => await DownloadModelAsync());

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
            private set => Set(ref _currentResolutionDescription, value);
        }

        public string NewResolutionDescription
        {
            get => _newResolutionDescription;
            private set => Set(ref _newResolutionDescription, value);
        }

        // ==================== UI State Properties ====================

        // Show AI size descriptions only when AI size is selected and not multiple files
        public bool ShowAiSizeDescriptions => Settings?.SelectedSize is AiSize && !_hasMultipleFiles;

        // Helper property: Is model currently being downloaded?
        public bool IsModelDownloading => _isDownloadingModel;

        public string ModelStatusMessage
        {
            get => _modelStatusMessage;
            private set => Set(ref _modelStatusMessage, value);
        }

        public double ModelDownloadProgress
        {
            get => _modelDownloadProgress;
            private set => Set(ref _modelDownloadProgress, value);
        }

        // Show download prompt when: AI size is selected and model is not ready (including downloading)
        public bool ShowModelDownloadPrompt =>
            Settings?.SelectedSize is AiSize &&
            (App.AiAvailabilityState == Properties.AiAvailabilityState.ModelNotReady || _isDownloadingModel);

        // Show AI controls when: AI size is selected and AI is ready
        public bool ShowAiControls =>
            Settings?.SelectedSize is AiSize &&
            App.AiAvailabilityState == Properties.AiAvailabilityState.Ready;

        /// <summary>
        /// Gets a value indicating whether the resize operation can proceed.
        /// For AI resize: only enabled when AI is fully ready.
        /// For non-AI resize: always enabled.
        /// </summary>
        public bool CanResize
        {
            get
            {
                // If AI size is selected, only allow resize when AI is fully ready
                if (Settings?.SelectedSize is AiSize)
                {
                    return App.AiAvailabilityState == Properties.AiAvailabilityState.Ready;
                }

                // Non-AI resize can always proceed
                return true;
            }
        }

        public ICommand ResizeCommand { get; }

        public ICommand CancelCommand { get; }

        public ICommand OpenSettingsCommand { get; }

        public ICommand EnterKeyPressedCommand { get; private set; }

        public ICommand DownloadModelCommand { get; private set; }

        // Any of the files is a gif
        public bool TryingToResizeGifFiles =>
                _batch?.Files.Any(filename => filename.EndsWith(".gif", System.StringComparison.InvariantCultureIgnoreCase)) == true;

        public void Resize()
        {
            Settings.Save();
            _mainViewModel.CurrentPage = new ProgressViewModel(_batch, _mainViewModel, _mainView);
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
                    // Notify UI state properties that depend on SelectedSize
                    NotifyAiStateChanged();
                    UpdateAiDetails();

                    // Trigger CanExecuteChanged for ResizeCommand
                    if (ResizeCommand is RelayCommand cmd)
                    {
                        cmd.OnCanExecuteChanged();
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
            // Clear AI details if AI size not selected
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
                : Resources.Input_AiUnknownSize;

            var scale = Settings.AiSize.Scale;
            NewResolutionDescription = hasConcreteSize
                ? FormatDimensions((long)_originalWidth!.Value * scale, (long)_originalHeight!.Value * scale)
                : Resources.Input_AiUnknownSize;
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
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
                var frame = decoder.Frames.FirstOrDefault();
                if (frame != null)
                {
                    _originalWidth = frame.PixelWidth;
                    _originalHeight = frame.PixelHeight;
                }
            }
            catch (Exception)
            {
                // Failed to load image dimensions - clear values
                _originalWidth = null;
                _originalHeight = null;
            }
            finally
            {
                _originalDimensionsLoaded = true;
            }
        }

        /// <summary>
        /// Initializes AI UI state based on App's cached availability state.
        /// Subscribe to state change event to update UI when background initialization completes.
        /// </summary>
        private void InitializeAiState()
        {
            // Subscribe to initialization completion event to refresh UI
            App.AiInitializationCompleted += OnAiInitializationCompleted;

            // Set initial status message based on current state
            UpdateStatusMessage();
        }

        /// <summary>
        /// Handles AI initialization completion event from App.
        /// Refreshes UI when background initialization finishes.
        /// </summary>
        private void OnAiInitializationCompleted(object sender, Properties.AiAvailabilityState finalState)
        {
            UpdateStatusMessage();
            NotifyAiStateChanged();
        }

        /// <summary>
        /// Updates status message based on current App availability state.
        /// </summary>
        private void UpdateStatusMessage()
        {
            ModelStatusMessage = App.AiAvailabilityState switch
            {
                Properties.AiAvailabilityState.Ready => string.Empty,
                Properties.AiAvailabilityState.ModelNotReady => Resources.Input_AiModelNotAvailable,
                Properties.AiAvailabilityState.NotSupported => Resources.Input_AiModelNotSupported,
                _ => string.Empty,
            };
        }

        /// <summary>
        /// Notifies UI when AI state changes (model availability, download status).
        /// </summary>
        private void NotifyAiStateChanged()
        {
            OnPropertyChanged(nameof(IsModelDownloading));
            OnPropertyChanged(nameof(ShowModelDownloadPrompt));
            OnPropertyChanged(nameof(ShowAiControls));
            OnPropertyChanged(nameof(ShowAiSizeDescriptions));
            OnPropertyChanged(nameof(CanResize));

            // Trigger CanExecuteChanged for ResizeCommand
            if (ResizeCommand is RelayCommand resizeCommand)
            {
                resizeCommand.OnCanExecuteChanged();
            }
        }

        /// <summary>
        /// Notifies UI when AI scale changes (slider value).
        /// </summary>
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
                // Set downloading flag and show progress
                _isDownloadingModel = true;
                ModelStatusMessage = Resources.Input_AiModelDownloading;
                ModelDownloadProgress = 0;
                NotifyAiStateChanged();

                // Create progress reporter to update UI
                var progress = new Progress<double>(value =>
                {
                    // progressValue could be 0-1 or 0-100, normalize to 0-100
                    ModelDownloadProgress = value > 1 ? value : value * 100;
                });

                // Call EnsureReadyAsync to download and prepare the AI model
                var result = await WinAiSuperResolutionService.EnsureModelReadyAsync(progress);

                if (result?.Status == Microsoft.Windows.AI.AIFeatureReadyResultState.Success)
                {
                    // Model successfully downloaded and ready
                    ModelDownloadProgress = 100;

                    // Update App's cached state
                    App.AiAvailabilityState = Properties.AiAvailabilityState.Ready;
                    UpdateStatusMessage();

                    // Initialize the AI service now that model is ready
                    var aiService = await WinAiSuperResolutionService.CreateAsync();
                    ResizeBatch.SetAiSuperResolutionService(aiService ?? (Services.IAISuperResolutionService)NoOpAiSuperResolutionService.Instance);
                }
                else
                {
                    // Download failed
                    ModelStatusMessage = Resources.Input_AiModelDownloadFailed;
                }
            }
            catch (Exception)
            {
                // Exception during download
                ModelStatusMessage = Resources.Input_AiModelDownloadFailed;
            }
            finally
            {
                // Clear downloading flag
                _isDownloadingModel = false;

                // Reset progress if not successful
                if (App.AiAvailabilityState != Properties.AiAvailabilityState.Ready)
                {
                    ModelDownloadProgress = 0;
                }

                NotifyAiStateChanged();
            }
        }
    }
}
