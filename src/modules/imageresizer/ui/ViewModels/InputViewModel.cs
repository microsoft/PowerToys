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
        private const int DefaultAiScale = 2;
        private const int MinAiScale = 1;
        private const int MaxAiScale = 8;

        private static WinAiSuperResolutionService _aiSuperResolutionService;

        private readonly ResizeBatch _batch;
        private readonly MainViewModel _mainViewModel;
        private readonly IMainView _mainView;
        private readonly bool _hasMultipleFiles;
        private bool _originalDimensionsLoaded;
        private int? _originalWidth;
        private int? _originalHeight;
        private string _currentResolutionDescription;
        private string _newResolutionDescription;
        private AiFeatureState _aiFeatureState = AiFeatureState.Unknown;
        private string _modelStatusMessage;
        private double _modelDownloadProgress;

        public enum AiFeatureState
        {
            Unknown,           // Initial state, not yet checked
            NotSupported,      // System doesn't support AI (non-ARM64 or policy disabled)
            ModelNotReady,     // AI supported but model not downloaded
            ModelDownloading,  // Model is being downloaded
            Ready,             // AI fully ready to use
        }

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
                settings.PropertyChanged += HandleSettingsPropertyChanged;
            }

            ResizeCommand = new RelayCommand(Resize, () => CanResize);
            CancelCommand = new RelayCommand(Cancel);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            EnterKeyPressedCommand = new RelayCommand<KeyPressParams>(HandleEnterKeyPress);
            DownloadModelCommand = new RelayCommand(async () => await DownloadModelAsync());

            // Initialize AI support state - this checks architecture support and model availability
            _ = InitializeAiSupportAsync();
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
                    OnPropertyChanged(nameof(AiSuperResolutionScale));
                }
            }
        }

        public string AiScaleDisplay => AiSuperResolutionFormatter.FormatScaleName(AiSuperResolutionScale);

        public string AiScaleDescription => FormatLabeledSize(Resources.Input_AiScaleLabel, AiScaleDisplay);

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
        public bool IsModelDownloading => _aiFeatureState == AiFeatureState.ModelDownloading;

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
            (_aiFeatureState == AiFeatureState.ModelNotReady || _aiFeatureState == AiFeatureState.ModelDownloading);

        // Show AI controls when: AI size is selected and model is ready
        public bool ShowAiControls =>
            Settings?.SelectedSize is AiSize &&
            _aiFeatureState == AiFeatureState.Ready;

        /// <summary>
        /// Gets a value indicating whether the resize operation can proceed.
        /// Returns true in these cases:
        /// 1. AI is not supported on the system (NotSupported) - proceed with normal resize.
        /// 2. AI is available but user hasn't enabled it - proceed with normal resize.
        /// 3. AI is enabled by user AND model is ready - proceed with AI resize.
        /// Returns false when:
        /// - AI state is Unknown AND user enabled it - wait for state check to complete.
        /// - AI is enabled by user BUT model is not ready (ModelNotReady or ModelDownloading).
        /// </summary>
        public bool CanResize
        {
            get
            {
                // If AI size is selected, check if AI is fully ready
                if (Settings?.SelectedSize is AiSize)
                {
                    return _aiFeatureState == AiFeatureState.Ready;
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
            SettingsDeepLink.OpenSettings(SettingsDeepLink.SettingsWindow.ImageResizer, false);
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
                case nameof(Settings.UseAiSuperResolution):
                    if (Settings.UseAiSuperResolution)
                    {
                        // Reset to default scale when enabling AI
                        if (Settings.AiSize.Scale != DefaultAiScale)
                        {
                            Settings.AiSize.Scale = DefaultAiScale;
                        }

                        // User enabled AI - check if it's supported and available
                        _aiFeatureState = AiFeatureState.Unknown;
                        ModelStatusMessage = Resources.Input_AiModelChecking;
                        _ = CheckModelAvailabilityAsync();
                    }
                    else if (Settings.Sizes.Count > 0 && Settings.SelectedSizeIndex != 0)
                    {
                        Settings.SelectedSizeIndex = 0;
                    }

                    EnsureAiScaleWithinRange();
                    OnPropertyChanged(nameof(ShowAiSizeDescriptions));
                    OnPropertyChanged(nameof(ShowModelDownloadPrompt));
                    OnPropertyChanged(nameof(ShowAiControls));
                    OnPropertyChanged(nameof(AiScaleDisplay));
                    OnPropertyChanged(nameof(AiScaleDescription));
                    OnPropertyChanged(nameof(AiSuperResolutionScale));
                    OnPropertyChanged(nameof(CanResize));
                    UpdateAiDetails();

                    // Trigger CanExecuteChanged for ResizeCommand when AI setting changes
                    if (ResizeCommand is RelayCommand resizeCommand)
                    {
                        resizeCommand.OnCanExecuteChanged();
                    }

                    break;

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
            var currentValue = hasConcreteSize
                ? FormatDimensions(_originalWidth!.Value, _originalHeight!.Value)
                : Resources.Input_AiUnknownSize;
            CurrentResolutionDescription = FormatLabeledSize(Resources.Input_AiCurrentLabel, currentValue);

            var scale = Settings.AiSize.Scale;
            var newValue = hasConcreteSize
                ? FormatDimensions((long)_originalWidth!.Value * scale, (long)_originalHeight!.Value * scale)
                : Resources.Input_AiUnknownSize;
            NewResolutionDescription = FormatLabeledSize(Resources.Input_AiNewLabel, newValue);
        }

        private static string FormatDimensions(long width, long height)
        {
            return string.Format(CultureInfo.CurrentCulture, "{0} × {1}", width, height);
        }

        private static string FormatLabeledSize(string label, string value)
        {
            return string.Format(CultureInfo.CurrentCulture, "{0}: {1}", label, value);
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
        /// Initializes AI support state based on system architecture.
        /// If architecture doesn't support AI, sets state to NotSupported.
        /// If architecture supports AI, checks model availability.
        /// </summary>
        private async Task InitializeAiSupportAsync()
        {
            if (Settings?.IsAiArchitectureSupported != true)
            {
                // Architecture doesn't support AI - set to NotSupported immediately
                SetAiState(AiFeatureState.NotSupported, Resources.Input_AiModelNotSupported);
                return;
            }

            // Architecture supports AI - check if model is available
            _aiFeatureState = AiFeatureState.Unknown;
            ModelStatusMessage = Resources.Input_AiModelChecking;
            await CheckModelAvailabilityAsync();
        }

        private async Task CheckModelAvailabilityAsync()
        {
            try
            {
                // Check Windows AI service state
                // Architecture check is now done in Settings.IsAiArchitectureSupported
                // Following the pattern from sample project (Sample.xaml.cs:31-52)
                var readyState = WinAiSuperResolutionService.GetModelReadyState();

                // Map AI service state to our internal state
                switch (readyState)
                {
                    case Microsoft.Windows.AI.AIFeatureReadyState.Ready:
                        // AI is fully supported and model is ready
                        SetAiState(AiFeatureState.Ready, string.Empty);
                        await InitializeAiServiceAsync();
                        break;

                    case Microsoft.Windows.AI.AIFeatureReadyState.NotReady:
                        // AI is supported but model needs to be downloaded
                        SetAiState(AiFeatureState.ModelNotReady, Resources.Input_AiModelNotAvailable);
                        break;

                    case Microsoft.Windows.AI.AIFeatureReadyState.DisabledByUser:
                        // User disabled AI features in system settings
                        SetAiState(AiFeatureState.NotSupported, Resources.Input_AiModelDisabledByUser);
                        break;

                    default:
                        // AI not supported on this system or unknown state
                        SetAiState(AiFeatureState.NotSupported, Resources.Input_AiModelNotSupported);
                        break;
                }
            }
            catch (Exception)
            {
                // Failed to check AI state - treat as not supported
                SetAiState(AiFeatureState.NotSupported, Resources.Input_AiModelNotSupported);
            }
        }

        private void SetAiState(AiFeatureState newState, string statusMessage)
        {
            _aiFeatureState = newState;
            ModelStatusMessage = statusMessage;

            // Notify UI of all related property changes
            NotifyAiStateChanged();
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
            OnPropertyChanged(nameof(AiScaleDescription));
            UpdateAiDetails();
        }

        private async Task InitializeAiServiceAsync()
        {
            try
            {
                // Create service instance if not already created
                if (_aiSuperResolutionService == null)
                {
                    _aiSuperResolutionService = new WinAiSuperResolutionService();
                }

                // Initialize ImageScaler on UI thread (async method)
                var success = await _aiSuperResolutionService.InitializeAsync();

                if (success)
                {
                    // Set the initialized service to ResizeBatch
                    ResizeBatch.SetAiSuperResolutionService(_aiSuperResolutionService);
                }
                else
                {
                    // Failed to initialize, use NoOp service
                    ResizeBatch.SetAiSuperResolutionService(NoOpAiSuperResolutionService.Instance);
                }
            }
            catch (Exception)
            {
                // Failed to initialize, use NoOp service
                ResizeBatch.SetAiSuperResolutionService(NoOpAiSuperResolutionService.Instance);
            }
        }

        private async Task DownloadModelAsync()
        {
            try
            {
                // Set state to downloading
                SetAiState(AiFeatureState.ModelDownloading, Resources.Input_AiModelDownloading);
                ModelDownloadProgress = 0;

                // Call EnsureReadyAsync to download and prepare the AI model
                // This is safe because we only show download button when state is ModelNotReady
                // Following sample project pattern (Sample.xaml.cs:36)
                var result = await WinAiSuperResolutionService.EnsureModelReadyAsync();

                if (result?.Status == Microsoft.Windows.AI.AIFeatureReadyResultState.Success)
                {
                    // Model successfully downloaded and ready
                    SetAiState(AiFeatureState.Ready, string.Empty);

                    // Initialize the AI service for actual use
                    await InitializeAiServiceAsync();
                }
                else
                {
                    // Download failed - revert to not ready state
                    SetAiState(AiFeatureState.ModelNotReady, Resources.Input_AiModelDownloadFailed);
                }
            }
            catch (Exception)
            {
                // Exception during download - revert to not ready state
                SetAiState(AiFeatureState.ModelNotReady, Resources.Input_AiModelDownloadFailed);
            }
            finally
            {
                ModelDownloadProgress = 0;
            }
        }
    }
}
