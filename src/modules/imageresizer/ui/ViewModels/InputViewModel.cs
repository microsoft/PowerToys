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

            // Check AI availability on startup if user had enabled it previously
            // CheckModelAvailabilityAsync will determine if AI is actually supported on this system
            if (settings?.UseAiSuperResolution == true)
            {
                // Set initial checking state
                _aiFeatureState = AiFeatureState.Unknown;
                ModelStatusMessage = Resources.Input_AiModelChecking;

                // Start async check - this will update state and UI
                _ = CheckModelAvailabilityAsync();
            }
            else
            {
                // Ensure initial UI state is properly notified
                NotifyAiPropertiesChanged();
            }
        }

        public Settings Settings { get; }

        public IEnumerable<ResizeFit> ResizeFitValues => Enum.GetValues<ResizeFit>();

        public IEnumerable<ResizeUnit> ResizeUnitValues => Enum.GetValues<ResizeUnit>();

        public int AiSuperResolutionScale
        {
            get => Settings?.AiSuperResolutionScale ?? 1;
            set
            {
                if (Settings == null || value == Settings.AiSuperResolutionScale)
                {
                    return;
                }

                Settings.AiSuperResolutionScale = value;
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

        /// <summary>
        /// Gets a value indicating whether AI feature is available on this system.
        /// True if the system supports AI (ARM64, not disabled by policy, etc.).
        /// This does NOT check if the model is downloaded or if the user enabled it.
        /// </summary>
        public bool IsAiFeatureAvailable =>
            _aiFeatureState != AiFeatureState.NotSupported &&
            _aiFeatureState != AiFeatureState.Unknown;

        /// <summary>
        /// Gets a value indicating whether the user has enabled AI feature.
        /// True if AI is available AND user checked the UseAiSuperResolution setting.
        /// This does NOT check if the model is downloaded.
        /// </summary>
        public bool IsAiFeatureEnabled =>
            IsAiFeatureAvailable &&
            Settings?.UseAiSuperResolution == true;

        /// <summary>
        /// Gets a value indicating whether AI feature is fully ready to use.
        /// True if AI is enabled AND the model is downloaded and ready.
        /// This is the final check before actually using AI for transformation.
        /// </summary>
        public bool IsAiFeatureReady =>
            IsAiFeatureEnabled &&
            _aiFeatureState == AiFeatureState.Ready;

        // ==================== UI State Properties ====================

        // Show AI size descriptions only when AI is enabled and not multiple files
        public bool ShowAiSizeDescriptions => IsAiFeatureEnabled && !_hasMultipleFiles;

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

        // Only show AI checkbox if the system supports AI features
        public bool ShowEnableAiCheckBox => IsAiFeatureAvailable;

        // Show download prompt when: AI is enabled but model is not ready (including downloading)
        public bool ShowModelDownloadPrompt =>
            IsAiFeatureEnabled &&
            (_aiFeatureState == AiFeatureState.ModelNotReady || _aiFeatureState == AiFeatureState.ModelDownloading);

        // Show AI controls only when: AI is fully ready
        public bool ShowAiControls => IsAiFeatureReady;

        // Show traditional size selector when: not showing AI controls and not showing download prompt
        public bool ShowTraditionalSizeSelector
        {
            get
            {
                var result = !ShowAiControls && !ShowModelDownloadPrompt;

                // Debug: log the calculation
                System.Diagnostics.Debug.WriteLine($"ShowTraditionalSizeSelector: {result} (ShowAiControls={ShowAiControls}, ShowModelDownloadPrompt={ShowModelDownloadPrompt}, _aiFeatureState={_aiFeatureState})");
                return result;
            }
        }

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
                // Special case: If state is Unknown and user enabled AI, wait for check to complete
                if (_aiFeatureState == AiFeatureState.Unknown && Settings?.UseAiSuperResolution == true)
                {
                    return false;
                }

                // If user enabled AI (and state is known), we must wait until the model is ready
                if (IsAiFeatureEnabled)
                {
                    return IsAiFeatureReady;
                }

                // If AI is not enabled (either not available or user didn't check it), we can always resize
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
                        if (Settings.AiSuperResolutionScale != 2)
                        {
                            Settings.AiSuperResolutionScale = 2;
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
                    OnPropertyChanged(nameof(IsAiFeatureEnabled));
                    OnPropertyChanged(nameof(IsAiFeatureReady));
                    OnPropertyChanged(nameof(ShowAiSizeDescriptions));
                    OnPropertyChanged(nameof(ShowModelDownloadPrompt));
                    OnPropertyChanged(nameof(ShowAiControls));
                    OnPropertyChanged(nameof(ShowTraditionalSizeSelector));
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

                case nameof(Settings.AiSuperResolutionScale):
                    EnsureAiScaleWithinRange();
                    OnPropertyChanged(nameof(AiScaleDisplay));
                    OnPropertyChanged(nameof(AiScaleDescription));
                    OnPropertyChanged(nameof(AiSuperResolutionScale));
                    UpdateAiDetails();
                    break;

                case nameof(Settings.SelectedSizeIndex):
                case nameof(Settings.SelectedSize):
                    if (!Settings.UseAiSuperResolution)
                    {
                        UpdateAiDetails();
                    }

                    break;
            }
        }

        private void EnsureAiScaleWithinRange()
        {
            if (Settings == null)
            {
                return;
            }

            if (Settings.AiSuperResolutionScale < 1 || Settings.AiSuperResolutionScale > 8)
            {
                Settings.AiSuperResolutionScale = 2;
            }
        }

        private void UpdateAiDetails()
        {
            // Clear AI details if AI not enabled
            if (Settings == null || !IsAiFeatureEnabled)
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

            var scale = Settings.AiSuperResolutionScale;
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
            catch (IOException)
            {
                _originalWidth = null;
                _originalHeight = null;
            }
            catch (NotSupportedException)
            {
                _originalWidth = null;
                _originalHeight = null;
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

        private async Task CheckModelAvailabilityAsync()
        {
            try
            {
                var readyState = WinAiSuperResolutionService.GetModelReadyState();

                SetAiState(AiFeatureState.Ready, string.Empty);
                await InitializeAiServiceAsync();
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
            NotifyAiPropertiesChanged();
        }

        private void NotifyAiPropertiesChanged()
        {
            OnPropertyChanged(nameof(IsAiFeatureAvailable));
            OnPropertyChanged(nameof(IsAiFeatureEnabled));
            OnPropertyChanged(nameof(IsAiFeatureReady));
            OnPropertyChanged(nameof(IsModelDownloading));
            OnPropertyChanged(nameof(ShowEnableAiCheckBox));
            OnPropertyChanged(nameof(ShowModelDownloadPrompt));
            OnPropertyChanged(nameof(ShowAiControls));
            OnPropertyChanged(nameof(ShowTraditionalSizeSelector));
            OnPropertyChanged(nameof(ShowAiSizeDescriptions));
            OnPropertyChanged(nameof(CanResize));

            // Trigger CanExecuteChanged for ResizeCommand
            if (ResizeCommand is RelayCommand resizeCommand)
            {
                resizeCommand.OnCanExecuteChanged();
            }
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
